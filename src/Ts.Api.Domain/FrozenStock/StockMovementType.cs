namespace Ts.Api.Domain.FrozenStock;

public enum StockMovementType
{
    ProductionEntry = 1,
    OrderExit = 2,
    OrderReversal = 3,
    ManualAdjustment = 4,
    ExpirationDisposal = 5,
}
