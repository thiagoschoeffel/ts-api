namespace Ts.Api.Domain.Orders;

public enum OrderStatus
{
    Open = 1,
    Confirmed = 2,
    InProduction = 3,
    InPacking = 4,
    InDelivery = 5,
    Completed = 6,
    Cancelled = 7,
    DeliveryFailed = 8,
}
