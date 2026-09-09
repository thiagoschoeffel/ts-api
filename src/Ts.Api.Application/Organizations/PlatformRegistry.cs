using Ts.Api.Domain.Common;
using Ts.Api.Domain.Organizations;
using Ts.Api.Application.Common;

namespace Ts.Api.Application.Organizations;

public sealed record PlatformOrganizationSummary(Guid Id, string Name, string Slug,
    OrganizationLifecycleStatus Status, long Version);
public sealed record PlatformOrganizationDetail(Guid Id, string Name, string Slug,
    OrganizationLifecycleStatus Status, long Version);
public sealed record PlatformAuditResult(Guid Id, Guid? ActorUserId, string ActorKind,
    string Action, string TargetType, Guid TargetId, string Result, string Reason,
    DateTimeOffset OccurredAt, string CorrelationId);
public sealed record PageResult<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int Total);
public enum PlatformOrganizationSort { Name, Slug, Status, Version }
public enum PlatformAuditSort { Action, ActorKind, Result, OccurredAt }
public enum PlatformSortDirection { Asc, Desc }

public interface IPlatformRegistryStore
{
    Task<(IReadOnlyCollection<Organization> Items, int Total)> ListOrganizationsAsync(
        string? search, OrganizationLifecycleStatus? status, PlatformOrganizationSort sortBy,
        PlatformSortDirection sortDirection, int skip, int take, CancellationToken token);
    Task<Organization?> FindOrganizationAsync(Guid id, CancellationToken token);
    Task<(IReadOnlyCollection<PlatformAuditEvent> Items, int Total)> ListAuditAsync(
        string? action, Guid? targetId, PlatformAuditSort sortBy,
        PlatformSortDirection sortDirection, int skip, int take, CancellationToken token);
}

public sealed class PlatformRegistryService(IPlatformRegistryStore store)
{
    public async Task<PageResult<PlatformOrganizationSummary>> ListOrganizationsAsync(
        string? search, OrganizationLifecycleStatus? status, PlatformOrganizationSort? sortBy,
        PlatformSortDirection? sortDirection, int page, int pageSize, CancellationToken token)
    {
        ValidatePage(page, pageSize);
        var result = await store.ListOrganizationsAsync(search?.Trim(), status,
            sortBy ?? PlatformOrganizationSort.Name, sortDirection ?? PlatformSortDirection.Asc,
            (page - 1) * pageSize, pageSize, token);
        return new(result.Items.Select(MapSummary).ToArray(), page, pageSize, result.Total);
    }

    public async Task<PlatformOrganizationDetail> GetOrganizationAsync(Guid id,
        CancellationToken token)
    {
        var organization = await store.FindOrganizationAsync(id, token)
            ?? throw new ResourceNotFoundException("Organização não encontrada.");
        return new(organization.Id, organization.Name, organization.Slug,
            organization.LifecycleStatus, organization.Version);
    }

    public async Task<PageResult<PlatformAuditResult>> ListAuditAsync(string? action,
        Guid? targetId, PlatformAuditSort? sortBy, PlatformSortDirection? sortDirection,
        int page, int pageSize, CancellationToken token)
    {
        ValidatePage(page, pageSize);
        var result = await store.ListAuditAsync(action?.Trim(), targetId,
            sortBy ?? PlatformAuditSort.OccurredAt, sortDirection ?? PlatformSortDirection.Desc,
            (page - 1) * pageSize, pageSize, token);
        return new(result.Items.Select(item => new PlatformAuditResult(item.Id, item.ActorUserId,
            item.ActorKind, item.Action, item.TargetType, item.TargetId, item.Result, item.Reason,
            item.OccurredAt, item.CorrelationId)).ToArray(), page, pageSize, result.Total);
    }

    private static PlatformOrganizationSummary MapSummary(Organization item) =>
        new(item.Id, item.Name, item.Slug, item.LifecycleStatus, item.Version);

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            throw new DomainException("A página deve ser positiva e pageSize deve estar entre 1 e 100.");
    }
}
