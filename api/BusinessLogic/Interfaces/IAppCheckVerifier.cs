namespace SeatCharter.Api.BusinessLogic.Interfaces;

public sealed record AppCheckResult(bool IsValid, string? AppId, string? Error)
{
    public static AppCheckResult Fail(string error) => new(false, null, error);
}

public interface IAppCheckVerifier
{
    /// <summary>Verifies a Firebase App Check token (RS256 JWT signed by Google, proving a genuine app on a genuine device).</summary>
    Task<AppCheckResult> VerifyAsync(string? token, CancellationToken ct = default);
}
