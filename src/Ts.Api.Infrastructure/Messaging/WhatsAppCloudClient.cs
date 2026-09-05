using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Ts.Api.Application.Attendance;

namespace Ts.Api.Infrastructure.Messaging;

public sealed class WhatsAppCloudClient(HttpClient http, IConfiguration configuration, TimeProvider timeProvider) : IWhatsAppCloudClient
{
    public async Task<WhatsAppSendResult> SendTextAsync(string phoneNumberId, string customerPhone, string text, CancellationToken token)
    {
        var accessToken = configuration["WhatsApp:AccessToken"] ?? throw new InvalidOperationException("WhatsApp:AccessToken não foi configurado.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{phoneNumberId}/messages") { Content = JsonContent.Create(new { messaging_product = "whatsapp", recipient_type = "individual", to = customerPhone, type = "text", text = new { preview_url = false, body = text } }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, token); var payload = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"A Meta recusou o envio ({(int)response.StatusCode}).");
        using var json = JsonDocument.Parse(payload); var id = json.RootElement.GetProperty("messages")[0].GetProperty("id").GetString();
        return new(id ?? throw new InvalidOperationException("A Meta não retornou o identificador da mensagem."), timeProvider.GetUtcNow());
    }
}
