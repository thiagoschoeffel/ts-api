using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Menus;
using Ts.Api.Domain.Production;

namespace Ts.Api.Application.Orders;

public sealed record OrderListItemResult(
    Guid Id,
    Guid CustomerId,
    DateOnly OperationalDate,
    OrderStatus Status,
    long Version,
    int ItemCount,
    int DailyCapacityUnits,
    decimal TotalAmount);

public sealed record OrderConfirmationEffectsResult(
    DateTimeOffset ConfirmedAt,
    decimal Subtotal,
    decimal PlanCreditCoveredAmount,
    decimal DiscountAmount,
    string? DiscountReason,
    decimal DeliveryFee,
    decimal FinancialCreditApplied,
    decimal AmountDue,
    IReadOnlyCollection<FrozenAllocation> FrozenAllocations,
    IReadOnlyCollection<ComponentSnapshot> Components,
    IReadOnlyCollection<PlanCreditAllocationResult> PlanCredits);

public sealed record OrderLifecycleEventResult(
    Guid Id,
    OrderLifecycleEventType Type,
    OrderStatus PreviousStatus,
    OrderStatus NewStatus,
    DateOnly PreviousOperationalDate,
    DateOnly NewOperationalDate,
    string Reason,
    DateTimeOffset OccurredAt,
    CommercialCancellationDisposition CommercialDisposition,
    FrozenCancellationDisposition FrozenDisposition,
    int CapacityUnitsReleased,
    int PlanCreditsReversed,
    decimal FinancialCreditReversed,
    int ChargesCancelled);

public sealed record OrderDetailsResult(
    Guid Id,
    Guid CustomerId,
    DateOnly OperationalDate,
    OrderStatus Status,
    long Version,
    int DailyCapacityUnits,
    decimal TotalAmount,
    IReadOnlyCollection<OrderItemResult> Items,
    OrderConfirmationEffectsResult? Confirmation,
    IReadOnlyCollection<OrderLifecycleEventResult> Lifecycle);

public sealed record OrderAuthoringOfferResult(
    Guid Id,
    string Name,
    OfferFulfillmentMode FulfillmentMode,
    decimal? EffectivePrice = null,
    bool RequiresMenuChoice = false);

public sealed record OrderAuthoringProducibleResult(Guid Id, string Name);
public sealed record OrderAuthoringMenuOptionResult(Guid Id, string Category, Guid ProducibleItemId,
    string ProducibleItemName, MenuAvailability Availability);

public sealed record OrderAuthoringFrozenConfigurationResult(
    Guid Id,
    Guid OfferId,
    Guid ProducibleItemId,
    string ProducibleItemName,
    string Presentation,
    decimal UnitPrice,
    int AvailableQuantity,
    DateOnly? NextExpiration);

public sealed record OrderAuthoringContextResult(
    IReadOnlyCollection<OrderAuthoringOfferResult> Offers,
    IReadOnlyCollection<OrderAuthoringProducibleResult> Producibles,
    IReadOnlyCollection<OrderAuthoringFrozenConfigurationResult> FrozenConfigurations,
    IReadOnlyCollection<OrderAuthoringMenuOptionResult>? MenuOptions = null);

