using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Common;
using Ts.Api.Application.Organizations;
using Ts.Api.Domain.Organizations;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Domain.Tests.Application;

public sealed class PlatformLifecycleServiceTests
{
    [Fact]
    public async Task Assigns_plan_activates_and_suspends_with_audit()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var organization = Organization.CreateProvisioning("Empresa B", "empresa-b");
        var actor = PlatformUser.Create("platform-actor", "Operador global");
        var owner = PlatformUser.Create("owner-b", "Proprietário B");
        var plan = SaasPlanVersion.Create("complete", "Completo", 1, true, DateTimeOffset.UtcNow);
        await using var database = new AppDbContext(options, new OrganizationContextFake(organization.Id));
        database.AddRange(organization, actor, owner, plan,
            OrganizationMembership.Create(organization.Id, owner.Id, OrganizationRole.Owner),
            SaasPlanEntitlement.Create(plan.Id, SaasEntitlements.BusinessAccess));
        await database.SaveChangesAsync();
        var service = new PlatformLifecycleService(new PlatformLifecycleStore(database),
            new ActorFake(actor.Id), TimeProvider.System);

        var assigned = await service.AssignPlanAsync(organization.Id, plan.Id, null,
            "correlation-assign", default);
        var activated = await service.ChangeStatusAsync(organization.Id,
            OrganizationLifecycleStatus.Active, assigned.Version, "Cadastro conferido.",
            "correlation-activate", default);
        var suspended = await service.ChangeStatusAsync(organization.Id,
            OrganizationLifecycleStatus.Suspended, activated.Version, "Solicitação administrativa.",
            "correlation-suspend", default);

        Assert.NotNull(assigned.Subscription);
        Assert.Equal(OrganizationLifecycleStatus.Active, activated.Status);
        Assert.Equal(OrganizationLifecycleStatus.Suspended, suspended.Status);
        Assert.Equal(3, await database.PlatformAuditEvents.CountAsync());
    }

    [Fact]
    public async Task Rejects_activation_without_a_plan()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var organization = Organization.CreateProvisioning("Empresa B", "empresa-b");
        var actor = PlatformUser.Create("platform-actor", "Operador global");
        await using var database = new AppDbContext(options, new OrganizationContextFake(organization.Id));
        database.AddRange(organization, actor);
        await database.SaveChangesAsync();
        var service = new PlatformLifecycleService(new PlatformLifecycleStore(database),
            new ActorFake(actor.Id), TimeProvider.System);

        await Assert.ThrowsAsync<ConflictException>(() => service.ChangeStatusAsync(organization.Id,
            OrganizationLifecycleStatus.Active, organization.Version, "Tentativa.", "correlation", default));
    }

    private sealed class OrganizationContextFake(Guid organizationId) : IOrganizationContext
    {
        public bool IsAvailable => true;
        public Guid OrganizationId { get; } = organizationId;
    }

    private sealed class ActorFake(Guid userId) : IPlatformActorContext
    {
        public bool IsAvailable => true;
        public Guid UserId { get; } = userId;
        public IReadOnlySet<string> Profiles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string>();
    }
}
