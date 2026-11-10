namespace SeatCharter.Api.Helpers;

public static class Enums
{
    public enum UserGroup
    {
        Admin,
        Lecturer,
        Student
    }

    public enum DeviceStatus
    {
        Pending,
        Active,
        Revoked
    }

    public enum SessionStatus
    {
        Scheduled,
        Open,
        Closed
    }

    public enum CheckInStatus
    {
        Present,
        Flagged,
        Rejected,
        Absent
    }

    public enum PresenceCheckKind
    {
        GeofenceExit,
        Prompt70
    }

    public enum PresenceCheckResult
    {
        Present,
        Left,
        NoResponse
    }

    public enum MfaMethod
    {
        None,
        Email,
        Totp
    }

    /// <summary>Hard-check failures (BR-1..BR-7). One per rejected check-in.</summary>
    public enum RejectionReason
    {
        UnknownDevice,
        InvalidSignature,
        InvalidToken,
        ExpiredToken,
        SessionNotOpen,
        NotEnrolled,
        DeviceNotActive,
        DeviceAlreadyUsed,
        OffCampus
    }

    /// <summary>Soft-check flags (BR-8..BR-12). A check-in can carry several.</summary>
    public enum CheckInFlag
    {
        FarFromLecturer,
        LocationUnverified,
        MockLocation,
        ClusterOutlier,
        LeftEarly,
        NoPresenceResponse
    }

    public enum QrValidation
    {
        Valid,
        Malformed,
        WrongSession,
        Expired,
        BadSignature
    }

    public enum ReviewDecision
    {
        Present,
        Absent
    }

    public enum TimetableStatus
    {
        Upcoming,
        Open,
        Closed,
        Ended
    }

    /// <summary>Student-facing outcome for one session.</summary>
    public enum AttendanceOutcome
    {
        Present,
        Flagged,
        Absent,
        Pending
    }
}
