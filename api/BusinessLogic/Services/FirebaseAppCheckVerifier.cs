using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.Models;

namespace SeatCharter.Api.BusinessLogic.Services;

/// <summary>
/// Verifies App Check tokens per Firebase's spec: RS256, signed by a key from the App Check JWKS,
/// issuer https://firebaseappcheck.googleapis.com/{projectNumber}, audience projects/{projectNumber}, unexpired,
/// and sub = one of our app ids.
/// </summary>
public class FirebaseAppCheckVerifier : IAppCheckVerifier
{
    public const string JwksUrl = "https://firebaseappcheck.googleapis.com/v1/jwks";
    private const string JwksCacheKey = "appcheck:jwks";
    private static readonly TimeSpan JwksCacheFor = TimeSpan.FromHours(6);   // Firebase: cache no longer than 6 h

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly AppCheckSettings _settings;
    private readonly ILogger<FirebaseAppCheckVerifier> _logger;
    private readonly JsonWebTokenHandler _handler = new();

    public FirebaseAppCheckVerifier(HttpClient http, IMemoryCache cache, AppCheckSettings settings, ILogger<FirebaseAppCheckVerifier> logger)
    {
        _http = http;
        _cache = cache;
        _settings = settings;
        _logger = logger;
    }

    public async Task<AppCheckResult> VerifyAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096) return AppCheckResult.Fail("missing");

        var result = await ValidateAsync(token, await GetKeysAsync(forceRefresh: false, ct));
        if (result.Exception is SecurityTokenSignatureKeyNotFoundException)
            result = await ValidateAsync(token, await GetKeysAsync(forceRefresh: true, ct));   // Google rotated keys

        if (!result.IsValid) return AppCheckResult.Fail(result.Exception?.GetType().Name ?? "invalid");

        var appId = ((JsonWebToken)result.SecurityToken).Subject;
        if (_settings.AppIds.Length > 0 && !_settings.AppIds.Contains(appId))
            return AppCheckResult.Fail("unknown app id");

        return new AppCheckResult(true, appId, null);
    }

    private Task<TokenValidationResult> ValidateAsync(string token, IList<SecurityKey> keys)
    {
        var audiences = new List<string> { $"projects/{_settings.ProjectNumber}" };
        if (!string.IsNullOrWhiteSpace(_settings.ProjectId)) audiences.Add($"projects/{_settings.ProjectId}");

        return _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = $"https://firebaseappcheck.googleapis.com/{_settings.ProjectNumber}",
            ValidAudiences = audiences,
            IssuerSigningKeys = keys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuerSigningKey = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        });
    }

    private async Task<IList<SecurityKey>> GetKeysAsync(bool forceRefresh, CancellationToken ct)
    {
        _cache.TryGetValue(JwksCacheKey, out IList<SecurityKey>? cached);
        if (!forceRefresh && cached is not null) return cached;

        try
        {
            var json = await _http.GetStringAsync(JwksUrl, ct);
            IList<SecurityKey> keys = new JsonWebKeySet(json).GetSigningKeys();
            _cache.Set(JwksCacheKey, keys, JwksCacheFor);
            return keys;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException)
        {
            _logger.LogError(ex, "Could not fetch App Check JWKS");
            return cached ?? [];   // fail closed: no keys → every token is rejected
        }
    }
}
