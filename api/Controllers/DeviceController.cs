using Microsoft.AspNetCore.Mvc;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.DTOs.Requests.Devices;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Devices;
using SeatCharter.Api.Helpers.Attributes;

namespace SeatCharter.Api.Controllers;

[ApiController]
[Route("api/v1/devices")]
public class DeviceController : ControllerBase
{
    private readonly IDeviceService _devices;

    public DeviceController(IDeviceService devices) => _devices = devices;

    /// <summary>POST /devices — bind this phone's public key. Active if first device, else Pending (BR-13)</summary>
    [HttpPost]
    [ValidateUser]
    [RequireAppCheck]
    [ProducesResponseType(typeof(ApiResponseBase<DeviceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponseBase<DeviceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request)
    {
        var result = await _devices.RegisterAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>GET /devices/me — my device bindings, newest first</summary>
    [HttpGet("me")]
    [ValidateUser]
    [ProducesResponseType(typeof(ApiResponseBase<List<DeviceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine()
    {
        var result = await _devices.GetMineAsync();
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>GET /devices/pending — device-change requests from my students</summary>
    [HttpGet("pending")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<List<PendingDeviceDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPending()
    {
        var result = await _devices.GetPendingAsync();
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /devices/{id}/approve — activate a pending device; the old one is revoked</summary>
    [HttpPost("{id}/approve")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<DeviceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Approve(string id)
    {
        var result = await _devices.ApproveAsync(id);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /devices/{id}/reject — deny a pending device</summary>
    [HttpPost("{id}/reject")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<DeviceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reject(string id)
    {
        var result = await _devices.RejectAsync(id);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>PUT /devices/{id}/push-token — refresh the FCM token</summary>
    [HttpPut("{id}/push-token")]
    [ValidateUser]
    [ProducesResponseType(typeof(ApiResponseBase<DeviceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePushToken(string id, [FromBody] UpdatePushTokenRequest request)
    {
        var result = await _devices.UpdatePushTokenAsync(id, request);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /devices/users/{userId}/reset — admin: revoke all of a user's devices</summary>
    [HttpPost("users/{userId}/reset")]
    [ValidateUser(allowedGroups: "Admin")]
    [ProducesResponseType(typeof(ApiResponseBase<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetUserDevices(string userId)
    {
        var result = await _devices.ResetUserDevicesAsync(userId);
        return StatusCode(result.StatusCode, result);
    }
}
