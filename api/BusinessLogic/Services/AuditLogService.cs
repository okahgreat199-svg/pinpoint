using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.Data;
using SeatCharter.Api.DTOs;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Audit;
using SeatCharter.Api.Helpers;

namespace SeatCharter.Api.BusinessLogic.Services;

public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _db;
    private readonly UserInfoToken _userInfo;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(ApplicationDbContext db, UserInfoToken userInfo, ILogger<AuditLogService> logger)
    {
        _db = db;
        _userInfo = userInfo;
        _logger = logger;
    }

    private ApiResponseBase<T> Fail500<T>(Exception ex, [CallerMemberName] string method = "")
    {
        _logger.LogError(ex, "{Method} failed", method);
        return ApiResponseBase<T>.ServerError("An error occurred. Please try again.");
    }

    public async Task<ApiResponseBase<PagedResponse<List<AuditLogDto>>>> GetAllAsync(PaginationFilter filter, string? entityType, string? entityId)
    {
        try
        {
            var query = _db.AuditLogs.AsNoTracking();
            if (!_userInfo.IsAdmin())
            {
                var userId = _userInfo.RequireUserId();
                query = query.Where(a => a.ActorId == userId);
            }
            if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(a => a.EntityType == entityType);
            if (!string.IsNullOrWhiteSpace(entityId)) query = query.Where(a => a.EntityId == entityId);
            if (!string.IsNullOrWhiteSpace(filter.QuerySearch))
            {
                var term = filter.QuerySearch.Trim().ToLower();
                query = query.Where(a => a.Action.ToLower().Contains(term));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.At)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(a => new AuditLogDto
                {
                    Id = a.Id,
                    ActorId = a.ActorId,
                    ActorName = a.Actor != null ? a.Actor.FullName : null,
                    Action = a.Action,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    At = a.At,
                    DataJson = a.DataJson
                })
                .ToListAsync();

            return ApiResponseBase<PagedResponse<List<AuditLogDto>>>.Ok(
                new(items, filter.PageNumber, filter.PageSize, total, filter.QuerySearch),
                $"{total} audit entr{(total == 1 ? "y" : "ies")} found.");
        }
        catch (Exception ex) { return Fail500<PagedResponse<List<AuditLogDto>>>(ex); }
    }
}
