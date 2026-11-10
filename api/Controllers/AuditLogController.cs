using Microsoft.AspNetCore.Mvc;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Audit;
using SeatCharter.Api.Helpers;
using SeatCharter.Api.Helpers.Attributes;

namespace SeatCharter.Api.Controllers;

[ApiController]
[Route("api/v1/audit-logs")]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogService _auditLogs;

    public AuditLogController(IAuditLogService auditLogs) => _auditLogs = auditLogs;

    /// <summary>GET /audit-logs?entityType=CheckIn&amp;entityId=... — admin: all; lecturer: own actions</summary>
    [HttpGet]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<PagedResponse<List<AuditLogDto>>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] PaginationFilter filter, [FromQuery] string? entityType, [FromQuery] string? entityId)
    {
        var result = await _auditLogs.GetAllAsync(filter, entityType, entityId);
        return StatusCode(result.StatusCode, result);
    }
}
