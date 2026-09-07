using System.Text.Json;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Operations;
using Ts.Api.Domain.Orders;

namespace Ts.Api.Application.Operations;

public sealed record ProductionWindowNeed(string Window, decimal Quantity);
public sealed record ProductionCustomization(string Label, int Quantity);
public sealed record ProductionNeedResult(
    string Id, string Name, string Unit, decimal Quantity, int OrderCount,
    IReadOnlyCollection<ProductionWindowNeed> Windows,
    IReadOnlyCollection<ProductionCustomization> Customizations);
public sealed record ProductionSnapshotResult(
    int OrderCount, int MealCount, int InProductionCount, int CustomizationCount,
    IReadOnlyCollection<ProductionNeedResult> Needs, DateTimeOffset UpdatedAt);

public sealed record DailyItemLabelSnapshot(
    string Id, Guid OrderItemId, Guid OrderId, string CustomerName,
    string ProductName, IReadOnlyCollection<string> DetailLines,
    IReadOnlyCollection<string> AttentionLines);
public sealed record ExternalPackageLabelSnapshot(
    string Id, Guid OrderId, string CustomerName, string? Phone,
    IReadOnlyCollection<string> AddressLines, IReadOnlyCollection<string> ItemSummary);
public sealed record PackingLabelSnapshot(
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<DailyItemLabelSnapshot> DailyItemLabels,
    IReadOnlyCollection<Guid> PreLabeledFrozenItemIds,
    ExternalPackageLabelSnapshot ExternalPackageLabel);
public sealed record PackingLabelSelection(
    IReadOnlyCollection<string> DailyItemLabelIds,
    bool IncludeExternalPackageLabel);
public sealed record LabelPrintAttemptResult(
    Guid Id, DateTimeOffset AttemptedAt, string ResponsibleName,
    PackingLabelSelection Selection, LabelPrintStatus Status, string? ErrorMessage);
public sealed record PackingItemResult(
    Guid Id, string Name, string? Presentation, int Quantity, bool IsFrozen,
    IReadOnlyCollection<string> DetailLines, IReadOnlyCollection<string> AttentionLines);
public sealed record PackingOrderResult(
    Guid Id, long Version, string CustomerName, string? CustomerPhone, string? DeliveryWindow,
    IReadOnlyCollection<PackingItemResult> Items, DateTimeOffset? PackedAt, string? PackedBy,
    PackingLabelSnapshot? Labels, IReadOnlyCollection<LabelPrintAttemptResult> PrintAttempts);
public sealed record PackingQueueResult(
    IReadOnlyCollection<PackingOrderResult> Awaiting,
    IReadOnlyCollection<PackingOrderResult> Packed,
    int AwaitingItemCount, int PackedItemCount, int AttentionCount);

