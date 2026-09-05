using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Attendance;
using Ts.Api.Application.Common;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Domain.Tests.Application;

public sealed class AttendanceServiceTests
{
    [Fact]
    public async Task DuplicateWebhookIsIdempotentAndConversationSequenceIsStable()
    {
        var tenant = new RequestContext();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var database = new AppDbContext(options, tenant);
        var store = new AttendanceStore(database);
        var service = new AttendanceService(store, new CloudFake(), tenant, tenant, new ConfigurationFake(), TimeProvider.System);
        var first = new WhatsAppInboundEvent("phone-1", "+551140002026", "wamid.1", "5511999999999", "Maria", "Primeira", DateTimeOffset.UtcNow);

        Assert.True(await service.ReceiveAsync(first, CancellationToken.None));
        Assert.False(await service.ReceiveAsync(first, CancellationToken.None));
        Assert.True(await service.ReceiveAsync(first with { ExternalId = "wamid.2", Content = "Segunda" }, CancellationToken.None));

        var snapshot = await service.GetAsync(CancellationToken.None);
        var conversation = Assert.Single(snapshot.Conversations);
        Assert.Equal(2, conversation.Messages.Count);
        Assert.Equal(["wamid.1", "wamid.2"], conversation.Messages.Select(x => x.ExternalId));
        Assert.All(conversation.Messages, message => Assert.Equal(Ts.Api.Domain.Attendance.MessageProcessingStatus.Processed, message.ProcessingStatus));
    }

    private sealed class RequestContext : IOrganizationContext, ICurrentUserContext
    {
        public bool IsAvailable => true;
        public Guid OrganizationId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public string CorrelationId => "test";
    }
    private sealed class ConfigurationFake : IWhatsAppConfiguration
    {
        public string PhoneNumberId => "phone-1"; public string BusinessPhoneNumber => "+551140002026";
        public int FreeServiceMessageLimit => 1000; public int AutomationPauseAt => 970;
    }
    private sealed class CloudFake : IWhatsAppCloudClient
    {
        public Task<WhatsAppSendResult> SendTextAsync(string phoneNumberId, string customerPhone, string text, CancellationToken token) => Task.FromResult(new WhatsAppSendResult("wamid.out", DateTimeOffset.UtcNow));
    }
}
