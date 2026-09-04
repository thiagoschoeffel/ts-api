namespace Ts.Api.Application.FrozenStock;

public sealed record RegisterFrozenProductionCommand(
    Guid FrozenConfigurationId,
    DateOnly ManufacturedOn,
    int ProducedQuantity,
    Guid ActorId,
    string IdempotencyKey);

public sealed record RegisterFrozenProductionResult(
    Guid FrozenLotId,
    DateOnly ManufacturedOn,
    DateOnly ExpiresOn,
    int ProducedQuantity);
