using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.Data;
using SeatCharter.Api.Domain;
using SeatCharter.Api.DTOs;
using SeatCharter.Api.DTOs.Requests.Sessions;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Me;
using SeatCharter.Api.DTOs.Responses.Sessions;
using SeatCharter.Api.Models;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.BusinessLogic.Services;

public class SessionService : ISessionService
{
    private readonly ApplicationDbContext _db;
    private readonly UserInfoToken _userInfo;
    private readonly IAttendanceJobService _jobs;
    private readonly ICampusService _campus;
    private readonly AttendanceSettings _settings;
    private readonly DeepLinkSettings _deepLinks;
    private readonly TimeProvider _clock;
    private readonly ILogger<SessionService> _logger;

    public SessionService(ApplicationDbContext db, UserInfoToken userInfo, IAttendanceJobService jobs, ICampusService campus,
        AttendanceSettings settings, DeepLinkSettings deepLinks, TimeProvider clock, ILogger<SessionService> logger)
    {
        _db = db;
        _userInfo = userInfo;
        _jobs = jobs;
        _campus = campus;
        _settings = settings;
        _deepLinks = deepLinks;
        _clock = clock;
        _logger = logger;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private ApiResponseBase<T> Fail500<T>(Exception ex, [CallerMemberName] string method = "")
    {
        _logger.LogError(ex, "{Method} failed", method);
        return ApiResponseBase<T>.ServerError("An error occurred. Please try again.");
    }

    /// <summary>Sessions of courses the caller teaches (admins: all). Lecturer-only reads and writes start here.</summary>
    private IQueryable<Session> Owned()
    {
        var userId = _userInfo.RequireUserId();
        return _userInfo.IsAdmin() ? _db.Sessions : _db.Sessions.Where(s => s.Course.LecturerId == userId);
    }

    /// <summary>Owned, plus sessions of courses the caller is enrolled in.</summary>
    private IQueryable<Session> Visible()
    {
        var userId = _userInfo.RequireUserId();
        if (_userInfo.IsStudent()) return _db.Sessions.Where(s => s.Course.Enrolments.Any(e => e.StudentId == userId));
        return Owned();
    }

    public async Task<ApiResponseBase<SessionDto>> StartAsync(StartSessionRequest request)
    {
        try
        {
            var lecturerId = _userInfo.RequireUserId();
            var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId && c.LecturerId == lecturerId);
            if (course is null) return ApiResponseBase<SessionDto>.NotFound("Course not found.");

            if (await _db.Sessions.AnyAsync(s => s.CourseId == course.Id && s.Status == SessionStatus.Open))
                return ApiResponseBase<SessionDto>.Conflict("This course already has an open session. Close it first.");

            Schedule? schedule = null;
            if (!string.IsNullOrWhiteSpace(request.ScheduleId))
            {
                schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.Id == request.ScheduleId && s.CourseId == course.Id);
                if (schedule is null) return ApiResponseBase<SessionDto>.NotFound("Schedule slot not found for this course.");
            }

            var room = schedule is { RoomLat: { } roomLat, RoomLng: { } roomLng } ? new GeoPoint(roomLat, roomLng) : (GeoPoint?)null;
            var anchorProblem = AnchorRules.Validate(new GeoPoint(request.AnchorLat, request.AnchorLng), request.AnchorAccuracyM,
                request.IsMocked, await _campus.GetPolygonsAsync(), room, schedule?.RoomRadiusM ?? _settings.RoomGeofenceRadiusM,
                schedule?.Room, _settings.MaxAnchorAccuracyM, _settings.MaxCampusAllowanceM);
            if (anchorProblem is not null)
            {
                _logger.LogInformation("Session start refused for course {CourseId}: {Reason}", course.Id, anchorProblem);
                return ApiResponseBase<SessionDto>.UnprocessableEntity(anchorProblem);
            }

            var now = Now;
            var offSchedule = schedule is null ? null : ScheduleRules.OffScheduleWarning(schedule.DayOfWeek, schedule.StartTime,
                schedule.EndTime, TimeZoneInfo.ConvertTimeFromUtc(now, TimeZone()), course.Code, _settings.ScheduleGraceMinutes);
            if (offSchedule is not null && !request.ConfirmOffSchedule)
                return ApiResponseBase<SessionDto>.ConfirmationRequired(offSchedule);

            // Off schedule (e.g. a catch-up class), the slot's end time today means nothing, so run for the slot's length.
            var endsAt = request.DurationMinutes is { } minutes ? now.AddMinutes(minutes)
                : schedule is null ? now.AddMinutes(_settings.DefaultSessionMinutes)
                : offSchedule is not null ? now.Add(SlotLength(schedule))
                : SlotEndToday(schedule, now) ?? now.AddMinutes(_settings.DefaultSessionMinutes);

            var session = new Session
            {
                CourseId = course.Id,
                ScheduleId = schedule?.Id,
                StartsAt = now,
                EndsAt = endsAt,
                Status = SessionStatus.Open,
                AnchorLat = request.AnchorLat,
                AnchorLng = request.AnchorLng,
                AnchorAccuracyM = request.AnchorAccuracyM,
                StepSeconds = _settings.QrStepSeconds
            };
            _db.Sessions.Add(session);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Session {SessionId} opened for course {CourseId} until {EndsAt}", session.Id, course.Id, endsAt);
            return ApiResponseBase<SessionDto>.Created((await ProjectAsync(_db.Sessions.Where(s => s.Id == session.Id)))!, "Session started.");
        }
        catch (Exception ex) { return Fail500<SessionDto>(ex); }
    }

    public async Task<ApiResponseBase<SessionDto>> GetByIdAsync(string id)
    {
        try
        {
            var dto = await ProjectAsync(Visible().Where(s => s.Id == id));
            return dto is null
                ? ApiResponseBase<SessionDto>.NotFound("Session not found.")
                : ApiResponseBase<SessionDto>.Ok(dto, "Session retrieved.");
        }
        catch (Exception ex) { return Fail500<SessionDto>(ex); }
    }

    public async Task<ApiResponseBase<SessionQrDto>> GetQrAsync(string id)
    {
        try
        {
            var session = await Owned().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            if (session is null) return ApiResponseBase<SessionQrDto>.NotFound("Session not found.");
            var now = _clock.GetUtcNow();
            if (session.Status != SessionStatus.Open || now.UtcDateTime > session.EndsAt)
                return ApiResponseBase<SessionQrDto>.UnprocessableEntity("This session is not open.");

            var secret = Convert.FromBase64String(session.QrSecret);
            var sessionGuid = Guid.Parse(session.Id);
            var step = QrToken.StepAt(now, session.StepSeconds);
            var token = QrToken.Create(sessionGuid, step, secret);

            var checkedIn = await _db.CheckIns.CountAsync(c => c.SessionId == id
                && (c.Status == CheckInStatus.Present || c.Status == CheckInStatus.Flagged));
            var enrolled = await _db.Enrolments.CountAsync(e => e.CourseId == session.CourseId);

            return ApiResponseBase<SessionQrDto>.Ok(new SessionQrDto
            {
                SessionId = session.Id,
                Token = token,
                QrPayload = string.IsNullOrWhiteSpace(_deepLinks.PublicBaseUrl) ? token : QrToken.ToDeepLink(_deepLinks.PublicBaseUrl, token),
                ManualCode = QrToken.ManualCode(sessionGuid, step, secret),
                Step = step,
                StepSeconds = session.StepSeconds,
                SecondsRemaining = QrToken.SecondsRemaining(now, session.StepSeconds),
                ServerTime = now.UtcDateTime,
                EndsAt = session.EndsAt,
                CheckedInCount = checkedIn,
                EnrolledCount = enrolled
            }, "QR token issued.");
        }
        catch (Exception ex) { return Fail500<SessionQrDto>(ex); }
    }

    public async Task<ApiResponseBase<SessionDto>> CloseAsync(string id)
    {
        try
        {
            var session = await Owned().FirstOrDefaultAsync(s => s.Id == id);
            if (session is null) return ApiResponseBase<SessionDto>.NotFound("Session not found.");
            if (session.Status != SessionStatus.Open)
                return ApiResponseBase<SessionDto>.UnprocessableEntity("This session is already closed.");

            await using var tx = await _db.Database.BeginTransactionAsync();
            await _jobs.CloseSessionAsync(session);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponseBase<SessionDto>.Ok((await ProjectAsync(_db.Sessions.Where(s => s.Id == id)))!, "Session closed.");
        }
        catch (Exception ex) { return Fail500<SessionDto>(ex); }
    }

    public async Task<ApiResponseBase<PromptResultDto>> TriggerPromptAsync(string id)
    {
        try
        {
            var session = await Owned().FirstOrDefaultAsync(s => s.Id == id);
            if (session is null) return ApiResponseBase<PromptResultDto>.NotFound("Session not found.");
            if (session.Status != SessionStatus.Open)
                return ApiResponseBase<PromptResultDto>.UnprocessableEntity("Prompts can only be sent while the session is open.");

            var (recipients, delivered) = await _jobs.SendPresencePromptAsync(session);
            await _db.SaveChangesAsync();

            return ApiResponseBase<PromptResultDto>.Ok(
                new PromptResultDto { Recipients = recipients, Delivered = delivered, PromptSentAt = session.PromptSentAt!.Value },
                $"Presence prompt sent to {delivered} of {recipients} checked-in student(s).");
        }
        catch (Exception ex) { return Fail500<PromptResultDto>(ex); }
    }

    public async Task<ApiResponseBase<SessionAttendanceDto>> GetAttendanceAsync(string id)
    {
        try
        {
            var sessionDto = await ProjectAsync(Owned().Where(s => s.Id == id));
            if (sessionDto is null) return ApiResponseBase<SessionAttendanceDto>.NotFound("Session not found.");

            var enrolled = await _db.Enrolments.AsNoTracking()
                .Where(e => e.CourseId == sessionDto.CourseId)
                .Select(e => new { e.StudentId, e.Student.FullName, e.Student.MatricNo })
                .ToListAsync();

            var checkIns = await _db.CheckIns.AsNoTracking()
                .Where(c => c.SessionId == id)
                .Include(c => c.PresenceChecks)
                .Include(c => c.Student)
                .OrderBy(c => c.At)
                .ToListAsync();

            var accepted = checkIns.Where(c => c.Status != CheckInStatus.Rejected).ToDictionary(c => c.StudentId);
            var rejected = checkIns.Where(c => c.Status == CheckInStatus.Rejected).ToList();

            // Roster = enrolled students, plus anyone with an accepted check-in who has since been un-enrolled.
            var people = enrolled.Select(e => (e.StudentId, e.FullName, e.MatricNo))
                .Concat(accepted.Values.Where(c => enrolled.All(e => e.StudentId != c.StudentId))
                    .Select(c => (c.StudentId, c.Student.FullName, c.Student.MatricNo)))
                .OrderBy(p => p.FullName)
                .ToList();

            var roster = people.Select(p =>
            {
                accepted.TryGetValue(p.StudentId, out var c);
                return new AttendanceRosterRowDto
                {
                    StudentId = p.StudentId,
                    FullName = p.FullName,
                    MatricNo = p.MatricNo,
                    CheckInId = c?.Id,
                    Status = (c?.Status ?? CheckInStatus.Absent).ToString(),
                    Flags = c?.Flags ?? [],
                    At = c?.At,
                    DistanceFromAnchorM = c?.DistanceFromAnchorM,
                    AccuracyM = c?.AccuracyM,
                    IsMocked = c?.IsMocked ?? false,
                    RejectedAttempts = rejected.Count(r => r.StudentId == p.StudentId),
                    ReviewedAt = c?.ReviewedAt,
                    ReviewNote = c?.ReviewNote,
                    PresenceEvents = c?.PresenceChecks.OrderBy(pc => pc.At)
                        .Select(pc => new PresenceEventDto { Kind = pc.Kind.ToString(), Result = pc.Result.ToString(), At = pc.At })
                        .ToList() ?? []
                };
            }).ToList();

            var dto = new SessionAttendanceDto
            {
                Session = sessionDto,
                Enrolled = enrolled.Count,
                Present = roster.Count(r => r.Status == nameof(CheckInStatus.Present)),
                Flagged = roster.Count(r => r.Status == nameof(CheckInStatus.Flagged)),
                Absent = roster.Count(r => r.Status == nameof(CheckInStatus.Absent)),
                RejectedAttempts = rejected.Count,
                Roster = roster,
                Rejected = rejected.OrderByDescending(r => r.At).Select(r => new RejectedAttemptDto
                {
                    CheckInId = r.Id,
                    StudentId = r.StudentId,
                    FullName = r.Student.FullName,
                    At = r.At,
                    Reason = r.RejectionReason?.ToString() ?? string.Empty
                }).ToList()
            };
            return ApiResponseBase<SessionAttendanceDto>.Ok(dto, "Attendance retrieved.");
        }
        catch (Exception ex) { return Fail500<SessionAttendanceDto>(ex); }
    }

    public async Task<ApiResponseBase<List<TimetableEntryDto>>> GetTimetableAsync(int days)
    {
        try
        {
            days = Math.Clamp(days, 1, 28);
            var userId = _userInfo.RequireUserId();
            var tz = TimeZone();
            var now = Now;
            var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, tz));
            var fromUtc = ToUtc(todayLocal, TimeOnly.MinValue, tz);
            var toUtc = ToUtc(todayLocal.AddDays(days), TimeOnly.MinValue, tz);

            var courses = _userInfo.IsStudent()
                ? _db.Courses.Where(c => c.Enrolments.Any(e => e.StudentId == userId))
                : _userInfo.IsAdmin() ? _db.Courses : _db.Courses.Where(c => c.LecturerId == userId);
            var courseInfo = await courses.AsNoTracking()
                .Select(c => new { c.Id, c.Code, c.Title })
                .ToDictionaryAsync(c => c.Id);
            var courseIds = courseInfo.Keys.ToList();

            var schedules = await _db.Schedules.AsNoTracking().Where(s => courseIds.Contains(s.CourseId)).ToListAsync();
            var sessions = await _db.Sessions.AsNoTracking()
                .Where(s => courseIds.Contains(s.CourseId)
                    && (s.Status == SessionStatus.Open || (s.StartsAt >= fromUtc && s.StartsAt < toUtc)))
                .Select(s => new { s.Id, s.CourseId, s.ScheduleId, s.StartsAt, s.EndsAt, s.Status, Room = s.Schedule != null ? s.Schedule.Room : null })
                .ToListAsync();

            Dictionary<string, CheckInStatus> myStatus = [];
            if (_userInfo.IsStudent())
            {
                var sessionIds = sessions.Select(s => s.Id).ToList();
                myStatus = (await _db.CheckIns.AsNoTracking()
                        .Where(c => c.StudentId == userId && sessionIds.Contains(c.SessionId) && c.Status != CheckInStatus.Rejected)
                        .Select(c => new { c.SessionId, c.Status })
                        .ToListAsync())
                    .ToDictionary(c => c.SessionId, c => c.Status);
            }

            var entries = new List<TimetableEntryDto>();
            var usedSessions = new HashSet<string>();

            for (var d = 0; d < days; d++)
            {
                var date = todayLocal.AddDays(d);
                foreach (var slot in schedules.Where(s => s.DayOfWeek == date.DayOfWeek))
                {
                    var startsAt = ToUtc(date, slot.StartTime, tz);
                    var endsAt = ToUtc(date, slot.EndTime, tz);
                    // A session belongs to this occurrence if it was started from the slot on the same local day.
                    var session = sessions.FirstOrDefault(s => s.ScheduleId == slot.Id
                        && DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(s.StartsAt, tz)) == date);
                    if (session is not null) usedSessions.Add(session.Id);

                    var c = courseInfo[slot.CourseId];
                    entries.Add(new TimetableEntryDto
                    {
                        CourseId = c.Id,
                        CourseCode = c.Code,
                        CourseTitle = c.Title,
                        ScheduleId = slot.Id,
                        SessionId = session?.Id,
                        Room = slot.Room,
                        StartsAt = session?.StartsAt ?? startsAt,
                        EndsAt = session?.EndsAt ?? endsAt,
                        Status = (session is null
                            ? endsAt < now ? TimetableStatus.Ended : TimetableStatus.Upcoming
                            : session.Status == SessionStatus.Open ? TimetableStatus.Open : TimetableStatus.Closed).ToString(),
                        MyCheckInStatus = session is not null && myStatus.TryGetValue(session.Id, out var st) ? st.ToString() : null
                    });
                }
            }

            // Ad-hoc sessions (no slot, or a slot on another day) still show up.
            foreach (var s in sessions.Where(s => !usedSessions.Contains(s.Id)))
            {
                var c = courseInfo[s.CourseId];
                entries.Add(new TimetableEntryDto
                {
                    CourseId = c.Id,
                    CourseCode = c.Code,
                    CourseTitle = c.Title,
                    ScheduleId = s.ScheduleId,
                    SessionId = s.Id,
                    Room = s.Room,
                    StartsAt = s.StartsAt,
                    EndsAt = s.EndsAt,
                    Status = (s.Status == SessionStatus.Open ? TimetableStatus.Open : TimetableStatus.Closed).ToString(),
                    MyCheckInStatus = myStatus.TryGetValue(s.Id, out var st) ? st.ToString() : null
                });
            }

            var ordered = entries.OrderBy(e => e.StartsAt).ToList();
            return ApiResponseBase<List<TimetableEntryDto>>.Ok(ordered, $"{ordered.Count} timetable entr{(ordered.Count == 1 ? "y" : "ies")}.");
        }
        catch (Exception ex) { return Fail500<List<TimetableEntryDto>>(ex); }
    }

    // ── helpers ──

    private TimeZoneInfo TimeZone() => TimeZoneInfo.FindSystemTimeZoneById(_settings.TimeZoneId);

    private static DateTime ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo tz)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (tz.IsInvalidTime(local)) local = local.AddHours(1);   // spring-forward gap
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }

    private TimeSpan SlotLength(Schedule slot)
    {
        var length = slot.EndTime - slot.StartTime;
        return length > TimeSpan.Zero ? length : TimeSpan.FromMinutes(_settings.DefaultSessionMinutes);
    }

    private DateTime? SlotEndToday(Schedule slot, DateTime nowUtc)
    {
        var tz = TimeZone();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz));
        var end = ToUtc(today, slot.EndTime, tz);
        return end > nowUtc.AddMinutes(5) ? end : null;   // slot already (nearly) over → fall back to default length
    }

    private static Task<SessionDto?> ProjectAsync(IQueryable<Session> query) => query.AsNoTracking()
        .Select(s => new SessionDto
        {
            Id = s.Id,
            CourseId = s.CourseId,
            CourseCode = s.Course.Code,
            CourseTitle = s.Course.Title,
            ScheduleId = s.ScheduleId,
            Room = s.Schedule != null ? s.Schedule.Room : null,
            StartsAt = s.StartsAt,
            EndsAt = s.EndsAt,
            Status = s.Status.ToString(),
            AnchorLat = s.AnchorLat,
            AnchorLng = s.AnchorLng,
            AnchorAccuracyM = s.AnchorAccuracyM,
            StepSeconds = s.StepSeconds,
            PromptSentAt = s.PromptSentAt,
            ClosedAt = s.ClosedAt
        })
        .FirstOrDefaultAsync();
}
