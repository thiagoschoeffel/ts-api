using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Orders;

public enum OrderLifecycleEventType
{
    StatusTransition = 1,
    Rescheduled = 2,
    Cancelled = 3,
}

public enum FrozenCancellationDisposition
{
    NotApplicable = 0,
    ReturnToStock = 1,
    Quarantine = 2,
    Discarded = 3,
}

public enum CommercialCancellationDisposition
{
    NotApplicable = 0,
    Reverse = 1,
    Preserve = 2,
}

public sealed record FrozenReturnInspection(
    bool PackagingIntact,
    bool TemperatureControlled,
    bool TraceabilityIntact);

public sealed class OrderLifecycleEvent : ITenantOwned
{
    private OrderLifecycleEvent() { }

    internal OrderLifecycleEvent(
        Guid organizationId,
        Guid orderId,
        OrderLifecycleEventType type,
        OrderStatus previousStatus,
        OrderStatus newStatus,
        DateOnly previousOperationalDate,
        DateOnly newOperationalDate,
        long previousVersion,
        string reason,
        Guid actorId,
        DateTimeOffset occurredAt,
        string idempotencyKey,
        CommercialCancellationDisposition commercialDisposition = CommercialCancellationDisposition.NotApplicable,
        FrozenCancellationDisposition frozenDisposition = FrozenCancellationDisposition.NotApplicable,
        FrozenReturnInspection? frozenReturnInspection = null,
        int capacityUnitsReleased = 0,
        int planCreditsReversed = 0,
        decimal financialCreditReversed = 0,
        int chargesCancelled = 0)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        OrderId = orderId;
        Type = type;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        PreviousOperationalDate = previousOperationalDate;
        NewOperationalDate = newOperationalDate;
        PreviousVersion = previousVersion;
        Reason = reason;
        ActorId = actorId;
        OccurredAt = occurredAt;
        IdempotencyKey = idempotencyKey;
        CommercialDisposition = commercialDisposition;
        FrozenDisposition = frozenDisposition;
        HumanInspectionPerformed = frozenReturnInspection is not null;
        PackagingIntact = frozenReturnInspection?.PackagingIntact ?? false;
        TemperatureControlled = frozenReturnInspection?.TemperatureControlled ?? false;
        TraceabilityIntact = frozenReturnInspection?.TraceabilityIntact ?? false;
        CapacityUnitsReleased = capacityUnitsReleased;
        PlanCreditsReversed = planCreditsReversed;
        FinancialCreditReversed = financialCreditReversed;
        ChargesCancelled = chargesCancelled;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid OrderId { get; private set; }
    public OrderLifecycleEventType Type { get; private set; }
    public OrderStatus PreviousStatus { get; private set; }
    public OrderStatus NewStatus { get; private set; }
    public DateOnly PreviousOperationalDate { get; private set; }
    public DateOnly NewOperationalDate { get; private set; }
    public long PreviousVersion { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid ActorId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public FrozenCancellationDisposition FrozenDisposition { get; private set; }
    public CommercialCancellationDisposition CommercialDisposition { get; private set; }
    public bool HumanInspectionPerformed { get; private set; }
    public bool PackagingIntact { get; private set; }
    public bool TemperatureControlled { get; private set; }
    public bool TraceabilityIntact { get; private set; }
    public int CapacityUnitsReleased { get; private set; }
    public int PlanCreditsReversed { get; private set; }
    public decimal FinancialCreditReversed { get; private set; }
    public int ChargesCancelled { get; private set; }
}
