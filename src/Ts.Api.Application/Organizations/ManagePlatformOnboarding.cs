using System.Security.Cryptography;
using System.Text;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Application.Organizations;

public sealed record CreatePlatformOnboardingCommand(string Name, string Slug, string OwnerEmail,
    string TimeZone, string Locale);
public sealed record PlatformOnboardingSummary(Guid Id, Guid OrganizationId, string OrganizationName,
    string OrganizationSlug, string OwnerEmail, PlatformOnboardingStatus Status,
    PlatformProvisioningOperationStatus OperationStatus, int Attempts, DateTimeOffset UpdatedAt, long Version);
public sealed record PlatformOnboardingDetail(Guid Id, Guid OrganizationId, string OrganizationName,
    string OrganizationSlug, string OwnerEmail, string TimeZone, string Locale,
    PlatformOnboardingStatus Status, Guid OperationId, PlatformProvisioningOperationStatus OperationStatus,
    string CurrentStep, int Attempts, DateTimeOffset? NextAttemptAt, string? LastError,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version, long OperationVersion);
public sealed record PlatformOnboardingAccepted(Guid OnboardingId, Guid OperationId,
    PlatformProvisioningOperationStatus Status);
public sealed record PlatformOnboardingWork(PlatformOnboarding Onboarding,
    PlatformProvisioningOperation Operation, PlatformOutboxMessage Outbox,
    OrganizationInvitation Invitation, Organization Organization);
public enum PlatformOnboardingSort { OrganizationName, OwnerEmail, Status, Attempts, UpdatedAt }

public interface IOnboardingInvitationTokenFactory
{
    string Create(Guid invitationId);
}

public interface IPlatformOnboardingStore
{
    Task<PlatformProvisioningOperation?> FindOperationByIdempotencyKeyAsync(string key, CancellationToken token);
    Task<bool> OrganizationSlugExistsAsync(string slug, CancellationToken token);
    Task<(IReadOnlyCollection<PlatformOnboardingWork> Items, int Total)> ListAsync(
        PlatformOnboardingStatus? status, PlatformOnboardingSort sortBy,
        PlatformSortDirection sortDirection, int skip, int take, CancellationToken token);
    Task<PlatformOnboardingWork?> FindAsync(Guid onboardingId, CancellationToken token);
    Task<PlatformOnboardingWork?> FindByOperationAsync(Guid operationId, CancellationToken token);
    Task<PlatformProvisioningOperation?> FindClaimableOperationAsync(DateTimeOffset now, CancellationToken token);
    Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token);
    void Add(object entity);
    Task SaveChangesAsync(CancellationToken token);
}

