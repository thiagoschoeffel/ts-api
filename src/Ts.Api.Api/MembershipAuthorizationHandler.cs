using Microsoft.AspNetCore.Authorization;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Api;

public sealed class MembershipRoleRequirement(params OrganizationRole[] allowedRoles) : IAuthorizationRequirement
{
    public IReadOnlySet<OrganizationRole> AllowedRoles { get; } = allowedRoles.ToHashSet();
}

public sealed class MembershipAuthorizationHandler(IHttpContextAccessor accessor)
    : AuthorizationHandler<MembershipRoleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        MembershipRoleRequirement requirement)
    {
        if (accessor.HttpContext?.Items[AuthorizationPolicies.MembershipRoleItem]
                is OrganizationRole role
            && requirement.AllowedRoles.Contains(role))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed class PlatformCapabilityRequirement(params string[] capabilities) : IAuthorizationRequirement
{
    public IReadOnlySet<string> Capabilities { get; } = capabilities.ToHashSet(StringComparer.Ordinal);
}

public sealed class PlatformCapabilityAuthorizationHandler(IPlatformActorContext actor)
    : AuthorizationHandler<PlatformCapabilityRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PlatformCapabilityRequirement requirement)
    {
        if (actor.IsAvailable && requirement.Capabilities.All(actor.Capabilities.Contains))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
