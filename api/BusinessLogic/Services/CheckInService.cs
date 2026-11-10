using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.Data;
using SeatCharter.Api.Domain;
using SeatCharter.Api.DTOs;
using SeatCharter.Api.DTOs.Requests.CheckIns;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.CheckIns;
using SeatCharter.Api.DTOs.Responses.Me;
using SeatCharter.Api.Models;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.BusinessLogic.Services;

public class CheckInService : ICheckInService
{
    private readonly ApplicationDbContext _db;
    private readonly UserInfoToken _userInfo;
    private readonly ICampusService _campus;
    private readonly AttendanceSettings _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<CheckInService> _logger;

    public CheckInService(ApplicationDbContext db, UserInfoToken userInfo, ICampusService campus,
        AttendanceSettings settings, TimeProvider clock, ILogger<CheckInService> logger)
    {
        _db = db;
        _userInfo = userInfo;
        _campus = campus;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    private ApiResponseBase<T> Fail500<T>(Exception ex, [CallerMemberName] string method = "")
    {
        _logger.LogError(ex, "{Method} failed", method);
        return ApiResponseBase<T>.ServerError("An error occurred. Please try again.");
    }

    private CheckInRuleOptions RuleOptions => new()
    {
        AnchorFlagDistanceM = _settings.AnchorFlagDistanceM,
        PoorAccuracyM = _settings.PoorAccuracyM,
        MaxCampusAllowanceM = _settings.MaxCampusAllowanceM
    };

    public async Task<ApiResponseBase<CheckInResultDto>> CheckInAsync(CheckInRequest request)
    {
        try
        {
            var studentId = _userInfo.RequireUserId();
            var nowOffset = _clock.GetUtcNow();
            var now = nowOffset.UtcDateTime;
            var token = QrToken.ExtractToken(request.Token);   // signature still covers request.Token exactly as sent

            // ── Load facts ──
            var device = await _db.Devices.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.UserId == studentId);

            var signatureValid = device is not null && DeviceSignature.Verify(device.PublicKey,
                DeviceSignature.CanonicalPayload(request.Token, request.Lat, request.Lng, request.AccuracyM,
                    request.IsMocked, request.ClientTime, request.DeviceId),
                request.Signature);

            var (session, tokenResult) = await ResolveSessionAsync(token, studentId, nowOffset);

            var enrolled = session is not null
                && await _db.Enrolments.AnyAsync(e => e.CourseId == session.CourseId && e.StudentId == studentId);

            // BR-5: same phone (by id, key or OS install id) already used for someone else in this session.
            var deviceUsedElsewhere = session is not null && device is not null && await _db.CheckIns.AnyAsync(c =>
                c.SessionId == session.Id && c.StudentId != studentId && c.Status != CheckInStatus.Rejected
                && (c.DeviceId == device.Id
                    || c.Device!.PublicKey == device.PublicKey
                    || (device.HardwareId != null && c.Device!.HardwareId == device.HardwareId)));

            var existing = session is null ? null : await _db.CheckIns.AsNoTracking()
                .FirstOrDefaultAsync(c => c.SessionId == session.Id && c.StudentId == studentId && c.Status != CheckInStatus.Rejected);

            var decision = CheckInRuleEngine.Evaluate(new CheckInFacts
            {
                DeviceFound = device is not null,
                SignatureValid = signatureValid,
                Token = tokenResult,
                SessionStatus = session?.Status ?? SessionStatus.Closed,
                SessionStartsAt = session?.StartsAt ?? DateTime.MinValue,
                SessionEndsAt = session?.EndsAt ?? DateTime.MinValue,
                Now = now,
                Enrolled = enrolled,
                DeviceStatus = device?.Status ?? DeviceStatus.Revoked,
                DeviceUsedByAnotherStudentThisSession = deviceUsedElsewhere,
                AlreadyCheckedIn = existing is not null,
                Location = new GeoPoint(request.Lat, request.Lng),
                AccuracyM = request.AccuracyM,
                IsMocked = request.IsMocked,
                Anchor = session is null ? default : new GeoPoint(session.AnchorLat, session.AnchorLng),
                CampusZones = await _campus.GetPolygonsAsync()
            }, RuleOptions);

            // BR-6: a repeat returns the existing record.
            if (decision.IsDuplicate)
                return ApiResponseBase<CheckInResultDto>.Ok(MapResult(existing!, session!, isDuplicate: true), "You're already checked in.");

            if (session is null)
            {
                // Can't tie the attempt to a session, so there's nothing to persist against.
                _logger.LogInformation("Check-in by {UserId} rejected before session lookup: {Reason}", studentId, decision.RejectionReason);
                var orphan = new CheckInResultDto
                {
                    Status = nameof(CheckInStatus.Rejected),
                    RejectionReason = decision.RejectionReason?.ToString(),
                    Message = RejectionMessage(decision.RejectionReason!.Value, device),
                    At = now
                };
                return new ApiResponseBase<CheckInResultDto>(orphan, StatusCodes.Status422UnprocessableEntity, orphan.Message, false);
            }

            // Persist one row either way, so rejected attempts show up for the lecturer.
            var checkIn = new CheckIn
            {
                SessionId = session.Id,
                StudentId = studentId,
                DeviceId = device?.Id,
                At = now,
                ClientTime = DateTimeOffset.TryParse(request.ClientTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var ct)
                    ? ct.UtcDateTime : null,
                Lat = request.Lat,
                Lng = request.Lng,
                AccuracyM = request.AccuracyM,
                IsMocked = request.IsMocked,
                DistanceFromAnchorM = decision.DistanceFromAnchorM,
                Status = decision.Status,
                RejectionReason = decision.RejectionReason,
                Flags = decision.Flags.Select(f => f.ToString()).ToList()
            };
            _db.CheckIns.Add(checkIn);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException) when (decision.Status != CheckInStatus.Rejected)
            {
                // Lost a race with a concurrent attempt (unique index on session+student or session+device).
                _db.Entry(checkIn).State = EntityState.Detached;
                var winner = await _db.CheckIns.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.SessionId == session.Id && c.StudentId == studentId && c.Status != CheckInStatus.Rejected);
                if (winner is not null)
                    return ApiResponseBase<CheckInResultDto>.Ok(MapResult(winner, session, isDuplicate: true), "You're already checked in.");
                throw;
            }

