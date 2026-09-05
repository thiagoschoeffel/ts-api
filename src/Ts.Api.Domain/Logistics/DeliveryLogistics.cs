using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Logistics;

public sealed class DeliveryDriver : ITenantOwned
{
    private DeliveryDriver() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Identification { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsAvailable { get; private set; }
    public long Version { get; private set; }

    public static DeliveryDriver Create(Guid organizationId, string identification, string name, string? phone, bool isActive = true, bool isAvailable = true)
    {
        if (organizationId == Guid.Empty) throw new DomainException("A organização é obrigatória.");
        return new DeliveryDriver { Id = Guid.NewGuid(), OrganizationId = organizationId,
            Identification = Required(identification, 40, "identificação"), Name = Required(name, 160, "nome"),
            Phone = PhoneValue(phone), IsActive = isActive, IsAvailable = isActive && isAvailable, Version = 1 };
    }

    public void Update(string identification, string name, string? phone, bool isActive, bool isAvailable, long expectedVersion)
    {
        if (Version != expectedVersion) throw new DomainException("O entregador foi alterado por outra pessoa.");
        Identification = Required(identification, 40, "identificação"); Name = Required(name, 160, "nome");
        Phone = PhoneValue(phone); IsActive = isActive; IsAvailable = isActive && isAvailable; Version++;
    }

    private static string Required(string value, int maximum, string field)
    { var normalized = value?.Trim() ?? string.Empty; if (normalized.Length is 0 || normalized.Length > maximum) throw new DomainException($"Informe uma {field} válida."); return normalized; }
    private static string? PhoneValue(string? value)
    { var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray()); if (digits.Length == 0) return null; if (digits.Length is < 10 or > 15) throw new DomainException("Informe um telefone válido com DDD."); return digits; }
}

public enum DeliveryRouteStatus { Planned = 1, InProgress = 2, Completed = 3, Cancelled = 4 }
public enum DeliveryAttemptResult { Succeeded = 1, Failed = 2 }

