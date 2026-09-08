using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Organizations;

public sealed class PlatformUser
{
    private PlatformUser() { }

    private PlatformUser(Guid id, string externalSubject, string displayName, string? email)
    {
        Id = id;
        ExternalSubject = externalSubject;
        DisplayName = displayName;
        Email = NormalizeEmail(email);
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string ExternalSubject { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }

    public static PlatformUser Create(string externalSubject, string displayName, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(externalSubject))
        {
            throw new DomainException("O identificador externo do usuário é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("O nome do usuário é obrigatório.");
        }

        return new PlatformUser(Guid.NewGuid(), externalSubject.Trim(), displayName.Trim(), email);
    }

    public void BindEmail(string email)
    {
        var normalized = NormalizeEmail(email) ?? throw new DomainException("O e-mail do usuário é obrigatório.");
        if (Email is not null && !string.Equals(Email, normalized, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("O e-mail autenticado não corresponde ao usuário da plataforma.");
        Email = normalized;
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var value = email.Trim().ToLowerInvariant();
        if (!value.Contains('@') || value.Length > 254) throw new DomainException("O e-mail do usuário é inválido.");
        return value;
    }
}