public interface IOrderQueryStore
{
    Task<IReadOnlyList<Order>> GetOrdersAsync(CancellationToken cancellationToken);
    Task<Order?> FindOrderDetailsAsync(Guid orderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogOffer>> GetActiveOffersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProducibleItem>> GetActiveProduciblesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FrozenConfiguration>> GetActiveFrozenConfigurationsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FrozenLot>> GetSellableFrozenLotsAsync(DateOnly sellableOn, CancellationToken cancellationToken);
    Task<DailyMenu?> GetPublishedMenuAsync(DateOnly date, CancellationToken cancellationToken) => Task.FromResult<DailyMenu?>(null);
    Task<bool> EnforcesPublishedMenusAsync(CancellationToken cancellationToken) => Task.FromResult(false);
}

public sealed class ListOrdersHandler(IOrderQueryStore store)
{
    public async Task<IReadOnlyCollection<OrderListItemResult>> HandleAsync(CancellationToken cancellationToken) =>
        (await store.GetOrdersAsync(cancellationToken))
        .Select(order => new OrderListItemResult(
            order.Id, order.CustomerId, order.OperationalDate, order.Status, order.Version,
            order.Items.Sum(item => item.Quantity), order.DailyCapacityUnits, order.TotalAmount))
        .ToArray();
}

public sealed class GetOrderDetailsHandler(IOrderQueryStore store)
{
    public async Task<OrderDetailsResult> HandleAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await store.FindOrderDetailsAsync(orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Pedido não encontrado.");
        var confirmation = order.ConfirmationAudits.OrderByDescending(item => item.OccurredAt).FirstOrDefault();
        return new OrderDetailsResult(
            order.Id, order.CustomerId, order.OperationalDate, order.Status, order.Version,
            order.DailyCapacityUnits, order.TotalAmount,
            OrderResultMapper.MapItems(order),
            confirmation is null ? null : new OrderConfirmationEffectsResult(
                confirmation.OccurredAt, confirmation.Subtotal, confirmation.PlanCreditCoveredAmount,
                confirmation.DiscountAmount, confirmation.DiscountReason, confirmation.DeliveryFee,
                confirmation.FinancialCreditApplied, confirmation.AmountDue,
                order.FrozenAllocations.Select(item => new FrozenAllocation(
                    item.OrderItemId, item.FrozenConfigurationId, item.FrozenLotId, item.Quantity)).ToArray(),
                order.ComponentSnapshots.Select(item => new ComponentSnapshot(
                    item.OrderItemId, item.CompositionId, item.CompositionVersion, item.Name,
                    item.QuantityPerUnit, item.TotalQuantity, item.MeasurementUnit,
                    item.DietaryMarkers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))).ToArray(),
                order.PlanCreditAllocations.Select(item => new PlanCreditAllocationResult(
                    item.OrderItemId, item.AcquisitionId, item.PlanName, item.Quantity, item.CoveredAmount)).ToArray()),
            order.LifecycleEvents.OrderByDescending(item => item.OccurredAt).Select(item =>
                new OrderLifecycleEventResult(
                    item.Id, item.Type, item.PreviousStatus, item.NewStatus,
                    item.PreviousOperationalDate, item.NewOperationalDate, item.Reason, item.OccurredAt,
                    item.CommercialDisposition, item.FrozenDisposition, item.CapacityUnitsReleased,
                    item.PlanCreditsReversed, item.FinancialCreditReversed, item.ChargesCancelled)).ToArray());
    }
}

public sealed class GetOrderAuthoringContextHandler(IOrderQueryStore store)
{
    public async Task<OrderAuthoringContextResult> HandleAsync(DateOnly sellableOn, CancellationToken cancellationToken)
    {
        var offers = await store.GetActiveOffersAsync(cancellationToken);
        var producibles = await store.GetActiveProduciblesAsync(cancellationToken);
        var configurations = await store.GetActiveFrozenConfigurationsAsync(cancellationToken);
        var lots = await store.GetSellableFrozenLotsAsync(sellableOn, cancellationToken);
        var producibleNames = producibles.ToDictionary(item => item.Id, item => item.Name);
        var menu = await store.GetPublishedMenuAsync(sellableOn, cancellationToken);
        var enforcesPublishedMenus = await store.EnforcesPublishedMenusAsync(cancellationToken);
        var dailyOffers = menu?.Offers.Where(x => x.Availability == MenuAvailability.Available).ToDictionary(x => x.OfferId);
        var visibleOffers = menu is null && !enforcesPublishedMenus ? offers
            : offers.Where(x => x.FulfillmentMode == OfferFulfillmentMode.FrozenStock || (dailyOffers?.ContainsKey(x.Id) ?? false)).ToArray();

        return new OrderAuthoringContextResult(
            visibleOffers.Select(item => new OrderAuthoringOfferResult(item.Id, item.Name, item.FulfillmentMode,
                dailyOffers?.GetValueOrDefault(item.Id)?.EffectivePrice, item.RequiresMenuChoice)).ToArray(),
            producibles.Select(item => new OrderAuthoringProducibleResult(item.Id, item.Name)).ToArray(),
            configurations.Select(configuration =>
            {
                var eligibleLots = lots.Where(lot => lot.FrozenConfigurationId == configuration.Id).ToArray();
                return new OrderAuthoringFrozenConfigurationResult(
                    configuration.Id, configuration.OfferId, configuration.ProducibleItemId,
                    producibleNames.GetValueOrDefault(configuration.ProducibleItemId, "Item produzível indisponível"),
                    configuration.Presentation, configuration.UnitPrice,
                    eligibleLots.Sum(lot => lot.Balance),
                    eligibleLots.Where(lot => lot.Balance > 0).MinBy(lot => lot.ExpiresOn)?.ExpiresOn);
            }).ToArray(),
            menu?.Options.Select(option => new OrderAuthoringMenuOptionResult(option.Id, option.Category,
                option.ProducibleItemId, producibleNames.GetValueOrDefault(option.ProducibleItemId, "Item indisponível"), option.Availability)).ToArray() ?? []);
    }
}
