using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Production;

namespace Ts.Api.Application.FrozenStock;

public sealed record CreateFrozenConfigurationCommand(
    Guid OfferId,
    Guid ProducibleItemId,
    string Presentation,
    decimal QuantityPerUnit,
    MeasurementUnit MeasurementUnit,
    decimal UnitPrice);

public sealed record CreateFrozenConfigurationResult(
    Guid Id,
    Guid OfferId,
    Guid ProducibleItemId,
    string Presentation,
    decimal QuantityPerUnit,
    MeasurementUnit MeasurementUnit,
    decimal UnitPrice,
    bool IsActive);

public interface IFrozenConfigurationStore
{
    Task<CatalogOffer?> FindOfferAsync(Guid id, CancellationToken cancellationToken);
    Task<ProducibleItem?> FindProducibleItemAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ConfigurationExistsAsync(
        Guid offerId,
        Guid producibleItemId,
        string presentation,
        CancellationToken cancellationToken);
    Task AddAsync(FrozenConfiguration configuration, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class CreateFrozenConfigurationHandler(IFrozenConfigurationStore store)
{
    public async Task<CreateFrozenConfigurationResult> HandleAsync(
        CreateFrozenConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        var offer = await store.FindOfferAsync(command.OfferId, cancellationToken)
            ?? throw new ResourceNotFoundException("A oferta informada não existe.");
        if (!offer.IsActive || offer.FulfillmentMode != OfferFulfillmentMode.FrozenStock)
        {
            throw new ConflictException("A configuração exige uma oferta ativa da modalidade Congelados.");
        }

        var producibleItem = await store.FindProducibleItemAsync(command.ProducibleItemId, cancellationToken)
            ?? throw new ResourceNotFoundException("O item produzível informado não existe.");
        if (!producibleItem.IsActive)
        {
            throw new ConflictException("O item produzível informado está inativo.");
        }

        var presentation = command.Presentation?.Trim() ?? string.Empty;
        if (await store.ConfigurationExistsAsync(
            command.OfferId,
            command.ProducibleItemId,
            presentation,
            cancellationToken))
        {
            throw new ConflictException("Essa configuração de congelado já existe.");
        }

        var configuration = FrozenConfiguration.Create(
            command.OfferId,
            command.ProducibleItemId,
            presentation,
            command.QuantityPerUnit,
            command.MeasurementUnit,
            command.UnitPrice);
        await store.AddAsync(configuration, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);

        return new CreateFrozenConfigurationResult(
            configuration.Id,
            configuration.OfferId,
            configuration.ProducibleItemId,
            configuration.Presentation,
            configuration.QuantityPerUnit,
            configuration.MeasurementUnit,
            configuration.UnitPrice,
            configuration.IsActive);
    }
}
