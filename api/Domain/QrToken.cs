using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Domain;

/// <summary>
/// Rotating QR token (BR-1): base64url(sessionId[16] | step[8, big-endian] | HMAC-SHA256(secret, sessionId | step)[0..16]).
/// step = floor(unixSeconds / stepSeconds). The current and previous step are accepted.
/// </summary>
public static class QrToken
{
    private const int GuidBytes = 16;
    private const int StepBytes = 8;
    private const int MacBytes = 16;
    private const int TokenBytes = GuidBytes + StepBytes + MacBytes;
    private static readonly byte[] ManualCodeDomain = Encoding.ASCII.GetBytes("manual-code");

    /// <summary>Path prefix of deep-link QR codes: https://host/c/{token}. The stock camera opens the app via Android App Links.</summary>
    public const string DeepLinkPath = "/c/";

    public static string ToDeepLink(string publicBaseUrl, string token) => publicBaseUrl.TrimEnd('/') + DeepLinkPath + token;

    /// <summary>
    /// Accepts what a scanner read: a deep link (any host; the HMAC is what's trusted, not the URL) or a bare token / manual code.
    /// </summary>
    public static string ExtractToken(string scanned)
    {
        var value = scanned.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && uri.AbsolutePath.StartsWith(DeepLinkPath, StringComparison.Ordinal))
            return uri.AbsolutePath[DeepLinkPath.Length..].TrimEnd('/');
        return value;
    }

    public static long StepAt(DateTimeOffset now, int stepSeconds) => now.ToUnixTimeSeconds() / stepSeconds;

    /// <summary>Seconds until the current step rolls over.</summary>
    public static int SecondsRemaining(DateTimeOffset now, int stepSeconds)
        => stepSeconds - (int)(now.ToUnixTimeSeconds() % stepSeconds);

    public static string Create(Guid sessionId, long step, byte[] secret)
    {
        var buffer = new byte[TokenBytes];
        WriteMessage(buffer, sessionId, step);
        var mac = HMACSHA256.HashData(secret, buffer.AsSpan(0, GuidBytes + StepBytes));
        mac.AsSpan(0, MacBytes).CopyTo(buffer.AsSpan(GuidBytes + StepBytes));
        return Base64UrlCodec.Encode(buffer);
    }

    /// <summary>Reads the session id without verifying anything, so the caller can load the session's secret.</summary>
    public static bool TryReadSessionId(string? token, out Guid sessionId)
    {
        sessionId = Guid.Empty;
        var bytes = Base64UrlCodec.TryDecode(token);
        if (bytes is null || bytes.Length != TokenBytes) return false;
        sessionId = new Guid(bytes.AsSpan(0, GuidBytes), bigEndian: true);
        return true;
    }

    public static QrValidation Validate(string? token, Guid expectedSessionId, byte[] secret, DateTimeOffset now, int stepSeconds)
    {
        var bytes = Base64UrlCodec.TryDecode(token);
        if (bytes is null || bytes.Length != TokenBytes) return QrValidation.Malformed;

        var sessionId = new Guid(bytes.AsSpan(0, GuidBytes), bigEndian: true);
        if (sessionId != expectedSessionId) return QrValidation.WrongSession;

        // Verify the MAC before trusting the step, so a forged step reads as tampering rather than expiry.
        var expected = HMACSHA256.HashData(secret, bytes.AsSpan(0, GuidBytes + StepBytes));
        if (!CryptographicOperations.FixedTimeEquals(expected.AsSpan(0, MacBytes), bytes.AsSpan(GuidBytes + StepBytes, MacBytes)))
            return QrValidation.BadSignature;

        var step = BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(GuidBytes, StepBytes));
        var current = StepAt(now, stepSeconds);
        return step == current || step == current - 1 ? QrValidation.Valid : QrValidation.Expired;
    }

    /// <summary>6-digit fallback code for students who can't scan, derived from the same secret and step.</summary>
    public static string ManualCode(Guid sessionId, long step, byte[] secret)
    {
        var message = new byte[ManualCodeDomain.Length + GuidBytes + StepBytes];
        ManualCodeDomain.CopyTo(message, 0);
        WriteMessage(message.AsSpan(ManualCodeDomain.Length), sessionId, step);
        var mac = HMACSHA256.HashData(secret, message);
        var value = BinaryPrimitives.ReadUInt32BigEndian(mac) % 1_000_000;
        return value.ToString("D6");
    }

    public static bool IsManualCodeShape(string? value) => value is { Length: 6 } && value.All(char.IsAsciiDigit);

    public static QrValidation ValidateManualCode(string code, Guid sessionId, byte[] secret, DateTimeOffset now, int stepSeconds)
    {
        var current = StepAt(now, stepSeconds);
        var codeBytes = Encoding.ASCII.GetBytes(code);
        foreach (var step in new[] { current, current - 1 })
        {
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(ManualCode(sessionId, step, secret)), codeBytes))
                return QrValidation.Valid;
        }
        return QrValidation.Expired;
    }

    private static void WriteMessage(Span<byte> destination, Guid sessionId, long step)
    {
        sessionId.TryWriteBytes(destination[..GuidBytes], bigEndian: true, out _);
        BinaryPrimitives.WriteInt64BigEndian(destination.Slice(GuidBytes, StepBytes), step);
    }
}

internal static class Base64UrlCodec
{
    public static string Encode(byte[] data) => System.Buffers.Text.Base64Url.EncodeToString(data);

    public static byte[]? TryDecode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256) return null;
        try { return System.Buffers.Text.Base64Url.DecodeFromChars(value.Trim()); }
        catch (FormatException) { return null; }
    }
}
