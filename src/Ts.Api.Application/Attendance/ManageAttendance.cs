using Ts.Api.Application.Common;
using Ts.Api.Application.Organizations;
using Ts.Api.Domain.Attendance;

namespace Ts.Api.Application.Attendance;

public sealed record AttendanceMessageResult(Guid Id, string ExternalId, MessageDirection Direction, MessageOrigin Origin,
    string Content, DateTimeOffset PlatformTimestamp, DateTimeOffset ReceivedTimestamp, MessageProcessingStatus ProcessingStatus,
    MessageDeliveryStatus DeliveryStatus, string? FailureReason);
public sealed record AttendanceConversationResult(Guid Id, Guid? CustomerId, string CustomerName, string Phone,
    AttendanceMode Mode, string? AssignedTo, int UnreadCount, DateTimeOffset LastMessageAt, Guid? OrderId, long Version,
    IReadOnlyCollection<AttendanceMessageResult> Messages);
public sealed record WhatsAppQuotaResult(string BusinessPhoneNumber, DateOnly PeriodStart, int FreeServiceMessageLimit,
    int AutomationPauseAt, int DeliveredServiceMessages, int ReservedServiceMessages, DateOnly RenewsAt, string Status);
public sealed record AttendanceSnapshotResult(IReadOnlyCollection<AttendanceConversationResult> Conversations, WhatsAppQuotaResult Quota);
public sealed record WhatsAppInboundEvent(string PhoneNumberId, string BusinessPhoneNumber, string ExternalId,
    string CustomerPhone, string? CustomerName, string Content, DateTimeOffset PlatformTimestamp, bool SentByBusinessApp = false);
public sealed record WhatsAppStatusEvent(string PhoneNumberId, string ExternalId, string Status, string? FailureReason);
public sealed record WhatsAppSendResult(string ExternalId, DateTimeOffset AcceptedAt);
public enum WhatsAppSendOutcome { Rejected, Unknown }
public sealed class WhatsAppProviderException(
    WhatsAppSendOutcome outcome,
    string message,
    int? httpStatus = null,
    int? providerCode = null,
    int? providerSubcode = null,
    string? traceId = null,
    bool? isTransient = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public WhatsAppSendOutcome Outcome { get; } = outcome;
    public int? HttpStatus { get; } = httpStatus;
    public int? ProviderCode { get; } = providerCode;
    public int? ProviderSubcode { get; } = providerSubcode;
    public string? TraceId { get; } = traceId;
    public bool? IsTransient { get; } = isTransient;
}

public interface IAttendanceStore
{
    Task<IReadOnlyList<WhatsAppConversation>> GetConversationsAsync(CancellationToken token);
    Task<IReadOnlyList<WhatsAppMessage>> GetMessagesAsync(IReadOnlyCollection<Guid> conversationIds, CancellationToken token);
    Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken token);
    Task<WhatsAppConversation?> FindConversationAsync(Guid id, CancellationToken token);
    Task<WhatsAppConversation?> FindConversationByPhoneAsync(string phoneNumberId, string customerPhone, CancellationToken token);
    Task<WhatsAppMessage?> FindMessageByExternalIdAsync(string externalId, CancellationToken token);
    Task<WhatsAppMessage?> FindMessageByIdempotencyKeyAsync(string key, CancellationToken token);
    Task<WhatsAppMessage?> FindMessageAsync(Guid id, CancellationToken token);
    Task<long> NextSequenceAsync(Guid conversationId, CancellationToken token);
    Task<WhatsAppQuotaPeriod?> FindQuotaAsync(string phoneNumberId, DateOnly period, CancellationToken token);
    Task<(Guid? CustomerId, Guid? OrderId)> ResolveLinksAsync(string customerPhone, CancellationToken token);
    Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken token);
    void Add(WhatsAppConversation conversation);
    void Add(WhatsAppMessage message);
    void Add(WhatsAppQuotaPeriod quota);
    Task SaveChangesAsync(CancellationToken token);
}

public interface IWhatsAppCloudClient
{
    Task<WhatsAppSendResult> SendTextAsync(string phoneNumberId, string customerPhone, string text,
        string accessToken, CancellationToken token);
}

