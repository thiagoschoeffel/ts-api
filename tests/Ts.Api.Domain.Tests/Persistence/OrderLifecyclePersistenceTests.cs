using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Orders;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Domain.Tests.Persistence;

public sealed class OrderLifecyclePersistenceTests
{
    [Fact]
    public async Task SaveChanges_PersistsLifecycleAuditAndChargeCancellation()
    {
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
            .Options;
        var offer = CatalogOffer.Create(organizationId, "Almoço", OfferFulfillmentMode.DailyProduction);
        var order = Order.CreateDraft(organizationId, Guid.NewGuid(), new DateOnly(2026, 9, 6),
            [new OrderItemDefinition(offer.Id, offer.FulfillmentMode, 1, 20m, OfferName: offer.Name)],
            "create-lifecycle-persistence");
        order.Confirm(actorId, now, "confirm-lifecycle-persistence");

        await using var context = new AppDbContext(options, new OrganizationContextFake(organizationId));
        context.CatalogOffers.Add(offer);
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var store = new OrderLifecycleStore(context);
        var persisted = await store.FindOrderAsync(order.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        persisted.TransitionStatus(OrderStatus.InProduction, "Produção iniciada", actorId, now.AddHours(1), "transition-persistence");
        persisted.Charges.Single().Cancel(actorId, now.AddHours(1));
        await store.SaveChangesAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await store.FindOrderAsync(order.Id, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal(OrderStatus.InProduction, reloaded.Status);
        Assert.Equal("transition-persistence", Assert.Single(reloaded.LifecycleEvents).IdempotencyKey);
        Assert.Equal(OrderChargeStatus.Cancelled, Assert.Single(reloaded.Charges).Status);
    }

    private sealed class OrganizationContextFake(Guid organizationId) : IOrganizationContext
    {
        public bool IsAvailable => true;
        public Guid OrganizationId { get; } = organizationId;
    }
}
