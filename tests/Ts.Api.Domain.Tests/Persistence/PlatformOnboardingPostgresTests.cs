using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Common;
using Ts.Api.Application.Organizations;
using Ts.Api.Domain.Organizations;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Domain.Tests.Persistence;

public sealed class PlatformOnboardingPostgresTests
{
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
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
