using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SeatCharter.Api.Helpers.Attributes;

namespace SeatCharter.Api.Models;

[Table("Courses", Schema = "dbo")]
[Auditable]
public class Course
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(20)]
    public string Code { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Title { get; set; } = null!;

    public string LecturerId { get; set; } = null!;
    [ForeignKey(nameof(LecturerId))]
    public User Lecturer { get; set; } = null!;

    /// <summary>Students self-enrol with this code. Unique among live courses.</summary>
    [Required, MaxLength(10)]
    public string JoinCode { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public ICollection<Enrolment> Enrolments { get; set; } = [];
    public ICollection<Schedule> Schedules { get; set; } = [];
}
