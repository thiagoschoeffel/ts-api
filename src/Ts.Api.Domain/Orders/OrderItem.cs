using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Orders;

public sealed class OrderItem : ITenantOwned
{
    private OrderItem() { }

    internal OrderItem(
        Guid organizationId,
        Guid orderId,
        Guid offerId,
        OfferFulfillmentMode fulfillmentMode,
        int quantity,
        decimal unitPrice,
        Guid? frozenConfigurationId)
    {
        if (offerId == Guid.Empty)
        {
            throw new DomainException("A oferta do item é obrigatória.");
        }

        if (!Enum.IsDefined(fulfillmentMode))
        {
            throw new DomainException("A modalidade de atendimento do item é inválida.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("A quantidade do item deve ser positiva.");
        }

        if (unitPrice < 0)
        {
            throw new DomainException("O preço unitário do item não pode ser negativo.");
        }

        if (fulfillmentMode == OfferFulfillmentMode.FrozenStock && frozenConfigurationId is null)
        {
            throw new DomainException("Um item congelado deve indicar a configuração de congelado.");
        }

        if (fulfillmentMode == OfferFulfillmentMode.DailyProduction && frozenConfigurationId is not null)
        {
            throw new DomainException("Um item da produção diária não pode indicar configuração de congelado.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        OrderId = orderId;
        OfferId = offerId;
        FulfillmentMode = fulfillmentMode;
        Quantity = quantity;
        UnitPrice = unitPrice;
        FrozenConfigurationId = frozenConfigurationId;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OfferId { get; private set; }
    public OfferFulfillmentMode FulfillmentMode { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public Guid? FrozenConfigurationId { get; private set; }
    public decimal Total => UnitPrice * Quantity;
}
