using Ts.Api.Application.Catalog;
using Ts.Api.Application.Common;
using Ts.Api.Application.FrozenStock;
using Ts.Api.Application.Orders;
using Ts.Api.Application.Operations;
using Ts.Api.Application.Organizations;
using Ts.Api.Application.Menus;
using Ts.Api.Application.Production;
using Ts.Api.Application.Commerce;
using Ts.Api.Application.Logistics;
using Ts.Api.Application.Attendance;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Menus;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Operations;
using Ts.Api.Domain.Logistics;
using Ts.Api.Domain.Organizations;
using Ts.Api.Domain.Production;
using Ts.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ts.Api.Api;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/session", GetSessionAsync)
            .WithApiContext(ApiContextKind.Identity)
            .RequireAuthorization()
            .WithName("GetSession");

        endpoints.MapGet("/api/identity/session", GetSessionAsync)
            .WithApiContext(ApiContextKind.Identity)
            .RequireAuthorization()
            .WithName("GetIdentitySession");
        endpoints.MapPost("/api/identity/invitations/accept", AcceptInvitationAsync)
            .WithApiContext(ApiContextKind.Identity)
            .AllowUnregisteredIdentity()
            .RequireAuthorization()
            .WithName("AcceptOrganizationInvitation");

        var platform = endpoints.MapGroup("/api/platform")
            .WithApiContext(ApiContextKind.Platform);
        platform.MapGet("/access", (IPlatformActorContext actor) =>
                TypedResults.Ok(new PlatformSession(actor.Profiles.Order().ToArray(),
                    actor.Capabilities.Order().ToArray())))
            .RequireAuthorization(AuthorizationPolicies.PlatformRead)
            .WithName("GetPlatformAccess");
        platform.MapGet("/organizations", (string? search, OrganizationLifecycleStatus? status,
                int page, int pageSize, PlatformRegistryService service, CancellationToken token) =>
                service.ListOrganizationsAsync(search, status, page == 0 ? 1 : page,
                    pageSize == 0 ? 20 : pageSize, token))
            .RequireAuthorization(AuthorizationPolicies.PlatformRead)
            .WithName("GetPlatformOrganizations");
        platform.MapGet("/organizations/{id:guid}", (Guid id, PlatformRegistryService service,
                CancellationToken token) => service.GetOrganizationAsync(id, token))
            .RequireAuthorization(AuthorizationPolicies.PlatformRead)
            .WithName("GetPlatformOrganization");
        platform.MapGet("/audit-events", (string? action, Guid? targetId, int page, int pageSize,
                PlatformRegistryService service, CancellationToken token) =>
                service.ListAuditAsync(action, targetId, page == 0 ? 1 : page,
                    pageSize == 0 ? 20 : pageSize, token))
            .RequireAuthorization(AuthorizationPolicies.PlatformAuditRead)
            .WithName("GetPlatformAuditEvents");

        var api = endpoints.MapGroup("/api")
            .WithApiContext(ApiContextKind.Business)
            .RequireAuthorization(AuthorizationPolicies.Read);
        api.MapGet("/memberships", (MembershipService service, CancellationToken token) => service.GetAsync(token))
            .RequireAuthorization(AuthorizationPolicies.Administer).WithName("GetMemberships");
        api.MapGet("/membership-invitations", (MembershipService service, CancellationToken token) => service.GetInvitationsAsync(token))
            .RequireAuthorization(AuthorizationPolicies.Administer).WithName("GetMembershipInvitations");
        api.MapPost("/membership-invitations", CreateInvitationAsync)
            .RequireAuthorization(AuthorizationPolicies.Administer).WithName("CreateMembershipInvitation");
        api.MapPut("/memberships/{userId:guid}", UpdateMembershipAsync)
            .RequireAuthorization(AuthorizationPolicies.Administer).WithName("UpdateMembership");

        api.MapPost("/catalog/offers", CreateOfferAsync)
            .RequireAuthorization(AuthorizationPolicies.Administer)
            .WithName("CreateCatalogOffer");
        api.MapGet("/catalog", GetCatalogAsync).WithName("GetCatalog");
        api.MapPut("/catalog/offers/{offerId:guid}", SaveOfferAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("UpdateCatalogOffer");
        api.MapPost("/catalog/offers/configured", CreateConfiguredOfferAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("CreateConfiguredCatalogOffer");
        api.MapPost("/catalog/component-types", CreateComponentTypeAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("CreateComponentType");
        api.MapPut("/catalog/component-types/{id:guid}", UpdateComponentTypeAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("UpdateComponentType");
        api.MapPost("/catalog/addons", CreateAddonAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("CreateCatalogAddon");
        api.MapPut("/catalog/addons/{id:guid}", UpdateAddonAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("UpdateCatalogAddon");
        api.MapPost("/production/items", CreateProducibleItemAsync)
            .RequireAuthorization(AuthorizationPolicies.Administer)
            .WithName("CreateProducibleItem");
        api.MapGet("/production/items", GetProduciblesAsync).WithName("GetProducibleItems");
        api.MapPost("/production/items/configured", CreateConfiguredProducibleAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("CreateConfiguredProducibleItem");
        api.MapPut("/production/items/{id:guid}", UpdateProducibleAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("UpdateProducibleItem");
        api.MapGet("/menus", ListMenusAsync).WithName("ListDailyMenus");
        api.MapGet("/menus/{date}", GetMenuAsync).WithName("GetDailyMenu");
        api.MapPut("/menus/{date}", SaveMenuAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("SaveDailyMenu");
        api.MapPost("/menus/{date}/publication", PublishMenuAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("PublishDailyMenu");
        api.MapPost("/menus/import", ImportMenusAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("ImportDailyMenus");
        api.MapPut("/menu-plans/{weekStart}", SaveWeeklyPlanAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("SaveWeeklyMenuPlan");
        api.MapGet("/menu-plans/{weekStart}", GetWeeklyPlanAsync).WithName("GetWeeklyMenuPlan");
        api.MapPost("/frozen-stock/configurations", CreateFrozenConfigurationAsync)
            .RequireAuthorization(AuthorizationPolicies.Administer)
            .WithName("CreateFrozenConfiguration");
        api.MapPut("/frozen-stock/configurations/{configurationId:guid}", UpdateFrozenConfigurationAsync)
            .RequireAuthorization(AuthorizationPolicies.Administer)
            .WithName("UpdateFrozenConfiguration");
        api.MapGet("/frozen-stock", GetFrozenStockManagementAsync)
            .WithName("GetFrozenStockManagement");
        api.MapGet("/frozen-stock/expiration", CalculateFrozenExpiration)
            .WithName("CalculateFrozenExpiration");
        api.MapGet("/frozen-stock/lots/{lotId:guid}", GetFrozenLotAsync)
            .WithName("GetFrozenLot");
        api.MapPost("/frozen-stock/production-entries", RegisterFrozenProductionAsync)
            .RequireAuthorization(AuthorizationPolicies.Operate)
            .WithName("RegisterFrozenProduction");
        api.MapPost("/frozen-stock/lots/{lotId:guid}/movements", RegisterFrozenMovementAsync)
            .RequireAuthorization(AuthorizationPolicies.Operate)
            .WithName("RegisterFrozenMovement");
        api.MapPost("/orders/{orderId:guid}/confirmation", ConfirmOrderAsync)
            .RequireAuthorization(AuthorizationPolicies.Operate)
            .WithName("ConfirmOrder");
        api.MapPost("/orders/{orderId:guid}/status-transitions", TransitionOrderStatusAsync)
            .RequireAuthorization(AuthorizationPolicies.Operate)
            .WithName("TransitionOrderStatus");
        api.MapPost("/orders/{orderId:guid}/rescheduling", RescheduleOrderAsync)
            .RequireAuthorization(AuthorizationPolicies.Operate)
            .WithName("RescheduleOrder");
        api.MapPost("/orders/{orderId:guid}/cancellation", CancelOrderAsync)
            .RequireAuthorization(AuthorizationPolicies.Operate)
            .WithName("CancelOrder");
        api.MapPost("/orders", CreateOrderAsync)
            .RequireAuthorization(AuthorizationPolicies.Operate)
            .WithName("CreateOrder");
        api.MapGet("/orders", ListOrdersAsync)
            .WithName("ListOrders");
        api.MapGet("/orders/authoring-context", GetOrderAuthoringContextAsync)
            .WithName("GetOrderAuthoringContext");
        api.MapPut("/orders/{orderId:guid}", EditOrderAsync)
            .RequireAuthorization(AuthorizationPolicies.Operate)
            .WithName("EditOrder");
        api.MapGet("/orders/{orderId:guid}", GetOrderDetailsAsync)
            .WithName("GetOrder");
        api.MapGet("/operations/production", GetProductionSnapshotAsync)
            .WithName("GetProductionSnapshot");
        api.MapGet("/operations/packing", GetPackingQueueAsync)
            .WithName("GetPackingQueue");
        api.MapPost("/operations/packing/{orderId:guid}", PackOrderAsync)
            .RequireAuthorization(AuthorizationPolicies.Operate)
            .WithName("PackOrder");
        api.MapPost("/operations/packing/{orderId:guid}/print-attempts", RecordLabelPrintAsync)
            .RequireAuthorization(AuthorizationPolicies.Operate)
            .WithName("RecordLabelPrint");
        api.MapPut("/daily-capacities/{operationalDate}", ConfigureDailyCapacityAsync)
            .RequireAuthorization(AuthorizationPolicies.Administer)
            .WithName("ConfigureDailyCapacity");
        api.MapGet("/daily-capacities/{operationalDate}", GetDailyCapacityAsync)
            .WithName("GetDailyCapacity");
        api.MapPost("/production/items/{producibleItemId:guid}/compositions", PublishCompositionAsync)
            .RequireAuthorization(AuthorizationPolicies.Administer)
            .WithName("PublishProducibleComposition");
        api.MapPost("/customers/{customerId:guid}/dietary-restrictions", AddCustomerRestrictionAsync)
            .RequireAuthorization(AuthorizationPolicies.Administer)
            .WithName("AddCustomerDietaryRestriction");
        api.MapPost("/plans/acquisitions", CreatePlanAcquisitionAsync)
            .RequireAuthorization(AuthorizationPolicies.Administer)
            .WithName("CreatePlanAcquisition");
        api.MapPost("/financial-credits", GrantFinancialCreditAsync)
            .RequireAuthorization(AuthorizationPolicies.Administer)
            .WithName("GrantFinancialCredit");
        api.MapGet("/commerce", GetCommerceAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("GetCommerce");
        api.MapPost("/customers", CreateCustomerAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("CreateCustomer");
        api.MapPut("/customers/{id:guid}", UpdateCustomerAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("UpdateCustomer");
        api.MapPost("/plans", CreatePlanAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("CreatePlan");
        api.MapPut("/plans/{id:guid}", UpdatePlanAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("UpdatePlan");
        api.MapPost("/plans/acquisitions/authoritative", CreateAuthoritativeAcquisitionAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("CreateAuthoritativePlanAcquisition");
        api.MapPost("/plan-credit-adjustments", AdjustPlanCreditAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("AdjustPlanCredit");
        api.MapPost("/financial-credit-adjustments", AdjustFinancialCreditAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("AdjustFinancialCredit");
        api.MapPost("/payments", RegisterPaymentAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("RegisterPayment");
        api.MapGet("/logistics", GetLogisticsAsync).WithName("GetLogistics");
        api.MapPost("/delivery-drivers", CreateDeliveryDriverAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("CreateDeliveryDriver");
        api.MapPut("/delivery-drivers/{id:guid}", UpdateDeliveryDriverAsync).RequireAuthorization(AuthorizationPolicies.Administer).WithName("UpdateDeliveryDriver");
        api.MapPost("/delivery-routes", CreateDeliveryRouteAsync).RequireAuthorization(AuthorizationPolicies.Operate).WithName("CreateDeliveryRoute");
        api.MapPut("/delivery-routes/{id:guid}", UpdateDeliveryRouteAsync).RequireAuthorization(AuthorizationPolicies.Operate).WithName("UpdateDeliveryRoute");
        api.MapPost("/delivery-routes/{id:guid}/start", StartDeliveryRouteAsync).RequireAuthorization(AuthorizationPolicies.Operate).WithName("StartDeliveryRoute");
        api.MapPost("/delivery-routes/{id:guid}/cancellation", CancelDeliveryRouteAsync).RequireAuthorization(AuthorizationPolicies.Operate).WithName("CancelDeliveryRoute");
        api.MapPost("/delivery-routes/{routeId:guid}/stops/{stopId:guid}/attempts", RecordDeliveryAttemptAsync).WithName("RecordDeliveryAttempt");
        api.MapPost("/orders/{orderId:guid}/delivery-rescheduling", RescheduleDeliveryAsync).RequireAuthorization(AuthorizationPolicies.Operate).WithName("RescheduleDelivery");
        api.MapGet("/attendance", (AttendanceService service, CancellationToken token) => service.GetAsync(token)).RequireAuthorization(AuthorizationPolicies.Operate).WithName("GetAttendance");
        api.MapPut("/attendance/conversations/{id:guid}/mode", (Guid id, AttendanceModeRequest request, AttendanceService service, CancellationToken token) => service.ChangeModeAsync(id, request.Mode, request.ExpectedVersion, token)).RequireAuthorization(AuthorizationPolicies.Operate).WithName("ChangeAttendanceMode");
        api.MapPost("/attendance/conversations/{id:guid}/messages", SendAttendanceMessageAsync).RequireAuthorization(AuthorizationPolicies.Operate).WithName("SendAttendanceMessage");
        api.MapPost("/attendance/conversations/{conversationId:guid}/messages/{messageId:guid}/retry", (Guid conversationId, Guid messageId, AttendanceService service, CancellationToken token) => service.RetryAsync(conversationId, messageId, token)).RequireAuthorization(AuthorizationPolicies.Operate).WithName("RetryAttendanceMessage");

        return endpoints;
    }

    private static async Task<IResult> GetLogisticsAsync(LogisticsService service, CancellationToken token) => TypedResults.Ok(await service.GetAsync(token));
    private static async Task<IResult> CreateInvitationAsync(InvitationRequest request, MembershipService service, HttpRequestContext context, CancellationToken token) =>
        TypedResults.Created("/api/membership-invitations", await service.InviteAsync(request.Email, request.Role, context.CorrelationId, token));
    private static async Task<IResult> UpdateMembershipAsync(Guid userId, MembershipRequest request, MembershipService service, HttpRequestContext context, CancellationToken token) =>
        TypedResults.Ok(await service.UpdateAsync(userId, request.Role, request.IsActive, request.ExpectedVersion, context.CorrelationId, token));
    private static async Task<IResult> AcceptInvitationAsync(InvitationAcceptanceRequest request, HttpContext httpContext, MembershipService service, CancellationToken token) =>
        TypedResults.Ok(await service.AcceptAsync(request.Token,
            httpContext.User.FindFirstValue("sub") ?? string.Empty,
            httpContext.User.FindFirstValue("email") ?? string.Empty,
            httpContext.User.FindFirstValue("name") ?? httpContext.User.Identity?.Name ?? "Usuário",
            httpContext.TraceIdentifier, token));
    private static async Task<IResult> SendAttendanceMessageAsync(Guid id, AttendanceMessageRequest request, HttpContext context, AttendanceService service, CancellationToken token)
    { var key = ReadIdempotencyKey(context); return key.Error ?? TypedResults.Ok(await service.SendAsync(id, request.Content, key.Value!, token)); }
    private static async Task<IResult> CreateDeliveryDriverAsync(DeliveryDriverRequest request, LogisticsService service, CancellationToken token) =>
        TypedResults.Created("/api/delivery-drivers", await service.SaveDriverAsync(null, new(request.Identification, request.Name, request.Phone, request.IsActive, request.IsAvailable), token));
    private static async Task<IResult> UpdateDeliveryDriverAsync(Guid id, DeliveryDriverRequest request, LogisticsService service, CancellationToken token) =>
        TypedResults.Ok(await service.SaveDriverAsync(id, new(request.Identification, request.Name, request.Phone, request.IsActive, request.IsAvailable, request.ExpectedVersion), token));
    private static async Task<IResult> CreateDeliveryRouteAsync(DeliveryRouteRequest request, LogisticsService service, ICurrentUserContext user, CancellationToken token) =>
        TypedResults.Created("/api/delivery-routes", await service.CreateRouteAsync(request.Date, request.DeliveryWindow, request.DriverId, request.OrderIds, user.UserId, token));
    private static async Task<IResult> UpdateDeliveryRouteAsync(Guid id, DeliveryRouteRequest request, LogisticsService service, CancellationToken token) =>
        TypedResults.Ok(await service.UpdateRouteAsync(id, request.DriverId, request.OrderIds, request.ExpectedVersion ?? 0, token));
    private static async Task<IResult> StartDeliveryRouteAsync(Guid id, DeliveryRouteVersionRequest request, HttpContext context, LogisticsService service, ICurrentUserContext user, CancellationToken token)
    { var key = ReadIdempotencyKey(context); return key.Error ?? TypedResults.Ok(await service.StartRouteAsync(id, request.ExpectedVersion, user.UserId, key.Value!, token)); }
    private static async Task<IResult> CancelDeliveryRouteAsync(Guid id, DeliveryRouteVersionRequest request, LogisticsService service, ICurrentUserContext user, CancellationToken token) =>
        TypedResults.Ok(await service.CancelRouteAsync(id, request.ExpectedVersion, user.UserId, token));
    private static async Task<IResult> RecordDeliveryAttemptAsync(Guid routeId, Guid stopId, DeliveryAttemptRequest request, HttpContext context, LogisticsService service, ICurrentUserContext user, CancellationToken token)
    { var key = ReadIdempotencyKey(context); return key.Error ?? TypedResults.Ok(await service.RecordAttemptAsync(routeId, stopId, request.Result, request.FailureReason, request.Note, request.ReceivedBy, user.UserId, key.Value!, token)); }
    private static async Task<IResult> RescheduleDeliveryAsync(Guid orderId, DeliveryRescheduleRequest request, HttpContext context, LogisticsService service, ICurrentUserContext user, CancellationToken token)
    { var key = ReadIdempotencyKey(context); return key.Error ?? TypedResults.Ok(await service.RescheduleAsync(orderId, request.NewDate, request.NewWindow, request.Reason, user.UserId, key.Value!, token)); }

    private static async Task<IResult> GetSessionAsync(
        ICurrentUserContext currentUser,
        HttpContext httpContext,
        AppDbContext database,
        CancellationToken cancellationToken)
    {
        var user = await database.Users.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == currentUser.UserId, cancellationToken);
        var memberships = await database.OrganizationMemberships.IgnoreQueryFilters()
            .Where(item => item.UserId == currentUser.UserId && item.IsActive)
            .Join(database.Organizations.IgnoreQueryFilters().Where(item => item.IsActive),
                membership => membership.OrganizationId,
                organization => organization.Id,
                (membership, organization) => new { Membership = membership, Organization = organization })
            .OrderBy(item => item.Organization.Name)
            .Select(item => new
            {
                item.Organization.Id,
                item.Organization.Name,
                item.Organization.Slug,
                item.Membership.Role
            })
            .ToArrayAsync(cancellationToken);

        var requestedHeader = httpContext.Request.Headers["X-Organization-Id"].FirstOrDefault();
        var requestedValue = requestedHeader ?? httpContext.User.FindFirstValue("organization_id");
        var requestedId = Guid.TryParse(requestedValue, out var parsed) ? parsed : (Guid?)null;
        if (requestedHeader is not null && (!requestedId.HasValue || memberships.All(item => item.Id != requestedId)))
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                title: "Acesso à organização negado",
                detail: "O usuário não possui associação ativa com a organização solicitada.");
        var activeOrganizationId = memberships.Any(item => item.Id == requestedId)
            ? requestedId
            : memberships.Select(item => (Guid?)item.Id).FirstOrDefault();
        var organizations = memberships.Select(item => new SessionOrganization(
            item.Id, item.Name, item.Slug, item.Role, item.Id == activeOrganizationId)).ToArray();
        var actor = httpContext.RequestServices.GetRequiredService<IPlatformActorContext>();
        return TypedResults.Ok(new SessionResponse(user.Id, user.DisplayName,
            activeOrganizationId, organizations, new PlatformSession(
                actor.Profiles.Order().ToArray(), actor.Capabilities.Order().ToArray())));
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

    private static async Task<IResult> GetCatalogAsync(GetCatalogHandler handler, CancellationToken token) =>
        TypedResults.Ok(await handler.HandleAsync(token));
    private static async Task<IResult> CreateConfiguredOfferAsync(CatalogOfferRequest request, SaveOfferHandler handler, CancellationToken token)
    {
        var result = await handler.HandleAsync(null, MapOffer(request), token);
        return TypedResults.Created($"/api/catalog/offers/{result.Id}", result);
    }
    private static async Task<IResult> SaveOfferAsync(Guid offerId, CatalogOfferRequest request, SaveOfferHandler handler, CancellationToken token) =>
        TypedResults.Ok(await handler.HandleAsync(offerId, MapOffer(request), token));
    private static CatalogOfferInput MapOffer(CatalogOfferRequest request) => new(request.Name, request.Description, request.BasePrice,
        request.FulfillmentMode, request.RequiresMenuChoice, request.IsActive, new(
            request.Components.Select(x => new OfferComponentInput(x.ComponentTypeId, x.Quantity)).ToArray(),
            request.ChoiceGroups.Select(x => new OfferChoiceGroupInput(x.Name, x.MinimumSelections, x.MaximumSelections,
                x.Options.Select(o => new OfferChoiceOptionInput(o.ComponentTypeId, o.Surcharge)).ToArray())).ToArray(), request.AllowedAddonIds));
    private static async Task<IResult> CreateComponentTypeAsync(ComponentTypeRequest request, SaveComponentTypeHandler handler, CancellationToken token) =>
        TypedResults.Created("/api/catalog/component-types", await handler.HandleAsync(null, new(request.Name, request.Description, request.IsActive), token));
    private static async Task<IResult> UpdateComponentTypeAsync(Guid id, ComponentTypeRequest request, SaveComponentTypeHandler handler, CancellationToken token) =>
        TypedResults.Ok(await handler.HandleAsync(id, new(request.Name, request.Description, request.IsActive), token));
    private static async Task<IResult> CreateAddonAsync(CatalogAddonRequest request, SaveAddonHandler handler, CancellationToken token) =>
        TypedResults.Created("/api/catalog/addons", await handler.HandleAsync(null, new(request.Name, request.Price, request.ProducibleItemId, request.OperationalQuantity, request.MeasurementUnit, request.IsActive), token));
    private static async Task<IResult> UpdateAddonAsync(Guid id, CatalogAddonRequest request, SaveAddonHandler handler, CancellationToken token) =>
        TypedResults.Ok(await handler.HandleAsync(id, new(request.Name, request.Price, request.ProducibleItemId, request.OperationalQuantity, request.MeasurementUnit, request.IsActive), token));

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

    private static async Task<IResult> GetProduciblesAsync(GetProduciblesHandler handler, CancellationToken token) =>
        TypedResults.Ok(await handler.HandleAsync(token));
    private static async Task<IResult> CreateConfiguredProducibleAsync(ProducibleRequest request, SaveProducibleHandler handler, CancellationToken token) =>
        TypedResults.Created("/api/production/items", await handler.HandleAsync(null, new(request.Name, request.Description, request.Category, request.MeasurementUnit, request.IsActive), token));
    private static async Task<IResult> UpdateProducibleAsync(Guid id, ProducibleRequest request, SaveProducibleHandler handler, CancellationToken token) =>
        TypedResults.Ok(await handler.HandleAsync(id, new(request.Name, request.Description, request.Category, request.MeasurementUnit, request.IsActive), token));
    private static async Task<IResult> ListMenusAsync(DateOnly? from, DateOnly? to, MenuService service, CancellationToken token) => TypedResults.Ok(await service.ListAsync(from, to, token));
    private static async Task<IResult> GetMenuAsync(DateOnly date, MenuService service, CancellationToken token) => TypedResults.Ok(await service.GetAsync(date, token));
    private static async Task<IResult> SaveMenuAsync(DateOnly date, DailyMenuRequest request, MenuService service, CancellationToken token) =>
        TypedResults.Ok(await service.SaveAsync(MapMenu(date, request), token));
    private static async Task<IResult> PublishMenuAsync(DateOnly date, PublishMenuRequest request, MenuService service, CancellationToken token) =>
        TypedResults.Ok(await service.PublishAsync(date, request.ExpectedVersion, token));
    private static async Task<IResult> ImportMenusAsync(IReadOnlyCollection<DailyMenuImportRequest> requests, MenuService service, CancellationToken token) =>
        TypedResults.Ok(await service.ImportAsync(requests.Select(x => MapMenu(x.Date, x.Menu)).ToArray(), token));
    private static async Task<IResult> SaveWeeklyPlanAsync(DateOnly weekStart, WeeklyPlanRequest request, MenuService service, CancellationToken token) =>
        TypedResults.Ok(await service.SavePlanAsync(new(weekStart, request.Days.Select(x => MapMenu(x.Date, x.Menu)).ToArray()), request.DeriveDrafts, token));
    private static async Task<IResult> GetWeeklyPlanAsync(DateOnly weekStart, MenuService service, CancellationToken token) =>
        TypedResults.Ok(await service.GetPlanAsync(weekStart, token));
    private static DailyMenuInput MapMenu(DateOnly date, DailyMenuRequest request) => new(date,
        request.Options.Select(x => new MenuOptionInput(x.Category, x.ProducibleItemId, x.Availability)).ToArray(),
        request.Offers.Select(x => new MenuOfferInput(x.OfferId, x.EffectivePrice, x.Availability, x.DisplayOrder)).ToArray(), request.ExpectedVersion);

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
        ICurrentUserContext currentUser,
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
                currentUser.UserId,
                idempotencyKey),
            cancellationToken);
        return TypedResults.Created($"/api/frozen-stock/lots/{result.FrozenLotId}", result);
    }

    private static async Task<IResult> GetFrozenStockManagementAsync(
        GetFrozenStockManagementHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static IResult CalculateFrozenExpiration(
        DateOnly manufacturedOn,
        CalculateFrozenExpirationHandler handler) => TypedResults.Ok(handler.Handle(manufacturedOn));

    private static async Task<IResult> GetFrozenLotAsync(
        Guid lotId,
        GetFrozenLotHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(lotId, cancellationToken));

    private static async Task<IResult> UpdateFrozenConfigurationAsync(
        Guid configurationId,
        UpdateFrozenConfigurationRequest request,
        UpdateFrozenConfigurationHandler handler,
        CancellationToken cancellationToken)
    {
        _ = await handler.HandleAsync(
            new UpdateFrozenConfigurationCommand(configurationId, request.UnitPrice, request.IsActive),
            cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> RegisterFrozenMovementAsync(
        Guid lotId,
        HttpContext httpContext,
        RegisterFrozenMovementRequest request,
        RegisterFrozenMovementHandler handler,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var key = ReadIdempotencyKey(httpContext);
        if (key.Error is not null) return key.Error;
        var movementId = await handler.HandleAsync(new RegisterFrozenMovementCommand(
            lotId,
            request.Type,
            request.Quantity,
            request.Reason,
            currentUser.UserId,
            key.Value!), cancellationToken);
        return TypedResults.Ok(new RegisterFrozenMovementResponse(movementId));
    }

    private static async Task<IResult> ConfirmOrderAsync(
        Guid orderId,
        HttpContext httpContext,
        ConfirmOrderRequest request,
        ConfirmOrderHandler handler,
        ICurrentUserContext currentUser,
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
                currentUser.UserId,
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
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var key = ReadIdempotencyKey(httpContext);
        if (key.Error is not null) return key.Error;
        return TypedResults.Ok(await handler.HandleAsync(new TransitionOrderStatusCommand(
            orderId, request.NewStatus, request.Reason, currentUser.UserId,
            request.ExpectedVersion, key.Value!), cancellationToken));
    }

    private static async Task<IResult> RescheduleOrderAsync(
        Guid orderId,
        HttpContext httpContext,
        RescheduleOrderRequest request,
        RescheduleOrderHandler handler,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var key = ReadIdempotencyKey(httpContext);
        if (key.Error is not null) return key.Error;
        return TypedResults.Ok(await handler.HandleAsync(new RescheduleOrderCommand(
            orderId, request.NewOperationalDate, request.Reason, currentUser.UserId,
            request.ExpectedVersion, key.Value!), cancellationToken));
    }

    private static async Task<IResult> CancelOrderAsync(
        Guid orderId,
        HttpContext httpContext,
        CancelOrderRequest request,
        CancelOrderHandler handler,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var key = ReadIdempotencyKey(httpContext);
        if (key.Error is not null) return key.Error;
        return TypedResults.Ok(await handler.HandleAsync(new CancelOrderCommand(
            orderId, request.Reason, currentUser.UserId, request.ExpectedVersion, key.Value!,
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
                idempotencyKeyResult.Value!,
                request.CustomerName,
                request.Fulfillment is null ? null : new OrderFulfillmentInput(
                    request.Fulfillment.Type, request.Fulfillment.Phone,
                    request.Fulfillment.AddressId, request.Fulfillment.DeliveryWindow)),
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
                idempotencyKeyResult.Value!,
                request.CustomerName,
                request.Fulfillment is null ? null : new OrderFulfillmentInput(
                    request.Fulfillment.Type, request.Fulfillment.Phone,
                    request.Fulfillment.AddressId, request.Fulfillment.DeliveryWindow)),
            cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> ListOrdersAsync(
        ListOrdersHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<IResult> GetOrderAuthoringContextAsync(
        DateOnly? operationalDate,
        GetOrderAuthoringContextHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(operationalDate ?? DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken));

    private static async Task<IResult> GetOrderDetailsAsync(
        Guid orderId,
        GetOrderDetailsHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(orderId, cancellationToken));

    private static async Task<IResult> GetProductionSnapshotAsync(
        DateOnly? operationalDate,
        GetProductionSnapshotHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            operationalDate ?? DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken));

    private static async Task<IResult> GetPackingQueueAsync(
        DateOnly? operationalDate,
        GetPackingQueueHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            operationalDate ?? DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken));

    private static async Task<IResult> PackOrderAsync(
        Guid orderId,
        HttpContext httpContext,
        PackOrderRequest request,
        PackOrderHandler handler,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var key = ReadIdempotencyKey(httpContext);
        if (key.Error is not null) return key.Error;
        return TypedResults.Ok(await handler.HandleAsync(
            new PackOrderCommand(orderId, request.ExpectedVersion, currentUser.UserId, key.Value!), cancellationToken));
    }

    private static async Task<IResult> RecordLabelPrintAsync(
        Guid orderId,
        HttpContext httpContext,
        RecordLabelPrintRequest request,
        RecordLabelPrintHandler handler,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var key = ReadIdempotencyKey(httpContext);
        if (key.Error is not null) return key.Error;
        return TypedResults.Ok(await handler.HandleAsync(new RecordLabelPrintCommand(
            orderId,
            new PackingLabelSelection(request.DailyItemLabelIds, request.IncludeExternalPackageLabel),
            request.Status, request.ErrorMessage, currentUser.UserId, key.Value!), cancellationToken));
    }

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
                item.Name, item.Quantity, item.MeasurementUnit, item.DietaryMarkers ?? [], item.ReferencedProducibleItemId, item.Kind)).ToArray()), cancellationToken);
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
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GrantFinancialCreditCommand(
            request.CustomerId, request.Amount, request.Reason, currentUser.UserId), cancellationToken);
        return TypedResults.Created($"/api/financial-credits/{result.Id}", result);
    }

    private static async Task<IResult> GetCommerceAsync(CommerceService service, CancellationToken token) =>
        TypedResults.Ok(await service.GetAsync(token));
    private static async Task<IResult> CreateCustomerAsync(CustomerInput request, CommerceService service, CancellationToken token)
    { var id = await service.SaveCustomerAsync(null, request, token); return TypedResults.Created($"/api/customers/{id}", new { id }); }
    private static async Task<IResult> UpdateCustomerAsync(Guid id, CustomerInput request, CommerceService service, CancellationToken token)
    { await service.SaveCustomerAsync(id, request, token); return TypedResults.NoContent(); }
    private static async Task<IResult> CreatePlanAsync(PlanInput request, CommerceService service, CancellationToken token)
    { var id = await service.SavePlanAsync(null, request, token); return TypedResults.Created($"/api/plans/{id}", new { id }); }
    private static async Task<IResult> UpdatePlanAsync(Guid id, PlanInput request, CommerceService service, CancellationToken token)
    { await service.SavePlanAsync(id, request, token); return TypedResults.NoContent(); }
    private static async Task<IResult> CreateAuthoritativeAcquisitionAsync(AcquisitionInput request, CommerceService service, CancellationToken token)
    { var id = await service.AcquireAsync(request, token); return TypedResults.Created($"/api/plans/acquisitions/{id}", new { id }); }
    private static async Task<IResult> AdjustPlanCreditAsync(CreditAdjustmentInput request, CommerceService service, CancellationToken token)
    { await service.AdjustCreditAsync(request, token); return TypedResults.NoContent(); }
    private static async Task<IResult> AdjustFinancialCreditAsync(FinancialAdjustmentInput request, CommerceService service, CancellationToken token)
    { await service.AdjustFinancialAsync(request, token); return TypedResults.NoContent(); }
    private static async Task<IResult> RegisterPaymentAsync(HttpContext context, PaymentInput request, CommerceService service, CancellationToken token)
    {
        var (key, error) = ReadIdempotencyKey(context); if (error is not null) return error;
        var payment = await service.PayAsync(request, key!, token);
        return TypedResults.Created($"/api/payments/{payment.Id}", new { payment.Id });
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

public sealed record AttendanceModeRequest(Ts.Api.Domain.Attendance.AttendanceMode Mode, long ExpectedVersion);
public sealed record AttendanceMessageRequest(string Content);

public sealed record CreateOfferRequest(string Name, OfferFulfillmentMode FulfillmentMode);
public sealed record CatalogOfferRequest(string Name, string? Description, decimal BasePrice, OfferFulfillmentMode FulfillmentMode,
    bool RequiresMenuChoice, bool IsActive, IReadOnlyCollection<OfferComponentRequest> Components,
    IReadOnlyCollection<OfferChoiceGroupRequest> ChoiceGroups, IReadOnlyCollection<Guid> AllowedAddonIds);
public sealed record OfferComponentRequest(Guid ComponentTypeId, decimal Quantity);
public sealed record OfferChoiceGroupRequest(string Name, int MinimumSelections, int MaximumSelections, IReadOnlyCollection<OfferChoiceOptionRequest> Options);
public sealed record OfferChoiceOptionRequest(Guid ComponentTypeId, decimal Surcharge);
public sealed record ComponentTypeRequest(string Name, string? Description, bool IsActive = true);
public sealed record CatalogAddonRequest(string Name, decimal Price, Guid? ProducibleItemId, decimal? OperationalQuantity, string? MeasurementUnit, bool IsActive = true);

public sealed record CreateProducibleItemRequest(string Name);
public sealed record ProducibleRequest(string Name, string? Description, string Category, string MeasurementUnit, bool IsActive = true);
public sealed record DailyMenuRequest(IReadOnlyCollection<MenuOptionRequest> Options, IReadOnlyCollection<MenuOfferRequest> Offers, long? ExpectedVersion = null);
public sealed record MenuOptionRequest(string Category, Guid ProducibleItemId, MenuAvailability Availability);
public sealed record MenuOfferRequest(Guid OfferId, decimal EffectivePrice, MenuAvailability Availability, int DisplayOrder);
public sealed record PublishMenuRequest(long ExpectedVersion);
public sealed record DailyMenuImportRequest(DateOnly Date, DailyMenuRequest Menu);
public sealed record WeeklyPlanRequest(IReadOnlyCollection<DailyMenuImportRequest> Days, bool DeriveDrafts = false);

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
    int ProducedQuantity);

public sealed record UpdateFrozenConfigurationRequest(decimal UnitPrice, bool IsActive);

public sealed record RegisterFrozenMovementRequest(
    StockMovementType Type,
    int Quantity,
    string Reason);

public sealed record RegisterFrozenMovementResponse(Guid MovementId);

public sealed record ConfirmOrderRequest(
    long ExpectedVersion,
    IReadOnlyCollection<PlanCreditRequestBody>? PlanCredits = null,
    decimal DiscountAmount = 0,
    string? DiscountReason = null,
    decimal DeliveryFee = 0,
    decimal FinancialCreditAmount = 0);

public sealed record TransitionOrderStatusRequest(
    OrderStatus NewStatus, string Reason, long ExpectedVersion);

public sealed record RescheduleOrderRequest(
    DateOnly NewOperationalDate, string Reason, long ExpectedVersion);

public sealed record CancelOrderRequest(
    string Reason,
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
    IReadOnlyCollection<OrderItemRequest> Items,
    string? CustomerName = null,
    OrderFulfillmentRequest? Fulfillment = null);

public sealed record EditOrderRequest(
    Guid CustomerId,
    DateOnly OperationalDate,
    IReadOnlyCollection<OrderItemRequest> Items,
    long ExpectedVersion,
    string? CustomerName = null,
    OrderFulfillmentRequest? Fulfillment = null);

public sealed record OrderFulfillmentRequest(
    OrderFulfillmentType Type,
    string Phone,
    Guid? AddressId = null,
    string? DeliveryWindow = null);

public sealed record OrderItemRequest(
    Guid OfferId,
    int Quantity,
    decimal? UnitPrice = null,
    Guid? FrozenConfigurationId = null,
    Guid? ProducibleItemId = null);

public sealed record PackOrderRequest(long ExpectedVersion);

public sealed record RecordLabelPrintRequest(
    IReadOnlyCollection<string> DailyItemLabelIds,
    bool IncludeExternalPackageLabel,
    LabelPrintStatus Status,
    string? ErrorMessage = null);
public sealed record DeliveryDriverRequest(string Identification, string Name, string? Phone, bool IsActive = true, bool IsAvailable = true, long? ExpectedVersion = null);
public sealed record DeliveryRouteRequest(DateOnly Date, string DeliveryWindow, Guid DriverId, IReadOnlyCollection<Guid> OrderIds, long? ExpectedVersion = null);
public sealed record DeliveryRouteVersionRequest(long ExpectedVersion);
public sealed record DeliveryAttemptRequest(DeliveryAttemptResult Result, string? FailureReason = null, string? Note = null, string? ReceivedBy = null);
public sealed record DeliveryRescheduleRequest(DateOnly NewDate, string NewWindow, string Reason);
public sealed record MembershipRequest(OrganizationRole Role, bool IsActive, long ExpectedVersion);
public sealed record InvitationRequest(string Email, OrganizationRole Role);
public sealed record InvitationAcceptanceRequest(string Token);

public sealed record ConfigureDailyCapacityRequest(int TotalUnits, long ExpectedVersion);

public sealed record PublishCompositionRequest(IReadOnlyCollection<ProducibleComponentRequest> Components);
public sealed record ProducibleComponentRequest(
    string Name, decimal Quantity, string MeasurementUnit, IReadOnlyCollection<string>? DietaryMarkers = null,
    Guid? ReferencedProducibleItemId = null, string Kind = "Ingredient");
public sealed record AddCustomerRestrictionRequest(string Marker);
public sealed record CreatePlanAcquisitionRequest(
    Guid CustomerId, Guid EligibleOfferId, string PlanName, int Credits,
    decimal BenefitAmountPerCredit, DateOnly AcquiredOn);
public sealed record GrantFinancialCreditRequest(Guid CustomerId, decimal Amount, string Reason);
public sealed record SessionOrganization(
    Guid Id, string Name, string Slug, OrganizationRole Role, bool IsActive);
public sealed record SessionResponse(
    Guid UserId, string DisplayName, Guid? ActiveOrganizationId,
    IReadOnlyCollection<SessionOrganization> Organizations, PlatformSession Platform);
public sealed record PlatformSession(
    IReadOnlyCollection<string> Profiles, IReadOnlyCollection<string> Capabilities);
