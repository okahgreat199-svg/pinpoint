using Microsoft.Extensions.Logging.Abstractions;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.BusinessLogic.Services;
using SeatCharter.Api.Data;
using SeatCharter.Api.Models;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Tests;

public class AttendanceJobServiceTests
{
    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly FakeClock _clock = new();
    private readonly Course _course;
    private readonly Session _session;

    private sealed class NoPush : IPushNotificationService
    {
        public bool IsConfigured => false;
        public List<PushMessage> Sent { get; } = [];
        public Task<int> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct = default)
        {
            Sent.AddRange(messages);
            return Task.FromResult(messages.Count);
        }
    }

    private readonly NoPush _push = new();

    public AttendanceJobServiceTests()
    {
        var lecturer = _db.AddUser(UserGroup.Lecturer, "lec");
        _course = _db.AddCourse(lecturer);
        _session = _db.AddOpenSession(_course, _clock.GetUtcNow().UtcDateTime.AddMinutes(-10), minutes: 60);
        _db.SaveChanges();
    }

    private AttendanceJobService Jobs => new(_db, _push, TestDb.Settings, _clock, NullLogger<AttendanceJobService>.Instance);

    private CheckIn AddCheckIn(string name, double lat, double lng, double acc = 10)
    {
        var s = _db.AddUser(UserGroup.Student, name);
        var (d, _) = _db.AddDevice(s);
        d.PushToken = $"fcm-{name}";
        var c = new CheckIn { SessionId = _session.Id, StudentId = s.Id, DeviceId = d.Id, Lat = lat, Lng = lng, AccuracyM = acc, Status = CheckInStatus.Present };
        _db.CheckIns.Add(c);
        _db.SaveChanges();
        return c;
    }

    [Fact]
    public async Task CloseSession_FlagsClusterOutlier_BR11()
    {
        var near = new[] { AddCheckIn("a", 56.45800, -2.98000), AddCheckIn("b", 56.45805, -2.98005), AddCheckIn("c", 56.45798, -2.97995), AddCheckIn("d", 56.45802, -2.98003) };
        var far = AddCheckIn("far", 56.46000, -2.98000);

        await Jobs.CloseSessionAsync(_session);
        await _db.SaveChangesAsync();

        Assert.Equal(SessionStatus.Closed, _session.Status);
        Assert.Equal(CheckInStatus.Flagged, far.Status);
        Assert.Contains(nameof(CheckInFlag.ClusterOutlier), far.Flags);
        Assert.All(near, c => Assert.Equal(CheckInStatus.Present, c.Status));
    }

    [Fact]
    public async Task CloseSession_LowAccuracyFixesIgnoredByCluster()
    {
        AddCheckIn("a", 56.45800, -2.98000); AddCheckIn("b", 56.45805, -2.98005); AddCheckIn("c", 56.45798, -2.97995);
        var noisy = AddCheckIn("noisy", 56.46000, -2.98000, acc: 80);

        await Jobs.CloseSessionAsync(_session);
        Assert.DoesNotContain(nameof(CheckInFlag.ClusterOutlier), noisy.Flags);
    }

    [Fact]
    public async Task Prompt_SentAt70Percent_OnlyOnce()
    {
        AddCheckIn("a", 56.458, -2.98);
        Assert.Equal(0, await Jobs.SendDuePromptsAsync());   // 10 of 60 min in

        _clock.Advance(TimeSpan.FromMinutes(33));               // 43 of 60 min ≈ 72%
        Assert.Equal(1, await Jobs.SendDuePromptsAsync());
        Assert.Equal(0, await Jobs.SendDuePromptsAsync());
        var msg = Assert.Single(_push.Sent);
        Assert.Equal("presence_prompt", msg.Data["type"]);
    }

    [Fact]
    public async Task PromptTimeout_NonResponderFlagged_ResponderNot_BR12()
    {
        var silent = AddCheckIn("silent", 56.458, -2.98);
        var answered = AddCheckIn("answered", 56.458, -2.98);
        await Jobs.SendPresencePromptAsync(_session);
        _db.PresenceChecks.Add(new PresenceCheck { CheckInId = answered.Id, Kind = PresenceCheckKind.Prompt70, At = _clock.GetUtcNow().UtcDateTime, Result = PresenceCheckResult.Present });
        await _db.SaveChangesAsync();

        _clock.Advance(TimeSpan.FromMinutes(9));
        Assert.Equal(0, await Jobs.ProcessPromptTimeoutsAsync());
        _clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, await Jobs.ProcessPromptTimeoutsAsync());

        Assert.Contains(nameof(CheckInFlag.NoPresenceResponse), silent.Flags);
        Assert.Equal(CheckInStatus.Flagged, silent.Status);       // flagged, never auto-absent
        Assert.Equal(CheckInStatus.Present, answered.Status);
    }

    [Fact]
    public async Task AutoClose_AtEndsAt()
    {
        _clock.Advance(TimeSpan.FromMinutes(49));
        Assert.Equal(0, await Jobs.AutoCloseExpiredSessionsAsync());
        _clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, await Jobs.AutoCloseExpiredSessionsAsync());
        Assert.Equal(SessionStatus.Closed, _session.Status);
    }

    [Fact]
    public void AddFlag_NeverOverridesLecturersAbsentDecision()
    {
        var c = new CheckIn { Status = CheckInStatus.Absent };
        c.AddFlag(CheckInFlag.LeftEarly);
        Assert.Equal(CheckInStatus.Absent, c.Status);
    }
}
