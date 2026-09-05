using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Production;

namespace Ts.Api.Application.FrozenStock;

public sealed record FrozenExpirationPreview(DateOnly ManufacturedOn, DateOnly ExpiresOn);

public sealed class CalculateFrozenExpirationHandler
{
    public FrozenExpirationPreview Handle(DateOnly manufacturedOn) =>
        new(manufacturedOn, FrozenShelfLifePolicy.CalculateExpiration(manufacturedOn));
}

public sealed record FrozenProducibleView(Guid Id, string Name, bool IsActive);

public sealed record FrozenConfigurationView(
    Guid Id,
    Guid OfferId,
    Guid ProducibleItemId,
    string ProducibleName,
    string Presentation,
    decimal QuantityPerUnit,
    MeasurementUnit MeasurementUnit,
    decimal UnitPrice,
    bool IsActive);

public sealed record FrozenLotView(
    Guid Id,
    Guid FrozenConfigurationId,
    DateOnly ManufacturedOn,
    DateOnly ExpiresOn,
    int ProducedQuantity,
    int PhysicalQuantity,
    int AvailableQuantity,
    FrozenProductLabelView LabelSnapshot);

public sealed record FrozenProductLabelView(
    Guid LotId,
    string ProducibleName,
    string Presentation,
    DateOnly ManufacturedOn,
    DateOnly ExpiresOn);

public sealed record FrozenStockSummaryView(
    FrozenConfigurationView Configuration,
    string ProducibleName,
    int AvailableQuantity,
    int PhysicalQuantity,
    int LotCount,
    DateOnly? NextExpiration,
    Guid? NextLotId,
    string Status);

public sealed record FrozenExpirationView(
    FrozenLotView Lot,
    FrozenConfigurationView Configuration,
    string ProducibleName,
    string Status);

public sealed record FrozenMovementView(
    Guid Id,
    Guid LotId,
    StockMovementType Type,
    int Quantity,
    string ResponsibleName,
    DateTimeOffset OccurredAt,
    string? Reason,
    int PhysicalQuantityAfter,
    int AvailableQuantityAfter);

public sealed record FrozenLotDetailView(
    FrozenLotView Lot,
    FrozenConfigurationView Configuration,
    string ProducibleName,
    string Status,
    IReadOnlyCollection<FrozenMovementView> Movements);

public sealed record FrozenStockManagementView(
    Guid? FrozenOfferId,
    IReadOnlyCollection<FrozenProducibleView> Producibles,
    IReadOnlyCollection<FrozenConfigurationView> Configurations,
    IReadOnlyCollection<FrozenStockSummaryView> Stock,
    IReadOnlyCollection<FrozenExpirationView> Expirations);

