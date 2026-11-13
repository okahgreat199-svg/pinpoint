using System.Security.Cryptography;
using SeatCharter.Api.Domain;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Tests;

public class QrTokenTests
{
    private static readonly byte[] Secret = RandomNumberGenerator.GetBytes(32);
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static string TokenAt(DateTimeOffset t) => QrToken.Create(SessionId, QrToken.StepAt(t, 15), Secret);

    [Fact]
    public void Validate_CurrentStep_IsValid()
        => Assert.Equal(QrValidation.Valid, QrToken.Validate(TokenAt(Now), SessionId, Secret, Now, 15));

    [Fact]
    public void Validate_PreviousStep_IsValid()
        => Assert.Equal(QrValidation.Valid, QrToken.Validate(TokenAt(Now.AddSeconds(-15)), SessionId, Secret, Now, 15));

    [Fact]
    public void Validate_ScreenshotThirtySecondsOld_IsExpired()
        => Assert.Equal(QrValidation.Expired, QrToken.Validate(TokenAt(Now.AddSeconds(-30)), SessionId, Secret, Now, 15));

    [Fact]
    public void Validate_FutureStep_IsExpired()
        => Assert.Equal(QrValidation.Expired, QrToken.Validate(TokenAt(Now.AddSeconds(30)), SessionId, Secret, Now, 15));

    [Fact]
    public void Validate_TamperedMac_IsBadSignature()
    {
        var bytes = System.Buffers.Text.Base64Url.DecodeFromChars(TokenAt(Now));
        bytes[^1] ^= 0x01;
        Assert.Equal(QrValidation.BadSignature, QrToken.Validate(System.Buffers.Text.Base64Url.EncodeToString(bytes), SessionId, Secret, Now, 15));
    }

    [Fact]
    public void Validate_ForgedStep_IsBadSignatureNotValid()
    {
        // Attacker rewrites an old token's step to the current one; the MAC no longer matches.
        var bytes = System.Buffers.Text.Base64Url.DecodeFromChars(TokenAt(Now.AddMinutes(-5)));
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(16, 8), QrToken.StepAt(Now, 15));
        Assert.Equal(QrValidation.BadSignature, QrToken.Validate(System.Buffers.Text.Base64Url.EncodeToString(bytes), SessionId, Secret, Now, 15));
    }

    [Fact]
    public void Validate_OtherSessionsSecret_IsBadSignature()
        => Assert.Equal(QrValidation.BadSignature, QrToken.Validate(TokenAt(Now), SessionId, RandomNumberGenerator.GetBytes(32), Now, 15));

    [Fact]
    public void Validate_OtherSessionId_IsWrongSession()
        => Assert.Equal(QrValidation.WrongSession, QrToken.Validate(TokenAt(Now), Guid.NewGuid(), Secret, Now, 15));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("!!!!")]
    public void Validate_Garbage_IsMalformed(string? token)
        => Assert.Equal(QrValidation.Malformed, QrToken.Validate(token, SessionId, Secret, Now, 15));

    [Fact]
    public void TryReadSessionId_RoundTrips()
    {
        Assert.True(QrToken.TryReadSessionId(TokenAt(Now), out var id));
        Assert.Equal(SessionId, id);
    }

    [Fact]
    public void ManualCode_IsSixDigits_AndValidForCurrentAndPreviousStepOnly()
    {
        var code = QrToken.ManualCode(SessionId, QrToken.StepAt(Now, 15), Secret);
        Assert.Matches("^[0-9]{6}$", code);
        Assert.Equal(QrValidation.Valid, QrToken.ValidateManualCode(code, SessionId, Secret, Now, 15));
        Assert.Equal(QrValidation.Valid, QrToken.ValidateManualCode(code, SessionId, Secret, Now.AddSeconds(15), 15));
        Assert.Equal(QrValidation.Expired, QrToken.ValidateManualCode(code, SessionId, Secret, Now.AddSeconds(30), 15));
    }

    [Fact]
    public void SecondsRemaining_CountsDownWithinStep()
    {
        var start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_005 - 1_800_000_005 % 15);
        Assert.Equal(15, QrToken.SecondsRemaining(start, 15));
        Assert.Equal(1, QrToken.SecondsRemaining(start.AddSeconds(14), 15));
    }
}
