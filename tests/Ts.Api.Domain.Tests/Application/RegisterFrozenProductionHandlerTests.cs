using Ts.Api.Application.Common;
using Ts.Api.Application.FrozenStock;
using Ts.Api.Domain.FrozenStock;

namespace Ts.Api.Domain.Tests.Application;

public sealed class RegisterFrozenProductionHandlerTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_ReusesResultForSameIdempotentRequest()
    {
        var configuration = CreateConfiguration();
        var store = new FrozenProductionStoreFake(configuration);
        var handler = new RegisterFrozenProductionHandler(
            store,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero)),
            new OrganizationContextFake(OrganizationId));
        var command = new RegisterFrozenProductionCommand(
            configuration.Id,
            new DateOnly(2026, 9, 4),
            24,
            Guid.NewGuid(),
            "entry-01");

        var first = await handler.HandleAsync(command, CancellationToken.None);
        var retry = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(first, retry);
        Assert.Equal(1, store.AddCount);
    }

    [Fact]
    public async Task HandleAsync_RejectsIdempotencyKeyReusedWithDifferentPayload()
    {
        var configuration = CreateConfiguration();
        var store = new FrozenProductionStoreFake(configuration);
        var handler = new RegisterFrozenProductionHandler(
            store,
            new FixedTimeProvider(DateTimeOffset.UtcNow),
            new OrganizationContextFake(OrganizationId));
        var actorId = Guid.NewGuid();

        _ = await handler.HandleAsync(
            new RegisterFrozenProductionCommand(
                configuration.Id,
                new DateOnly(2026, 9, 4),
                24,
                actorId,
                "entry-02"),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            new RegisterFrozenProductionCommand(
                configuration.Id,
                new DateOnly(2026, 9, 4),
                25,
                actorId,
                "entry-02"),
            CancellationToken.None));
    }

    private static FrozenConfiguration CreateConfiguration() => FrozenConfiguration.Create(
        OrganizationId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        "300 g",
        300,
        MeasurementUnit.Gram,
        24.90m);

    private sealed class FrozenProductionStoreFake(FrozenConfiguration configuration) : IFrozenProductionStore
    {
        private readonly Dictionary<string, FrozenLot> _lots = [];

        public int AddCount { get; private set; }

        public Task<FrozenConfiguration?> FindActiveConfigurationAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(configuration.Id == id && configuration.IsActive ? configuration : null);

        public Task<Ts.Api.Domain.Production.ProducibleItem?> FindProducibleItemAsync(
            Guid id,
            CancellationToken cancellationToken) => Task.FromResult<Ts.Api.Domain.Production.ProducibleItem?>(
                id == configuration.ProducibleItemId
                    ? Ts.Api.Domain.Production.ProducibleItem.Create(OrganizationId, "Estrogonofe")
                    : null);

        public Task<FrozenLot?> FindByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(_lots.GetValueOrDefault(idempotencyKey));

        public Task<FrozenLot> AddOrGetByIdempotencyKeyAsync(
            FrozenLot lot,
            CancellationToken cancellationToken)
        {
            AddCount++;
            _lots.TryAdd(lot.IdempotencyKey, lot);
            return Task.FromResult(_lots[lot.IdempotencyKey]);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class OrganizationContextFake(Guid organizationId) : IOrganizationContext
    {
        public bool IsAvailable => true;
        public Guid OrganizationId { get; } = organizationId;
    }
}
