using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Organizations;

public enum ExternalIntegrationProvider { WhatsApp }
public enum ExternalIntegrationStatus { PendingVerification, Active, Disabled }
public enum ExternalIntegrationHealth { Unknown, Healthy, Degraded }
public enum ExternalIntegrationSecretPurpose { AccessToken, AppSecret, WebhookVerifyToken }

public sealed class ExternalIntegrationConnection
{
    private ExternalIntegrationConnection() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public ExternalIntegrationProvider Provider { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string ExternalAccountId { get; private set; } = string.Empty;
    public string AssetId { get; private set; } = string.Empty;
    public string AssetLabel { get; private set; } = string.Empty;
    public Guid AccessTokenSecretId { get; private set; }
    public Guid AppSecretSecretId { get; private set; }
    public Guid WebhookVerifyTokenSecretId { get; private set; }
    public int FreeServiceMessageLimit { get; private set; }
    public int AutomationPauseAt { get; private set; }
    public ExternalIntegrationStatus Status { get; private set; }
    public ExternalIntegrationHealth Health { get; private set; }
    public DateTimeOffset? LastHealthCheckAt { get; private set; }
    public string? LastHealthError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static ExternalIntegrationConnection CreateWhatsApp(Guid organizationId, string displayName,
        string externalAccountId, string phoneNumberId, string businessPhoneNumber,
        Guid accessTokenSecretId, Guid appSecretSecretId, Guid verifyTokenSecretId,
        int freeServiceMessageLimit, int automationPauseAt, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty || accessTokenSecretId == Guid.Empty || appSecretSecretId == Guid.Empty
            || verifyTokenSecretId == Guid.Empty) throw new DomainException("A conexão externa é inválida.");
        return new ExternalIntegrationConnection
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, Provider = ExternalIntegrationProvider.WhatsApp,
            DisplayName = Required(displayName, "nome"), ExternalAccountId = Required(externalAccountId, "conta externa"),
            AssetId = Required(phoneNumberId, "identificador do telefone"), AssetLabel = Required(businessPhoneNumber, "telefone comercial"),
            AccessTokenSecretId = accessTokenSecretId, AppSecretSecretId = appSecretSecretId,
            WebhookVerifyTokenSecretId = verifyTokenSecretId,
            FreeServiceMessageLimit = ValidLimits(freeServiceMessageLimit, automationPauseAt).Limit,
            AutomationPauseAt = ValidLimits(freeServiceMessageLimit, automationPauseAt).PauseAt,
            Status = ExternalIntegrationStatus.Active,
            Health = ExternalIntegrationHealth.Unknown, CreatedAt = now, UpdatedAt = now, Version = 1,
        };
    }

    public void Update(string displayName, string externalAccountId, string phoneNumberId,
        string businessPhoneNumber, Guid accessTokenSecretId, Guid appSecretSecretId,
        Guid verifyTokenSecretId, int freeServiceMessageLimit, int automationPauseAt,
        long expectedVersion, DateTimeOffset now)
    {
        EnsureVersion(expectedVersion);
        DisplayName = Required(displayName, "nome"); ExternalAccountId = Required(externalAccountId, "conta externa");
        AssetId = Required(phoneNumberId, "identificador do telefone"); AssetLabel = Required(businessPhoneNumber, "telefone comercial");
        AccessTokenSecretId = accessTokenSecretId; AppSecretSecretId = appSecretSecretId;
        WebhookVerifyTokenSecretId = verifyTokenSecretId;
        (FreeServiceMessageLimit, AutomationPauseAt) = ValidLimits(freeServiceMessageLimit, automationPauseAt);
        Status = ExternalIntegrationStatus.Active;
        Health = ExternalIntegrationHealth.Unknown; LastHealthCheckAt = null; LastHealthError = null;
        UpdatedAt = now; Version++;
    }

    public void Disable(long expectedVersion, DateTimeOffset now)
    {
        EnsureVersion(expectedVersion); Status = ExternalIntegrationStatus.Disabled;
        UpdatedAt = now; Version++;
    }

    public void ReportHealth(bool healthy, string? error, DateTimeOffset now)
    {
        Health = healthy ? ExternalIntegrationHealth.Healthy : ExternalIntegrationHealth.Degraded;
        LastHealthError = healthy ? null : string.IsNullOrWhiteSpace(error) ? "Falha não detalhada." : error.Trim();
        LastHealthCheckAt = now; UpdatedAt = now; Version++;
    }

    public void MarkVerified(DateTimeOffset now)
    {
        Status = ExternalIntegrationStatus.Active; Health = ExternalIntegrationHealth.Healthy;
        LastHealthError = null; LastHealthCheckAt = now; UpdatedAt = now;
    }

    private void EnsureVersion(long expectedVersion)
    {
        if (Version != expectedVersion) throw new DomainException("A conexão foi alterada por outro operador.");
    }

    private static string Required(string value, string field)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 200) throw new DomainException($"Informe {field} com até 200 caracteres.");
        return normalized;
    }

    private static (int Limit, int PauseAt) ValidLimits(int limit, int pauseAt)
    {
        if (limit <= 0 || pauseAt <= 0 || pauseAt > limit)
            throw new DomainException("A franquia e a margem de pausa do WhatsApp são inválidas.");
        return (limit, pauseAt);
    }
}

public sealed class ExternalIntegrationSecret
{
    private ExternalIntegrationSecret() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public ExternalIntegrationSecretPurpose Purpose { get; private set; }
    public string ProtectedValue { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static ExternalIntegrationSecret Create(Guid organizationId, ExternalIntegrationSecretPurpose purpose,
        string protectedValue, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty || string.IsNullOrWhiteSpace(protectedValue))
            throw new DomainException("O segredo da integração é inválido.");
        return new() { Id = Guid.NewGuid(), OrganizationId = organizationId, Purpose = purpose,
            ProtectedValue = protectedValue, CreatedAt = now };
    }
}

public sealed class ExternalIntegrationWebhookReceipt
{
    private ExternalIntegrationWebhookReceipt() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ConnectionId { get; private set; }
    public string ExternalEventId { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; private set; }

    public static ExternalIntegrationWebhookReceipt Create(Guid organizationId, Guid connectionId,
        string externalEventId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = organizationId, ConnectionId = connectionId,
        ExternalEventId = externalEventId.Trim(), ReceivedAt = now,
    };
}
