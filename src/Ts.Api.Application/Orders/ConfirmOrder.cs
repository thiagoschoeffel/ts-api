using Ts.Api.Application.Common;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Customers;
using Ts.Api.Domain.Finance;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Plans;
using Ts.Api.Domain.Production;

namespace Ts.Api.Application.Orders;

public interface IOrderConfirmationStore
{
    Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
    Task<Order?> FindByConfirmationKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<Order?> FindOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<DailyCapacity?> FindDailyCapacityAsync(DateOnly operationalDate, CancellationToken cancellationToken);
    Task<IReadOnlyList<FrozenLot>> FindSellableLotsAsync(Guid configurationId, DateOnly sellableOn, CancellationToken cancellationToken);
    Task<ProducibleComposition?> FindPublishedCompositionAsync(Guid producibleItemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerDietaryRestriction>> FindCustomerRestrictionsAsync(Guid customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlanAcquisition>> FindEligiblePlanAcquisitionsAsync(Guid customerId, Guid offerId, CancellationToken cancellationToken);
    Task<decimal> GetFinancialCreditBalanceAsync(Guid customerId, CancellationToken cancellationToken);
    void AddFinancialCreditMovement(FinancialCreditMovement movement);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ConfirmOrderHandler(IOrderConfirmationStore store, TimeProvider timeProvider)
{
    public Task<ConfirmOrderResult> HandleAsync(ConfirmOrderCommand command, CancellationToken cancellationToken)
    {
        var key = command.IdempotencyKey?.Trim() ?? string.Empty;
        if (key.Length is 0 or > 200)
            throw new DomainException("A chave de idempotência deve possuir entre 1 e 200 caracteres.");
        if (command.DiscountAmount < 0 || command.DeliveryFee < 0 || command.FinancialCreditAmount < 0)
            throw new DomainException("Desconto, taxa e crédito financeiro não podem ser negativos.");

        return store.ExecuteSerializableAsync(token => ConfirmAsync(command, key, token), cancellationToken);
    }

    private async Task<ConfirmOrderResult> ConfirmAsync(ConfirmOrderCommand command, string key, CancellationToken cancellationToken)
    {
        var previous = await store.FindByConfirmationKeyAsync(key, cancellationToken);
        if (previous is not null)
        {
            if (previous.Id != command.OrderId || !MatchesConfirmation(previous, command))
                throw new ConflictException("A chave de idempotência já foi usada para outro pedido ou condições de confirmação.");
            return ToResult(previous);
        }

        var order = await store.FindOrderAsync(command.OrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Pedido não encontrado.");
        if (order.Status != OrderStatus.Open)
            throw new ConflictException("O pedido não está aberto para confirmação.");
        if (order.Version != command.ExpectedVersion)
            throw new ConflictException("O pedido foi alterado. Recarregue os dados antes de confirmar.");

        var confirmedAt = timeProvider.GetUtcNow();
        await ConsolidateComponentsAsync(order, cancellationToken);
        await ConsumePlanCreditsAsync(order, command, confirmedAt, cancellationToken);

        var planCovered = order.PlanCreditAllocations.Sum(item => item.CoveredAmount);
        if (command.DiscountAmount > order.TotalAmount - planCovered)
            throw new ConflictException("O desconto excede o saldo dos itens após créditos de plano.");

        var remainingAmount = order.TotalAmount - planCovered - command.DiscountAmount + command.DeliveryFee;
        if (command.FinancialCreditAmount > remainingAmount)
            throw new ConflictException("O crédito financeiro excede o saldo restante do pedido.");
        if (command.FinancialCreditAmount > 0)
        {
            var available = await store.GetFinancialCreditBalanceAsync(order.CustomerId, cancellationToken);
            if (command.FinancialCreditAmount > available)
                throw new ConflictException("O cliente não possui crédito financeiro suficiente.");
            store.AddFinancialCreditMovement(FinancialCreditMovement.Consume(
                order.OrganizationId, order.CustomerId, order.Id,
                command.FinancialCreditAmount, command.ActorId, confirmedAt));
        }

        await ReserveCapacityAsync(order, cancellationToken);
        await AllocateFrozenStockAsync(order, command.ActorId, confirmedAt, cancellationToken);
        order.Confirm(command.ActorId, confirmedAt, key, command.DiscountAmount,
            command.DiscountReason, command.DeliveryFee, command.FinancialCreditAmount);
        await store.SaveChangesAsync(cancellationToken);
        return ToResult(order);
    }

    private async Task ConsolidateComponentsAsync(Order order, CancellationToken cancellationToken)
    {
        var restrictions = (await store.FindCustomerRestrictionsAsync(order.CustomerId, cancellationToken))
            .Select(item => item.Marker).ToHashSet(StringComparer.Ordinal);
        foreach (var item in order.Items.Where(item => item.ProducibleItemId.HasValue))
        {
            var composition = await store.FindPublishedCompositionAsync(item.ProducibleItemId!.Value, cancellationToken)
                ?? throw new ConflictException($"O item produzível de '{item.OfferName}' não possui composição publicada.");
            foreach (var component in composition.Components)
            {
                var incompatible = component.Markers.FirstOrDefault(restrictions.Contains);
                if (incompatible is not null)
                    throw new ConflictException($"A composição de '{item.OfferName}' é incompatível com a restrição '{incompatible}'.");
                order.SnapshotComponent(item, composition.Id, composition.Version, component.Name,
                    component.Quantity, component.MeasurementUnit, component.DietaryMarkers);
            }
        }
    }

    private async Task ConsumePlanCreditsAsync(Order order, ConfirmOrderCommand command, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var requests = command.PlanCredits ?? [];
        if (requests.GroupBy(item => item.OrderItemId).Any(group => group.Count() > 1))
            throw new DomainException("Cada item do pedido deve possuir uma única solicitação de crédito.");
        foreach (var request in requests)
        {
            var item = order.Items.SingleOrDefault(candidate => candidate.Id == request.OrderItemId)
                ?? throw new DomainException("O item solicitado para crédito não pertence ao pedido.");
            if (request.Quantity <= 0 || request.Quantity > item.Quantity)
                throw new DomainException("A quantidade de créditos solicitada é inválida.");

            var acquisitions = await store.FindEligiblePlanAcquisitionsAsync(order.CustomerId, item.OfferId, cancellationToken);
            var remaining = request.Quantity;
            foreach (var acquisition in acquisitions)
            {
                var quantity = Math.Min(remaining, acquisition.Balance);
                if (quantity <= 0) continue;
                acquisition.Consume(order.Id, item.Id, quantity, command.ActorId, occurredAt);
                var covered = Math.Min(item.UnitPrice, acquisition.BenefitAmountPerCredit) * quantity;
                order.AllocatePlanCredit(item, acquisition.Id, acquisition.PlanName, quantity, covered);
                remaining -= quantity;
                if (remaining == 0) break;
            }

            if (remaining > 0)
                throw new ConflictException($"O cliente não possui créditos de plano compatíveis suficientes para '{item.OfferName}'.");
        }
    }

    private async Task ReserveCapacityAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.DailyCapacityUnits == 0) return;
        var capacity = await store.FindDailyCapacityAsync(order.OperationalDate, cancellationToken)
            ?? throw new ConflictException("A capacidade do dia operacional não foi configurada.");
        if (order.DailyCapacityUnits > capacity.AvailableUnits)
            throw new ConflictException("A capacidade diária disponível é insuficiente para confirmar o pedido.");
        capacity.Reserve(order.DailyCapacityUnits);
    }

    private async Task AllocateFrozenStockAsync(Order order, Guid actorId, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var sellableOn = DateOnly.FromDateTime(occurredAt.UtcDateTime);
        foreach (var item in order.Items.Where(item => item.FrozenConfigurationId.HasValue))
        {
            var lots = await store.FindSellableLotsAsync(item.FrozenConfigurationId!.Value, sellableOn, cancellationToken);
            var remaining = item.Quantity;
            foreach (var lot in lots)
            {
                var quantity = Math.Min(remaining, lot.Balance);
                if (quantity <= 0) continue;
                lot.RemoveForOrder(order.Id, item.Id, quantity, actorId, occurredAt);
                order.AllocateFrozenStock(item, lot.Id, quantity);
                remaining -= quantity;
                if (remaining == 0) break;
            }
            if (remaining > 0)
                throw new ConflictException("O estoque congelado vendável é insuficiente para confirmar o pedido.");
        }
    }

    private static bool MatchesConfirmation(Order order, ConfirmOrderCommand command)
    {
        var audit = order.ConfirmationAudits.SingleOrDefault();
        if (audit is null)
        {
            return order.ConfirmedBy == command.ActorId
                && (command.PlanCredits?.Count ?? 0) == 0
                && command.DiscountAmount == 0
                && string.IsNullOrWhiteSpace(command.DiscountReason)
                && command.DeliveryFee == 0
                && command.FinancialCreditAmount == 0;
        }
        var requested = (command.PlanCredits ?? []).OrderBy(item => item.OrderItemId).ToArray();
        var persisted = order.PlanCreditAllocations.GroupBy(item => item.OrderItemId)
            .Select(group => new PlanCreditRequest(group.Key, group.Sum(item => item.Quantity)))
            .OrderBy(item => item.OrderItemId).ToArray();
        return audit.ActorId == command.ActorId
            && audit.DiscountAmount == command.DiscountAmount
            && audit.DiscountReason == command.DiscountReason?.Trim()
            && audit.DeliveryFee == command.DeliveryFee
            && audit.FinancialCreditApplied == command.FinancialCreditAmount
            && requested.SequenceEqual(persisted);
    }

    private static ConfirmOrderResult ToResult(Order order)
    {
        var audit = order.ConfirmationAudits.SingleOrDefault();
        return new ConfirmOrderResult(order.Id, order.Status, order.Version,
            order.FrozenAllocations.Select(item => new FrozenAllocation(item.OrderItemId, item.FrozenConfigurationId, item.FrozenLotId, item.Quantity)).ToArray(),
            order.ComponentSnapshots.Select(item => new ComponentSnapshot(item.OrderItemId, item.CompositionId, item.CompositionVersion,
                item.Name, item.QuantityPerUnit, item.TotalQuantity, item.MeasurementUnit,
                item.DietaryMarkers.Split(',', StringSplitOptions.RemoveEmptyEntries))).ToArray(),
            order.PlanCreditAllocations.Select(item => new PlanCreditAllocationResult(item.OrderItemId, item.AcquisitionId,
                item.PlanName, item.Quantity, item.CoveredAmount)).ToArray(),
            audit is null
                ? new ConfirmationFinancialSummary(order.TotalAmount, 0, 0, null, 0, 0, order.Charges.SingleOrDefault()?.Amount ?? 0)
                : new ConfirmationFinancialSummary(audit.Subtotal, audit.PlanCreditCoveredAmount, audit.DiscountAmount,
                    audit.DiscountReason, audit.DeliveryFee, audit.FinancialCreditApplied, audit.AmountDue));
    }
}
