using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Orders;

public sealed class OrderCharge : ITenantOwned
{
    private OrderCharge() { }

    internal OrderCharge(
        Guid organizationId,
        Guid orderId,
        decimal amount,
        DateOnly dueOn,
        DateTimeOffset createdAt)
    {
        if (amount <= 0)
        {
            throw new DomainException("O valor da cobrança deve ser positivo.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        OrderId = orderId;
        Amount = amount;
        DueOn = dueOn;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly DueOn { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public OrderChargeStatus Status { get; private set; } = OrderChargeStatus.Pending;
    public Guid? CancelledBy { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    public void Cancel(Guid actorId, DateTimeOffset cancelledAt)
    {
        if (Status != OrderChargeStatus.Pending || actorId == Guid.Empty)
        {
            throw new DomainException("Somente uma cobrança pendente pode ser cancelada por um responsável válido.");
        }

        Status = OrderChargeStatus.Cancelled;
        CancelledBy = actorId;
        CancelledAt = cancelledAt;
    }
}

public enum OrderChargeStatus
{
    Pending = 0,
    Cancelled = 1,
}
