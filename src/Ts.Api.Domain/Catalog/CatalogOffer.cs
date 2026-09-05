using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Catalog;

public sealed class CatalogOffer : ITenantOwned
{
    private CatalogOffer() { }

    private CatalogOffer(Guid id, Guid organizationId, string name, OfferFulfillmentMode fulfillmentMode,
        string? description, decimal basePrice, bool requiresMenuChoice)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        FulfillmentMode = fulfillmentMode;
        Description = NormalizeDescription(description);
        BasePrice = ValidatePrice(basePrice);
        RequiresMenuChoice = requiresMenuChoice;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public OfferFulfillmentMode FulfillmentMode { get; private set; }
    public string? Description { get; private set; }
    public decimal BasePrice { get; private set; }
    public bool RequiresMenuChoice { get; private set; }
    public bool IsActive { get; private set; }

    public static CatalogOffer Create(Guid organizationId, string name, OfferFulfillmentMode fulfillmentMode,
        string? description = null, decimal basePrice = 0, bool requiresMenuChoice = false)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A organização é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160)
        {
            throw new DomainException("O nome da oferta é obrigatório.");
        }

        if (!Enum.IsDefined(fulfillmentMode))
        {
            throw new DomainException("A modalidade de atendimento da oferta é inválida.");
        }

        return new CatalogOffer(Guid.NewGuid(), organizationId, name.Trim(), fulfillmentMode,
            description, basePrice, requiresMenuChoice);
    }

    public void Update(string name, string? description, decimal basePrice, bool requiresMenuChoice, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160)
            throw new DomainException("O nome da oferta deve possuir entre 1 e 160 caracteres.");
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Description = NormalizeDescription(description);
        BasePrice = ValidatePrice(basePrice);
        RequiresMenuChoice = requiresMenuChoice;
        IsActive = isActive;
    }

    private static string? NormalizeDescription(string? value)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > 4_000) throw new DomainException("A descrição da oferta deve possuir até 4.000 caracteres.");
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static decimal ValidatePrice(decimal value) => value >= 0
        ? decimal.Round(value, 2)
        : throw new DomainException("O preço base da oferta não pode ser negativo.");

    public void Deactivate() => IsActive = false;
}
