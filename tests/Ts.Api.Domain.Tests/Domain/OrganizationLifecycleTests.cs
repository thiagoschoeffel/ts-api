using Ts.Api.Domain.Common;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Domain.Tests.Domain;

public sealed class OrganizationLifecycleTests
{
    [Fact]
    public void Existing_creation_remains_active_and_versioned()
    {
        var organization = Organization.Create("Sabor Santè", "sabor-sante");
        Assert.Equal(OrganizationLifecycleStatus.Active, organization.LifecycleStatus);
        Assert.True(organization.IsActive);
        Assert.Equal(1, organization.Version);
    }

    [Fact]
    public void Provisioning_creation_is_not_available_to_business_apis()
    {
        var organization = Organization.CreateProvisioning("Empresa B", "empresa-b");
        Assert.Equal(OrganizationLifecycleStatus.Provisioning, organization.LifecycleStatus);
        Assert.False(organization.IsActive);
        Assert.Equal(1, organization.Version);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("empresa com espaço")]
    [InlineData("empresa--b")]
    public void Rejects_reserved_or_non_normalized_slugs(string slug)
    {
        Assert.Throws<DomainException>(() => Organization.CreateProvisioning("Empresa", slug));
    }

    [Fact]
    public void Lifecycle_change_keeps_legacy_active_flag_consistent()
    {
        var organization = Organization.Create("Empresa", "empresa");
        organization.SetStatus(OrganizationLifecycleStatus.Suspended);
        Assert.False(organization.IsActive);
        Assert.Equal(2, organization.Version);
    }
}
