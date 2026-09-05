using Microsoft.AspNetCore.Authorization;
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