public interface IFrozenStockManagementStore
{
    Task<IReadOnlyList<CatalogOffer>> GetActiveFrozenOffersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProducibleItem>> GetProduciblesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FrozenConfiguration>> GetConfigurationsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FrozenLot>> GetLotsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
    Task<FrozenConfiguration?> FindConfigurationAsync(Guid id, CancellationToken cancellationToken);
    Task<FrozenLot?> FindLotAsync(Guid id, CancellationToken cancellationToken);
    Task<FrozenStockMovement?> FindMovementByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);
    Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class GetFrozenStockManagementHandler(
    IFrozenStockManagementStore store,
    TimeProvider timeProvider)
{
    public async Task<FrozenStockManagementView> HandleAsync(CancellationToken cancellationToken)
    {
        var offers = await store.GetActiveFrozenOffersAsync(cancellationToken);
        var producibles = await store.GetProduciblesAsync(cancellationToken);
        var configurations = await store.GetConfigurationsAsync(cancellationToken);
        var lots = await store.GetLotsAsync(cancellationToken);
        var names = producibles.ToDictionary(item => item.Id, item => item.Name);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var configurationViews = configurations
            .Select(item => ToConfigurationView(item, names))
            .ToArray();
        var configurationById = configurationViews.ToDictionary(item => item.Id);

        var stock = configurations.Select(configuration =>
        {
            var view = configurationById[configuration.Id];
            var configurationLots = lots.Where(item => item.FrozenConfigurationId == configuration.Id).ToArray();
            var eligibleLots = configuration.IsActive
                ? configurationLots.Where(item => item.IsSellableOn(today))
                    .OrderBy(item => item.ExpiresOn).ThenBy(item => item.ManufacturedOn).ThenBy(item => item.Id)
                    .ToArray()
                : [];
            var available = eligibleLots.Sum(item => item.Balance);
            var next = eligibleLots.FirstOrDefault();
            return new FrozenStockSummaryView(
                view,
                view.ProducibleName,
                available,
                configurationLots.Sum(item => item.Balance),
                configurationLots.Length,
                next?.ExpiresOn,
                next?.Id,
                StatusFor(configuration, available, next, today));
        }).ToArray();

        var expirations = lots
            .Where(item => configurationById.ContainsKey(item.FrozenConfigurationId))
            .OrderBy(item => item.ExpiresOn).ThenBy(item => item.ManufacturedOn).ThenBy(item => item.Id)
            .Select(item =>
            {
                var configuration = configurationById[item.FrozenConfigurationId];
                return new FrozenExpirationView(
                    ToLotView(item, configuration, today),
                    configuration,
                    item.ProducibleNameSnapshot,
                    LotStatusFor(item, configuration.IsActive, today));
            }).ToArray();

        return new FrozenStockManagementView(
            offers.OrderBy(item => item.Id).Select(item => (Guid?)item.Id).FirstOrDefault(),
            producibles.OrderBy(item => item.Name)
                .Select(item => new FrozenProducibleView(item.Id, item.Name, item.IsActive)).ToArray(),
            configurationViews,
            stock,
            expirations);
    }

    internal static FrozenConfigurationView ToConfigurationView(
        FrozenConfiguration configuration,
        IReadOnlyDictionary<Guid, string> names) => new(
            configuration.Id,
            configuration.OfferId,
            configuration.ProducibleItemId,
            names.GetValueOrDefault(configuration.ProducibleItemId, "Item produzível indisponível"),
            configuration.Presentation,
            configuration.QuantityPerUnit,
            configuration.MeasurementUnit,
            configuration.UnitPrice,
            configuration.IsActive);

    internal static FrozenLotView ToLotView(
        FrozenLot lot,
        FrozenConfigurationView configuration,
        DateOnly today) => new(
            lot.Id,
            lot.FrozenConfigurationId,
            lot.ManufacturedOn,
            lot.ExpiresOn,
            lot.ProducedQuantity,
            lot.Balance,
            configuration.IsActive && lot.IsSellableOn(today) ? lot.Balance : 0,
            new FrozenProductLabelView(
                lot.Id,
                lot.ProducibleNameSnapshot,
                lot.PresentationSnapshot,
                lot.ManufacturedOn,
                lot.ExpiresOn));

    internal static string LotStatusFor(FrozenLot lot, bool configurationActive, DateOnly today)
    {
        if (!configurationActive) return "configuration-inactive";
        if (lot.ExpiresOn < today) return "expired";
        if (lot.Balance == 0) return "depleted";
        return lot.ExpiresOn <= today.AddDays(14) ? "near-expiration" : "available";
    }

    private static string StatusFor(
        FrozenConfiguration configuration,
        int available,
        FrozenLot? next,
        DateOnly today)
    {
        if (!configuration.IsActive) return "configuration-inactive";
        if (available == 0) return "depleted";
        return next?.ExpiresOn <= today.AddDays(14) ? "near-expiration" : "available";
    }
}

