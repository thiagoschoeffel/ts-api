using Ts.Api.Application.Common;
using Ts.Api.Application.FrozenStock;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Production;

namespace Ts.Api.Domain.Tests.Application;

public sealed class RegisterFrozenMovementHandlerTests
{
    [Fact]
    public async Task HandleAsync_IsIdempotentAndDoesNotDuplicateBalanceEffects()
    {
        var actorId = Guid.NewGuid();
        var lot = FrozenLot.RegisterProduction(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 4), 10,
            actorId, DateTimeOffset.UtcNow, "production-management");
        var store = new StoreFake(lot);
        var handler = new RegisterFrozenMovementHandler(
            store, new FixedTimeProvider(new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero)));
        var command = new RegisterFrozenMovementCommand(
            lot.Id, StockMovementType.ManualAdjustment, -2, "Conferência", actorId, "movement-01");

        var first = await handler.HandleAsync(command, CancellationToken.None);
        var retry = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(first, retry);
        Assert.Equal(8, lot.Balance);
        Assert.Equal(2, lot.Movements.Count);
    }

    [Fact]
    public async Task HandleAsync_RejectsAReusedKeyWithDifferentPayload()
    {
        var actorId = Guid.NewGuid();
        var lot = FrozenLot.RegisterProduction(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 4), 10,
            actorId, DateTimeOffset.UtcNow, "production-conflict");
        var store = new StoreFake(lot);
        var handler = new RegisterFrozenMovementHandler(store, TimeProvider.System);

        _ = await handler.HandleAsync(new RegisterFrozenMovementCommand(
            lot.Id, StockMovementType.ExpirationDisposal, 2, "Vencido", actorId, "movement-02"),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            new RegisterFrozenMovementCommand(
                lot.Id, StockMovementType.ExpirationDisposal, 3, "Vencido", actorId, "movement-02"),
            CancellationToken.None));
    }

    private sealed class StoreFake(FrozenLot lot) : IFrozenStockManagementStore
    {
        public Task<IReadOnlyList<CatalogOffer>> GetActiveFrozenOffersAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CatalogOffer>>([]);
        public Task<IReadOnlyList<ProducibleItem>> GetProduciblesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProducibleItem>>([]);
        public Task<IReadOnlyList<FrozenConfiguration>> GetConfigurationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FrozenConfiguration>>([]);
        public Task<IReadOnlyList<FrozenLot>> GetLotsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FrozenLot>>([lot]);
        public Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
        public Task<FrozenConfiguration?> FindConfigurationAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<FrozenConfiguration?>(null);
        public Task<FrozenLot?> FindLotAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(id == lot.Id ? lot : null);
        public Task<FrozenStockMovement?> FindMovementByIdempotencyKeyAsync(
            string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(
                lot.Movements.SingleOrDefault(item => item.IdempotencyKey == idempotencyKey));
        public Task<T> ExecuteSerializableAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