public sealed class AttendanceService(IAttendanceStore store, IWhatsAppCloudClient cloud, IOrganizationContext organization,
    ICurrentUserContext currentUser, IWhatsAppConnectionResolver connections, TimeProvider timeProvider)
{
    public async Task<AttendanceSnapshotResult> GetAsync(CancellationToken token)
    {
        var runtime = await connections.GetCurrentAsync(token);
        var conversations = await store.GetConversationsAsync(token);
        var messages = await store.GetMessagesAsync(conversations.Select(x => x.Id).ToArray(), token);
        var names = await store.GetUserNamesAsync(conversations.Where(x => x.AssignedTo.HasValue).Select(x => x.AssignedTo!.Value).Distinct().ToArray(), token);
        var phoneId = conversations.FirstOrDefault()?.BusinessPhoneNumberId ?? runtime.PhoneNumberId;
        var phone = conversations.FirstOrDefault()?.BusinessPhoneNumber ?? runtime.BusinessPhoneNumber;
        var quota = await GetOrCreateQuota(phoneId, phone, runtime, token);
        return new(conversations.Select(c => Map(c, messages.Where(m => m.ConversationId == c.Id).ToArray(), c.AssignedTo is Guid id && names.TryGetValue(id, out var name) ? name : null)).ToArray(), Map(quota));
    }

    public async Task<AttendanceConversationResult> ChangeModeAsync(Guid id, AttendanceMode mode, long expectedVersion, CancellationToken token)
    {
        var conversation = await store.FindConversationAsync(id, token) ?? throw new ResourceNotFoundException("Conversa não encontrada.");
        conversation.ChangeMode(mode, currentUser.UserId, expectedVersion);
        await store.SaveChangesAsync(token);
        return await Result(conversation, token);
    }

    public async Task<AttendanceConversationResult> SendAsync(Guid id, string content, string idempotencyKey, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(idempotencyKey)) throw new ConflictException("Conteúdo e chave de idempotência são obrigatórios.");
        var existing = await store.FindMessageByIdempotencyKeyAsync(idempotencyKey, token);
        if (existing is not null) return await Result(await store.FindConversationAsync(existing.ConversationId, token) ?? throw new ResourceNotFoundException("Conversa não encontrada."), token);
        var conversation = await store.FindConversationAsync(id, token) ?? throw new ResourceNotFoundException("Conversa não encontrada.");
        if (conversation.Mode != AttendanceMode.Human) throw new ConflictException("Assuma o atendimento antes de enviar uma mensagem manual.");
        var runtime = await connections.GetCurrentAsync(token);
        if (!string.Equals(conversation.BusinessPhoneNumberId, runtime.PhoneNumberId, StringComparison.Ordinal))
            throw new ConflictException("A conversa pertence a outro ativo WhatsApp e não pode enviar pela conexão atual.");
        var quota = await GetOrCreateQuota(conversation.BusinessPhoneNumberId, conversation.BusinessPhoneNumber, runtime, token);
        var message = await store.ExecuteSerializableAsync(async ct =>
        {
            quota.Reserve(false);
            var reserved = WhatsAppMessage.ReserveOutbound(organization.OrganizationId, id, idempotencyKey,
                await store.NextSequenceAsync(id, ct), content.Trim(), timeProvider.GetUtcNow(), MessageOrigin.Operator);
            store.Add(reserved); await store.SaveChangesAsync(ct); return reserved;
        }, token);
        WhatsAppSendResult sent;
        try { sent = await cloud.SendTextAsync(conversation.BusinessPhoneNumberId, conversation.CustomerPhone,
            content.Trim(), runtime.AccessToken, token); }
        catch (WhatsAppProviderException exception) when (exception.Outcome == WhatsAppSendOutcome.Rejected)
        { message.MarkFailed(exception.Message); quota.Release(); await store.SaveChangesAsync(CancellationToken.None); throw; }
        catch (Exception exception)
        { message.MarkOutcomeUnknown(UnknownOutcomeReason(exception)); await store.SaveChangesAsync(CancellationToken.None); throw; }
        message.MarkSent(sent.ExternalId, sent.AcceptedAt); conversation.RecordOutbound(sent.AcceptedAt);
        await store.SaveChangesAsync(token);
        return await Result(conversation, token);
    }

    public async Task<AttendanceConversationResult> RetryAsync(Guid conversationId, Guid messageId, CancellationToken token)
    {
        var message = await store.FindMessageAsync(messageId, token) ?? throw new ResourceNotFoundException("Mensagem não encontrada.");
        if (message.ConversationId != conversationId || message.ProcessingStatus != MessageProcessingStatus.Failed) throw new ConflictException("Somente mensagens com falha podem ser repetidas.");
        var conversation = await store.FindConversationAsync(conversationId, token) ?? throw new ResourceNotFoundException("Conversa não encontrada.");
        var runtime = await connections.GetCurrentAsync(token);
        if (!string.Equals(conversation.BusinessPhoneNumberId, runtime.PhoneNumberId, StringComparison.Ordinal))
            throw new ConflictException("A conversa pertence a outro ativo WhatsApp e não pode enviar pela conexão atual.");
        var quota = await GetOrCreateQuota(conversation.BusinessPhoneNumberId, conversation.BusinessPhoneNumber, runtime, token);
        quota.Reserve(message.Origin == MessageOrigin.Automation); message.MarkProcessing(); await store.SaveChangesAsync(token);
        WhatsAppSendResult sent;
        try { sent = await cloud.SendTextAsync(conversation.BusinessPhoneNumberId, conversation.CustomerPhone,
            message.Content, runtime.AccessToken, token); }
        catch (WhatsAppProviderException exception) when (exception.Outcome == WhatsAppSendOutcome.Rejected)
        { message.MarkFailed(exception.Message); quota.Release(); await store.SaveChangesAsync(CancellationToken.None); throw; }
        catch (Exception exception)
        { message.MarkOutcomeUnknown(UnknownOutcomeReason(exception)); await store.SaveChangesAsync(CancellationToken.None); throw; }
        message.MarkSent(sent.ExternalId, sent.AcceptedAt); conversation.RecordOutbound(sent.AcceptedAt);
        await store.SaveChangesAsync(token);
        return await Result(conversation, token);
    }

    public async Task<bool> ReceiveAsync(WhatsAppInboundEvent input, CancellationToken token)
    {
        Guid conversationId = Guid.Empty;
        var inserted = await store.ExecuteSerializableAsync(async ct =>
        {
            if (await store.FindMessageByExternalIdAsync(input.ExternalId, ct) is not null) return false;
            var conversation = await store.FindConversationByPhoneAsync(input.PhoneNumberId, input.CustomerPhone, ct);
            if (conversation is null) { conversation = WhatsAppConversation.Create(organization.OrganizationId, input.PhoneNumberId, input.BusinessPhoneNumber, input.CustomerPhone, input.CustomerName, input.PlatformTimestamp); var links = await store.ResolveLinksAsync(input.CustomerPhone, ct); conversation.Link(links.CustomerId, links.OrderId); store.Add(conversation); }
            else if (input.SentByBusinessApp) conversation.DetectHumanReply(); else conversation.Receive(input.PlatformTimestamp);
            conversationId = conversation.Id;
            store.Add(WhatsAppMessage.Receive(organization.OrganizationId, conversation.Id, input.ExternalId,
                await store.NextSequenceAsync(conversation.Id, ct), input.Content, input.PlatformTimestamp, timeProvider.GetUtcNow(), input.SentByBusinessApp ? MessageOrigin.Operator : MessageOrigin.Customer));
            await store.SaveChangesAsync(ct); return true;
        }, token);
        if (inserted) await ProcessConversationAsync(conversationId, token);
        return inserted;
    }

    public async Task ApplyStatusAsync(WhatsAppStatusEvent input, CancellationToken token)
    {
        var message = await store.FindMessageByExternalIdAsync(input.ExternalId, token); if (message is null) return;
        var conversation = await store.FindConversationAsync(message.ConversationId, token); if (conversation is null) return;
        var runtime = await connections.GetCurrentAsync(token);
        var quota = await GetOrCreateQuota(input.PhoneNumberId, conversation.BusinessPhoneNumber, runtime, token);
        if (input.Status.Equals("delivered", StringComparison.OrdinalIgnoreCase) && message.DeliveryStatus != MessageDeliveryStatus.Delivered) { message.MarkDelivered(); quota.Deliver(); }
        else if (input.Status.Equals("failed", StringComparison.OrdinalIgnoreCase) && message.DeliveryStatus is MessageDeliveryStatus.Reserved or MessageDeliveryStatus.Sent) { message.MarkFailed(input.FailureReason ?? "Falha informada pelo provedor."); quota.Release(); }
        if (quota.AutomationBlocked) foreach (var automated in (await store.GetConversationsAsync(token)).Where(x => x.Mode == AttendanceMode.Automated)) automated.DetectHumanReply();
        await store.SaveChangesAsync(token);
    }

    public async Task ProcessNextAsync(Guid messageId, CancellationToken token)
    {
        var message = await store.FindMessageAsync(messageId, token); if (message is null || message.ProcessingStatus != MessageProcessingStatus.Received) return;
        message.MarkProcessing(); await store.SaveChangesAsync(token); message.MarkProcessed(); await store.SaveChangesAsync(token);
    }

    private async Task ProcessConversationAsync(Guid conversationId, CancellationToken token) => await store.ExecuteSerializableAsync(async ct =>
    {
        foreach (var message in (await store.GetMessagesAsync([conversationId], ct)).Where(x => x.ProcessingStatus == MessageProcessingStatus.Received).OrderBy(x => x.Sequence))
        { message.MarkProcessing(); await store.SaveChangesAsync(ct); message.MarkProcessed(); await store.SaveChangesAsync(ct); }
        return true;
    }, token);

    private async Task<WhatsAppQuotaPeriod> GetOrCreateQuota(string phoneId, string phone,
        WhatsAppRuntimeConnection runtime, CancellationToken token)
    {
        var now = timeProvider.GetUtcNow(); var period = new DateOnly(now.Year, now.Month, 1);
        var quota = await store.FindQuotaAsync(phoneId, period, token);
        if (quota is null) { quota = WhatsAppQuotaPeriod.Create(organization.OrganizationId, phoneId, phone, period,
            runtime.FreeServiceMessageLimit, runtime.AutomationPauseAt); store.Add(quota); await store.SaveChangesAsync(token); }
        return quota;
    }
    private static string UnknownOutcomeReason(Exception exception) => exception is WhatsAppProviderException provider
        ? provider.Message
        : "O provedor não confirmou se a mensagem foi aceita. A reserva foi mantida para reconciliação.";
    private async Task<AttendanceConversationResult> Result(WhatsAppConversation c, CancellationToken token) => Map(c, await store.GetMessagesAsync([c.Id], token), null);
    private static AttendanceConversationResult Map(WhatsAppConversation c, IReadOnlyCollection<WhatsAppMessage> messages, string? assigned) => new(c.Id, c.CustomerId, c.CustomerName, c.CustomerPhone, c.Mode, assigned, c.UnreadCount, c.LastMessageAt, c.OrderId, c.Version, messages.OrderBy(x => x.Sequence).Select(m => new AttendanceMessageResult(m.Id, m.ExternalId, m.Direction, m.Origin, m.Content, m.PlatformTimestamp, m.ReceivedTimestamp, m.ProcessingStatus, m.DeliveryStatus, m.FailureReason)).ToArray());
    private static WhatsAppQuotaResult Map(WhatsAppQuotaPeriod q) { var used = q.Delivered + q.Reserved; var status = used >= q.FreeLimit ? "automation-blocked" : used >= q.AutomationPauseAt ? "automation-blocked" : used >= q.FreeLimit * .9 ? "critical" : used >= q.FreeLimit * .75 ? "alert" : used >= q.FreeLimit * .5 ? "attention" : "normal"; return new(q.BusinessPhoneNumber, q.PeriodStart, q.FreeLimit, q.AutomationPauseAt, q.Delivered, q.Reserved, q.PeriodStart.AddMonths(1), status); }
}
