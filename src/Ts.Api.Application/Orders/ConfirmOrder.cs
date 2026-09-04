using Ts.Api.Application.Common;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Orders;

namespace Ts.Api.Application.Orders;

public interface IOrderConfirmationStore
{
    Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);

    Task<Order?> FindByConfirmationKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<Order?> FindOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<DailyCapacity?> FindDailyCapacityAsync(
        DateOnly operationalDate,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FrozenLot>> FindSellableLotsAsync(
        Guid frozenConfigurationId,
        DateOnly sellableOn,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ConfirmOrderHandler(
    IOrderConfirmationStore store,
    TimeProvider timeProvider)
{
    public Task<ConfirmOrderResult> HandleAsync(
        ConfirmOrderCommand command,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = command.IdempotencyKey?.Trim() ?? string.Empty;
        if (idempotencyKey.Length is 0 or > 200)
        {
            throw new DomainException("A chave de idempotência deve possuir entre 1 e 200 caracteres.");
        }

        return store.ExecuteSerializableAsync(
            transactionCancellationToken => ConfirmAsync(
                command,
                idempotencyKey,
                transactionCancellationToken),
            cancellationToken);
    }

    private async Task<ConfirmOrderResult> ConfirmAsync(
        ConfirmOrderCommand command,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var previousConfirmation = await store.FindByConfirmationKeyAsync(
            idempotencyKey,
            cancellationToken);
        if (previousConfirmation is not null)
        {
            if (previousConfirmation.Id != command.OrderId)
            {
                throw new ConflictException("A chave de idempotência já foi usada para outro pedido.");
            }

            return ToResult(previousConfirmation);
        }

        var order = await store.FindOrderAsync(command.OrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Pedido não encontrado.");

        if (order.Status != OrderStatus.Open)
        {
            throw new ConflictException("O pedido não está aberto para confirmação.");
        }

        if (order.Version != command.ExpectedVersion)
        {
            throw new ConflictException("O pedido foi alterado. Recarregue os dados antes de confirmar.");
        }

        if (order.DailyCapacityUnits > 0)
        {
            var capacity = await store.FindDailyCapacityAsync(
                order.OperationalDate,
                cancellationToken)
                ?? throw new ConflictException("A capacidade do dia operacional não foi configurada.");

            if (order.DailyCapacityUnits > capacity.AvailableUnits)
            {
                throw new ConflictException(
                    "A capacidade diária disponível é insuficiente para confirmar o pedido.");
            }

            capacity.Reserve(order.DailyCapacityUnits);
        }

        var confirmedAt = timeProvider.GetUtcNow();
        var sellableOn = DateOnly.FromDateTime(confirmedAt.UtcDateTime);
        foreach (var item in order.Items.Where(item => item.FrozenConfigurationId.HasValue))
        {
            var frozenConfigurationId = item.FrozenConfigurationId!.Value;
            var lots = await store.FindSellableLotsAsync(
                frozenConfigurationId,
                sellableOn,
                cancellationToken);
            var remaining = item.Quantity;
            foreach (var lot in lots)
            {
                if (remaining == 0)
                {
                    break;
                }

                var allocatedQuantity = Math.Min(remaining, lot.Balance);
                if (allocatedQuantity <= 0)
                {
                    continue;
                }

                lot.RemoveForOrder(
                    order.Id,
                    item.Id,
                    allocatedQuantity,
                    command.ActorId,
                    confirmedAt);
                order.AllocateFrozenStock(item, lot.Id, allocatedQuantity);
                remaining -= allocatedQuantity;
            }

            if (remaining > 0)
            {
                throw new ConflictException("O estoque congelado vendável é insuficiente para confirmar o pedido.");
            }
        }

        order.Confirm(command.ActorId, confirmedAt, idempotencyKey);
        await store.SaveChangesAsync(cancellationToken);
        return ToResult(order);
    }

    private static ConfirmOrderResult ToResult(Order order) => new(
        order.Id,
        order.Status,
        order.Version,
        order.FrozenAllocations
            .Select(allocation => new FrozenAllocation(
                allocation.OrderItemId,
                allocation.FrozenConfigurationId,
                allocation.FrozenLotId,
                allocation.Quantity))
            .ToArray());
}
