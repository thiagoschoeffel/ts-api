using Ts.Api.Application.Catalog;
using Ts.Api.Application.FrozenStock;
using Ts.Api.Application.Orders;
using Ts.Api.Application.Production;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.FrozenStock;

namespace Ts.Api.Api;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");

        api.MapPost("/catalog/offers", CreateOfferAsync)
            .WithName("CreateCatalogOffer");
        api.MapPost("/production/items", CreateProducibleItemAsync)
            .WithName("CreateProducibleItem");
        api.MapPost("/frozen-stock/configurations", CreateFrozenConfigurationAsync)
            .WithName("CreateFrozenConfiguration");
        api.MapPost("/frozen-stock/production-entries", RegisterFrozenProductionAsync)
            .WithName("RegisterFrozenProduction");
        api.MapPost("/orders/{orderId:guid}/confirmation", ConfirmOrderAsync)
            .WithName("ConfirmOrder");
        api.MapPost("/orders", CreateOrderAsync)
            .WithName("CreateOrder");
        api.MapPut("/orders/{orderId:guid}", EditOrderAsync)
            .WithName("EditOrder");
        api.MapGet("/orders/{orderId:guid}", GetOrderAsync)
            .WithName("GetOrder");
        api.MapPut("/daily-capacities/{operationalDate}", ConfigureDailyCapacityAsync)
            .WithName("ConfigureDailyCapacity");
        api.MapGet("/daily-capacities/{operationalDate}", GetDailyCapacityAsync)
            .WithName("GetDailyCapacity");

        return endpoints;
    }

    private static async Task<IResult> CreateOfferAsync(
        CreateOfferRequest request,
        CreateOfferHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateOfferCommand(request.Name, request.FulfillmentMode),
            cancellationToken);
        return TypedResults.Created($"/api/catalog/offers/{result.Id}", result);
    }

    private static async Task<IResult> CreateProducibleItemAsync(
        CreateProducibleItemRequest request,
        CreateProducibleItemHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateProducibleItemCommand(request.Name),
            cancellationToken);
        return TypedResults.Created($"/api/production/items/{result.Id}", result);
    }

    private static async Task<IResult> CreateFrozenConfigurationAsync(
        CreateFrozenConfigurationRequest request,
        CreateFrozenConfigurationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateFrozenConfigurationCommand(
                request.OfferId,
                request.ProducibleItemId,
                request.Presentation,
                request.QuantityPerUnit,
                request.MeasurementUnit,
                request.UnitPrice),
            cancellationToken);
        return TypedResults.Created($"/api/frozen-stock/configurations/{result.Id}", result);
    }

    private static async Task<IResult> RegisterFrozenProductionAsync(
        HttpContext httpContext,
        RegisterFrozenProductionRequest request,
        RegisterFrozenProductionHandler handler,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["O cabeçalho Idempotency-Key é obrigatório."],
            });
        }

        var result = await handler.HandleAsync(
            new RegisterFrozenProductionCommand(
                request.FrozenConfigurationId,
                request.ManufacturedOn,
                request.ProducedQuantity,
                request.ActorId,
                idempotencyKey),
            cancellationToken);
        return TypedResults.Created($"/api/frozen-stock/lots/{result.FrozenLotId}", result);
    }

    private static async Task<IResult> ConfirmOrderAsync(
        Guid orderId,
        HttpContext httpContext,
        ConfirmOrderRequest request,
        ConfirmOrderHandler handler,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["O cabeçalho Idempotency-Key é obrigatório."],
            });
        }

        var result = await handler.HandleAsync(
            new ConfirmOrderCommand(
                orderId,
                request.ActorId,
                idempotencyKey,
                request.ExpectedVersion),
            cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> CreateOrderAsync(
        HttpContext httpContext,
        CreateOrderRequest request,
        CreateOrderHandler handler,
        CancellationToken cancellationToken)
    {
        var idempotencyKeyResult = ReadIdempotencyKey(httpContext);
        if (idempotencyKeyResult.Error is not null)
        {
            return idempotencyKeyResult.Error;
        }

        var result = await handler.HandleAsync(
            new CreateOrderCommand(
                request.CustomerId,
                request.OperationalDate,
                request.Items.Select(MapOrderItem).ToArray(),
                idempotencyKeyResult.Value!),
            cancellationToken);
        return TypedResults.Created($"/api/orders/{result.Id}", result);
    }

    private static async Task<IResult> EditOrderAsync(
        Guid orderId,
        HttpContext httpContext,
        EditOrderRequest request,
        EditOrderHandler handler,
        CancellationToken cancellationToken)
    {
        var idempotencyKeyResult = ReadIdempotencyKey(httpContext);
        if (idempotencyKeyResult.Error is not null)
        {
            return idempotencyKeyResult.Error;
        }

        var result = await handler.HandleAsync(
            new EditOrderCommand(
                orderId,
                request.CustomerId,
                request.OperationalDate,
                request.Items.Select(MapOrderItem).ToArray(),
                request.ExpectedVersion,
                idempotencyKeyResult.Value!),
            cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> GetOrderAsync(
        Guid orderId,
        GetOrderHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(orderId, cancellationToken));

    private static async Task<IResult> ConfigureDailyCapacityAsync(
        DateOnly operationalDate,
        HttpContext httpContext,
        ConfigureDailyCapacityRequest request,
        ConfigureDailyCapacityHandler handler,
        CancellationToken cancellationToken)
    {
        var idempotencyKeyResult = ReadIdempotencyKey(httpContext);
        if (idempotencyKeyResult.Error is not null)
        {
            return idempotencyKeyResult.Error;
        }

        var result = await handler.HandleAsync(
            new ConfigureDailyCapacityCommand(
                operationalDate,
                request.TotalUnits,
                request.ExpectedVersion,
                idempotencyKeyResult.Value!),
            cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> GetDailyCapacityAsync(
        DateOnly operationalDate,
        GetDailyCapacityHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(operationalDate, cancellationToken));

    private static OrderItemInput MapOrderItem(OrderItemRequest item) => new(
        item.OfferId,
        item.Quantity,
        item.UnitPrice,
        item.FrozenConfigurationId);

    private static (string? Value, IResult? Error) ReadIdempotencyKey(HttpContext httpContext)
    {
        var value = httpContext.Request.Headers["Idempotency-Key"].ToString();
        return string.IsNullOrWhiteSpace(value)
            ? (null, TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["O cabeçalho Idempotency-Key é obrigatório."],
            }))
            : (value, null);
    }
}

public sealed record CreateOfferRequest(string Name, OfferFulfillmentMode FulfillmentMode);

public sealed record CreateProducibleItemRequest(string Name);

public sealed record CreateFrozenConfigurationRequest(
    Guid OfferId,
    Guid ProducibleItemId,
    string Presentation,
    decimal QuantityPerUnit,
    MeasurementUnit MeasurementUnit,
    decimal UnitPrice);

public sealed record RegisterFrozenProductionRequest(
    Guid FrozenConfigurationId,
    DateOnly ManufacturedOn,
    int ProducedQuantity,
    Guid ActorId);

public sealed record ConfirmOrderRequest(Guid ActorId, long ExpectedVersion);

public sealed record CreateOrderRequest(
    Guid CustomerId,
    DateOnly OperationalDate,
    IReadOnlyCollection<OrderItemRequest> Items);

public sealed record EditOrderRequest(
    Guid CustomerId,
    DateOnly OperationalDate,
    IReadOnlyCollection<OrderItemRequest> Items,
    long ExpectedVersion);

public sealed record OrderItemRequest(
    Guid OfferId,
    int Quantity,
    decimal? UnitPrice = null,
    Guid? FrozenConfigurationId = null);

public sealed record ConfigureDailyCapacityRequest(int TotalUnits, long ExpectedVersion);
