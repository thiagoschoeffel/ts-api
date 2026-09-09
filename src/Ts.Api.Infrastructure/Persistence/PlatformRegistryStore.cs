using Microsoft.EntityFrameworkCore;
using System.Data;
using Ts.Api.Application.Organizations;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Infrastructure.Persistence;

public sealed class PlatformRegistryStore(AppDbContext database) : IPlatformRegistryStore
{
    public async Task<(IReadOnlyCollection<Organization> Items, int Total)> ListOrganizationsAsync(
        string? search, OrganizationLifecycleStatus? status, PlatformOrganizationSort sortBy,
        PlatformSortDirection sortDirection, int skip, int take, CancellationToken token)
    {
        var query = database.Organizations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(item => EF.Functions.ILike(item.Name, pattern)
                || EF.Functions.ILike(item.Slug, pattern));
        }
        if (status.HasValue) query = query.Where(item => item.LifecycleStatus == status);
        var total = await query.CountAsync(token);
        var descending = sortDirection == PlatformSortDirection.Desc;
        var ordered = sortBy switch
        {
            PlatformOrganizationSort.Slug => descending ? query.OrderByDescending(item => item.Slug) : query.OrderBy(item => item.Slug),
            PlatformOrganizationSort.Status => descending ? query.OrderByDescending(item => item.LifecycleStatus) : query.OrderBy(item => item.LifecycleStatus),
            PlatformOrganizationSort.Version => descending ? query.OrderByDescending(item => item.Version) : query.OrderBy(item => item.Version),
            _ => descending ? query.OrderByDescending(item => item.Name) : query.OrderBy(item => item.Name)
        };
        var stableOrder = descending
            ? ordered.ThenByDescending(item => item.Id)
            : ordered.ThenBy(item => item.Id);
        var items = await stableOrder
            .Skip(skip).Take(take).ToArrayAsync(token);
        return (items, total);
    }

    public Task<Organization?> FindOrganizationAsync(Guid id, CancellationToken token) =>
        database.Organizations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, token);

    public async Task<(IReadOnlyCollection<PlatformAuditEvent> Items, int Total)> ListAuditAsync(
        string? action, Guid? targetId, PlatformAuditSort sortBy,
        PlatformSortDirection sortDirection, int skip, int take, CancellationToken token)
    {
        var query = database.PlatformAuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(item => item.Action == action);
        if (targetId.HasValue) query = query.Where(item => item.TargetId == targetId);
        var total = await query.CountAsync(token);
        var descending = sortDirection == PlatformSortDirection.Desc;
        var ordered = sortBy switch
        {
            PlatformAuditSort.Action => descending ? query.OrderByDescending(item => item.Action) : query.OrderBy(item => item.Action),
            PlatformAuditSort.ActorKind => descending ? query.OrderByDescending(item => item.ActorKind) : query.OrderBy(item => item.ActorKind),
            PlatformAuditSort.Result => descending ? query.OrderByDescending(item => item.Result) : query.OrderBy(item => item.Result),
            _ => descending ? query.OrderByDescending(item => item.OccurredAt) : query.OrderBy(item => item.OccurredAt)
        };
        var stableOrder = descending
            ? ordered.ThenByDescending(item => item.Id)
            : ordered.ThenBy(item => item.Id);
        var items = await stableOrder
            .Skip(skip).Take(take).ToArrayAsync(token);
        return (items, total);
    }
}

public sealed class PlatformLifecycleStore(AppDbContext database) : IPlatformLifecycleStore
{
    public async Task<IReadOnlyCollection<(SaasPlanVersion Plan, IReadOnlyCollection<string> Entitlements)>>
        ListAvailablePlansAsync(CancellationToken token)
    {
        var plans = await database.SaasPlanVersions.AsNoTracking().Where(item => item.IsAvailable)
            .OrderBy(item => item.Name).ThenByDescending(item => item.Version).ToArrayAsync(token);
        var ids = plans.Select(item => item.Id).ToArray();
        var entitlements = await database.SaasPlanEntitlements.AsNoTracking()
            .Where(item => ids.Contains(item.PlanVersionId)).ToArrayAsync(token);
        return plans.Select(plan => (plan, (IReadOnlyCollection<string>)entitlements
            .Where(item => item.PlanVersionId == plan.Id).Select(item => item.Code).ToArray())).ToArray();
    }

