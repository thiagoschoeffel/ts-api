using Ts.Api.Domain.Common;
using Ts.Api.Domain.Logistics;

namespace Ts.Api.Domain.Tests.Domain;

public sealed class DeliveryLogisticsTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Route_RejectsDriverFromAnotherOrganization()
    {
        var driver = DeliveryDriver.Create(Guid.NewGuid(), "MOTO-01", "Ana", null);
        Assert.Throws<DomainException>(() => DeliveryRoute.Create(OrganizationId, new(2026, 9, 5), "11:00–12:00", driver,
            [new(Guid.NewGuid(), "Cliente", null, "Rua A, 10")], ActorId, Now));
    }

    [Fact]
    public void Route_OnlyAllowsPlannedTransitionsAndKeepsOrderedStops()
    {
        var driver = DeliveryDriver.Create(OrganizationId, "MOTO-01", "Ana", null);
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var route = DeliveryRoute.Create(OrganizationId, new(2026, 9, 5), "11:00–12:00", driver,
            [new(first, "A", null, "Rua A"), new(second, "B", null, "Rua B")], ActorId, Now);
        route.Start(route.Version, Now);
        Assert.Equal([first, second], route.Stops.OrderBy(x => x.Position).Select(x => x.OrderId));
        Assert.Throws<DomainException>(() => route.Cancel(route.Version, ActorId, Now));
    }

    [Fact]
    public void Attempt_RequiresFailureReasonAndPreservesExecutorSnapshot()
    {
        var driver = DeliveryDriver.Create(OrganizationId, "MOTO-01", "Ana", null);
        var route = DeliveryRoute.Create(OrganizationId, new(2026, 9, 5), "11:00–12:00", driver,
            [new(Guid.NewGuid(), "Cliente", null, "Rua A")], ActorId, Now);
        route.Start(route.Version, Now); var stop = route.Stops.Single();
        Assert.Throws<DomainException>(() => DeliveryAttempt.Create(OrganizationId, route, stop, DeliveryAttemptResult.Failed, null, null, null, ActorId, Now, "attempt-1"));
        var attempt = DeliveryAttempt.Create(OrganizationId, route, stop, DeliveryAttemptResult.Succeeded, null, "Portaria", "João", ActorId, Now, "attempt-2");
        Assert.Equal(driver.Name, attempt.DriverNameSnapshot);
        Assert.Equal(stop.OrderId, attempt.OrderId);
    }

    [Fact]
    public void CompletedRouteCanContainFailedAttempt()
    {
        var driver = DeliveryDriver.Create(OrganizationId, "MOTO-01", "Ana", null);
        var route = DeliveryRoute.Create(OrganizationId, new(2026, 9, 5), "11:00–12:00", driver,
            [new(Guid.NewGuid(), "Cliente", null, "Rua A")], ActorId, Now);
        route.Start(route.Version, Now);
        _ = DeliveryAttempt.Create(OrganizationId, route, route.Stops.Single(), DeliveryAttemptResult.Failed, "Cliente ausente", null, null, ActorId, Now, "attempt");
        route.CompleteIfTreated(1, Now);
        Assert.Equal(DeliveryRouteStatus.Completed, route.Status);
    }
}
