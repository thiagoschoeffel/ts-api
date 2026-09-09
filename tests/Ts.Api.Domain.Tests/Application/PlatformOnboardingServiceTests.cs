using Ts.Api.Application.Common;
using Ts.Api.Application.Organizations;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Domain.Tests.Application;

public sealed class PlatformOnboardingServiceTests
{
    [Fact]
    public async Task Replays_same_idempotency_key_without_duplicate_effects()
    {
        var fixture = new Fixture();
        var command = new CreatePlatformOnboardingCommand("Empresa B", "empresa-b",
            "owner@empresa.test", "America/Sao_Paulo", "pt-BR");

        var first = await fixture.Service.CreateAsync(command, "onboarding-1", "corr-1", default);
        var replay = await fixture.Service.CreateAsync(command, "onboarding-1", "corr-2", default);

        Assert.Equal(first, replay);
        Assert.Single(fixture.Store.Onboardings);
        Assert.Single(fixture.Store.Organizations);
        Assert.Single(fixture.Store.Invitations);
        Assert.Single(fixture.Store.Outbox);
    }

    [Fact]
    public async Task Rejects_same_idempotency_key_with_different_fingerprint()
    {
        var fixture = new Fixture();
        await fixture.Service.CreateAsync(new("Empresa B", "empresa-b", "owner@empresa.test",
            "America/Sao_Paulo", "pt-BR"), "onboarding-1", "corr-1", default);

        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CreateAsync(
            new("Outra empresa", "outra-empresa", "owner@empresa.test",
                "America/Sao_Paulo", "pt-BR"), "onboarding-1", "corr-2", default));
    }

    [Fact]
    public async Task Exhausted_delivery_can_be_retried_and_completed_without_new_invitation()
    {
        var fixture = new Fixture();
        var created = await fixture.Service.CreateAsync(new("Empresa B", "empresa-b",
            "owner@empresa.test", "America/Sao_Paulo", "pt-BR"), "onboarding-1", "corr-1", default);
        fixture.Sender.FailuresRemaining = PlatformProvisioningOperation.MaximumAttempts;
        var processor = new PlatformOnboardingProcessor(fixture.Store, fixture.Scope,
            fixture.TokenFactory, fixture.Sender, fixture.Time);

        for (var attempt = 0; attempt < PlatformProvisioningOperation.MaximumAttempts; attempt++)
        {
            var claimed = await processor.ClaimNextAsync("worker-a", default);
            Assert.Equal(created.OperationId, claimed);
            await processor.ProcessAsync(claimed!.Value, default);
            fixture.Time.Advance(TimeSpan.FromHours(1));
        }

        var failed = await fixture.Service.GetAsync(created.OnboardingId, default);
        Assert.Equal(PlatformProvisioningOperationStatus.NeedsAttention, failed.OperationStatus);
        Assert.Equal(PlatformOnboardingStatus.NeedsAttention, failed.Status);

        var retried = await fixture.Service.RetryAsync(created.OnboardingId,
            failed.OperationVersion, "corr-retry", default);
        Assert.Equal(PlatformProvisioningOperationStatus.Pending, retried.OperationStatus);
        var retryClaim = await processor.ClaimNextAsync("worker-b", default);
        await processor.ProcessAsync(retryClaim!.Value, default);

        var completed = await fixture.Service.GetAsync(created.OnboardingId, default);
        Assert.Equal(PlatformProvisioningOperationStatus.Succeeded, completed.OperationStatus);
        Assert.Equal(PlatformOnboardingStatus.AwaitingOwner, completed.Status);
        Assert.Single(fixture.Store.Invitations);
        Assert.Single(fixture.Store.Outbox);
        Assert.NotNull(fixture.Store.Invitations[0].EmailSentAt);
    }

    [Fact]
    public void Expired_lease_is_claimable_after_worker_restart()
    {
        var now = new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        var operation = PlatformProvisioningOperation.Create(Guid.NewGuid(), "request-a",
            new string('A', 64), now);
        operation.Claim("worker-before-restart", now, TimeSpan.FromMinutes(2));

        Assert.False(operation.IsClaimable(now.AddMinutes(1)));
        Assert.True(operation.IsClaimable(now.AddMinutes(3)));
        operation.Claim("worker-after-restart", now.AddMinutes(3), TimeSpan.FromMinutes(2));
        Assert.Equal(2, operation.Attempts);
    }

    private sealed class Fixture
    {
        public TestTimeProvider Time { get; } = new();
        public StoreFake Store { get; } = new();
        public ActorFake Actor { get; } = new();
        public ScopeFake Scope { get; } = new();
        public TokenFactoryFake TokenFactory { get; } = new();
        public EmailSenderFake Sender { get; } = new();
        public PlatformOnboardingService Service { get; }

        public Fixture() => Service = new(Store, Actor, Scope, TokenFactory, Time);
    }

    private sealed class StoreFake : IPlatformOnboardingStore
    {
        public List<Organization> Organizations { get; } = [];
        public List<OrganizationInvitation> Invitations { get; } = [];
        public List<PlatformOnboarding> Onboardings { get; } = [];
        public List<PlatformProvisioningOperation> Operations { get; } = [];
        public List<PlatformOutboxMessage> Outbox { get; } = [];

        public Task<PlatformProvisioningOperation?> FindOperationByIdempotencyKeyAsync(string key, CancellationToken token) =>
            Task.FromResult(Operations.SingleOrDefault(item => item.IdempotencyKey == key));
        public Task<bool> OrganizationSlugExistsAsync(string slug, CancellationToken token) =>
            Task.FromResult(Organizations.Any(item => item.Slug == slug));
        public Task<(IReadOnlyCollection<PlatformOnboardingWork> Items, int Total)> ListAsync(
            PlatformOnboardingStatus? status, PlatformOnboardingSort sortBy,
            PlatformSortDirection sortDirection, int skip, int take, CancellationToken token)
        {
            var all = Onboardings.Where(item => !status.HasValue || item.Status == status.Value)
                .Select(Work).ToArray();
            return Task.FromResult<(IReadOnlyCollection<PlatformOnboardingWork>, int)>
                ((all.Skip(skip).Take(take).ToArray(), all.Length));
        }
        public Task<PlatformOnboardingWork?> FindAsync(Guid onboardingId, CancellationToken token) =>
            Task.FromResult(Onboardings.Where(item => item.Id == onboardingId).Select(Work).SingleOrDefault());
        public Task<PlatformOnboardingWork?> FindByOperationAsync(Guid operationId, CancellationToken token) =>
            Task.FromResult(Operations.Where(item => item.Id == operationId)
                .Select(item => Work(Onboardings.Single(onboarding => onboarding.Id == item.OnboardingId)))
                .SingleOrDefault());
        public Task<PlatformProvisioningOperation?> FindClaimableOperationAsync(DateTimeOffset now, CancellationToken token) =>
            Task.FromResult(Operations.Where(item => item.IsClaimable(now)).OrderBy(item => item.CreatedAt).FirstOrDefault());
        public Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token) => action(token);
        public void Add(object entity)
        {
            switch (entity)
            {
                case Organization value: Organizations.Add(value); break;
                case OrganizationInvitation value: Invitations.Add(value); break;
                case PlatformOnboarding value: Onboardings.Add(value); break;
                case PlatformProvisioningOperation value: Operations.Add(value); break;
                case PlatformOutboxMessage value: Outbox.Add(value); break;
            }
        }
        public Task SaveChangesAsync(CancellationToken token) => Task.CompletedTask;
        private PlatformOnboardingWork Work(PlatformOnboarding onboarding)
        {
            var operation = Operations.Single(item => item.OnboardingId == onboarding.Id);
            return new(onboarding, operation, Outbox.Single(item => item.OperationId == operation.Id),
                Invitations.Single(item => item.Id == onboarding.OwnerInvitationId),
                Organizations.Single(item => item.Id == onboarding.OrganizationId));
        }
    }

    private sealed class ActorFake : IPlatformActorContext
    {
        public bool IsAvailable => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public IReadOnlySet<string> Profiles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string>();
    }
    private sealed class ScopeFake : IIdentityOrganizationScope
    {
        public void SelectOrganizationForInvitation(Guid organizationId) { }
    }
    private sealed class TokenFactoryFake : IOnboardingInvitationTokenFactory
    {
        public string Create(Guid invitationId) => $"token-{invitationId:N}";
    }
    private sealed class EmailSenderFake : IInvitationEmailSender
    {
        public int FailuresRemaining { get; set; }
        public Task<string> SendAsync(Guid invitationId, string email, string organizationName,
            OrganizationRole role, string token, CancellationToken cancellationToken)
        {
            if (FailuresRemaining-- > 0) throw new HttpRequestException("Resend indisponível.");
            return Task.FromResult($"message-{invitationId:N}");
        }
    }
    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now = now.Add(duration);
    }
}
