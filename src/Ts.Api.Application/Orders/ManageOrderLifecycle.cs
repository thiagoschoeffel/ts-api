using Ts.Api.Application.Common;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Finance;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Plans;
using static Ts.Api.Application.Orders.OrderLifecycleHandlerSupport;

namespace Ts.Api.Application.Orders;

public interface IOrderLifecycleStore
{
    Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
    Task<OrderLifecycleEvent?> FindLifecycleEventAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<Order?> FindOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<DailyCapacity?> FindDailyCapacityAsync(DateOnly operationalDate, CancellationToken cancellationToken);
    Task<FrozenLot?> FindFrozenLotAsync(Guid frozenLotId, CancellationToken cancellationToken);
    Task<PlanAcquisition?> FindPlanAcquisitionAsync(Guid acquisitionId, CancellationToken cancellationToken);
    void AddFinancialCreditMovement(FinancialCreditMovement movement);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class TransitionOrderStatusHandler(IOrderLifecycleStore store, TimeProvider timeProvider)
{
    public Task<OrderLifecycleResult> HandleAsync(
        TransitionOrderStatusCommand command,
        CancellationToken cancellationToken)
    {
        var key = ValidateCommon(command.OrderId, command.ActorId, command.Reason, command.IdempotencyKey);
        return store.ExecuteSerializableAsync(async token =>
        {
            var previous = await store.FindLifecycleEventAsync(key, token);
            if (previous is not null)
            {
                EnsureReplay(previous, command.OrderId, command.ActorId, command.ExpectedVersion,
                    command.Reason, OrderLifecycleEventType.StatusTransition,
                    newStatus: command.NewStatus);
                return ToResult(await LoadOrderAsync(store, command.OrderId, token), previous);
            }

            var order = await LoadOrderAsync(store, command.OrderId, token);
            EnsureVersion(order, command.ExpectedVersion);
            order.TransitionStatus(command.NewStatus, command.Reason, command.ActorId,
                timeProvider.GetUtcNow(), key);
            await store.SaveChangesAsync(token);
            return ToResult(order, order.LifecycleEvents.Last());
        }, cancellationToken);
    }
}

public sealed class RescheduleOrderHandler(IOrderLifecycleStore store, TimeProvider timeProvider)
{
    public Task<OrderLifecycleResult> HandleAsync(
        RescheduleOrderCommand command,
        CancellationToken cancellationToken)
    {
        var key = ValidateCommon(command.OrderId, command.ActorId, command.Reason, command.IdempotencyKey);
        return store.ExecuteSerializableAsync(async token =>
        {
            var previous = await store.FindLifecycleEventAsync(key, token);
            if (previous is not null)
            {
                EnsureReplay(previous, command.OrderId, command.ActorId, command.ExpectedVersion,
                    command.Reason, OrderLifecycleEventType.Rescheduled,
                    newOperationalDate: command.NewOperationalDate);
                return ToResult(await LoadOrderAsync(store, command.OrderId, token), previous);
            }

            var order = await LoadOrderAsync(store, command.OrderId, token);
            EnsureVersion(order, command.ExpectedVersion);
            if (order.Status != OrderStatus.Confirmed)
                throw new ConflictException("Somente um pedido confirmado e ainda não iniciado pode ser reagendado.");
            if (command.NewOperationalDate == order.OperationalDate)
                throw new DomainException("A nova data operacional deve ser diferente da data atual.");

            if (order.DailyCapacityUnits > 0)
            {
                var target = await store.FindDailyCapacityAsync(command.NewOperationalDate, token)
                    ?? throw new ConflictException("A capacidade da nova data operacional não foi configurada.");
                if (order.DailyCapacityUnits > target.AvailableUnits)
                    throw new ConflictException("A nova data não possui capacidade diária suficiente.");
                var source = await store.FindDailyCapacityAsync(order.OperationalDate, token)
                    ?? throw new ConflictException("A reserva de capacidade atual do pedido não foi encontrada.");
                source.Release(order.DailyCapacityUnits);
                target.Reserve(order.DailyCapacityUnits);
            }

            order.Reschedule(command.NewOperationalDate, command.Reason, command.ActorId,
                timeProvider.GetUtcNow(), key);
            await store.SaveChangesAsync(token);
            return ToResult(order, order.LifecycleEvents.Last());
        }, cancellationToken);
    }
}

public sealed class CancelOrderHandler(IOrderLifecycleStore store, TimeProvider timeProvider)
{
    public Task<OrderLifecycleResult> HandleAsync(
        CancelOrderCommand command,
        CancellationToken cancellationToken)
    {
        var key = ValidateCommon(command.OrderId, command.ActorId, command.Reason, command.IdempotencyKey);
        return store.ExecuteSerializableAsync(async token =>
        {
            var previous = await store.FindLifecycleEventAsync(key, token);
            if (previous is not null)
            {
                EnsureReplay(previous, command.OrderId, command.ActorId, command.ExpectedVersion,
                    command.Reason, OrderLifecycleEventType.Cancelled,
                    commercialDisposition: command.CommercialDisposition,
                    frozenDisposition: command.FrozenDisposition,
                    frozenReturnInspection: command.FrozenReturnInspection);
                return ToResult(await LoadOrderAsync(store, command.OrderId, token), previous);
            }

            var order = await LoadOrderAsync(store, command.OrderId, token);
            EnsureVersion(order, command.ExpectedVersion);
            order.EnsureCanCancel(command.CommercialDisposition, command.FrozenDisposition, command.FrozenReturnInspection);
            var occurredAt = timeProvider.GetUtcNow();
            var previousStatus = order.Status;

            var capacityReleased = await ReleaseCapacityAsync(order, token);
            var reverseCommercial = command.CommercialDisposition == CommercialCancellationDisposition.Reverse;
            var planCreditsReversed = reverseCommercial
                ? await ReversePlanCreditsAsync(order, command.ActorId, occurredAt, token) : 0;
            var financialCreditReversed = reverseCommercial
                ? ReverseFinancialCredit(order, command.ActorId, occurredAt) : 0;
            var chargesCancelled = reverseCommercial
                ? CancelCharges(order, command.ActorId, occurredAt) : 0;
            await ApplyFrozenDispositionAsync(order, command, occurredAt, token);

            order.Cancel(command.Reason, command.ActorId, occurredAt, key,
                command.CommercialDisposition, command.FrozenDisposition, command.FrozenReturnInspection,
                capacityReleased, planCreditsReversed, financialCreditReversed, chargesCancelled);
            await store.SaveChangesAsync(token);
            var lifecycleEvent = order.LifecycleEvents.Last();
            if (lifecycleEvent.PreviousStatus != previousStatus)
                throw new InvalidOperationException("O estágio auditado do cancelamento ficou inconsistente.");
            return ToResult(order, lifecycleEvent);
        }, cancellationToken);
    }

