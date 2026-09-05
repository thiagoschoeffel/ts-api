using Ts.Api.Domain.Orders;

namespace Ts.Api.Application.Orders;

public sealed record TransitionOrderStatusCommand(
    Guid OrderId,
    OrderStatus NewStatus,
    string Reason,
    Guid ActorId,
    long ExpectedVersion,
    string IdempotencyKey);

public sealed record RescheduleOrderCommand(
    Guid OrderId,
    DateOnly NewOperationalDate,
    string Reason,
    Guid ActorId,
    long ExpectedVersion,
    string IdempotencyKey);

public sealed record CancelOrderCommand(
    Guid OrderId,
    string Reason,
    Guid ActorId,
    long ExpectedVersion,
    string IdempotencyKey,
    CommercialCancellationDisposition CommercialDisposition = CommercialCancellationDisposition.NotApplicable,
    FrozenCancellationDisposition FrozenDisposition = FrozenCancellationDisposition.NotApplicable,
    FrozenReturnInspection? FrozenReturnInspection = null);

public sealed record OrderLifecycleResult(
    Guid OrderId,
    OrderStatus Status,
    long Version,
    DateOnly OperationalDate,
    OrderLifecycleEventType EventType,
    OrderStatus PreviousStatus,
    DateOnly PreviousOperationalDate,
    CommercialCancellationDisposition CommercialDisposition,
    FrozenCancellationDisposition FrozenDisposition,
    FrozenReturnInspection? FrozenReturnInspection,
    int CapacityUnitsReleased,
    int PlanCreditsReversed,
    decimal FinancialCreditReversed,
    int ChargesCancelled);
