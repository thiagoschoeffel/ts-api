using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Ts.Api.Api;

public sealed class ApiMetrics
{
    private long requests;
    private long failures;
    private long inFlight;
    private long durationTicks;

    public void Started()
    {
        Interlocked.Increment(ref requests);
        Interlocked.Increment(ref inFlight);
    }

    public void Finished(int statusCode, long elapsedTicks)
    {
        Interlocked.Decrement(ref inFlight);
        Interlocked.Add(ref durationTicks, elapsedTicks);
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            Interlocked.Increment(ref failures);
        }
    }

    public string Snapshot()
    {
        var elapsedSeconds = TimeSpan.FromTicks(Interlocked.Read(ref durationTicks)).TotalSeconds;
        var output = new StringBuilder()
            .AppendLine("# HELP ts_api_http_requests_total Total de requisições HTTP recebidas.")
            .AppendLine("# TYPE ts_api_http_requests_total counter")
            .Append("ts_api_http_requests_total ").AppendLine(Interlocked.Read(ref requests).ToString(CultureInfo.InvariantCulture))
            .AppendLine("# HELP ts_api_http_request_failures_total Total de respostas HTTP 5xx.")
            .AppendLine("# TYPE ts_api_http_request_failures_total counter")
            .Append("ts_api_http_request_failures_total ").AppendLine(Interlocked.Read(ref failures).ToString(CultureInfo.InvariantCulture))
            .AppendLine("# HELP ts_api_http_requests_in_flight Requisições HTTP em processamento.")
            .AppendLine("# TYPE ts_api_http_requests_in_flight gauge")
            .Append("ts_api_http_requests_in_flight ").AppendLine(Interlocked.Read(ref inFlight).ToString(CultureInfo.InvariantCulture))
            .AppendLine("# HELP ts_api_http_request_duration_seconds_sum Soma da duração das requisições HTTP.")
            .AppendLine("# TYPE ts_api_http_request_duration_seconds_sum counter")
            .Append("ts_api_http_request_duration_seconds_sum ").AppendLine(elapsedSeconds.ToString("0.000000", CultureInfo.InvariantCulture));
        return output.ToString();
    }
}

public sealed class RequestObservabilityMiddleware(RequestDelegate next, ILogger<RequestObservabilityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ApiMetrics metrics)
    {
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        var started = Stopwatch.GetTimestamp();
        metrics.Started();

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = Activity.Current?.TraceId.ToString() ?? correlationId,
        }))
        {
            try
            {
                await next(context);
            }
            finally
            {
                var elapsed = Stopwatch.GetElapsedTime(started);
                metrics.Finished(context.Response.StatusCode, elapsed.Ticks);
                logger.LogInformation("HTTP {Method} {Path} respondeu {StatusCode} em {ElapsedMilliseconds} ms",
                    context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsed.TotalMilliseconds);
            }
        }
    }

    public static string ResolveCorrelationId(HttpContext context)
    {
        var requested = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        return IsValid(requested) ? requested! : Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    }

    private static bool IsValid(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 100
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
}
