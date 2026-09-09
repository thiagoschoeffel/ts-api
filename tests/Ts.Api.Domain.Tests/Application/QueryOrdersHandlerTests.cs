using Ts.Api.Application.Orders;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Production;

namespace Ts.Api.Domain.Tests.Application;

public sealed class QueryOrdersHandlerTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    [Fact]
    public async Task List_ProjectsDailyCapacityWithoutCountingFrozenItems()
    {
        var dailyOffer = CatalogOffer.Create(OrganizationId, "Prato do dia", OfferFulfillmentMode.DailyProduction);
        var frozenOffer = CatalogOffer.Create(OrganizationId, "Congelados", OfferFulfillmentMode.FrozenStock);
        var producible = ProducibleItem.Create(OrganizationId, "Sopa");
        var configuration = FrozenConfiguration.Create(
            OrganizationId, frozenOffer.Id, producible.Id, "400 ml", 400, MeasurementUnit.Milliliter, 24m);
        var order = Order.CreateDraft(OrganizationId, Guid.NewGuid(), new DateOnly(2026, 9, 4),
        [
            new OrderItemDefinition(dailyOffer.Id, dailyOffer.FulfillmentMode, 2, 30m,
                ProducibleItemId: producible.Id, OfferName: dailyOffer.Name, ProducibleItemName: producible.Name),
            new OrderItemDefinition(frozenOffer.Id, frozenOffer.FulfillmentMode, 3, configuration.UnitPrice,
                configuration.Id, producible.Id, frozenOffer.Name, producible.Name, configuration.Presentation),
        ]);
        var result = await new ListOrdersHandler(new QueryStoreFake { Orders = [order] })
            .HandleAsync(DefaultQuery(), CancellationToken.None);

        Assert.Equal(5, result.Items.Single().ItemCount);
        Assert.Equal(2, result.Items.Single().DailyCapacityUnits);
        Assert.Equal(132m, result.Items.Single().TotalAmount);
    }

    [Fact]
    public async Task AuthoringContext_ExposesOnlySellableBalanceForFrozenConfiguration()
    {
        var offer = CatalogOffer.Create(OrganizationId, "Congelados", OfferFulfillmentMode.FrozenStock);
        var producible = ProducibleItem.Create(OrganizationId, "Sopa");
        var configuration = FrozenConfiguration.Create(
            OrganizationId, offer.Id, producible.Id, "400 ml", 400, MeasurementUnit.Milliliter, 24m);
        var lot = FrozenLot.RegisterProduction(
            OrganizationId, configuration.Id, new DateOnly(2026, 9, 1), 7,
            Guid.NewGuid(), DateTimeOffset.UtcNow, "production-1", producible.Name, configuration.Presentation);
        var store = new QueryStoreFake
        {
            Offers = [offer], Producibles = [producible], Configurations = [configuration], Lots = [lot],
        };

        var result = await new GetOrderAuthoringContextHandler(store)
            .HandleAsync(new DateOnly(2026, 9, 4), CancellationToken.None);

        var frozen = Assert.Single(result.FrozenConfigurations);
        Assert.Equal(7, frozen.AvailableQuantity);
        Assert.Equal(new DateOnly(2026, 11, 30), frozen.NextExpiration);
        Assert.Equal(producible.Name, frozen.ProducibleItemName);
    }

    [Fact]
    public async Task Details_ExposesPersistedConfirmationEffects()
    {
        var offer = CatalogOffer.Create(OrganizationId, "Prato do dia", OfferFulfillmentMode.DailyProduction);
        var producible = ProducibleItem.Create(OrganizationId, "Frango");
        var order = Order.CreateDraft(OrganizationId, Guid.NewGuid(), new DateOnly(2026, 9, 4),
        [
            new OrderItemDefinition(offer.Id, offer.FulfillmentMode, 2, 30m,
                ProducibleItemId: producible.Id, OfferName: offer.Name, ProducibleItemName: producible.Name),
        ]);
        order.Confirm(Guid.NewGuid(), DateTimeOffset.UtcNow, "confirm-1", deliveryFee: 5m);

        var result = await new GetOrderDetailsHandler(new QueryStoreFake { Orders = [order] })
            .HandleAsync(order.Id, CancellationToken.None);

        Assert.NotNull(result.Confirmation);
        Assert.Equal(60m, result.Confirmation.Subtotal);
        Assert.Equal(65m, result.Confirmation.AmountDue);
        Assert.Equal(OrderStatus.Confirmed, result.Status);
    }

    private sealed class QueryStoreFake : IOrderQueryStore
    {
        public IReadOnlyList<Order> Orders { get; init; } = [];
        public IReadOnlyList<CatalogOffer> Offers { get; init; } = [];
        public IReadOnlyList<ProducibleItem> Producibles { get; init; } = [];
        public IReadOnlyList<FrozenConfiguration> Configurations { get; init; } = [];
        public IReadOnlyList<FrozenLot> Lots { get; init; } = [];

        public Task<OrderQueryPage> GetOrdersAsync(OrderListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new OrderQueryPage(Orders, Orders.Count,
                Orders.GroupBy(item => item.Status).ToDictionary(group => group.Key, group => group.Count())));
        public Task<Order?> FindOrderDetailsAsync(Guid orderId, CancellationToken cancellationToken) =>
            Task.FromResult(Orders.SingleOrDefault(item => item.Id == orderId));
        public Task<IReadOnlyList<CatalogOffer>> GetActiveOffersAsync(CancellationToken cancellationToken) => Task.FromResult(Offers);
        public Task<IReadOnlyList<ProducibleItem>> GetActiveProduciblesAsync(CancellationToken cancellationToken) => Task.FromResult(Producibles);
        public Task<IReadOnlyList<FrozenConfiguration>> GetActiveFrozenConfigurationsAsync(CancellationToken cancellationToken) => Task.FromResult(Configurations);
        public Task<IReadOnlyList<FrozenLot>> GetSellableFrozenLotsAsync(DateOnly sellableOn, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FrozenLot>>(Lots.Where(item => item.IsSellableOn(sellableOn)).ToArray());
    }

    private static OrderListQuery DefaultQuery() => new(null, null, null,
        OrderListStatusGroup.All, OrderListSort.OperationalDate, OrderListSortDirection.Desc, 1, 20);
}
