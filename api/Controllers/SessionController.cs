using Microsoft.AspNetCore.Mvc;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.DTOs.Requests.Sessions;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Sessions;
using SeatCharter.Api.Helpers.Attributes;

namespace SeatCharter.Api.Controllers;

[ApiController]
[Route("api/v1/sessions")]
public class SessionController : ControllerBase
{
    private readonly ISessionService _sessions;

    public SessionController(ISessionService sessions) => _sessions = sessions;

    /// <summary>POST /sessions — open a session with the lecturer's anchor location</summary>
    [HttpPost]
    [ValidateUser(allowedGroups: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<SessionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Start([FromBody] StartSessionRequest request)
    {
        var result = await _sessions.StartAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>GET /sessions/{id}</summary>
    [HttpGet("{id}")]
    [ValidateUser]
    [ProducesResponseType(typeof(ApiResponseBase<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id)
    {
        var result = await _sessions.GetByIdAsync(id);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>GET /sessions/{id}/qr — current rotating token + manual code (owner only; poll every ~5 s)</summary>
    [HttpGet("{id}/qr")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<SessionQrDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetQr(string id)
    {
        var result = await _sessions.GetQrAsync(id);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /sessions/{id}/close — close and run the cluster outlier pass (BR-11)</summary>
    [HttpPost("{id}/close")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Close(string id)
    {
        var result = await _sessions.CloseAsync(id);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /sessions/{id}/prompt — send the presence prompt now (normally automatic at 70%)</summary>
    [HttpPost("{id}/prompt")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<PromptResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> TriggerPrompt(string id)
    {
        var result = await _sessions.TriggerPromptAsync(id);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>GET /sessions/{id}/attendance — roster with status, flags and rejected attempts</summary>
    [HttpGet("{id}/attendance")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<SessionAttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttendance(string id)
    {
        var result = await _sessions.GetAttendanceAsync(id);
        return StatusCode(result.StatusCode, result);
    }
}
