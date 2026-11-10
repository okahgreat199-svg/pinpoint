using SeatCharter.Api.DTOs.Requests.Sessions;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Me;
using SeatCharter.Api.DTOs.Responses.Sessions;

namespace SeatCharter.Api.BusinessLogic.Interfaces;

public interface ISessionService
{
    Task<ApiResponseBase<SessionDto>> StartAsync(StartSessionRequest request);
    Task<ApiResponseBase<SessionDto>> GetByIdAsync(string id);
    Task<ApiResponseBase<SessionQrDto>> GetQrAsync(string id);
    Task<ApiResponseBase<SessionDto>> CloseAsync(string id);
    /// <summary>Sends the 70% prompt now (demo step 6). Also used when the lecturer wants an extra check.</summary>
    Task<ApiResponseBase<PromptResultDto>> TriggerPromptAsync(string id);
    Task<ApiResponseBase<SessionAttendanceDto>> GetAttendanceAsync(string id);
    Task<ApiResponseBase<List<TimetableEntryDto>>> GetTimetableAsync(int days);
}
