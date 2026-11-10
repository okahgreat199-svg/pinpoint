using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Models;

/// <summary>Evidence for BR-12: a geofence-exit event, a prompt answer, or a prompt non-response.</summary>
[Table("PresenceChecks", Schema = "dbo")]
public class PresenceCheck
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string CheckInId { get; set; } = null!;
    [ForeignKey(nameof(CheckInId))]
    public CheckIn CheckIn { get; set; } = null!;

    public PresenceCheckKind Kind { get; set; }

    public DateTime At { get; set; } = DateTime.UtcNow;

    /// <summary>Nullable: not every event carries a fix, and coordinates are purged after 30 days.</summary>
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public double? AccuracyM { get; set; }

    public PresenceCheckResult Result { get; set; }
}
