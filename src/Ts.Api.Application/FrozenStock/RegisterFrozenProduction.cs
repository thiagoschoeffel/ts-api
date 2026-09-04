using Ts.Api.Application.Common;
using Ts.Api.Domain.FrozenStock;

namespace Ts.Api.Application.FrozenStock;

public interface IFrozenProductionStore
{
    Task<FrozenConfiguration?> FindActiveConfigurationAsync(
        Guid id,
        CancellationToken cancellationToken);
    Task<FrozenLot?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);
    Task<FrozenLot> AddOrGetByIdempotencyKeyAsync(
        FrozenLot lot,
        CancellationToken cancellationToken);
}

public sealed class RegisterFrozenProductionHandler(
    IFrozenProductionStore store,
    TimeProvider timeProvider,
    IOrganizationContext organizationContext)
{
    public async Task<RegisterFrozenProductionResult> HandleAsync(
        RegisterFrozenProductionCommand command,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = command.IdempotencyKey?.Trim() ?? string.Empty;
        var existing = await store.FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return ResultForMatchingRequest(existing, command);
        }

        _ = await store.FindActiveConfigurationAsync(command.FrozenConfigurationId, cancellationToken)
            ?? throw new ResourceNotFoundException("A configuração de congelado não existe ou está inativa.");

        var candidate = FrozenLot.RegisterProduction(
            organizationContext.OrganizationId,
            command.FrozenConfigurationId,
            command.ManufacturedOn,
            command.ProducedQuantity,
            command.ActorId,
            timeProvider.GetUtcNow(),
            idempotencyKey);
        var persisted = await store.AddOrGetByIdempotencyKeyAsync(candidate, cancellationToken);
        return ResultForMatchingRequest(persisted, command);
    }

    private static RegisterFrozenProductionResult ResultForMatchingRequest(
        FrozenLot lot,
        RegisterFrozenProductionCommand command)
    {
        if (!lot.MatchesRegistration(
            command.FrozenConfigurationId,
            command.ManufacturedOn,
            command.ProducedQuantity,
            command.ActorId))
        {
            throw new ConflictException("A chave de idempotência já foi usada com dados diferentes.");
        }

        return new RegisterFrozenProductionResult(
            lot.Id,
            lot.ManufacturedOn,
            lot.ExpiresOn,
            lot.ProducedQuantity);
    }
}
