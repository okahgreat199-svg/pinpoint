using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SeatCharter.Api.Models;

[Table("RefreshTokens", Schema = "dbo")]
public class RefreshToken
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string UserId { get; set; } = null!;
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public string CreatedByIp { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
