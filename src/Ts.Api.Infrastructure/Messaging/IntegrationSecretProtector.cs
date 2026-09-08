using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Ts.Api.Application.Organizations;

namespace Ts.Api.Infrastructure.Messaging;

public sealed class IntegrationSecretProtector : IIntegrationSecretProtector
{
    private readonly byte[] key;

    public IntegrationSecretProtector(IConfiguration configuration)
    {
        var configured = configuration["IntegrationSecrets:EncryptionKey"];
        try { key = Convert.FromBase64String(configured ?? string.Empty); }
        catch (FormatException exception) { throw new InvalidOperationException(
            "IntegrationSecrets:EncryptionKey deve ser uma chave Base64 de 32 bytes.", exception); }
        if (key.Length != 32) throw new InvalidOperationException(
            "IntegrationSecrets:EncryptionKey deve ser uma chave Base64 de 32 bytes.");
    }

    public string Protect(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12); var tag = new byte[16];
        var source = Encoding.UTF8.GetBytes(plaintext); var encrypted = new byte[source.Length];
        using var aes = new AesGcm(key, tag.Length); aes.Encrypt(nonce, source, encrypted, tag);
        return $"v1.{Convert.ToBase64String(nonce)}.{Convert.ToBase64String(tag)}.{Convert.ToBase64String(encrypted)}";
    }

    public string Unprotect(string protectedValue)
    {
        var parts = protectedValue.Split('.');
        if (parts.Length != 4 || parts[0] != "v1") throw new CryptographicException("Formato de segredo desconhecido.");
        var nonce = Convert.FromBase64String(parts[1]); var tag = Convert.FromBase64String(parts[2]);
        var encrypted = Convert.FromBase64String(parts[3]); var plaintext = new byte[encrypted.Length];
        using var aes = new AesGcm(key, tag.Length); aes.Decrypt(nonce, encrypted, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }
}
