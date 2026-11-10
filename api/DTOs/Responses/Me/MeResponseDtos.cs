namespace SeatCharter.Api.DTOs.Responses.Me;

public class TimetableEntryDto
{
    public string CourseId { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string? ScheduleId { get; set; }
    public string? SessionId { get; set; }
    public string? Room { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    /// <summary>Upcoming, Open, Closed or Ended (past slot with no session).</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Students only: their check-in status for this session, if any.</summary>
    public string? MyCheckInStatus { get; set; }
}

public class MyAttendanceDto
{
    public List<CourseAttendanceDto> Courses { get; set; } = [];
}

public class CourseAttendanceDto
{
    public string CourseId { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    /// <summary>Closed sessions only.</summary>
    public int TotalSessions { get; set; }
    public int Present { get; set; }
    public int Flagged { get; set; }
    public int Absent { get; set; }
    /// <summary>(Present + Flagged) / TotalSessions × 100. Flags are pending review, not absences.</summary>
    public double AttendancePercentage { get; set; }
    public List<MySessionAttendanceDto> Sessions { get; set; } = [];
}

public class MySessionAttendanceDto
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string SessionStatus { get; set; } = string.Empty;
    /// <summary>Present, Flagged, Absent, or Pending (session still open, not checked in).</summary>
    public string Outcome { get; set; } = string.Empty;
    public string? CheckInId { get; set; }
    /// <summary>Students can see the reason for any flag.</summary>
    public List<string> Flags { get; set; } = [];
    public string? LastRejectionReason { get; set; }
    public bool Reviewed { get; set; }
}