    private async Task<int> ReleaseCapacityAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.Status != OrderStatus.Confirmed || order.DailyCapacityUnits == 0)
            return 0;

        var capacity = await store.FindDailyCapacityAsync(order.OperationalDate, cancellationToken)
            ?? throw new ConflictException("A reserva de capacidade atual do pedido não foi encontrada.");
        capacity.Release(order.DailyCapacityUnits);
        return order.DailyCapacityUnits;
    }

    private async Task<int> ReversePlanCreditsAsync(
        Order order, Guid actorId, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var reversed = 0;
        foreach (var allocation in order.PlanCreditAllocations)
        {
            var acquisition = await store.FindPlanAcquisitionAsync(allocation.AcquisitionId, cancellationToken)
                ?? throw new ConflictException("A aquisição original do crédito de plano não foi encontrada.");
            acquisition.Reverse(order.Id, allocation.OrderItemId, allocation.Quantity, actorId, occurredAt);
            reversed += allocation.Quantity;
        }
        return reversed;
    }

    private decimal ReverseFinancialCredit(Order order, Guid actorId, DateTimeOffset occurredAt)
    {
        var amount = order.ConfirmationAudits.SingleOrDefault()?.FinancialCreditApplied ?? 0;
        if (amount > 0)
        {
            store.AddFinancialCreditMovement(FinancialCreditMovement.Reverse(
                order.OrganizationId, order.CustomerId, order.Id, amount, actorId, occurredAt));
        }
        return amount;
    }

    private static int CancelCharges(Order order, Guid actorId, DateTimeOffset occurredAt)
    {
        var pending = order.Charges.Where(item => item.Status == OrderChargeStatus.Pending).ToArray();
        foreach (var charge in pending)
            charge.Cancel(actorId, occurredAt);
        return pending.Length;
    }

    private async Task ApplyFrozenDispositionAsync(
        Order order, CancelOrderCommand command, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        if (command.FrozenDisposition != FrozenCancellationDisposition.ReturnToStock)
            return;

        foreach (var allocation in order.FrozenAllocations)
        {
            var lot = await store.FindFrozenLotAsync(allocation.FrozenLotId, cancellationToken)
                ?? throw new ConflictException("O lote original do congelado não foi encontrado.");
            lot.ReverseOrderExit(order.Id, allocation.OrderItemId, allocation.Quantity,
                command.ActorId, occurredAt, command.Reason);
        }
    }
}

