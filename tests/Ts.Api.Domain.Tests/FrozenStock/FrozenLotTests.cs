using Ts.Api.Domain.Common;
using Ts.Api.Domain.FrozenStock;

namespace Ts.Api.Domain.Tests.FrozenStock;

public sealed class FrozenLotTests
{
    [Fact]
    public void RegisterProduction_CreatesLotAndProductionEntry()
    {
        var actorId = Guid.NewGuid();
        var recordedAt = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

        var lot = FrozenLot.RegisterProduction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 4),
            24,
            actorId,
            recordedAt,
            "production-2026-09-04-01");

        var movement = Assert.Single(lot.Movements);
        Assert.Equal(new DateOnly(2026, 12, 3), lot.ExpiresOn);
        Assert.Equal(24, lot.Balance);
        Assert.Equal(StockMovementType.ProductionEntry, movement.Type);
        Assert.Equal(actorId, movement.ActorId);
    }

    [Fact]
    public void RegisterProduction_RejectsNonPositiveQuantity()
    {
        var action = () => FrozenLot.RegisterProduction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 4),
            0,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "production-invalid");

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void IsSellableOn_RejectsExpiredLotEvenWithPhysicalBalance()
    {
        var lot = FrozenLot.RegisterProduction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            10,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "production-expired");

        Assert.False(lot.IsSellableOn(lot.ExpiresOn.AddDays(1)));
        Assert.Equal(10, lot.Balance);
    }

    [Fact]
    public void RegisterProduction_RejectsMissingIdempotencyKey()
    {
        var action = () => FrozenLot.RegisterProduction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 4),
            10,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            " ");

        Assert.Throws<DomainException>(action);
    }
}
