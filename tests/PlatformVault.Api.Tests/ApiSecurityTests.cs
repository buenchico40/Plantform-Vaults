using System.Net;
using System.Security.Cryptography;
using System.Text;
using PlatformVault.Api.Security;
using PlatformVault.Infrastructure.Identity;

namespace PlatformVault.Api.Tests;

public sealed class ApiClientTests
{
    private const string Key = "clave-de-prueba-del-canal-web-0123456789";

    private static ApiClientOptions Options()
    {
        var client = new ApiClient { Name = "PlatformVault.Web", KeySha256 = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Key))) };
        client.AllowedNetworks.Add("10.20.30.0/24");
        client.AllowedNetworks.Add("::1/128");
        var options = new ApiClientOptions();
        options.Clients.Add(client);
        return options;
    }

    [Theory]
    [InlineData("10.20.30.15", Key, true)]
    [InlineData("::ffff:10.20.30.15", Key, true)]
    [InlineData("::1", Key, true)]
    [InlineData("10.20.31.15", Key, false)]
    [InlineData("10.20.30.15", "otra-clave", false)]
    [InlineData("10.20.30.15", "", false)]
    public void Client_requires_allowed_network_and_valid_key_IMP05(string ip, string key, bool allowed) =>
        Assert.Equal(allowed, ApiClientMiddleware.Match(Options(), IPAddress.Parse(ip), key) is not null);

    [Fact]
    public void Oversized_keys_are_rejected() =>
        Assert.Null(ApiClientMiddleware.Match(Options(), IPAddress.Loopback, new string('x', 1000)));
}

public sealed class SessionTokenTests
{
    [Fact]
    public void Only_well_formed_256_bit_tokens_are_hashed_IMP26()
    {
        var raw = RandomNumberGenerator.GetBytes(32);
        var token = System.Buffers.Text.Base64Url.EncodeToString(raw);
        Assert.Equal(SHA256.HashData(raw), SessionPolicy.Hash(token));
        Assert.Empty(SessionPolicy.Hash("corto"));
        Assert.Empty(SessionPolicy.Hash(token + "AAAA"));
        Assert.Empty(SessionPolicy.Hash("!!!!"));
    }

    [Fact]
    public void Session_policy_matches_IMP26()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), SessionPolicy.IdleTimeout);
        Assert.Equal(TimeSpan.FromHours(8), SessionPolicy.AbsoluteLifetime);
        Assert.Equal(TimeSpan.FromDays(90), SessionPolicy.PasswordMaxAge);
    }
}
