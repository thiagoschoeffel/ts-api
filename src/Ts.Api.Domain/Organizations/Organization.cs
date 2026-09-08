using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Organizations;

public sealed class Organization
{
    private Organization() { }

    private Organization(Guid id, string name, string slug)
    {
        Id = id;
        Name = name;
        Slug = slug;
        IsActive = true;
        LifecycleStatus = OrganizationLifecycleStatus.Active;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public OrganizationLifecycleStatus LifecycleStatus { get; private set; }
    public long Version { get; private set; }

    public static Organization Create(string name, string slug)
        => Create(name, slug, OrganizationLifecycleStatus.Active);

    public static Organization CreateProvisioning(string name, string slug)
        => Create(name, slug, OrganizationLifecycleStatus.Provisioning);

    private static Organization Create(string name, string slug, OrganizationLifecycleStatus status)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("O nome da organização é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainException("O identificador da organização é obrigatório.");
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (normalizedSlug.Length > 100
            || !System.Text.RegularExpressions.Regex.IsMatch(normalizedSlug, "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
        {
            throw new DomainException("O identificador da organização deve conter apenas letras minúsculas, números e hífens internos.");
        }
        if (normalizedSlug is "api" or "admin" or "plataforma" or "platform" or "www")
        {
            throw new DomainException("O identificador da organização é reservado.");
        }

        var organization = new Organization(Guid.NewGuid(), name.Trim(), normalizedSlug);
        organization.LifecycleStatus = status;
        organization.IsActive = status == OrganizationLifecycleStatus.Active;
        return organization;
    }

    public void Deactivate() => SetStatus(OrganizationLifecycleStatus.Archived);

    public void SetStatus(OrganizationLifecycleStatus status)
    {
        if (!Enum.IsDefined(status)) throw new DomainException("O estado da organização é inválido.");
        if (LifecycleStatus == status) return;
        LifecycleStatus = status;
        IsActive = status == OrganizationLifecycleStatus.Active;
        Version = Version == 0 ? 1 : Version + 1;
    }
}

public enum OrganizationLifecycleStatus
{
    Provisioning = 1,
    Active = 2,
    Suspended = 3,
    Archived = 4,
}