    public Task<SaasPlanVersion?> FindPlanAsync(Guid id, CancellationToken token) =>
        database.SaasPlanVersions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, token);

    public async Task<IReadOnlyCollection<string>> GetEntitlementsAsync(Guid planVersionId,
        CancellationToken token) => await database.SaasPlanEntitlements.AsNoTracking()
        .Where(item => item.PlanVersionId == planVersionId).Select(item => item.Code).ToArrayAsync(token);

    public Task<Organization?> FindOrganizationAsync(Guid id, CancellationToken token) =>
        database.Organizations.SingleOrDefaultAsync(item => item.Id == id, token);

    public Task<OrganizationSaasSubscription?> FindSubscriptionAsync(Guid organizationId,
        CancellationToken token) => database.OrganizationSaasSubscriptions
        .SingleOrDefaultAsync(item => item.OrganizationId == organizationId, token);

    public Task<bool> HasActiveOwnerAsync(Guid organizationId, CancellationToken token) =>
        database.OrganizationMemberships.IgnoreQueryFilters().AnyAsync(item =>
            item.OrganizationId == organizationId && item.Role == OrganizationRole.Owner && item.IsActive, token);

    public async Task<bool> IsOnboardingReadyAsync(Guid organizationId, CancellationToken token)
    {
        var onboarding = await database.PlatformOnboardings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OrganizationId == organizationId, token);
        if (onboarding is null) return true;
        return await database.PlatformProvisioningOperations.AsNoTracking().AnyAsync(item =>
            item.OnboardingId == onboarding.Id && item.Status == PlatformProvisioningOperationStatus.Succeeded, token);
    }

    public async Task MarkOnboardingActiveAsync(Guid organizationId, DateTimeOffset now,
        CancellationToken token)
    {
        var onboarding = await database.PlatformOnboardings
            .SingleOrDefaultAsync(item => item.OrganizationId == organizationId, token);
        if (onboarding is not null) onboarding.MarkActive(now);
    }

    public async Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> action,
        CancellationToken token)
    {
        if (!database.Database.IsRelational()) return await action(token);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var result = await action(token);
        await transaction.CommitAsync(token);
        return result;
    }

    public void Add(object entity) => database.Add(entity);
    public Task SaveChangesAsync(CancellationToken token) => database.SaveChangesAsync(token);
}

public sealed class PlatformOnboardingStore(AppDbContext database) : IPlatformOnboardingStore
{
    public Task<PlatformProvisioningOperation?> FindOperationByIdempotencyKeyAsync(
        string key, CancellationToken token) => database.PlatformProvisioningOperations
        .SingleOrDefaultAsync(item => item.IdempotencyKey == key, token);

    public Task<bool> OrganizationSlugExistsAsync(string slug, CancellationToken token) =>
        database.Organizations.AnyAsync(item => item.Slug == slug, token);

    public async Task<(IReadOnlyCollection<PlatformOnboardingWork> Items, int Total)> ListAsync(
        PlatformOnboardingStatus? status, PlatformOnboardingSort sortBy,
        PlatformSortDirection sortDirection, int skip, int take, CancellationToken token)
    {
        var query = database.PlatformOnboardings.AsNoTracking();
        if (status.HasValue) query = query.Where(item => item.Status == status);
        var total = await query.CountAsync(token);
        var sortableQuery = query
            .Join(database.Organizations.AsNoTracking(), onboarding => onboarding.OrganizationId,
                organization => organization.Id, (onboarding, organization) => new
                {
                    Onboarding = onboarding,
                    OrganizationName = organization.Name
                })
            .Join(database.PlatformProvisioningOperations.AsNoTracking(),
                item => item.Onboarding.Id, operation => operation.OnboardingId,
                (item, operation) => new
                {
                    item.Onboarding,
                    item.OrganizationName,
                    operation.Attempts
                });
        var descending = sortDirection == PlatformSortDirection.Desc;
        var ordered = sortBy switch
        {
            PlatformOnboardingSort.OrganizationName => descending
                ? sortableQuery.OrderByDescending(item => item.OrganizationName)
                : sortableQuery.OrderBy(item => item.OrganizationName),
            PlatformOnboardingSort.OwnerEmail => descending
                ? sortableQuery.OrderByDescending(item => item.Onboarding.OwnerEmail)
                : sortableQuery.OrderBy(item => item.Onboarding.OwnerEmail),
            PlatformOnboardingSort.Status => descending
                ? sortableQuery.OrderByDescending(item => item.Onboarding.Status)
                : sortableQuery.OrderBy(item => item.Onboarding.Status),
            PlatformOnboardingSort.Attempts => descending
                ? sortableQuery.OrderByDescending(item => item.Attempts)
                : sortableQuery.OrderBy(item => item.Attempts),
            _ => descending
                ? sortableQuery.OrderByDescending(item => item.Onboarding.UpdatedAt)
                : sortableQuery.OrderBy(item => item.Onboarding.UpdatedAt)
        };
        var onboardings = await ordered.ThenBy(item => item.Onboarding.Id)
            .Select(item => item.Onboarding)
            .Skip(skip).Take(take).ToArrayAsync(token);
        var items = new List<PlatformOnboardingWork>(onboardings.Length);
        foreach (var onboarding in onboardings)
            items.Add((await LoadAsync(onboarding, true, token))!);
        return (items, total);
    }

