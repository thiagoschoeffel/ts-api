using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Ts.Api.Application.Organizations;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Infrastructure.Messaging;

public sealed class ResendInvitationEmailSender(HttpClient client, IConfiguration configuration) : IInvitationEmailSender
{
    public async Task<string> SendAsync(Guid invitationId, string email, string organizationName,
        OrganizationRole role, string token, CancellationToken cancellationToken)
    {
        var apiKey = Required("Resend:ApiKey");
        var from = Required("Resend:From");
        var appUrl = Required("Resend:InvitationUrl").TrimEnd('/');
        var link = $"{appUrl}?token={Uri.EscapeDataString(token)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("Idempotency-Key", $"membership-invitation/{invitationId:N}");
        request.Content = JsonContent.Create(new
        {
            from,
            to = new[] { email },
            subject = $"Convite para acessar {organizationName}",
            html = $"<p>Você foi convidado para acessar <strong>{WebUtility.HtmlEncode(organizationName)}</strong> como {WebUtility.HtmlEncode(role.ToString())}.</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Aceitar convite</a></p><p>Este convite expira em 7 dias.</p>"
        });
        using var response = await client.SendAsync(request, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<ResendResponse>(cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(result?.Id))
            throw new HttpRequestException($"O Resend rejeitou o envio do convite ({(int)response.StatusCode}).");
        return result.Id;
    }

    private string Required(string key) => configuration[key] is { Length: > 0 } value
        ? value : throw new InvalidOperationException($"A configuração {key} é obrigatória para enviar convites.");
    private sealed record ResendResponse([property: JsonPropertyName("id")] string Id);
}