            _logger.LogInformation("Check-in {CheckInId} for session {SessionId} by {UserId}: {Status} {Reason}",
                checkIn.Id, session.Id, studentId, checkIn.Status, (object?)checkIn.RejectionReason ?? string.Join(",", checkIn.Flags));

            var result = MapResult(checkIn, session, isDuplicate: false, device);
            return checkIn.Status == CheckInStatus.Rejected
                ? new ApiResponseBase<CheckInResultDto>(result, StatusCodes.Status422UnprocessableEntity, result.Message, false)
                : ApiResponseBase<CheckInResultDto>.Created(result, result.Message);
        }
        catch (Exception ex) { return Fail500<CheckInResultDto>(ex); }
    }

    public async Task<ApiResponseBase<PresenceResultDto>> ReportPresenceAsync(string checkInId, PresenceReportRequest request)
    {
        try
        {
            var studentId = _userInfo.RequireUserId();
            var now = _clock.GetUtcNow().UtcDateTime;
            var checkIn = await _db.CheckIns
                .Include(c => c.Session)
                .Include(c => c.PresenceChecks)
                .FirstOrDefaultAsync(c => c.Id == checkInId && c.StudentId == studentId && c.Status != CheckInStatus.Rejected);
            if (checkIn is null) return ApiResponseBase<PresenceResultDto>.NotFound("Check-in not found.");
            var session = checkIn.Session;

            PresenceCheckResult result;
            if (request.Kind == PresenceCheckKind.GeofenceExit)
            {
                if (session.Status != SessionStatus.Open || now > session.EndsAt)
                    return ApiResponseBase<PresenceResultDto>.UnprocessableEntity("This session has ended, so no presence report is needed.");

                // BR-12: leaving before 70% of the session counts as leaving early.
                var leftEarly = now < AttendanceJobService.PromptDueAt(session, _settings.PromptAtFraction);
                result = leftEarly ? PresenceCheckResult.Left : PresenceCheckResult.Present;
                if (leftEarly) checkIn.AddFlag(CheckInFlag.LeftEarly);
            }
            else
            {
                if (session.PromptSentAt is null)
                    return ApiResponseBase<PresenceResultDto>.UnprocessableEntity("There is no presence prompt for this session yet.");

                var answered = checkIn.PresenceChecks.FirstOrDefault(p => p.Kind == PresenceCheckKind.Prompt70 && p.At >= session.PromptSentAt);
                if (answered is not null)
                    return ApiResponseBase<PresenceResultDto>.Ok(MapPresence(checkIn, answered), "You've already answered this prompt.");

                if (now > session.PromptSentAt.Value.AddMinutes(_settings.PromptResponseMinutes))
                    return ApiResponseBase<PresenceResultDto>.UnprocessableEntity("The presence prompt has expired.");

                // A fix far from the anchor while answering "still here" contradicts the answer: flag it for review.
                var away = request.Lat is { } lat && request.Lng is { } lng
                    && (request.AccuracyM ?? double.MaxValue) <= _settings.PoorAccuracyM
                    && GeoMath.HaversineM(new GeoPoint(lat, lng), new GeoPoint(session.AnchorLat, session.AnchorLng)) > _settings.AnchorFlagDistanceM;
                result = away ? PresenceCheckResult.Left : PresenceCheckResult.Present;
                if (away) checkIn.AddFlag(CheckInFlag.LeftEarly);
            }

            var presence = new PresenceCheck
            {
                CheckInId = checkIn.Id,
                Kind = request.Kind,
                At = now,
                Lat = request.Lat,
                Lng = request.Lng,
                AccuracyM = request.AccuracyM,
                Result = result
            };
            _db.PresenceChecks.Add(presence);
            await _db.SaveChangesAsync();

            return ApiResponseBase<PresenceResultDto>.Ok(MapPresence(checkIn, presence),
                result == PresenceCheckResult.Present ? "Thanks, your presence is confirmed." : "Recorded. Your lecturer will review this.");
        }
        catch (Exception ex) { return Fail500<PresenceResultDto>(ex); }
    }

    public async Task<ApiResponseBase<CheckInDto>> ReviewAsync(string checkInId, ReviewCheckInRequest request)
    {
        try
        {
            var reviewerId = _userInfo.RequireUserId();
            var query = _db.CheckIns.Where(c => c.Id == checkInId);
            if (!_userInfo.IsAdmin()) query = query.Where(c => c.Session.Course.LecturerId == reviewerId);

            var checkIn = await query.FirstOrDefaultAsync();
            if (checkIn is null) return ApiResponseBase<CheckInDto>.NotFound("Check-in not found.");
            if (checkIn.Status != CheckInStatus.Flagged)
                return ApiResponseBase<CheckInDto>.UnprocessableEntity($"Only flagged check-ins can be reviewed. This one is {checkIn.Status}.");

            var from = checkIn.Status;
            checkIn.Status = request.Decision == ReviewDecision.Present ? CheckInStatus.Present : CheckInStatus.Absent;
            checkIn.ReviewedById = reviewerId;
            checkIn.ReviewedAt = _clock.GetUtcNow().UtcDateTime;
            checkIn.ReviewNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

            _db.AuditLogs.Add(new AuditLog
            {
                ActorId = reviewerId,
                Action = "CheckInReviewed",
                EntityType = nameof(CheckIn),
                EntityId = checkIn.Id,
                At = checkIn.ReviewedAt.Value,
                DataJson = JsonSerializer.Serialize(new
                {
                    checkIn.SessionId,
                    checkIn.StudentId,
                    from = from.ToString(),
                    to = checkIn.Status.ToString(),
                    flags = checkIn.Flags,
                    note = checkIn.ReviewNote
                })
            });
            await _db.SaveChangesAsync();

            return ApiResponseBase<CheckInDto>.Ok(MapToDto(checkIn), $"Check-in marked {checkIn.Status}.");
        }
        catch (Exception ex) { return Fail500<CheckInDto>(ex); }
    }

    public async Task<ApiResponseBase<MyAttendanceDto>> GetMyAttendanceAsync()
    {
        try
        {
            var studentId = _userInfo.RequireUserId();
            var courses = await _db.Courses.AsNoTracking()
                .Where(c => c.Enrolments.Any(e => e.StudentId == studentId))
                .OrderBy(c => c.Code)
                .Select(c => new { c.Id, c.Code, c.Title })
                .ToListAsync();
            var courseIds = courses.Select(c => c.Id).ToList();

            var sessions = await _db.Sessions.AsNoTracking()
                .Where(s => courseIds.Contains(s.CourseId) && s.Status != SessionStatus.Scheduled)
                .OrderByDescending(s => s.StartsAt)
                .Select(s => new { s.Id, s.CourseId, s.StartsAt, s.EndsAt, s.Status })
                .ToListAsync();
            var sessionIds = sessions.Select(s => s.Id).ToList();

            var checkIns = await _db.CheckIns.AsNoTracking()
                .Where(c => c.StudentId == studentId && sessionIds.Contains(c.SessionId))
                .OrderBy(c => c.At)
                .ToListAsync();

            var dto = new MyAttendanceDto();
            foreach (var course in courses)
            {
                var rows = sessions.Where(s => s.CourseId == course.Id).Select(s =>
                {
                    var mine = checkIns.Where(c => c.SessionId == s.Id).ToList();
                    var accepted = mine.FirstOrDefault(c => c.Status != CheckInStatus.Rejected);
                    var outcome = accepted?.Status switch
                    {
                        CheckInStatus.Present => AttendanceOutcome.Present,
                        CheckInStatus.Flagged => AttendanceOutcome.Flagged,
                        CheckInStatus.Absent => AttendanceOutcome.Absent,
                        _ => s.Status == SessionStatus.Open ? AttendanceOutcome.Pending : AttendanceOutcome.Absent
                    };
                    return new MySessionAttendanceDto
                    {
                        SessionId = s.Id,
                        StartsAt = s.StartsAt,
                        EndsAt = s.EndsAt,
                        SessionStatus = s.Status.ToString(),
                        Outcome = outcome.ToString(),
                        CheckInId = accepted?.Id,
                        Flags = accepted?.Flags ?? [],
                        LastRejectionReason = mine.LastOrDefault(c => c.Status == CheckInStatus.Rejected)?.RejectionReason?.ToString(),
                        Reviewed = accepted?.ReviewedAt is not null
                    };
                }).ToList();

                var closed = rows.Where(r => r.SessionStatus == nameof(SessionStatus.Closed)).ToList();
                var present = closed.Count(r => r.Outcome == nameof(AttendanceOutcome.Present));
                var flagged = closed.Count(r => r.Outcome == nameof(AttendanceOutcome.Flagged));
                dto.Courses.Add(new CourseAttendanceDto
                {
                    CourseId = course.Id,
                    CourseCode = course.Code,
                    CourseTitle = course.Title,
                    TotalSessions = closed.Count,
                    Present = present,
                    Flagged = flagged,
                    Absent = closed.Count - present - flagged,
                    AttendancePercentage = closed.Count == 0 ? 0 : Math.Round(100.0 * (present + flagged) / closed.Count, 1),
                    Sessions = rows
                });
            }

            return ApiResponseBase<MyAttendanceDto>.Ok(dto, "Attendance history retrieved.");
        }
        catch (Exception ex) { return Fail500<MyAttendanceDto>(ex); }
    }

    // ── helpers ──

    /// <summary>
    /// QR tokens carry their session id. A 6-digit manual code doesn't, so it is matched against the open
    /// sessions of the student's courses (normally just one).
    /// </summary>
    private async Task<(Session? Session, QrValidation Result)> ResolveSessionAsync(string token, string studentId, DateTimeOffset now)
    {
        if (QrToken.IsManualCodeShape(token))
        {
            var candidates = await _db.Sessions.AsNoTracking()
                .Where(s => s.Status == SessionStatus.Open && s.Course.Enrolments.Any(e => e.StudentId == studentId))
                .ToListAsync();
            foreach (var s in candidates)
            {
                if (QrToken.ValidateManualCode(token, Guid.Parse(s.Id), Convert.FromBase64String(s.QrSecret), now, s.StepSeconds) == QrValidation.Valid)
                    return (s, QrValidation.Valid);
            }
            // One open session: attribute the failed attempt to it so the lecturer sees it.
            return candidates.Count == 1 ? (candidates[0], QrValidation.Expired) : (null, QrValidation.Malformed);
        }

        if (!QrToken.TryReadSessionId(token, out var sessionGuid)) return (null, QrValidation.Malformed);

        var session = await _db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionGuid.ToString());
        if (session is null) return (null, QrValidation.Malformed);

        return (session, QrToken.Validate(token, sessionGuid, Convert.FromBase64String(session.QrSecret), now, session.StepSeconds));
    }

    private CheckInResultDto MapResult(CheckIn c, Session s, bool isDuplicate, Device? device = null)
    {
        var accepted = c.Status != CheckInStatus.Rejected;
        return new CheckInResultDto
        {
            CheckInId = c.Id,
            SessionId = c.SessionId,
            Status = c.Status.ToString(),
            RejectionReason = c.RejectionReason?.ToString(),
            Flags = c.Flags,
            Message = c.Status switch
            {
                CheckInStatus.Rejected => RejectionMessage(c.RejectionReason!.Value, device),
                CheckInStatus.Flagged => $"You're checked in. Your lecturer will review: {string.Join("; ", c.Flags.Select(FlagMessage))}.",
                CheckInStatus.Absent => "Your lecturer has marked you absent for this session.",
                _ => "You're checked in."
            },
            At = c.At,
            DistanceFromAnchorM = c.DistanceFromAnchorM,
            IsDuplicate = isDuplicate,
            AnchorLat = accepted ? s.AnchorLat : null,
            AnchorLng = accepted ? s.AnchorLng : null,
            GeofenceRadiusM = accepted ? _settings.RoomGeofenceRadiusM : null,
            SessionEndsAt = accepted ? s.EndsAt : null
        };
    }

    private static string RejectionMessage(RejectionReason reason, Device? device) => reason switch
    {
        RejectionReason.UnknownDevice => "This phone isn't registered to your account. Register it from the device screen.",
        RejectionReason.InvalidSignature => "This check-in couldn't be verified as coming from your registered phone.",
        RejectionReason.InvalidToken => "That isn't a valid attendance code.",
        RejectionReason.ExpiredToken => "That code has expired. Scan the code currently on screen.",
        RejectionReason.SessionNotOpen => "This session isn't open for check-in.",
        RejectionReason.NotEnrolled => "You aren't enrolled in this course.",
        RejectionReason.DeviceNotActive => device?.Status == DeviceStatus.Pending
            ? "This phone is waiting for lecturer approval."
            : "This phone has been revoked. Register it again or contact your lecturer.",
        RejectionReason.DeviceAlreadyUsed => "This phone has already checked in another student for this session.",
        RejectionReason.OffCampus => "Your location is outside the campus area.",
        _ => "Check-in rejected."
    };

    private static string FlagMessage(string flag) => flag switch
    {
        nameof(CheckInFlag.FarFromLecturer) => "far from the lecturer",
        nameof(CheckInFlag.LocationUnverified) => "location accuracy too low to verify",
        nameof(CheckInFlag.MockLocation) => "mock location detected",
        nameof(CheckInFlag.ClusterOutlier) => "away from where the class checked in",
        nameof(CheckInFlag.LeftEarly) => "left before 70% of the session",
        nameof(CheckInFlag.NoPresenceResponse) => "no answer to the presence prompt",
        _ => flag
    };

    private static PresenceResultDto MapPresence(CheckIn c, PresenceCheck p) => new()
    {
        CheckInId = c.Id,
        Kind = p.Kind.ToString(),
        Result = p.Result.ToString(),
        CheckInStatus = c.Status.ToString(),
        Flags = c.Flags
    };

    private static CheckInDto MapToDto(CheckIn c) => new()
    {
        Id = c.Id,
        SessionId = c.SessionId,
        StudentId = c.StudentId,
        Status = c.Status.ToString(),
        Flags = c.Flags,
        RejectionReason = c.RejectionReason?.ToString(),
        At = c.At,
        ReviewedAt = c.ReviewedAt,
        ReviewedById = c.ReviewedById,
        ReviewNote = c.ReviewNote
    };
}
