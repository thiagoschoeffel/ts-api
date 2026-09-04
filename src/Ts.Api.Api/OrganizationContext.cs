using System.Security.Claims;
using Ts.Api.Application.Common;

namespace Ts.Api.Api;

public sealed class HttpOrganizationContext(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    IHostEnvironment environment) : IOrganizationContext
{
    private const string OrganizationIdClaim = "organization_id";

    public bool IsAvailable => TryResolveOrganizationId(out _);

    public Guid OrganizationId => TryResolveOrganizationId(out var organizationId)
        ? organizationId
        : throw new InvalidOperationException("Nenhuma organização ativa foi resolvida para a requisição.");

    private bool TryResolveOrganizationId(out Guid organizationId)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var claimValue = user?.Identity?.IsAuthenticated == true
            ? user.FindFirstValue(OrganizationIdClaim)
            : null;

        if (Guid.TryParse(claimValue, out organizationId) && organizationId != Guid.Empty)
        {
            return true;
        }

        if (environment.IsDevelopment()
            && Guid.TryParse(configuration["Tenancy:DevelopmentOrganizationId"], out organizationId)
            && organizationId != Guid.Empty)
        {
            return true;
        }

        organizationId = Guid.Empty;
        return false;
    }
}

public sealed class OrganizationContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IOrganizationContext organizationContext)
    {
        if (httpContext.Request.Path.StartsWithSegments("/api") && !organizationContext.IsAvailable)
        {
            await Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Organização não identificada",
                detail: "A identidade autenticada não informa uma organização ativa.")
                .ExecuteAsync(httpContext);
            return;
        }

        await next(httpContext);
    }
}
