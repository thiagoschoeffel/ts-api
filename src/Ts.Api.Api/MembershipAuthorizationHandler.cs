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

public sealed class PlatformMfaRequirement(string claimType, string claimValue) : IAuthorizationRequirement
{
    public string ClaimType { get; } = !string.IsNullOrWhiteSpace(claimType)
        ? claimType : throw new ArgumentException("O tipo da claim MFA é obrigatório.", nameof(claimType));
    public string ClaimValue { get; } = !string.IsNullOrWhiteSpace(claimValue)
        ? claimValue : throw new ArgumentException("O valor da claim MFA é obrigatório.", nameof(claimValue));
}

public sealed class PlatformMfaAuthorizationHandler : AuthorizationHandler<PlatformMfaRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PlatformMfaRequirement requirement)
    {
        var hasMfa = context.User.FindAll(requirement.ClaimType)
            .SelectMany(claim => claim.Value.Split([' ', '[', ']', ',', '"'],
                StringSplitOptions.RemoveEmptyEntries))
            .Any(value => string.Equals(value, requirement.ClaimValue, StringComparison.Ordinal));
        if (hasMfa) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
