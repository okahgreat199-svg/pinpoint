using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SeatCharter.Api.BusinessLogic.Services;
using SeatCharter.Api.Data;
using SeatCharter.Api.Domain;
using SeatCharter.Api.DTOs.Requests.CheckIns;
using SeatCharter.Api.Models;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Tests;

public class CheckInServiceTests
{
    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly FakeClock _clock = new();
    private readonly User _lecturer;
    private readonly User _student;
    private readonly Course _course;
    private readonly Session _session;
    private readonly Device _device;
    private readonly ECDsa _key;

    public CheckInServiceTests()
    {
        _lecturer = _db.AddUser(UserGroup.Lecturer, "lec");
        _student = _db.AddUser(UserGroup.Student, "stu");
        _course = _db.AddCourse(_lecturer);
        _db.Enrol(_course, _student);
        _session = _db.AddOpenSession(_course, _clock.GetUtcNow().UtcDateTime.AddMinutes(-5));
        (_device, _key) = _db.AddDevice(_student);
        _db.SaveChanges();
    }

    private CheckInService ServiceFor(User u) => new(_db, TestDb.As(u),
        new CampusService(_db, new MemoryCache(new MemoryCacheOptions()), TestDb.Settings, NullLogger<CampusService>.Instance),
        TestDb.Settings, _clock, NullLogger<CheckInService>.Instance);

    private string CurrentToken() => QrToken.Create(Guid.Parse(_session.Id), QrToken.StepAt(_clock.GetUtcNow(), 15), Convert.FromBase64String(_session.QrSecret));

