using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using SeatCharter.Api.Helpers.Attributes;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Models;

[Table("Sessions", Schema = "dbo")]
[Auditable]
public class Session
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string CourseId { get; set; } = null!;
    [ForeignKey(nameof(CourseId))]
    public Course Course { get; set; } = null!;

    /// <summary>Optional link to the recurring schedule slot this session belongs to (null = ad-hoc session).</summary>
    public string? ScheduleId { get; set; }
    [ForeignKey(nameof(ScheduleId))]
    public Schedule? Schedule { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

    /// <summary>32 random bytes, base64. Never leaves the server: only tokens derived from it reach the lecturer's display.</summary>
    [Required, MaxLength(64)]
    public string QrSecret { get; set; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>Lecturer's location when the session started; the reference point for BR-8.</summary>
    public double AnchorLat { get; set; }
    public double AnchorLng { get; set; }
    public double AnchorAccuracyM { get; set; }

    /// <summary>QR rotation interval in seconds (BR-1).</summary>
    public int StepSeconds { get; set; } = 15;

    /// <summary>When the 70% presence prompt went out (BR-12). Null = not yet sent.</summary>
    public DateTime? PromptSentAt { get; set; }
    /// <summary>When non-responders to the prompt were flagged. Guards the job from running twice.</summary>
    public DateTime? PromptTimeoutProcessedAt { get; set; }

    public DateTime? ClosedAt { get; set; }
    /// <summary>When raw coordinates for this session were deleted (30-day retention).</summary>
    public DateTime? LocationsPurgedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public ICollection<CheckIn> CheckIns { get; set; } = [];
}
