using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SeatCharter.Api.Helpers.Attributes;

namespace SeatCharter.Api.Models;

/// <summary>Recurring weekly slot. Times are wall-clock in AttendanceSettings.TimeZoneId.</summary>
[Table("Schedules", Schema = "dbo")]
[Auditable]
public class Schedule
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string CourseId { get; set; } = null!;
    [ForeignKey(nameof(CourseId))]
    public Course Course { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    [MaxLength(100)]
    public string? Room { get; set; }

    public double? RoomLat { get; set; }
    public double? RoomLng { get; set; }
    public double? RoomRadiusM { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}
