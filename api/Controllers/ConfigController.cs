using Microsoft.AspNetCore.Mvc;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.DTOs.Requests.Config;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Config;
using SeatCharter.Api.Helpers.Attributes;

namespace SeatCharter.Api.Controllers;

[ApiController]
[Route("api/v1/config")]
public class ConfigController : ControllerBase
{
    private readonly ICampusService _campus;

    public ConfigController(ICampusService campus) => _campus = campus;

    /// <summary>GET /config/campus — campus polygon(s) for the app to show and pre-check</summary>
    [HttpGet("campus")]
    [ValidateUser]
    [ProducesResponseType(typeof(ApiResponseBase<CampusConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCampus()
    {
        var result = await _campus.GetCampusAsync();
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>PUT /config/campus — admin: replace the campus geofence</summary>
    [HttpPut("campus")]
    [ValidateUser(allowedGroups: "Admin")]
    [ProducesResponseType(typeof(ApiResponseBase<CampusConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateCampus([FromBody] UpdateCampusRequest request)
    {
        var result = await _campus.UpdateCampusAsync(request);
        return StatusCode(result.StatusCode, result);
    }
}
