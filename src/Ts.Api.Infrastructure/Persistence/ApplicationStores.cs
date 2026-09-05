using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ts.Api.Application.Catalog;
using Ts.Api.Application.Common;
using Ts.Api.Application.FrozenStock;
using Ts.Api.Application.Orders;
using Ts.Api.Application.Production;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Customers;
using Ts.Api.Domain.Finance;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Plans;
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

    public Task<ProducibleItem?> FindProducibleItemAsync(
        Guid id,
        CancellationToken cancellationToken) => database.ProducibleItems
        .SingleOrDefaultAsync(item => item.Id == id && item.IsActive, cancellationToken);

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

public sealed class FrozenStockManagementStore(AppDbContext database) : IFrozenStockManagementStore
{
    public async Task<IReadOnlyList<CatalogOffer>> GetActiveFrozenOffersAsync(
        CancellationToken cancellationToken) => await database.CatalogOffers
        .Where(item => item.IsActive && item.FulfillmentMode == OfferFulfillmentMode.FrozenStock)
        .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProducibleItem>> GetProduciblesAsync(
        CancellationToken cancellationToken) => await database.ProducibleItems
        .OrderBy(item => item.Name)
        .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FrozenConfiguration>> GetConfigurationsAsync(
        CancellationToken cancellationToken) => await database.FrozenConfigurations
        .OrderBy(item => item.Presentation)
        .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FrozenLot>> GetLotsAsync(
        CancellationToken cancellationToken) => await database.FrozenLots
        .Include("_movements")
        .AsSplitQuery()
        .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) => await database.Users
        .Where(item => userIds.Contains(item.Id))
        .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);

    public Task<FrozenConfiguration?> FindConfigurationAsync(
        Guid id,
        CancellationToken cancellationToken) => database.FrozenConfigurations
        .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<FrozenLot?> FindLotAsync(
        Guid id,
        CancellationToken cancellationToken) => database.FrozenLots
        .Include("_movements")
        .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<FrozenStockMovement?> FindMovementByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken) => database.FrozenStockMovements
        .SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 3;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            await using var transaction = await database.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                database.ChangeTracker.Clear();
                if (attempt == maximumAttempts)
                {
                    throw new ConflictException(
                        "A movimentação conflitou com outra operação. Recarregue o lote e tente novamente.");
                }
            }
        }

        throw new InvalidOperationException("O registro da movimentação terminou sem resultado.");
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var entry in database.ChangeTracker.Entries<FrozenStockMovement>()
                     .Where(entry => entry.State == EntityState.Modified
                         && entry.Entity.IdempotencyKey is not null))
        {
            entry.State = EntityState.Added;
        }

        return database.SaveChangesAsync(cancellationToken);
    }

    private static bool IsRetryable(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        DbUpdateException { InnerException: PostgresException postgres }
            when postgres.SqlState is PostgresErrorCodes.SerializationFailure
                or PostgresErrorCodes.DeadlockDetected
                or PostgresErrorCodes.UniqueViolation => true,
        PostgresException postgres when postgres.SqlState is PostgresErrorCodes.SerializationFailure
            or PostgresErrorCodes.DeadlockDetected => true,
        _ => false,
    };
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

    public Task<ProducibleComposition?> FindPublishedCompositionAsync(
        Guid producibleItemId,
        CancellationToken cancellationToken) => database.ProducibleCompositions
        .Include("_components")
        .Where(item => item.ProducibleItemId == producibleItemId)
        .OrderByDescending(item => item.Version)
        .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CustomerDietaryRestriction>> FindCustomerRestrictionsAsync(
        Guid customerId,
        CancellationToken cancellationToken) => await database.CustomerDietaryRestrictions
        .Where(item => item.CustomerId == customerId)
        .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PlanAcquisition>> FindEligiblePlanAcquisitionsAsync(
        Guid customerId,
        Guid offerId,
        CancellationToken cancellationToken) => await database.PlanAcquisitions
        .Include("_movements")
        .Where(item => item.CustomerId == customerId && item.EligibleOfferId == offerId)
        .OrderBy(item => item.AcquiredOn)
        .ThenBy(item => item.Id)
        .ToListAsync(cancellationToken);

    public async Task<decimal> GetFinancialCreditBalanceAsync(
        Guid customerId,
        CancellationToken cancellationToken) => (await database.FinancialCreditMovements
            .Where(item => item.CustomerId == customerId)
            .ToListAsync(cancellationToken))
        .Sum(item => item.SignedAmount);

    public void AddFinancialCreditMovement(FinancialCreditMovement movement) =>
        database.FinancialCreditMovements.Add(movement);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        MarkNewEffectsAsAdded<FrozenStockAllocation>();
        MarkNewEffectsAsAdded<FrozenStockMovement>();
        MarkNewEffectsAsAdded<OrderCharge>();
        MarkNewEffectsAsAdded<OrderItemComponent>();
        MarkNewEffectsAsAdded<OrderPlanCreditAllocation>();
        MarkNewEffectsAsAdded<OrderConfirmationAudit>();
        MarkNewEffectsAsAdded<PlanCreditMovement>();
        return database.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Order> OrdersWithConfirmationGraph() =>
        database.Orders
            .Include("_items")
            .Include("_frozenAllocations")
            .Include("_charges")
            .Include("_componentSnapshots")
            .Include("_planCreditAllocations")
            .Include("_confirmationAudits")
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

