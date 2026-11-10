using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SeatCharter.Api.Helpers.Attributes;

namespace SeatCharter.Api.Models;

/// <summary>Campus geofence for BR-7. A check-in passes if it falls in any zone.</summary>
[Table("CampusZones", Schema = "dbo")]
[Auditable]
public class CampusZone
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;

    /// <summary>JSON array of [lat, lng] pairs, in order.</summary>
    [Required]
    public string PolygonJson { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}
