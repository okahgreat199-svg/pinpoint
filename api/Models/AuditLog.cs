using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SeatCharter.Api.Models;

[Table("AuditLogs", Schema = "dbo")]
public class AuditLog
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Null for system-initiated actions.</summary>
    public string? ActorId { get; set; }
    [ForeignKey(nameof(ActorId))]
    public User? Actor { get; set; }

    [Required, MaxLength(100)]
    public string Action { get; set; } = null!;

    [Required, MaxLength(100)]
    public string EntityType { get; set; } = null!;

    [Required]
    public string EntityId { get; set; } = null!;

    public DateTime At { get; set; } = DateTime.UtcNow;

    /// <summary>JSON snapshot of old/new values.</summary>
    public string? DataJson { get; set; }
}
