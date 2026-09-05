using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Finance;

public enum PaymentMethod { Pix, Cash, DebitCard, CreditCard, BankTransfer }

public sealed class Payment : ITenantOwned
{
    private Payment() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string CustomerNameSnapshot { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateOnly ReceivedOn { get; private set; }
    public PaymentMethod Method { get; private set; }
    public string? Reference { get; private set; }
    public Guid RecordedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;

    public static Payment Create(Guid organizationId, Guid customerId, string customerName, decimal amount,
        DateOnly receivedOn, PaymentMethod method, string? reference, Guid actorId, DateTimeOffset createdAt, string idempotencyKey)
    {
        if (organizationId == Guid.Empty || customerId == Guid.Empty || actorId == Guid.Empty || amount <= 0 || string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("Os dados do pagamento são inválidos.");
        return new() { Id = Guid.NewGuid(), OrganizationId = organizationId, CustomerId = customerId,
            CustomerNameSnapshot = customerName.Trim(), Amount = decimal.Round(amount, 2), ReceivedOn = receivedOn,
            Method = method, Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            RecordedBy = actorId, CreatedAt = createdAt, IdempotencyKey = idempotencyKey.Trim() };
    }
}

public sealed class PaymentAllocation : ITenantOwned
{
    private PaymentAllocation() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid ChargeId { get; private set; }
    public decimal Amount { get; private set; }
    public static PaymentAllocation Create(Guid organizationId, Guid paymentId, Guid chargeId, decimal amount)
    {
        if (amount <= 0) throw new DomainException("A alocação deve ser positiva.");
        return new() { Id = Guid.NewGuid(), OrganizationId = organizationId, PaymentId = paymentId,
            ChargeId = chargeId, Amount = decimal.Round(amount, 2) };
    }
}
