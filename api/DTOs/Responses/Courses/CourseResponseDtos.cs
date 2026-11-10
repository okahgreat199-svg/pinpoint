namespace SeatCharter.Api.DTOs.Responses.Courses;

public class CourseDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string LecturerId { get; set; } = string.Empty;
    public string LecturerName { get; set; } = string.Empty;
    /// <summary>Only returned to the owning lecturer and admins.</summary>
    public string? JoinCode { get; set; }
    public int StudentCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EnrolledStudentDto
{
    public string StudentId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? MatricNo { get; set; }
    public DateTime EnrolledAt { get; set; }
}

public class EnrolStudentsResultDto
{
    public List<string> Enrolled { get; set; } = [];
    public List<string> AlreadyEnrolled { get; set; } = [];
    public List<string> NotFound { get; set; } = [];
}

public class ScheduleDto
{
    public string Id { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Room { get; set; }
    public double? RoomLat { get; set; }
    public double? RoomLng { get; set; }
    public double? RoomRadiusM { get; set; }
}
