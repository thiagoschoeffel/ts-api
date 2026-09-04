using Ts.Api.Domain.Orders;

namespace Ts.Api.Application.Orders;

public sealed record ConfirmOrderCommand(
    Guid OrderId,
    Guid ActorId,
    string IdempotencyKey,
    long ExpectedVersion);

public sealed record ConfirmOrderResult(
    Guid OrderId,
    OrderStatus Status,
    long Version,
    IReadOnlyCollection<FrozenAllocation> FrozenAllocations);

public sealed record FrozenAllocation(
    Guid OrderItemId,
    Guid FrozenConfigurationId,
    Guid FrozenLotId,
    int Quantity);
