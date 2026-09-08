using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Ts.Api.Application.Attendance;
using Ts.Api.Application.Organizations;

namespace Ts.Api.Infrastructure.Messaging;

public sealed class WhatsAppCloudClient(HttpClient http, TimeProvider timeProvider) : IWhatsAppCloudClient,
    IWhatsAppConnectionVerifier
{
    public async Task VerifyAsync(string phoneNumberId, string accessToken, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{Uri.EscapeDataString(phoneNumberId)}?fields=id,display_phone_number,verified_name");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, token);
        var payload = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode) throw ProviderRejection(response, payload);
        try
        {
            using var json = JsonDocument.Parse(payload);
            if (!string.Equals(json.RootElement.GetProperty("id").GetString(), phoneNumberId,
                    StringComparison.Ordinal))
                throw new JsonException("O ativo retornado não corresponde ao solicitado.");
        }
        catch (JsonException exception)
        {
            throw new WhatsAppProviderException(WhatsAppSendOutcome.Rejected,
                "A Meta não confirmou o identificador do número informado.",
                (int)response.StatusCode, innerException: exception);
        }
    }

    public async Task<WhatsAppSendResult> SendTextAsync(string phoneNumberId, string customerPhone, string text,
        string accessToken, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{phoneNumberId}/messages") { Content = JsonContent.Create(new { messaging_product = "whatsapp", recipient_type = "individual", to = customerPhone, type = "text", text = new { preview_url = false, body = text } }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, token); var payload = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode) throw ProviderRejection(response, payload);
        try
        {
            using var json = JsonDocument.Parse(payload); var id = json.RootElement.GetProperty("messages")[0].GetProperty("id").GetString();
            return new(id ?? throw new JsonException("Identificador ausente."), timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new WhatsAppProviderException(WhatsAppSendOutcome.Unknown,
                "A Meta respondeu com sucesso, mas o identificador da mensagem não pôde ser confirmado.",
                httpStatus: (int)response.StatusCode, innerException: exception);
        }
    }

    private static WhatsAppProviderException ProviderRejection(HttpResponseMessage response, string payload)
    {
        int? code = null; int? subcode = null; string? traceId = null; bool? transient = null;
        try
        {
            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("code", out var codeNode) && codeNode.TryGetInt32(out var parsedCode)) code = parsedCode;
                if (error.TryGetProperty("error_subcode", out var subcodeNode) && subcodeNode.TryGetInt32(out var parsedSubcode)) subcode = parsedSubcode;
                if (error.TryGetProperty("fbtrace_id", out var traceNode)) traceId = traceNode.GetString();
                if (error.TryGetProperty("is_transient", out var transientNode) && transientNode.ValueKind is JsonValueKind.True or JsonValueKind.False) transient = transientNode.GetBoolean();
            }
        }
        catch (JsonException) { /* Resposta inválida continua sendo relatada sem incluir o payload. */ }

        var details = new List<string> { $"HTTP {(int)response.StatusCode}" };
        if (code.HasValue) details.Add($"código {code}");
        if (subcode.HasValue) details.Add($"subcódigo {subcode}");
        if (!string.IsNullOrWhiteSpace(traceId)) details.Add($"rastreio {traceId}");
        return new WhatsAppProviderException(WhatsAppSendOutcome.Rejected,
            $"A Meta recusou o envio ({string.Join(", ", details)}).", (int)response.StatusCode,
            code, subcode, traceId, transient);
    }
}
