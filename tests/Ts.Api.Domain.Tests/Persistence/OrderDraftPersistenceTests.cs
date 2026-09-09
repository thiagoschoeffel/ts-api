using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Orders;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Domain.Tests.Persistence;

public sealed class OrderDraftPersistenceTests
{
    [Fact]
    public async Task SaveChanges_ReplacesDraftItemsAndPersistsVersion()
    {
        var organizationId = Guid.NewGuid();
        var databaseRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), databaseRoot)
            .Options;
        var organizationContext = new OrganizationContextFake(organizationId);
        var offer = CatalogOffer.Create(
            organizationId,
            "Almoço",
            OfferFulfillmentMode.DailyProduction);
        var order = Order.CreateDraft(
            organizationId,
            Guid.NewGuid(),
            new DateOnly(2026, 9, 5),
            [new OrderItemDefinition(offer.Id, offer.FulfillmentMode, 1, 18m, OfferName: offer.Name)],
            "persistence-create");

        await using var context = new AppDbContext(options, organizationContext);
        context.CatalogOffers.Add(offer);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var previousItems = order.Items.ToArray();
        order.EditDraft(
            order.CustomerId,
            order.OperationalDate,
            [new OrderItemDefinition(offer.Id, offer.FulfillmentMode, 3, 20m, OfferName: offer.Name)],
            "persistence-edit",
            financialTerms: new OrderFinancialTermsDefinition(
                "deferred", "pix", new DateOnly(2026, 9, 12), 4.50m, 2m, "Fidelidade"));
        var store = new OrderManagementStore(context);
        store.ReplaceItems(previousItems, order.Items);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var updated = await context.Orders.Include("_items").SingleAsync();
        Assert.Equal(1, updated.Version);
        var item = Assert.Single(updated.Items);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(20m, item.UnitPrice);
        Assert.Equal("deferred", updated.PaymentCondition);
        Assert.Equal("pix", updated.PaymentMethod);
        Assert.Equal(new DateOnly(2026, 9, 12), updated.PaymentDueDate);
        Assert.Equal(4.50m, updated.DraftDeliveryFee);
        Assert.Equal(2m, updated.DraftDiscountAmount);
        Assert.Equal("Fidelidade", updated.DraftDiscountReason);
    }

    private sealed class OrganizationContextFake(Guid organizationId) : IOrganizationContext
    {
        public bool IsAvailable => true;
        public Guid OrganizationId { get; } = organizationId;
    }
}
