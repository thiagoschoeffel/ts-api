using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Catalog;

public sealed class ComponentType : ITenantOwned
{
    private ComponentType() { }
    private ComponentType(Guid organizationId, string name, string? description)
    {
        Id = Guid.NewGuid(); OrganizationId = organizationId; Name = Required(name, 120);
        NormalizedName = Name.ToUpperInvariant(); Description = Optional(description, 1_000); IsActive = true;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public static ComponentType Create(Guid organizationId, string name, string? description = null) =>
        organizationId == Guid.Empty ? throw new DomainException("A organização é obrigatória.") : new(organizationId, name, description);
    public void Update(string name, string? description, bool active)
    { Name = Required(name, 120); NormalizedName = Name.ToUpperInvariant(); Description = Optional(description, 1_000); IsActive = active; }
    private static string Required(string value, int max) { var result = value?.Trim() ?? ""; return result.Length is > 0 and <= 120 ? result : throw new DomainException("Nome inválido."); }
    private static string? Optional(string? value, int max) { var result = value?.Trim(); return result?.Length <= max ? result : throw new DomainException("Descrição inválida."); }
}

public sealed class CatalogAddon : ITenantOwned
{
    private CatalogAddon() { }
    private CatalogAddon(Guid organizationId, string name, decimal price, Guid? producibleItemId, decimal? quantity, string? unit)
    { Id = Guid.NewGuid(); OrganizationId = organizationId; Apply(name, price, producibleItemId, quantity, unit, true); }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public Guid? ProducibleItemId { get; private set; }
    public decimal? OperationalQuantity { get; private set; }
    public string? MeasurementUnit { get; private set; }
    public bool IsActive { get; private set; }
    public static CatalogAddon Create(Guid organizationId, string name, decimal price, Guid? producibleItemId,
        decimal? quantity, string? unit) => organizationId == Guid.Empty
        ? throw new DomainException("A organização é obrigatória.") : new(organizationId, name, price, producibleItemId, quantity, unit);
    public void Update(string name, decimal price, Guid? producibleItemId, decimal? quantity, string? unit, bool active) =>
        Apply(name, price, producibleItemId, quantity, unit, active);
    private void Apply(string name, decimal price, Guid? producibleItemId, decimal? quantity, string? unit, bool active)
    {
        var normalized = name?.Trim() ?? "";
        if (normalized.Length is 0 or > 160 || price < 0) throw new DomainException("Nome e preço válido são obrigatórios no adicional.");
        if (quantity.HasValue != !string.IsNullOrWhiteSpace(unit) || quantity <= 0) throw new DomainException("Quantidade e unidade operacionais devem ser informadas juntas.");
        Name = normalized; NormalizedName = normalized.ToUpperInvariant(); Price = decimal.Round(price, 2);
        ProducibleItemId = producibleItemId; OperationalQuantity = quantity; MeasurementUnit = unit?.Trim(); IsActive = active;
    }
}

public sealed class CatalogOfferVersion : ITenantOwned
{
    private CatalogOfferVersion() { }
    private CatalogOfferVersion(Guid organizationId, Guid offerId, int version, string configurationJson, DateTimeOffset createdAt)
    { Id = Guid.NewGuid(); OrganizationId = organizationId; OfferId = offerId; Version = version; ConfigurationJson = configurationJson; CreatedAt = createdAt; }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid OfferId { get; private set; }
    public int Version { get; private set; }
    public string ConfigurationJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }
    public static CatalogOfferVersion Create(Guid organizationId, Guid offerId, int version, string json, DateTimeOffset at)
    {
        if (organizationId == Guid.Empty || offerId == Guid.Empty || version <= 0 || string.IsNullOrWhiteSpace(json))
            throw new DomainException("A versão da oferta é inválida.");
        return new(organizationId, offerId, version, json, at);
    }
}
