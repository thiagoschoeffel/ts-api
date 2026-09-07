using Ts.Api.Application.Common;
using Ts.Api.Domain.Customers;
using Ts.Api.Domain.Logistics;
using Ts.Api.Domain.Operations;
using Ts.Api.Domain.Orders;

namespace Ts.Api.Application.Logistics;

public sealed record DeliveryDriverInput(string Identification, string Name, string? Phone, bool IsActive = true, bool IsAvailable = true, long? ExpectedVersion = null);
public sealed record DeliveryDriverResult(Guid Id, string Identification, string Name, string? Phone, bool IsActive, bool IsAvailable, long Version);
public sealed record DeliveryOrderResult(Guid Id, long Version, OrderStatus Status, DateOnly Date, string DeliveryWindow, string CustomerName, string? CustomerPhone, string Address, Guid? PreferredDriverId);
public sealed record DeliveryStopResult(Guid Id, Guid OrderId, int Position, string CustomerName, string? CustomerPhone, string Address, DeliveryAttemptResult? Result);
public sealed record DeliveryAttemptResultDto(Guid Id, Guid OrderId, Guid DriverId, string DriverName, DeliveryAttemptResult Result, string? FailureReason, string? Note, string? ReceivedBy, DateTimeOffset OccurredAt);
public sealed record DeliveryRescheduleResult(Guid Id, Guid OrderId, DateOnly PreviousDate, string PreviousWindow, DateOnly NewDate, string NewWindow, string Reason, DateTimeOffset OccurredAt);
public sealed record DeliveryRouteResult(Guid Id, DateOnly Date, string DeliveryWindow, Guid DriverId, string DriverName, DeliveryRouteStatus Status, long Version,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, DateTimeOffset? CancelledAt,
    IReadOnlyCollection<DeliveryStopResult> Stops, IReadOnlyCollection<DeliveryAttemptResultDto> Attempts);
public sealed record LogisticsSnapshotResult(IReadOnlyCollection<DeliveryDriverResult> Drivers, IReadOnlyCollection<DeliveryOrderResult> AvailableOrders,
    IReadOnlyCollection<DeliveryRouteResult> Routes, IReadOnlyCollection<DeliveryRescheduleResult> Reschedules);

public interface ILogisticsStore
{
    Task<IReadOnlyList<DeliveryDriver>> GetDriversAsync(CancellationToken token);
    Task<DeliveryDriver?> FindDriverAsync(Guid id, CancellationToken token);
    Task<bool> DriverIdentificationExistsAsync(string identification, Guid? exceptId, CancellationToken token);
    Task<IReadOnlyList<Order>> GetDeliveryOrdersAsync(CancellationToken token);
    Task<Order?> FindOrderAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<Customer>> GetCustomersAsync(IReadOnlyCollection<Guid> ids, CancellationToken token);
    Task<IReadOnlyList<CustomerAddress>> GetAddressesAsync(IReadOnlyCollection<Guid> ids, CancellationToken token);
    Task<IReadOnlyList<PackingRecord>> GetPackingsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken token);
    Task<IReadOnlyList<DeliveryRoute>> GetRoutesAsync(CancellationToken token);
    Task<DeliveryRoute?> FindRouteAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<DeliveryAttempt>> GetAttemptsAsync(IReadOnlyCollection<Guid> stopIds, CancellationToken token);
    Task<DeliveryAttempt?> FindAttemptByKeyAsync(string key, CancellationToken token);
    Task<IReadOnlyList<DeliveryReschedule>> GetReschedulesAsync(CancellationToken token);
    Task<DeliveryReschedule?> FindRescheduleByKeyAsync(string key, CancellationToken token);
    void Add(object entity);
    void ReplaceStops(DeliveryRoute route);
    Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken token);
    Task SaveChangesAsync(CancellationToken token);
}