internal static class OrderLifecycleHandlerSupport
{
    public static string ValidateCommon(Guid orderId, Guid actorId, string reason, string idempotencyKey)
    {
        var key = idempotencyKey?.Trim() ?? string.Empty;
        var normalizedReason = reason?.Trim() ?? string.Empty;
        if (orderId == Guid.Empty || actorId == Guid.Empty)
            throw new DomainException("Pedido e responsável são obrigatórios.");
        if (normalizedReason.Length is 0 or > 500)
            throw new DomainException("O motivo deve possuir entre 1 e 500 caracteres.");
        if (key.Length is 0 or > 200)
            throw new DomainException("A chave de idempotência deve possuir entre 1 e 200 caracteres.");
        return key;
    }

    public static async Task<Order> LoadOrderAsync(
        IOrderLifecycleStore store, Guid orderId, CancellationToken cancellationToken) =>
        await store.FindOrderAsync(orderId, cancellationToken)
        ?? throw new ResourceNotFoundException("Pedido não encontrado.");

    public static void EnsureVersion(Order order, long expectedVersion)
    {
        if (order.Version != expectedVersion)
            throw new ConflictException("O pedido foi alterado. Recarregue os dados antes de continuar.");
    }

    public static void EnsureReplay(
        OrderLifecycleEvent lifecycleEvent,
        Guid orderId,
        Guid actorId,
        long expectedVersion,
        string reason,
        OrderLifecycleEventType eventType,
        OrderStatus? newStatus = null,
        DateOnly? newOperationalDate = null,
        CommercialCancellationDisposition commercialDisposition = CommercialCancellationDisposition.NotApplicable,
        FrozenCancellationDisposition frozenDisposition = FrozenCancellationDisposition.NotApplicable,
        FrozenReturnInspection? frozenReturnInspection = null)
    {
        if (lifecycleEvent.OrderId != orderId
            || lifecycleEvent.ActorId != actorId
            || lifecycleEvent.PreviousVersion != expectedVersion
            || lifecycleEvent.Reason != reason.Trim()
            || lifecycleEvent.Type != eventType
            || newStatus.HasValue && lifecycleEvent.NewStatus != newStatus
            || newOperationalDate.HasValue && lifecycleEvent.NewOperationalDate != newOperationalDate
            || eventType == OrderLifecycleEventType.Cancelled
                && (lifecycleEvent.CommercialDisposition != commercialDisposition
                    || lifecycleEvent.FrozenDisposition != frozenDisposition
                    || lifecycleEvent.HumanInspectionPerformed != (frozenReturnInspection is not null)
                    || lifecycleEvent.PackagingIntact != (frozenReturnInspection?.PackagingIntact ?? false)
                    || lifecycleEvent.TemperatureControlled != (frozenReturnInspection?.TemperatureControlled ?? false)
                    || lifecycleEvent.TraceabilityIntact != (frozenReturnInspection?.TraceabilityIntact ?? false)))
        {
            throw new ConflictException("A chave de idempotência já foi usada com outro recurso ou conteúdo.");
        }
    }

    public static OrderLifecycleResult ToResult(Order order, OrderLifecycleEvent lifecycleEvent) => new(
        order.Id, lifecycleEvent.NewStatus, lifecycleEvent.PreviousVersion + 1,
        lifecycleEvent.NewOperationalDate,
        lifecycleEvent.Type, lifecycleEvent.PreviousStatus,
        lifecycleEvent.PreviousOperationalDate, lifecycleEvent.CommercialDisposition,
        lifecycleEvent.FrozenDisposition,
        lifecycleEvent.HumanInspectionPerformed
            ? new FrozenReturnInspection(lifecycleEvent.PackagingIntact,
                lifecycleEvent.TemperatureControlled, lifecycleEvent.TraceabilityIntact)
            : null,
        lifecycleEvent.CapacityUnitsReleased,
        lifecycleEvent.PlanCreditsReversed, lifecycleEvent.FinancialCreditReversed,
        lifecycleEvent.ChargesCancelled);
}
