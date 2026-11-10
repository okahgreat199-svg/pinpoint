using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Audit;
using SeatCharter.Api.Helpers;

namespace SeatCharter.Api.BusinessLogic.Interfaces;

public interface IAuditLogService
{
    /// <summary>Admin: everything. Lecturer: their own actions only.</summary>
    Task<ApiResponseBase<PagedResponse<List<AuditLogDto>>>> GetAllAsync(PaginationFilter filter, string? entityType, string? entityId);
}