public sealed class DeliveryRoute : ITenantOwned
{
    private readonly List<DeliveryRouteStop> _stops = [];
    private DeliveryRoute() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public DateOnly Date { get; private set; }
    public string DeliveryWindow { get; private set; } = string.Empty;
    public Guid DriverId { get; private set; }
    public string DriverNameSnapshot { get; private set; } = string.Empty;
    public DeliveryRouteStatus Status { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public IReadOnlyCollection<DeliveryRouteStop> Stops => _stops.AsReadOnly();

    public static DeliveryRoute Create(Guid organizationId, DateOnly date, string window, DeliveryDriver driver,
        IReadOnlyCollection<DeliveryStopSnapshot> stops, Guid actorId, DateTimeOffset now)
    {
        if (!driver.IsActive || !driver.IsAvailable || driver.OrganizationId != organizationId) throw new DomainException("Selecione um entregador ativo e disponível desta organização.");
        if (stops.Count == 0) throw new DomainException("A rota deve possuir ao menos uma parada.");
        var route = new DeliveryRoute { Id = Guid.NewGuid(), OrganizationId = organizationId, Date = date,
            DeliveryWindow = Required(window, 80, "janela"), DriverId = driver.Id, DriverNameSnapshot = driver.Name,
            Status = DeliveryRouteStatus.Planned, Version = 1, CreatedAt = now, CreatedBy = actorId };
        var position = 1;
        foreach (var stop in stops) route._stops.Add(DeliveryRouteStop.Create(organizationId, route.Id, position++, stop));
        if (route._stops.Select(x => x.OrderId).Distinct().Count() != route._stops.Count) throw new DomainException("Um pedido não pode aparecer duas vezes na mesma rota.");
        return route;
    }

    public void Update(DeliveryDriver driver, IReadOnlyCollection<DeliveryStopSnapshot> stops, long expectedVersion)
    {
        EnsurePlanned(expectedVersion); if (!driver.IsActive || !driver.IsAvailable || driver.OrganizationId != OrganizationId) throw new DomainException("Selecione um entregador ativo e disponível desta organização.");
        if (stops.Count == 0 || stops.Select(x => x.OrderId).Distinct().Count() != stops.Count) throw new DomainException("Informe paradas válidas e sem duplicidade.");
        DriverId = driver.Id; DriverNameSnapshot = driver.Name; _stops.Clear(); var position = 1;
        foreach (var stop in stops) _stops.Add(DeliveryRouteStop.Create(OrganizationId, Id, position++, stop)); Version++;
    }
    public void Start(long expectedVersion, DateTimeOffset now) { EnsurePlanned(expectedVersion); Status = DeliveryRouteStatus.InProgress; StartedAt = now; Version++; }
    public void Cancel(long expectedVersion, Guid actorId, DateTimeOffset now) { EnsurePlanned(expectedVersion); Status = DeliveryRouteStatus.Cancelled; CancelledAt = now; CancelledBy = actorId; Version++; }
    public void CompleteIfTreated(int treatedStops, DateTimeOffset now) { if (Status == DeliveryRouteStatus.InProgress && treatedStops == _stops.Count) { Status = DeliveryRouteStatus.Completed; CompletedAt = now; Version++; } }
    private void EnsurePlanned(long expectedVersion) { if (Version != expectedVersion) throw new DomainException("A rota foi alterada por outra pessoa."); if (Status != DeliveryRouteStatus.Planned) throw new DomainException("Somente uma rota planejada pode ser alterada."); }
    private static string Required(string value, int maximum, string field) { var normalized = value?.Trim() ?? string.Empty; if (normalized.Length is 0 || normalized.Length > maximum) throw new DomainException($"Informe uma {field} válida."); return normalized; }
}

public sealed record DeliveryStopSnapshot(Guid OrderId, string CustomerName, string? CustomerPhone, string AddressSnapshot);

public sealed class DeliveryRouteStop : ITenantOwned
{
    private DeliveryRouteStop() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid RouteId { get; private set; }
    public Guid OrderId { get; private set; }
    public int Position { get; private set; }
    public string CustomerNameSnapshot { get; private set; } = string.Empty;
    public string? CustomerPhoneSnapshot { get; private set; }
    public string AddressSnapshot { get; private set; } = string.Empty;
    internal static DeliveryRouteStop Create(Guid organizationId, Guid routeId, int position, DeliveryStopSnapshot value) =>
        value.OrderId == Guid.Empty ? throw new DomainException("O pedido da parada é obrigatório.") : new() { Id = Guid.NewGuid(), OrganizationId = organizationId, RouteId = routeId,
            OrderId = value.OrderId, Position = position, CustomerNameSnapshot = value.CustomerName.Trim(), CustomerPhoneSnapshot = value.CustomerPhone?.Trim(), AddressSnapshot = value.AddressSnapshot.Trim() };
}

public sealed class DeliveryAttempt : ITenantOwned
{
    private DeliveryAttempt() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid RouteStopId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid DriverId { get; private set; }
    public string DriverNameSnapshot { get; private set; } = string.Empty;
    public DeliveryAttemptResult Result { get; private set; }
    public string? FailureReason { get; private set; }
    public string? Note { get; private set; }
    public string? ReceivedBy { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid RecordedBy { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public static DeliveryAttempt Create(Guid organizationId, DeliveryRoute route, DeliveryRouteStop stop, DeliveryAttemptResult result,
        string? failureReason, string? note, string? receivedBy, Guid actorId, DateTimeOffset now, string key)
    {
        if (route.Status != DeliveryRouteStatus.InProgress || stop.RouteId != route.Id) throw new DomainException("A parada não pertence a uma rota em execução.");
        var reason = failureReason?.Trim(); if (result == DeliveryAttemptResult.Failed && string.IsNullOrWhiteSpace(reason)) throw new DomainException("Informe o motivo da falha.");
        return new() { Id = Guid.NewGuid(), OrganizationId = organizationId, RouteStopId = stop.Id, OrderId = stop.OrderId, DriverId = route.DriverId,
            DriverNameSnapshot = route.DriverNameSnapshot, Result = result, FailureReason = reason, Note = note?.Trim(), ReceivedBy = receivedBy?.Trim(),
            OccurredAt = now, RecordedBy = actorId, IdempotencyKey = key.Trim() };
    }
}

public sealed class DeliveryReschedule : ITenantOwned
{
    private DeliveryReschedule() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid AttemptId { get; private set; }
    public DateOnly PreviousDate { get; private set; }
    public string PreviousWindow { get; private set; } = string.Empty;
    public DateOnly NewDate { get; private set; }
    public string NewWindow { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public Guid ActorId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public static DeliveryReschedule Create(Guid organizationId, Guid orderId, Guid attemptId, DateOnly previousDate, string previousWindow,
        DateOnly newDate, string newWindow, string reason, Guid actorId, DateTimeOffset now, string key)
    {
        if (newDate < DateOnly.FromDateTime(now.Date)) throw new DomainException("A nova data de entrega não pode estar no passado.");
        if (string.IsNullOrWhiteSpace(newWindow) || string.IsNullOrWhiteSpace(reason)) throw new DomainException("Nova janela e motivo são obrigatórios.");
        return new() { Id = Guid.NewGuid(), OrganizationId = organizationId, OrderId = orderId, AttemptId = attemptId,
            PreviousDate = previousDate, PreviousWindow = previousWindow, NewDate = newDate, NewWindow = newWindow.Trim(), Reason = reason.Trim(), ActorId = actorId, OccurredAt = now, IdempotencyKey = key.Trim() };
    }
}
