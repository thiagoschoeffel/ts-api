using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Orders;

public sealed class Order : ITenantOwned
{
    private readonly List<OrderItem> _items = [];
    private readonly List<FrozenStockAllocation> _frozenAllocations = [];
    private readonly List<OrderCharge> _charges = [];

    private Order() { }

    private Order(
        Guid organizationId,
        Guid customerId,
        DateOnly operationalDate,
        string creationIdempotencyKey)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CustomerId = customerId;
        OperationalDate = operationalDate;
        Status = OrderStatus.Open;
        CreationIdempotencyKey = creationIdempotencyKey;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public DateOnly OperationalDate { get; private set; }
    public OrderStatus Status { get; private set; }
    public long Version { get; private set; }
    public Guid? ConfirmedBy { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public string? ConfirmationIdempotencyKey { get; private set; }
    public string CreationIdempotencyKey { get; private set; } = string.Empty;
    public string? LastModificationIdempotencyKey { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<FrozenStockAllocation> FrozenAllocations => _frozenAllocations.AsReadOnly();
    public IReadOnlyCollection<OrderCharge> Charges => _charges.AsReadOnly();
    public int DailyCapacityUnits => _items
        .Where(item => item.FulfillmentMode == OfferFulfillmentMode.DailyProduction)
        .Sum(item => item.Quantity);
    public decimal TotalAmount => _items.Sum(item => item.Total);

    public static Order CreateDraft(
        Guid organizationId,
        Guid customerId,
        DateOnly operationalDate,
        IReadOnlyCollection<OrderItemDefinition> items,
        string? creationIdempotencyKey = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A organização é obrigatória.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException("O cliente é obrigatório.");
        }

        if (items.Count == 0)
        {
            throw new DomainException("O pedido deve possuir ao menos um item.");
        }

        var normalizedKey = creationIdempotencyKey is null
            ? $"internal-{Guid.NewGuid():N}"
            : NormalizeIdempotencyKey(creationIdempotencyKey);
        var order = new Order(organizationId, customerId, operationalDate, normalizedKey);
        order.ReplaceItems(items);

        return order;
    }

    public void EditDraft(
        Guid customerId,
        DateOnly operationalDate,
        IReadOnlyCollection<OrderItemDefinition> items,
        string idempotencyKey)
    {
        if (Status != OrderStatus.Open)
        {
            throw new DomainException("Somente um pedido aberto pode ser alterado.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException("O cliente é obrigatório.");
        }

        if (items.Count == 0)
        {
            throw new DomainException("O pedido deve possuir ao menos um item.");
        }

        CustomerId = customerId;
        OperationalDate = operationalDate;
        ReplaceItems(items);
        LastModificationIdempotencyKey = NormalizeIdempotencyKey(idempotencyKey);
        Version++;
    }

    public FrozenStockAllocation AllocateFrozenStock(OrderItem item, Guid frozenLotId, int quantity)
    {
        if (Status != OrderStatus.Open || !_items.Contains(item))
        {
            throw new DomainException("O estoque só pode ser alocado a um item do pedido aberto.");
        }

        if (item.FulfillmentMode != OfferFulfillmentMode.FrozenStock
            || item.FrozenConfigurationId is not Guid frozenConfigurationId)
        {
            throw new DomainException("O item informado não é atendido por estoque congelado.");
        }

        var allocated = _frozenAllocations
            .Where(allocation => allocation.OrderItemId == item.Id)
            .Sum(allocation => allocation.Quantity);
        if (allocated + quantity > item.Quantity)
        {
            throw new DomainException("A alocação de congelados excede a quantidade do item.");
        }

        var allocation = new FrozenStockAllocation(
            OrganizationId,
            Id,
            item.Id,
            frozenConfigurationId,
            frozenLotId,
            quantity);
        _frozenAllocations.Add(allocation);
        return allocation;
    }

    public void Confirm(Guid actorId, DateTimeOffset confirmedAt, string idempotencyKey)
    {
        if (Status != OrderStatus.Open)
        {
            throw new DomainException("Somente um pedido aberto pode ser confirmado.");
        }

        if (actorId == Guid.Empty)
        {
            throw new DomainException("O responsável pela confirmação é obrigatório.");
        }

        var normalizedKey = idempotencyKey.Trim();
        if (normalizedKey.Length is 0 or > 200)
        {
            throw new DomainException("A chave de idempotência deve possuir entre 1 e 200 caracteres.");
        }

        foreach (var item in _items.Where(item => item.FulfillmentMode == OfferFulfillmentMode.FrozenStock))
        {
            var allocated = _frozenAllocations
                .Where(allocation => allocation.OrderItemId == item.Id)
                .Sum(allocation => allocation.Quantity);
            if (allocated != item.Quantity)
            {
                throw new DomainException("Todo item congelado deve estar integralmente alocado antes da confirmação.");
            }
        }

        if (TotalAmount > 0)
        {
            _charges.Add(new OrderCharge(OrganizationId, Id, TotalAmount, OperationalDate, confirmedAt));
        }

        Status = OrderStatus.Confirmed;
        ConfirmedBy = actorId;
        ConfirmedAt = confirmedAt;
        ConfirmationIdempotencyKey = normalizedKey;
        Version++;
    }

    private void ReplaceItems(IReadOnlyCollection<OrderItemDefinition> items)
    {
        _items.Clear();
        foreach (var item in items)
        {
            _items.Add(new OrderItem(
                OrganizationId,
                Id,
                item.OfferId,
                item.FulfillmentMode,
                item.Quantity,
                item.UnitPrice,
                item.FrozenConfigurationId,
                item.OfferName,
                item.ProducibleItemName,
                item.FrozenPresentation));
        }
    }

    private static string NormalizeIdempotencyKey(string idempotencyKey)
    {
        var normalized = idempotencyKey?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 200)
        {
            throw new DomainException("A chave de idempotência deve possuir entre 1 e 200 caracteres.");
        }

        return normalized;
    }
}

public sealed record OrderItemDefinition(
    Guid OfferId,
    OfferFulfillmentMode FulfillmentMode,
    int Quantity,
    decimal UnitPrice,
    Guid? FrozenConfigurationId = null,
    string OfferName = "",
    string? ProducibleItemName = null,
    string? FrozenPresentation = null);
