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
            "production-2026-09-04-01",
            "Estrogonofe de frango",
            "300 g");

        var movement = Assert.Single(lot.Movements);
        Assert.Equal(new DateOnly(2026, 12, 3), lot.ExpiresOn);
        Assert.Equal(24, lot.Balance);
        Assert.Equal(StockMovementType.ProductionEntry, movement.Type);
        Assert.Equal(actorId, movement.ActorId);
        Assert.Equal("Estrogonofe de frango", lot.ProducibleNameSnapshot);
        Assert.Equal("300 g", lot.PresentationSnapshot);
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

    [Fact]
    public void AdjustStock_ChangesBalanceThroughMovementWithoutAllowingNegativeStock()
    {
        var lot = FrozenLot.RegisterProduction(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 4), 10,
            Guid.NewGuid(), DateTimeOffset.UtcNow, "production-adjustment");

        var movement = lot.AdjustStock(
            -3, Guid.NewGuid(), DateTimeOffset.UtcNow, "Conferência física", "adjustment-01");

        Assert.Equal(7, lot.Balance);
        Assert.Equal(-3, movement.SignedQuantity);
        Assert.Equal(StockMovementType.ManualAdjustment, movement.Type);
        Assert.Throws<DomainException>(() => lot.AdjustStock(
            -8, Guid.NewGuid(), DateTimeOffset.UtcNow, "Saldo inválido", "adjustment-02"));
    }

    [Fact]
    public void DisposeExpiredStock_RemovesPhysicalUnitsAndPreservesTraceability()
    {
        var actorId = Guid.NewGuid();
        var lot = FrozenLot.RegisterProduction(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), 10,
            actorId, DateTimeOffset.UtcNow, "production-disposal");

        var movement = lot.DisposeExpiredStock(
            4, actorId, DateTimeOffset.UtcNow, "Validade expirada", "disposal-01");

        Assert.Equal(6, lot.Balance);
        Assert.Equal(-4, movement.SignedQuantity);
        Assert.Equal("Validade expirada", movement.Reason);
        Assert.Equal("disposal-01", movement.IdempotencyKey);
    }
}
