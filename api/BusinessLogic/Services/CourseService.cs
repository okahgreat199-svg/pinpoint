using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.Data;
using SeatCharter.Api.DTOs;
using SeatCharter.Api.DTOs.Requests.Courses;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Courses;
using SeatCharter.Api.Helpers;
using SeatCharter.Api.Models;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.BusinessLogic.Services;

public class CourseService : ICourseService
{
    // No 0/O/1/I/L: codes get read off a projector.
    private const string JoinCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int JoinCodeLength = 6;

    private readonly ApplicationDbContext _db;
    private readonly UserInfoToken _userInfo;
    private readonly ILogger<CourseService> _logger;

    public CourseService(ApplicationDbContext db, UserInfoToken userInfo, ILogger<CourseService> logger)
    {
        _db = db;
        _userInfo = userInfo;
        _logger = logger;
    }

    private ApiResponseBase<T> Fail500<T>(Exception ex, [CallerMemberName] string method = "")
    {
        _logger.LogError(ex, "{Method} failed", method);
        return ApiResponseBase<T>.ServerError("An error occurred. Please try again.");
    }

    /// <summary>Courses the caller may see. Single place that applies visibility; every read starts here.</summary>
    private IQueryable<Course> Visible()
    {
        var userId = _userInfo.RequireUserId();
        if (_userInfo.IsAdmin()) return _db.Courses;
        if (_userInfo.IsLecturer()) return _db.Courses.Where(c => c.LecturerId == userId);
        return _db.Courses.Where(c => c.Enrolments.Any(e => e.StudentId == userId));
    }

    /// <summary>Courses the caller may change: own courses (admins: all).</summary>
    private IQueryable<Course> Owned()
    {
        var userId = _userInfo.RequireUserId();
        return _userInfo.IsAdmin() ? _db.Courses : _db.Courses.Where(c => c.LecturerId == userId);
    }

