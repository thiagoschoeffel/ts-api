using Ts.Api.Application.Common;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Application.Organizations;

public sealed record ExternalIntegrationResult(Guid Id, Guid OrganizationId, ExternalIntegrationProvider Provider,
    string DisplayName, string ExternalAccountId, string AssetId, string AssetLabel,
    ExternalIntegrationStatus Status, ExternalIntegrationHealth Health, DateTimeOffset? LastHealthCheckAt,
    string? LastHealthError, bool HasAccessToken, bool HasAppSecret, bool HasWebhookVerifyToken,
    int FreeServiceMessageLimit, int AutomationPauseAt, string WebhookPath, long Version);
public sealed record SaveWhatsAppIntegration(string DisplayName, string ExternalAccountId,
    string PhoneNumberId, string BusinessPhoneNumber, string? AccessToken, string? AppSecret,
    string? WebhookVerifyToken, int FreeServiceMessageLimit, int AutomationPauseAt,
    long? ExpectedVersion);
public sealed record WhatsAppRuntimeConnection(Guid Id, Guid OrganizationId, string PhoneNumberId,
    string BusinessPhoneNumber, string AccessToken, string AppSecret, string WebhookVerifyToken,
    int FreeServiceMessageLimit, int AutomationPauseAt);

public interface IIntegrationSecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}

public interface IWhatsAppConnectionVerifier
{
    Task VerifyAsync(string phoneNumberId, string accessToken, CancellationToken token);
}

public interface IExternalIntegrationStore
{
    Task<Organization?> FindOrganizationAsync(Guid id, CancellationToken token);
    Task<IReadOnlyCollection<ExternalIntegrationConnection>> ListAsync(Guid organizationId, CancellationToken token);
    Task<ExternalIntegrationConnection?> FindWhatsAppAsync(Guid organizationId, CancellationToken token);
    Task<ExternalIntegrationConnection?> FindConnectionAsync(Guid connectionId, CancellationToken token);
    Task<ExternalIntegrationSecret?> FindSecretAsync(Guid id, CancellationToken token);
    Task<bool> TryRegisterWebhookAsync(ExternalIntegrationWebhookReceipt receipt, CancellationToken token);
    void Add(object entity);
    Task SaveChangesAsync(CancellationToken token);
}

public sealed class ExternalIntegrationService(IExternalIntegrationStore store,
    IIntegrationSecretProtector protector, IWhatsAppConnectionVerifier verifier,
    IPlatformActorContext actor, TimeProvider timeProvider)
{
    public async Task<IReadOnlyCollection<ExternalIntegrationResult>> ListAsync(Guid organizationId,
        CancellationToken token)
    {
        _ = await store.FindOrganizationAsync(organizationId, token)
            ?? throw new ResourceNotFoundException("Organização não encontrada.");
        return (await store.ListAsync(organizationId, token)).Select(Map).ToArray();
    }

    public async Task<ExternalIntegrationResult> SaveWhatsAppAsync(Guid organizationId,
        SaveWhatsAppIntegration input, string correlationId, CancellationToken token)
    {
        _ = await store.FindOrganizationAsync(organizationId, token)
            ?? throw new ResourceNotFoundException("Organização não encontrada.");
        var connection = await store.FindWhatsAppAsync(organizationId, token);
        var now = timeProvider.GetUtcNow();
        if (connection is null && (!Present(input.AccessToken) || !Present(input.AppSecret)
                || !Present(input.WebhookVerifyToken)))
            throw new DomainException("Informe os três segredos ao criar a conexão WhatsApp.");
        if (connection is null && input.ExpectedVersion.HasValue
            || connection is not null && (!input.ExpectedVersion.HasValue
                || connection.Version != input.ExpectedVersion.Value))
            throw new PreconditionFailedException("A conexão foi alterada. Recarregue os dados.");

        var verificationToken = Present(input.AccessToken) ? input.AccessToken!.Trim()
            : protector.Unprotect((await store.FindSecretAsync(connection!.AccessTokenSecretId, token)
                ?? throw new ConflictException("O token seguro da conexão não foi encontrado.")).ProtectedValue);
        await verifier.VerifyAsync(input.PhoneNumberId.Trim(), verificationToken, token);

        var access = await Secret(organizationId, ExternalIntegrationSecretPurpose.AccessToken,
            input.AccessToken, connection?.AccessTokenSecretId, now, token);
        var app = await Secret(organizationId, ExternalIntegrationSecretPurpose.AppSecret,
            input.AppSecret, connection?.AppSecretSecretId, now, token);
        var verify = await Secret(organizationId, ExternalIntegrationSecretPurpose.WebhookVerifyToken,
            input.WebhookVerifyToken, connection?.WebhookVerifyTokenSecretId, now, token);
        if (connection is null)
        {
            connection = ExternalIntegrationConnection.CreateWhatsApp(organizationId, input.DisplayName,
                input.ExternalAccountId, input.PhoneNumberId, input.BusinessPhoneNumber,
                access.Id, app.Id, verify.Id, input.FreeServiceMessageLimit,
                input.AutomationPauseAt, now);
            store.Add(connection);
        }
        else
        {
            try { connection.Update(input.DisplayName, input.ExternalAccountId, input.PhoneNumberId,
                input.BusinessPhoneNumber, access.Id, app.Id, verify.Id,
                input.FreeServiceMessageLimit, input.AutomationPauseAt,
                input.ExpectedVersion ?? throw new PreconditionFailedException(
                    "Informe a versão atual da conexão."), now); }
            catch (DomainException exception) { throw new PreconditionFailedException(exception.Message); }
        }
        connection.MarkVerified(now);
        store.Add(PlatformAuditEvent.Create(actor.UserId, "PlatformOperator", "integration.whatsapp.saved",
            "ExternalIntegrationConnection", connection.Id, "Succeeded", "Conexão WhatsApp configurada.",
            now, correlationId));
        await store.SaveChangesAsync(token);
        return Map(connection);
    }

    public async Task<ExternalIntegrationResult> DisableAsync(Guid organizationId, Guid connectionId,
        long expectedVersion, string correlationId, CancellationToken token)
    {
        var connection = await store.FindConnectionAsync(connectionId, token);
        if (connection is null || connection.OrganizationId != organizationId)
            throw new ResourceNotFoundException("Conexão não encontrada.");
        try { connection.Disable(expectedVersion, timeProvider.GetUtcNow()); }
        catch (DomainException exception) { throw new PreconditionFailedException(exception.Message); }
        store.Add(PlatformAuditEvent.Create(actor.UserId, "PlatformOperator", "integration.disabled",
            "ExternalIntegrationConnection", connection.Id, "Succeeded", "Conexão externa desabilitada.",
            timeProvider.GetUtcNow(), correlationId));
        await store.SaveChangesAsync(token);
        return Map(connection);
    }

    private async Task<ExternalIntegrationSecret> Secret(Guid organizationId,
        ExternalIntegrationSecretPurpose purpose, string? plaintext, Guid? currentId,
        DateTimeOffset now, CancellationToken token)
    {
        if (Present(plaintext))
        {
            var secret = ExternalIntegrationSecret.Create(organizationId, purpose,
                protector.Protect(plaintext!.Trim()), now);
            store.Add(secret); return secret;
        }
        return currentId.HasValue
            ? await store.FindSecretAsync(currentId.Value, token)
                ?? throw new ConflictException("A referência segura da conexão não foi encontrada.")
            : throw new DomainException("O segredo da integração é obrigatório.");
    }

    private static bool Present(string? value) => !string.IsNullOrWhiteSpace(value);
    private static ExternalIntegrationResult Map(ExternalIntegrationConnection item) => new(item.Id,
        item.OrganizationId, item.Provider, item.DisplayName, item.ExternalAccountId, item.AssetId,
        item.AssetLabel, item.Status, item.Health, item.LastHealthCheckAt, item.LastHealthError,
        item.AccessTokenSecretId != Guid.Empty, item.AppSecretSecretId != Guid.Empty,
        item.WebhookVerifyTokenSecretId != Guid.Empty, item.FreeServiceMessageLimit,
        item.AutomationPauseAt, $"/webhooks/whatsapp/{item.Id}", item.Version);
}

