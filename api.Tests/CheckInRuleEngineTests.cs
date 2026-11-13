using SeatCharter.Api.Domain;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Tests;

/// <summary>One test (at least) per business rule BR-1..BR-10.</summary>
public class CheckInRuleEngineTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);
    private static readonly GeoPoint Anchor = new(56.4580, -2.9800);
    private static readonly IReadOnlyList<GeoPoint> Campus =
        [new(56.4612, -2.9880), new(56.4612, -2.9725), new(56.4560, -2.9725), new(56.4545, -2.9800), new(56.4555, -2.9880)];
    private static readonly CheckInRuleOptions Options = new();

    private static readonly CheckInFacts Good = new()
    {
        DeviceFound = true, SignatureValid = true, Token = QrValidation.Valid,
        SessionStatus = SessionStatus.Open, SessionStartsAt = Now.AddMinutes(-30), SessionEndsAt = Now.AddMinutes(30), Now = Now,
        Enrolled = true, DeviceStatus = DeviceStatus.Active, DeviceUsedByAnotherStudentThisSession = false, AlreadyCheckedIn = false,
        Location = new GeoPoint(56.45805, -2.98000), AccuracyM = 10, IsMocked = false, Anchor = Anchor, CampusZones = [Campus]
    };

    private static CheckInDecision Run(CheckInFacts f) => CheckInRuleEngine.Evaluate(f, Options);

    [Fact]
    public void Evaluate_AllGood_Present()
    {
        var d = Run(Good);
        Assert.Equal(CheckInStatus.Present, d.Status);
        Assert.Empty(d.Flags);
        Assert.NotNull(d.DistanceFromAnchorM);
    }

    [Theory]
    [InlineData(QrValidation.Expired, RejectionReason.ExpiredToken)]
    [InlineData(QrValidation.BadSignature, RejectionReason.InvalidToken)]
    [InlineData(QrValidation.Malformed, RejectionReason.InvalidToken)]
    [InlineData(QrValidation.WrongSession, RejectionReason.InvalidToken)]
    public void Evaluate_BR1_BadToken_Rejected(QrValidation token, RejectionReason expected)
        => Assert.Equal(expected, Run(Good with { Token = token }).RejectionReason);

    [Fact]
    public void Evaluate_BR2_SessionClosed_Rejected()
        => Assert.Equal(RejectionReason.SessionNotOpen, Run(Good with { SessionStatus = SessionStatus.Closed }).RejectionReason);

    [Fact]
    public void Evaluate_BR2_AfterEndsAt_Rejected()
        => Assert.Equal(RejectionReason.SessionNotOpen, Run(Good with { SessionEndsAt = Now.AddSeconds(-1) }).RejectionReason);

    [Fact]
    public void Evaluate_BR3_NotEnrolled_Rejected()
        => Assert.Equal(RejectionReason.NotEnrolled, Run(Good with { Enrolled = false }).RejectionReason);

    [Fact]
    public void Evaluate_BR4_UnknownDevice_Rejected()
        => Assert.Equal(RejectionReason.UnknownDevice, Run(Good with { DeviceFound = false }).RejectionReason);

    [Fact]
    public void Evaluate_BR4_BadSignature_Rejected()
        => Assert.Equal(RejectionReason.InvalidSignature, Run(Good with { SignatureValid = false }).RejectionReason);

    [Theory]
    [InlineData(DeviceStatus.Pending)]
    [InlineData(DeviceStatus.Revoked)]
    public void Evaluate_BR4_InactiveDevice_Rejected(DeviceStatus status)
        => Assert.Equal(RejectionReason.DeviceNotActive, Run(Good with { DeviceStatus = status }).RejectionReason);

    [Fact]
    public void Evaluate_BR5_DeviceUsedForAnotherStudent_Rejected()
        => Assert.Equal(RejectionReason.DeviceAlreadyUsed, Run(Good with { DeviceUsedByAnotherStudentThisSession = true }).RejectionReason);

    [Fact]
    public void Evaluate_BR6_AlreadyCheckedIn_Duplicate()
    {
        var d = Run(Good with { AlreadyCheckedIn = true });
        Assert.True(d.IsDuplicate);
        Assert.Null(d.RejectionReason);
    }

    [Fact]
    public void Evaluate_BR7_OffCampus_Rejected()
        => Assert.Equal(RejectionReason.OffCampus, Run(Good with { Location = new GeoPoint(55.9533, -3.1883) }).RejectionReason);

    [Fact]
    public void Evaluate_BR7_JustOutsideButWithinAccuracy_Accepted()
    {
        // ~40 m north of the campus edge, reporting 50 m accuracy → benefit of the doubt (flagged unverified at most).
        var d = Run(Good with { Location = new GeoPoint(56.46156, -2.9800), AccuracyM = 50 });
        Assert.NotEqual(CheckInStatus.Rejected, d.Status);
    }

    [Fact]
    public void Evaluate_BR7_AccuracyAllowanceIsCapped()
    {
        // 5 km accuracy must not let a fix 1 km off campus through.
        var d = Run(Good with { Location = new GeoPoint(56.4700, -2.9800), AccuracyM = 5000 });
        Assert.Equal(RejectionReason.OffCampus, d.RejectionReason);
    }

    [Fact]
    public void Evaluate_BR7_NoZonesConfigured_Skipped()
        => Assert.NotEqual(CheckInStatus.Rejected, Run(Good with { CampusZones = [], Location = new GeoPoint(55.9533, -3.1883) }).Status);

    [Fact]
    public void Evaluate_BR8_TwoHundredMetresAway_FlaggedFarFromLecturer()
    {
        var d = Run(Good with { Location = new GeoPoint(56.4598, -2.9800) });   // ~200 m north, still on campus
        Assert.Equal(CheckInStatus.Flagged, d.Status);
        Assert.Contains(CheckInFlag.FarFromLecturer, d.Flags);
        Assert.InRange(d.DistanceFromAnchorM!.Value, 190, 210);
    }

    [Fact]
    public void Evaluate_BR9_PoorAccuracy_FlaggedUnverified_NotFar()
    {
        var d = Run(Good with { Location = new GeoPoint(56.4598, -2.9800), AccuracyM = 80 });
        Assert.Contains(CheckInFlag.LocationUnverified, d.Flags);
        Assert.DoesNotContain(CheckInFlag.FarFromLecturer, d.Flags);
    }

    [Fact]
    public void Evaluate_BR10_Mocked_Flagged()
    {
        var d = Run(Good with { IsMocked = true });
        Assert.Equal(CheckInStatus.Flagged, d.Status);
        Assert.Contains(CheckInFlag.MockLocation, d.Flags);
    }

    [Fact]
    public void Evaluate_HardChecksRunBeforeSoft_MockedButOffCampusIsRejected()
        => Assert.Equal(CheckInStatus.Rejected, Run(Good with { IsMocked = true, Location = new GeoPoint(55.9533, -3.1883) }).Status);

    [Fact]
    public void Evaluate_PipelineOrder_SignatureBeforeToken()
        => Assert.Equal(RejectionReason.InvalidSignature, Run(Good with { SignatureValid = false, Token = QrValidation.Expired }).RejectionReason);
}
