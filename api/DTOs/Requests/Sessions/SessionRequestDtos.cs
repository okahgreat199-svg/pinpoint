using System.ComponentModel.DataAnnotations;

namespace SeatCharter.Api.DTOs.Requests.Sessions;

public class StartSessionRequest
{
    [Required]
    public string CourseId { get; set; } = string.Empty;

    /// <summary>Optional schedule slot. When set and DurationMinutes is omitted, the session ends at the slot's end time.</summary>
    public string? ScheduleId { get; set; }

    [Required, Range(-90, 90)]
    public double AnchorLat { get; set; }

    [Required, Range(-180, 180)]
    public double AnchorLng { get; set; }

    [Required, Range(0, 10000)]
    public double AnchorAccuracyM { get; set; }

    /// <summary>The lecturer's phone reported a mock location. A session can't be anchored on one.</summary>
    public bool IsMocked { get; set; }

    /// <summary>The lecturer confirmed starting outside the slot's time. Without it an off-schedule start returns 428 with a warning.</summary>
    public bool ConfirmOffSchedule { get; set; }

    [Range(5, 480)]
    public int? DurationMinutes { get; set; }
}