public sealed class WhatsAppConnectionResolver(IExternalIntegrationStore store,
    IIntegrationSecretProtector protector, IOrganizationContext organization) : IWhatsAppConnectionResolver
{
    public async Task<WhatsAppRuntimeConnection> GetCurrentAsync(CancellationToken token)
    {
        Guid organizationId;
        try { organizationId = organization.OrganizationId; }
        catch (InvalidOperationException) { throw new ConflictException("A organização da conexão não foi resolvida."); }
        var connection = await store.FindWhatsAppAsync(organizationId, token);
        return await Runtime(connection, token);
    }

    public async Task<WhatsAppRuntimeConnection?> ResolveWebhookAsync(Guid connectionId, CancellationToken token)
    {
        var connection = await store.FindConnectionAsync(connectionId, token);
        return connection is null || connection.Status != ExternalIntegrationStatus.Active
            ? null : await Runtime(connection, token);
    }

    public Task<bool> TryRegisterWebhookAsync(WhatsAppRuntimeConnection connection, string externalEventId,
        CancellationToken token) => store.TryRegisterWebhookAsync(
            ExternalIntegrationWebhookReceipt.Create(connection.OrganizationId, connection.Id,
                externalEventId, DateTimeOffset.UtcNow), token);

    private async Task<WhatsAppRuntimeConnection> Runtime(ExternalIntegrationConnection? connection,
        CancellationToken token)
    {
        if (connection is null || connection.Status != ExternalIntegrationStatus.Active)
            throw new ConflictException("A organização não possui uma conexão WhatsApp ativa.");
        var access = await RequiredSecret(connection.AccessTokenSecretId, token);
        var app = await RequiredSecret(connection.AppSecretSecretId, token);
        var verify = await RequiredSecret(connection.WebhookVerifyTokenSecretId, token);
        return new(connection.Id, connection.OrganizationId, connection.AssetId, connection.AssetLabel,
            protector.Unprotect(access.ProtectedValue), protector.Unprotect(app.ProtectedValue),
            protector.Unprotect(verify.ProtectedValue), connection.FreeServiceMessageLimit,
            connection.AutomationPauseAt);
    }

    private async Task<ExternalIntegrationSecret> RequiredSecret(Guid id, CancellationToken token) =>
        await store.FindSecretAsync(id, token)
            ?? throw new ConflictException("A configuração segura da conexão está incompleta.");
}

public interface IWhatsAppConnectionResolver
{
    Task<WhatsAppRuntimeConnection> GetCurrentAsync(CancellationToken token);
    Task<WhatsAppRuntimeConnection?> ResolveWebhookAsync(Guid connectionId, CancellationToken token);
    Task<bool> TryRegisterWebhookAsync(WhatsAppRuntimeConnection connection, string externalEventId,
        CancellationToken token);
}
