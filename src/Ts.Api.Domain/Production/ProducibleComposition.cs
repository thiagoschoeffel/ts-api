using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Production;

public sealed class ProducibleComposition : ITenantOwned
{
    private readonly List<ProducibleComponent> _components = [];
    private ProducibleComposition() { }

    private ProducibleComposition(
        Guid organizationId,
        Guid producibleItemId,
        int version,
        DateTimeOffset publishedAt,
        IReadOnlyCollection<ProducibleComponentDefinition> components)
    {
        if (components.Count == 0)
        {
            throw new DomainException("A composição deve possuir ao menos um componente.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProducibleItemId = producibleItemId;
        Version = version;
        PublishedAt = publishedAt;
        foreach (var component in components)
        {
            _components.Add(ProducibleComponent.Create(organizationId, Id, component));
        }
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ProducibleItemId { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
    public IReadOnlyCollection<ProducibleComponent> Components => _components.AsReadOnly();

    public static ProducibleComposition Publish(
        Guid organizationId,
        Guid producibleItemId,
        int version,
        DateTimeOffset publishedAt,
        IReadOnlyCollection<ProducibleComponentDefinition> components)
    {
        if (organizationId == Guid.Empty || producibleItemId == Guid.Empty)
        {
            throw new DomainException("A organização e o item produzível são obrigatórios.");
        }

        if (version <= 0)
        {
            throw new DomainException("A versão da composição deve ser positiva.");
        }

        return new ProducibleComposition(
            organizationId, producibleItemId, version, publishedAt, components);
    }
}

public sealed class ProducibleComponent : ITenantOwned
{
    private ProducibleComponent() { }

    private ProducibleComponent(
        Guid organizationId,
        Guid compositionId,
        string name,
        decimal quantity,
        string measurementUnit,
        string dietaryMarkers,
        Guid? referencedProducibleItemId,
        string kind)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CompositionId = compositionId;
        Name = name;
        Quantity = quantity;
        MeasurementUnit = measurementUnit;
        DietaryMarkers = dietaryMarkers;
        ReferencedProducibleItemId = referencedProducibleItemId;
        Kind = kind;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CompositionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string MeasurementUnit { get; private set; } = string.Empty;
    public string DietaryMarkers { get; private set; } = string.Empty;
    public Guid? ReferencedProducibleItemId { get; private set; }
    public string Kind { get; private set; } = "Ingredient";
    public IReadOnlyCollection<string> Markers => DietaryMarkers
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal static ProducibleComponent Create(
        Guid organizationId,
        Guid compositionId,
        ProducibleComponentDefinition definition)
    {
        var name = definition.Name?.Trim() ?? string.Empty;
        var unit = definition.MeasurementUnit?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 160 || unit.Length is 0 or > 30 || definition.Quantity <= 0)
        {
            throw new DomainException("Nome, quantidade positiva e unidade válida são obrigatórios no componente.");
        }

        var markers = string.Join(',', definition.DietaryMarkers
            .Select(NormalizeMarker)
            .Where(marker => marker.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(marker => marker, StringComparer.Ordinal));
        var kind = definition.Kind?.Trim() ?? "Ingredient";
        if (kind is not ("Ingredient" or "ProducibleItem")
            || (kind == "ProducibleItem") != definition.ReferencedProducibleItemId.HasValue)
            throw new DomainException("O tipo e a referência do componente são incompatíveis.");
        return new ProducibleComponent(
            organizationId, compositionId, name, definition.Quantity, unit, markers,
            definition.ReferencedProducibleItemId, kind);
    }

    public static string NormalizeMarker(string marker) => (marker ?? string.Empty).Trim().ToUpperInvariant();
}

public sealed record ProducibleComponentDefinition(
    string Name,
    decimal Quantity,
    string MeasurementUnit,
    IReadOnlyCollection<string> DietaryMarkers,
    Guid? ReferencedProducibleItemId = null,
    string Kind = "Ingredient");
