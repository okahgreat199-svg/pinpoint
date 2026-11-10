using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.DTOs.Requests.Auth;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Auth;
using SeatCharter.Api.Helpers.Attributes;

namespace SeatCharter.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    private string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>POST /auth/register — student, or lecturer with the invite code</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [RequireAppCheck]
    [EnableRateLimiting("auth-tight")]
    [ProducesResponseType(typeof(ApiResponseBase<AuthResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _auth.RegisterAsync(request, ClientIp);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /auth/login — access token (15 min) + refresh token (7 days)</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-tight")]
    [ProducesResponseType(typeof(ApiResponseBase<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _auth.LoginAsync(request, ClientIp);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /auth/refresh — rotate the refresh token</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseBase<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var result = await _auth.RefreshAsync(request, ClientIp);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /auth/logout — revoke the presented refresh token</summary>
    [HttpPost("logout")]
    [ValidateUser]
    [ProducesResponseType(typeof(ApiResponseBase<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var result = await _auth.LogoutAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /auth/change-password — reachable while MustChangePassword is set</summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponseBase<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var result = await _auth.ChangePasswordAsync(User.FindFirst("sub")!.Value, request);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>GET /auth/me — profile + current device status</summary>
    [HttpGet("me")]
    [ValidateUser]
    [ProducesResponseType(typeof(ApiResponseBase<UserProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
    {
        var result = await _auth.GetMeAsync();
        return StatusCode(result.StatusCode, result);
    }
}