    public async Task<PlatformOnboardingWork?> FindAsync(Guid onboardingId, CancellationToken token)
    {
        var onboarding = await database.PlatformOnboardings.SingleOrDefaultAsync(item => item.Id == onboardingId, token);
        return onboarding is null ? null : await LoadAsync(onboarding, false, token);
    }

    public async Task<PlatformOnboardingWork?> FindByOperationAsync(Guid operationId, CancellationToken token)
    {
        var operation = await database.PlatformProvisioningOperations
            .SingleOrDefaultAsync(item => item.Id == operationId, token);
        if (operation is null) return null;
        var onboarding = await database.PlatformOnboardings
            .SingleAsync(item => item.Id == operation.OnboardingId, token);
        return await LoadAsync(onboarding, false, token, operation);
    }

    public Task<PlatformProvisioningOperation?> FindClaimableOperationAsync(
        DateTimeOffset now, CancellationToken token) => database.PlatformProvisioningOperations
        .Where(item => (item.Status == PlatformProvisioningOperationStatus.Pending
                && item.NextAttemptAt <= now)
            || (item.Status == PlatformProvisioningOperationStatus.Running
                && item.LeaseUntil <= now))
        .OrderBy(item => item.NextAttemptAt ?? item.LeaseUntil)
        .ThenBy(item => item.CreatedAt).ThenBy(item => item.Id)
        .FirstOrDefaultAsync(token);

    public async Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> action,
        CancellationToken token)
    {
        if (!database.Database.IsRelational()) return await action(token);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var result = await action(token);
        await transaction.CommitAsync(token);
        return result;
    }

    public void Add(object entity) => database.Add(entity);
    public Task SaveChangesAsync(CancellationToken token) => database.SaveChangesAsync(token);

    private async Task<PlatformOnboardingWork?> LoadAsync(PlatformOnboarding onboarding,
        bool noTracking, CancellationToken token, PlatformProvisioningOperation? loadedOperation = null)
    {
        var operations = noTracking ? database.PlatformProvisioningOperations.AsNoTracking()
            : database.PlatformProvisioningOperations;
        var outboxMessages = noTracking ? database.PlatformOutboxMessages.AsNoTracking()
            : database.PlatformOutboxMessages;
        var invitations = noTracking ? database.OrganizationInvitations.IgnoreQueryFilters().AsNoTracking()
            : database.OrganizationInvitations.IgnoreQueryFilters();
        var organizations = noTracking ? database.Organizations.AsNoTracking() : database.Organizations;
        var operation = loadedOperation ?? await operations.SingleAsync(item => item.OnboardingId == onboarding.Id, token);
        var outbox = await outboxMessages.SingleAsync(item => item.OperationId == operation.Id, token);
        var invitation = await invitations.SingleAsync(item => item.Id == onboarding.OwnerInvitationId, token);
        var organization = await organizations.SingleAsync(item => item.Id == onboarding.OrganizationId, token);
        return new(onboarding, operation, outbox, invitation, organization);
    }
}
