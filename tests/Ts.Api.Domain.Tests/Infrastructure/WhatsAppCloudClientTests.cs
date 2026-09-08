using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Ts.Api.Application.Attendance;
using Ts.Api.Infrastructure.Messaging;

namespace Ts.Api.Domain.Tests.Infrastructure;

public sealed class WhatsAppCloudClientTests
{
    [Fact]
    public async Task Extracts_allowlisted_meta_diagnostics_without_exposing_the_payload()
    {
        const string payload = """{"error":{"message":"private customer data","code":131047,"error_subcode":2494010,"fbtrace_id":"trace-safe","is_transient":false}}""";
        var client = Create(HttpStatusCode.BadRequest, payload);

        var error = await Assert.ThrowsAsync<WhatsAppProviderException>(() =>
            client.SendTextAsync("phone-1", "5511999999999", "Olá", "test-token", CancellationToken.None));

        Assert.Equal(WhatsAppSendOutcome.Rejected, error.Outcome);
        Assert.Equal(400, error.HttpStatus);
        Assert.Equal(131047, error.ProviderCode);
        Assert.Equal(2494010, error.ProviderSubcode);
        Assert.Equal("trace-safe", error.TraceId);
        Assert.DoesNotContain("private customer data", error.Message);
    }

    [Fact]
    public async Task Successful_but_malformed_response_is_an_unknown_outcome()
    {
        var client = Create(HttpStatusCode.OK, "{}");

        var error = await Assert.ThrowsAsync<WhatsAppProviderException>(() =>
            client.SendTextAsync("phone-1", "5511999999999", "Olá", "test-token", CancellationToken.None));

        Assert.Equal(WhatsAppSendOutcome.Unknown, error.Outcome);
    }

    [Fact]
    public async Task Verifies_that_meta_returns_the_requested_phone_asset()
    {
        var client = Create(HttpStatusCode.OK, """{"id":"phone-1","display_phone_number":"+551100000001"}""");

        await client.VerifyAsync("phone-1", "connection-token", CancellationToken.None);
    }

    private static WhatsAppCloudClient Create(HttpStatusCode status, string payload)
    {
        var handler = new StubHandler(new HttpResponseMessage(status)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });
        return new WhatsAppCloudClient(new HttpClient(handler) { BaseAddress = new Uri("https://graph.test/") },
            TimeProvider.System);
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
