using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.Data;
using SeatCharter.Api.DTOs;
using SeatCharter.Api.DTOs.Responses;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Helpers.Attributes;

/// <summary>
/// Layer-2 authorization. The JWT proves who you were; this checks who you are now (active, role unchanged,
/// allowed group) on every request, then hydrates the scoped UserInfoToken from the DB snapshot.
/// Runs before model binding, so unauthenticated callers get 401/403, never 400.
/// </summary>
/// <example>
/// [ValidateUser]                                  // any signed-in user
/// [ValidateUser(requiredGroup: "Lecturer")]       // hierarchy: Student &lt; Lecturer &lt; Admin
/// [ValidateUser(allowedGroups: "Student")]        // exact groups, comma-separated
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class ValidateUserAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string? _module;
    private readonly string? _permission;
    private readonly string? _requiredGroup;
    private readonly HashSet<UserGroup>? _allowedGroups;

    public ValidateUserAttribute(string? module = null, string? permission = null, string? requiredGroup = null, string? allowedGroups = null)
    {
        _module = module;
        _permission = permission;
        _requiredGroup = requiredGroup;
        _allowedGroups = string.IsNullOrWhiteSpace(allowedGroups)
            ? null
            : allowedGroups.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(g => Enum.Parse<UserGroup>(g, ignoreCase: true))
                .ToHashSet();
    }

    private static readonly Dictionary<UserGroup, int> GroupHierarchy = new()
    {
        [UserGroup.Student]  = 0,
        [UserGroup.Lecturer] = 1,
        [UserGroup.Admin]    = 2,
    };

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;

        // 1. sub claim. Missing → 401.
        var sub = http.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub))
        {
            context.Result = Fail(ApiResponseBase<object>.Unauthorized("Authentication required."));
            return;
        }

        // 2. Reject MFA partial tokens.
        if (http.User.FindFirst("mfa_pending")?.Value == "true")
        {
            context.Result = Fail(ApiResponseBase<object>.Unauthorized("MFA verification is required."));
            return;
        }
        if (http.User.FindFirst("mfa_setup_required")?.Value == "true")
        {
            context.Result = Fail(ApiResponseBase<object>.Forbidden("MFA setup is required before proceeding."));
            return;
        }

        // 3. Snapshot from cache, falling back to the DB.
        var cache = http.RequestServices.GetRequiredService<IUserCacheService>();
        var userData = cache.Get(sub);
        if (userData is null)
        {
            var db = http.RequestServices.GetRequiredService<ApplicationDbContext>();
            userData = await db.Users.AsNoTracking()
                .Where(u => u.Id == sub)
                .Select(u => new UserValidationData
                {
                    UserId = u.Id,
                    Email = u.Email ?? string.Empty,
                    FullName = u.FullName,
                    UserGroup = u.UserGroup,
                    IsActive = u.IsActive,
                    MustChangePassword = u.MustChangePassword
                })
                .FirstOrDefaultAsync();

            if (userData is null)
            {
                context.Result = Fail(ApiResponseBase<object>.Unauthorized("User not found."));
                return;
            }
            cache.Set(userData);
        }

        // 4. Account state.
        if (!userData.IsActive)
        {
            context.Result = Fail(ApiResponseBase<object>.Forbidden("Your account has been deactivated."));
            return;
        }
        if (userData.MustChangePassword)
        {
            context.Result = Fail(ApiResponseBase<object>.Forbidden("Password change required before proceeding."));
            return;
        }

        // 5. Tamper/staleness: JWT group must match the DB.
        if (http.User.FindFirst("users_group")?.Value != userData.UserGroup.ToString())
        {
            context.Result = Fail(ApiResponseBase<object>.Forbidden("Token claims do not match current user state."));
            return;
        }

        // 6. Group checks.
        if (!string.IsNullOrWhiteSpace(_requiredGroup)
            && Enum.TryParse<UserGroup>(_requiredGroup, ignoreCase: true, out var requiredGroup)
            && GroupHierarchy[userData.UserGroup] < GroupHierarchy[requiredGroup])
        {
            context.Result = Fail(ApiResponseBase<object>.Forbidden($"This action requires the {requiredGroup} role."));
            return;
        }
        if (_allowedGroups is not null && !_allowedGroups.Contains(userData.UserGroup))
        {
            context.Result = Fail(ApiResponseBase<object>.Forbidden(
                $"This action is only available to: {string.Join(", ", _allowedGroups)}."));
            return;
        }

        // 7. Permission check.
        if (!string.IsNullOrWhiteSpace(_module) && !string.IsNullOrWhiteSpace(_permission))
        {
            var required = $"{_module}:{_permission}";
            if (!userData.Permissions.Contains(required))
            {
                context.Result = Fail(ApiResponseBase<object>.Forbidden($"Missing permission: {required}."));
                return;
            }
        }

        // 8. Hydrate from the DB snapshot, not the JWT.
        var userInfo = http.RequestServices.GetRequiredService<UserInfoToken>();
        userInfo.UserId = userData.UserId;
        userInfo.Email = userData.Email;
        userInfo.FullName = userData.FullName;
        userInfo.UsersGroup = userData.UserGroup.ToString();
        userInfo.Permissions = userData.Permissions;
    }

    private static ObjectResult Fail(ApiResponseBase<object> envelope) => new(envelope) { StatusCode = envelope.StatusCode };
}
