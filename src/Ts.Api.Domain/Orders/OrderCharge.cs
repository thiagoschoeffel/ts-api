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
}
