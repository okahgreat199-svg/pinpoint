using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SeatCharter.Api.Data;
using SeatCharter.Api.DTOs;
using SeatCharter.Api.Models;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Tests;

public sealed class FakeClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>Fresh in-memory DB per test, plus helpers to seed the minimum graph.</summary>
public static class TestDb
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    public static UserInfoToken As(User u) => new()
    {
        UserId = u.Id, Email = u.Email, FullName = u.FullName, UsersGroup = u.UserGroup.ToString()
    };

    public static User AddUser(this ApplicationDbContext db, UserGroup group, string name)
    {
        var u = new User
        {
            UserName = $"{name}@test", Email = $"{name}@test", FullName = name, UserGroup = group,
            MatricNo = group == UserGroup.Student ? name.ToUpperInvariant() : null
        };
        db.Users.Add(u);
        return u;
    }

    public static Course AddCourse(this ApplicationDbContext db, User lecturer, string code = "CS1")
    {
        var c = new Course { Code = code, Title = code, LecturerId = lecturer.Id, JoinCode = code + "JC" };
        db.Courses.Add(c);
        return c;
    }

    public static void Enrol(this ApplicationDbContext db, Course c, User s) => db.Enrolments.Add(new Enrolment { CourseId = c.Id, StudentId = s.Id });

    public static Session AddOpenSession(this ApplicationDbContext db, Course c, DateTime now, int minutes = 60)
    {
        var s = new Session
        {
            CourseId = c.Id, StartsAt = now, EndsAt = now.AddMinutes(minutes), Status = SessionStatus.Open,
            AnchorLat = 56.4580, AnchorLng = -2.9800, AnchorAccuracyM = 5
        };
        db.Sessions.Add(s);
        return s;
    }

    public static (Device Device, ECDsa Key) AddDevice(this ApplicationDbContext db, User u, DeviceStatus status = DeviceStatus.Active, string? hardwareId = null)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var d = new Device
        {
            UserId = u.Id, PublicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            Platform = "android", Model = "Pixel", Status = status, HardwareId = hardwareId
        };
        db.Devices.Add(d);
        return (d, key);
    }

    public static AttendanceSettings Settings => new();
}
