using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SeatCharter.Api.Domain;

/// <summary>
/// BR-4: ECDSA P-256 / SHA-256 signature over the check-in fields.
///
/// Canonical payload (UTF-8, fields joined with '|'):
///   token | lat (6 dp) | lng (6 dp) | accuracyM (1 dp) | isMocked ("true"/"false") | clientTime (verbatim) | deviceId
/// Dart: [token, lat.toStringAsFixed(6), lng.toStringAsFixed(6), acc.toStringAsFixed(1), isMocked.toString(), clientTime, deviceId].join('|')
///
/// Public key: base64 SubjectPublicKeyInfo (DER) or base64 raw uncompressed point (65 bytes, 0x04|X|Y).
/// Signature: base64, DER (ASN.1) or raw r|s (64 bytes). Both are accepted.
/// </summary>
public static class DeviceSignature
{
    public static string CanonicalPayload(string token, double lat, double lng, double accuracyM, bool isMocked, string clientTime, string deviceId)
        => string.Join('|',
            token,
            lat.ToString("F6", CultureInfo.InvariantCulture),
            lng.ToString("F6", CultureInfo.InvariantCulture),
            accuracyM.ToString("F1", CultureInfo.InvariantCulture),
            isMocked ? "true" : "false",
            clientTime,
            deviceId);

    public static bool IsValidPublicKey(string? publicKeyBase64)
    {
        using var key = TryImport(publicKeyBase64);
        return key is not null;
    }

    public static bool Verify(string? publicKeyBase64, string payload, string? signatureBase64)
    {
        if (string.IsNullOrWhiteSpace(signatureBase64)) return false;
        byte[] signature;
        try { signature = Convert.FromBase64String(signatureBase64); }
        catch (FormatException) { return false; }

        using var key = TryImport(publicKeyBase64);
        if (key is null) return false;

        var data = Encoding.UTF8.GetBytes(payload);
        try
        {
            if (signature.Length == 64
                && key.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                return true;
            return key.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (CryptographicException) { return false; }
    }

    private static ECDsa? TryImport(string? publicKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(publicKeyBase64)) return null;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(publicKeyBase64.Trim()); }
        catch (FormatException) { return null; }

        var key = ECDsa.Create();
        try
        {
            if (bytes.Length == 65 && bytes[0] == 0x04)
            {
                key.ImportParameters(new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint { X = bytes[1..33], Y = bytes[33..65] }
                });
            }
            else
            {
                key.ImportSubjectPublicKeyInfo(bytes, out _);
                if (key.KeySize != 256) { key.Dispose(); return null; }
            }
            return key;
        }
        catch (CryptographicException)
        {
            key.Dispose();
            return null;
        }
    }
}
