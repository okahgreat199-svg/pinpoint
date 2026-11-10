using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Domain;

/// <summary>
/// Everything the pipeline needs, pre-loaded by the service. The engine itself does no I/O,
/// so each business rule is unit-testable.
/// </summary>
public sealed record CheckInFacts
{
    // BR-4 (part 1): device lookup + signature
    public bool DeviceFound { get; init; }
    public bool SignatureValid { get; init; }

    // BR-1
    public QrValidation Token { get; init; }

    // BR-2
    public SessionStatus SessionStatus { get; init; }
    public DateTime SessionStartsAt { get; init; }
    public DateTime SessionEndsAt { get; init; }
    public DateTime Now { get; init; }

    // BR-3
    public bool Enrolled { get; init; }

    // BR-4 (part 2) + BR-5
    public DeviceStatus DeviceStatus { get; init; }
    public bool DeviceUsedByAnotherStudentThisSession { get; init; }

    // BR-6
    public bool AlreadyCheckedIn { get; init; }

    // Location (BR-7..BR-10)
    public GeoPoint Location { get; init; }
    public double AccuracyM { get; init; }
    public bool IsMocked { get; init; }
    public GeoPoint Anchor { get; init; }
    public IReadOnlyList<IReadOnlyList<GeoPoint>> CampusZones { get; init; } = [];
}

public sealed class CheckInRuleOptions
{
    public double AnchorFlagDistanceM { get; init; } = 75;
    public double PoorAccuracyM { get; init; } = 50;
    /// <summary>Cap on how much reported accuracy can widen the campus fence, so a 5 km fix can't pass BR-7.</summary>
    public double MaxCampusAllowanceM { get; init; } = 100;
}

public sealed record CheckInDecision(
    CheckInStatus Status,
    RejectionReason? RejectionReason,
    IReadOnlyList<CheckInFlag> Flags,
    double? DistanceFromAnchorM,
    bool IsDuplicate)
{
    public static CheckInDecision Reject(RejectionReason reason) => new(CheckInStatus.Rejected, reason, [], null, false);
    public static CheckInDecision Duplicate() => new(CheckInStatus.Present, null, [], null, true);
}

public static class CheckInRuleEngine
{
    /// <summary>
    /// Pipeline order (BRD §4): signature → token → session → enrolment → device rules → duplicate → campus (hard),
    /// then anchor distance, accuracy, mock (soft). Hard checks reject; soft checks only flag.
    /// </summary>
    public static CheckInDecision Evaluate(CheckInFacts f, CheckInRuleOptions o)
    {
        if (!f.DeviceFound) return CheckInDecision.Reject(RejectionReason.UnknownDevice);
        if (!f.SignatureValid) return CheckInDecision.Reject(RejectionReason.InvalidSignature);

        switch (f.Token)
        {
            case QrValidation.Valid: break;
            case QrValidation.Expired: return CheckInDecision.Reject(RejectionReason.ExpiredToken);
            default: return CheckInDecision.Reject(RejectionReason.InvalidToken);
        }

        if (f.SessionStatus != SessionStatus.Open || f.Now < f.SessionStartsAt || f.Now > f.SessionEndsAt)
            return CheckInDecision.Reject(RejectionReason.SessionNotOpen);

        if (!f.Enrolled) return CheckInDecision.Reject(RejectionReason.NotEnrolled);

        if (f.DeviceStatus != DeviceStatus.Active) return CheckInDecision.Reject(RejectionReason.DeviceNotActive);
        if (f.DeviceUsedByAnotherStudentThisSession) return CheckInDecision.Reject(RejectionReason.DeviceAlreadyUsed);

        if (f.AlreadyCheckedIn) return CheckInDecision.Duplicate();

        if (f.CampusZones.Count > 0)
        {
            var allowance = Math.Clamp(f.AccuracyM, 0, o.MaxCampusAllowanceM);
            if (!f.CampusZones.Any(z => GeoMath.IsInsideWithAllowance(f.Location, z, allowance)))
                return CheckInDecision.Reject(RejectionReason.OffCampus);
        }

        // ── Soft checks ──
        var flags = new List<CheckInFlag>();
        var distance = GeoMath.HaversineM(f.Location, f.Anchor);

        if (f.AccuracyM > o.PoorAccuracyM)
            flags.Add(CheckInFlag.LocationUnverified);          // BR-9: too noisy to call it far; unverified instead
        else if (distance > o.AnchorFlagDistanceM)
            flags.Add(CheckInFlag.FarFromLecturer);             // BR-8

        if (f.IsMocked) flags.Add(CheckInFlag.MockLocation);    // BR-10

        return new CheckInDecision(
            flags.Count == 0 ? CheckInStatus.Present : CheckInStatus.Flagged,
            null, flags, Math.Round(distance, 1), false);
    }
}
