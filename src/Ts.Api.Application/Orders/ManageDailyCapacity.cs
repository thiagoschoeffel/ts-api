using Ts.Api.Application.Common;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Orders;

namespace Ts.Api.Application.Orders;

public sealed record ConfigureDailyCapacityCommand(
    DateOnly OperationalDate,
    int TotalUnits,
    long ExpectedVersion,
    string IdempotencyKey);

public sealed record DailyCapacityResult(
    DateOnly OperationalDate,
    int TotalUnits,
    int ReservedUnits,
    int AvailableUnits,
    long Version);

public interface IDailyCapacityManagementStore
{
    Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
    Task<DailyCapacity?> FindAsync(DateOnly operationalDate, CancellationToken cancellationToken);
    Task<DailyCapacity?> FindByConfigurationKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);
    Task AddAsync(DailyCapacity capacity, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ConfigureDailyCapacityHandler(
    IDailyCapacityManagementStore store,
    IOrganizationContext organizationContext)
{
    public Task<DailyCapacityResult> HandleAsync(
        ConfigureDailyCapacityCommand command,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = CreateOrderHandler.ValidateIdempotencyKey(command.IdempotencyKey);
        return store.ExecuteSerializableAsync(
            token => ConfigureAsync(command, idempotencyKey, token),
            cancellationToken);
    }

    private async Task<DailyCapacityResult> ConfigureAsync(
        ConfigureDailyCapacityCommand command,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var previous = await store.FindByConfigurationKeyAsync(idempotencyKey, cancellationToken);
        if (previous is not null)
        {
            if (previous.OperationalDate != command.OperationalDate
                || previous.TotalUnits != command.TotalUnits)
            {
                throw new ConflictException("A chave de idempotência já foi usada para outra configuração de capacidade.");
            }

            return Map(previous);
        }

        var capacity = await store.FindAsync(command.OperationalDate, cancellationToken);
        if (capacity is null)
        {
            if (command.ExpectedVersion != 0)
            {
                throw new ConflictException("A capacidade diária ainda não existe; use a versão esperada 0.");
            }

            capacity = DailyCapacity.Create(
                organizationContext.OrganizationId,
                command.OperationalDate,
                command.TotalUnits,
                idempotencyKey);
            await store.AddAsync(capacity, cancellationToken);
        }
        else
        {
            if (capacity.Version != command.ExpectedVersion)
            {
                throw new ConflictException("A capacidade diária foi alterada. Recarregue os dados antes de editar.");
            }

            try
            {
                capacity.Configure(command.TotalUnits, idempotencyKey);
            }
            catch (DomainException exception) when (command.TotalUnits < capacity.ReservedUnits)
            {
                throw new ConflictException(exception.Message);
            }
        }

        await store.SaveChangesAsync(cancellationToken);
        return Map(capacity);
    }

    private static DailyCapacityResult Map(DailyCapacity capacity) => new(
        capacity.OperationalDate,
        capacity.TotalUnits,
        capacity.ReservedUnits,
        capacity.AvailableUnits,
        capacity.Version);
}

public sealed class GetDailyCapacityHandler(IDailyCapacityManagementStore store)
{
    public async Task<DailyCapacityResult> HandleAsync(
        DateOnly operationalDate,
        CancellationToken cancellationToken)
    {
        var capacity = await store.FindAsync(operationalDate, cancellationToken)
            ?? throw new ResourceNotFoundException("A capacidade do dia operacional não foi configurada.");
        return new DailyCapacityResult(
            capacity.OperationalDate,
            capacity.TotalUnits,
            capacity.ReservedUnits,
            capacity.AvailableUnits,
            capacity.Version);
    }
}
