using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SeatCharter.Api.Models;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Data;

/// <summary>
/// Idempotent startup work: optional migrations, then seed data. Seed:Profile picks the data set:
/// "Demo" (default; fake accounts the tests and demo script use) or "Dundee" (the real CS32005/CS31002 lecturers and students).
/// </summary>
public static class DbSeeder
{
    private const string Domain = "seatcharter.dev";

    // Approximate outline of the University of Dundee city campus. Replace with a geojson.io trace
    // (open question in the BRD) via PUT /api/v1/config/campus.
    private static readonly double[][] DundeeCityCampus =
    [
        [56.4612, -2.9880],
        [56.4612, -2.9725],
        [56.4560, -2.9725],
        [56.4545, -2.9800],
        [56.4555, -2.9880]
    ];

    public static async Task RunAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var settings = sp.GetRequiredService<SeedSettings>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbSeeder));
        var db = sp.GetRequiredService<ApplicationDbContext>();

        if (settings.MigrateOnStartup)
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrated");
        }

        if (!settings.Enabled) return;
        if (string.IsNullOrWhiteSpace(settings.DefaultPassword))
        {
            logger.LogWarning("Seed enabled but Seed:DefaultPassword is empty; skipping demo seed");
            return;
        }

        db.IsSeeding = true;
        var users = sp.GetRequiredService<UserManager<User>>();

        await EnsureUserAsync(users, $"admin@{Domain}", "Platform Admin", UserGroup.Admin, null,
            string.IsNullOrWhiteSpace(settings.AdminPassword) ? settings.DefaultPassword : settings.AdminPassword);

        if (string.Equals(settings.Profile, "Dundee", StringComparison.OrdinalIgnoreCase))
            await SeedDundeeAsync(db, users, settings.DefaultPassword);
        else
            await SeedDemoAsync(db, users, settings.DefaultPassword);

        if (!await db.CampusZones.AnyAsync())
        {
            db.CampusZones.Add(new CampusZone { Name = "Dundee City Campus", PolygonJson = JsonSerializer.Serialize(DundeeCityCampus) });
            await db.SaveChangesAsync();
        }

        db.IsSeeding = false;
        logger.LogInformation("Seed complete ({Profile})", settings.Profile);
    }

    private static async Task SeedDemoAsync(ApplicationDbContext db, UserManager<User> users, string password)
    {
        var lecturer = await EnsureUserAsync(users, $"lecturer@{Domain}", "Dr Ada Lovelace", UserGroup.Lecturer, null, password);

        string[] names = ["Amara Okafor", "Ben Carter", "Chloe Nguyen", "Daniel Mensah", "Ella Fraser"];
        var students = new List<User>();
        for (var i = 0; i < names.Length; i++)
            students.Add(await EnsureUserAsync(users, $"student{i + 1}@{Domain}", names[i], UserGroup.Student, $"24000{i + 1:D2}", password));

        var mobile = await EnsureCourseAsync(db, lecturer.Id, "CS32005", "Mobile Application Development", "MOBILE",
            [(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(15, 0), "Dalhousie 2G11"),
             (DayOfWeek.Wednesday, new TimeOnly(10, 0), new TimeOnly(12, 0), "Dalhousie 3G05")]);
        var secure = await EnsureCourseAsync(db, lecturer.Id, "CS32006", "Secure Systems", "SECURE",
            [(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(10, 0), "Queen Mother 1.07"),
             (DayOfWeek.Thursday, new TimeOnly(13, 0), new TimeOnly(14, 0), "Queen Mother 1.07")]);

        await EnsureEnrolmentsAsync(db, mobile.Id, students.Select(s => s.Id));
        await EnsureEnrolmentsAsync(db, secure.Id, students.Take(3).Select(s => s.Id));
    }

    // Rooms carry no coordinates, so a session start is checked against the campus outline only, not a room radius.
    private static async Task SeedDundeeAsync(ApplicationDbContext db, UserManager<User> users, string password)
    {
        var lecturerA = await EnsureUserAsync(users, "lecturer.a@dundee.example", "Lecturer A", UserGroup.Lecturer, null, password);
        var lecturerB = await EnsureUserAsync(users, "lecturer.b@dundee.example", "Lecturer B", UserGroup.Lecturer, null, password);

        var students = new[]
        {
            await EnsureUserAsync(users, "student.a@dundee.example", "Student A", UserGroup.Student, "1000001", password),
            await EnsureUserAsync(users, "student.b@dundee.example", "Student B", UserGroup.Student, "1000002", password)
        };

        var mobile = await EnsureCourseAsync(db, lecturerA.Id, "CS32005", "Mobile Application Development", "MOBDEV",
            [(DayOfWeek.Wednesday, new TimeOnly(11, 0), new TimeOnly(14, 0), "Queen Mother Lab 0")]);
        var security = await EnsureCourseAsync(db, lecturerB.Id, "CS31002", "Information and Network Security", "NETSEC",
            [(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(10, 0), "Tower T9"),
             (DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(11, 0), "Queen Mother Lab 0"),
             (DayOfWeek.Thursday, new TimeOnly(13, 0), new TimeOnly(14, 0), "Matthew 5013")]);

        await EnsureEnrolmentsAsync(db, mobile.Id, students.Select(s => s.Id));
        await EnsureEnrolmentsAsync(db, security.Id, students.Select(s => s.Id));
    }

    private static async Task<User> EnsureUserAsync(UserManager<User> users, string email, string name, UserGroup group, string? matricNo, string password)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is not null) return user;

        user = new User
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = name,
            UserGroup = group,
            MatricNo = matricNo,
            MustChangePassword = false,
            PasswordChangedAt = DateTime.UtcNow
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Seeding {email} failed: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        return user;
    }

    private static async Task<Course> EnsureCourseAsync(ApplicationDbContext db, string lecturerId, string code, string title, string joinCode,
        (DayOfWeek Day, TimeOnly Start, TimeOnly End, string Room)[] slots)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.LecturerId == lecturerId && c.Code == code);
        if (course is not null) return course;

        course = new Course { Code = code, Title = title, LecturerId = lecturerId, JoinCode = joinCode };
        db.Courses.Add(course);
        foreach (var s in slots)
            db.Schedules.Add(new Schedule { CourseId = course.Id, DayOfWeek = s.Day, StartTime = s.Start, EndTime = s.End, Room = s.Room });
        await db.SaveChangesAsync();
        return course;
    }

    private static async Task EnsureEnrolmentsAsync(ApplicationDbContext db, string courseId, IEnumerable<string> studentIds)
    {
        var existing = await db.Enrolments.Where(e => e.CourseId == courseId).Select(e => e.StudentId).ToListAsync();
        foreach (var id in studentIds.Except(existing))
            db.Enrolments.Add(new Enrolment { CourseId = courseId, StudentId = id });
        await db.SaveChangesAsync();
    }
}
