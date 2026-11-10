using SeatCharter.Api.DTOs.Requests.Courses;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Courses;
using SeatCharter.Api.Helpers;

namespace SeatCharter.Api.BusinessLogic.Interfaces;

public interface ICourseService
{
    /// <summary>Lecturer: own courses. Student: enrolled courses. Admin: all.</summary>
    Task<ApiResponseBase<PagedResponse<List<CourseDto>>>> GetAllAsync(PaginationFilter filter);
    Task<ApiResponseBase<CourseDto>> GetByIdAsync(string id);
    Task<ApiResponseBase<CourseDto>> CreateAsync(CreateCourseRequest request);
    Task<ApiResponseBase<CourseDto>> UpdateAsync(string id, UpdateCourseRequest request);
    Task<ApiResponseBase<bool>> DeleteAsync(string id);
    Task<ApiResponseBase<CourseDto>> RegenerateJoinCodeAsync(string id);
    Task<ApiResponseBase<CourseDto>> JoinAsync(JoinCourseRequest request);

    Task<ApiResponseBase<List<EnrolledStudentDto>>> GetStudentsAsync(string courseId);
    Task<ApiResponseBase<EnrolStudentsResultDto>> EnrolStudentsAsync(string courseId, EnrolStudentsRequest request);
    Task<ApiResponseBase<bool>> RemoveStudentAsync(string courseId, string studentId);

    Task<ApiResponseBase<List<ScheduleDto>>> GetSchedulesAsync(string courseId);
    Task<ApiResponseBase<ScheduleDto>> CreateScheduleAsync(string courseId, CreateScheduleRequest request);
    Task<ApiResponseBase<bool>> DeleteScheduleAsync(string courseId, string scheduleId);
}
