using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Common;
using Ts.Api.Application.Organizations;
using Ts.Api.Domain.Organizations;
using Ts.Api.Domain.Customers;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Domain.Tests.Persistence;

public sealed class PlatformOnboardingPostgresTests
{
    [Fact]
    public async Task Completes_company_b_onboarding_without_changing_company_a()
    {
        var connectionString = Environment.GetEnvironmentVariable("TS_API_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        var now = new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero);
        var time = new FixedTimeProvider(now);
        var suffix = Guid.NewGuid().ToString("N");
        var organizationA = Organization.Create("Empresa A", $"empresa-a-{suffix}");
        var actor = PlatformUser.Create($"operator-{suffix}", "Operador global");
        var ownerA = PlatformUser.Create($"owner-a-{suffix}", "Proprietário A", $"owner-a-{suffix}@empresa.test");
        var plan = SaasPlanVersion.Create($"complete-{suffix}", "Completo", 1, true, now);
        var customerA = Customer.Create(organizationA.Id, "Cliente preservado", "5511999999999");

        var seedContext = new ContextFake(actor.Id); seedContext.SelectOrganizationForInvitation(organizationA.Id);
        await using (var database = new AppDbContext(options, seedContext, seedContext, time))
        {
            await database.Database.MigrateAsync();
            database.AddRange(organizationA, actor, ownerA, plan,
                OrganizationMembership.Create(organizationA.Id, ownerA.Id, OrganizationRole.Owner),
                SaasPlanEntitlement.Create(plan.Id, SaasEntitlements.BusinessAccess),
                OrganizationSaasSubscription.Create(organizationA.Id, plan.Id, actor.Id, now), customerA);
            await database.SaveChangesAsync();
        }

        var ownerEmail = $"owner-b-{suffix}@empresa.test";
        var createContext = new ContextFake(actor.Id);
        PlatformOnboardingAccepted accepted;
        await using (var database = new AppDbContext(options, createContext, createContext, time))
        {
            accepted = await new PlatformOnboardingService(new PlatformOnboardingStore(database),
                new ActorFake(actor.Id), createContext, new TokenFactoryFake(), time).CreateAsync(
                new("Empresa B", $"empresa-b-{suffix}", ownerEmail, "America/Sao_Paulo", "pt-BR"),
                $"onboarding-{suffix}", $"create-{suffix}", default);
        }

        var processContext = new ContextFake(actor.Id);
        await using (var database = new AppDbContext(options, processContext, processContext, time))
        {
            var processor = new PlatformOnboardingProcessor(new PlatformOnboardingStore(database),
                processContext, new TokenFactoryFake(), new SenderFake(), time);
            var operationId = await processor.ClaimNextAsync($"worker-{suffix}", default);
            Assert.Equal(accepted.OperationId, operationId);
            await processor.ProcessAsync(operationId!.Value, default);
        }

        Guid organizationBId; Guid invitationId;
        await using (var database = new AppDbContext(options, new ContextFake()))
        {
            var onboarding = await database.PlatformOnboardings.SingleAsync(item => item.Id == accepted.OnboardingId);
            organizationBId = onboarding.OrganizationId; invitationId = onboarding.OwnerInvitationId;
        }

        var acceptContext = new ContextFake();
        await using (var database = new AppDbContext(options, acceptContext, acceptContext, time))
        {
            var service = new MembershipService(new MembershipStore(database, acceptContext), acceptContext,
                acceptContext, acceptContext, new SenderFake(), time);
            var membership = await service.AcceptAsync(new TokenFactoryFake().Create(invitationId),
                $"owner-b-subject-{suffix}", ownerEmail, "Proprietário B", $"accept-{suffix}", default);
            Assert.Equal(organizationBId, membership.OrganizationId);
            Assert.Equal(OrganizationRole.Owner, membership.Role);
        }

        var lifecycleContext = new ContextFake(actor.Id);
        await using (var database = new AppDbContext(options, lifecycleContext, lifecycleContext, time))
        {
            var lifecycle = new PlatformLifecycleService(new PlatformLifecycleStore(database),
                new ActorFake(actor.Id), time);
            var assigned = await lifecycle.AssignPlanAsync(organizationBId, plan.Id, null,
                $"plan-{suffix}", default);
            var activated = await lifecycle.ChangeStatusAsync(organizationBId,
                OrganizationLifecycleStatus.Active, assigned.Version, "Aceite A/B concluído.",
                $"activate-{suffix}", default);
            Assert.Equal(OrganizationLifecycleStatus.Active, activated.Status);

            var integrations = new ExternalIntegrationService(new ExternalIntegrationStore(database),
                new ProtectorFake(), new VerifierFake(), new ActorFake(actor.Id), time);
            await integrations.SaveWhatsAppAsync(organizationA.Id,
                IntegrationInput($"phone-a-{suffix}", "+551100000001", $"access-a-{suffix}"),
                $"integration-a-{suffix}", default);
            await integrations.SaveWhatsAppAsync(organizationBId,
                IntegrationInput($"phone-b-{suffix}", "+551100000002", $"access-b-{suffix}"),
                $"integration-b-{suffix}", default);
            Assert.Single(await integrations.ListAsync(organizationA.Id, default));
            Assert.Single(await integrations.ListAsync(organizationBId, default));
        }

        var contextA = new ContextFake(actor.Id); contextA.SelectOrganizationForInvitation(organizationA.Id);
        await using (var database = new AppDbContext(options, contextA, contextA, time))
        {
            Assert.Equal(1, await database.Customers.CountAsync(item => item.Id == customerA.Id));
            var persistedA = await database.Organizations.AsNoTracking().SingleAsync(item => item.Id == organizationA.Id);
            Assert.Equal(OrganizationLifecycleStatus.Active, persistedA.LifecycleStatus);
            Assert.Equal(1, persistedA.Version);
        }

        var contextB = new ContextFake(actor.Id); contextB.SelectOrganizationForInvitation(organizationBId);
        await using (var database = new AppDbContext(options, contextB, contextB, time))
        {
            Assert.Equal(0, await database.Customers.CountAsync(item => item.Id == customerA.Id));
            database.Customers.Add(Customer.Create(organizationA.Id, "Escrita cruzada", "5511888888888"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => database.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Pending_operation_survives_context_restart_and_completes_once()
    {
        var connectionString = Environment.GetEnvironmentVariable("TS_API_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        var now = new DateTimeOffset(2026, 9, 8, 18, 0, 0, TimeSpan.Zero);
        var time = new FixedTimeProvider(now);
        Guid actorId;
        var commandKey = $"restart-{Guid.NewGuid():N}";
        Guid onboardingId;

        await using (var database = new AppDbContext(options, new ContextFake()))
        {
            await database.Database.MigrateAsync();
            var user = PlatformUser.Create($"subject-{Guid.NewGuid():N}", "Operador de teste");
            actorId = user.Id;
            database.Users.Add(user);
            await database.SaveChangesAsync();
        }

        var context = new ContextFake(actorId);
        await using (var scopedDatabase = new AppDbContext(options, context, context, time))
        {
            var service = new PlatformOnboardingService(new PlatformOnboardingStore(scopedDatabase),
                new ActorFake(actorId), context, new TokenFactoryFake(), time);
            var result = await service.CreateAsync(new("Empresa Restart", $"empresa-{Guid.NewGuid():N}",
                $"owner-{Guid.NewGuid():N}@empresa.test", "America/Sao_Paulo", "pt-BR"),
                commandKey, "correlation-create", default);
            onboardingId = result.OnboardingId;
        }

        var sender = new SenderFake();
        var restartedContext = new ContextFake(actorId);
        await using (var restartedDatabase = new AppDbContext(options, restartedContext, restartedContext, time))
        {
            var store = new PlatformOnboardingStore(restartedDatabase);
            var processor = new PlatformOnboardingProcessor(store, restartedContext,
                new TokenFactoryFake(), sender, time);
            var claimed = await processor.ClaimNextAsync("worker-after-restart", default);
            Assert.NotNull(claimed);
            await processor.ProcessAsync(claimed!.Value, default);
        }

        await using var verification = new AppDbContext(options, new ContextFake());
        var onboarding = await verification.PlatformOnboardings.SingleAsync(item => item.Id == onboardingId);
        Assert.Equal(PlatformOnboardingStatus.AwaitingOwner, onboarding.Status);
        Assert.Equal(1, sender.Calls);
        Assert.Equal(1, await verification.PlatformProvisioningOperations.CountAsync(item => item.OnboardingId == onboardingId));
        Assert.Equal(1, await verification.PlatformOutboxMessages.CountAsync(item => item.OperationId ==
            verification.PlatformProvisioningOperations.Single(operation => operation.OnboardingId == onboardingId).Id));
    }

    private static SaveWhatsAppIntegration IntegrationInput(string phoneId, string phone, string token) =>
        new("WhatsApp principal", "waba-test", phoneId, phone, token, $"app-{token}",
            $"verify-{token}", 1000, 970, null);

    private sealed class ContextFake : IOrganizationContext, ICurrentUserContext, IIdentityOrganizationScope
    {
        private Guid? organizationId;
        public ContextFake(Guid? userId = null) => UserId = userId ?? Guid.Empty;
        public bool IsAvailable => organizationId.HasValue && UserId != Guid.Empty;
        public Guid OrganizationId => organizationId ?? throw new InvalidOperationException();
        public Guid UserId { get; }
        public string CorrelationId => "postgres-restart-test";
        public void SelectOrganizationForInvitation(Guid value) => organizationId = value;
    }
    private sealed class ActorFake(Guid userId) : IPlatformActorContext
    {
        public bool IsAvailable => true;
        public Guid UserId { get; } = userId;
        public IReadOnlySet<string> Profiles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string>();
    }
    private sealed class TokenFactoryFake : IOnboardingInvitationTokenFactory
    {
        public string Create(Guid invitationId) => $"token-{invitationId:N}";
    }
    private sealed class SenderFake : IInvitationEmailSender
    {
        public int Calls { get; private set; }
        public Task<string> SendAsync(Guid invitationId, string email, string organizationName,
            OrganizationRole role, string token, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult($"message-{invitationId:N}"); }
    }
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
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
