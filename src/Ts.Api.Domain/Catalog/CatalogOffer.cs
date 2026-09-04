using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Catalog;

public sealed class CatalogOffer : ITenantOwned
{
    private CatalogOffer() { }

    private CatalogOffer(Guid id, Guid organizationId, string name, OfferFulfillmentMode fulfillmentMode)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        FulfillmentMode = fulfillmentMode;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public OfferFulfillmentMode FulfillmentMode { get; private set; }
    public bool IsActive { get; private set; }

    public static CatalogOffer Create(Guid organizationId, string name, OfferFulfillmentMode fulfillmentMode)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A organização é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("O nome da oferta é obrigatório.");
        }

        if (!Enum.IsDefined(fulfillmentMode))
        {
            throw new DomainException("A modalidade de atendimento da oferta é inválida.");
        }

        return new CatalogOffer(Guid.NewGuid(), organizationId, name.Trim(), fulfillmentMode);
    }

    public void Deactivate() => IsActive = false;
}
