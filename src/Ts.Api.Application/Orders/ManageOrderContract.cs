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
    string IdempotencyKey,
    string? CustomerName = null,
    OrderFulfillmentInput? Fulfillment = null);

public sealed record EditOrderCommand(
    Guid OrderId,
    Guid CustomerId,
    DateOnly OperationalDate,
    IReadOnlyCollection<OrderItemInput> Items,
    long ExpectedVersion,
    string IdempotencyKey,
    string? CustomerName = null,
    OrderFulfillmentInput? Fulfillment = null);

public sealed record OrderFulfillmentInput(
    OrderFulfillmentType Type,
    string Phone,
    Guid? AddressId = null,
    string? DeliveryWindow = null);

public sealed record OrderFulfillmentResult(
    OrderFulfillmentType? Type,
    string? ContactName,
    string? Phone,
    string? AddressLabel,
    string? Street,
    string? Number,
    string? Complement,
    string? Neighborhood,
    string? City,
    string? State,
    string? PostalCode,
    string? Reference,
    string? DeliveryWindow,
    DateTimeOffset? FrozenAt,
    bool IsComplete);

public sealed record OrderResult(
    Guid Id,
    Guid CustomerId,
    DateOnly OperationalDate,
    OrderStatus Status,
    long Version,
    int DailyCapacityUnits,
    decimal TotalAmount,
    IReadOnlyCollection<OrderItemResult> Items,
    OrderFulfillmentResult Fulfillment);

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
