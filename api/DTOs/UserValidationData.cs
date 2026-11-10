using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.DTOs;

/// <summary>Cached identity snapshot loaded by [ValidateUser] from IUserCacheService (or DB fallback).</summary>
public class UserValidationData
{
    public string UserId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public UserGroup UserGroup { get; set; }
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    /// <summary>Flattened "MODULE:Permission" grants. Unused today (roles suffice) but kept so the filter contract matches.</summary>
    public List<string> Permissions { get; set; } = [];
}
