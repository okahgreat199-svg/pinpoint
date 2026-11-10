using SeatCharter.Api.DTOs.Requests.CheckIns;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.CheckIns;
using SeatCharter.Api.DTOs.Responses.Me;

namespace SeatCharter.Api.BusinessLogic.Interfaces;

public interface ICheckInService
{
    /// <summary>The check-in pipeline. 201 Present/Flagged, 200 duplicate, 422 Rejected (with data).</summary>
    Task<ApiResponseBase<CheckInResultDto>> CheckInAsync(CheckInRequest request);
    Task<ApiResponseBase<PresenceResultDto>> ReportPresenceAsync(string checkInId, PresenceReportRequest request);
    Task<ApiResponseBase<CheckInDto>> ReviewAsync(string checkInId, ReviewCheckInRequest request);
    Task<ApiResponseBase<MyAttendanceDto>> GetMyAttendanceAsync();
}
