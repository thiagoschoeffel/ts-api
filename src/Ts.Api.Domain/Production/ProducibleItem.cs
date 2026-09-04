using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Production;

public sealed class ProducibleItem : ITenantOwned
{
    private ProducibleItem() { }

    private ProducibleItem(Guid id, Guid organizationId, string name)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public static ProducibleItem Create(Guid organizationId, string name)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A organização é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("O nome do item produzível é obrigatório.");
        }

        return new ProducibleItem(Guid.NewGuid(), organizationId, name.Trim());
    }

    public void Deactivate() => IsActive = false;
}