public sealed class OrderLifecycleStore(AppDbContext database) : IOrderLifecycleStore
{
    public async Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 3;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            await using var transaction = await database.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
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
                    throw new ConflictException("A operação de ciclo do pedido conflitou com outra gravação. Tente novamente.");
            }
        }

        throw new InvalidOperationException("A operação de ciclo do pedido terminou sem resultado.");
    }

    public Task<OrderLifecycleEvent?> FindLifecycleEventAsync(
        string idempotencyKey, CancellationToken cancellationToken) =>
        database.OrderLifecycleEvents.SingleOrDefaultAsync(
            item => item.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<Order?> FindOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        database.Orders
            .Include("_items")
            .Include("_frozenAllocations")
            .Include("_charges")
            .Include("_planCreditAllocations")
            .Include("_confirmationAudits")
            .Include("_lifecycleEvents")
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);

    public Task<DailyCapacity?> FindDailyCapacityAsync(
        DateOnly operationalDate, CancellationToken cancellationToken) =>
        database.DailyCapacities.SingleOrDefaultAsync(
            item => item.OperationalDate == operationalDate, cancellationToken);

    public Task<FrozenLot?> FindFrozenLotAsync(
        Guid frozenLotId, CancellationToken cancellationToken) =>
        database.FrozenLots.Include("_movements")
            .SingleOrDefaultAsync(item => item.Id == frozenLotId, cancellationToken);

    public Task<PlanAcquisition?> FindPlanAcquisitionAsync(
        Guid acquisitionId, CancellationToken cancellationToken) =>
        database.PlanAcquisitions.Include("_movements")
            .SingleOrDefaultAsync(item => item.Id == acquisitionId, cancellationToken);

    public void AddFinancialCreditMovement(FinancialCreditMovement movement) =>
        database.FinancialCreditMovements.Add(movement);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        MarkNewEffectsAsAdded<FrozenStockMovement>();
        MarkNewEffectsAsAdded<PlanCreditMovement>();
        MarkNewEffectsAsAdded<OrderLifecycleEvent>();
        return database.SaveChangesAsync(cancellationToken);
    }

    private void MarkNewEffectsAsAdded<TEntity>() where TEntity : class
    {
        foreach (var entry in database.ChangeTracker.Entries<TEntity>()
                     .Where(entry => entry.State == EntityState.Modified))
            entry.State = EntityState.Added;
    }

    private static bool IsRetryableConcurrencyConflict(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        PostgresException postgresException =>
            postgresException.SqlState == PostgresErrorCodes.SerializationFailure
            || postgresException.SqlState == PostgresErrorCodes.DeadlockDetected
            || postgresException.SqlState == PostgresErrorCodes.UniqueViolation,
        _ when exception.InnerException is not null => IsRetryableConcurrencyConflict(exception.InnerException),
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

public sealed class OrderQueryStore(AppDbContext database) : IOrderQueryStore
{
    public async Task<IReadOnlyList<Order>> GetOrdersAsync(CancellationToken cancellationToken) =>
        await database.Orders.Include("_items")
            .OrderByDescending(item => item.OperationalDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

    public Task<Order?> FindOrderDetailsAsync(Guid orderId, CancellationToken cancellationToken) =>
        database.Orders
            .Include("_items")
            .Include("_frozenAllocations")
            .Include("_componentSnapshots")
            .Include("_planCreditAllocations")
            .Include("_confirmationAudits")
            .Include("_lifecycleEvents")
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);

    public async Task<IReadOnlyList<CatalogOffer>> GetActiveOffersAsync(CancellationToken cancellationToken) =>
        await database.CatalogOffers.Where(item => item.IsActive).OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProducibleItem>> GetActiveProduciblesAsync(CancellationToken cancellationToken) =>
        await database.ProducibleItems.Where(item => item.IsActive).OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FrozenConfiguration>> GetActiveFrozenConfigurationsAsync(CancellationToken cancellationToken) =>
        await database.FrozenConfigurations.Where(item => item.IsActive).OrderBy(item => item.Presentation).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FrozenLot>> GetSellableFrozenLotsAsync(
        DateOnly sellableOn,
        CancellationToken cancellationToken) => await database.FrozenLots
        .Include("_movements")
        .Where(item => item.ExpiresOn >= sellableOn)
        .OrderBy(item => item.ExpiresOn)
        .ThenBy(item => item.ManufacturedOn)
        .ThenBy(item => item.Id)
        .ToListAsync(cancellationToken);
}

public sealed class OrderConfirmationSetupStore(AppDbContext database) : IOrderConfirmationSetupStore
{
    public Task<ProducibleItem?> FindProducibleItemAsync(Guid id, CancellationToken cancellationToken) =>
        database.ProducibleItems.SingleOrDefaultAsync(item => item.Id == id && item.IsActive, cancellationToken);

    public Task<CatalogOffer?> FindOfferAsync(Guid id, CancellationToken cancellationToken) =>
        database.CatalogOffers.SingleOrDefaultAsync(item => item.Id == id && item.IsActive, cancellationToken);

    public async Task<int> GetNextCompositionVersionAsync(Guid producibleItemId, CancellationToken cancellationToken) =>
        (await database.ProducibleCompositions
            .Where(item => item.ProducibleItemId == producibleItemId)
            .MaxAsync(item => (int?)item.Version, cancellationToken) ?? 0) + 1;

    public Task<bool> RestrictionExistsAsync(Guid customerId, string marker, CancellationToken cancellationToken) =>
        database.CustomerDietaryRestrictions.AnyAsync(
            item => item.CustomerId == customerId && item.Marker == marker, cancellationToken);

    public void Add(ProducibleComposition composition) => database.ProducibleCompositions.Add(composition);
    public void Add(CustomerDietaryRestriction restriction) => database.CustomerDietaryRestrictions.Add(restriction);
    public void Add(PlanAcquisition acquisition) => database.PlanAcquisitions.Add(acquisition);
    public void Add(FinancialCreditMovement movement) => database.FinancialCreditMovements.Add(movement);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => database.SaveChangesAsync(cancellationToken);
}
