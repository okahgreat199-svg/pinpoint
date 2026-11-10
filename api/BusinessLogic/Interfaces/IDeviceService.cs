using SeatCharter.Api.DTOs.Requests.Devices;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Devices;

namespace SeatCharter.Api.BusinessLogic.Interfaces;

public interface IDeviceService
{
    Task<ApiResponseBase<DeviceDto>> RegisterAsync(RegisterDeviceRequest request);
    Task<ApiResponseBase<List<DeviceDto>>> GetMineAsync();
    Task<ApiResponseBase<List<PendingDeviceDto>>> GetPendingAsync();
    Task<ApiResponseBase<DeviceDto>> ApproveAsync(string id);
    Task<ApiResponseBase<DeviceDto>> RejectAsync(string id);
    Task<ApiResponseBase<DeviceDto>> UpdatePushTokenAsync(string id, UpdatePushTokenRequest request);
    Task<ApiResponseBase<int>> ResetUserDevicesAsync(string userId);
}
