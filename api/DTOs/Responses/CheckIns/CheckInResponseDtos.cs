namespace SeatCharter.Api.DTOs.Responses.CheckIns;

public class CheckInResultDto
{
    /// <summary>Null when the attempt couldn't be tied to a session (unreadable token).</summary>
    public string? CheckInId { get; set; }
    public string? SessionId { get; set; }
    /// <summary>Present, Flagged or Rejected.</summary>
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public List<string> Flags { get; set; } = [];
    /// <summary>Plain-English explanation for the result screen (and TalkBack).</summary>
    public string Message { get; set; } = string.Empty;
    public DateTime At { get; set; }
    public double? DistanceFromAnchorM { get; set; }
    public bool IsDuplicate { get; set; }
    /// <summary>For the room geofence (flow 2): anchor point, radius and session end.</summary>
    public double? AnchorLat { get; set; }
    public double? AnchorLng { get; set; }
    public double? GeofenceRadiusM { get; set; }
    public DateTime? SessionEndsAt { get; set; }
}

public class CheckInDto
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<string> Flags { get; set; } = [];
    public string? RejectionReason { get; set; }
    public DateTime At { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedById { get; set; }
    public string? ReviewNote { get; set; }
}

public class PresenceResultDto
{
    public string CheckInId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string CheckInStatus { get; set; } = string.Empty;
    public List<string> Flags { get; set; } = [];
}
