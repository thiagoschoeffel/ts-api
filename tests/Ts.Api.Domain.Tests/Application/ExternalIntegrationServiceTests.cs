using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Common;
using Ts.Api.Application.Organizations;
using Ts.Api.Domain.Organizations;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Domain.Tests.Application;

public sealed class ExternalIntegrationServiceTests
{
    [Fact]
    public async Task Keeps_connections_and_runtime_secrets_isolated_between_organizations()
    {
        var organizationA = Organization.Create("Empresa A", "empresa-a");
        var organizationB = Organization.Create("Empresa B", "empresa-b");
        var actor = PlatformUser.Create("operator", "Operador");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new OrganizationContext(organizationA.Id);
        await using var database = new AppDbContext(options, context);
        database.AddRange(organizationA, organizationB, actor); await database.SaveChangesAsync();
        var store = new ExternalIntegrationStore(database); var protector = new ProtectorFake();
        var service = new ExternalIntegrationService(store, protector, new VerifierFake(),
            new ActorFake(actor.Id), TimeProvider.System);

        var savedA = await service.SaveWhatsAppAsync(organizationA.Id,
            Input("phone-a", "+551100000001", "access-a"), "a", default);
        var savedB = await service.SaveWhatsAppAsync(organizationB.Id,
            Input("phone-b", "+551100000002", "access-b"), "b", default);

        Assert.Equal(savedA.Id, Assert.Single(await service.ListAsync(organizationA.Id, default)).Id);
        Assert.Equal(savedB.Id, Assert.Single(await service.ListAsync(organizationB.Id, default)).Id);
        Assert.DoesNotContain("access-a", System.Text.Json.JsonSerializer.Serialize(savedA));
        var runtimeA = await new WhatsAppConnectionResolver(store, protector, context).GetCurrentAsync(default);
        context.OrganizationId = organizationB.Id;
        var runtimeB = await new WhatsAppConnectionResolver(store, protector, context).GetCurrentAsync(default);
        Assert.Equal("access-a", runtimeA.AccessToken);
        Assert.Equal("access-b", runtimeB.AccessToken);
        Assert.NotEqual(runtimeA.Id, runtimeB.Id);
    }

    [Fact]
    public async Task Rejects_cross_organization_disable()
    {
        var organizationA = Organization.Create("Empresa A", "empresa-a");
        var organizationB = Organization.Create("Empresa B", "empresa-b");
        var actor = PlatformUser.Create("operator", "Operador");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var database = new AppDbContext(options, new OrganizationContext(organizationA.Id));
        database.AddRange(organizationA, organizationB, actor); await database.SaveChangesAsync();
        var service = new ExternalIntegrationService(new ExternalIntegrationStore(database),
            new ProtectorFake(), new VerifierFake(), new ActorFake(actor.Id), TimeProvider.System);
        var saved = await service.SaveWhatsAppAsync(organizationA.Id,
            Input("phone-a", "+551100000001", "access-a"), "save", default);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => service.DisableAsync(
            organizationB.Id, saved.Id, saved.Version, "disable", default));
    }

    private static SaveWhatsAppIntegration Input(string phoneId, string phone, string access) =>
        new("WhatsApp", "business-account", phoneId, phone, access, $"secret-{phoneId}",
            $"verify-{phoneId}", 1000, 970, null);

    private sealed class ProtectorFake : IIntegrationSecretProtector
    {
        public string Protect(string plaintext) => $"protected:{plaintext}";
        public string Unprotect(string protectedValue) => protectedValue[10..];
    }
    private sealed class VerifierFake : IWhatsAppConnectionVerifier
    {
        public Task VerifyAsync(string phoneNumberId, string accessToken, CancellationToken token) =>
            Task.CompletedTask;
    }
    private sealed class ActorFake(Guid id) : IPlatformActorContext
    {
        public bool IsAvailable => true; public Guid UserId { get; } = id;
        public IReadOnlySet<string> Profiles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string>();
    }
    private sealed class OrganizationContext(Guid id) : IOrganizationContext
    {
        public bool IsAvailable => true; public Guid OrganizationId { get; set; } = id;
    }
}
