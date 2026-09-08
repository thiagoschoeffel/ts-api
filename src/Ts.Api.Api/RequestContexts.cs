using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Organizations;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Api;

public sealed class HttpRequestContext : IOrganizationContext, ICurrentUserContext, IIdentityOrganizationScope
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

    public void SetUser(Guid resolvedUserId, string correlationId)
    {
        userId = resolvedUserId;
        CorrelationId = correlationId;
    }

    public void SetWebhookOrganization(Guid resolvedOrganizationId, string correlationId)
    {
        organizationId = resolvedOrganizationId;
        CorrelationId = correlationId;
    }

    public void SelectOrganizationForInvitation(Guid resolvedOrganizationId)
    {
        organizationId = resolvedOrganizationId;
    }
}

public sealed class PlatformActorContext : IPlatformActorContext
{
    private Guid? userId;
    private IReadOnlySet<string> profiles = new HashSet<string>();
    private IReadOnlySet<string> capabilities = new HashSet<string>();

    public bool IsAvailable => userId.HasValue && capabilities.Count > 0;
    public Guid UserId => userId
        ?? throw new InvalidOperationException("Nenhum ator da plataforma foi resolvido para a requisição.");
    public IReadOnlySet<string> Profiles => profiles;
    public IReadOnlySet<string> Capabilities => capabilities;

    public void Set(Guid resolvedUserId, IEnumerable<PlatformOperatorProfile> resolvedProfiles)
    {
        userId = resolvedUserId;
        profiles = resolvedProfiles.Select(item => item.ToString()).ToHashSet(StringComparer.Ordinal);
        capabilities = resolvedProfiles.SelectMany(PlatformCapabilities.ForProfile)
            .ToHashSet(StringComparer.Ordinal);
    }
}

public sealed class OrganizationContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, HttpRequestContext requestContext,
        AppDbContext database, PlatformActorContext platformActor, TimeProvider timeProvider)
    {
        if (!httpContext.Request.Path.StartsWithSegments("/api"))
        {
            await next(httpContext);
            return;
        }

        var correlationId = httpContext.TraceIdentifier;

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

        var endpoint = httpContext.GetEndpoint();
        var platformUser = await database.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.ExternalSubject == subject && item.IsActive,
                httpContext.RequestAborted);
        if (platformUser is null)
        {
            if (endpoint?.Metadata.GetMetadata<AllowUnregisteredIdentityMetadata>() is not null)
            {
                await next(httpContext);
                return;
            }
            await Forbid(httpContext, "O usuário autenticado não está cadastrado na plataforma.");
            return;
        }

        requestContext.SetUser(platformUser.Id, correlationId);
        if (endpoint is null)
        {
            await next(httpContext);
            return;
        }
        var contextMetadata = endpoint.Metadata.GetMetadata<ApiContextMetadata>();
        if (contextMetadata is null)
        {
            await Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Endpoint sem classificação de segurança",
                detail: "O endpoint da API não informa o contexto de autorização obrigatório.")
                .ExecuteAsync(httpContext);
            return;
        }
        if (contextMetadata.Kind is ApiContextKind.Identity or ApiContextKind.Platform)
        {
            var activeProfiles = await database.PlatformOperatorGrants.AsNoTracking()
                .Where(item => item.UserId == platformUser.Id && item.RevokedAt == null
                    && (item.ExpiresAt == null || item.ExpiresAt > timeProvider.GetUtcNow()))
                .Select(item => item.Profile)
                .ToArrayAsync(httpContext.RequestAborted);
            platformActor.Set(platformUser.Id, activeProfiles);
        }
        if (contextMetadata.Kind is ApiContextKind.Identity or ApiContextKind.Platform)
        {
            await next(httpContext);
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

        var entitlements = await database.OrganizationSaasSubscriptions.AsNoTracking()
            .Where(item => item.OrganizationId == requestedOrganizationId)
            .Join(database.SaasPlanEntitlements.AsNoTracking(), subscription => subscription.PlanVersionId,
                entitlement => entitlement.PlanVersionId, (_, entitlement) => entitlement.Code)
            .ToArrayAsync(httpContext.RequestAborted);
        var requiredEntitlement = RequiredEntitlement(httpContext.Request.Path);
        if (!entitlements.Contains(SaasEntitlements.BusinessAccess, StringComparer.Ordinal)
            || requiredEntitlement is not null
            && !entitlements.Contains(requiredEntitlement, StringComparer.Ordinal))
        {
            await Forbid(httpContext, "O plano SaaS da organização não habilita esta API de negócio.");
            return;
        }

        requestContext.Set(requestedOrganizationId, platformUser.Id, correlationId);
        httpContext.Items[AuthorizationPolicies.MembershipRoleItem] = membership!.Role;
        await next(httpContext);
    }

    private static Task Forbid(HttpContext context, string detail) => Results.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Acesso à organização negado",
        detail: detail).ExecuteAsync(context);

    internal static string? RequiredEntitlement(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (value.StartsWith("/api/attendance", StringComparison.OrdinalIgnoreCase)) return SaasEntitlements.Attendance;
        if (value.StartsWith("/api/catalog", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/menus", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/menu-plans", StringComparison.OrdinalIgnoreCase)) return SaasEntitlements.Catalog;
        if (value.StartsWith("/api/commerce", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/customers", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/plans", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/plan-credit", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/financial-credit", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/payments", StringComparison.OrdinalIgnoreCase)) return SaasEntitlements.Commerce;
        if (value.StartsWith("/api/logistics", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/delivery-", StringComparison.OrdinalIgnoreCase)) return SaasEntitlements.Logistics;
        if (value.StartsWith("/api/production", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/frozen-stock", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/operations", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/daily-capacities", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/orders", StringComparison.OrdinalIgnoreCase)) return SaasEntitlements.Operations;
        return null;
    }
}

public static class AuthorizationPolicies
{
    public const string Read = "organization:read";
    public const string Operate = "organization:operate";
    public const string Administer = "organization:administer";
    public const string MembershipRoleItem = "organization_membership_role";
    public const string PlatformRead = "platform:read";
    public const string PlatformOnboarding = "platform:onboarding";
    public const string PlatformAdminister = "platform:administer";
    public const string PlatformAuditRead = "platform:audit:read";
}

public static class PlatformCapabilities
{
    public const string OrganizationsRead = "platform.organizations.read";
    public const string OnboardingManage = "platform.onboarding.manage";
    public const string OrganizationsActivate = "platform.organizations.activate";
    public const string OrganizationsAdminister = "platform.organizations.administer";
    public const string AuditRead = "platform.audit.read";

    public static IEnumerable<string> ForProfile(PlatformOperatorProfile profile) => profile switch
    {
        PlatformOperatorProfile.PlatformAdministrator =>
            [OrganizationsRead, OnboardingManage, OrganizationsActivate, OrganizationsAdminister, AuditRead],
        PlatformOperatorProfile.PlatformOnboardingOperator =>
            [OrganizationsRead, OnboardingManage, OrganizationsActivate],
        PlatformOperatorProfile.PlatformSupportReader => [OrganizationsRead, AuditRead],
        _ => [],
    };
}
