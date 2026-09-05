using Ts.Api.Application.Common;
using Ts.Api.Application.Orders;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Customers;
using Ts.Api.Domain.Finance;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Plans;
using Ts.Api.Domain.Production;

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

    [Fact]
    public async Task HandleAsync_ConsumesCompatiblePlanCreditsByFifoAndConsolidatesFinancialEffects()
    {
        var offerId = Guid.NewGuid();
        var producibleItemId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var order = Order.CreateDraft(OrganizationId, customerId, new DateOnly(2026, 9, 5),
            [new OrderItemDefinition(offerId, OfferFulfillmentMode.DailyProduction, 2, 20m,
                ProducibleItemId: producibleItemId, OfferName: "Prato", ProducibleItemName: "Frango")]);
        var composition = ProducibleComposition.Publish(OrganizationId, producibleItemId, 1, Now,
            [new ProducibleComponentDefinition("Frango", 150m, "g", [])]);
        var oldest = PlanAcquisition.Create(OrganizationId, customerId, offerId, "Plano antigo", 1, 10m,
            new DateOnly(2026, 7, 1), Now.AddMonths(-2));
        var newest = PlanAcquisition.Create(OrganizationId, customerId, offerId, "Plano novo", 2, 15m,
            new DateOnly(2026, 8, 1), Now.AddMonths(-1));
        var financialMovements = new List<FinancialCreditMovement>
        {
            FinancialCreditMovement.Grant(OrganizationId, customerId, 20m, "Crédito", ActorId, Now.AddDays(-1)),
        };
        var store = new OrderConfirmationStoreFake(order,
            DailyCapacity.Create(OrganizationId, order.OperationalDate, 10), [], [composition], [],
            [oldest, newest], financialMovements);
        var handler = new ConfirmOrderHandler(store, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new ConfirmOrderCommand(
            order.Id, ActorId, "commercial-effects", 0,
            [new PlanCreditRequest(order.Items.Single().Id, 2)],
            DiscountAmount: 5m, DiscountReason: "Cortesia", DeliveryFee: 7m, FinancialCreditAmount: 10m),
            CancellationToken.None);

        Assert.Equal(0, oldest.Balance);
        Assert.Equal(1, newest.Balance);
        Assert.Equal(25m, result.Financial.PlanCreditCoveredAmount);
        Assert.Equal(7m, result.Financial.AmountDue);
        Assert.Equal(2, result.PlanCredits.Count);
        Assert.Single(result.Components);
        Assert.Equal(300m, result.Components.Single().TotalQuantity);
        Assert.Equal(7m, order.Charges.Single().Amount);
        Assert.Equal(10m, financialMovements.Sum(item => item.SignedAmount));
        Assert.Single(order.ConfirmationAudits);
    }

    [Fact]
    public async Task HandleAsync_RejectsCompositionIncompatibleWithCustomerRestriction()
    {
        var producibleItemId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var order = Order.CreateDraft(OrganizationId, customerId, new DateOnly(2026, 9, 5),
            [new OrderItemDefinition(Guid.NewGuid(), OfferFulfillmentMode.DailyProduction, 1, 20m,
                ProducibleItemId: producibleItemId, OfferName: "Massa")]);
        var composition = ProducibleComposition.Publish(OrganizationId, producibleItemId, 1, Now,
            [new ProducibleComponentDefinition("Molho branco", 100m, "g", ["lactose"])]);
        var restriction = CustomerDietaryRestriction.Create(OrganizationId, customerId, "LACTOSE");
        var store = new OrderConfirmationStoreFake(order,
            DailyCapacity.Create(OrganizationId, order.OperationalDate, 10), [], [composition], [restriction]);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => new ConfirmOrderHandler(
            store, new FixedTimeProvider(Now)).HandleAsync(
                new ConfirmOrderCommand(order.Id, ActorId, "restriction", 0), CancellationToken.None));

        Assert.Contains("LACTOSE", exception.Message);
        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Empty(order.ComponentSnapshots);
        Assert.Equal(0, store.SaveCount);
        Assert.True(store.TransactionRolledBack);
    }

    [Fact]
    public async Task HandleAsync_RejectsInsufficientCompatiblePlanCredit()
    {
        var offerId = Guid.NewGuid();
        var order = Order.CreateDraft(OrganizationId, Guid.NewGuid(), new DateOnly(2026, 9, 5),
            [new OrderItemDefinition(offerId, OfferFulfillmentMode.DailyProduction, 2, 20m)]);
        var acquisition = PlanAcquisition.Create(OrganizationId, order.CustomerId, offerId, "Plano", 1, 20m,
            new DateOnly(2026, 8, 1), Now.AddMonths(-1));
        var store = new OrderConfirmationStoreFake(order,
            DailyCapacity.Create(OrganizationId, order.OperationalDate, 10), [], acquisitions: [acquisition]);

        await Assert.ThrowsAsync<ConflictException>(() => new ConfirmOrderHandler(store, new FixedTimeProvider(Now))
            .HandleAsync(new ConfirmOrderCommand(order.Id, ActorId, "insufficient-plan", 0,
                [new PlanCreditRequest(order.Items.Single().Id, 2)]), CancellationToken.None));

        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(0, store.SaveCount);
        Assert.True(store.TransactionRolledBack);
    }

    [Fact]
    public async Task HandleAsync_IdempotentReplayPreservesHistoricalComponentSnapshot()
    {
        var producibleItemId = Guid.NewGuid();
        var order = Order.CreateDraft(OrganizationId, Guid.NewGuid(), new DateOnly(2026, 9, 5),
            [new OrderItemDefinition(Guid.NewGuid(), OfferFulfillmentMode.DailyProduction, 1, 20m,
                ProducibleItemId: producibleItemId, OfferName: "Prato")]);
        var compositions = new List<ProducibleComposition>
        {
            ProducibleComposition.Publish(OrganizationId, producibleItemId, 1, Now,
                [new ProducibleComponentDefinition("Arroz", 100m, "g", [])]),
        };
        var store = new OrderConfirmationStoreFake(order,
            DailyCapacity.Create(OrganizationId, order.OperationalDate, 10), [], compositions);
        var handler = new ConfirmOrderHandler(store, new FixedTimeProvider(Now));
        var command = new ConfirmOrderCommand(order.Id, ActorId, "historical", 0);

        var first = await handler.HandleAsync(command, CancellationToken.None);
        compositions.Add(ProducibleComposition.Publish(OrganizationId, producibleItemId, 2, Now.AddDays(1),
            [new ProducibleComponentDefinition("Legumes", 120m, "g", [])]));
        var replay = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("Arroz", first.Components.Single().Name);
        Assert.Equal("Arroz", replay.Components.Single().Name);
        Assert.Equal(1, replay.Components.Single().CompositionVersion);
        Assert.Equal(1, store.SaveCount);
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
        IReadOnlyCollection<FrozenLot> lots,
        IReadOnlyCollection<ProducibleComposition>? compositions = null,
        IReadOnlyCollection<CustomerDietaryRestriction>? restrictions = null,
        IReadOnlyCollection<PlanAcquisition>? acquisitions = null,
        IReadOnlyCollection<FinancialCreditMovement>? financialMovements = null) : IOrderConfirmationStore
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

        public Task<ProducibleComposition?> FindPublishedCompositionAsync(
            Guid producibleItemId,
            CancellationToken cancellationToken) => Task.FromResult(
            compositions?.Where(item => item.ProducibleItemId == producibleItemId)
                .OrderByDescending(item => item.Version).FirstOrDefault());

        public Task<IReadOnlyList<CustomerDietaryRestriction>> FindCustomerRestrictionsAsync(
            Guid customerId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CustomerDietaryRestriction>>(
            (restrictions ?? []).Where(item => item.CustomerId == customerId).ToArray());

        public Task<IReadOnlyList<PlanAcquisition>> FindEligiblePlanAcquisitionsAsync(
            Guid customerId,
            Guid offerId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlanAcquisition>>(
            (acquisitions ?? []).Where(item => item.CustomerId == customerId && item.EligibleOfferId == offerId)
                .OrderBy(item => item.AcquiredOn).ThenBy(item => item.Id).ToArray());

        public Task<decimal> GetFinancialCreditBalanceAsync(
            Guid customerId,
            CancellationToken cancellationToken) => Task.FromResult(
            (financialMovements ?? []).Where(item => item.CustomerId == customerId).Sum(item => item.SignedAmount));

        public void AddFinancialCreditMovement(FinancialCreditMovement movement)
        {
            if (financialMovements is List<FinancialCreditMovement> mutable)
                mutable.Add(movement);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
