using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Production;

public sealed class ProducibleItem : ITenantOwned
{
    private ProducibleItem() { }

    private ProducibleItem(Guid id, Guid organizationId, string name, string? description, string category, string measurementUnit)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        Description = description;
        Category = category;
        MeasurementUnit = measurementUnit;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string MeasurementUnit { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public static ProducibleItem Create(Guid organizationId, string name, string? description = null,
        string category = "Preparação", string measurementUnit = "un")
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A organização é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160)
        {
            throw new DomainException("O nome do item produzível é obrigatório.");
        }

        return new ProducibleItem(Guid.NewGuid(), organizationId, name.Trim(),
            NormalizeOptional(description, 4_000, "descrição"), NormalizeRequired(category, 80, "categoria"),
            NormalizeRequired(measurementUnit, 30, "unidade"));
    }

    public void Update(string name, string? description, string category, string measurementUnit, bool isActive)
    {
        Name = NormalizeRequired(name, 160, "nome");
        NormalizedName = Name.ToUpperInvariant();
        Description = NormalizeOptional(description, 4_000, "descrição");
        Category = NormalizeRequired(category, 80, "categoria");
        MeasurementUnit = NormalizeRequired(measurementUnit, 30, "unidade");
        IsActive = isActive;
    }

    private static string NormalizeRequired(string value, int maximum, string field)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximum)
            throw new DomainException($"O campo {field} deve possuir entre 1 e {maximum} caracteres.");
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maximum, string field)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > maximum) throw new DomainException($"O campo {field} deve possuir até {maximum} caracteres.");
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    public void Deactivate() => IsActive = false;
}
