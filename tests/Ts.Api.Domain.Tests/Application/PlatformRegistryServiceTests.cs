using Ts.Api.Application.Organizations;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Domain.Tests.Application;

public sealed class PlatformRegistryServiceTests
{
    [Fact]
    public async Task Returns_stable_page_metadata_without_operational_data()
    {
        var organization = Organization.Create("Sabor Santè", "sabor-sante");
        var service = new PlatformRegistryService(new StoreFake([organization]));
        var result = await service.ListOrganizationsAsync(null, null, 1, 20, default);
        var item = Assert.Single(result.Items);
        Assert.Equal(organization.Id, item.Id);
        Assert.Equal(1, result.Total);
        Assert.Equal(OrganizationLifecycleStatus.Active, item.Status);
    }

    private sealed class StoreFake(IReadOnlyCollection<Organization> organizations)
        : IPlatformRegistryStore
    {
        public Task<(IReadOnlyCollection<Organization> Items, int Total)> ListOrganizationsAsync(
            string? search, OrganizationLifecycleStatus? status, int skip, int take,
            CancellationToken token)
        {
            var filtered = organizations.Where(item => !status.HasValue
                || item.LifecycleStatus == status).Skip(skip).Take(take).ToArray();
            return Task.FromResult<(IReadOnlyCollection<Organization>, int)>((filtered, organizations.Count));
        }

        public Task<Organization?> FindOrganizationAsync(Guid id, CancellationToken token) =>
            Task.FromResult(organizations.SingleOrDefault(item => item.Id == id));

        public Task<(IReadOnlyCollection<PlatformAuditEvent> Items, int Total)> ListAuditAsync(
            string? action, Guid? targetId, int skip, int take, CancellationToken token) =>
            Task.FromResult<(IReadOnlyCollection<PlatformAuditEvent>, int)>(([], 0));
    }
}
