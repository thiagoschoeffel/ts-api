using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ts.Api.Application.Catalog;
using Ts.Api.Application.Common;
using Ts.Api.Application.FrozenStock;
using Ts.Api.Application.Orders;
using Ts.Api.Application.Production;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Orders;
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

public sealed class OrderConfirmationStore(AppDbContext database) : IOrderConfirmationStore
{
    public async Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 3;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            await using var transaction = await database.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            try
            {
                var result = await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception exception) when (IsRetryableConcurrencyConflict(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                database.ChangeTracker.Clear();

                if (attempt == maximumAttempts)
                {
                    throw new ConflictException(
                        "A confirmação conflitou com outra operação. Recarregue os dados e tente novamente.");
                }
            }
        }

        throw new InvalidOperationException("O fluxo de confirmação terminou sem resultado.");
    }

    public Task<Order?> FindByConfirmationKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        OrdersWithConfirmationGraph()
            .SingleOrDefaultAsync(
                item => item.ConfirmationIdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<Order?> FindOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        OrdersWithConfirmationGraph()
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);

    public Task<DailyCapacity?> FindDailyCapacityAsync(
        DateOnly operationalDate,
        CancellationToken cancellationToken) =>
        database.DailyCapacities.SingleOrDefaultAsync(
            item => item.OperationalDate == operationalDate,
            cancellationToken);

    public async Task<IReadOnlyList<FrozenLot>> FindSellableLotsAsync(
        Guid frozenConfigurationId,
        DateOnly sellableOn,
        CancellationToken cancellationToken) =>
        await database.FrozenLots
            .Include("_movements")
            .Where(item => item.FrozenConfigurationId == frozenConfigurationId
                && item.ExpiresOn >= sellableOn)
            .OrderBy(item => item.ExpiresOn)
            .ThenBy(item => item.ManufacturedOn)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        MarkNewEffectsAsAdded<FrozenStockAllocation>();
        MarkNewEffectsAsAdded<FrozenStockMovement>();
        MarkNewEffectsAsAdded<OrderCharge>();
        return database.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Order> OrdersWithConfirmationGraph() =>
        database.Orders
            .Include("_items")
            .Include("_frozenAllocations")
            .Include("_charges")
            .AsSplitQuery();

    private void MarkNewEffectsAsAdded<TEntity>() where TEntity : class
    {
        foreach (var entry in database.ChangeTracker.Entries<TEntity>()
                     .Where(entry => entry.State == EntityState.Modified))
        {
            entry.State = EntityState.Added;
        }
    }

    private static bool IsRetryableConcurrencyConflict(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        PostgresException postgresException =>
            postgresException.SqlState == PostgresErrorCodes.SerializationFailure
            || postgresException.SqlState == PostgresErrorCodes.DeadlockDetected
            || (postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                && postgresException.ConstraintName
                    == "IX_orders_OrganizationId_ConfirmationIdempotencyKey"),
        _ when exception.InnerException is not null =>
            IsRetryableConcurrencyConflict(exception.InnerException),
        _ => false,
    };
}

public sealed class OrderManagementStore(AppDbContext database) :
    IOrderManagementStore,
    IDailyCapacityManagementStore
{
    public async Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 3;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            await using var transaction = await database.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            try
            {
                var result = await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception exception) when (IsRetryableConcurrencyConflict(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                database.ChangeTracker.Clear();
                if (attempt == maximumAttempts)
                {
                    throw new ConflictException(
                        "A operação conflitou com outra gravação. Recarregue os dados e tente novamente.");
                }
            }
        }

        throw new InvalidOperationException("A operação terminou sem resultado.");
    }

    public Task<Order?> FindByCreationKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken) => OrdersWithItems().SingleOrDefaultAsync(
        item => item.CreationIdempotencyKey == idempotencyKey,
        cancellationToken);

    public Task<Order?> FindByModificationKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken) => OrdersWithItems().SingleOrDefaultAsync(
        item => item.LastModificationIdempotencyKey == idempotencyKey,
        cancellationToken);

    public Task<Order?> FindOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        OrdersWithItems().SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);

    public Task<CatalogOffer?> FindActiveOfferAsync(
        Guid offerId,
        CancellationToken cancellationToken) => database.CatalogOffers.SingleOrDefaultAsync(
        item => item.Id == offerId && item.IsActive,
        cancellationToken);

    public Task<FrozenConfiguration?> FindActiveFrozenConfigurationAsync(
        Guid configurationId,
        CancellationToken cancellationToken) => database.FrozenConfigurations.SingleOrDefaultAsync(
        item => item.Id == configurationId && item.IsActive,
        cancellationToken);

    public Task<ProducibleItem?> FindActiveProducibleItemAsync(
        Guid producibleItemId,
        CancellationToken cancellationToken) => database.ProducibleItems.SingleOrDefaultAsync(
        item => item.Id == producibleItemId && item.IsActive,
        cancellationToken);

    public async Task AddAsync(Order order, CancellationToken cancellationToken) =>
        await database.Orders.AddAsync(order, cancellationToken);

    public void ReplaceItems(
        IReadOnlyCollection<OrderItem> previousItems,
        IReadOnlyCollection<OrderItem> replacementItems)
    {
        database.OrderItems.RemoveRange(previousItems);
        database.OrderItems.AddRange(replacementItems);
    }

    public Task<DailyCapacity?> FindAsync(
        DateOnly operationalDate,
        CancellationToken cancellationToken) => database.DailyCapacities.SingleOrDefaultAsync(
        item => item.OperationalDate == operationalDate,
        cancellationToken);

    public Task<DailyCapacity?> FindByConfigurationKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken) => database.DailyCapacities.SingleOrDefaultAsync(
        item => item.LastConfigurationIdempotencyKey == idempotencyKey,
        cancellationToken);

    public async Task AddAsync(DailyCapacity capacity, CancellationToken cancellationToken) =>
        await database.DailyCapacities.AddAsync(capacity, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        database.SaveChangesAsync(cancellationToken);

    private IQueryable<Order> OrdersWithItems() => database.Orders.Include("_items");

    private static bool IsRetryableConcurrencyConflict(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        PostgresException postgresException =>
            postgresException.SqlState == PostgresErrorCodes.SerializationFailure
            || postgresException.SqlState == PostgresErrorCodes.DeadlockDetected
            || postgresException.SqlState == PostgresErrorCodes.UniqueViolation,
        _ when exception.InnerException is not null =>
            IsRetryableConcurrencyConflict(exception.InnerException),
        _ => false,
    };
}