    private static CheckInRequest Signed(ECDsa key, string deviceId, string token, double lat = 56.4580, double lng = -2.9800, double acc = 10, bool mocked = false)
    {
        var clientTime = "2026-11-18T10:00:00Z";
        var payload = string.Join('|', token, lat.ToString("F6", CultureInfo.InvariantCulture), lng.ToString("F6", CultureInfo.InvariantCulture),
            acc.ToString("F1", CultureInfo.InvariantCulture), mocked ? "true" : "false", clientTime, deviceId);
        return new CheckInRequest
        {
            Token = token, Lat = lat, Lng = lng, AccuracyM = acc, IsMocked = mocked, ClientTime = clientTime, DeviceId = deviceId,
            Signature = Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256))
        };
    }

    [Fact]
    public async Task CheckIn_ValidScan_Present()
    {
        var result = await ServiceFor(_student).CheckInAsync(Signed(_key, _device.Id, CurrentToken()));

        Assert.Equal(201, result.StatusCode);
        Assert.Equal(nameof(CheckInStatus.Present), result.ResponseData!.Status);
        Assert.Single(_db.CheckIns);
    }

    [Fact]
    public async Task CheckIn_ManualCode_Present()
    {
        var code = QrToken.ManualCode(Guid.Parse(_session.Id), QrToken.StepAt(_clock.GetUtcNow(), 15), Convert.FromBase64String(_session.QrSecret));
        var result = await ServiceFor(_student).CheckInAsync(Signed(_key, _device.Id, code));
        Assert.Equal(nameof(CheckInStatus.Present), result.ResponseData!.Status);
    }

    [Fact]
    public async Task CheckIn_ExpiredToken_RejectedAndPersisted()
    {
        var token = CurrentToken();
        _clock.Advance(TimeSpan.FromSeconds(30));

        var result = await ServiceFor(_student).CheckInAsync(Signed(_key, _device.Id, token));

        Assert.Equal(422, result.StatusCode);
        Assert.Equal(nameof(RejectionReason.ExpiredToken), result.ResponseData!.RejectionReason);
        Assert.Equal(CheckInStatus.Rejected, Assert.Single(_db.CheckIns).Status);   // visible to the lecturer
    }

    [Fact]
    public async Task CheckIn_Repeat_ReturnsExistingRecord()
    {
        var first = await ServiceFor(_student).CheckInAsync(Signed(_key, _device.Id, CurrentToken()));
        var second = await ServiceFor(_student).CheckInAsync(Signed(_key, _device.Id, CurrentToken()));

        Assert.Equal(200, second.StatusCode);
        Assert.True(second.ResponseData!.IsDuplicate);
        Assert.Equal(first.ResponseData!.CheckInId, second.ResponseData.CheckInId);
        Assert.Single(_db.CheckIns);
    }

    [Fact]
    public async Task CheckIn_NotEnrolled_Rejected()
    {
        var outsider = _db.AddUser(UserGroup.Student, "outsider");
        var (device, key) = _db.AddDevice(outsider);
        await _db.SaveChangesAsync();

        var result = await ServiceFor(outsider).CheckInAsync(Signed(key, device.Id, CurrentToken()));
        Assert.Equal(nameof(RejectionReason.NotEnrolled), result.ResponseData!.RejectionReason);
    }

    [Fact]
    public async Task CheckIn_SamePhoneSecondStudent_Rejected()
    {
        await ServiceFor(_student).CheckInAsync(Signed(_key, _device.Id, CurrentToken()));

        // Second account bound to the same phone (same OS install id) after an admin reset slipped through.
        var friend = _db.AddUser(UserGroup.Student, "friend");
        _db.Enrol(_course, friend);
        _db.Devices.Find(_device.Id)!.HardwareId = "android-1";
        var (friendDevice, friendKey) = _db.AddDevice(friend, hardwareId: "android-1");
        await _db.SaveChangesAsync();

        var result = await ServiceFor(friend).CheckInAsync(Signed(friendKey, friendDevice.Id, CurrentToken()));
        Assert.Equal(nameof(RejectionReason.DeviceAlreadyUsed), result.ResponseData!.RejectionReason);
    }

    [Fact]
    public async Task CheckIn_PendingDevice_Rejected()
    {
        var (pending, key) = _db.AddDevice(_student, DeviceStatus.Pending);
        await _db.SaveChangesAsync();

        var result = await ServiceFor(_student).CheckInAsync(Signed(key, pending.Id, CurrentToken()));
        Assert.Equal(nameof(RejectionReason.DeviceNotActive), result.ResponseData!.RejectionReason);
        Assert.Contains("approval", result.ResponseMessage);
    }

    [Fact]
    public async Task CheckIn_SignatureFromAnotherKey_Rejected()
    {
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var result = await ServiceFor(_student).CheckInAsync(Signed(otherKey, _device.Id, CurrentToken()));
        Assert.Equal(nameof(RejectionReason.InvalidSignature), result.ResponseData!.RejectionReason);
    }

    [Fact]
    public async Task CheckIn_OffCampus_Rejected()
    {
        _db.CampusZones.Add(new CampusZone
        {
            Name = "Campus",
            PolygonJson = "[[56.4612,-2.9880],[56.4612,-2.9725],[56.4560,-2.9725],[56.4545,-2.9800],[56.4555,-2.9880]]"
        });
        await _db.SaveChangesAsync();

        var result = await ServiceFor(_student).CheckInAsync(Signed(_key, _device.Id, CurrentToken(), lat: 55.9533, lng: -3.1883));
        Assert.Equal(nameof(RejectionReason.OffCampus), result.ResponseData!.RejectionReason);
    }

    [Fact]
    public async Task Review_FlaggedToPresent_WritesAuditLog()
    {
        var flagged = await ServiceFor(_student).CheckInAsync(Signed(_key, _device.Id, CurrentToken(), mocked: true));
        Assert.Equal(nameof(CheckInStatus.Flagged), flagged.ResponseData!.Status);

        var review = await ServiceFor(_lecturer).ReviewAsync(flagged.ResponseData.CheckInId!, new ReviewCheckInRequest { Decision = ReviewDecision.Present, Note = "Seen" });

        Assert.Equal(200, review.StatusCode);
        Assert.Equal(nameof(CheckInStatus.Present), review.ResponseData!.Status);
        var audit = Assert.Single(_db.AuditLogs);
        Assert.Equal("CheckInReviewed", audit.Action);
        Assert.Equal(_lecturer.Id, audit.ActorId);
    }

    [Fact]
    public async Task Review_OtherLecturersCheckIn_NotFound()
    {
        var flagged = await ServiceFor(_student).CheckInAsync(Signed(_key, _device.Id, CurrentToken(), mocked: true));
        var stranger = _db.AddUser(UserGroup.Lecturer, "stranger");
        await _db.SaveChangesAsync();

        var review = await ServiceFor(stranger).ReviewAsync(flagged.ResponseData!.CheckInId!, new ReviewCheckInRequest { Decision = ReviewDecision.Absent });
        Assert.Equal(404, review.StatusCode);
    }

    [Fact]
    public async Task Review_PresentCheckIn_Unprocessable()
    {
        var present = await ServiceFor(_student).CheckInAsync(Signed(_key, _device.Id, CurrentToken()));
        var review = await ServiceFor(_lecturer).ReviewAsync(present.ResponseData!.CheckInId!, new ReviewCheckInRequest { Decision = ReviewDecision.Absent });
        Assert.Equal(422, review.StatusCode);
    }

    [Fact]
    public async Task Presence_GeofenceExitBefore70Percent_FlagsLeftEarly()
    {
        var checkIn = await ServiceFor(_student).CheckInAsync(Signed(_key, _device.Id, CurrentToken()));
        var result = await ServiceFor(_student).ReportPresenceAsync(checkIn.ResponseData!.CheckInId!, new PresenceReportRequest { Kind = PresenceCheckKind.GeofenceExit });

        Assert.Equal(nameof(PresenceCheckResult.Left), result.ResponseData!.Result);
        Assert.Contains(nameof(CheckInFlag.LeftEarly), result.ResponseData.Flags);
        Assert.Equal(nameof(CheckInStatus.Flagged), result.ResponseData.CheckInStatus);
    }

    [Fact]
    public async Task Presence_GeofenceExitAfter70Percent_Fine()
    {
        var checkIn = await ServiceFor(_student).CheckInAsync(Signed(_key, _device.Id, CurrentToken()));
        _clock.Advance(TimeSpan.FromMinutes(45));   // session started 5 min before; 50/60 min in
        var result = await ServiceFor(_student).ReportPresenceAsync(checkIn.ResponseData!.CheckInId!, new PresenceReportRequest { Kind = PresenceCheckKind.GeofenceExit });

        Assert.Equal(nameof(PresenceCheckResult.Present), result.ResponseData!.Result);
        Assert.Empty(result.ResponseData.Flags);
    }

    [Fact]
    public async Task MyAttendance_CountsFlaggedAsAttended()
    {
        await ServiceFor(_student).CheckInAsync(Signed(_key, _device.Id, CurrentToken(), mocked: true));
        _db.Sessions.Find(_session.Id)!.Status = SessionStatus.Closed;
        _db.Sessions.Add(new Session { CourseId = _course.Id, StartsAt = _clock.GetUtcNow().UtcDateTime.AddDays(-7), EndsAt = _clock.GetUtcNow().UtcDateTime.AddDays(-7).AddHours(1), Status = SessionStatus.Closed });
        await _db.SaveChangesAsync();

        var result = await ServiceFor(_student).GetMyAttendanceAsync();
        var course = Assert.Single(result.ResponseData!.Courses);
        Assert.Equal(2, course.TotalSessions);
        Assert.Equal(1, course.Flagged);
        Assert.Equal(1, course.Absent);
        Assert.Equal(50, course.AttendancePercentage);
    }
}
