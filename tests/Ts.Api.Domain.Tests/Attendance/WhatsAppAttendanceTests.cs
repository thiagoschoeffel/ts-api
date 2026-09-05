using Ts.Api.Domain.Attendance;
using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Tests.Attendance;

public sealed class WhatsAppAttendanceTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    [Fact]
    public void Quota_ReservesAtomicallyAndProtectsAutomationMargin()
    {
        var quota = WhatsAppQuotaPeriod.Create(OrganizationId, "phone-1", "+551140002026", new DateOnly(2026, 9, 1), 3, 2);
        quota.Reserve(true);
        quota.Deliver();
        quota.Reserve(true);

        var error = Assert.Throws<DomainException>(() => quota.Reserve(true));
        Assert.Contains("automação", error.Message);
        quota.Deliver(); quota.Reserve(false);
        Assert.Throws<DomainException>(() => quota.Reserve(false));
    }

    [Fact]
    public void HumanReplyPausesAutomationAndOptimisticVersionRejectsStaleChange()
    {
        var conversation = WhatsAppConversation.Create(OrganizationId, "phone-1", "+551140002026", "5511999999999", "Maria", DateTimeOffset.UtcNow);
        var initialVersion = conversation.Version;
        conversation.DetectHumanReply();

        Assert.Equal(AttendanceMode.Human, conversation.Mode);
        Assert.Throws<DomainException>(() => conversation.ChangeMode(AttendanceMode.Automated, Guid.NewGuid(), initialVersion));
    }

    [Fact]
    public void DeliveryReconciliationMovesReservationExactlyOnceAtAggregateBoundary()
    {
        var quota = WhatsAppQuotaPeriod.Create(OrganizationId, "phone-1", "+551140002026", new DateOnly(2026, 9, 1), 1000, 970);
        var message = WhatsAppMessage.ReserveOutbound(OrganizationId, Guid.NewGuid(), "send-1", 1, "Olá", DateTimeOffset.UtcNow, MessageOrigin.Operator);
        quota.Reserve(false); message.MarkSent("wamid.1", DateTimeOffset.UtcNow); message.MarkDelivered(); quota.Deliver();

        Assert.Equal(MessageDeliveryStatus.Delivered, message.DeliveryStatus);
        Assert.Equal(1, quota.Delivered);
        Assert.Equal(0, quota.Reserved);
    }
}
