using System.ComponentModel.DataAnnotations;

namespace SeatCharter.Api.DTOs.Requests.Courses;

public class CreateCourseRequest
{
    [Required, StringLength(20, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;
}

public class UpdateCourseRequest
{
    [Required, StringLength(20, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;
}

public class JoinCourseRequest
{
    [Required, StringLength(10, MinimumLength = 4)]
    public string JoinCode { get; set; } = string.Empty;
}

public class EnrolStudentsRequest
{
    /// <summary>Student emails or matric numbers.</summary>
    [Required, MinLength(1), MaxLength(500)]
    public List<string> Identifiers { get; set; } = [];
}

public class CreateScheduleRequest
{
    [Required]
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>"HH:mm" or "HH:mm:ss", local time.</summary>
    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }

    [StringLength(100)]
    public string? Room { get; set; }

    [Range(-90, 90)]
    public double? RoomLat { get; set; }

    [Range(-180, 180)]
    public double? RoomLng { get; set; }

    [Range(10, 1000)]
    public double? RoomRadiusM { get; set; }
}