    public async Task<ApiResponseBase<PagedResponse<List<CourseDto>>>> GetAllAsync(PaginationFilter filter)
    {
        try
        {
            var query = Visible();
            if (!string.IsNullOrWhiteSpace(filter.QuerySearch))
            {
                var term = filter.QuerySearch.Trim().ToLower();
                query = query.Where(c => c.Code.ToLower().Contains(term) || c.Title.ToLower().Contains(term));
            }

            var total = await query.CountAsync();
            var showCode = !_userInfo.IsStudent();
            var items = await query
                .OrderBy(c => c.Code)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(c => new CourseDto
                {
                    Id = c.Id,
                    Code = c.Code,
                    Title = c.Title,
                    LecturerId = c.LecturerId,
                    LecturerName = c.Lecturer.FullName,
                    JoinCode = showCode ? c.JoinCode : null,
                    StudentCount = c.Enrolments.Count,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();

            return ApiResponseBase<PagedResponse<List<CourseDto>>>.Ok(
                new(items, filter.PageNumber, filter.PageSize, total, filter.QuerySearch),
                $"{total} course(s) found.");
        }
        catch (Exception ex) { return Fail500<PagedResponse<List<CourseDto>>>(ex); }
    }

    public async Task<ApiResponseBase<CourseDto>> GetByIdAsync(string id)
    {
        try
        {
            var dto = await ProjectAsync(Visible().Where(c => c.Id == id));
            return dto is null
                ? ApiResponseBase<CourseDto>.NotFound("Course not found.")
                : ApiResponseBase<CourseDto>.Ok(dto, "Course retrieved.");
        }
        catch (Exception ex) { return Fail500<CourseDto>(ex); }
    }

    public async Task<ApiResponseBase<CourseDto>> CreateAsync(CreateCourseRequest request)
    {
        try
        {
            var lecturerId = _userInfo.RequireUserId();
            var code = request.Code.Trim().ToUpperInvariant();
            if (await _db.Courses.AnyAsync(c => c.LecturerId == lecturerId && c.Code == code))
                return ApiResponseBase<CourseDto>.Conflict("You already have a course with this code.");

            var course = new Course
            {
                Code = code,
                Title = request.Title.Trim(),
                LecturerId = lecturerId,
                JoinCode = await GenerateJoinCodeAsync()
            };
            _db.Courses.Add(course);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Course {CourseId} created by {UserId}", course.Id, lecturerId);
            return ApiResponseBase<CourseDto>.Created((await ProjectAsync(_db.Courses.Where(c => c.Id == course.Id)))!, "Course created.");
        }
        catch (Exception ex) { return Fail500<CourseDto>(ex); }
    }

    public async Task<ApiResponseBase<CourseDto>> UpdateAsync(string id, UpdateCourseRequest request)
    {
        try
        {
            var course = await Owned().FirstOrDefaultAsync(c => c.Id == id);
            if (course is null) return ApiResponseBase<CourseDto>.NotFound("Course not found.");

            var code = request.Code.Trim().ToUpperInvariant();
            if (code != course.Code && await _db.Courses.AnyAsync(c => c.LecturerId == course.LecturerId && c.Code == code && c.Id != id))
                return ApiResponseBase<CourseDto>.Conflict("You already have a course with this code.");

            course.Code = code;
            course.Title = request.Title.Trim();
            await _db.SaveChangesAsync();
            return ApiResponseBase<CourseDto>.Ok((await ProjectAsync(_db.Courses.Where(c => c.Id == id)))!, "Course updated.");
        }
        catch (Exception ex) { return Fail500<CourseDto>(ex); }
    }

    public async Task<ApiResponseBase<bool>> DeleteAsync(string id)
    {
        try
        {
            var course = await Owned().FirstOrDefaultAsync(c => c.Id == id);
            if (course is null) return ApiResponseBase<bool>.NotFound("Course not found.");
            if (await _db.Sessions.AnyAsync(s => s.CourseId == id && s.Status == SessionStatus.Open))
                return ApiResponseBase<bool>.UnprocessableEntity("Close the open session before deleting this course.");

            course.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return ApiResponseBase<bool>.Ok(true, "Course deleted.");
        }
        catch (Exception ex) { return Fail500<bool>(ex); }
    }

    public async Task<ApiResponseBase<CourseDto>> RegenerateJoinCodeAsync(string id)
    {
        try
        {
            var course = await Owned().FirstOrDefaultAsync(c => c.Id == id);
            if (course is null) return ApiResponseBase<CourseDto>.NotFound("Course not found.");

            course.JoinCode = await GenerateJoinCodeAsync();
            await _db.SaveChangesAsync();
            return ApiResponseBase<CourseDto>.Ok((await ProjectAsync(_db.Courses.Where(c => c.Id == id)))!, "Join code regenerated.");
        }
        catch (Exception ex) { return Fail500<CourseDto>(ex); }
    }

    public async Task<ApiResponseBase<CourseDto>> JoinAsync(JoinCourseRequest request)
    {
        try
        {
            var studentId = _userInfo.RequireUserId();
            var code = request.JoinCode.Trim().ToUpperInvariant();
            var course = await _db.Courses.FirstOrDefaultAsync(c => c.JoinCode == code);
            if (course is null) return ApiResponseBase<CourseDto>.NotFound("No course matches that join code.");

            var already = await _db.Enrolments.AnyAsync(e => e.CourseId == course.Id && e.StudentId == studentId);
            if (!already)
            {
                _db.Enrolments.Add(new Enrolment { CourseId = course.Id, StudentId = studentId });
                await _db.SaveChangesAsync();
            }

            var dto = (await ProjectAsync(_db.Courses.Where(c => c.Id == course.Id)))!;
            return already
                ? ApiResponseBase<CourseDto>.Ok(dto, "You are already enrolled in this course.")
                : ApiResponseBase<CourseDto>.Created(dto, $"Enrolled in {course.Code}.");
        }
        catch (Exception ex) { return Fail500<CourseDto>(ex); }
    }

    public async Task<ApiResponseBase<List<EnrolledStudentDto>>> GetStudentsAsync(string courseId)
    {
        try
        {
            if (!await Owned().AnyAsync(c => c.Id == courseId))
                return ApiResponseBase<List<EnrolledStudentDto>>.NotFound("Course not found.");

            var items = await _db.Enrolments.AsNoTracking()
                .Where(e => e.CourseId == courseId)
                .OrderBy(e => e.Student.FullName)
                .Select(e => new EnrolledStudentDto
                {
                    StudentId = e.StudentId,
                    FullName = e.Student.FullName,
                    Email = e.Student.Email ?? string.Empty,
                    MatricNo = e.Student.MatricNo,
                    EnrolledAt = e.EnrolledAt
                })
                .ToListAsync();
            return ApiResponseBase<List<EnrolledStudentDto>>.Ok(items, $"{items.Count} student(s) enrolled.");
        }
        catch (Exception ex) { return Fail500<List<EnrolledStudentDto>>(ex); }
    }

    public async Task<ApiResponseBase<EnrolStudentsResultDto>> EnrolStudentsAsync(string courseId, EnrolStudentsRequest request)
    {
        try
        {
            if (!await Owned().AnyAsync(c => c.Id == courseId))
                return ApiResponseBase<EnrolStudentsResultDto>.NotFound("Course not found.");

            var wanted = request.Identifiers
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var emails = wanted.Select(i => i.ToLowerInvariant()).ToList();
            var matrics = wanted.Select(i => i.ToUpperInvariant()).ToList();

            var students = await _db.Users
                .Where(u => u.UserGroup == UserGroup.Student
                    && (emails.Contains(u.Email!) || (u.MatricNo != null && matrics.Contains(u.MatricNo))))
                .Select(u => new { u.Id, u.Email, u.MatricNo })
                .ToListAsync();
            var enrolledIds = await _db.Enrolments.Where(e => e.CourseId == courseId).Select(e => e.StudentId).ToListAsync();

            var result = new EnrolStudentsResultDto();
            var added = new HashSet<string>();
            foreach (var identifier in wanted)
            {
                var student = students.FirstOrDefault(s =>
                    string.Equals(s.Email, identifier, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.MatricNo, identifier, StringComparison.OrdinalIgnoreCase));
                if (student is null) { result.NotFound.Add(identifier); continue; }
                if (enrolledIds.Contains(student.Id) || !added.Add(student.Id)) { result.AlreadyEnrolled.Add(identifier); continue; }

                _db.Enrolments.Add(new Enrolment { CourseId = courseId, StudentId = student.Id });
                result.Enrolled.Add(identifier);
            }
            await _db.SaveChangesAsync();

            return ApiResponseBase<EnrolStudentsResultDto>.Ok(result,
                $"{result.Enrolled.Count} enrolled, {result.AlreadyEnrolled.Count} already enrolled, {result.NotFound.Count} not found.");
        }
        catch (Exception ex) { return Fail500<EnrolStudentsResultDto>(ex); }
    }

    public async Task<ApiResponseBase<bool>> RemoveStudentAsync(string courseId, string studentId)
    {
        try
        {
            if (!await Owned().AnyAsync(c => c.Id == courseId))
                return ApiResponseBase<bool>.NotFound("Course not found.");

            var enrolment = await _db.Enrolments.FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId);
            if (enrolment is null) return ApiResponseBase<bool>.NotFound("Student is not enrolled in this course.");

            // Enrolment is a join row, not business history (check-ins keep their own record), so it is removed outright.
            _db.Enrolments.Remove(enrolment);
            await _db.SaveChangesAsync();
            return ApiResponseBase<bool>.Ok(true, "Student removed from course.");
        }
        catch (Exception ex) { return Fail500<bool>(ex); }
    }

    public async Task<ApiResponseBase<List<ScheduleDto>>> GetSchedulesAsync(string courseId)
    {
        try
        {
            if (!await Visible().AnyAsync(c => c.Id == courseId))
                return ApiResponseBase<List<ScheduleDto>>.NotFound("Course not found.");

            var items = await _db.Schedules.AsNoTracking()
                .Where(s => s.CourseId == courseId)
                .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
                .ToListAsync();
            return ApiResponseBase<List<ScheduleDto>>.Ok(items.Select(MapSchedule).ToList(), $"{items.Count} schedule slot(s) found.");
        }
        catch (Exception ex) { return Fail500<List<ScheduleDto>>(ex); }
    }

    public async Task<ApiResponseBase<ScheduleDto>> CreateScheduleAsync(string courseId, CreateScheduleRequest request)
    {
        try
        {
            if (!await Owned().AnyAsync(c => c.Id == courseId))
                return ApiResponseBase<ScheduleDto>.NotFound("Course not found.");
            if (request.EndTime <= request.StartTime)
                return ApiResponseBase<ScheduleDto>.UnprocessableEntity("End time must be after start time.");
            if (request.RoomLat.HasValue != request.RoomLng.HasValue)
                return ApiResponseBase<ScheduleDto>.BadRequest("Provide both room latitude and longitude, or neither.");

            var clash = await _db.Schedules.AnyAsync(s => s.CourseId == courseId && s.DayOfWeek == request.DayOfWeek
                && s.StartTime < request.EndTime && request.StartTime < s.EndTime);
            if (clash) return ApiResponseBase<ScheduleDto>.Conflict("This slot overlaps an existing slot for the course.");

            var schedule = new Schedule
            {
                CourseId = courseId,
                DayOfWeek = request.DayOfWeek,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Room = request.Room?.Trim(),
                RoomLat = request.RoomLat,
                RoomLng = request.RoomLng,
                RoomRadiusM = request.RoomRadiusM
            };
            _db.Schedules.Add(schedule);
            await _db.SaveChangesAsync();
            return ApiResponseBase<ScheduleDto>.Created(MapSchedule(schedule), "Schedule slot added.");
        }
        catch (Exception ex) { return Fail500<ScheduleDto>(ex); }
    }

    public async Task<ApiResponseBase<bool>> DeleteScheduleAsync(string courseId, string scheduleId)
    {
        try
        {
            if (!await Owned().AnyAsync(c => c.Id == courseId))
                return ApiResponseBase<bool>.NotFound("Course not found.");

            var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.Id == scheduleId && s.CourseId == courseId);
            if (schedule is null) return ApiResponseBase<bool>.NotFound("Schedule slot not found.");

            schedule.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return ApiResponseBase<bool>.Ok(true, "Schedule slot removed.");
        }
        catch (Exception ex) { return Fail500<bool>(ex); }
    }

