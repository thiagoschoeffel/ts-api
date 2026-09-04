using Ts.Api.Application.Common;
using Ts.Api.Application.Orders;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Orders;

namespace Ts.Api.Domain.Tests.Application;

public sealed class ConfirmOrderHandlerTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_ConfirmsMixedOrderAndAllocatesFrozenStockByFefo()
    {
        var frozenConfigurationId = Guid.NewGuid();
        var order = CreateMixedOrder(frozenConfigurationId);
        var firstLot = CreateLot(frozenConfigurationId, new DateOnly(2026, 8, 1), 2);
        var secondLot = CreateLot(frozenConfigurationId, new DateOnly(2026, 8, 10), 5);
        var capacity = DailyCapacity.Create(OrganizationId, order.OperationalDate, 3);
        var store = new OrderConfirmationStoreFake(order, capacity, [secondLot, firstLot]);
        var handler = new ConfirmOrderHandler(store, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(
            new ConfirmOrderCommand(order.Id, ActorId, "confirm-001", 0),
            CancellationToken.None);

        Assert.Equal(OrderStatus.Confirmed, result.Status);
        Assert.Equal(1, result.Version);
        Assert.Equal(2, capacity.ReservedUnits);
        Assert.Equal(0, firstLot.Balance);
        Assert.Equal(3, secondLot.Balance);
        Assert.Equal(2, result.FrozenAllocations.Count);
        Assert.Collection(
            result.FrozenAllocations,
            allocation =>
            {
                Assert.Equal(firstLot.Id, allocation.FrozenLotId);
                Assert.Equal(2, allocation.Quantity);
            },
            allocation =>
            {
                Assert.Equal(secondLot.Id, allocation.FrozenLotId);
                Assert.Equal(2, allocation.Quantity);
            });
        Assert.Single(order.Charges);
        Assert.Equal(70m, order.Charges.Single().Amount);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task HandleAsync_RepeatedKeyReturnsPreviousResultWithoutDuplicatingEffects()
    {
        var frozenConfigurationId = Guid.NewGuid();
        var order = CreateMixedOrder(frozenConfigurationId);
        var lot = CreateLot(frozenConfigurationId, new DateOnly(2026, 8, 1), 10);
        var capacity = DailyCapacity.Create(OrganizationId, order.OperationalDate, 10);
        var store = new OrderConfirmationStoreFake(order, capacity, [lot]);
        var handler = new ConfirmOrderHandler(store, new FixedTimeProvider(Now));
        var command = new ConfirmOrderCommand(order.Id, ActorId, "confirm-002", 0);

        var first = await handler.HandleAsync(command, CancellationToken.None);
        var retry = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(first.OrderId, retry.OrderId);
        Assert.Equal(first.Status, retry.Status);
        Assert.Equal(first.Version, retry.Version);
        Assert.Equal(first.FrozenAllocations, retry.FrozenAllocations);
        Assert.Equal(2, capacity.ReservedUnits);
        Assert.Equal(6, lot.Balance);
        Assert.Single(order.Charges);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task HandleAsync_RejectsStaleOrderVersion()
    {
        var order = Order.CreateDraft(
            OrganizationId,
            Guid.NewGuid(),
            new DateOnly(2026, 9, 5),
            [new OrderItemDefinition(Guid.NewGuid(), OfferFulfillmentMode.DailyProduction, 1, 12m)]);
        var capacity = DailyCapacity.Create(OrganizationId, order.OperationalDate, 10);
        var store = new OrderConfirmationStoreFake(order, capacity, []);
        var handler = new ConfirmOrderHandler(store, new FixedTimeProvider(Now));

        var exception = await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            new ConfirmOrderCommand(order.Id, ActorId, "confirm-003", 1),
            CancellationToken.None));

        Assert.Equal("O pedido foi alterado. Recarregue os dados antes de confirmar.", exception.Message);
        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task HandleAsync_RejectsInsufficientDailyCapacityAsConflict()
    {
        var order = Order.CreateDraft(
            OrganizationId,
            Guid.NewGuid(),
            new DateOnly(2026, 9, 5),
            [new OrderItemDefinition(Guid.NewGuid(), OfferFulfillmentMode.DailyProduction, 3, 12m)]);
        var capacity = DailyCapacity.Create(OrganizationId, order.OperationalDate, 2);
        var store = new OrderConfirmationStoreFake(order, capacity, []);
        var handler = new ConfirmOrderHandler(store, new FixedTimeProvider(Now));

        var exception = await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            new ConfirmOrderCommand(order.Id, ActorId, "confirm-capacity", 0),
            CancellationToken.None));

        Assert.Equal(
            "A capacidade diária disponível é insuficiente para confirmar o pedido.",
            exception.Message);
        Assert.Equal(0, capacity.ReservedUnits);
        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.True(store.TransactionRolledBack);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task HandleAsync_RejectsInsufficientSellableFrozenStock()
    {
        var frozenConfigurationId = Guid.NewGuid();
        var order = Order.CreateDraft(
            OrganizationId,
            Guid.NewGuid(),
            new DateOnly(2026, 9, 5),
            [new OrderItemDefinition(
                Guid.NewGuid(),
                OfferFulfillmentMode.FrozenStock,
                3,
                15m,
                frozenConfigurationId)]);
        var expiredLot = CreateLot(frozenConfigurationId, new DateOnly(2026, 5, 1), 5);
        var store = new OrderConfirmationStoreFake(order, null, [expiredLot]);
        var handler = new ConfirmOrderHandler(store, new FixedTimeProvider(Now));

        var exception = await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            new ConfirmOrderCommand(order.Id, ActorId, "confirm-004", 0),
            CancellationToken.None));

        Assert.Equal("O estoque congelado vendável é insuficiente para confirmar o pedido.", exception.Message);
        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.True(store.TransactionRolledBack);
        Assert.Equal(0, store.SaveCount);
    }

    private static Order CreateMixedOrder(Guid frozenConfigurationId) => Order.CreateDraft(
        OrganizationId,
        Guid.NewGuid(),
        new DateOnly(2026, 9, 5),
        [
            new OrderItemDefinition(Guid.NewGuid(), OfferFulfillmentMode.DailyProduction, 2, 5m),
            new OrderItemDefinition(
                Guid.NewGuid(),
                OfferFulfillmentMode.FrozenStock,
                4,
                15m,
                frozenConfigurationId),
        ]);

    private static FrozenLot CreateLot(
        Guid frozenConfigurationId,
        DateOnly manufacturedOn,
        int quantity) => FrozenLot.RegisterProduction(
            OrganizationId,
            frozenConfigurationId,
            manufacturedOn,
            quantity,
            ActorId,
            Now.AddDays(-1),
            Guid.NewGuid().ToString("N"));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class OrderConfirmationStoreFake(
        Order order,
        DailyCapacity? capacity,
        IReadOnlyCollection<FrozenLot> lots) : IOrderConfirmationStore
    {
        public int SaveCount { get; private set; }
        public bool TransactionRolledBack { get; private set; }

        public async Task<T> ExecuteSerializableAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch
            {
                TransactionRolledBack = true;
                throw;
            }
        }

        public Task<Order?> FindByConfirmationKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken) => Task.FromResult<Order?>(
            order.ConfirmationIdempotencyKey == idempotencyKey ? order : null);

        public Task<Order?> FindOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
            Task.FromResult<Order?>(order.Id == orderId ? order : null);

        public Task<DailyCapacity?> FindDailyCapacityAsync(
            DateOnly operationalDate,
            CancellationToken cancellationToken) => Task.FromResult(
            capacity?.OperationalDate == operationalDate ? capacity : null);

        public Task<IReadOnlyList<FrozenLot>> FindSellableLotsAsync(
            Guid frozenConfigurationId,
            DateOnly sellableOn,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<FrozenLot>>(
            lots.Where(item => item.FrozenConfigurationId == frozenConfigurationId
                    && item.IsSellableOn(sellableOn))
                .OrderBy(item => item.ExpiresOn)
                .ThenBy(item => item.ManufacturedOn)
                .ThenBy(item => item.Id)
                .ToArray());

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
