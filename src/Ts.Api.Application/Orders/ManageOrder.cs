using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Production;

namespace Ts.Api.Application.Orders;

public interface IOrderManagementStore
{
    Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);

    Task<Order?> FindByCreationKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<Order?> FindByModificationKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<Order?> FindOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<CatalogOffer?> FindActiveOfferAsync(Guid offerId, CancellationToken cancellationToken);
    Task<FrozenConfiguration?> FindActiveFrozenConfigurationAsync(
        Guid configurationId,
        CancellationToken cancellationToken);
    Task<ProducibleItem?> FindActiveProducibleItemAsync(
        Guid producibleItemId,
        CancellationToken cancellationToken);
    Task AddAsync(Order order, CancellationToken cancellationToken);
    void ReplaceItems(
        IReadOnlyCollection<OrderItem> previousItems,
        IReadOnlyCollection<OrderItem> replacementItems);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class CreateOrderHandler(
    IOrderManagementStore store,
    IOrganizationContext organizationContext)
{
    public Task<OrderResult> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = ValidateIdempotencyKey(command.IdempotencyKey);
        return store.ExecuteSerializableAsync(
            token => CreateAsync(command, idempotencyKey, token),
            cancellationToken);
    }

    private async Task<OrderResult> CreateAsync(
        CreateOrderCommand command,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var previous = await store.FindByCreationKeyAsync(idempotencyKey, cancellationToken);
        if (previous is not null)
        {
            if (!OrderResultMapper.MatchesInput(
                    previous,
                    command.CustomerId,
                    command.OperationalDate,
                    command.Items,
                    command.CustomerName))
            {
                throw new ConflictException("A chave de idempotência já foi usada com outro conteúdo de pedido.");
            }

            return OrderResultMapper.Map(previous);
        }

        var definitions = await OrderItemResolver.ResolveAsync(store, command.Items, cancellationToken);
        var order = Order.CreateDraft(
            organizationContext.OrganizationId,
            command.CustomerId,
            command.OperationalDate,
            definitions,
            idempotencyKey,
            command.CustomerName);
        await store.AddAsync(order, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return OrderResultMapper.Map(order);
    }

    internal static string ValidateIdempotencyKey(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 200)
        {
            throw new DomainException("A chave de idempotência deve possuir entre 1 e 200 caracteres.");
        }

        return normalized;
    }
}

public sealed class EditOrderHandler(IOrderManagementStore store)
{
    public Task<OrderResult> HandleAsync(EditOrderCommand command, CancellationToken cancellationToken)
    {
        var idempotencyKey = CreateOrderHandler.ValidateIdempotencyKey(command.IdempotencyKey);
        return store.ExecuteSerializableAsync(
            token => EditAsync(command, idempotencyKey, token),
            cancellationToken);
    }

