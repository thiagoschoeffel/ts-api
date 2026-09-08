using Microsoft.AspNetCore.Authorization;
using Ts.Api.Api;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Domain.Tests.Security;

public sealed class PlatformAuthorizationTests
{
    [Fact]
    public async Task Tenant_owner_does_not_receive_platform_capabilities()
    {
        var actor = new PlatformActorFake([]);
        var requirement = new PlatformCapabilityRequirement(PlatformCapabilities.OrganizationsRead);
        var context = new AuthorizationHandlerContext([requirement], new(), null);

        await new PlatformCapabilityAuthorizationHandler(actor).HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [InlineData(PlatformOperatorProfile.PlatformAdministrator, PlatformCapabilities.OrganizationsAdminister)]
    [InlineData(PlatformOperatorProfile.PlatformOnboardingOperator, PlatformCapabilities.OnboardingManage)]
    [InlineData(PlatformOperatorProfile.PlatformSupportReader, PlatformCapabilities.AuditRead)]
    public async Task Global_profiles_receive_only_their_declared_capabilities(
        PlatformOperatorProfile profile, string capability)
    {
        var actor = new PlatformActorFake(PlatformCapabilities.ForProfile(profile));
        var requirement = new PlatformCapabilityRequirement(capability);
        var context = new AuthorizationHandlerContext([requirement], new(), null);

        await new PlatformCapabilityAuthorizationHandler(actor).HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Support_reader_cannot_administer_organizations()
    {
        var actor = new PlatformActorFake(
            PlatformCapabilities.ForProfile(PlatformOperatorProfile.PlatformSupportReader));
        var requirement = new PlatformCapabilityRequirement(PlatformCapabilities.OrganizationsAdminister);
        var context = new AuthorizationHandlerContext([requirement], new(), null);

        await new PlatformCapabilityAuthorizationHandler(actor).HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private sealed class PlatformActorFake(IEnumerable<string> capabilities) : IPlatformActorContext
    {
        public bool IsAvailable => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public IReadOnlySet<string> Profiles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Capabilities { get; } = capabilities.ToHashSet(StringComparer.Ordinal);
    }
}
