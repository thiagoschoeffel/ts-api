using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Catalog;
using Ts.Api.Application.FrozenStock;
using Ts.Api.Application.Production;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Production;

namespace Ts.Api.Infrastructure.Persistence;

public sealed class CatalogOfferStore(AppDbContext database) : ICatalogOfferStore
{
    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken) =>
        database.CatalogOffers.AnyAsync(
            item => item.NormalizedName == name.ToUpper(),
            cancellationToken);

    public async Task AddAsync(CatalogOffer offer, CancellationToken cancellationToken) =>
        await database.CatalogOffers.AddAsync(offer, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        database.SaveChangesAsync(cancellationToken);
}

public sealed class ProducibleItemStore(AppDbContext database) : IProducibleItemStore
{
    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken) =>
        database.ProducibleItems.AnyAsync(
            item => item.NormalizedName == name.ToUpper(),
            cancellationToken);

    public async Task AddAsync(ProducibleItem item, CancellationToken cancellationToken) =>
        await database.ProducibleItems.AddAsync(item, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        database.SaveChangesAsync(cancellationToken);
}

public sealed class FrozenConfigurationStore(AppDbContext database) : IFrozenConfigurationStore
{
    public Task<CatalogOffer?> FindOfferAsync(Guid id, CancellationToken cancellationToken) =>
        database.CatalogOffers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<ProducibleItem?> FindProducibleItemAsync(Guid id, CancellationToken cancellationToken) =>
        database.ProducibleItems.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> ConfigurationExistsAsync(
        Guid offerId,
        Guid producibleItemId,
        string presentation,
        CancellationToken cancellationToken) =>
        database.FrozenConfigurations.AnyAsync(
            item => item.OfferId == offerId
                && item.ProducibleItemId == producibleItemId
                && item.Presentation == presentation,
            cancellationToken);

    public async Task AddAsync(FrozenConfiguration configuration, CancellationToken cancellationToken) =>
        await database.FrozenConfigurations.AddAsync(configuration, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        database.SaveChangesAsync(cancellationToken);
}

public sealed class FrozenProductionStore(AppDbContext database) : IFrozenProductionStore
{
    public Task<FrozenConfiguration?> FindActiveConfigurationAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        database.FrozenConfigurations.SingleOrDefaultAsync(
            item => item.Id == id && item.IsActive,
            cancellationToken);

    public Task<FrozenLot?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        database.FrozenLots.SingleOrDefaultAsync(
            item => item.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public async Task<FrozenLot> AddOrGetByIdempotencyKeyAsync(
        FrozenLot lot,
        CancellationToken cancellationToken)
    {
        await database.FrozenLots.AddAsync(lot, cancellationToken);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return lot;
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            var existing = await FindByIdempotencyKeyAsync(lot.IdempotencyKey, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return existing;
        }
    }
}