    private async Task<OrderResult> EditAsync(
        EditOrderCommand command,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var previous = await store.FindByModificationKeyAsync(idempotencyKey, cancellationToken);
        if (previous is not null)
        {
            if (previous.Id != command.OrderId
                || !OrderResultMapper.MatchesInput(
                    previous,
                    command.CustomerId,
                    command.OperationalDate,
                    command.Items,
                    command.CustomerName))
            {
                throw new ConflictException(
                    "A chave de idempotência já foi usada para outro pedido ou conteúdo.");
            }

            return OrderResultMapper.Map(previous);
        }

        var order = await store.FindOrderAsync(command.OrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Pedido não encontrado.");
        if (order.Status != OrderStatus.Open)
        {
            throw new ConflictException("Somente um pedido aberto pode ser alterado.");
        }

        if (order.Version != command.ExpectedVersion)
        {
            throw new ConflictException("O pedido foi alterado. Recarregue os dados antes de editar.");
        }

        var definitions = await OrderItemResolver.ResolveAsync(store, command.Items, cancellationToken);
        var previousItems = order.Items.ToArray();
        order.EditDraft(command.CustomerId, command.OperationalDate, definitions, idempotencyKey, command.CustomerName);
        store.ReplaceItems(previousItems, order.Items);
        await store.SaveChangesAsync(cancellationToken);
        return OrderResultMapper.Map(order);
    }
}

public sealed class GetOrderHandler(IOrderManagementStore store)
{
    public async Task<OrderResult> HandleAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await store.FindOrderAsync(orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Pedido não encontrado.");
        return OrderResultMapper.Map(order);
    }
}

internal static class OrderItemResolver
{
    public static async Task<IReadOnlyCollection<OrderItemDefinition>> ResolveAsync(
        IOrderManagementStore store,
        IReadOnlyCollection<OrderItemInput> inputs,
        CancellationToken cancellationToken)
    {
        if (inputs.Count == 0)
        {
            throw new DomainException("O pedido deve possuir ao menos um item.");
        }

        var definitions = new List<OrderItemDefinition>(inputs.Count);
        foreach (var input in inputs)
        {
            var offer = await store.FindActiveOfferAsync(input.OfferId, cancellationToken)
                ?? throw new DomainException("A oferta informada não existe ou está inativa.");

            if (offer.FulfillmentMode == OfferFulfillmentMode.DailyProduction)
            {
                if (input.FrozenConfigurationId is not null)
                {
                    throw new DomainException("Uma oferta da produção diária não aceita configuração de congelado.");
                }

                if (input.UnitPrice is not decimal dailyPrice)
                {
                    throw new DomainException("O preço unitário é obrigatório para uma oferta da produção diária.");
                }

                if (input.ProducibleItemId is not Guid producibleItemId)
                {
                    throw new DomainException("O item produzível é obrigatório para uma oferta da produção diária.");
                }

                var dailyProducibleItem = await store.FindActiveProducibleItemAsync(
                    producibleItemId, cancellationToken)
                    ?? throw new DomainException("O item produzível informado não existe ou está inativo.");

                definitions.Add(new OrderItemDefinition(
                    offer.Id,
                    offer.FulfillmentMode,
                    input.Quantity,
                    dailyPrice,
                    ProducibleItemId: dailyProducibleItem.Id,
                    ProducibleItemName: dailyProducibleItem.Name,
                    OfferName: offer.Name));
                continue;
            }

            if (input.UnitPrice is not null)
            {
                throw new DomainException("O preço do congelado é definido pela configuração autoritativa.");
            }

            if (input.ProducibleItemId is not null)
            {
                throw new DomainException("O item produzível do congelado é definido pela configuração autoritativa.");
            }

            if (input.FrozenConfigurationId is not Guid frozenConfigurationId)
            {
                throw new DomainException("Uma oferta de congelados exige uma configuração de congelado.");
            }

            var configuration = await store.FindActiveFrozenConfigurationAsync(
                frozenConfigurationId,
                cancellationToken)
                ?? throw new DomainException("A configuração de congelado não existe ou está inativa.");
            if (configuration.OfferId != offer.Id)
            {
                throw new DomainException("A configuração de congelado não pertence à oferta informada.");
            }

            var producibleItem = await store.FindActiveProducibleItemAsync(
                configuration.ProducibleItemId,
                cancellationToken)
                ?? throw new DomainException("O item produzível da configuração não existe ou está inativo.");
            definitions.Add(new OrderItemDefinition(
                offer.Id,
                offer.FulfillmentMode,
                input.Quantity,
                configuration.UnitPrice,
                configuration.Id,
                configuration.ProducibleItemId,
                offer.Name,
                producibleItem.Name,
                configuration.Presentation));
        }

        return definitions;
    }
}

internal static class OrderResultMapper
{
    public static bool MatchesInput(
        Order order,
        Guid customerId,
        DateOnly operationalDate,
        IReadOnlyCollection<OrderItemInput> inputs,
        string? customerName = null)
    {
        if (order.CustomerId != customerId
            || order.OperationalDate != operationalDate
            || customerName is not null && order.CustomerNameSnapshot != customerName.Trim()
            || order.Items.Count != inputs.Count)
        {
            return false;
        }

        return order.Items.Zip(inputs).All(pair =>
            pair.First.OfferId == pair.Second.OfferId
            && pair.First.Quantity == pair.Second.Quantity
            && pair.First.FrozenConfigurationId == pair.Second.FrozenConfigurationId
            && (pair.First.FulfillmentMode == OfferFulfillmentMode.FrozenStock
                ? pair.Second.ProducibleItemId is null
                : pair.First.ProducibleItemId == pair.Second.ProducibleItemId)
            && (pair.First.FulfillmentMode == OfferFulfillmentMode.FrozenStock
                ? pair.Second.UnitPrice is null
                : pair.First.UnitPrice == pair.Second.UnitPrice));
    }

    public static IReadOnlyCollection<OrderItemResult> MapItems(Order order) => order.Items.Select(item => new OrderItemResult(
        item.Id,
        item.OfferId,
        item.OfferName,
        item.FulfillmentMode,
        item.Quantity,
        item.UnitPrice,
        item.Total,
        item.FrozenConfigurationId,
        item.ProducibleItemId,
        item.ProducibleItemName,
        item.FrozenPresentation)).ToArray();

    public static OrderResult Map(Order order) => new(
        order.Id,
        order.CustomerId,
        order.OperationalDate,
        order.Status,
        order.Version,
        order.DailyCapacityUnits,
        order.TotalAmount,
        MapItems(order));
}