public sealed class LogisticsService(ILogisticsStore store, IOrganizationContext organization, TimeProvider timeProvider)
{
    public async Task<LogisticsSnapshotResult> GetAsync(CancellationToken token)
    {
        var drivers = await store.GetDriversAsync(token); var orders = await store.GetDeliveryOrdersAsync(token);
        var customers = await store.GetCustomersAsync(orders.Select(x => x.CustomerId).Distinct().ToArray(), token);
        var packings = await store.GetPackingsAsync(orders.Select(x => x.Id).ToArray(), token);
        var routes = await store.GetRoutesAsync(token); var attempts = await store.GetAttemptsAsync(routes.SelectMany(x => x.Stops).Select(x => x.Id).ToArray(), token);
        var reschedules = await store.GetReschedulesAsync(token);
        var assigned = routes.Where(x => x.Status is DeliveryRouteStatus.Planned or DeliveryRouteStatus.InProgress).SelectMany(x => x.Stops).Select(x => x.OrderId).ToHashSet();
        var packingIds = packings.Select(x => x.OrderId).ToHashSet(); var customerById = customers.ToDictionary(x => x.Id);
        var latestReschedule = reschedules.GroupBy(x => x.OrderId).ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.OccurredAt).First());
        var available = orders.Where(x => x.FulfillmentType == OrderFulfillmentType.Delivery
                && HasCompleteDeliverySnapshot(x) && !assigned.Contains(x.Id)
                && (x.Status == OrderStatus.InPacking && packingIds.Contains(x.Id) || x.Status == OrderStatus.DeliveryFailed && latestReschedule.ContainsKey(x.Id)))
            .Select(order => MapAvailable(order, customerById.GetValueOrDefault(order.CustomerId), latestReschedule.GetValueOrDefault(order.Id))).ToArray();
        return new(drivers.Select(MapDriver).ToArray(), available, routes.Select(route => MapRoute(route, attempts)).ToArray(),
            reschedules.Select(x => new DeliveryRescheduleResult(x.Id, x.OrderId, x.PreviousDate, x.PreviousWindow, x.NewDate, x.NewWindow, x.Reason, x.OccurredAt)).ToArray());
    }

    public async Task<DeliveryDriverResult> SaveDriverAsync(Guid? id, DeliveryDriverInput input, CancellationToken token)
    {
        var normalized = input.Identification.Trim().ToUpperInvariant();
        if (await store.DriverIdentificationExistsAsync(normalized, id, token)) throw new ConflictException("Já existe um entregador com esta identificação.");
        DeliveryDriver driver;
        if (id is null) { driver = DeliveryDriver.Create(organization.OrganizationId, normalized, input.Name, input.Phone, input.IsActive, input.IsAvailable); store.Add(driver); }
        else { driver = await store.FindDriverAsync(id.Value, token) ?? throw new ResourceNotFoundException("Entregador não encontrado."); driver.Update(normalized, input.Name, input.Phone, input.IsActive, input.IsAvailable, input.ExpectedVersion ?? 0); }
        await store.SaveChangesAsync(token); return MapDriver(driver);
    }

    public Task<DeliveryRouteResult> CreateRouteAsync(DateOnly date, string window, Guid driverId, IReadOnlyCollection<Guid> orderIds, Guid actorId, CancellationToken token) =>
        store.ExecuteSerializableAsync(async tx =>
        {
            var driver = await store.FindDriverAsync(driverId, tx) ?? throw new ResourceNotFoundException("Entregador não encontrado.");
            var snapshots = await ResolveEligibleStops(orderIds, date, window, tx);
            var route = DeliveryRoute.Create(organization.OrganizationId, date, window, driver, snapshots, actorId, timeProvider.GetUtcNow()); store.Add(route);
            await store.SaveChangesAsync(tx); return MapRoute(route, []);
        }, token);

    public Task<DeliveryRouteResult> UpdateRouteAsync(Guid routeId, Guid driverId, IReadOnlyCollection<Guid> orderIds, long version, CancellationToken token) =>
        store.ExecuteSerializableAsync(async tx =>
        {
            var route = await store.FindRouteAsync(routeId, tx) ?? throw new ResourceNotFoundException("Rota não encontrada.");
            var driver = await store.FindDriverAsync(driverId, tx) ?? throw new ResourceNotFoundException("Entregador não encontrado.");
            var snapshots = await ResolveEligibleStops(orderIds, route.Date, route.DeliveryWindow, tx, route.Id);
            store.ReplaceStops(route); route.Update(driver, snapshots, version); await store.SaveChangesAsync(tx); return MapRoute(route, []);
        }, token);

    public Task<DeliveryRouteResult> StartRouteAsync(Guid routeId, long version, Guid actorId, string key, CancellationToken token) =>
        store.ExecuteSerializableAsync(async tx =>
        {
            var route = await store.FindRouteAsync(routeId, tx) ?? throw new ResourceNotFoundException("Rota não encontrada.");
            var driver = await store.FindDriverAsync(route.DriverId, tx) ?? throw new ResourceNotFoundException("Entregador não encontrado.");
            if (!driver.IsActive || !driver.IsAvailable) throw new ConflictException("O entregador não está disponível para iniciar a rota.");
            foreach (var stop in route.Stops) { var order = await store.FindOrderAsync(stop.OrderId, tx) ?? throw new ConflictException("Um pedido da rota não existe mais.");
                if (order.Status is not (OrderStatus.InPacking or OrderStatus.DeliveryFailed)) throw new ConflictException("Um ou mais pedidos deixaram de estar aptos para entrega.");
                order.TransitionStatus(OrderStatus.InDelivery, "Rota de entrega iniciada", actorId, timeProvider.GetUtcNow(), $"{key}:{order.Id:N}"); }
            route.Start(version, timeProvider.GetUtcNow()); await store.SaveChangesAsync(tx); return MapRoute(route, []);
        }, token);

    public async Task<DeliveryRouteResult> CancelRouteAsync(Guid routeId, long version, Guid actorId, CancellationToken token)
    { var route = await store.FindRouteAsync(routeId, token) ?? throw new ResourceNotFoundException("Rota não encontrada."); route.Cancel(version, actorId, timeProvider.GetUtcNow()); await store.SaveChangesAsync(token); return MapRoute(route, []); }

    public Task<DeliveryAttemptResultDto> RecordAttemptAsync(Guid routeId, Guid stopId, DeliveryAttemptResult result, string? reason, string? note,
        string? receivedBy, Guid actorId, string key, CancellationToken token) => store.ExecuteSerializableAsync(async tx =>
    {
        var replay = await store.FindAttemptByKeyAsync(key, tx);
        if (replay is not null)
        {
            if (replay.RouteStopId != stopId || replay.Result != result || replay.FailureReason != reason?.Trim()
                || replay.Note != note?.Trim() || replay.ReceivedBy != receivedBy?.Trim())
                throw new ConflictException("A chave de idempotência já foi usada com outra tentativa.");
            return MapAttempt(replay);
        }
        var route = await store.FindRouteAsync(routeId, tx) ?? throw new ResourceNotFoundException("Rota não encontrada.");
        var stop = route.Stops.SingleOrDefault(x => x.Id == stopId) ?? throw new ResourceNotFoundException("Parada não encontrada.");
        var prior = await store.GetAttemptsAsync([stop.Id], tx); if (prior.Count > 0) throw new ConflictException("Esta parada já foi tratada.");
        var order = await store.FindOrderAsync(stop.OrderId, tx) ?? throw new ResourceNotFoundException("Pedido não encontrado.");
        if (order.Status != OrderStatus.InDelivery) throw new ConflictException("O pedido não está em entrega.");
        var now = timeProvider.GetUtcNow(); var attempt = DeliveryAttempt.Create(organization.OrganizationId, route, stop, result, reason, note, receivedBy, actorId, now, key); store.Add(attempt);
        order.TransitionStatus(result == DeliveryAttemptResult.Succeeded ? OrderStatus.Completed : OrderStatus.DeliveryFailed,
            result == DeliveryAttemptResult.Succeeded ? "Entrega concluída" : reason!, actorId, now, $"{key}:order");
        route.CompleteIfTreated((await store.GetAttemptsAsync(route.Stops.Select(x => x.Id).ToArray(), tx)).Count + 1, now);
        await store.SaveChangesAsync(tx); return MapAttempt(attempt);
    }, token);

    public Task<DeliveryRescheduleResult> RescheduleAsync(Guid orderId, DateOnly newDate, string newWindow, string reason, Guid actorId, string key, CancellationToken token) =>
        store.ExecuteSerializableAsync(async tx =>
        {
            var replay = await store.FindRescheduleByKeyAsync(key, tx);
            if (replay is not null)
            {
                if (replay.OrderId != orderId || replay.NewDate != newDate || replay.NewWindow != newWindow.Trim() || replay.Reason != reason.Trim())
                    throw new ConflictException("A chave de idempotência já foi usada com outro reagendamento.");
                return MapReschedule(replay);
            }
            var order = await store.FindOrderAsync(orderId, tx) ?? throw new ResourceNotFoundException("Pedido não encontrado."); if (order.Status != OrderStatus.DeliveryFailed) throw new ConflictException("Somente uma entrega com falha pode ser reagendada.");
            var routes = await store.GetRoutesAsync(tx); var route = routes.Where(x => x.Stops.Any(s => s.OrderId == orderId)).OrderByDescending(x => x.CreatedAt).FirstOrDefault() ?? throw new ConflictException("A tentativa anterior não foi encontrada.");
            var stop = route.Stops.Single(x => x.OrderId == orderId); var attempts = await store.GetAttemptsAsync([stop.Id], tx); var attempt = attempts.OrderByDescending(x => x.OccurredAt).FirstOrDefault() ?? throw new ConflictException("A tentativa anterior não foi encontrada.");
            var item = DeliveryReschedule.Create(organization.OrganizationId, orderId, attempt.Id, route.Date, route.DeliveryWindow, newDate, newWindow, reason, actorId, timeProvider.GetUtcNow(), key); store.Add(item); await store.SaveChangesAsync(tx); return MapReschedule(item);
        }, token);

    private async Task<IReadOnlyCollection<DeliveryStopSnapshot>> ResolveEligibleStops(IReadOnlyCollection<Guid> ids, DateOnly date, string window, CancellationToken token, Guid? currentRouteId = null)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count) throw new ConflictException("Informe pedidos válidos e sem duplicidade.");
        var orders = await store.GetDeliveryOrdersAsync(token); var selected = orders.Where(x => ids.Contains(x.Id)).ToArray(); if (selected.Length != ids.Count) throw new ResourceNotFoundException("Um ou mais pedidos não foram encontrados.");
        var packings = await store.GetPackingsAsync(ids, token); var packed = packings.Select(x => x.OrderId).ToHashSet(); var reschedules = await store.GetReschedulesAsync(token);
        var routes = await store.GetRoutesAsync(token); var assigned = routes.Where(x => x.Id != currentRouteId && x.Status is DeliveryRouteStatus.Planned or DeliveryRouteStatus.InProgress).SelectMany(x => x.Stops).Select(x => x.OrderId).ToHashSet();
        if (selected.Any(x => assigned.Contains(x.Id) || x.Status == OrderStatus.InPacking && (!packed.Contains(x.Id) || x.OperationalDate != date) || x.Status != OrderStatus.InPacking && x.Status != OrderStatus.DeliveryFailed)) throw new ConflictException("Um ou mais pedidos não estão aptos para esta rota.");
        if (selected.Any(x => x.Status == OrderStatus.DeliveryFailed && !reschedules.Any(r => r.OrderId == x.Id && r.NewDate == date && r.NewWindow == window))) throw new ConflictException("A nova tentativa deve respeitar o reagendamento registrado.");
        if (selected.Any(order => order.FulfillmentType != OrderFulfillmentType.Delivery || !HasCompleteDeliverySnapshot(order)))
            throw new ConflictException("Um ou mais pedidos não possuem snapshot histórico completo de entrega.");
        return ids.Select(id => { var order = selected.Single(x => x.Id == id);
            return new DeliveryStopSnapshot(order.Id, order.FulfillmentContactName ?? order.CustomerNameSnapshot,
                order.FulfillmentPhone, AddressText(order)); }).ToArray();
    }
    private static DeliveryOrderResult MapAvailable(Order o, Customer? c, DeliveryReschedule? r) => new(o.Id, o.Version, o.Status,
        r?.NewDate ?? o.OperationalDate, r?.NewWindow ?? o.DeliveryWindow!, o.FulfillmentContactName ?? o.CustomerNameSnapshot,
        o.FulfillmentPhone, AddressText(o), ParseDriver(c?.PreferredDeliveryDriverId));
    private static Guid? ParseDriver(string? value) => Guid.TryParse(value, out var id) ? id : null;
    private static bool HasCompleteDeliverySnapshot(Order order) => order.FulfillmentPhone is not null
        && order.FulfillmentStreet is not null && order.DeliveryWindow is not null;
    private static string AddressText(Order order) => string.Join(", ", new[] {
        order.FulfillmentStreet, order.FulfillmentNumber, order.FulfillmentComplement,
        order.FulfillmentNeighborhood, order.FulfillmentCity, order.FulfillmentState,
        order.FulfillmentPostalCode, order.FulfillmentReference
    }.Where(value => !string.IsNullOrWhiteSpace(value)));
    private static DeliveryDriverResult MapDriver(DeliveryDriver x) => new(x.Id, x.Identification, x.Name, x.Phone, x.IsActive, x.IsAvailable, x.Version);
    private static DeliveryRouteResult MapRoute(DeliveryRoute x, IReadOnlyCollection<DeliveryAttempt> all) { var attempts = all.Where(a => x.Stops.Any(s => s.Id == a.RouteStopId)).ToArray(); return new(x.Id, x.Date, x.DeliveryWindow, x.DriverId, x.DriverNameSnapshot, x.Status, x.Version, x.CreatedAt, x.StartedAt, x.CompletedAt, x.CancelledAt,
        x.Stops.OrderBy(s => s.Position).Select(s => new DeliveryStopResult(s.Id, s.OrderId, s.Position, s.CustomerNameSnapshot, s.CustomerPhoneSnapshot, s.AddressSnapshot, attempts.SingleOrDefault(a => a.RouteStopId == s.Id)?.Result)).ToArray(), attempts.Select(MapAttempt).ToArray()); }
    private static DeliveryAttemptResultDto MapAttempt(DeliveryAttempt x) => new(x.Id, x.OrderId, x.DriverId, x.DriverNameSnapshot, x.Result, x.FailureReason, x.Note, x.ReceivedBy, x.OccurredAt);
    private static DeliveryRescheduleResult MapReschedule(DeliveryReschedule x) => new(x.Id, x.OrderId, x.PreviousDate, x.PreviousWindow, x.NewDate, x.NewWindow, x.Reason, x.OccurredAt);
}
