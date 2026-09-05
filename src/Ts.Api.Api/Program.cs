using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Ts.Api.Api;
using Ts.Api.Application.Catalog;
using Ts.Api.Application.Common;
using Ts.Api.Application.FrozenStock;
using Ts.Api.Application.Orders;
using Ts.Api.Application.Production;
using Ts.Api.Infrastructure;
using Ts.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IOrganizationContext, HttpOrganizationContext>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CreateOfferHandler>();
builder.Services.AddScoped<CreateProducibleItemHandler>();
builder.Services.AddScoped<CreateFrozenConfigurationHandler>();
builder.Services.AddScoped<RegisterFrozenProductionHandler>();
builder.Services.AddScoped<ConfirmOrderHandler>();
builder.Services.AddScoped<CreateOrderHandler>();
builder.Services.AddScoped<EditOrderHandler>();
builder.Services.AddScoped<GetOrderHandler>();
builder.Services.AddScoped<ConfigureDailyCapacityHandler>();
builder.Services.AddScoped<GetDailyCapacityHandler>();
builder.Services.AddScoped<PublishCompositionHandler>();
builder.Services.AddScoped<AddCustomerRestrictionHandler>();
builder.Services.AddScoped<CreatePlanAcquisitionHandler>();
builder.Services.AddScoped<GrantFinancialCreditHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<OrganizationContextMiddleware>();

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

app.MapApplicationEndpoints();

app.Run();

public partial class Program;
