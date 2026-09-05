using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ts.Api.Application.Attendance;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Attendance;

namespace Ts.Api.Infrastructure.Persistence;

public sealed class AttendanceStore(AppDbContext database) : IAttendanceStore
{
    public async Task<IReadOnlyList<WhatsAppConversation>> GetConversationsAsync(CancellationToken token) => await database.WhatsAppConversations.OrderByDescending(x => x.LastMessageAt).ToListAsync(token);
    public async Task<IReadOnlyList<WhatsAppMessage>> GetMessagesAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => await database.WhatsAppMessages.Where(x => ids.Contains(x.ConversationId)).OrderBy(x => x.Sequence).ToListAsync(token);
    public async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => await database.Users.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, token);
    public Task<WhatsAppConversation?> FindConversationAsync(Guid id, CancellationToken token) => database.WhatsAppConversations.SingleOrDefaultAsync(x => x.Id == id, token);
    public Task<WhatsAppConversation?> FindConversationByPhoneAsync(string phoneId, string customerPhone, CancellationToken token) => database.WhatsAppConversations.SingleOrDefaultAsync(x => x.BusinessPhoneNumberId == phoneId && x.CustomerPhone == customerPhone, token);
    public Task<WhatsAppMessage?> FindMessageByExternalIdAsync(string externalId, CancellationToken token) => database.WhatsAppMessages.SingleOrDefaultAsync(x => x.ExternalId == externalId, token);
    public Task<WhatsAppMessage?> FindMessageByIdempotencyKeyAsync(string key, CancellationToken token) => database.WhatsAppMessages.SingleOrDefaultAsync(x => x.IdempotencyKey == key, token);
    public Task<WhatsAppMessage?> FindMessageAsync(Guid id, CancellationToken token) => database.WhatsAppMessages.SingleOrDefaultAsync(x => x.Id == id, token);
    public async Task<long> NextSequenceAsync(Guid id, CancellationToken token) => (await database.WhatsAppMessages.Where(x => x.ConversationId == id).MaxAsync(x => (long?)x.Sequence, token) ?? 0) + 1;
    public Task<WhatsAppQuotaPeriod?> FindQuotaAsync(string phoneId, DateOnly period, CancellationToken token) => database.WhatsAppQuotaPeriods.SingleOrDefaultAsync(x => x.BusinessPhoneNumberId == phoneId && x.PeriodStart == period, token);
    public async Task<(Guid? CustomerId, Guid? OrderId)> ResolveLinksAsync(string phone, CancellationToken token)
    {
        var customer = await database.Customers.SingleOrDefaultAsync(x => x.Phone == phone, token);
        if (customer is null) return (null, null);
        var orderId = await database.Orders.Where(x => x.CustomerId == customer.Id).OrderByDescending(x => x.OperationalDate).ThenByDescending(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(token);
        return (customer.Id, orderId);
    }
    public async Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken token)
    {
        if (!database.Database.IsRelational()) return await operation(token);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        try { var result = await operation(token); await transaction.CommitAsync(token); return result; }
        catch (Exception e) when (e is DbUpdateConcurrencyException or DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }) { await transaction.RollbackAsync(CancellationToken.None); throw new ConflictException("O atendimento conflitou com outro processamento. Tente novamente."); }
    }
    public void Add(WhatsAppConversation value) => database.Add(value);
    public void Add(WhatsAppMessage value) => database.Add(value);
    public void Add(WhatsAppQuotaPeriod value) => database.Add(value);
    public Task SaveChangesAsync(CancellationToken token) => database.SaveChangesAsync(token);
}
