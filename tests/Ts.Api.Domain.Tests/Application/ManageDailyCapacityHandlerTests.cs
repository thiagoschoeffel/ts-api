using Ts.Api.Application.Common;
using Ts.Api.Application.Orders;
using Ts.Api.Domain.Orders;

namespace Ts.Api.Domain.Tests.Application;

public sealed class ManageDailyCapacityHandlerTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    [Fact]
    public async Task Configure_CreatesUpdatesAndReturnsCoherentBalance()
    {
        var store = new CapacityStoreFake();
        var handler = new ConfigureDailyCapacityHandler(store, new OrganizationContextFake());
        var date = new DateOnly(2026, 9, 5);

        var created = await handler.HandleAsync(
            new ConfigureDailyCapacityCommand(date, 10, 0, "capacity-create"),
            CancellationToken.None);
        store.Capacity!.Reserve(3);
        var updated = await handler.HandleAsync(
            new ConfigureDailyCapacityCommand(date, 12, 1, "capacity-update"),
            CancellationToken.None);
        var retry = await handler.HandleAsync(
            new ConfigureDailyCapacityCommand(date, 12, 0, "capacity-update"),
            CancellationToken.None);

        Assert.Equal(10, created.AvailableUnits);
        Assert.Equal(12, updated.TotalUnits);
        Assert.Equal(3, updated.ReservedUnits);
        Assert.Equal(9, updated.AvailableUnits);
        Assert.Equal(2, updated.Version);
        Assert.Equal(updated, retry);
        Assert.Equal(2, store.SaveCount);
    }

    [Fact]
    public async Task Configure_RejectsCapacityBelowAlreadyReservedUnits()
    {
        var capacity = DailyCapacity.Create(
            OrganizationId,
            new DateOnly(2026, 9, 5),
            10,
            "capacity-original");
        capacity.Reserve(6);
        var store = new CapacityStoreFake { Capacity = capacity };
        var handler = new ConfigureDailyCapacityHandler(store, new OrganizationContextFake());

        var exception = await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            new ConfigureDailyCapacityCommand(capacity.OperationalDate, 5, 1, "capacity-invalid"),
            CancellationToken.None));

        Assert.Equal(
            "A capacidade total não pode ser menor que a capacidade já reservada.",
            exception.Message);
        Assert.Equal(10, capacity.TotalUnits);
    }

    private sealed class OrganizationContextFake : IOrganizationContext
    {
        public bool IsAvailable => true;
        public Guid OrganizationId => ManageDailyCapacityHandlerTests.OrganizationId;
    }

    private sealed class CapacityStoreFake : IDailyCapacityManagementStore
    {
        public DailyCapacity? Capacity { get; set; }
        public int SaveCount { get; private set; }

        public async Task<T> ExecuteSerializableAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken) => await operation(cancellationToken);

        public Task<DailyCapacity?> FindAsync(DateOnly date, CancellationToken cancellationToken) =>
            Task.FromResult(Capacity?.OperationalDate == date ? Capacity : null);

        public Task<DailyCapacity?> FindByConfigurationKeyAsync(
            string key,
            CancellationToken cancellationToken) => Task.FromResult(
            Capacity?.LastConfigurationIdempotencyKey == key ? Capacity : null);

        public Task AddAsync(DailyCapacity capacity, CancellationToken cancellationToken)
        {
            Capacity = capacity;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
