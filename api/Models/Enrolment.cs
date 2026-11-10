using System.ComponentModel.DataAnnotations.Schema;

namespace SeatCharter.Api.Models;

[Table("Enrolments", Schema = "dbo")]
public class Enrolment
{
    public string CourseId { get; set; } = null!;
    [ForeignKey(nameof(CourseId))]
    public Course Course { get; set; } = null!;

    public string StudentId { get; set; } = null!;
    [ForeignKey(nameof(StudentId))]
    public User Student { get; set; } = null!;

    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
}
