using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ts.Api.Application.Attendance;
using Ts.Api.Application.Organizations;

namespace Ts.Api.Api;

public static class WhatsAppWebhooks
{
    public static IEndpointRouteBuilder MapWhatsAppWebhooks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/webhooks/whatsapp/{connectionId:guid}", Verify)
            .WithApiContext(ApiContextKind.Webhook);
        endpoints.MapPost("/webhooks/whatsapp/{connectionId:guid}", Receive)
            .WithApiContext(ApiContextKind.Webhook);
        return endpoints;
    }

    private static async Task<IResult> Verify(Guid connectionId, HttpRequest request,
        IWhatsAppConnectionResolver connections, CancellationToken cancellationToken)
    {
        var connection = await connections.ResolveWebhookAsync(connectionId, cancellationToken);
        if (connection is null) return Results.NotFound();
        var mode = request.Query["hub.mode"].ToString(); var token = request.Query["hub.verify_token"].ToString(); var challenge = request.Query["hub.challenge"].ToString();
        return mode == "subscribe" && FixedEquals(token, connection.WebhookVerifyToken) && !string.IsNullOrWhiteSpace(challenge)
            ? Results.Text(challenge, "text/plain") : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> Receive(Guid connectionId, HttpRequest request,
        IWhatsAppConnectionResolver connections, HttpRequestContext context, AttendanceService service,
        CancellationToken token)
    {
        var connection = await connections.ResolveWebhookAsync(connectionId, token);
        if (connection is null) return Results.NotFound();
        using var reader = new StreamReader(request.Body, Encoding.UTF8); var body = await reader.ReadToEndAsync(token);
        if (!ValidSignature(body, request.Headers["X-Hub-Signature-256"].ToString(), connection.AppSecret)) return Results.StatusCode(StatusCodes.Status401Unauthorized);
        context.SetWebhookOrganization(connection.OrganizationId, request.HttpContext.TraceIdentifier);
        using var document = JsonDocument.Parse(body);
        foreach (var entry in document.RootElement.GetProperty("entry").EnumerateArray())
        foreach (var change in entry.GetProperty("changes").EnumerateArray())
        {
            var value = change.GetProperty("value"); if (!value.TryGetProperty("metadata", out var metadata)) continue;
            var phoneId = metadata.GetProperty("phone_number_id").GetString() ?? string.Empty; var displayPhone = metadata.TryGetProperty("display_phone_number", out var display) ? display.GetString() ?? phoneId : phoneId;
            if (!string.Equals(phoneId, connection.PhoneNumberId, StringComparison.Ordinal))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            var names = value.TryGetProperty("contacts", out var contacts) ? contacts.EnumerateArray().ToDictionary(x => x.GetProperty("wa_id").GetString() ?? "", x => x.GetProperty("profile").GetProperty("name").GetString()) : new Dictionary<string, string?>();
            if (value.TryGetProperty("messages", out var messages)) foreach (var message in messages.EnumerateArray())
            {
                var from = message.GetProperty("from").GetString() ?? string.Empty; var id = message.GetProperty("id").GetString() ?? string.Empty;
                var text = message.TryGetProperty("text", out var textNode) ? textNode.GetProperty("body").GetString() ?? "" : $"[{message.GetProperty("type").GetString() ?? "conteúdo não textual"}]";
                var at = ParseTimestamp(message);
                await service.ReceiveAsync(new(phoneId, displayPhone, id, from, names.GetValueOrDefault(from), text, at), token);
                await connections.TryRegisterWebhookAsync(connection, $"message:{id}", token);
            }
            if (value.TryGetProperty("message_echoes", out var echoes)) foreach (var message in echoes.EnumerateArray())
            {
                var to = message.GetProperty("to").GetString() ?? string.Empty; var id = message.GetProperty("id").GetString() ?? string.Empty;
                var text = message.TryGetProperty("text", out var textNode) ? textNode.GetProperty("body").GetString() ?? "" : "[conteúdo enviado pelo aplicativo]";
                await service.ReceiveAsync(new(phoneId, displayPhone, id, to, names.GetValueOrDefault(to), text, ParseTimestamp(message), true), token);
                await connections.TryRegisterWebhookAsync(connection, $"echo:{id}", token);
            }
            if (value.TryGetProperty("statuses", out var statuses)) foreach (var status in statuses.EnumerateArray())
            {
                var statusName = status.GetProperty("status").GetString() ?? string.Empty;
                var statusId = status.GetProperty("id").GetString() ?? string.Empty;
                var failure = status.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0 ? errors[0].GetProperty("title").GetString() : null;
                await service.ApplyStatusAsync(new(phoneId, statusId, statusName, failure), token);
                await connections.TryRegisterWebhookAsync(connection, $"status:{statusId}:{statusName}", token);
            }
        }
        return Results.Ok();
    }

    private static DateTimeOffset ParseTimestamp(JsonElement message) => message.TryGetProperty("timestamp", out var value) && long.TryParse(value.GetString(), out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : DateTimeOffset.UtcNow;
    private static bool ValidSignature(string body, string supplied, string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || !supplied.StartsWith("sha256=", StringComparison.Ordinal)) return false;
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
        try { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(supplied[7..])); }
        catch (FormatException) { return false; }
    }
    private static bool FixedEquals(string value, string? expected) => expected is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(value), Encoding.UTF8.GetBytes(expected));
}
