using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Plans;

public sealed class PlanAcquisition : ITenantOwned
{
    private readonly List<PlanCreditMovement> _movements = [];
    private PlanAcquisition() { }

    private PlanAcquisition(
        Guid organizationId,
        Guid customerId,
        Guid eligibleOfferId,
        string planName,
        int credits,
        decimal benefitAmountPerCredit,
        DateOnly acquiredOn,
        DateTimeOffset recordedAt)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CustomerId = customerId;
        EligibleOfferId = eligibleOfferId;
        CompatibleOfferIds = eligibleOfferId.ToString("D");
        PlanName = planName;
        BenefitDescriptionSnapshot = planName;
        BenefitAmountPerCredit = benefitAmountPerCredit;
        AcquiredOn = acquiredOn;
        CustomerNameSnapshot = string.Empty;
        PaidAmount = credits * benefitAmountPerCredit;
        CreatedAt = recordedAt;
        _movements.Add(PlanCreditMovement.Acquire(
            organizationId, Id, credits, recordedAt));
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? PlanId { get; private set; }
    public string CustomerNameSnapshot { get; private set; } = string.Empty;
    public Guid EligibleOfferId { get; private set; }
    public string CompatibleOfferIds { get; private set; } = string.Empty;
    public string PlanName { get; private set; } = string.Empty;
    public string BenefitDescriptionSnapshot { get; private set; } = string.Empty;
    public decimal BenefitAmountPerCredit { get; private set; }
    public DateOnly AcquiredOn { get; private set; }
    public decimal PaidAmount { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<PlanCreditMovement> Movements => _movements.AsReadOnly();
    public int Balance => _movements.Sum(movement => movement.SignedQuantity);

    public static PlanAcquisition Create(
        Guid organizationId,
        Guid customerId,
        Guid eligibleOfferId,
        string planName,
        int credits,
        decimal benefitAmountPerCredit,
        DateOnly acquiredOn,
        DateTimeOffset recordedAt)
    {
        var normalizedName = planName?.Trim() ?? string.Empty;
        if (organizationId == Guid.Empty || customerId == Guid.Empty || eligibleOfferId == Guid.Empty)
        {
            throw new DomainException("Organização, cliente e oferta elegível são obrigatórios.");
        }

        if (normalizedName.Length is 0 or > 160 || credits <= 0 || benefitAmountPerCredit <= 0)
        {
            throw new DomainException("Plano, créditos e benefício financeiro devem ser válidos.");
        }

        return new PlanAcquisition(
            organizationId, customerId, eligibleOfferId, normalizedName,
            credits, benefitAmountPerCredit, acquiredOn, recordedAt);
    }

    public static PlanAcquisition CreateFromPlan(Guid organizationId, Guid customerId, string customerName,
        Guid planId, IReadOnlyCollection<Guid> compatibleOfferIds, string planName, string benefitDescription,
        int credits, decimal paidAmount,
        decimal benefitAmountPerCredit, DateOnly acquiredOn, DateOnly? expiresOn, DateTimeOffset recordedAt)
    {
        var eligibleOfferId = compatibleOfferIds.FirstOrDefault();
        var acquisition = Create(organizationId, customerId, eligibleOfferId, planName, credits,
            benefitAmountPerCredit, acquiredOn, recordedAt);
        if (planId == Guid.Empty || string.IsNullOrWhiteSpace(customerName) || paidAmount < 0 || expiresOn < acquiredOn)
            throw new DomainException("As condições históricas da aquisição são inválidas.");
        acquisition.PlanId = planId;
        acquisition.CustomerNameSnapshot = customerName.Trim();
        acquisition.BenefitDescriptionSnapshot = benefitDescription.Trim();
        acquisition.PaidAmount = decimal.Round(paidAmount, 2);
        acquisition.ExpiresOn = expiresOn;
        acquisition.CompatibleOfferIds = string.Join(',', compatibleOfferIds.Distinct().Select(x => x.ToString("D")));
        return acquisition;
    }

    public bool IsEligibleFor(Guid offerId) => CompatibleOfferIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Any(value => Guid.TryParse(value, out var parsed) && parsed == offerId);

    public PlanCreditMovement Adjust(int signedQuantity, Guid actorId, DateTimeOffset occurredAt)
    {
        if (signedQuantity == 0 || actorId == Guid.Empty || Balance + signedQuantity < 0)
            throw new DomainException("O ajuste de créditos é inválido.");
        var movement = PlanCreditMovement.Adjust(OrganizationId, Id, signedQuantity, actorId, occurredAt);
        _movements.Add(movement);
        return movement;
    }

    public PlanCreditMovement Consume(
        Guid orderId,
        Guid orderItemId,
        int quantity,
        Guid actorId,
        DateTimeOffset occurredAt)
    {
        if (orderId == Guid.Empty || orderItemId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new DomainException("Pedido, item e responsável são obrigatórios no consumo de crédito.");
        }

        if (quantity <= 0 || quantity > Balance)
        {
            throw new DomainException("A aquisição não possui créditos suficientes.");
        }

        var movement = PlanCreditMovement.Consume(
            OrganizationId, Id, orderId, orderItemId, quantity, actorId, occurredAt);
        _movements.Add(movement);
        return movement;
    }

    public PlanCreditMovement Reverse(
        Guid orderId,
        Guid orderItemId,
        int quantity,
        Guid actorId,
        DateTimeOffset occurredAt)
    {
        if (orderId == Guid.Empty || orderItemId == Guid.Empty || actorId == Guid.Empty || quantity <= 0)
        {
            throw new DomainException("Pedido, item, quantidade e responsável são obrigatórios no estorno de crédito.");
        }

        var consumed = _movements
            .Where(item => item.Type == PlanCreditMovementType.Consumed
                && item.OrderId == orderId
                && item.OrderItemId == orderItemId)
            .Sum(item => item.Quantity);
        var reversed = _movements
            .Where(item => item.Type == PlanCreditMovementType.Reversed
                && item.OrderId == orderId
                && item.OrderItemId == orderItemId)
            .Sum(item => item.Quantity);
        if (quantity > consumed - reversed)
        {
            throw new DomainException("O estorno excede os créditos consumidos por este item do pedido.");
        }

        var movement = PlanCreditMovement.Reverse(
            OrganizationId, Id, orderId, orderItemId, quantity, actorId, occurredAt);
        _movements.Add(movement);
        return movement;
    }
}

public enum PlanCreditMovementType { Acquired, Consumed, Reversed, ManualAdjustment }

public sealed class PlanCreditMovement : ITenantOwned
{
    private PlanCreditMovement() { }
    private PlanCreditMovement(
        Guid organizationId, Guid acquisitionId, PlanCreditMovementType type,
        int quantity, Guid? orderId, Guid? orderItemId, Guid? actorId, DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        AcquisitionId = acquisitionId;
        Type = type;
        Quantity = quantity;
        OrderId = orderId;
        OrderItemId = orderItemId;
        ActorId = actorId;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid AcquisitionId { get; private set; }
    public PlanCreditMovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public Guid? OrderId { get; private set; }
    public Guid? OrderItemId { get; private set; }
    public Guid? ActorId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public int SignedQuantity => Type is PlanCreditMovementType.Acquired or PlanCreditMovementType.Reversed
        ? Quantity : Type == PlanCreditMovementType.ManualAdjustment ? Quantity : -Quantity;

    internal static PlanCreditMovement Acquire(
        Guid organizationId, Guid acquisitionId, int quantity, DateTimeOffset occurredAt) =>
        new(organizationId, acquisitionId, PlanCreditMovementType.Acquired,
            quantity, null, null, null, occurredAt);

    internal static PlanCreditMovement Consume(
        Guid organizationId, Guid acquisitionId, Guid orderId, Guid orderItemId,
        int quantity, Guid actorId, DateTimeOffset occurredAt) =>
        new(organizationId, acquisitionId, PlanCreditMovementType.Consumed,
            quantity, orderId, orderItemId, actorId, occurredAt);

    internal static PlanCreditMovement Reverse(
        Guid organizationId, Guid acquisitionId, Guid orderId, Guid orderItemId,
        int quantity, Guid actorId, DateTimeOffset occurredAt) =>
        new(organizationId, acquisitionId, PlanCreditMovementType.Reversed,
            quantity, orderId, orderItemId, actorId, occurredAt);

    internal static PlanCreditMovement Adjust(Guid organizationId, Guid acquisitionId, int signedQuantity,
        Guid actorId, DateTimeOffset occurredAt) =>
        new(organizationId, acquisitionId, PlanCreditMovementType.ManualAdjustment,
            signedQuantity, null, null, actorId, occurredAt);
}
