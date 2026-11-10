using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using SeatCharter.Api.Helpers.Attributes;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Models;

[Table("Users", Schema = "dbo")]
[Auditable]
public class User : IdentityUser
{
    [Required, MaxLength(200)]
    public string FullName { get; set; } = null!;

    [Required]
    public UserGroup UserGroup { get; set; }

    /// <summary>University matriculation number. Students only; unique when set.</summary>
    [MaxLength(30)]
    public string? MatricNo { get; set; }

    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = false;          // true only for accounts created with a temp password
    public bool HasPassword { get; set; } = true;
    public DateTime? PasswordChangedAt { get; set; }

    public bool MfaEnabled { get; set; } = false;
    public MfaMethod MfaMethod { get; set; } = MfaMethod.None;
    /// <summary>AES-GCM ciphertext, never plaintext.</summary>
    public string? MfaSecret { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}