public interface IOperationsStore
{
    Task<IReadOnlyList<Order>> GetOperationalOrdersAsync(DateOnly date, CancellationToken cancellationToken);
    Task<Order?> FindOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<PackingRecord?> FindPackingByOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<PackingRecord?> FindPackingByKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<LabelPrintAttempt?> FindPrintAttemptByKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<PackingRecord>> GetPackingsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<LabelPrintAttempt>> GetPrintAttemptsAsync(IReadOnlyCollection<Guid> packingIds, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
    Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
    void Add(PackingRecord record);
    void Add(LabelPrintAttempt attempt);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class GetProductionSnapshotHandler(IOperationsStore store, TimeProvider timeProvider)
{
    private static readonly HashSet<OrderStatus> IncludedStatuses =
        [OrderStatus.Confirmed, OrderStatus.InProduction, OrderStatus.InPacking, OrderStatus.InDelivery, OrderStatus.Completed];

    public async Task<ProductionSnapshotResult> HandleAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var orders = (await store.GetOperationalOrdersAsync(date, cancellationToken))
            .Where(order => IncludedStatuses.Contains(order.Status)).ToArray();
        var needs = orders.SelectMany(order => order.ComponentSnapshots.Select(component => new { order, component }))
            .GroupBy(item => new { item.component.CompositionId, item.component.Name, item.component.MeasurementUnit })
            .Select(group => new ProductionNeedResult(
                $"{group.Key.CompositionId:N}-{group.Key.Name.ToUpperInvariant()}", group.Key.Name, group.Key.MeasurementUnit,
                group.Sum(item => item.component.TotalQuantity),
                group.Select(item => item.order.Id).Distinct().Count(),
                [new ProductionWindowNeed("Dia", group.Sum(item => item.component.TotalQuantity))],
                []))
            .OrderByDescending(item => item.Quantity).ThenBy(item => item.Name).ToArray();

        return new ProductionSnapshotResult(
            orders.Length,
            orders.Sum(order => order.Items.Where(item => item.FulfillmentMode == OfferFulfillmentMode.DailyProduction).Sum(item => item.Quantity)),
            orders.Count(order => order.Status == OrderStatus.InProduction),
            0,
            needs,
            timeProvider.GetUtcNow());
    }
}

public sealed class GetPackingQueueHandler(IOperationsStore store)
{
    public async Task<PackingQueueResult> HandleAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var orders = (await store.GetOperationalOrdersAsync(date, cancellationToken))
            .Where(order => order.Status is OrderStatus.Confirmed or OrderStatus.InProduction or OrderStatus.InPacking)
            .ToArray();
        var packings = await store.GetPackingsAsync(orders.Select(item => item.Id).ToArray(), cancellationToken);
        var attempts = await store.GetPrintAttemptsAsync(packings.Select(item => item.Id).ToArray(), cancellationToken);
        var userIds = packings.Select(item => item.PackedBy).Concat(attempts.Select(item => item.AttemptedBy)).Distinct().ToArray();
        var users = await store.GetUserNamesAsync(userIds, cancellationToken);
        var packingByOrder = packings.ToDictionary(item => item.OrderId);
        var mapped = orders.Select(order => MapOrder(order, packingByOrder.GetValueOrDefault(order.Id), attempts, users)).ToArray();
        var awaiting = mapped.Where(item => item.PackedAt is null).ToArray();
        var packed = mapped.Where(item => item.PackedAt is not null).ToArray();
        return new PackingQueueResult(
            awaiting, packed,
            awaiting.Sum(item => item.Items.Sum(child => child.Quantity)),
            packed.Sum(item => item.Items.Sum(child => child.Quantity)),
            awaiting.Count(item => item.Items.Any(child => child.AttentionLines.Count > 0)));
    }

    private static PackingOrderResult MapOrder(
        Order order, PackingRecord? packing, IReadOnlyCollection<LabelPrintAttempt> attempts,
        IReadOnlyDictionary<Guid, string> users)
    {
        PackingLabelSnapshot? labels = packing is null ? null : JsonSerializer.Deserialize<PackingLabelSnapshot>(packing.SnapshotJson);
        var items = order.Items.Select(item => new PackingItemResult(
            item.Id, item.ProducibleItemName ?? item.OfferName, item.FrozenPresentation, item.Quantity,
            item.FulfillmentMode == OfferFulfillmentMode.FrozenStock,
            order.ComponentSnapshots.Where(component => component.OrderItemId == item.Id)
                .Select(component => $"{component.Name}: {component.TotalQuantity:g} {component.MeasurementUnit}").ToArray(),
            order.ComponentSnapshots.Where(component => component.OrderItemId == item.Id)
                .SelectMany(component => SplitMarkers(component.DietaryMarkers)).Distinct().ToArray())).ToArray();
        var printAttempts = packing is null ? [] : attempts.Where(item => item.PackingRecordId == packing.Id)
            .OrderByDescending(item => item.AttemptedAt)
            .Select(item => new LabelPrintAttemptResult(
                item.Id, item.AttemptedAt, users.GetValueOrDefault(item.AttemptedBy, "Operador"),
                JsonSerializer.Deserialize<PackingLabelSelection>(item.SelectionJson)!, item.Status, item.ErrorMessage)).ToArray();
        return new PackingOrderResult(
            order.Id, order.Version, order.CustomerNameSnapshot, order.FulfillmentPhone, order.DeliveryWindow,
            items, packing?.PackedAt, packing is null ? null : users.GetValueOrDefault(packing.PackedBy, "Operador"),
            labels, printAttempts);
    }