public sealed class GetFrozenLotHandler(
    IFrozenStockManagementStore store,
    TimeProvider timeProvider)
{
    public async Task<FrozenLotDetailView> HandleAsync(Guid lotId, CancellationToken cancellationToken)
    {
        var lot = await store.FindLotAsync(lotId, cancellationToken)
            ?? throw new ResourceNotFoundException("O lote de congelado não existe.");
        var configuration = await store.FindConfigurationAsync(lot.FrozenConfigurationId, cancellationToken)
            ?? throw new ResourceNotFoundException("A configuração do lote não existe.");
        var producibles = await store.GetProduciblesAsync(cancellationToken);
        var names = producibles.ToDictionary(item => item.Id, item => item.Name);
        var configurationView = GetFrozenStockManagementHandler.ToConfigurationView(configuration, names);
        var actorIds = lot.Movements.Select(item => item.ActorId).Distinct().ToArray();
        var actorNames = await store.GetUserNamesAsync(actorIds, cancellationToken);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var balance = 0;
        var movements = lot.Movements.OrderBy(item => item.OccurredAt).ThenBy(item => item.Id)
            .Select(item =>
            {
                balance += item.SignedQuantity;
                var available = lot.ExpiresOn >= DateOnly.FromDateTime(item.OccurredAt.UtcDateTime)
                    ? balance
                    : 0;
                return new FrozenMovementView(
                    item.Id, lot.Id, item.Type, item.SignedQuantity,
                    actorNames.GetValueOrDefault(item.ActorId, "Usuário indisponível"),
                    item.OccurredAt, item.Reason, balance, available);
            })
            .OrderByDescending(item => item.OccurredAt)
            .ThenByDescending(item => item.Id)
            .ToArray();

        return new FrozenLotDetailView(
            GetFrozenStockManagementHandler.ToLotView(lot, configurationView, today),
            configurationView,
            lot.ProducibleNameSnapshot,
            GetFrozenStockManagementHandler.LotStatusFor(lot, configuration.IsActive, today),
            movements);
    }
}

public sealed record UpdateFrozenConfigurationCommand(Guid Id, decimal UnitPrice, bool IsActive);

public sealed class UpdateFrozenConfigurationHandler(IFrozenStockManagementStore store)
{
    public async Task<FrozenConfiguration> HandleAsync(
        UpdateFrozenConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        var configuration = await store.FindConfigurationAsync(command.Id, cancellationToken)
            ?? throw new ResourceNotFoundException("A configuração de congelado não existe.");
        configuration.UpdateSalesSettings(command.UnitPrice, command.IsActive);
        await store.SaveChangesAsync(cancellationToken);
        return configuration;
    }
}

public sealed record RegisterFrozenMovementCommand(
    Guid LotId,
    StockMovementType Type,
    int Quantity,
    string Reason,
    Guid ActorId,
    string IdempotencyKey);

public sealed class RegisterFrozenMovementHandler(
    IFrozenStockManagementStore store,
    TimeProvider timeProvider)
{
    public Task<Guid> HandleAsync(
        RegisterFrozenMovementCommand command,
        CancellationToken cancellationToken) => store.ExecuteSerializableAsync(async transactionCancellationToken =>
    {
        if (command.Type is not StockMovementType.ManualAdjustment and not StockMovementType.ExpirationDisposal)
        {
            throw new ConflictException("Somente ajuste manual e descarte podem ser registrados por este recurso.");
        }

        var key = command.IdempotencyKey?.Trim() ?? string.Empty;
        var reason = command.Reason?.Trim() ?? string.Empty;
        var existing = await store.FindMovementByIdempotencyKeyAsync(key, transactionCancellationToken);
        if (existing is not null)
        {
            var expectedQuantity = command.Type == StockMovementType.ExpirationDisposal
                ? -command.Quantity
                : command.Quantity;
            if (existing.FrozenLotId != command.LotId || existing.Type != command.Type
                || existing.SignedQuantity != expectedQuantity || existing.ActorId != command.ActorId
                || existing.Reason != reason)
            {
                throw new ConflictException("A chave de idempotência já foi usada com dados diferentes.");
            }

            return existing.Id;
        }

        var lot = await store.FindLotAsync(command.LotId, transactionCancellationToken)
            ?? throw new ResourceNotFoundException("O lote de congelado não existe.");
        var now = timeProvider.GetUtcNow();
        var movement = command.Type == StockMovementType.ManualAdjustment
            ? lot.AdjustStock(command.Quantity, command.ActorId, now, reason, key)
            : lot.DisposeExpiredStock(command.Quantity, command.ActorId, now, reason, key);
        await store.SaveChangesAsync(transactionCancellationToken);
        return movement.Id;
    }, cancellationToken);
}
