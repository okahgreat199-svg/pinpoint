using System.IdentityModel.Tokens.Jwt;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.Data;
using SeatCharter.Api.DTOs;
using SeatCharter.Api.DTOs.Requests.Auth;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Auth;
using SeatCharter.Api.Models;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.BusinessLogic.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly UserInfoToken _userInfo;
    private readonly JwtSettings _jwt;
    private readonly AttendanceSettings _attendance;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuthService> _logger;

    public AuthService(ApplicationDbContext db, UserManager<User> userManager, UserInfoToken userInfo,
        JwtSettings jwt, AttendanceSettings attendance, TimeProvider clock, ILogger<AuthService> logger)
    {
        _db = db;
        _userManager = userManager;
        _userInfo = userInfo;
        _jwt = jwt;
        _attendance = attendance;
        _clock = clock;
        _logger = logger;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private ApiResponseBase<T> Fail500<T>(Exception ex, [CallerMemberName] string method = "")
    {
        _logger.LogError(ex, "{Method} failed", method);
        return ApiResponseBase<T>.ServerError("An error occurred. Please try again.");
    }

    public async Task<ApiResponseBase<AuthResponse>> RegisterAsync(RegisterRequest request, string clientIp)
    {
        try
        {
            if (request.Role is not (UserGroup.Lecturer or UserGroup.Student))
                return ApiResponseBase<AuthResponse>.BadRequest("Role must be Lecturer or Student.");

            if (request.Role == UserGroup.Lecturer)
            {
                if (string.IsNullOrEmpty(_attendance.LecturerInviteCode))
                    return ApiResponseBase<AuthResponse>.Forbidden("Lecturer self-registration is disabled.");
                if (!FixedTimeEquals(request.InviteCode, _attendance.LecturerInviteCode))
                    return ApiResponseBase<AuthResponse>.Forbidden("The lecturer invite code is not valid.");
            }

            var matricNo = string.IsNullOrWhiteSpace(request.MatricNo) ? null : request.MatricNo.Trim().ToUpperInvariant();
            if (request.Role == UserGroup.Student && matricNo is null)
                return ApiResponseBase<AuthResponse>.BadRequest("Matric number is required for students.");

            var email = request.Email.Trim().ToLowerInvariant();
            if (await _userManager.FindByEmailAsync(email) is not null)
                return ApiResponseBase<AuthResponse>.Conflict("An account with this email already exists.");
            if (matricNo is not null && await _db.Users.AnyAsync(u => u.MatricNo == matricNo))
                return ApiResponseBase<AuthResponse>.Conflict("An account with this matric number already exists.");

            var user = new User
            {
                UserName = email,
                Email = email,
                FullName = request.FullName.Trim(),
                UserGroup = request.Role,
                MatricNo = request.Role == UserGroup.Student ? matricNo : null,
                MustChangePassword = false,
                PasswordChangedAt = Now
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return ApiResponseBase<AuthResponse>.BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)));

            _logger.LogInformation("User {UserId} registered as {UserGroup}", user.Id, user.UserGroup);
            var auth = await IssueTokensAsync(user, clientIp);
            return ApiResponseBase<AuthResponse>.Created(auth, "Account created.");
        }
        catch (Exception ex) { return Fail500<AuthResponse>(ex); }
    }

    public async Task<ApiResponseBase<AuthResponse>> LoginAsync(LoginRequest request, string clientIp)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
            if (user is null)
                return ApiResponseBase<AuthResponse>.Unauthorized("Invalid email or password.");

            if (await _userManager.IsLockedOutAsync(user))
                return ApiResponseBase<AuthResponse>.Forbidden("Too many failed attempts. Try again in 15 minutes.");

            if (!await _userManager.CheckPasswordAsync(user, request.Password))
            {
                await _userManager.AccessFailedAsync(user);
                return ApiResponseBase<AuthResponse>.Unauthorized("Invalid email or password.");
            }

            if (!user.IsActive)
                return ApiResponseBase<AuthResponse>.Forbidden("Your account has been deactivated.");

            await _userManager.ResetAccessFailedCountAsync(user);

            // One active refresh token per user: a fresh login revokes the rest.
            await _db.RefreshTokens
                .Where(t => t.UserId == user.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, Now));

            var auth = await IssueTokensAsync(user, clientIp);
            return ApiResponseBase<AuthResponse>.Ok(auth, "Signed in.");
        }
        catch (Exception ex) { return Fail500<AuthResponse>(ex); }
    }

    public async Task<ApiResponseBase<AuthResponse>> RefreshAsync(RefreshRequest request, string clientIp)
    {
        try
        {
            var userId = ReadUserIdFromExpiredToken(request.AccessToken);
            if (userId is null)
                return ApiResponseBase<AuthResponse>.Unauthorized("Invalid token.");

            var hash = HashToken(request.RefreshToken);
            var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == userId && t.TokenHash == hash);
            if (stored is null || stored.ExpiresAt <= Now)
                return ApiResponseBase<AuthResponse>.Unauthorized("Session expired. Please sign in again.");

            if (stored.RevokedAt is not null)
            {
                // A rotated-out token came back: assume it was stolen and end every session for this user.
                _logger.LogWarning("Refresh token reuse detected for {UserId}", userId);
                await _db.RefreshTokens
                    .Where(t => t.UserId == userId && t.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, Now));
                return ApiResponseBase<AuthResponse>.Unauthorized("Session expired. Please sign in again.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || !user.IsActive)
                return ApiResponseBase<AuthResponse>.Unauthorized("Session expired. Please sign in again.");

            stored.RevokedAt = Now;
            var auth = await IssueTokensAsync(user, clientIp);   // saves the revoke and the new token together
            return ApiResponseBase<AuthResponse>.Ok(auth, "Token refreshed.");
        }
        catch (Exception ex) { return Fail500<AuthResponse>(ex); }
    }

    public async Task<ApiResponseBase<bool>> LogoutAsync(LogoutRequest request)
    {
        try
        {
            var userId = _userInfo.RequireUserId();
            var hash = HashToken(request.RefreshToken);
            var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == userId && t.TokenHash == hash && t.RevokedAt == null);
            if (stored is not null)
            {
                stored.RevokedAt = Now;
                await _db.SaveChangesAsync();
            }
            return ApiResponseBase<bool>.Ok(true, "Signed out.");
        }
        catch (Exception ex) { return Fail500<bool>(ex); }
    }

    public async Task<ApiResponseBase<bool>> ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return ApiResponseBase<bool>.NotFound("User not found.");

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
                return ApiResponseBase<bool>.BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)));

            user.MustChangePassword = false;
            user.PasswordChangedAt = Now;
            await _userManager.UpdateAsync(user);
            return ApiResponseBase<bool>.Ok(true, "Password changed.");
        }
        catch (Exception ex) { return Fail500<bool>(ex); }
    }

    public async Task<ApiResponseBase<UserProfileDto>> GetMeAsync()
    {
        try
        {
            var userId = _userInfo.RequireUserId();
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return ApiResponseBase<UserProfileDto>.NotFound("User not found.");
            return ApiResponseBase<UserProfileDto>.Ok(await MapProfileAsync(user), "Profile retrieved.");
        }
        catch (Exception ex) { return Fail500<UserProfileDto>(ex); }
    }

    // ── helpers ──

    private async Task<AuthResponse> IssueTokensAsync(User user, string clientIp)
    {
        var (raw, hashed) = GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hashed,
            ExpiresAt = Now.AddDays(_jwt.RefreshTokenExpiryDays),
            CreatedByIp = clientIp
        });
        await _db.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = GenerateAccessToken(user),
            RefreshToken = raw,
            ExpiresIn = _jwt.MinutesToExpiration,
            User = await MapProfileAsync(user)
        };
    }

    private async Task<UserProfileDto> MapProfileAsync(User user)
    {
        // Newest non-revoked binding: Active wins over Pending.
        var deviceStatus = await _db.Devices.AsNoTracking()
            .Where(d => d.UserId == user.Id && d.Status != DeviceStatus.Revoked)
            .OrderBy(d => d.Status == DeviceStatus.Active ? 0 : 1)
            .Select(d => (DeviceStatus?)d.Status)
            .FirstOrDefaultAsync();

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            UserGroup = user.UserGroup.ToString(),
            MatricNo = user.MatricNo,
            DeviceStatus = deviceStatus?.ToString()
        };
    }

    private string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new("username",    user.UserName ?? string.Empty),
            new("full_name",   user.FullName),
            new("users_group", user.UserGroup.ToString()),
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims,
            notBefore: Now, expires: Now.AddMinutes(_jwt.MinutesToExpiration), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string? ReadUserIdFromExpiredToken(string accessToken)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
            ValidateIssuer = true, ValidIssuer = _jwt.Issuer,
            ValidateAudience = true, ValidAudience = _jwt.Audience,
            ValidateLifetime = false   // the whole point: we expect it to be expired
        };
        try
        {
            var principal = handler.ValidateToken(accessToken, parameters, out var validated);
            if (validated is not JwtSecurityToken jwt || jwt.Header.Alg != SecurityAlgorithms.HmacSha256) return null;
            return principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }

    private static (string Raw, string Hashed) GenerateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (raw, HashToken(raw));
    }

    private static string HashToken(string raw)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLower();

    private static bool FixedTimeEquals(string? a, string b)
        => a is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
