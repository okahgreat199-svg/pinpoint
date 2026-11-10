using Microsoft.AspNetCore.Mvc;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.DTOs.Requests.Courses;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Courses;
using SeatCharter.Api.Helpers;
using SeatCharter.Api.Helpers.Attributes;

namespace SeatCharter.Api.Controllers;

[ApiController]
[Route("api/v1/courses")]
public class CourseController : ControllerBase
{
    private readonly ICourseService _courses;

    public CourseController(ICourseService courses) => _courses = courses;

    /// <summary>GET /courses — lecturer: own; student: enrolled; admin: all</summary>
    [HttpGet]
    [ValidateUser]
    [ProducesResponseType(typeof(ApiResponseBase<PagedResponse<List<CourseDto>>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] PaginationFilter filter)
    {
        var result = await _courses.GetAllAsync(filter);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>GET /courses/{id}</summary>
    [HttpGet("{id}")]
    [ValidateUser]
    [ProducesResponseType(typeof(ApiResponseBase<CourseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id)
    {
        var result = await _courses.GetByIdAsync(id);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /courses — create; a join code is generated</summary>
    [HttpPost]
    [ValidateUser(allowedGroups: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<CourseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCourseRequest request)
    {
        var result = await _courses.CreateAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>PUT /courses/{id}</summary>
    [HttpPut("{id}")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<CourseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateCourseRequest request)
    {
        var result = await _courses.UpdateAsync(id, request);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>DELETE /courses/{id} — soft delete</summary>
    [HttpDelete("{id}")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _courses.DeleteAsync(id);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /courses/{id}/join-code — issue a new join code (old one stops working)</summary>
    [HttpPost("{id}/join-code")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<CourseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegenerateJoinCode(string id)
    {
        var result = await _courses.RegenerateJoinCodeAsync(id);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /courses/join — student enrols with a join code</summary>
    [HttpPost("join")]
    [ValidateUser(allowedGroups: "Student")]
    [ProducesResponseType(typeof(ApiResponseBase<CourseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponseBase<CourseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Join([FromBody] JoinCourseRequest request)
    {
        var result = await _courses.JoinAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>GET /courses/{id}/students — enrolment list</summary>
    [HttpGet("{id}/students")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<List<EnrolledStudentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudents(string id)
    {
        var result = await _courses.GetStudentsAsync(id);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /courses/{id}/students — enrol by list of emails or matric numbers</summary>
    [HttpPost("{id}/students")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<EnrolStudentsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnrolStudents(string id, [FromBody] EnrolStudentsRequest request)
    {
        var result = await _courses.EnrolStudentsAsync(id, request);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>DELETE /courses/{id}/students/{studentId} — un-enrol</summary>
    [HttpDelete("{id}/students/{studentId}")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveStudent(string id, string studentId)
    {
        var result = await _courses.RemoveStudentAsync(id, studentId);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>GET /courses/{id}/schedules — weekly slots</summary>
    [HttpGet("{id}/schedules")]
    [ValidateUser]
    [ProducesResponseType(typeof(ApiResponseBase<List<ScheduleDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSchedules(string id)
    {
        var result = await _courses.GetSchedulesAsync(id);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>POST /courses/{id}/schedules — add a weekly slot</summary>
    [HttpPost("{id}/schedules")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<ScheduleDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateSchedule(string id, [FromBody] CreateScheduleRequest request)
    {
        var result = await _courses.CreateScheduleAsync(id, request);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>DELETE /courses/{id}/schedules/{scheduleId} — soft delete</summary>
    [HttpDelete("{id}/schedules/{scheduleId}")]
    [ValidateUser(requiredGroup: "Lecturer")]
    [ProducesResponseType(typeof(ApiResponseBase<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseBase<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSchedule(string id, string scheduleId)
    {
        var result = await _courses.DeleteScheduleAsync(id, scheduleId);
        return StatusCode(result.StatusCode, result);
    }
}
