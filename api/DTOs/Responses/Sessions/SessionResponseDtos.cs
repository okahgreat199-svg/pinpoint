namespace SeatCharter.Api.DTOs.Responses.Sessions;

public class SessionDto
{
    public string Id { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string? ScheduleId { get; set; }
    public string? Room { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public double AnchorLat { get; set; }
    public double AnchorLng { get; set; }
    public double AnchorAccuracyM { get; set; }
    public int StepSeconds { get; set; }
    public DateTime? PromptSentAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

/// <summary>What the lecturer's live-QR screen draws. Poll every few seconds.</summary>
public class SessionQrDto
{
    public string SessionId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    /// <summary>What to draw in the QR: a deep link (https://host/c/{token}) when DeepLink:PublicBaseUrl is set, else the bare token.</summary>
    public string QrPayload { get; set; } = string.Empty;
    /// <summary>6-digit fallback for students who can't scan (same rotation as the token).</summary>
    public string ManualCode { get; set; } = string.Empty;
    public long Step { get; set; }
    public int StepSeconds { get; set; }
    public int SecondsRemaining { get; set; }
    public DateTime ServerTime { get; set; }
    public DateTime EndsAt { get; set; }
    public int CheckedInCount { get; set; }
    public int EnrolledCount { get; set; }
}

public class SessionAttendanceDto
{
    public SessionDto Session { get; set; } = null!;
    public int Enrolled { get; set; }
    public int Present { get; set; }
    public int Flagged { get; set; }
    public int Absent { get; set; }
    public int RejectedAttempts { get; set; }
    public List<AttendanceRosterRowDto> Roster { get; set; } = [];
    /// <summary>Every rejected attempt, including from students who aren't enrolled.</summary>
    public List<RejectedAttemptDto> Rejected { get; set; } = [];
}

public class AttendanceRosterRowDto
{
    public string StudentId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? MatricNo { get; set; }
    public string? CheckInId { get; set; }
    /// <summary>Present, Flagged or Absent. No check-in yet = Absent.</summary>
    public string Status { get; set; } = string.Empty;
    public List<string> Flags { get; set; } = [];
    public DateTime? At { get; set; }
    public double? DistanceFromAnchorM { get; set; }
    public double? AccuracyM { get; set; }
    public bool IsMocked { get; set; }
    public int RejectedAttempts { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public List<PresenceEventDto> PresenceEvents { get; set; } = [];
}

public class RejectedAttemptDto
{
    public string CheckInId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime At { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class PresenceEventDto
{
    public string Kind { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTime At { get; set; }
}

public class PromptResultDto
{
    public int Recipients { get; set; }
    public int Delivered { get; set; }
    public DateTime PromptSentAt { get; set; }
}
