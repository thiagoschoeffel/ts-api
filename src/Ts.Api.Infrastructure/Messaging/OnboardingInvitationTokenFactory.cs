using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Ts.Api.Application.Organizations;

namespace Ts.Api.Infrastructure.Messaging;

public sealed class OnboardingInvitationTokenFactory(IConfiguration configuration)
    : IOnboardingInvitationTokenFactory
{
    public string Create(Guid invitationId)
    {
        if (invitationId == Guid.Empty) throw new ArgumentException("O convite é obrigatório.", nameof(invitationId));
        var secret = configuration["Onboarding:InvitationTokenSecret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException("Onboarding:InvitationTokenSecret deve possuir ao menos 32 caracteres.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(invitationId.ToByteArray()))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
