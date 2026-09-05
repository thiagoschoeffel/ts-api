using Ts.Api.Application.Catalog;
using Ts.Api.Application.FrozenStock;
using Ts.Api.Application.Orders;
using Ts.Api.Application.Production;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Production;

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
        api.MapPost("/orders/{orderId:guid}/status-transitions", TransitionOrderStatusAsync)
            .WithName("TransitionOrderStatus");
        api.MapPost("/orders/{orderId:guid}/rescheduling", RescheduleOrderAsync)
            .WithName("RescheduleOrder");
        api.MapPost("/orders/{orderId:guid}/cancellation", CancelOrderAsync)
            .WithName("CancelOrder");
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
        api.MapPost("/production/items/{producibleItemId:guid}/compositions", PublishCompositionAsync)
            .WithName("PublishProducibleComposition");
        api.MapPost("/customers/{customerId:guid}/dietary-restrictions", AddCustomerRestrictionAsync)
            .WithName("AddCustomerDietaryRestriction");
        api.MapPost("/plans/acquisitions", CreatePlanAcquisitionAsync)
            .WithName("CreatePlanAcquisition");
        api.MapPost("/financial-credits", GrantFinancialCreditAsync)
            .WithName("GrantFinancialCredit");

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
                request.ExpectedVersion,
                request.PlanCredits?.Select(item => new PlanCreditRequest(item.OrderItemId, item.Quantity)).ToArray(),
                request.DiscountAmount,
                request.DiscountReason,
                request.DeliveryFee,
                request.FinancialCreditAmount),
            cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> TransitionOrderStatusAsync(
        Guid orderId,
        HttpContext httpContext,
        TransitionOrderStatusRequest request,
        TransitionOrderStatusHandler handler,
        CancellationToken cancellationToken)
    {
        var key = ReadIdempotencyKey(httpContext);
        if (key.Error is not null) return key.Error;
        return TypedResults.Ok(await handler.HandleAsync(new TransitionOrderStatusCommand(
            orderId, request.NewStatus, request.Reason, request.ActorId,
            request.ExpectedVersion, key.Value!), cancellationToken));
    }

    private static async Task<IResult> RescheduleOrderAsync(
        Guid orderId,
        HttpContext httpContext,
        RescheduleOrderRequest request,
        RescheduleOrderHandler handler,
        CancellationToken cancellationToken)
    {
        var key = ReadIdempotencyKey(httpContext);
        if (key.Error is not null) return key.Error;
        return TypedResults.Ok(await handler.HandleAsync(new RescheduleOrderCommand(
            orderId, request.NewOperationalDate, request.Reason, request.ActorId,
            request.ExpectedVersion, key.Value!), cancellationToken));
    }

    private static async Task<IResult> CancelOrderAsync(
        Guid orderId,
        HttpContext httpContext,
        CancelOrderRequest request,
        CancelOrderHandler handler,
        CancellationToken cancellationToken)
    {
        var key = ReadIdempotencyKey(httpContext);
        if (key.Error is not null) return key.Error;
        return TypedResults.Ok(await handler.HandleAsync(new CancelOrderCommand(
            orderId, request.Reason, request.ActorId, request.ExpectedVersion, key.Value!,
            request.CommercialDisposition, request.FrozenDisposition,
            request.FrozenReturnInspection is null ? null : new FrozenReturnInspection(
                request.FrozenReturnInspection.PackagingIntact,
                request.FrozenReturnInspection.TemperatureControlled,
                request.FrozenReturnInspection.TraceabilityIntact)), cancellationToken));
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

    private static async Task<IResult> PublishCompositionAsync(
        Guid producibleItemId,
        PublishCompositionRequest request,
        PublishCompositionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new PublishCompositionCommand(
            producibleItemId,
            request.Components.Select(item => new ProducibleComponentDefinition(
                item.Name, item.Quantity, item.MeasurementUnit, item.DietaryMarkers ?? [])).ToArray()), cancellationToken);
        return TypedResults.Created($"/api/production/items/{producibleItemId}/compositions/{result.Id}", result);
    }

    private static async Task<IResult> AddCustomerRestrictionAsync(
        Guid customerId,
        AddCustomerRestrictionRequest request,
        AddCustomerRestrictionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AddCustomerRestrictionCommand(customerId, request.Marker), cancellationToken);
        return TypedResults.Created($"/api/customers/{customerId}/dietary-restrictions/{result.Id}", result);
    }

    private static async Task<IResult> CreatePlanAcquisitionAsync(
        CreatePlanAcquisitionRequest request,
        CreatePlanAcquisitionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new CreatePlanAcquisitionCommand(
            request.CustomerId, request.EligibleOfferId, request.PlanName, request.Credits,
            request.BenefitAmountPerCredit, request.AcquiredOn), cancellationToken);
        return TypedResults.Created($"/api/plans/acquisitions/{result.Id}", result);
    }

    private static async Task<IResult> GrantFinancialCreditAsync(
        GrantFinancialCreditRequest request,
        GrantFinancialCreditHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GrantFinancialCreditCommand(
            request.CustomerId, request.Amount, request.Reason, request.ActorId), cancellationToken);
        return TypedResults.Created($"/api/financial-credits/{result.Id}", result);
    }

    private static OrderItemInput MapOrderItem(OrderItemRequest item) => new(
        item.OfferId,
        item.Quantity,
        item.UnitPrice,
        item.FrozenConfigurationId,
        item.ProducibleItemId);

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

public sealed record ConfirmOrderRequest(
    Guid ActorId,
    long ExpectedVersion,
    IReadOnlyCollection<PlanCreditRequestBody>? PlanCredits = null,
    decimal DiscountAmount = 0,
    string? DiscountReason = null,
    decimal DeliveryFee = 0,
    decimal FinancialCreditAmount = 0);

public sealed record TransitionOrderStatusRequest(
    OrderStatus NewStatus, string Reason, Guid ActorId, long ExpectedVersion);

public sealed record RescheduleOrderRequest(
    DateOnly NewOperationalDate, string Reason, Guid ActorId, long ExpectedVersion);

public sealed record CancelOrderRequest(
    string Reason,
    Guid ActorId,
    long ExpectedVersion,
    CommercialCancellationDisposition CommercialDisposition = CommercialCancellationDisposition.NotApplicable,
    FrozenCancellationDisposition FrozenDisposition = FrozenCancellationDisposition.NotApplicable,
    FrozenReturnInspectionRequest? FrozenReturnInspection = null);

public sealed record FrozenReturnInspectionRequest(
    bool PackagingIntact,
    bool TemperatureControlled,
    bool TraceabilityIntact);

public sealed record PlanCreditRequestBody(Guid OrderItemId, int Quantity);

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
    Guid? FrozenConfigurationId = null,
    Guid? ProducibleItemId = null);

public sealed record ConfigureDailyCapacityRequest(int TotalUnits, long ExpectedVersion);

public sealed record PublishCompositionRequest(IReadOnlyCollection<ProducibleComponentRequest> Components);
public sealed record ProducibleComponentRequest(
    string Name, decimal Quantity, string MeasurementUnit, IReadOnlyCollection<string>? DietaryMarkers = null);
public sealed record AddCustomerRestrictionRequest(string Marker);
public sealed record CreatePlanAcquisitionRequest(
    Guid CustomerId, Guid EligibleOfferId, string PlanName, int Credits,
    decimal BenefitAmountPerCredit, DateOnly AcquiredOn);
public sealed record GrantFinancialCreditRequest(Guid CustomerId, decimal Amount, string Reason, Guid ActorId);
