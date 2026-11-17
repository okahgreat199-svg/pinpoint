using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using SeatCharter.Api.BusinessLogic.Services;
using SeatCharter.Api.Data;
using SeatCharter.Api.DTOs.Requests.Devices;
using SeatCharter.Api.Models;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Tests;

public class DeviceServiceTests
{
    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly User _lecturer;
    private readonly User _student;

    public DeviceServiceTests()
    {
        _lecturer = _db.AddUser(UserGroup.Lecturer, "lec");
        _student = _db.AddUser(UserGroup.Student, "stu");
        var course = _db.AddCourse(_lecturer);
        _db.Enrol(course, _student);
        _db.SaveChanges();
    }

    private DeviceService ServiceFor(User u) => new(_db, TestDb.As(u), new FakeClock(), NullLogger<DeviceService>.Instance);

    private static RegisterDeviceRequest NewKey(string? hardwareId = null)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new RegisterDeviceRequest { PublicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), Platform = "android", Model = "Pixel", HardwareId = hardwareId };
    }

    [Fact]
    public async Task Register_FirstDevice_Active()
    {
        var result = await ServiceFor(_student).RegisterAsync(NewKey());
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(nameof(DeviceStatus.Active), result.ResponseData!.Status);
    }

    [Fact]
    public async Task Register_SecondDevice_PendingUntilApproved_ThenOldRevoked()
    {
        var first = await ServiceFor(_student).RegisterAsync(NewKey());
        var second = await ServiceFor(_student).RegisterAsync(NewKey());
        Assert.Equal(nameof(DeviceStatus.Pending), second.ResponseData!.Status);

        var approved = await ServiceFor(_lecturer).ApproveAsync(second.ResponseData.Id);

        Assert.Equal(nameof(DeviceStatus.Active), approved.ResponseData!.Status);
        Assert.Equal(DeviceStatus.Revoked, _db.Devices.Find(first.ResponseData!.Id)!.Status);
        Assert.Equal("DeviceApproved", Assert.Single(_db.AuditLogs).Action);
    }

    [Fact]
    public async Task Register_SameKeyAgain_Idempotent()
    {
        var req = NewKey();
        var first = await ServiceFor(_student).RegisterAsync(req);
        var again = await ServiceFor(_student).RegisterAsync(req);

        Assert.Equal(200, again.StatusCode);
        Assert.Equal(first.ResponseData!.Id, again.ResponseData!.Id);
    }

    [Fact]
    public async Task Register_PhoneBoundToAnotherStudent_Conflict()
    {
        await ServiceFor(_student).RegisterAsync(NewKey(hardwareId: "android-1"));
        var other = _db.AddUser(UserGroup.Student, "other");
        await _db.SaveChangesAsync();

        var result = await ServiceFor(other).RegisterAsync(NewKey(hardwareId: "android-1"));
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidKey_BadRequest()
    {
        var result = await ServiceFor(_student).RegisterAsync(new RegisterDeviceRequest { PublicKey = "bm90IGEga2V5", Platform = "android", Model = "x" });
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Approve_StudentNotInMyCourses_NotFound()
    {
        await ServiceFor(_student).RegisterAsync(NewKey());
        var pending = await ServiceFor(_student).RegisterAsync(NewKey());
        var stranger = _db.AddUser(UserGroup.Lecturer, "stranger");
        await _db.SaveChangesAsync();

        Assert.Equal(404, (await ServiceFor(stranger).ApproveAsync(pending.ResponseData!.Id)).StatusCode);
        Assert.Empty((await ServiceFor(stranger).GetPendingAsync()).ResponseData!);
    }
}