    // ── helpers ──

    private Task<CourseDto?> ProjectAsync(IQueryable<Course> query)
    {
        var showCode = !_userInfo.IsStudent();
        return query.AsNoTracking().Select(c => new CourseDto
        {
            Id = c.Id,
            Code = c.Code,
            Title = c.Title,
            LecturerId = c.LecturerId,
            LecturerName = c.Lecturer.FullName,
            JoinCode = showCode ? c.JoinCode : null,
            StudentCount = c.Enrolments.Count,
            CreatedAt = c.CreatedAt
        }).FirstOrDefaultAsync();
    }

    private async Task<string> GenerateJoinCodeAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = RandomNumberGenerator.GetString(JoinCodeAlphabet, JoinCodeLength);
            if (!await _db.Courses.IgnoreQueryFilters().AnyAsync(c => c.JoinCode == code)) return code;
        }
        throw new InvalidOperationException("Could not generate a unique join code.");
    }

    private static ScheduleDto MapSchedule(Schedule s) => new()
    {
        Id = s.Id,
        CourseId = s.CourseId,
        DayOfWeek = s.DayOfWeek,
        StartTime = s.StartTime,
        EndTime = s.EndTime,
        Room = s.Room,
        RoomLat = s.RoomLat,
        RoomLng = s.RoomLng,
        RoomRadiusM = s.RoomRadiusM
    };
}
