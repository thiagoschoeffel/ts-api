using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Ts.Api.Api;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Domain.Tests.Security;

public sealed class MembershipAuthorizationHandlerTests
{
    [Fact]
    public async Task Operator_cannot_satisfy_administration_policy()
    {
        var context = CreateContext(OrganizationRole.Operator,
            new MembershipRoleRequirement(OrganizationRole.Owner, OrganizationRole.Administrator));

        await context.Handler.HandleAsync(context.AuthorizationContext);

        Assert.False(context.AuthorizationContext.HasSucceeded);
    }

    [Fact]
    public async Task Administrator_satisfies_administration_policy()
    {
        var context = CreateContext(OrganizationRole.Administrator,
            new MembershipRoleRequirement(OrganizationRole.Owner, OrganizationRole.Administrator));

        await context.Handler.HandleAsync(context.AuthorizationContext);

        Assert.True(context.AuthorizationContext.HasSucceeded);
    }

    private static TestContext CreateContext(OrganizationRole role,
        MembershipRoleRequirement requirement)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[AuthorizationPolicies.MembershipRoleItem] = role;
        var handler = new MembershipAuthorizationHandler(new HttpContextAccessor
        {
            HttpContext = httpContext,
        });
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], "test"));
        return new TestContext(handler,
            new AuthorizationHandlerContext([requirement], principal, null));
    }

    private sealed record TestContext(
        MembershipAuthorizationHandler Handler,
        AuthorizationHandlerContext AuthorizationContext);
}
