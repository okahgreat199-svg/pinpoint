using SeatCharter.Api.Models;

namespace SeatCharter.Api.BusinessLogic.Interfaces;

/// <summary>
/// Session lifecycle work shared by the background worker and lecturer actions. Has no UserInfoToken
/// dependency, so it's safe to call from a job scope. Returns plain values (not envelopes) because
/// callers are other services and the worker, never controllers.
/// </summary>
public interface IAttendanceJobService
{
    /// <summary>BR-12: send the presence prompt to every checked-in student. Sets PromptSentAt. Caller saves.</summary>
    Task<(int Recipients, int Delivered)> SendPresencePromptAsync(Session session, CancellationToken ct = default);

    /// <summary>BR-11: close the session and flag cluster outliers. Caller saves.</summary>
    Task CloseSessionAsync(Session session, CancellationToken ct = default);

    Task<int> SendDuePromptsAsync(CancellationToken ct = default);
    Task<int> ProcessPromptTimeoutsAsync(CancellationToken ct = default);
    Task<int> AutoCloseExpiredSessionsAsync(CancellationToken ct = default);
    Task<int> PurgeExpiredLocationsAsync(CancellationToken ct = default);
}
