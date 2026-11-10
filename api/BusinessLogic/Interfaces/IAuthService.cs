using SeatCharter.Api.DTOs.Requests.Auth;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Auth;

namespace SeatCharter.Api.BusinessLogic.Interfaces;

public interface IAuthService
{
    Task<ApiResponseBase<AuthResponse>> RegisterAsync(RegisterRequest request, string clientIp);
    Task<ApiResponseBase<AuthResponse>> LoginAsync(LoginRequest request, string clientIp);
    Task<ApiResponseBase<AuthResponse>> RefreshAsync(RefreshRequest request, string clientIp);
    Task<ApiResponseBase<bool>> LogoutAsync(LogoutRequest request);
    /// <summary>Reachable with bare [Authorize] (works while MustChangePassword = true), so the user id comes from the validated JWT.</summary>
    Task<ApiResponseBase<bool>> ChangePasswordAsync(string userId, ChangePasswordRequest request);
    Task<ApiResponseBase<UserProfileDto>> GetMeAsync();
}
