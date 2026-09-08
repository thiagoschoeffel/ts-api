using Microsoft.EntityFrameworkCore;
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
