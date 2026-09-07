using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Orders;

namespace Ts.Api.Domain.Tests;

public sealed class OrderFulfillmentSnapshotTests
{
    [Fact]
    public void Confirm_PreservesDeliverySnapshotAndFrozenInstant()
    {
        var order = Order.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 7),
            [new OrderItemDefinition(Guid.NewGuid(), OfferFulfillmentMode.DailyProduction, 1, 25m)],
            fulfillment: new OrderFulfillmentSnapshotDefinition(
                OrderFulfillmentType.Delivery, "Maria", "(11) 99999-9999", "Casa",
                "Rua A", "10", Neighborhood: "Centro", City: "São Paulo", State: "SP",
                DeliveryWindow: "11:00–12:00"));
        var confirmedAt = new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

        order.Confirm(Guid.NewGuid(), confirmedAt, "confirm-snapshot");

        Assert.Equal(OrderFulfillmentType.Delivery, order.FulfillmentType);
        Assert.Equal("11999999999", order.FulfillmentPhone);
        Assert.Equal("Rua A", order.FulfillmentStreet);
        Assert.Equal("11:00–12:00", order.DeliveryWindow);
        Assert.Equal(confirmedAt, order.FulfillmentFrozenAt);
    }
}
