using Serilog.Context;

namespace SeatCharter.Api.Middleware;

/// <summary>Reads or creates X-Correlation-Id, echoes it back, and pushes CorrelationId/UserId into the log context.</summary>
public class LoggingContextMiddleware
{
    private const string Header = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public LoggingContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[Header].ToString();
        var correlationId = !string.IsNullOrWhiteSpace(incoming) && incoming.Length <= 64 ? incoming : Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("UserId", context.User.FindFirst("sub")?.Value))
        {
            await _next(context);
        }
    }
}
