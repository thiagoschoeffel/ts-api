using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Ts.Api.Application.Common;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Domain.Tests.Persistence;

public sealed class FrozenStockManagementPersistenceTests
{
    [Fact]
    public async Task ManualMovement_IsPersistedWithIdempotencyAndTrustedAudit()
    {
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
            .Options;
        var request = new RequestContextFake(organizationId, actorId, "corr-frozen-adjustment");

        await using var database = new AppDbContext(options, request, request, new FixedTimeProvider(now));
        var lot = FrozenLot.RegisterProduction(
            organizationId, Guid.NewGuid(), new DateOnly(2026, 9, 4), 10,
            actorId, now.AddDays(-1), "production-persistence");
        database.FrozenLots.Add(lot);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var persisted = await database.FrozenLots.Include("_movements").SingleAsync();
        persisted.AdjustStock(-2, actorId, now, "Conferência física", "adjustment-persistence");
        var store = new FrozenStockManagementStore(database);
        await store.SaveChangesAsync(CancellationToken.None);
        database.ChangeTracker.Clear();

        var movement = await database.FrozenStockMovements
            .SingleAsync(item => item.IdempotencyKey == "adjustment-persistence");
        var audit = await database.AuditEvents
            .SingleAsync(item => item.Action == "FrozenStock.ManualAdjustment");
        Assert.Equal(-2, movement.SignedQuantity);
        Assert.Equal(actorId, audit.ActorId);
        Assert.Equal(lot.Id, audit.ResourceId);
        Assert.Equal("corr-frozen-adjustment", audit.CorrelationId);
    }

    private sealed class RequestContextFake(Guid organizationId, Guid userId, string correlationId)
        : IOrganizationContext, ICurrentUserContext
    {
        public bool IsAvailable => true;
        public Guid OrganizationId { get; } = organizationId;
        public Guid UserId { get; } = userId;
        public string CorrelationId { get; } = correlationId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
