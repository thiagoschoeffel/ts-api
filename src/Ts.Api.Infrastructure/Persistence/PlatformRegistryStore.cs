using Microsoft.EntityFrameworkCore;
using System.Data;
using Ts.Api.Application.Organizations;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Infrastructure.Persistence;

public sealed class PlatformRegistryStore(AppDbContext database) : IPlatformRegistryStore
{
    public async Task<(IReadOnlyCollection<Organization> Items, int Total)> ListOrganizationsAsync(
        string? search, OrganizationLifecycleStatus? status, int skip, int take,
        CancellationToken token)
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
        var items = await query.OrderBy(item => item.Name).ThenBy(item => item.Id)
            .Skip(skip).Take(take).ToArrayAsync(token);
        return (items, total);
    }

    public Task<Organization?> FindOrganizationAsync(Guid id, CancellationToken token) =>
        database.Organizations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, token);

    public async Task<(IReadOnlyCollection<PlatformAuditEvent> Items, int Total)> ListAuditAsync(
        string? action, Guid? targetId, int skip, int take, CancellationToken token)
    {
        var query = database.PlatformAuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(item => item.Action == action);
        if (targetId.HasValue) query = query.Where(item => item.TargetId == targetId);
        var total = await query.CountAsync(token);
        var items = await query.OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id)
            .Skip(skip).Take(take).ToArrayAsync(token);
        return (items, total);
    }
}

public sealed class PlatformOnboardingStore(AppDbContext database) : IPlatformOnboardingStore
{
    public Task<PlatformProvisioningOperation?> FindOperationByIdempotencyKeyAsync(
        string key, CancellationToken token) => database.PlatformProvisioningOperations
        .SingleOrDefaultAsync(item => item.IdempotencyKey == key, token);

    public Task<bool> OrganizationSlugExistsAsync(string slug, CancellationToken token) =>
        database.Organizations.AnyAsync(item => item.Slug == slug, token);

    public async Task<(IReadOnlyCollection<PlatformOnboardingWork> Items, int Total)> ListAsync(
        PlatformOnboardingStatus? status, int skip, int take, CancellationToken token)
    {
        var query = database.PlatformOnboardings.AsNoTracking();
        if (status.HasValue) query = query.Where(item => item.Status == status);
        var total = await query.CountAsync(token);
        var onboardings = await query.OrderByDescending(item => item.UpdatedAt).ThenBy(item => item.Id)
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
