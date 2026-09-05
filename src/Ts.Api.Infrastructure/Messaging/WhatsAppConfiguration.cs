using Microsoft.Extensions.Configuration;
using Ts.Api.Application.Attendance;

namespace Ts.Api.Infrastructure.Messaging;

public sealed class WhatsAppConfiguration(IConfiguration configuration) : IWhatsAppConfiguration
{
    public string PhoneNumberId => Required("PhoneNumberId");
    public string BusinessPhoneNumber => Required("BusinessPhoneNumber");
    public int FreeServiceMessageLimit => ReadInt("FreeServiceMessageLimit", 1000);
    public int AutomationPauseAt => ReadInt("AutomationPauseAt", 970);
    private string Required(string name) => configuration[$"WhatsApp:{name}"] is { Length: > 0 } value ? value : throw new InvalidOperationException($"WhatsApp:{name} não foi configurado.");
    private int ReadInt(string name, int fallback) => int.TryParse(configuration[$"WhatsApp:{name}"], out var value) ? value : fallback;
}
