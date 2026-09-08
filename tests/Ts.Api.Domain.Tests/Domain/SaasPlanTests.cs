using Ts.Api.Domain.Common;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Domain.Tests.Domain;

public sealed class SaasPlanTests
{
    [Fact]
    public void Plan_versions_are_explicit_and_entitlements_are_normalized()
    {
        var plan = SaasPlanVersion.Create(" COMPLETE ", "Completo", 1, true, DateTimeOffset.UtcNow);
        var entitlement = SaasPlanEntitlement.Create(plan.Id, " BUSINESS.ACCESS ");

        Assert.Equal("complete", plan.Code);
        Assert.Equal(SaasEntitlements.BusinessAccess, entitlement.Code);
        Assert.True(plan.IsAvailable);
    }

    [Fact]
    public void Subscription_requires_the_expected_version_when_reassigned()
    {
        var subscription = OrganizationSaasSubscription.Create(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => subscription.Assign(Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, expectedVersion: 2));
    }
}
