using Ts.Api.Application.Organizations;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Api;

public sealed class LegacyWhatsAppConnectionMigrator(IServiceScopeFactory scopeFactory,
    IConfiguration configuration, ILogger<LegacyWhatsAppConnectionMigrator> logger,
    TimeProvider timeProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var organizationText = configuration["WhatsApp:OrganizationId"];
        var phoneNumberId = configuration["WhatsApp:PhoneNumberId"];
        var businessPhone = configuration["WhatsApp:BusinessPhoneNumber"];
        var accessToken = configuration["WhatsApp:AccessToken"];
        var appSecret = configuration["WhatsApp:AppSecret"];
        var verifyToken = configuration["WhatsApp:WebhookVerifyToken"];
        if (!Guid.TryParse(organizationText, out var organizationId)
            || Blank(phoneNumberId) || Blank(businessPhone) || Blank(accessToken)
            || Blank(appSecret) || Blank(verifyToken)) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IExternalIntegrationStore>();
        if (await store.FindWhatsAppAsync(organizationId, cancellationToken) is not null) return;
        if (await store.FindOrganizationAsync(organizationId, cancellationToken) is null)
            throw new InvalidOperationException("WhatsApp:OrganizationId não referencia uma organização existente.");
        var protector = scope.ServiceProvider.GetRequiredService<IIntegrationSecretProtector>();
        var now = timeProvider.GetUtcNow();
        var access = ExternalIntegrationSecret.Create(organizationId,
            ExternalIntegrationSecretPurpose.AccessToken, protector.Protect(accessToken!), now);
        var app = ExternalIntegrationSecret.Create(organizationId,
            ExternalIntegrationSecretPurpose.AppSecret, protector.Protect(appSecret!), now);
        var verify = ExternalIntegrationSecret.Create(organizationId,
            ExternalIntegrationSecretPurpose.WebhookVerifyToken, protector.Protect(verifyToken!), now);
        var connection = ExternalIntegrationConnection.CreateWhatsApp(organizationId,
            "WhatsApp legado", configuration["WhatsApp:BusinessAccountId"] ?? "legacy",
            phoneNumberId!, businessPhone!, access.Id, app.Id, verify.Id,
            ReadInt("FreeServiceMessageLimit", 1000), ReadInt("AutomationPauseAt", 970), now);
        store.Add(access); store.Add(app); store.Add(verify); store.Add(connection);
        store.Add(PlatformAuditEvent.Create(null, "System", "integration.whatsapp.legacy-migrated",
            "ExternalIntegrationConnection", connection.Id, "Succeeded",
            "Configuração global legada vinculada explicitamente à organização.", now,
            $"legacy-whatsapp:{connection.Id:N}"));
        await store.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Conexão WhatsApp legada migrada para {ConnectionId} da organização {OrganizationId}.",
            connection.Id, organizationId);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);
    private int ReadInt(string name, int fallback) =>
        int.TryParse(configuration[$"WhatsApp:{name}"], out var value) ? value : fallback;
}
