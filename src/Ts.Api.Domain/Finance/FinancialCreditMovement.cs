using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Finance;

public enum FinancialCreditMovementType { Granted, Consumed, Reversed, ManualAdjustment }

public sealed class FinancialCreditMovement : ITenantOwned
{
    private FinancialCreditMovement() { }
    private FinancialCreditMovement(
        Guid organizationId, Guid customerId, FinancialCreditMovementType type,
        decimal amount, string reason, Guid actorId, DateTimeOffset occurredAt, Guid? orderId)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CustomerId = customerId;
        Type = type;
        Amount = amount;
        Reason = reason;
        ActorId = actorId;
        OccurredAt = occurredAt;
        OrderId = orderId;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public FinancialCreditMovementType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid ActorId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? OrderId { get; private set; }
    public Guid? PaymentId { get; private set; }
    public decimal SignedAmount => Type is FinancialCreditMovementType.Granted
        or FinancialCreditMovementType.Reversed ? Amount
        : Type == FinancialCreditMovementType.ManualAdjustment ? Amount : -Amount;

    public static FinancialCreditMovement Grant(
        Guid organizationId, Guid customerId, decimal amount, string reason,
        Guid actorId, DateTimeOffset occurredAt) =>
        Create(organizationId, customerId, FinancialCreditMovementType.Granted,
            amount, reason, actorId, occurredAt, null);

    public static FinancialCreditMovement GrantFromPayment(Guid organizationId, Guid customerId, Guid paymentId,
        decimal amount, Guid actorId, DateTimeOffset occurredAt)
    {
        var movement = Create(organizationId, customerId, FinancialCreditMovementType.Granted,
            amount, "Excedente não alocado do pagamento", actorId, occurredAt, null);
        movement.PaymentId = paymentId;
        return movement;
    }

    public static FinancialCreditMovement ManualAdjustment(Guid organizationId, Guid customerId,
        decimal signedAmount, string reason, Guid actorId, DateTimeOffset occurredAt)
    {
        if (signedAmount == 0) throw new DomainException("O ajuste financeiro não pode ser zero.");
        return Create(organizationId, customerId, FinancialCreditMovementType.ManualAdjustment,
            signedAmount, reason, actorId, occurredAt, null);
    }

    public static FinancialCreditMovement Consume(
        Guid organizationId, Guid customerId, Guid orderId, decimal amount,
        Guid actorId, DateTimeOffset occurredAt) =>
        Create(organizationId, customerId, FinancialCreditMovementType.Consumed,
            amount, "Consumo na confirmação do pedido", actorId, occurredAt, orderId);

    public static FinancialCreditMovement Reverse(
        Guid organizationId, Guid customerId, Guid orderId, decimal amount,
        Guid actorId, DateTimeOffset occurredAt) =>
        Create(organizationId, customerId, FinancialCreditMovementType.Reversed,
            amount, "Estorno pelo cancelamento do pedido", actorId, occurredAt, orderId);

    private static FinancialCreditMovement Create(
        Guid organizationId, Guid customerId, FinancialCreditMovementType type,
        decimal amount, string reason, Guid actorId, DateTimeOffset occurredAt, Guid? orderId)
    {
        var normalizedReason = reason?.Trim() ?? string.Empty;
        if (organizationId == Guid.Empty || customerId == Guid.Empty || actorId == Guid.Empty
            || amount == 0 || normalizedReason.Length is 0 or > 500)
        {
            throw new DomainException("Os dados da movimentação de crédito financeiro são inválidos.");
        }

        return new FinancialCreditMovement(
            organizationId, customerId, type, amount, normalizedReason, actorId, occurredAt, orderId);
    }
}
