using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Ts.Api.Api;
using Ts.Api.Application.Catalog;
using Ts.Api.Application.Common;
using Ts.Api.Application.FrozenStock;
using Ts.Api.Application.Orders;
using Ts.Api.Application.Operations;
using Ts.Api.Application.Organizations;
using Ts.Api.Application.Logistics;
using Ts.Api.Application.Menus;
using Ts.Api.Application.Production;
using Ts.Api.Application.Commerce;
using Ts.Api.Application.Attendance;
using Ts.Api.Infrastructure;
using Ts.Api.Infrastructure.Persistence;
using Ts.Api.Domain.Organizations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ApiMetrics>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("X-Correlation-Id")));
builder.Services.AddScoped<HttpRequestContext>();
builder.Services.AddScoped<IOrganizationContext>(provider => provider.GetRequiredService<HttpRequestContext>());
builder.Services.AddScoped<ICurrentUserContext>(provider => provider.GetRequiredService<HttpRequestContext>());
builder.Services.AddScoped<PlatformActorContext>();
builder.Services.AddScoped<IPlatformActorContext>(provider => provider.GetRequiredService<PlatformActorContext>());
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        var metadataAddress = builder.Configuration["Authentication:MetadataAddress"];
        if (!string.IsNullOrWhiteSpace(metadataAddress))
        {
            options.MetadataAddress = metadataAddress;
        }
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = options.Authority,
        };
        options.RequireHttpsMetadata = builder.Configuration.GetValue("Authentication:RequireHttpsMetadata", true);
        options.MapInboundClaims = false;
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.Read, policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new MembershipRoleRequirement(OrganizationRole.Owner,
            OrganizationRole.Administrator, OrganizationRole.Operator,
            OrganizationRole.DeliveryDriver)));
    options.AddPolicy(AuthorizationPolicies.Operate, policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new MembershipRoleRequirement(OrganizationRole.Owner,
            OrganizationRole.Administrator, OrganizationRole.Operator)));
    options.AddPolicy(AuthorizationPolicies.Administer, policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new MembershipRoleRequirement(OrganizationRole.Owner,
            OrganizationRole.Administrator)));
    options.AddPolicy(AuthorizationPolicies.PlatformRead, policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new PlatformCapabilityRequirement(PlatformCapabilities.OrganizationsRead)));
    options.AddPolicy(AuthorizationPolicies.PlatformOnboarding, policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new PlatformCapabilityRequirement(PlatformCapabilities.OnboardingManage)));
    options.AddPolicy(AuthorizationPolicies.PlatformAdminister, policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new PlatformCapabilityRequirement(PlatformCapabilities.OrganizationsAdminister)));
    options.AddPolicy(AuthorizationPolicies.PlatformAuditRead, policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new PlatformCapabilityRequirement(PlatformCapabilities.AuditRead)));
});
builder.Services.AddSingleton<IAuthorizationHandler, MembershipAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PlatformCapabilityAuthorizationHandler>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CreateOfferHandler>();
builder.Services.AddScoped<GetCatalogHandler>();
builder.Services.AddScoped<SaveOfferHandler>();
builder.Services.AddScoped<SaveComponentTypeHandler>();
builder.Services.AddScoped<SaveAddonHandler>();
builder.Services.AddScoped<GetProduciblesHandler>();
builder.Services.AddScoped<SaveProducibleHandler>();
builder.Services.AddScoped<MenuService>();
builder.Services.AddScoped<CreateProducibleItemHandler>();
builder.Services.AddScoped<CreateFrozenConfigurationHandler>();
builder.Services.AddScoped<RegisterFrozenProductionHandler>();
builder.Services.AddScoped<GetFrozenStockManagementHandler>();
builder.Services.AddScoped<GetFrozenLotHandler>();
builder.Services.AddScoped<UpdateFrozenConfigurationHandler>();
builder.Services.AddScoped<RegisterFrozenMovementHandler>();
builder.Services.AddScoped<CalculateFrozenExpirationHandler>();
builder.Services.AddScoped<ConfirmOrderHandler>();
builder.Services.AddScoped<TransitionOrderStatusHandler>();
builder.Services.AddScoped<RescheduleOrderHandler>();
builder.Services.AddScoped<CancelOrderHandler>();
builder.Services.AddScoped<CreateOrderHandler>();
builder.Services.AddScoped<EditOrderHandler>();
builder.Services.AddScoped<GetOrderHandler>();
builder.Services.AddScoped<ListOrdersHandler>();
builder.Services.AddScoped<GetOrderDetailsHandler>();
builder.Services.AddScoped<GetOrderAuthoringContextHandler>();
builder.Services.AddScoped<GetProductionSnapshotHandler>();
builder.Services.AddScoped<GetPackingQueueHandler>();
builder.Services.AddScoped<PackOrderHandler>();
builder.Services.AddScoped<RecordLabelPrintHandler>();
builder.Services.AddScoped<MembershipService>();
builder.Services.AddScoped<PlatformRegistryService>();
builder.Services.AddScoped<ConfigureDailyCapacityHandler>();
builder.Services.AddScoped<GetDailyCapacityHandler>();
builder.Services.AddScoped<PublishCompositionHandler>();
builder.Services.AddScoped<AddCustomerRestrictionHandler>();
builder.Services.AddScoped<CreatePlanAcquisitionHandler>();
builder.Services.AddScoped<GrantFinancialCreditHandler>();
builder.Services.AddScoped<CommerceService>();
builder.Services.AddScoped<LogisticsService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

