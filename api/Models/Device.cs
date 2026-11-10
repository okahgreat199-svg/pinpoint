using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Models;

/// <summary>A phone's keypair bound to one user (BR-13, BR-14). Approvals/resets write explicit AuditLog rows.</summary>
[Table("Devices", Schema = "dbo")]
public class Device
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string UserId { get; set; } = null!;
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    /// <summary>Base64 ECDSA P-256 public key. The private key never leaves the phone.</summary>
    [Required, MaxLength(512)]
    public string PublicKey { get; set; } = null!;

    /// <summary>OS-level install identifier (e.g. Android ID). Lets BR-14 catch a second account on the same phone even with a fresh keypair.</summary>
    [MaxLength(200)]
    public string? HardwareId { get; set; }

    [Required, MaxLength(50)]
    public string Platform { get; set; } = null!;
    [Required, MaxLength(200)]
    public string Model { get; set; } = null!;

    public DeviceStatus Status { get; set; } = DeviceStatus.Pending;

    /// <summary>FCM registration token for the 70% prompt.</summary>
    [MaxLength(4096)]
    public string? PushToken { get; set; }

    public DateTime BoundAt { get; set; } = DateTime.UtcNow;

    public string? ApprovedById { get; set; }
    [ForeignKey(nameof(ApprovedById))]
    public User? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}
