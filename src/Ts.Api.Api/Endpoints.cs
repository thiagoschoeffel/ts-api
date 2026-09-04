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
