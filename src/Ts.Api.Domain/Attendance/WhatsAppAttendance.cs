using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Attendance;

public enum AttendanceMode { Automated, Human, Closed }
public enum MessageDirection { Inbound, Outbound }
public enum MessageOrigin { Customer, Automation, Operator }
public enum MessageProcessingStatus { Received, Processing, Processed, Failed, Ignored }
public enum MessageDeliveryStatus { NotApplicable, Reserved, Sent, Delivered, Failed }

public sealed class WhatsAppConversation : ITenantOwned
{
    private WhatsAppConversation() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string BusinessPhoneNumberId { get; private set; } = string.Empty;
    public string BusinessPhoneNumber { get; private set; } = string.Empty;
    public string CustomerPhone { get; private set; } = string.Empty;
    public string CustomerName { get; private set; } = string.Empty;
    public Guid? CustomerId { get; private set; }
    public Guid? OrderId { get; private set; }
    public AttendanceMode Mode { get; private set; }
    public Guid? AssignedTo { get; private set; }
    public int UnreadCount { get; private set; }
    public DateTimeOffset LastMessageAt { get; private set; }
    public long Version { get; private set; }

    public static WhatsAppConversation Create(Guid organizationId, string phoneNumberId, string businessPhone,
        string customerPhone, string? customerName, DateTimeOffset at)
    {
        if (organizationId == Guid.Empty || string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(customerPhone))
            throw new DomainException("Os dados da conversa do WhatsApp são inválidos.");
        return new() { Id = Guid.NewGuid(), OrganizationId = organizationId, BusinessPhoneNumberId = phoneNumberId.Trim(),
            BusinessPhoneNumber = businessPhone.Trim(), CustomerPhone = customerPhone.Trim(), CustomerName = string.IsNullOrWhiteSpace(customerName) ? customerPhone.Trim() : customerName.Trim(),
            Mode = AttendanceMode.Automated, LastMessageAt = at, Version = 1 };
    }

    public void Receive(DateTimeOffset at) { UnreadCount++; LastMessageAt = at; Version++; }
    public void RecordOutbound(DateTimeOffset at) { LastMessageAt = at; Version++; }
    public void Read() { UnreadCount = 0; Version++; }
    public void ChangeMode(AttendanceMode mode, Guid actorId, long expectedVersion)
    {
        if (Version != expectedVersion) throw new DomainException("A conversa foi alterada por outro operador.");
        Mode = mode; AssignedTo = mode == AttendanceMode.Human ? actorId : null; Version++;
    }
    public void DetectHumanReply(Guid? actorId = null) { Mode = AttendanceMode.Human; AssignedTo = actorId; Version++; }
    public void Link(Guid? customerId, Guid? orderId) { CustomerId = customerId; OrderId = orderId; Version++; }
}

public sealed class WhatsAppMessage : ITenantOwned
{
    private WhatsAppMessage() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ConversationId { get; private set; }
    public string ExternalId { get; private set; } = string.Empty;
    public string? IdempotencyKey { get; private set; }
    public long Sequence { get; private set; }
    public MessageDirection Direction { get; private set; }
    public MessageOrigin Origin { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset PlatformTimestamp { get; private set; }
    public DateTimeOffset ReceivedTimestamp { get; private set; }
    public MessageProcessingStatus ProcessingStatus { get; private set; }
    public MessageDeliveryStatus DeliveryStatus { get; private set; }
    public string? FailureReason { get; private set; }

    public static WhatsAppMessage Receive(Guid organizationId, Guid conversationId, string externalId, long sequence,
        string content, DateTimeOffset platformAt, DateTimeOffset receivedAt, MessageOrigin origin = MessageOrigin.Customer) =>
        new() { Id = Guid.NewGuid(), OrganizationId = organizationId, ConversationId = conversationId, ExternalId = externalId.Trim(), Sequence = sequence,
            Direction = MessageDirection.Inbound, Origin = origin, Content = content, PlatformTimestamp = platformAt, ReceivedTimestamp = receivedAt,
            ProcessingStatus = MessageProcessingStatus.Received, DeliveryStatus = MessageDeliveryStatus.NotApplicable };

    public static WhatsAppMessage ReserveOutbound(Guid organizationId, Guid conversationId, string idempotencyKey,
        long sequence, string content, DateTimeOffset at, MessageOrigin origin) => new()
        { Id = Guid.NewGuid(), OrganizationId = organizationId, ConversationId = conversationId, ExternalId = $"pending:{Guid.NewGuid():N}", IdempotencyKey = idempotencyKey,
            Sequence = sequence, Direction = MessageDirection.Outbound, Origin = origin, Content = content, PlatformTimestamp = at,
            ReceivedTimestamp = at, ProcessingStatus = MessageProcessingStatus.Processing, DeliveryStatus = MessageDeliveryStatus.Reserved };

    public void MarkProcessing() { if (ProcessingStatus == MessageProcessingStatus.Received || ProcessingStatus == MessageProcessingStatus.Failed) ProcessingStatus = MessageProcessingStatus.Processing; }
    public void MarkProcessed() { ProcessingStatus = MessageProcessingStatus.Processed; FailureReason = null; }
    public void MarkSent(string externalId, DateTimeOffset at) { ExternalId = externalId; PlatformTimestamp = at; ProcessingStatus = MessageProcessingStatus.Processed; DeliveryStatus = MessageDeliveryStatus.Sent; FailureReason = null; }
    public void MarkDelivered() { DeliveryStatus = MessageDeliveryStatus.Delivered; }
    public void MarkFailed(string reason) { ProcessingStatus = MessageProcessingStatus.Failed; DeliveryStatus = MessageDeliveryStatus.Failed; FailureReason = reason; }
}

public sealed class WhatsAppQuotaPeriod : ITenantOwned
{
    private WhatsAppQuotaPeriod() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string BusinessPhoneNumberId { get; private set; } = string.Empty;
    public string BusinessPhoneNumber { get; private set; } = string.Empty;
    public DateOnly PeriodStart { get; private set; }
    public int FreeLimit { get; private set; }
    public int AutomationPauseAt { get; private set; }
    public int Delivered { get; private set; }
    public int Reserved { get; private set; }
    public bool PaidMessagesEnabled { get; private set; }
    public long Version { get; private set; }

    public static WhatsAppQuotaPeriod Create(Guid organizationId, string phoneNumberId, string phone, DateOnly periodStart, int freeLimit, int pauseAt) =>
        new() { Id = Guid.NewGuid(), OrganizationId = organizationId, BusinessPhoneNumberId = phoneNumberId, BusinessPhoneNumber = phone,
            PeriodStart = new DateOnly(periodStart.Year, periodStart.Month, 1), FreeLimit = freeLimit, AutomationPauseAt = pauseAt, Version = 1 };
    public bool AutomationBlocked => Delivered + Reserved >= AutomationPauseAt;
    public void Reserve(bool automated)
    {
        if ((!PaidMessagesEnabled && Delivered + Reserved >= FreeLimit) || automated && AutomationBlocked)
            throw new DomainException(automated ? "A automação foi pausada pela margem da franquia do WhatsApp." : "A franquia gratuita do WhatsApp foi esgotada.");
        Reserved++; Version++;
    }
    public void Deliver() { if (Reserved > 0) Reserved--; Delivered++; Version++; }
    public void Release() { if (Reserved > 0) Reserved--; Version++; }
}
