using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Orders;

namespace Ts.Api.Application.Orders;

public sealed record OrderItemInput(
    Guid OfferId,
    int Quantity,
    decimal? UnitPrice = null,
    Guid? FrozenConfigurationId = null,
    Guid? ProducibleItemId = null);

public sealed record CreateOrderCommand(
    Guid CustomerId,
    DateOnly OperationalDate,
    IReadOnlyCollection<OrderItemInput> Items,
    string IdempotencyKey);

public sealed record EditOrderCommand(
    Guid OrderId,
    Guid CustomerId,
    DateOnly OperationalDate,
    IReadOnlyCollection<OrderItemInput> Items,
    long ExpectedVersion,
    string IdempotencyKey);

public sealed record OrderResult(
    Guid Id,
    Guid CustomerId,
    DateOnly OperationalDate,
    OrderStatus Status,
    long Version,
    int DailyCapacityUnits,
    decimal TotalAmount,
    IReadOnlyCollection<OrderItemResult> Items);

public sealed record OrderItemResult(
    Guid Id,
    Guid OfferId,
    string OfferName,
    OfferFulfillmentMode FulfillmentMode,
    int Quantity,
    decimal UnitPrice,
    decimal Total,
    Guid? FrozenConfigurationId,
    Guid? ProducibleItemId,
    string? ProducibleItemName,
    string? FrozenPresentation);
