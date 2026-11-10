using System.Text.Json;
using SeatCharter.Api.DTOs.Responses;

namespace SeatCharter.Api.Middleware;

public class ExceptionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionLoggingMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private static readonly JsonSerializerOptions EnvelopeJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ExceptionLoggingMiddleware(RequestDelegate next, ILogger<ExceptionLoggingMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var userId = context.User.FindFirst("sub")?.Value ?? "anonymous";
            _logger.LogError(ex,
                "Unhandled exception on {Method} {Path}. User={UserId} ExceptionType={ExceptionType}",
                context.Request.Method, context.Request.Path, userId, ex.GetType().Name);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json; charset=utf-8";

                var message = _environment.IsDevelopment()
                    ? ex.Message
                    : "An unexpected error occurred.";

                var envelope = ApiResponseBase<object>.ServerError(message);
                await context.Response.WriteAsync(JsonSerializer.Serialize(envelope, EnvelopeJson));
            }
        }
    }
}
