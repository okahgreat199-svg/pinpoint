using System.Security.Cryptography;
using System.Text;
using SeatCharter.Api.Domain;

namespace SeatCharter.Api.Tests;

public class DeviceSignatureTests
{
    private static readonly ECDsa Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private static readonly string Spki = Convert.ToBase64String(Key.ExportSubjectPublicKeyInfo());
    private static readonly string Payload = DeviceSignature.CanonicalPayload("tok", 56.458, -2.98, 12.34, false, "2026-11-18T10:00:00Z", "dev-1");

    private static string Sign(DSASignatureFormat format)
        => Convert.ToBase64String(Key.SignData(Encoding.UTF8.GetBytes(Payload), HashAlgorithmName.SHA256, format));

    [Fact]
    public void CanonicalPayload_UsesFixedDecimalsAndInvariantCulture()
        => Assert.Equal("tok|56.458000|-2.980000|12.3|false|2026-11-18T10:00:00Z|dev-1", Payload);

    [Fact]
    public void Verify_DerSignature_Passes() => Assert.True(DeviceSignature.Verify(Spki, Payload, Sign(DSASignatureFormat.Rfc3279DerSequence)));

    [Fact]
    public void Verify_RawRsSignature_Passes() => Assert.True(DeviceSignature.Verify(Spki, Payload, Sign(DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));

    [Fact]
    public void Verify_RawUncompressedPublicKey_Passes()
    {
        var p = Key.ExportParameters(false);
        var raw = Convert.ToBase64String([0x04, .. p.Q.X!, .. p.Q.Y!]);
        Assert.True(DeviceSignature.Verify(raw, Payload, Sign(DSASignatureFormat.Rfc3279DerSequence)));
    }

    [Fact]
    public void Verify_ChangedLocation_Fails()
    {
        var moved = DeviceSignature.CanonicalPayload("tok", 56.459, -2.98, 12.34, false, "2026-11-18T10:00:00Z", "dev-1");
        Assert.False(DeviceSignature.Verify(Spki, moved, Sign(DSASignatureFormat.Rfc3279DerSequence)));
    }

    [Fact]
    public void Verify_OtherPhonesKey_Fails()
    {
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.False(DeviceSignature.Verify(Convert.ToBase64String(other.ExportSubjectPublicKeyInfo()), Payload, Sign(DSASignatureFormat.Rfc3279DerSequence)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("%%%")]
    [InlineData("AAAA")]
    public void Verify_GarbageSignature_Fails(string? sig) => Assert.False(DeviceSignature.Verify(Spki, Payload, sig));

    [Fact]
    public void IsValidPublicKey_RejectsNonP256()
    {
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        Assert.False(DeviceSignature.IsValidPublicKey(Convert.ToBase64String(p384.ExportSubjectPublicKeyInfo())));
        Assert.False(DeviceSignature.IsValidPublicKey("not base64"));
        Assert.True(DeviceSignature.IsValidPublicKey(Spki));
    }
}
