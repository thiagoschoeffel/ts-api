using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Orders;

public sealed class FrozenStockAllocation : ITenantOwned
{
    private FrozenStockAllocation() { }

    internal FrozenStockAllocation(
        Guid organizationId,
        Guid orderId,
        Guid orderItemId,
        Guid frozenConfigurationId,
        Guid frozenLotId,
        int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("A quantidade alocada deve ser positiva.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        OrderId = orderId;
        OrderItemId = orderItemId;
        FrozenConfigurationId = frozenConfigurationId;
        FrozenLotId = frozenLotId;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public Guid FrozenConfigurationId { get; private set; }
    public Guid FrozenLotId { get; private set; }
    public int Quantity { get; private set; }
}
