using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Attendance;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Attendance;
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

    [Fact]
    public async Task Definitive_provider_rejection_is_retryable_and_releases_quota()
    {
        var (service, _) = CreateService(new CloudFake(new WhatsAppProviderException(
            WhatsAppSendOutcome.Rejected, "A Meta recusou o envio (HTTP 400, código 131047).")));
        var conversation = await CreateHumanConversationAsync(service);

        await Assert.ThrowsAsync<WhatsAppProviderException>(() =>
            service.SendAsync(conversation.Id, "Olá", "send-rejected", CancellationToken.None));

        var snapshot = await service.GetAsync(CancellationToken.None);
        var message = Assert.Single(Assert.Single(snapshot.Conversations).Messages,
            item => item.Direction == MessageDirection.Outbound);
        Assert.Equal(MessageProcessingStatus.Failed, message.ProcessingStatus);
        Assert.Equal(0, snapshot.Quota.ReservedServiceMessages);
    }

    [Fact]
    public async Task Unknown_provider_outcome_keeps_quota_and_cannot_be_retried_automatically()
    {
        var (service, _) = CreateService(new CloudFake(new HttpRequestException("connection reset")));
        var conversation = await CreateHumanConversationAsync(service);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.SendAsync(conversation.Id, "Olá", "send-unknown", CancellationToken.None));

        var snapshot = await service.GetAsync(CancellationToken.None);
        var message = Assert.Single(Assert.Single(snapshot.Conversations).Messages,
            item => item.Direction == MessageDirection.Outbound);
        Assert.Equal(MessageProcessingStatus.OutcomeUnknown, message.ProcessingStatus);
        Assert.Equal(1, snapshot.Quota.ReservedServiceMessages);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RetryAsync(conversation.Id, message.Id, CancellationToken.None));
    }

    private static (AttendanceService Service, RequestContext Context) CreateService(IWhatsAppCloudClient cloud)
    {
        var tenant = new RequestContext();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var database = new AppDbContext(options, tenant);
        return (new AttendanceService(new AttendanceStore(database), cloud, tenant, tenant,
            new ConfigurationFake(), TimeProvider.System), tenant);
    }

    private static async Task<AttendanceConversationResult> CreateHumanConversationAsync(AttendanceService service)
    {
        await service.ReceiveAsync(new WhatsAppInboundEvent("phone-1", "+551140002026", $"wamid.{Guid.NewGuid():N}",
            "5511999999999", "Maria", "Olá", DateTimeOffset.UtcNow), CancellationToken.None);
        var conversation = Assert.Single((await service.GetAsync(CancellationToken.None)).Conversations);
        return await service.ChangeModeAsync(conversation.Id, AttendanceMode.Human, conversation.Version, CancellationToken.None);
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
    private sealed class CloudFake(Exception? exception = null) : IWhatsAppCloudClient
    {
        public Task<WhatsAppSendResult> SendTextAsync(string phoneNumberId, string customerPhone, string text, CancellationToken token) =>
            exception is null
                ? Task.FromResult(new WhatsAppSendResult("wamid.out", DateTimeOffset.UtcNow))
                : Task.FromException<WhatsAppSendResult>(exception);
    }
}
