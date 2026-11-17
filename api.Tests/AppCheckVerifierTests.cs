using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SeatCharter.Api.BusinessLogic.Services;
using SeatCharter.Api.Models;

namespace SeatCharter.Api.Tests;

public class AppCheckVerifierTests
{
    private const string ProjectNumber = "123456789";
    private const string AppId = "1:123456789:android:abc";

    private readonly RsaSecurityKey _googleKey = new(RSA.Create(2048)) { KeyId = "kid-1" };
    private readonly AppCheckSettings _settings = new() { Enabled = true, ProjectNumber = ProjectNumber, AppIds = [AppId] };

    private sealed class JwksHandler(string json) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }

    private string JwksJson(params RsaSecurityKey[] keys) => JsonSerializer.Serialize(new
    {
        keys = keys.Select(k =>
        {
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(k);
            return new { kty = jwk.Kty, kid = jwk.Kid, n = jwk.N, e = jwk.E, alg = "RS256", use = "sig" };
        })
    });

    private FirebaseAppCheckVerifier Verifier(JwksHandler handler) =>
        new(new HttpClient(handler), new MemoryCache(new MemoryCacheOptions()), _settings, NullLogger<FirebaseAppCheckVerifier>.Instance);

    private string Token(SecurityKey key, string? issuer = null, string? audience = null, string sub = AppId, DateTime? expires = null)
        => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer ?? $"https://firebaseappcheck.googleapis.com/{ProjectNumber}",
            Audience = audience ?? $"projects/{ProjectNumber}",
            Claims = new Dictionary<string, object> { ["sub"] = sub },
            NotBefore = DateTime.UtcNow.AddMinutes(-5),
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            IssuedAt = DateTime.UtcNow.AddMinutes(-5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        });

    [Fact]
    public async Task Verify_GenuineToken_Valid()
    {
        var result = await Verifier(new JwksHandler(JwksJson(_googleKey))).VerifyAsync(Token(_googleKey));
        Assert.True(result.IsValid, result.Error);
        Assert.Equal(AppId, result.AppId);
    }

    [Fact]
    public async Task Verify_SignedByAttackerKey_Invalid()
    {
        var attacker = new RsaSecurityKey(RSA.Create(2048)) { KeyId = "kid-1" };
        Assert.False((await Verifier(new JwksHandler(JwksJson(_googleKey))).VerifyAsync(Token(attacker))).IsValid);
    }

    [Fact]
    public async Task Verify_OtherFirebaseProject_Invalid()
    {
        var v = Verifier(new JwksHandler(JwksJson(_googleKey)));
        Assert.False((await v.VerifyAsync(Token(_googleKey, issuer: "https://firebaseappcheck.googleapis.com/999"))).IsValid);
        Assert.False((await v.VerifyAsync(Token(_googleKey, audience: "projects/999"))).IsValid);
    }

    [Fact]
    public async Task Verify_OtherAppInProject_Invalid()
        => Assert.False((await Verifier(new JwksHandler(JwksJson(_googleKey))).VerifyAsync(Token(_googleKey, sub: "1:123456789:android:other"))).IsValid);

    [Fact]
    public async Task Verify_Expired_Invalid()
        => Assert.False((await Verifier(new JwksHandler(JwksJson(_googleKey))).VerifyAsync(Token(_googleKey, expires: DateTime.UtcNow.AddMinutes(-2)))).IsValid);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not.a.jwt")]
    public async Task Verify_Garbage_Invalid(string? token)
        => Assert.False((await Verifier(new JwksHandler(JwksJson(_googleKey))).VerifyAsync(token)).IsValid);

    [Fact]
    public async Task Verify_CachesJwks()
    {
        var handler = new JwksHandler(JwksJson(_googleKey));
        var v = Verifier(handler);
        await v.VerifyAsync(Token(_googleKey));
        await v.VerifyAsync(Token(_googleKey));
        Assert.Equal(1, handler.Calls);
    }
}
