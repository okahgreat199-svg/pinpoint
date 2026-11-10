using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.Models;

namespace SeatCharter.Api.BusinessLogic.Jobs;

/// <summary>
/// Ticks every AttendanceSettings.WorkerIntervalSeconds: send due 70% prompts, flag non-responders,
/// auto-close sessions at EndsAt (+ BR-11), and purge raw coordinates past retention.
/// </summary>
public class SessionLifecycleWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly AttendanceSettings _settings;
    private readonly ILogger<SessionLifecycleWorker> _logger;

    public SessionLifecycleWorker(IServiceScopeFactory scopes, AttendanceSettings settings, ILogger<SessionLifecycleWorker> logger)
    {
        _scopes = scopes;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _settings.WorkerIntervalSeconds)));
        do
        {
            await RunStepAsync("prompts", (j, ct) => j.SendDuePromptsAsync(ct), stoppingToken);
            await RunStepAsync("prompt-timeouts", (j, ct) => j.ProcessPromptTimeoutsAsync(ct), stoppingToken);
            await RunStepAsync("auto-close", (j, ct) => j.AutoCloseExpiredSessionsAsync(ct), stoppingToken);
            await RunStepAsync("location-purge", (j, ct) => j.PurgeExpiredLocationsAsync(ct), stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // Each step gets its own scope/DbContext so one failure can't poison the others' change tracker.
    private async Task RunStepAsync(string name, Func<IAttendanceJobService, CancellationToken, Task<int>> step, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var jobs = scope.ServiceProvider.GetRequiredService<IAttendanceJobService>();
            await step(jobs, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session lifecycle step {Step} failed", name);
        }
    }
}
