using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.FrozenStock;

public sealed class FrozenStockMovement : ITenantOwned
{
    private FrozenStockMovement() { }

    private FrozenStockMovement(
        Guid id,
        Guid organizationId,
        Guid frozenLotId,
        StockMovementType type,
        int quantity,
        string origin,
        Guid actorId,
        DateTimeOffset occurredAt,
        string? reason)
    {
        Id = id;
        OrganizationId = organizationId;
        FrozenLotId = frozenLotId;
        Type = type;
        Quantity = quantity;
        Origin = origin;
        ActorId = actorId;
        OccurredAt = occurredAt;
        Reason = reason;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid FrozenLotId { get; private set; }
    public StockMovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public string Origin { get; private set; } = string.Empty;
    public Guid ActorId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? Reason { get; private set; }

    public int SignedQuantity => Type is StockMovementType.OrderExit or StockMovementType.ExpirationDisposal
        ? -Quantity
        : Quantity;

    internal static FrozenStockMovement CreateProductionEntry(
        Guid organizationId,
        Guid frozenLotId,
        int quantity,
        Guid actorId,
        DateTimeOffset occurredAt)
    {
        if (quantity <= 0)
        {
            throw new DomainException("A quantidade da movimentação deve ser positiva.");
        }

        return new FrozenStockMovement(
            Guid.NewGuid(),
            organizationId,
            frozenLotId,
            StockMovementType.ProductionEntry,
            quantity,
            "FrozenProduction",
            actorId,
            occurredAt,
            null);
    }

    internal static FrozenStockMovement CreateOrderExit(
        Guid organizationId,
        Guid frozenLotId,
        Guid orderId,
        Guid orderItemId,
        int quantity,
        Guid actorId,
        DateTimeOffset occurredAt)
    {
        if (quantity <= 0)
        {
            throw new DomainException("A quantidade da movimentação deve ser positiva.");
        }

        return new FrozenStockMovement(
            Guid.NewGuid(),
            organizationId,
            frozenLotId,
            StockMovementType.OrderExit,
            quantity,
            $"Order:{orderId:N}:{orderItemId:N}",
            actorId,
            occurredAt,
            null);
    }
}
