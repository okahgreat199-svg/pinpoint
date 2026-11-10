using Microsoft.EntityFrameworkCore;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.Data;
using SeatCharter.Api.Domain;
using SeatCharter.Api.Models;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.BusinessLogic.Services;

public class AttendanceJobService : IAttendanceJobService
{
    private readonly ApplicationDbContext _db;
    private readonly IPushNotificationService _push;
    private readonly AttendanceSettings _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<AttendanceJobService> _logger;

    public AttendanceJobService(ApplicationDbContext db, IPushNotificationService push, AttendanceSettings settings,
        TimeProvider clock, ILogger<AttendanceJobService> logger)
    {
        _db = db;
        _push = push;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public static DateTime PromptDueAt(Session s, double fraction) => s.StartsAt + (s.EndsAt - s.StartsAt) * fraction;

    public async Task<(int Recipients, int Delivered)> SendPresencePromptAsync(Session session, CancellationToken ct = default)
    {
        var checkIns = await _db.CheckIns
            .Where(c => c.SessionId == session.Id && (c.Status == CheckInStatus.Present || c.Status == CheckInStatus.Flagged))
            .Select(c => new { c.Id, c.StudentId })
            .ToListAsync(ct);
        var studentIds = checkIns.Select(c => c.StudentId).ToList();

        var tokens = await _db.Devices
            .Where(d => studentIds.Contains(d.UserId) && d.Status == DeviceStatus.Active && d.PushToken != null)
            .Select(d => new { d.UserId, d.PushToken })
            .ToListAsync(ct);

        var courseCode = await _db.Courses.Where(c => c.Id == session.CourseId).Select(c => c.Code).FirstOrDefaultAsync(ct);
        var deadline = Now.AddMinutes(_settings.PromptResponseMinutes);

        var messages = (from c in checkIns
                        join t in tokens on c.StudentId equals t.UserId
                        select new PushMessage(t.PushToken!, "Are you still in the lecture?",
                            $"Tap to confirm you're still in {courseCode}.",
                            new Dictionary<string, string>
                            {
                                ["type"] = "presence_prompt",
                                ["sessionId"] = session.Id,
                                ["checkInId"] = c.Id,
                                ["respondBy"] = deadline.ToString("O")
                            })).ToList();

        var delivered = await _push.SendAsync(messages, ct);
        session.PromptSentAt = Now;
        session.PromptTimeoutProcessedAt = null;

        _logger.LogInformation("Presence prompt for session {SessionId}: {Recipients} checked in, {Delivered} delivered",
            session.Id, checkIns.Count, delivered);
        return (checkIns.Count, delivered);
    }

    public async Task CloseSessionAsync(Session session, CancellationToken ct = default)
    {
        session.Status = SessionStatus.Closed;
        session.ClosedAt = Now;
        if (session.EndsAt > Now) session.EndsAt = Now;   // closed early: keep the window honest

        // BR-11. Low-accuracy fixes are "unverified", not evidence, so they don't shape the cluster.
        var candidates = await _db.CheckIns
            .Where(c => c.SessionId == session.Id
                && (c.Status == CheckInStatus.Present || c.Status == CheckInStatus.Flagged)
                && c.Lat != null && c.Lng != null && c.AccuracyM <= _settings.PoorAccuracyM)
            .ToListAsync(ct);

        var outliers = GeoMath.FindClusterOutliers(
            candidates.Select(c => (c.Id, new GeoPoint(c.Lat!.Value, c.Lng!.Value))).ToList(),
            _settings.OutlierMinDistanceM, _settings.OutlierMadMultiplier);

        foreach (var c in candidates.Where(c => outliers.Contains(c.Id)))
            c.AddFlag(CheckInFlag.ClusterOutlier);

        _logger.LogInformation("Session {SessionId} closed; {Outliers} cluster outlier(s) of {Candidates}",
            session.Id, outliers.Count, candidates.Count);
    }

    public async Task<int> SendDuePromptsAsync(CancellationToken ct = default)
    {
        var open = await _db.Sessions
            .Where(s => s.Status == SessionStatus.Open && s.PromptSentAt == null)
            .ToListAsync(ct);
        var due = open.Where(s => PromptDueAt(s, _settings.PromptAtFraction) <= Now && s.EndsAt > Now).ToList();

        foreach (var s in due)
        {
            await SendPresencePromptAsync(s, ct);
            await _db.SaveChangesAsync(ct);
        }
        return due.Count;
    }

    public async Task<int> ProcessPromptTimeoutsAsync(CancellationToken ct = default)
    {
        var cutoff = Now.AddMinutes(-_settings.PromptResponseMinutes);
        var sessions = await _db.Sessions
            .Where(s => s.PromptSentAt != null && s.PromptSentAt <= cutoff && s.PromptTimeoutProcessedAt == null)
            .ToListAsync(ct);

        foreach (var s in sessions)
        {
            var silent = await _db.CheckIns
                .Where(c => c.SessionId == s.Id
                    && (c.Status == CheckInStatus.Present || c.Status == CheckInStatus.Flagged)
                    && !c.PresenceChecks.Any(p => p.Kind == PresenceCheckKind.Prompt70))
                .ToListAsync(ct);

            foreach (var c in silent)
            {
                _db.PresenceChecks.Add(new PresenceCheck
                {
                    CheckInId = c.Id,
                    Kind = PresenceCheckKind.Prompt70,
                    At = Now,
                    Result = PresenceCheckResult.NoResponse
                });
                c.AddFlag(CheckInFlag.NoPresenceResponse);   // BR-12; a person decides, never auto-absent
            }
            s.PromptTimeoutProcessedAt = Now;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Session {SessionId}: {Count} student(s) didn't answer the presence prompt", s.Id, silent.Count);
        }
        return sessions.Count;
    }

    public async Task<int> AutoCloseExpiredSessionsAsync(CancellationToken ct = default)
    {
        var expired = await _db.Sessions
            .Where(s => s.Status == SessionStatus.Open && s.EndsAt <= Now)
            .ToListAsync(ct);

        foreach (var s in expired)
        {
            await CloseSessionAsync(s, ct);
            await _db.SaveChangesAsync(ct);
        }
        return expired.Count;
    }

    public async Task<int> PurgeExpiredLocationsAsync(CancellationToken ct = default)
    {
        var cutoff = Now.AddDays(-_settings.LocationRetentionDays);
        var ids = await _db.Sessions
            .Where(s => s.Status == SessionStatus.Closed && s.ClosedAt <= cutoff && s.LocationsPurgedAt == null)
            .OrderBy(s => s.ClosedAt)
            .Select(s => s.Id)
            .Take(50)
            .ToListAsync(ct);
        if (ids.Count == 0) return 0;

        // Status and flags stay; only raw coordinates go.
        await _db.CheckIns.Where(c => ids.Contains(c.SessionId))
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.Lat, (double?)null).SetProperty(c => c.Lng, (double?)null), ct);
        await _db.PresenceChecks.Where(p => ids.Contains(p.CheckIn.SessionId))
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.Lat, (double?)null).SetProperty(p => p.Lng, (double?)null), ct);
        var now = Now;
        await _db.Sessions.Where(s => ids.Contains(s.Id))
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.LocationsPurgedAt, now), ct);

        _logger.LogInformation("Purged raw locations for {Count} session(s)", ids.Count);
        return ids.Count;
    }
}
