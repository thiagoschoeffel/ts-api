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
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public static Organization Create(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("O nome da organização é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainException("O identificador da organização é obrigatório.");
        }

        return new Organization(Guid.NewGuid(), name.Trim(), slug.Trim().ToLowerInvariant());
    }

    public void Deactivate() => IsActive = false;
}
