using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Organizations;

public sealed class OrganizationInvitation : ITenantOwned
{
    private OrganizationInvitation() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public OrganizationRole Role { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public string? EmailMessageId { get; private set; }
    public DateTimeOffset? EmailSentAt { get; private set; }
    public long Version { get; private set; } = 1;

    public bool IsPending(DateTimeOffset now) => RevokedAt is null && AcceptedAt is null && ExpiresAt > now;

    public static OrganizationInvitation Create(Guid organizationId, string email, OrganizationRole role,
        string tokenHash, Guid createdBy, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        var normalized = NormalizeEmail(email);
        if (organizationId == Guid.Empty || createdBy == Guid.Empty) throw new DomainException("Organização e autor são obrigatórios no convite.");
        if (!Enum.IsDefined(role)) throw new DomainException("O papel do convite é inválido.");
        if (string.IsNullOrWhiteSpace(tokenHash)) throw new DomainException("O token do convite é obrigatório.");
        if (expiresAt <= now) throw new DomainException("A expiração do convite deve estar no futuro.");
        return new() { Id = Guid.NewGuid(), OrganizationId = organizationId, Email = email.Trim(),
            NormalizedEmail = normalized, Role = role, TokenHash = tokenHash, CreatedBy = createdBy,
            CreatedAt = now, ExpiresAt = expiresAt };
    }

    public void Revoke(DateTimeOffset now) { if (AcceptedAt is null && RevokedAt is null) { RevokedAt = now; Version++; } }
    public void MarkSent(string messageId, DateTimeOffset now) { EmailMessageId = messageId; EmailSentAt = now; Version++; }
    public void Accept(Guid userId, DateTimeOffset now)
    {
        if (!IsPending(now)) throw new DomainException("O convite expirou, foi revogado ou já foi utilizado.");
        AcceptedByUserId = userId; AcceptedAt = now; Version++;
    }

    public static string NormalizeEmail(string email)
    {
        var value = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!value.Contains('@') || value.Length > 254) throw new DomainException("Informe um e-mail válido.");
        return value;
    }
}
