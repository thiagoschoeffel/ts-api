using Microsoft.Extensions.Configuration;
using Ts.Api.Infrastructure.Messaging;

namespace Ts.Api.Domain.Tests.Infrastructure;

public sealed class IntegrationSecretProtectorTests
{
    [Fact]
    public void Encrypts_with_authenticated_random_nonces_and_round_trips()
    {
        var key = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["IntegrationSecrets:EncryptionKey"] = key }).Build();
        var protector = new IntegrationSecretProtector(configuration);

        var first = protector.Protect("sensitive"); var second = protector.Protect("sensitive");

        Assert.NotEqual(first, second);
        Assert.DoesNotContain("sensitive", first);
        Assert.Equal("sensitive", protector.Unprotect(first));
    }
}