public sealed class PlatformOnboardingService(IPlatformOnboardingStore store,
    IPlatformActorContext actor, IIdentityOrganizationScope organizationScope,
    IOnboardingInvitationTokenFactory tokenFactory, TimeProvider timeProvider)
{
    public Task<PlatformOnboardingAccepted> CreateAsync(CreatePlatformOnboardingCommand command,
        string idempotencyKey, string correlationId, CancellationToken token)
    {
        var fingerprint = Fingerprint(command);
        return store.ExecuteSerializableAsync(async transactionalToken =>
        {
            var existingOperation = await store.FindOperationByIdempotencyKeyAsync(idempotencyKey, transactionalToken);
            if (existingOperation is not null)
            {
                if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(existingOperation.RequestFingerprint), Encoding.UTF8.GetBytes(fingerprint)))
                    throw new ConflictException("A chave idempotente já foi usada com outro conteúdo.");
                return new PlatformOnboardingAccepted(existingOperation.OnboardingId,
                    existingOperation.Id, existingOperation.Status);
            }

            var organization = Organization.CreateProvisioning(command.Name, command.Slug);
            if (await store.OrganizationSlugExistsAsync(organization.Slug, transactionalToken))
                throw new ConflictException("Já existe uma organização com este identificador.");

            organizationScope.SelectOrganizationForInvitation(organization.Id);
            var now = timeProvider.GetUtcNow();
            var invitationId = Guid.NewGuid();
            var invitationToken = tokenFactory.Create(invitationId);
            var invitation = OrganizationInvitation.Create(organization.Id, command.OwnerEmail,
                OrganizationRole.Owner, MembershipService.HashToken(invitationToken), actor.UserId,
                now, now.AddDays(7), invitationId);
            var onboarding = PlatformOnboarding.Create(organization.Id, invitation.Id,
                command.OwnerEmail, command.TimeZone, command.Locale, now);
            var operation = PlatformProvisioningOperation.Create(onboarding.Id, idempotencyKey,
                fingerprint, now);
            var outbox = PlatformOutboxMessage.CreateOwnerInvitation(operation.Id, invitation.Id, now);

            store.Add(organization); store.Add(invitation); store.Add(onboarding);
            store.Add(operation); store.Add(outbox);
            store.Add(PlatformAuditEvent.Create(actor.UserId, "PlatformOperator",
                "platform-onboarding.created", "PlatformOnboarding", onboarding.Id,
                "Accepted", "Onboarding registrado para provisionamento durável.", now, correlationId));
            await store.SaveChangesAsync(transactionalToken);
            return new PlatformOnboardingAccepted(onboarding.Id, operation.Id, operation.Status);
        }, token);
    }

    public async Task<PageResult<PlatformOnboardingSummary>> ListAsync(PlatformOnboardingStatus? status,
        PlatformOnboardingSort? sortBy, PlatformSortDirection? sortDirection,
        int page, int pageSize, CancellationToken token)
    {
        ValidatePage(page, pageSize);
        var result = await store.ListAsync(status, sortBy ?? PlatformOnboardingSort.UpdatedAt,
            sortDirection ?? PlatformSortDirection.Desc,
            (page - 1) * pageSize, pageSize, token);
        return new(result.Items.Select(MapSummary).ToArray(), page, pageSize, result.Total);
    }

    public async Task<PlatformOnboardingDetail> GetAsync(Guid id, CancellationToken token) =>
        MapDetail(await store.FindAsync(id, token)
            ?? throw new ResourceNotFoundException("Onboarding não encontrado."));

    public Task<PlatformOnboardingDetail> RetryAsync(Guid id, long expectedVersion,
        string correlationId, CancellationToken token) => store.ExecuteSerializableAsync(async transactionalToken =>
    {
        var work = await store.FindAsync(id, transactionalToken)
            ?? throw new ResourceNotFoundException("Onboarding não encontrado.");
        if (work.Operation.Version != expectedVersion)
            throw new PreconditionFailedException("A operação foi alterada. Recarregue os dados.");
        var now = timeProvider.GetUtcNow();
        work.Operation.Retry(now);
        work.Onboarding.Resume(now);
        store.Add(PlatformAuditEvent.Create(actor.UserId, "PlatformOperator",
            "platform-onboarding.retried", "PlatformOnboarding", id, "Accepted",
            "Retentativa manual solicitada.", now, correlationId));
        await store.SaveChangesAsync(transactionalToken);
        return MapDetail(work);
    }, token);

    internal static string Fingerprint(CreatePlatformOnboardingCommand command)
    {
        var canonical = string.Join('\n', command.Name.Trim(), command.Slug.Trim().ToLowerInvariant(),
            OrganizationInvitation.NormalizeEmail(command.OwnerEmail), command.TimeZone.Trim(), command.Locale.Trim());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static PlatformOnboardingSummary MapSummary(PlatformOnboardingWork work) => new(
        work.Onboarding.Id, work.Organization.Id, work.Organization.Name, work.Organization.Slug,
        work.Onboarding.OwnerEmail, work.Onboarding.Status, work.Operation.Status,
        work.Operation.Attempts, work.Onboarding.UpdatedAt, work.Onboarding.Version);

    private static PlatformOnboardingDetail MapDetail(PlatformOnboardingWork work) => new(
        work.Onboarding.Id, work.Organization.Id, work.Organization.Name, work.Organization.Slug,
        work.Onboarding.OwnerEmail, work.Onboarding.TimeZone, work.Onboarding.Locale,
        work.Onboarding.Status, work.Operation.Id, work.Operation.Status,
        work.Operation.CurrentStep, work.Operation.Attempts, work.Operation.NextAttemptAt,
        work.Operation.LastError, work.Onboarding.CreatedAt, work.Onboarding.UpdatedAt,
        work.Onboarding.Version, work.Operation.Version);

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            throw new DomainException("A página deve ser positiva e pageSize deve estar entre 1 e 100.");
    }
}

public sealed class PlatformOnboardingProcessor(IPlatformOnboardingStore store,
    IIdentityOrganizationScope organizationScope, IOnboardingInvitationTokenFactory tokenFactory,
    IInvitationEmailSender emailSender, TimeProvider timeProvider)
{
    public Task<Guid?> ClaimNextAsync(string workerId, CancellationToken token) =>
        store.ExecuteSerializableAsync<Guid?>(async transactionalToken =>
        {
            var now = timeProvider.GetUtcNow();
            var operation = await store.FindClaimableOperationAsync(now, transactionalToken);
            if (operation is null) return null;
            operation.Claim(workerId, now, TimeSpan.FromMinutes(2));
            await store.SaveChangesAsync(transactionalToken);
            return operation.Id;
        }, token);

    public async Task ProcessAsync(Guid operationId, CancellationToken token)
    {
        var work = await store.FindByOperationAsync(operationId, token)
            ?? throw new ResourceNotFoundException("Operação de provisionamento não encontrada.");
        organizationScope.SelectOrganizationForInvitation(work.Organization.Id);
        try
        {
            var plainToken = tokenFactory.Create(work.Invitation.Id);
            var messageId = await emailSender.SendAsync(work.Invitation.Id, work.Invitation.Email,
                work.Organization.Name, work.Invitation.Role, plainToken, token);
            var now = timeProvider.GetUtcNow();
            work.Invitation.MarkSent(messageId, now);
            work.Outbox.MarkDelivered(now);
            work.Operation.Complete(now);
            work.Onboarding.MarkAwaitingOwner(now);
            await store.SaveChangesAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            var now = timeProvider.GetUtcNow();
            if (work.Operation.Fail(exception.Message, now)) work.Onboarding.MarkNeedsAttention(now);
            await store.SaveChangesAsync(CancellationToken.None);
        }
    }
}
