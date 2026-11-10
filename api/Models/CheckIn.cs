using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Models;

/// <summary>
/// One row per check-in attempt, including rejected ones, so the lecturer can see them.
/// Not [Auditable]: rows are written by the pipeline; lecturer reviews write an explicit AuditLog row instead.
/// </summary>
[Table("CheckIns", Schema = "dbo")]
public class CheckIn
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string SessionId { get; set; } = null!;
    [ForeignKey(nameof(SessionId))]
    public Session Session { get; set; } = null!;

    public string StudentId { get; set; } = null!;
    [ForeignKey(nameof(StudentId))]
    public User Student { get; set; } = null!;

    /// <summary>Null only on rejected attempts from a device we don't recognise.</summary>
    public string? DeviceId { get; set; }
    [ForeignKey(nameof(DeviceId))]
    public Device? Device { get; set; }

    /// <summary>Server receipt time; the only time the pipeline trusts.</summary>
    public DateTime At { get; set; } = DateTime.UtcNow;
    /// <summary>Phone's clock as reported. Informational only.</summary>
    public DateTime? ClientTime { get; set; }

    /// <summary>Nullable because raw coordinates are purged 30 days after session close (privacy).</summary>
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public double AccuracyM { get; set; }
    public bool IsMocked { get; set; }

    public double? DistanceFromAnchorM { get; set; }

    public CheckInStatus Status { get; set; }

    /// <summary>Soft-check flags (CheckInFlag names). Stored as text[].</summary>
    public List<string> Flags { get; set; } = [];

    public RejectionReason? RejectionReason { get; set; }

    public string? ReviewedById { get; set; }
    [ForeignKey(nameof(ReviewedById))]
    public User? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [MaxLength(500)]
    public string? ReviewNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PresenceCheck> PresenceChecks { get; set; } = [];

    /// <summary>
    /// Adds a soft flag. Present becomes Flagged; a lecturer's Absent decision is never overridden.
    /// </summary>
    public void AddFlag(CheckInFlag flag)
    {
        var name = flag.ToString();
        if (!Flags.Contains(name)) Flags = [.. Flags, name];   // reassign so EF sees the collection change
        if (Status == CheckInStatus.Present) Status = CheckInStatus.Flagged;
    }
}
