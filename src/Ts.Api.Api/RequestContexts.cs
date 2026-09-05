using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Organizations;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Api;

public sealed class HttpRequestContext : IOrganizationContext, ICurrentUserContext
{
    private Guid? organizationId;
    private Guid? userId;

    public bool IsAvailable => organizationId.HasValue && userId.HasValue;
    public Guid OrganizationId => organizationId
        ?? throw new InvalidOperationException("Nenhuma organização ativa foi resolvida para a requisição.");
    public Guid UserId => userId
        ?? throw new InvalidOperationException("Nenhum usuário foi resolvido para a requisição.");
    public string CorrelationId { get; private set; } = string.Empty;

    public void Set(Guid resolvedOrganizationId, Guid resolvedUserId, string correlationId)
    {
        organizationId = resolvedOrganizationId;
        userId = resolvedUserId;
        CorrelationId = correlationId;
    }
}

public sealed class OrganizationContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, HttpRequestContext requestContext,
        AppDbContext database)
    {
        if (!httpContext.Request.Path.StartsWithSegments("/api"))
        {
            await next(httpContext);
            return;
        }

        var correlationId = ReadCorrelationId(httpContext);
        httpContext.TraceIdentifier = correlationId;
        httpContext.Response.Headers["X-Correlation-Id"] = correlationId;

        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            await Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                title: "Autenticação necessária",
                detail: "Apresente um token de acesso válido.").ExecuteAsync(httpContext);
            return;
        }

        var subject = httpContext.User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            await Forbid(httpContext, "A identidade não contém o subject obrigatório.");
            return;
        }

        var platformUser = await database.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.ExternalSubject == subject && item.IsActive,
                httpContext.RequestAborted);
        if (platformUser is null)
        {
            await Forbid(httpContext, "O usuário autenticado não está cadastrado na plataforma.");
            return;
        }

        var requestedValue = httpContext.Request.Headers["X-Organization-Id"].FirstOrDefault()
            ?? httpContext.User.FindFirstValue("organization_id");
        if (!Guid.TryParse(requestedValue, out var requestedOrganizationId)
            || requestedOrganizationId == Guid.Empty)
        {
            await Forbid(httpContext, "A identidade não informa uma organização ativa válida.");
            return;
        }

        var membership = await database.OrganizationMemberships.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.OrganizationId == requestedOrganizationId
                && item.UserId == platformUser.Id && item.IsActive, httpContext.RequestAborted);
        var organizationIsActive = membership is not null && await database.Organizations
            .IgnoreQueryFilters().AnyAsync(item => item.Id == requestedOrganizationId && item.IsActive,
                httpContext.RequestAborted);
        if (!organizationIsActive)
        {
            await Forbid(httpContext, "O usuário não possui associação ativa com a organização solicitada.");
            return;
        }

        requestContext.Set(requestedOrganizationId, platformUser.Id, correlationId);
        httpContext.Items[AuthorizationPolicies.MembershipRoleItem] = membership!.Role;
        await next(httpContext);
    }

    private static string ReadCorrelationId(HttpContext context)
    {
        var requested = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(requested) && requested.Length <= 100
            ? requested
            : context.TraceIdentifier;
    }

    private static Task Forbid(HttpContext context, string detail) => Results.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Acesso à organização negado",
        detail: detail).ExecuteAsync(context);
}

public static class AuthorizationPolicies
{
    public const string Read = "organization:read";
    public const string Operate = "organization:operate";
    public const string Administer = "organization:administer";
    public const string MembershipRoleItem = "organization_membership_role";
}
