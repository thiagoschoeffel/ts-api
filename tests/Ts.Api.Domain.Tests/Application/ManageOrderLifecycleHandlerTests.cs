using Ts.Api.Application.Common;
using Ts.Api.Application.Orders;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Finance;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Plans;

namespace Ts.Api.Domain.Tests.Application;

public sealed class ManageOrderLifecycleHandlerTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TransitionStatus_EnforcesCompleteOperationalMatrix()
    {
        var statuses = Enum.GetValues<OrderStatus>();
        foreach (var current in statuses)
        foreach (var target in statuses)
        {
            var order = CreateOrderAt(current);
            var allowed = (current, target) is
                (OrderStatus.Confirmed, OrderStatus.InProduction)
                or (OrderStatus.InProduction, OrderStatus.InPacking)
                or (OrderStatus.InPacking, OrderStatus.InDelivery)
                or (OrderStatus.InDelivery, OrderStatus.Completed)
                or (OrderStatus.InDelivery, OrderStatus.DeliveryFailed)
                or (OrderStatus.DeliveryFailed, OrderStatus.InDelivery);

            var action = () => order.TransitionStatus(
                target, "Avanço operacional", ActorId, Now, Guid.NewGuid().ToString("N"));

            if (allowed)
                action();
            else
                Assert.Throws<DomainException>(action);
        }
    }

    [Fact]
    public async Task CancelAsync_BeforeProductionReversesAllEffectsOnce()
    {
        var scenario = CreateConfirmedScenario();
        var store = scenario.Store;
        var handler = new CancelOrderHandler(store, new FixedTimeProvider(Now));
        var command = new CancelOrderCommand(
            scenario.Order.Id, "Cliente desistiu", ActorId, scenario.Order.Version,
            "cancel-before-production", CommercialCancellationDisposition.Reverse,
            FrozenCancellationDisposition.ReturnToStock);

        var first = await handler.HandleAsync(command, CancellationToken.None);
        var replay = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, first.Status);
        Assert.Equal(first, replay);
        Assert.Equal(0, scenario.Capacity.ReservedUnits);
        Assert.Equal(2, scenario.Lot.Balance);
        Assert.Equal(2, scenario.Acquisition.Balance);
        Assert.Equal(OrderChargeStatus.Cancelled, scenario.Order.Charges.Single().Status);
        Assert.Equal(1, first.CapacityUnitsReleased);
        Assert.Equal(1, first.PlanCreditsReversed);
        Assert.Equal(3m, first.FinancialCreditReversed);
        Assert.Equal(1, first.ChargesCancelled);
        Assert.Single(store.FinancialMovements);
        Assert.Single(scenario.Order.LifecycleEvents);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task CancelAsync_AfterSeparationRequiresInspectionToReturnFrozenStock()
    {
        var scenario = CreateConfirmedScenario();
        scenario.Order.TransitionStatus(OrderStatus.InProduction, "Produção iniciada", ActorId, Now, "production");
        var handler = new CancelOrderHandler(scenario.Store, new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new CancelOrderCommand(scenario.Order.Id, "Cancelado", ActorId, scenario.Order.Version,
                "cancel-without-inspection", CommercialCancellationDisposition.Reverse,
                FrozenCancellationDisposition.ReturnToStock),
            CancellationToken.None));

        Assert.Equal(OrderStatus.InProduction, scenario.Order.Status);
        Assert.Equal(1, scenario.Capacity.ReservedUnits);
        Assert.Equal(1, scenario.Lot.Balance);
    }

    [Fact]
    public async Task CancelAsync_AfterProductionKeepsCapacityAndQuarantinesWithoutReturningStock()
    {
        var scenario = CreateConfirmedScenario();
        scenario.Order.TransitionStatus(OrderStatus.InProduction, "Produção iniciada", ActorId, Now, "production-2");
        var result = await new CancelOrderHandler(scenario.Store, new FixedTimeProvider(Now)).HandleAsync(
            new CancelOrderCommand(scenario.Order.Id, "Falha operacional", ActorId, scenario.Order.Version,
                "cancel-quarantine", CommercialCancellationDisposition.Preserve,
                FrozenCancellationDisposition.Quarantine), CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, result.Status);
        Assert.Equal(0, result.CapacityUnitsReleased);
        Assert.Equal(1, scenario.Capacity.ReservedUnits);
        Assert.Equal(1, scenario.Lot.Balance);
        Assert.Equal(FrozenCancellationDisposition.Quarantine, result.FrozenDisposition);
        Assert.Equal(1, scenario.Acquisition.Balance);
        Assert.Equal(OrderChargeStatus.Pending, scenario.Order.Charges.Single().Status);
    }

    [Fact]
    public async Task CancelAsync_AfterSeparationReturnsToSameLotOnlyWithCompleteInspection()
    {
        var scenario = CreateConfirmedScenario();
        scenario.Order.TransitionStatus(OrderStatus.InProduction, "Produção iniciada", ActorId, Now, "production-3");
        var inspection = new FrozenReturnInspection(true, true, true);

        var result = await new CancelOrderHandler(scenario.Store, new FixedTimeProvider(Now)).HandleAsync(
            new CancelOrderCommand(scenario.Order.Id, "Retorno conferido", ActorId, scenario.Order.Version,
                "cancel-inspected-return", CommercialCancellationDisposition.Reverse,
                FrozenCancellationDisposition.ReturnToStock, inspection),
            CancellationToken.None);

        Assert.Equal(2, scenario.Lot.Balance);
        Assert.Equal(inspection, result.FrozenReturnInspection);
        Assert.Equal(1, scenario.Capacity.ReservedUnits);
    }

    [Fact]
    public async Task CancelAsync_AfterDispatchNeverReturnsFrozenStockToSellableBalance()
    {
        var scenario = CreateConfirmedScenario();
        scenario.Order.TransitionStatus(OrderStatus.InProduction, "Produção", ActorId, Now, "s1");
        scenario.Order.TransitionStatus(OrderStatus.InPacking, "Embalagem", ActorId, Now, "s2");
        scenario.Order.TransitionStatus(OrderStatus.InDelivery, "Expedição", ActorId, Now, "s3");
        var handler = new CancelOrderHandler(scenario.Store, new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new CancelOrderCommand(scenario.Order.Id, "Entrega cancelada", ActorId, scenario.Order.Version,
                "cancel-dispatched-return", CommercialCancellationDisposition.Preserve,
                FrozenCancellationDisposition.ReturnToStock,
                new FrozenReturnInspection(true, true, true)),
            CancellationToken.None));

        var result = await handler.HandleAsync(
            new CancelOrderCommand(scenario.Order.Id, "Entrega cancelada", ActorId, scenario.Order.Version,
                "cancel-dispatched-discard", CommercialCancellationDisposition.Preserve,
                FrozenCancellationDisposition.Discarded),
            CancellationToken.None);
        Assert.Equal(FrozenCancellationDisposition.Discarded, result.FrozenDisposition);
        Assert.Equal(1, scenario.Lot.Balance);
    }

    [Fact]
    public async Task RescheduleAsync_AtomicallyTransfersCapacityAndIsIdempotent()
    {
        var scenario = CreateConfirmedScenario();
        var newDate = scenario.Order.OperationalDate.AddDays(1);
        var target = DailyCapacity.Create(OrganizationId, newDate, 5);
        scenario.Store.Capacities.Add(target);
        var command = new RescheduleOrderCommand(
            scenario.Order.Id, newDate, "Solicitação do cliente", ActorId,
            scenario.Order.Version, "reschedule-1");
        var handler = new RescheduleOrderHandler(scenario.Store, new FixedTimeProvider(Now));

        var first = await handler.HandleAsync(command, CancellationToken.None);
        var replay = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(first, replay);
        Assert.Equal(newDate, scenario.Order.OperationalDate);
        Assert.Equal(0, scenario.Capacity.ReservedUnits);
        Assert.Equal(1, target.ReservedUnits);
        Assert.Equal(1, scenario.Store.SaveCount);
    }

    [Fact]
    public async Task RescheduleAsync_RejectsDateWithoutCapacityBeforeChangingAnything()
    {
        var scenario = CreateConfirmedScenario();
        var originalDate = scenario.Order.OperationalDate;

        await Assert.ThrowsAsync<ConflictException>(() => new RescheduleOrderHandler(
            scenario.Store, new FixedTimeProvider(Now)).HandleAsync(
                new RescheduleOrderCommand(scenario.Order.Id, originalDate.AddDays(1), "Mudança",
                    ActorId, scenario.Order.Version, "no-capacity"), CancellationToken.None));

        Assert.Equal(originalDate, scenario.Order.OperationalDate);
        Assert.Equal(1, scenario.Capacity.ReservedUnits);
        Assert.Equal(0, scenario.Store.SaveCount);
    }

    [Fact]
    public async Task TransitionAsync_RejectsStaleVersionAndReplaysSameRequest()
    {
        var scenario = CreateConfirmedScenario();
        var handler = new TransitionOrderStatusHandler(scenario.Store, new FixedTimeProvider(Now));
        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            new TransitionOrderStatusCommand(scenario.Order.Id, OrderStatus.InProduction,
                "Início", ActorId, scenario.Order.Version + 1, "stale"), CancellationToken.None));

        var command = new TransitionOrderStatusCommand(scenario.Order.Id, OrderStatus.InProduction,
            "Início", ActorId, scenario.Order.Version, "transition-1");
        var first = await handler.HandleAsync(command, CancellationToken.None);
        var replay = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(first, replay);
        Assert.Equal(OrderStatus.InProduction, scenario.Order.Status);
        Assert.Equal(1, scenario.Store.SaveCount);
    }

    private static ConfirmedScenario CreateConfirmedScenario()
    {
        var frozenConfigurationId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var order = Order.CreateDraft(OrganizationId, Guid.NewGuid(), new DateOnly(2026, 9, 6),
        [
            new OrderItemDefinition(Guid.NewGuid(), OfferFulfillmentMode.DailyProduction, 1, 10m),
            new OrderItemDefinition(offerId, OfferFulfillmentMode.FrozenStock, 1, 20m, frozenConfigurationId),
        ]);
        var capacity = DailyCapacity.Create(OrganizationId, order.OperationalDate, 5);
        capacity.Reserve(1);
        var lot = FrozenLot.RegisterProduction(OrganizationId, frozenConfigurationId,
            new DateOnly(2026, 9, 1), 2, ActorId, Now.AddDays(-1), "lot");
        var frozenItem = order.Items.Single(item => item.FulfillmentMode == OfferFulfillmentMode.FrozenStock);
        lot.RemoveForOrder(order.Id, frozenItem.Id, 1, ActorId, Now);
        order.AllocateFrozenStock(frozenItem, lot.Id, 1);
        var acquisition = PlanAcquisition.Create(OrganizationId, order.CustomerId, offerId,
            "Plano", 2, 5m, new DateOnly(2026, 8, 1), Now.AddMonths(-1));
        acquisition.Consume(order.Id, frozenItem.Id, 1, ActorId, Now);
        order.AllocatePlanCredit(frozenItem, acquisition.Id, acquisition.PlanName, 1, 5m);
        order.Confirm(ActorId, Now, "confirmed", financialCreditApplied: 3m);

        var store = new LifecycleStoreFake(order, [capacity], [lot], [acquisition]);
        return new ConfirmedScenario(order, capacity, lot, acquisition, store);
    }

    private static Order CreateOrderAt(OrderStatus status)
    {
        var order = Order.CreateDraft(OrganizationId, Guid.NewGuid(), new DateOnly(2026, 9, 6),
            [new OrderItemDefinition(Guid.NewGuid(), OfferFulfillmentMode.DailyProduction, 1, 10m)]);
        if (status == OrderStatus.Open) return order;
        order.Confirm(ActorId, Now, "matrix-confirm");
        if (status == OrderStatus.Confirmed) return order;
        if (status == OrderStatus.Cancelled)
        {
            order.Cancel("Cancelamento", ActorId, Now, "matrix-cancel",
                CommercialCancellationDisposition.Reverse,
                FrozenCancellationDisposition.NotApplicable, null, 0, 0, 0, 1);
            return order;
        }
        order.TransitionStatus(OrderStatus.InProduction, "Produção", ActorId, Now, "matrix-production");
        if (status == OrderStatus.InProduction) return order;
        order.TransitionStatus(OrderStatus.InPacking, "Embalagem", ActorId, Now, "matrix-packing");
        if (status == OrderStatus.InPacking) return order;
        order.TransitionStatus(OrderStatus.InDelivery, "Entrega", ActorId, Now, "matrix-delivery");
        if (status == OrderStatus.InDelivery) return order;
        order.TransitionStatus(status, "Resultado", ActorId, Now, "matrix-result");
        return order;
    }

    private sealed record ConfirmedScenario(
        Order Order,
        DailyCapacity Capacity,
        FrozenLot Lot,
        PlanAcquisition Acquisition,
        LifecycleStoreFake Store);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class LifecycleStoreFake(
        Order order,
        IEnumerable<DailyCapacity> capacities,
        IEnumerable<FrozenLot> lots,
        IEnumerable<PlanAcquisition> acquisitions) : IOrderLifecycleStore
    {
        public List<DailyCapacity> Capacities { get; } = [.. capacities];
        public List<FinancialCreditMovement> FinancialMovements { get; } = [];
        public int SaveCount { get; private set; }

        public Task<T> ExecuteSerializableAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);

        public Task<OrderLifecycleEvent?> FindLifecycleEventAsync(
            string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(order.LifecycleEvents.SingleOrDefault(item => item.IdempotencyKey == idempotencyKey));

        public Task<Order?> FindOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
            Task.FromResult<Order?>(order.Id == orderId ? order : null);

        public Task<DailyCapacity?> FindDailyCapacityAsync(
            DateOnly operationalDate, CancellationToken cancellationToken) =>
            Task.FromResult(Capacities.SingleOrDefault(item => item.OperationalDate == operationalDate));

        public Task<FrozenLot?> FindFrozenLotAsync(Guid frozenLotId, CancellationToken cancellationToken) =>
            Task.FromResult(lots.SingleOrDefault(item => item.Id == frozenLotId));

        public Task<PlanAcquisition?> FindPlanAcquisitionAsync(
            Guid acquisitionId, CancellationToken cancellationToken) =>
            Task.FromResult(acquisitions.SingleOrDefault(item => item.Id == acquisitionId));

        public void AddFinancialCreditMovement(FinancialCreditMovement movement) =>
            FinancialMovements.Add(movement);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
