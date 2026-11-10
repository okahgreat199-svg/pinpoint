using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.DTOs;

/// <summary>Scoped per-request identity, hydrated by [ValidateUser] from the DB snapshot (never the JWT).</summary>
public class UserInfoToken
{
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? UsersGroup { get; set; }
    public List<string>? Permissions { get; set; }
}

public static class UserInfoTokenExtensions
{
    /// <summary>Use at every FK write. A null means the endpoint is mis-gated: fail loudly.</summary>
    public static string RequireUserId(this UserInfoToken u) => string.IsNullOrWhiteSpace(u.UserId)
        ? throw new InvalidOperationException("UserInfoToken.UserId is not populated. Endpoint must be gated by [ValidateUser].")
        : u.UserId;

    public static bool IsAdmin(this UserInfoToken u) => u.UsersGroup == nameof(UserGroup.Admin);
    public static bool IsLecturer(this UserInfoToken u) => u.UsersGroup == nameof(UserGroup.Lecturer);
    public static bool IsStudent(this UserInfoToken u) => u.UsersGroup == nameof(UserGroup.Student);
}
