using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.DTOs.Requests.CheckIns;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.CheckIns;
using SeatCharter.Api.Helpers.Attributes;

namespace SeatCharter.Api.Controllers;

[ApiController]
[Route("api/v1/checkins")]
public class CheckInController : ControllerBase
{
    private readonly ICheckInService _checkIns;

    public CheckInController(ICheckInService checkIns) => _checkIns = checkIns;

    /// <summary>POST /checkins — the check-in pipeline: 201 Present/Flagged, 200 duplicate, 422 Rejected</summary>
    [HttpPost]
    [ValidateUser(allowedGroups: "Student")]
    [RequireAppCheck]
    [EnableRateLimiting("checkin")]
    [ProducesResponseType(typeof(ApiResponseBase<CheckInResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponseBase<CheckInResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<CheckInResultDto>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
    {
        var result = await _checkIns.CheckInAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /checkins/{id}/presence — geofence exit or answer to the 70% prompt</summary>
    [HttpPost("{id}/presence")]
    [ValidateUser(allowedGroups: "Student")]
    [RequireAppCheck]
    [ProducesResponseType(typeof(ApiResponseBase<PresenceResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReportPresence(string id, [FromBody] PresenceReportRequest request)
    {
        var result = await _checkIns.ReportPresenceAsync(id, request);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>PATCH /checkins/{id}/review — resolve a flag to Present or Absent (audited)</summary>
    [HttpPatch("{id}/review")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<CheckInDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Review(string id, [FromBody] ReviewCheckInRequest request)
    {
        var result = await _checkIns.ReviewAsync(id, request);
        return StatusCode(result.StatusCode, result);
    }
}