if (PlatformOperatorBootstrap.IsRequested(args))
{
    Environment.ExitCode = await PlatformOperatorBootstrap.ExecuteAsync(args, app.Services);
    return;
}

app.UseMiddleware<RequestObservabilityMiddleware>();
app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<OrganizationContextMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await database.Database.MigrateAsync();
    app.MapOpenApi();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }))
    .WithName("Liveness");

app.MapGet("/health/ready", async (AppDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "healthy" })
        : Results.Problem("O PostgreSQL não está disponível.", statusCode: StatusCodes.Status503ServiceUnavailable))
    .WithName("Readiness");

app.MapGet("/metrics", (ApiMetrics metrics) => Results.Text(metrics.Snapshot(), "text/plain; version=0.0.4"))
    .WithName("Metrics");

app.MapPost("/api/telemetry/client-errors", (ClientErrorReport report, HttpRequestContext context,
    ILogger<Program> logger) =>
{
    logger.LogError("Erro do frontend {ErrorName} em {Source}: {Message}",
        ClientErrorReport.Sanitize(report.Name, 120), ClientErrorReport.Sanitize(report.Source, 200),
        ClientErrorReport.Sanitize(report.Message, 1000));
    return Results.Accepted();
})
    .WithApiContext(ApiContextKind.Business)
    .RequireAuthorization(AuthorizationPolicies.Read)
    .WithName("ReportClientError");

app.MapPost("/api/identity/telemetry/client-errors", (ClientErrorReport report,
    ILogger<Program> logger) =>
{
    logger.LogError("Erro do frontend {ErrorName} em {Source}: {Message}",
        ClientErrorReport.Sanitize(report.Name, 120), ClientErrorReport.Sanitize(report.Source, 200),
        ClientErrorReport.Sanitize(report.Message, 1000));
    return Results.Accepted();
})
    .WithApiContext(ApiContextKind.Identity)
    .RequireAuthorization()
    .WithName("ReportIdentityClientError");

app.MapApplicationEndpoints();
app.MapWhatsAppWebhooks();

app.Run();

public partial class Program;

public sealed record ClientErrorReport(string Name, string Message, string Source)
{
    public static string Sanitize(string? value, int maximumLength)
    {
        var sanitized = string.IsNullOrWhiteSpace(value) ? "unknown" : value.ReplaceLineEndings(" ");
        return sanitized.Length <= maximumLength ? sanitized : sanitized[..maximumLength];
    }
}
