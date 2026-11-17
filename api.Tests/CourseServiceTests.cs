using Microsoft.Extensions.Logging.Abstractions;
using SeatCharter.Api.BusinessLogic.Services;
using SeatCharter.Api.Data;
using SeatCharter.Api.DTOs.Requests.Courses;
using SeatCharter.Api.Helpers;
using SeatCharter.Api.Models;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Tests;

public class CourseServiceTests
{
    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly User _lecturerA;
    private readonly User _lecturerB;
    private readonly User _student;

    public CourseServiceTests()
    {
        _lecturerA = _db.AddUser(UserGroup.Lecturer, "a");
        _lecturerB = _db.AddUser(UserGroup.Lecturer, "b");
        _student = _db.AddUser(UserGroup.Student, "s1");
        _db.SaveChanges();
    }

    private CourseService ServiceFor(User u) => new(_db, TestDb.As(u), NullLogger<CourseService>.Instance);

    [Fact]
    public async Task GetById_OtherLecturersCourse_NotFound()
    {
        // Isolation: lecturers only see their own courses, and never learn another's exists.
        var created = await ServiceFor(_lecturerB).CreateAsync(new CreateCourseRequest { Code = "cs99", Title = "Theirs" });

        Assert.Equal(404, (await ServiceFor(_lecturerA).GetByIdAsync(created.ResponseData!.Id)).StatusCode);
        Assert.Equal(0, (await ServiceFor(_lecturerA).GetAllAsync(new PaginationFilter())).ResponseData!.TotalRecords);
    }

    [Fact]
    public async Task Create_NormalisesCode_AndIssuesJoinCode()
    {
        var result = await ServiceFor(_lecturerA).CreateAsync(new CreateCourseRequest { Code = " cs32005 ", Title = "Mobile" });
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("CS32005", result.ResponseData!.Code);
        Assert.Matches("^[A-Z2-9]{6}$", result.ResponseData.JoinCode);
    }

    [Fact]
    public async Task Create_DuplicateCodeForSameLecturer_Conflict()
    {
        await ServiceFor(_lecturerA).CreateAsync(new CreateCourseRequest { Code = "CS1", Title = "One" });
        Assert.Equal(409, (await ServiceFor(_lecturerA).CreateAsync(new CreateCourseRequest { Code = "cs1", Title = "Again" })).StatusCode);
    }

    [Fact]
    public async Task Join_WithCode_Enrols_AndHidesJoinCodeFromStudent()
    {
        var course = await ServiceFor(_lecturerA).CreateAsync(new CreateCourseRequest { Code = "CS1", Title = "One" });
        var joined = await ServiceFor(_student).JoinAsync(new JoinCourseRequest { JoinCode = course.ResponseData!.JoinCode!.ToLower() });

        Assert.Equal(201, joined.StatusCode);
        Assert.Null(joined.ResponseData!.JoinCode);
        Assert.Equal(200, (await ServiceFor(_student).JoinAsync(new JoinCourseRequest { JoinCode = course.ResponseData.JoinCode })).StatusCode);
        Assert.Single(_db.Enrolments);
    }

    [Fact]
    public async Task Join_UnknownCode_NotFound()
        => Assert.Equal(404, (await ServiceFor(_student).JoinAsync(new JoinCourseRequest { JoinCode = "NOPE99" })).StatusCode);

    [Fact]
    public async Task EnrolStudents_ByEmailOrMatric_ReportsEachOutcome()
    {
        var course = await ServiceFor(_lecturerA).CreateAsync(new CreateCourseRequest { Code = "CS1", Title = "One" });
        var result = await ServiceFor(_lecturerA).EnrolStudentsAsync(course.ResponseData!.Id,
            new EnrolStudentsRequest { Identifiers = ["s1@test", "S1", "ghost@test"] });

        Assert.Equal(["s1@test"], result.ResponseData!.Enrolled);
        Assert.Equal(["S1"], result.ResponseData.AlreadyEnrolled);
        Assert.Equal(["ghost@test"], result.ResponseData.NotFound);
    }

    [Fact]
    public async Task CreateSchedule_EndBeforeStart_Unprocessable()
    {
        var course = await ServiceFor(_lecturerA).CreateAsync(new CreateCourseRequest { Code = "CS1", Title = "One" });
        var result = await ServiceFor(_lecturerA).CreateScheduleAsync(course.ResponseData!.Id, new CreateScheduleRequest
        {
            DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(10, 0)
        });
        Assert.Equal(422, result.StatusCode);
    }
}
