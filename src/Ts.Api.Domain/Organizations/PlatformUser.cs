using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Organizations;

public sealed class PlatformUser
{
    private PlatformUser() { }

    private PlatformUser(Guid id, string externalSubject, string displayName)
    {
        Id = id;
        ExternalSubject = externalSubject;
        DisplayName = displayName;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string ExternalSubject { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public static PlatformUser Create(string externalSubject, string displayName)
    {
        if (string.IsNullOrWhiteSpace(externalSubject))
        {
            throw new DomainException("O identificador externo do usuário é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("O nome do usuário é obrigatório.");
        }

        return new PlatformUser(Guid.NewGuid(), externalSubject.Trim(), displayName.Trim());
    }
}
