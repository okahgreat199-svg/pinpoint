namespace SeatCharter.Api.DTOs.Responses.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    /// <summary>Access-token lifetime in minutes.</summary>
    public int ExpiresIn { get; set; }
    public UserProfileDto User { get; set; } = null!;
}

public class UserProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string UserGroup { get; set; } = string.Empty;
    public string? MatricNo { get; set; }
    /// <summary>Status of the caller's current device binding, if any (Active/Pending). Lets the app pick the right screen after login.</summary>
    public string? DeviceStatus { get; set; }
}
