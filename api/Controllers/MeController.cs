using Microsoft.AspNetCore.Mvc;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Me;
using SeatCharter.Api.Helpers.Attributes;

namespace SeatCharter.Api.Controllers;

[ApiController]
[Route("api/v1/me")]
public class MeController : ControllerBase
{
    private readonly ISessionService _sessions;
    private readonly ICheckInService _checkIns;

    public MeController(ISessionService sessions, ICheckInService checkIns)
    {
        _sessions = sessions;
        _checkIns = checkIns;
    }

    /// <summary>GET /me/timetable?days=7 — schedule occurrences + live sessions for the caller</summary>
    [HttpGet("timetable")]
    [ValidateUser]
    [ProducesResponseType(typeof(ApiResponseBase<List<TimetableEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimetable([FromQuery] int days = 7)
    {
        var result = await _sessions.GetTimetableAsync(days);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>GET /me/attendance — own history per course with percentage</summary>
    [HttpGet("attendance")]
    [ValidateUser(allowedGroups: "Student")]
    [ProducesResponseType(typeof(ApiResponseBase<MyAttendanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttendance()
    {
        var result = await _checkIns.GetMyAttendanceAsync();
        return StatusCode(result.StatusCode, result);
    }
}
