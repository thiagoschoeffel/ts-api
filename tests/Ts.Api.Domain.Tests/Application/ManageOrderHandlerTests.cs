using Ts.Api.Application.Common;
using Ts.Api.Application.Orders;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Production;

namespace Ts.Api.Domain.Tests.Application;

public sealed class ManageOrderHandlerTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    [Fact]
    public async Task Create_UsesAuthoritativeFrozenPriceAndPreservesSnapshots()
    {
        var dailyOffer = CatalogOffer.Create(
            OrganizationId,
            "Almoço do dia",
            OfferFulfillmentMode.DailyProduction);
        var frozenOffer = CatalogOffer.Create(
            OrganizationId,
            "Congelados",
            OfferFulfillmentMode.FrozenStock);
        var producible = ProducibleItem.Create(OrganizationId, "Lasanha integral");
        var configuration = FrozenConfiguration.Create(
            OrganizationId,
            frozenOffer.Id,
            producible.Id,
            "300 g",
            300,
            MeasurementUnit.Gram,
            24.90m);
        var store = new OrderManagementStoreFake(
            [dailyOffer, frozenOffer],
            [configuration],
            [producible]);
        var handler = new CreateOrderHandler(store, new OrganizationContextFake());
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new DateOnly(2026, 9, 5),
            [
                new OrderItemInput(dailyOffer.Id, 2, 18m, ProducibleItemId: producible.Id),
                new OrderItemInput(frozenOffer.Id, 3, FrozenConfigurationId: configuration.Id),
            ],
            "create-order-001");

        var result = await handler.HandleAsync(command, CancellationToken.None);
        var retry = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(result.Id, retry.Id);
        Assert.Equal(110.70m, result.TotalAmount);
        Assert.Equal(2, result.DailyCapacityUnits);
        var frozenItem = Assert.Single(
            result.Items,
            item => item.FulfillmentMode == OfferFulfillmentMode.FrozenStock);
        Assert.Equal(24.90m, frozenItem.UnitPrice);
        Assert.Equal("Congelados", frozenItem.OfferName);
        Assert.Equal("Lasanha integral", frozenItem.ProducibleItemName);
        Assert.Equal("300 g", frozenItem.FrozenPresentation);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Create_RejectsFrozenConfigurationFromAnotherOffer()
    {
        var requestedOffer = CatalogOffer.Create(
            OrganizationId,
            "Congelados A",
            OfferFulfillmentMode.FrozenStock);
        var configurationOffer = CatalogOffer.Create(
            OrganizationId,
            "Congelados B",
            OfferFulfillmentMode.FrozenStock);
        var producible = ProducibleItem.Create(OrganizationId, "Sopa");
        var configuration = FrozenConfiguration.Create(
            OrganizationId,
            configurationOffer.Id,
            producible.Id,
            "400 ml",
            400,
            MeasurementUnit.Milliliter,
            20m);
        var store = new OrderManagementStoreFake(
            [requestedOffer, configurationOffer],
            [configuration],
            [producible]);
        var handler = new CreateOrderHandler(store, new OrganizationContextFake());

        var exception = await Assert.ThrowsAsync<Ts.Api.Domain.Common.DomainException>(() =>
            handler.HandleAsync(
                new CreateOrderCommand(
                    Guid.NewGuid(),
                    new DateOnly(2026, 9, 5),
                    [new OrderItemInput(
                        requestedOffer.Id,
                        1,
                        FrozenConfigurationId: configuration.Id)],
                    "invalid-frozen"),
                CancellationToken.None));

        Assert.Equal("A configuração de congelado não pertence à oferta informada.", exception.Message);
        Assert.Empty(store.Orders);
    }

    [Fact]
    public async Task Edit_RejectsStaleVersionAndRetryDoesNotDuplicateChange()
    {
        var offer = CatalogOffer.Create(
            OrganizationId,
            "Almoço",
            OfferFulfillmentMode.DailyProduction);
        var order = Order.CreateDraft(
            OrganizationId,
            Guid.NewGuid(),
            new DateOnly(2026, 9, 5),
            [new OrderItemDefinition(offer.Id, offer.FulfillmentMode, 1, 10m, OfferName: offer.Name)],
            "create-editable");
        var producible = ProducibleItem.Create(OrganizationId, "Prato do dia");
        var store = new OrderManagementStoreFake([offer], [], [producible], [order]);
        var handler = new EditOrderHandler(store);
        var command = new EditOrderCommand(
            order.Id,
            order.CustomerId,
            order.OperationalDate,
            [new OrderItemInput(offer.Id, 2, 10m, ProducibleItemId: producible.Id)],
            0,
            "edit-001");

        var result = await handler.HandleAsync(command, CancellationToken.None);
        var retry = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(1, result.Version);
        Assert.Equal(result.Version, retry.Version);
        Assert.Equal(2, retry.DailyCapacityUnits);
        Assert.Equal(1, store.SaveCount);

        var reusedKeyException = await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            command with { Items = [new OrderItemInput(offer.Id, 3, 10m, ProducibleItemId: producible.Id)] },
            CancellationToken.None));
        Assert.Equal(
            "A chave de idempotência já foi usada para outro pedido ou conteúdo.",
            reusedKeyException.Message);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            command with { IdempotencyKey = "edit-stale", Items = [new OrderItemInput(offer.Id, 3, 10m, ProducibleItemId: producible.Id)] },
            CancellationToken.None));
        Assert.Equal("O pedido foi alterado. Recarregue os dados antes de editar.", exception.Message);
    }

    private sealed class OrganizationContextFake : IOrganizationContext
    {
        public bool IsAvailable => true;
        public Guid OrganizationId => ManageOrderHandlerTests.OrganizationId;
    }

    private sealed class OrderManagementStoreFake(
        IReadOnlyCollection<CatalogOffer> offers,
        IReadOnlyCollection<FrozenConfiguration> configurations,
        IReadOnlyCollection<ProducibleItem> producibleItems,
        IReadOnlyCollection<Order>? initialOrders = null) : IOrderManagementStore
    {
        public List<Order> Orders { get; } = initialOrders?.ToList() ?? [];
        public int SaveCount { get; private set; }

        public async Task<T> ExecuteSerializableAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken) => await operation(cancellationToken);

        public Task<Order?> FindByCreationKeyAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(Orders.SingleOrDefault(item => item.CreationIdempotencyKey == key));

        public Task<Order?> FindByModificationKeyAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(Orders.SingleOrDefault(item => item.LastModificationIdempotencyKey == key));

        public Task<Order?> FindOrderAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Orders.SingleOrDefault(item => item.Id == id));

        public Task<CatalogOffer?> FindActiveOfferAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(offers.SingleOrDefault(item => item.Id == id && item.IsActive));

        public Task<FrozenConfiguration?> FindActiveFrozenConfigurationAsync(
            Guid id,
            CancellationToken cancellationToken) => Task.FromResult(
            configurations.SingleOrDefault(item => item.Id == id && item.IsActive));

        public Task<ProducibleItem?> FindActiveProducibleItemAsync(
            Guid id,
            CancellationToken cancellationToken) => Task.FromResult(
            producibleItems.SingleOrDefault(item => item.Id == id && item.IsActive));

        public Task AddAsync(Order order, CancellationToken cancellationToken)
        {
            Orders.Add(order);
            return Task.CompletedTask;
        }

        public void ReplaceItems(
            IReadOnlyCollection<OrderItem> previousItems,
            IReadOnlyCollection<OrderItem> replacementItems)
        {
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
