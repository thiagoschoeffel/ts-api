using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Ts.Api.Api;

namespace Ts.Api.Domain.Tests.Security;

public sealed class RequestObservabilityTests
{
    [Fact]
    public async Task Preserves_a_valid_frontend_correlation_id_and_records_metrics()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "web-123.valid";
        var metrics = new ApiMetrics();
        var middleware = new RequestObservabilityMiddleware(next: request =>
        {
            request.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }, NullLogger<RequestObservabilityMiddleware>.Instance);

        await middleware.InvokeAsync(context, metrics);

        Assert.Equal("web-123.valid", context.TraceIdentifier);
        Assert.Equal("web-123.valid", context.Response.Headers["X-Correlation-Id"]);
        Assert.Contains("ts_api_http_requests_total 1", metrics.Snapshot());
    }

    [Fact]
    public void Rejects_an_unsafe_correlation_id()
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "server-generated";
        context.Request.Headers["X-Correlation-Id"] = "invalid value with spaces";

        Assert.Equal("server-generated", RequestObservabilityMiddleware.ResolveCorrelationId(context));
    }
}
