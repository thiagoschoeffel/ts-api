using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Catalog;

public sealed class CatalogOffer
{
    private CatalogOffer() { }

    private CatalogOffer(Guid id, string name, OfferFulfillmentMode fulfillmentMode)
    {
        Id = id;
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        FulfillmentMode = fulfillmentMode;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public OfferFulfillmentMode FulfillmentMode { get; private set; }
    public bool IsActive { get; private set; }

    public static CatalogOffer Create(string name, OfferFulfillmentMode fulfillmentMode)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("O nome da oferta é obrigatório.");
        }

        if (!Enum.IsDefined(fulfillmentMode))
        {
            throw new DomainException("A modalidade de atendimento da oferta é inválida.");
        }

        return new CatalogOffer(Guid.NewGuid(), name.Trim(), fulfillmentMode);
    }

    public void Deactivate() => IsActive = false;
}
