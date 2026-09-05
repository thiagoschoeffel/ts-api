using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Orders;

public sealed class Order : ITenantOwned
{
    private readonly List<OrderItem> _items = [];
    private readonly List<FrozenStockAllocation> _frozenAllocations = [];
    private readonly List<OrderCharge> _charges = [];
    private readonly List<OrderItemComponent> _componentSnapshots = [];
    private readonly List<OrderPlanCreditAllocation> _planCreditAllocations = [];
    private readonly List<OrderConfirmationAudit> _confirmationAudits = [];
    private readonly List<OrderLifecycleEvent> _lifecycleEvents = [];

    private Order() { }

    private Order(
        Guid organizationId,
        Guid customerId,
        DateOnly operationalDate,
        string creationIdempotencyKey)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CustomerId = customerId;
        OperationalDate = operationalDate;
        Status = OrderStatus.Open;
        CreationIdempotencyKey = creationIdempotencyKey;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public DateOnly OperationalDate { get; private set; }
    public OrderStatus Status { get; private set; }
    public long Version { get; private set; }
    public Guid? ConfirmedBy { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public string? ConfirmationIdempotencyKey { get; private set; }
    public string CreationIdempotencyKey { get; private set; } = string.Empty;
    public string? LastModificationIdempotencyKey { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<FrozenStockAllocation> FrozenAllocations => _frozenAllocations.AsReadOnly();
    public IReadOnlyCollection<OrderCharge> Charges => _charges.AsReadOnly();
    public IReadOnlyCollection<OrderItemComponent> ComponentSnapshots => _componentSnapshots.AsReadOnly();
    public IReadOnlyCollection<OrderPlanCreditAllocation> PlanCreditAllocations => _planCreditAllocations.AsReadOnly();
    public IReadOnlyCollection<OrderConfirmationAudit> ConfirmationAudits => _confirmationAudits.AsReadOnly();
    public IReadOnlyCollection<OrderLifecycleEvent> LifecycleEvents => _lifecycleEvents.AsReadOnly();
    public int DailyCapacityUnits => _items
        .Where(item => item.FulfillmentMode == OfferFulfillmentMode.DailyProduction)
        .Sum(item => item.Quantity);
    public decimal TotalAmount => _items.Sum(item => item.Total);

    public static Order CreateDraft(
        Guid organizationId,
        Guid customerId,
        DateOnly operationalDate,
        IReadOnlyCollection<OrderItemDefinition> items,
        string? creationIdempotencyKey = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A organização é obrigatória.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException("O cliente é obrigatório.");
        }

        if (items.Count == 0)
        {
            throw new DomainException("O pedido deve possuir ao menos um item.");
        }

        var normalizedKey = creationIdempotencyKey is null
            ? $"internal-{Guid.NewGuid():N}"
            : NormalizeIdempotencyKey(creationIdempotencyKey);
        var order = new Order(organizationId, customerId, operationalDate, normalizedKey);
        order.ReplaceItems(items);

        return order;
    }

    public void EditDraft(
        Guid customerId,
        DateOnly operationalDate,
        IReadOnlyCollection<OrderItemDefinition> items,
        string idempotencyKey)
    {
        if (Status != OrderStatus.Open)
        {
            throw new DomainException("Somente um pedido aberto pode ser alterado.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException("O cliente é obrigatório.");
        }

        if (items.Count == 0)
        {
            throw new DomainException("O pedido deve possuir ao menos um item.");
        }

        CustomerId = customerId;
        OperationalDate = operationalDate;
        ReplaceItems(items);
        LastModificationIdempotencyKey = NormalizeIdempotencyKey(idempotencyKey);
        Version++;
    }

    public FrozenStockAllocation AllocateFrozenStock(OrderItem item, Guid frozenLotId, int quantity)
    {
        if (Status != OrderStatus.Open || !_items.Contains(item))
        {
            throw new DomainException("O estoque só pode ser alocado a um item do pedido aberto.");
        }

        if (item.FulfillmentMode != OfferFulfillmentMode.FrozenStock
            || item.FrozenConfigurationId is not Guid frozenConfigurationId)
        {
            throw new DomainException("O item informado não é atendido por estoque congelado.");
        }

        var allocated = _frozenAllocations
            .Where(allocation => allocation.OrderItemId == item.Id)
            .Sum(allocation => allocation.Quantity);
        if (allocated + quantity > item.Quantity)
        {
            throw new DomainException("A alocação de congelados excede a quantidade do item.");
        }

        var allocation = new FrozenStockAllocation(
            OrganizationId,
            Id,
            item.Id,
            frozenConfigurationId,
            frozenLotId,
            quantity);
        _frozenAllocations.Add(allocation);
        return allocation;
    }

    public void SnapshotComponent(
        OrderItem item, Guid compositionId, int compositionVersion, string name,
        decimal quantityPerUnit, string measurementUnit, string dietaryMarkers)
    {
        if (Status != OrderStatus.Open || !_items.Contains(item))
        {
            throw new DomainException("A composição só pode ser consolidada em item do pedido aberto.");
        }

        _componentSnapshots.Add(new OrderItemComponent(
            OrganizationId, Id, item.Id, compositionId, compositionVersion, name,
            quantityPerUnit, quantityPerUnit * item.Quantity, measurementUnit, dietaryMarkers));
    }

    public void AllocatePlanCredit(
        OrderItem item, Guid acquisitionId, string planName, int quantity, decimal coveredAmount)
    {
        if (Status != OrderStatus.Open || !_items.Contains(item) || quantity <= 0 || coveredAmount <= 0)
        {
            throw new DomainException("A alocação de crédito de plano é inválida.");
        }

        _planCreditAllocations.Add(new OrderPlanCreditAllocation(
            OrganizationId, Id, item.Id, acquisitionId, planName, quantity, coveredAmount));
    }

    public void Confirm(
        Guid actorId,
        DateTimeOffset confirmedAt,
        string idempotencyKey,
        decimal discountAmount = 0,
        string? discountReason = null,
        decimal deliveryFee = 0,
        decimal financialCreditApplied = 0)
    {
        if (Status != OrderStatus.Open)
        {
            throw new DomainException("Somente um pedido aberto pode ser confirmado.");
        }

        if (actorId == Guid.Empty)
        {
            throw new DomainException("O responsável pela confirmação é obrigatório.");
        }

        var normalizedKey = idempotencyKey.Trim();
        if (normalizedKey.Length is 0 or > 200)
        {
            throw new DomainException("A chave de idempotência deve possuir entre 1 e 200 caracteres.");
        }

        foreach (var item in _items.Where(item => item.FulfillmentMode == OfferFulfillmentMode.FrozenStock))
        {
            var allocated = _frozenAllocations
                .Where(allocation => allocation.OrderItemId == item.Id)
                .Sum(allocation => allocation.Quantity);
            if (allocated != item.Quantity)
            {
                throw new DomainException("Todo item congelado deve estar integralmente alocado antes da confirmação.");
            }
        }

        var normalizedReason = discountReason?.Trim();
        var planCovered = _planCreditAllocations.Sum(allocation => allocation.CoveredAmount);
        if (discountAmount < 0 || deliveryFee < 0 || financialCreditApplied < 0
            || discountAmount > 0 && string.IsNullOrWhiteSpace(normalizedReason)
            || normalizedReason?.Length > 500
            || discountAmount > TotalAmount - planCovered)
        {
            throw new DomainException("Desconto, motivo, taxa ou crédito financeiro são inválidos.");
        }

        var beforeFinancialCredit = TotalAmount - planCovered - discountAmount + deliveryFee;
        if (financialCreditApplied > beforeFinancialCredit)
        {
            throw new DomainException("O crédito financeiro não pode exceder o saldo do pedido.");
        }

        var amountDue = beforeFinancialCredit - financialCreditApplied;
        if (amountDue > 0)
        {
            _charges.Add(new OrderCharge(OrganizationId, Id, amountDue, OperationalDate, confirmedAt));
        }

        _confirmationAudits.Add(new OrderConfirmationAudit(
            OrganizationId, Id, actorId, confirmedAt, normalizedKey, TotalAmount,
            planCovered, discountAmount, normalizedReason, deliveryFee,
            financialCreditApplied, amountDue));

        Status = OrderStatus.Confirmed;
        ConfirmedBy = actorId;
        ConfirmedAt = confirmedAt;
        ConfirmationIdempotencyKey = normalizedKey;
        Version++;
    }

    public void TransitionStatus(
        OrderStatus newStatus,
        string reason,
        Guid actorId,
        DateTimeOffset occurredAt,
        string idempotencyKey)
    {
        var allowed = Status switch
        {
            OrderStatus.Confirmed => newStatus == OrderStatus.InProduction,
            OrderStatus.InProduction => newStatus == OrderStatus.InPacking,
            OrderStatus.InPacking => newStatus == OrderStatus.InDelivery,
            OrderStatus.InDelivery => newStatus is OrderStatus.Completed or OrderStatus.DeliveryFailed,
            OrderStatus.DeliveryFailed => newStatus == OrderStatus.InDelivery,
            _ => false,
        };
        if (!allowed)
        {
            throw new DomainException($"A transição de {Status} para {newStatus} não é permitida.");
        }

        var previousStatus = Status;
        Status = newStatus;
        _lifecycleEvents.Add(CreateLifecycleEvent(
            OrderLifecycleEventType.StatusTransition, previousStatus, newStatus,
            OperationalDate, OperationalDate, reason, actorId, occurredAt, idempotencyKey));
        Version++;
    }

    public void Reschedule(
        DateOnly newOperationalDate,
        string reason,
        Guid actorId,
        DateTimeOffset occurredAt,
        string idempotencyKey)
    {
        if (Status != OrderStatus.Confirmed)
        {
            throw new DomainException("Somente um pedido confirmado e ainda não iniciado pode ser reagendado.");
        }

        if (newOperationalDate == OperationalDate)
        {
            throw new DomainException("A nova data operacional deve ser diferente da data atual.");
        }

        var previousDate = OperationalDate;
        OperationalDate = newOperationalDate;
        _lifecycleEvents.Add(CreateLifecycleEvent(
            OrderLifecycleEventType.Rescheduled, Status, Status,
            previousDate, newOperationalDate, reason, actorId, occurredAt, idempotencyKey));
        Version++;
    }

    public void Cancel(
        string reason,
        Guid actorId,
        DateTimeOffset occurredAt,
        string idempotencyKey,
        CommercialCancellationDisposition commercialDisposition,
        FrozenCancellationDisposition frozenDisposition,
        FrozenReturnInspection? frozenReturnInspection,
        int capacityUnitsReleased,
        int planCreditsReversed,
        decimal financialCreditReversed,
        int chargesCancelled)
    {
        EnsureCanCancel(commercialDisposition, frozenDisposition, frozenReturnInspection);
        var previousStatus = Status;
        Status = OrderStatus.Cancelled;
        _lifecycleEvents.Add(CreateLifecycleEvent(
            OrderLifecycleEventType.Cancelled, previousStatus, Status,
            OperationalDate, OperationalDate, reason, actorId, occurredAt, idempotencyKey,
            commercialDisposition, frozenDisposition, frozenReturnInspection, capacityUnitsReleased,
            planCreditsReversed, financialCreditReversed, chargesCancelled));
        Version++;
    }

    public void EnsureCanCancel(
        CommercialCancellationDisposition commercialDisposition,
        FrozenCancellationDisposition frozenDisposition,
        FrozenReturnInspection? frozenReturnInspection)
    {
        if (Status is OrderStatus.Cancelled or OrderStatus.Completed)
            throw new DomainException("Um pedido cancelado ou concluído não pode ser cancelado.");

        if (!Enum.IsDefined(commercialDisposition) || !Enum.IsDefined(frozenDisposition))
            throw new DomainException("As decisões comercial e física do cancelamento são inválidas.");

        if (frozenReturnInspection is not null
            && frozenDisposition != FrozenCancellationDisposition.ReturnToStock)
            throw new DomainException("A conferência de retorno só se aplica a congelados destinados ao estoque.");

        if (Status == OrderStatus.Open
            && commercialDisposition != CommercialCancellationDisposition.NotApplicable)
            throw new DomainException("Pedido aberto não possui efeitos comerciais para estornar ou preservar.");

        if (Status == OrderStatus.Confirmed
            && commercialDisposition != CommercialCancellationDisposition.Reverse)
            throw new DomainException("O cancelamento antes da produção deve reverter os efeitos comerciais.");

        if (Status is not OrderStatus.Open and not OrderStatus.Confirmed
            && commercialDisposition == CommercialCancellationDisposition.NotApplicable)
            throw new DomainException("O cancelamento após o início da produção exige decidir entre estornar ou preservar os efeitos comerciais.");

        ValidateFrozenCancellation(frozenDisposition, frozenReturnInspection);
    }

    private void ReplaceItems(IReadOnlyCollection<OrderItemDefinition> items)
    {
        _items.Clear();
        foreach (var item in items)
        {
            _items.Add(new OrderItem(
                OrganizationId,
                Id,
                item.OfferId,
                item.FulfillmentMode,
                item.Quantity,
                item.UnitPrice,
                item.FrozenConfigurationId,
                item.ProducibleItemId,
                item.OfferName,
                item.ProducibleItemName,
                item.FrozenPresentation));
        }
    }

    private static string NormalizeIdempotencyKey(string idempotencyKey)
    {
        var normalized = idempotencyKey?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 200)
        {
            throw new DomainException("A chave de idempotência deve possuir entre 1 e 200 caracteres.");
        }

        return normalized;
    }

    private void ValidateFrozenCancellation(
        FrozenCancellationDisposition disposition,
        FrozenReturnInspection? inspection)
    {
        if (_frozenAllocations.Count == 0)
        {
            if (disposition != FrozenCancellationDisposition.NotApplicable)
                throw new DomainException("Pedido sem congelados deve usar a destinação NotApplicable.");
            return;
        }

        if (disposition == FrozenCancellationDisposition.NotApplicable)
            throw new DomainException("A destinação física dos congelados é obrigatória.");

        if (Status == OrderStatus.Confirmed
            && disposition != FrozenCancellationDisposition.ReturnToStock)
            throw new DomainException("Congelados ainda não separados devem retornar ao mesmo lote.");

        if (Status is OrderStatus.InProduction or OrderStatus.InPacking
            && disposition == FrozenCancellationDisposition.ReturnToStock
            && (inspection is null
                || !inspection.PackagingIntact
                || !inspection.TemperatureControlled
                || !inspection.TraceabilityIntact))
            throw new DomainException("O retorno de congelados separados exige conferência humana com embalagem, temperatura e rastreabilidade íntegras.");

        if (Status is OrderStatus.InDelivery or OrderStatus.DeliveryFailed
            && disposition == FrozenCancellationDisposition.ReturnToStock)
            throw new DomainException("Congelados expedidos ou com cadeia fria duvidosa não podem retornar ao estoque vendável.");
    }

    private OrderLifecycleEvent CreateLifecycleEvent(
        OrderLifecycleEventType type,
        OrderStatus previousStatus,
        OrderStatus newStatus,
        DateOnly previousOperationalDate,
        DateOnly newOperationalDate,
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
        var normalizedReason = reason?.Trim() ?? string.Empty;
        if (actorId == Guid.Empty || normalizedReason.Length is 0 or > 500)
            throw new DomainException("Responsável e motivo com até 500 caracteres são obrigatórios.");

        return new OrderLifecycleEvent(
            OrganizationId, Id, type, previousStatus, newStatus,
            previousOperationalDate, newOperationalDate, Version, normalizedReason,
            actorId, occurredAt, NormalizeIdempotencyKey(idempotencyKey),
            commercialDisposition, frozenDisposition, frozenReturnInspection, capacityUnitsReleased,
            planCreditsReversed, financialCreditReversed, chargesCancelled);
    }
}

public sealed record OrderItemDefinition(
    Guid OfferId,
    OfferFulfillmentMode FulfillmentMode,
    int Quantity,
    decimal UnitPrice,
    Guid? FrozenConfigurationId = null,
    Guid? ProducibleItemId = null,
    string OfferName = "",
    string? ProducibleItemName = null,
    string? FrozenPresentation = null);