    private static IEnumerable<string> SplitMarkers(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public sealed record PackOrderCommand(Guid OrderId, long ExpectedVersion, Guid ActorId, string IdempotencyKey);
public sealed class PackOrderHandler(IOperationsStore store, TimeProvider timeProvider)
{
    public Task<PackingOrderResult> HandleAsync(PackOrderCommand command, CancellationToken cancellationToken) =>
        store.ExecuteSerializableAsync(async transactionalToken =>
        {
            var existing = await store.FindPackingByKeyAsync(command.IdempotencyKey, transactionalToken)
                ?? await store.FindPackingByOrderAsync(command.OrderId, transactionalToken);
            if (existing is not null)
            {
                if (existing.OrderId != command.OrderId) throw new ConflictException("A chave de idempotência já pertence a outro pedido.");
                return await GetResult(existing, transactionalToken);
            }

            var order = await store.FindOrderAsync(command.OrderId, transactionalToken)
                ?? throw new ResourceNotFoundException("Pedido não encontrado.");
            if (order.Version != command.ExpectedVersion) throw new ConflictException("O pedido foi alterado. Recarregue a fila e tente novamente.");
            var now = timeProvider.GetUtcNow();
            if (order.Status == OrderStatus.Confirmed && order.DailyCapacityUnits == 0)
                order.TransitionStatus(OrderStatus.InPacking, "Pedido de estoque congelado conferido e embalado", command.ActorId, now, $"{command.IdempotencyKey}:packing");
            else if (order.Status == OrderStatus.Confirmed)
                throw new ConflictException("A produção diária precisa ser iniciada antes da embalagem.");
            if (order.Status == OrderStatus.InProduction)
                order.TransitionStatus(OrderStatus.InPacking, "Pedido conferido e embalado", command.ActorId, now, $"{command.IdempotencyKey}:packing");
            if (order.Status != OrderStatus.InPacking) throw new ConflictException("O pedido não está elegível para embalagem.");

            var snapshot = CreateSnapshot(order, now);
            var packing = PackingRecord.Create(order.OrganizationId, order.Id, command.ActorId, now,
                command.IdempotencyKey, JsonSerializer.Serialize(snapshot));
            store.Add(packing);
            await store.SaveChangesAsync(transactionalToken);
            return await GetResult(packing, transactionalToken);
        }, cancellationToken);

    private async Task<PackingOrderResult> GetResult(PackingRecord packing, CancellationToken cancellationToken)
    {
        var order = await store.FindOrderAsync(packing.OrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Pedido não encontrado.");
        var users = await store.GetUserNamesAsync([packing.PackedBy], cancellationToken);
        var labels = JsonSerializer.Deserialize<PackingLabelSnapshot>(packing.SnapshotJson)!;
        var items = order.Items.Select(item => new PackingItemResult(
            item.Id, item.ProducibleItemName ?? item.OfferName, item.FrozenPresentation, item.Quantity,
            item.FulfillmentMode == OfferFulfillmentMode.FrozenStock, [], [])).ToArray();
        return new PackingOrderResult(order.Id, order.Version,
            labels.ExternalPackageLabel.CustomerName, labels.ExternalPackageLabel.Phone, order.DeliveryWindow, items,
            packing.PackedAt, users.GetValueOrDefault(packing.PackedBy, "Operador"), labels, []);
    }

    private static PackingLabelSnapshot CreateSnapshot(Order order, DateTimeOffset createdAt)
    {
        var customerName = order.CustomerNameSnapshot;
        var dailyLabels = order.Items.Where(item => item.FulfillmentMode == OfferFulfillmentMode.DailyProduction)
            .SelectMany(item => Enumerable.Range(1, item.Quantity).Select(unit => new DailyItemLabelSnapshot(
                $"pedido-{order.Id:N}-item-{item.Id:N}-{unit}", item.Id, order.Id, customerName,
                item.ProducibleItemName ?? item.OfferName,
                order.ComponentSnapshots.Where(component => component.OrderItemId == item.Id)
                    .Select(component => $"{component.Name}: {component.QuantityPerUnit:g} {component.MeasurementUnit}").ToArray(),
                order.ComponentSnapshots.Where(component => component.OrderItemId == item.Id)
                    .SelectMany(component => component.DietaryMarkers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Distinct().ToArray()))).ToArray();
        return new PackingLabelSnapshot(
            createdAt,
            dailyLabels,
            order.Items.Where(item => item.FulfillmentMode == OfferFulfillmentMode.FrozenStock).Select(item => item.Id).ToArray(),
            new ExternalPackageLabelSnapshot(
                $"pedido-{order.Id:N}-pacote", order.Id, customerName, order.FulfillmentPhone,
                AddressLines(order),
                order.Items.Select(item => $"{item.Quantity}× {item.ProducibleItemName ?? item.OfferName}").ToArray()));
    }

    private static IReadOnlyCollection<string> AddressLines(Order order)
    {
        if (order.FulfillmentType != OrderFulfillmentType.Delivery) return [];
        return new[]
        {
            string.Join(", ", new[] { order.FulfillmentStreet, order.FulfillmentNumber, order.FulfillmentComplement }.Where(value => !string.IsNullOrWhiteSpace(value))),
            string.Join(" · ", new[] { order.FulfillmentNeighborhood, order.FulfillmentCity, order.FulfillmentState, order.FulfillmentPostalCode }.Where(value => !string.IsNullOrWhiteSpace(value))),
            order.FulfillmentReference is null ? null : $"Referência: {order.FulfillmentReference}",
        }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToArray();
    }
}

public sealed record RecordLabelPrintCommand(
    Guid OrderId, PackingLabelSelection Selection, LabelPrintStatus Status,
    string? ErrorMessage, Guid ActorId, string IdempotencyKey);
public sealed class RecordLabelPrintHandler(IOperationsStore store, TimeProvider timeProvider)
{
    public async Task<LabelPrintAttemptResult> HandleAsync(RecordLabelPrintCommand command, CancellationToken cancellationToken)
    {
        var existing = await store.FindPrintAttemptByKeyAsync(command.IdempotencyKey, cancellationToken);
        if (existing is not null) return await Map(existing, cancellationToken);
        var packing = await store.FindPackingByOrderAsync(command.OrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("A embalagem do pedido não foi registrada.");
        var snapshot = JsonSerializer.Deserialize<PackingLabelSnapshot>(packing.SnapshotJson)!;
        var available = snapshot.DailyItemLabels.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        if (command.Selection.DailyItemLabelIds.Count == 0 && !command.Selection.IncludeExternalPackageLabel)
            throw new DomainException("Selecione ao menos uma etiqueta.");
        if (command.Selection.DailyItemLabelIds.Count != command.Selection.DailyItemLabelIds.Distinct().Count()
            || command.Selection.DailyItemLabelIds.Any(id => !available.Contains(id)))
            throw new DomainException("A seleção contém etiquetas que não pertencem ao snapshot histórico.");
        var attempt = LabelPrintAttempt.Create(
            packing.OrganizationId, packing.Id, command.ActorId, timeProvider.GetUtcNow(), command.IdempotencyKey,
            JsonSerializer.Serialize(command.Selection), command.Status, command.ErrorMessage);
        store.Add(attempt);
        await store.SaveChangesAsync(cancellationToken);
        return await Map(attempt, cancellationToken);
    }

    private async Task<LabelPrintAttemptResult> Map(LabelPrintAttempt attempt, CancellationToken cancellationToken)
    {
        var users = await store.GetUserNamesAsync([attempt.AttemptedBy], cancellationToken);
        return new LabelPrintAttemptResult(
            attempt.Id, attempt.AttemptedAt, users.GetValueOrDefault(attempt.AttemptedBy, "Operador"),
            JsonSerializer.Deserialize<PackingLabelSelection>(attempt.SelectionJson)!, attempt.Status, attempt.ErrorMessage);
    }
}
