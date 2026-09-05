using Ts.Api.Application.Operations;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Operations;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Production;

namespace Ts.Api.Domain.Tests.Application;

public sealed class ManageOperationsHandlerTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateOnly OperationalDate = new(2026, 9, 5);

    [Fact]
    public async Task Production_AggregatesConfirmedDailyComponentsAndExcludesFrozenItems()
    {
        var order = CreateConfirmedMixedOrder();
        var result = await new GetProductionSnapshotHandler(
            new OperationsStoreFake { Orders = [order] }, TimeProvider.System)
            .HandleAsync(OperationalDate, CancellationToken.None);

        Assert.Equal(1, result.OrderCount);
        Assert.Equal(2, result.MealCount);
        var need = Assert.Single(result.Needs);
        Assert.Equal("Arroz", need.Name);
        Assert.Equal(200m, need.Quantity);
    }

    [Fact]
    public async Task Pack_CreatesOneDailyLabelPerUnitAndNeverLabelsFrozenUnitsAgain()
    {
        var order = CreateConfirmedMixedOrder();
        var store = new OperationsStoreFake { Orders = [order] };
        var result = await new PackOrderHandler(store, TimeProvider.System).HandleAsync(
            new PackOrderCommand(order.Id, order.Version, ActorId, "pack-1"), CancellationToken.None);

        Assert.Equal(OrderStatus.InPacking, order.Status);
        Assert.Equal(2, result.Labels!.DailyItemLabels.Count);
        Assert.Single(result.Labels.PreLabeledFrozenItemIds);
        Assert.Equal("Maria Silva", result.Labels.ExternalPackageLabel.CustomerName);
        Assert.NotNull(result.PackedAt);

        var repeated = await new PackOrderHandler(store, TimeProvider.System).HandleAsync(
            new PackOrderCommand(order.Id, 0, ActorId, "pack-1"), CancellationToken.None);
        Assert.Equal(result.Labels.DailyItemLabels.Select(item => item.Id), repeated.Labels!.DailyItemLabels.Select(item => item.Id));
        Assert.Equal(result.Labels.ExternalPackageLabel.Id, repeated.Labels.ExternalPackageLabel.Id);
        Assert.Single(store.Packings);
    }

    [Fact]
    public async Task PrintFailure_IsRecordedWithoutChangingOrderOrStockState()
    {
        var order = CreateConfirmedMixedOrder();
        var store = new OperationsStoreFake { Orders = [order] };
        var packed = await new PackOrderHandler(store, TimeProvider.System).HandleAsync(
            new PackOrderCommand(order.Id, order.Version, ActorId, "pack-2"), CancellationToken.None);
        var versionAfterPacking = order.Version;
        var selection = new PackingLabelSelection(
            [packed.Labels!.DailyItemLabels.First().Id], true);

        var attempt = await new RecordLabelPrintHandler(store, TimeProvider.System).HandleAsync(
            new RecordLabelPrintCommand(order.Id, selection, LabelPrintStatus.Failed,
                "Impressora offline", ActorId, "print-1"), CancellationToken.None);

        Assert.Equal(LabelPrintStatus.Failed, attempt.Status);
        Assert.Equal(versionAfterPacking, order.Version);
        Assert.Equal(OrderStatus.InPacking, order.Status);
        Assert.Single(store.Attempts);
    }

    private static Order CreateConfirmedMixedOrder()
    {
        var dailyOffer = CatalogOffer.Create(OrganizationId, "Prato do dia", OfferFulfillmentMode.DailyProduction);
        var frozenOffer = CatalogOffer.Create(OrganizationId, "Congelados", OfferFulfillmentMode.FrozenStock);
        var daily = ProducibleItem.Create(OrganizationId, "Frango executivo");
        var frozen = ProducibleItem.Create(OrganizationId, "Sopa");
        var configuration = FrozenConfiguration.Create(
            OrganizationId, frozenOffer.Id, frozen.Id, "400 ml", 400, MeasurementUnit.Milliliter, 24m);
        var order = Order.CreateDraft(OrganizationId, Guid.NewGuid(), OperationalDate,
        [
            new OrderItemDefinition(dailyOffer.Id, dailyOffer.FulfillmentMode, 2, 30m,
                ProducibleItemId: daily.Id, OfferName: dailyOffer.Name, ProducibleItemName: daily.Name),
            new OrderItemDefinition(frozenOffer.Id, frozenOffer.FulfillmentMode, 1, 24m,
                configuration.Id, frozen.Id, frozenOffer.Name, frozen.Name, configuration.Presentation),
        ], customerNameSnapshot: "Maria Silva");
        var dailyItem = order.Items.Single(item => item.FulfillmentMode == OfferFulfillmentMode.DailyProduction);
        var frozenItem = order.Items.Single(item => item.FulfillmentMode == OfferFulfillmentMode.FrozenStock);
        order.SnapshotComponent(dailyItem, Guid.NewGuid(), 1, "Arroz", 100, "g", string.Empty);
        order.AllocateFrozenStock(frozenItem, Guid.NewGuid(), 1);
        order.Confirm(ActorId, DateTimeOffset.UtcNow, $"confirm-{Guid.NewGuid():N}");
        return order;
    }

    private sealed class OperationsStoreFake : IOperationsStore
    {
        public IReadOnlyList<Order> Orders { get; init; } = [];
        public List<PackingRecord> Packings { get; } = [];
        public List<LabelPrintAttempt> Attempts { get; } = [];

        public Task<IReadOnlyList<Order>> GetOperationalOrdersAsync(DateOnly date, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Order>>(Orders.Where(item => item.OperationalDate == date).ToArray());
        public Task<Order?> FindOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
            Task.FromResult(Orders.SingleOrDefault(item => item.Id == orderId));
        public Task<PackingRecord?> FindPackingByOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
            Task.FromResult(Packings.SingleOrDefault(item => item.OrderId == orderId));
        public Task<PackingRecord?> FindPackingByKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(Packings.SingleOrDefault(item => item.IdempotencyKey == idempotencyKey));
        public Task<LabelPrintAttempt?> FindPrintAttemptByKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(Attempts.SingleOrDefault(item => item.IdempotencyKey == idempotencyKey));
        public Task<IReadOnlyList<PackingRecord>> GetPackingsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PackingRecord>>(Packings.Where(item => orderIds.Contains(item.OrderId)).ToArray());
        public Task<IReadOnlyList<LabelPrintAttempt>> GetPrintAttemptsAsync(IReadOnlyCollection<Guid> packingIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LabelPrintAttempt>>(Attempts.Where(item => packingIds.Contains(item.PackingRecordId)).ToArray());
        public Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(userIds.ToDictionary(item => item, _ => "Ana"));
        public Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) => operation(cancellationToken);
        public void Add(PackingRecord record) => Packings.Add(record);
        public void Add(LabelPrintAttempt attempt) => Attempts.Add(attempt);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
