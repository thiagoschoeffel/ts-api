using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ts.Api.Application.Common;
using Ts.Api.Application.Logistics;
using Ts.Api.Domain.Customers;
using Ts.Api.Domain.Logistics;
using Ts.Api.Domain.Operations;
using Ts.Api.Domain.Orders;

namespace Ts.Api.Infrastructure.Persistence;

public sealed class LogisticsStore(AppDbContext database) : ILogisticsStore
{
    public async Task<IReadOnlyList<DeliveryDriver>> GetDriversAsync(CancellationToken token) => await database.DeliveryDrivers.OrderBy(x => x.Name).ToListAsync(token);
    public Task<DeliveryDriver?> FindDriverAsync(Guid id, CancellationToken token) => database.DeliveryDrivers.SingleOrDefaultAsync(x => x.Id == id, token);
    public Task<bool> DriverIdentificationExistsAsync(string identification, Guid? exceptId, CancellationToken token) => database.DeliveryDrivers.AnyAsync(x => x.Identification == identification && x.Id != exceptId, token);
    public async Task<IReadOnlyList<Order>> GetDeliveryOrdersAsync(CancellationToken token) => await database.Orders.Include("_lifecycleEvents").Where(x => x.Status == OrderStatus.InPacking || x.Status == OrderStatus.InDelivery || x.Status == OrderStatus.DeliveryFailed || x.Status == OrderStatus.Completed).ToListAsync(token);
    public Task<Order?> FindOrderAsync(Guid id, CancellationToken token) => database.Orders.Include("_lifecycleEvents").SingleOrDefaultAsync(x => x.Id == id, token);
    public async Task<IReadOnlyList<Customer>> GetCustomersAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => await database.Customers.Where(x => ids.Contains(x.Id)).ToListAsync(token);
    public async Task<IReadOnlyList<CustomerAddress>> GetAddressesAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => await database.CustomerAddresses.Where(x => ids.Contains(x.CustomerId)).OrderBy(x => x.Id).ToListAsync(token);
    public async Task<IReadOnlyList<PackingRecord>> GetPackingsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken token) => await database.PackingRecords.Where(x => orderIds.Contains(x.OrderId)).ToListAsync(token);
    public async Task<IReadOnlyList<DeliveryRoute>> GetRoutesAsync(CancellationToken token) => await database.DeliveryRoutes.Include("_stops").OrderByDescending(x => x.CreatedAt).ToListAsync(token);
    public Task<DeliveryRoute?> FindRouteAsync(Guid id, CancellationToken token) => database.DeliveryRoutes.Include("_stops").SingleOrDefaultAsync(x => x.Id == id, token);
    public async Task<IReadOnlyList<DeliveryAttempt>> GetAttemptsAsync(IReadOnlyCollection<Guid> stopIds, CancellationToken token) => await database.DeliveryAttempts.Where(x => stopIds.Contains(x.RouteStopId)).OrderBy(x => x.OccurredAt).ToListAsync(token);
    public Task<DeliveryAttempt?> FindAttemptByKeyAsync(string key, CancellationToken token) => database.DeliveryAttempts.SingleOrDefaultAsync(x => x.IdempotencyKey == key, token);
    public async Task<IReadOnlyList<DeliveryReschedule>> GetReschedulesAsync(CancellationToken token) => await database.DeliveryReschedules.OrderByDescending(x => x.OccurredAt).ToListAsync(token);
    public Task<DeliveryReschedule?> FindRescheduleByKeyAsync(string key, CancellationToken token) => database.DeliveryReschedules.SingleOrDefaultAsync(x => x.IdempotencyKey == key, token);
    public void Add(object entity) => database.Add(entity);
    public void ReplaceStops(DeliveryRoute route) => database.DeliveryRouteStops.RemoveRange(route.Stops);
    public async Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken token)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        try { var result = await operation(token); await transaction.CommitAsync(token); return result; }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException or DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } })
        { await transaction.RollbackAsync(CancellationToken.None); throw new ConflictException("A logística conflitou com outra operação. Recarregue os dados e tente novamente."); }
    }
    public Task SaveChangesAsync(CancellationToken token)
    {
        foreach (var entry in database.ChangeTracker.Entries<OrderLifecycleEvent>().Where(x => x.State == EntityState.Modified)) entry.State = EntityState.Added;
        return database.SaveChangesAsync(token);
    }
}
