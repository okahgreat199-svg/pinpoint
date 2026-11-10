using SeatCharter.Api.Helpers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.BusinessLogic.Jobs;
using SeatCharter.Api.BusinessLogic.Services;
using SeatCharter.Api.Data;
using SeatCharter.Api.DTOs;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.Helpers.Jwt;
using SeatCharter.Api.Middleware;
using SeatCharter.Api.Models;

// ── .env loader (BEFORE CreateBuilder). OS env vars win → prod safe ──────
{
    var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (File.Exists(envPath))
        DotNetEnv.Env.Load(envPath, new DotNetEnv.LoadOptions(
            setEnvVars: true, clobberExistingVars: false, onlyExactPath: true));
}

// Npgsql: accept DateTime Kind=Unspecified for timestamptz (treated as UTC)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// ── Serilog bootstrap ─────────────────────────────────────────────────────
var logDirectory = Environment.GetEnvironmentVariable("LOG_DIRECTORY") is { Length: > 0 } dir
    ? dir
    : Path.Combine(Directory.GetCurrentDirectory(), "logs");
Directory.CreateDirectory(logDirectory);
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "SeatCharter.Api")
    .WriteTo.Console()
    .WriteTo.File(new CompactJsonFormatter(), Path.Combine(logDirectory, "api-.log"),
        rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30, shared: true)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ── Sentry (disabled when SENTRY_DSN unset) ───────────────────────────────
builder.WebHost.UseSentry(o =>
{
    o.Dsn = Environment.GetEnvironmentVariable("SENTRY_DSN") ?? string.Empty;
    o.Environment = builder.Environment.EnvironmentName;
    o.SendDefaultPii = false;
    o.TracesSampleRate = 0.0;
});

// ── Settings (POCO singletons) ────────────────────────────────────────────
var attendanceSettings = builder.Configuration.GetSection("Attendance").Get<AttendanceSettings>() ?? new AttendanceSettings();
builder.Services.AddSingleton(attendanceSettings);
builder.Services.AddSingleton(builder.Configuration.GetSection("Seed").Get<SeedSettings>() ?? new SeedSettings());
var rateLimits = builder.Configuration.GetSection("RateLimit").Get<RateLimitSettings>() ?? new RateLimitSettings();
builder.Services.AddSingleton(builder.Configuration.GetSection("Firebase").Get<FirebaseSettings>() ?? new FirebaseSettings());
builder.Services.AddSingleton(builder.Configuration.GetSection("DeepLink").Get<DeepLinkSettings>() ?? new DeepLinkSettings());
var appCheckSettings = builder.Configuration.GetSection("AppCheck").Get<AppCheckSettings>() ?? new AppCheckSettings();
if (appCheckSettings.Enabled && string.IsNullOrWhiteSpace(appCheckSettings.ProjectNumber))
    throw new InvalidOperationException("AppCheck:Enabled is true but AppCheck:ProjectNumber is empty.");
builder.Services.AddSingleton(appCheckSettings);

// ── Database ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<UserCacheInvalidationInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddInterceptors(sp.GetRequiredService<UserCacheInvalidationInterceptor>()));

// ── Identity ──────────────────────────────────────────────────────────────
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── JWT Authentication ────────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
if (jwtSettings.Key.Length < 32)
    throw new InvalidOperationException("JwtSettings:Key must be at least 32 characters. Set JwtSettings__Key in api/.env.");
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddJwtAuthenticationConfiguration(jwtSettings);   // MUST come after AddIdentity (overrides its cookie defaults)

// ── HTTP Context / Token ──────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserInfoToken>();
builder.Services.AddSingleton(TimeProvider.System);

// ── Caching ───────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IUserCacheService, UserCacheService>();

// ── Auth Services ─────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpClient<IAppCheckVerifier, FirebaseAppCheckVerifier>(c => c.Timeout = TimeSpan.FromSeconds(5));

// ── Feature Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<ICheckInService, CheckInService>();
builder.Services.AddScoped<ICampusService, CampusService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// ── Jobs / Push ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<IPushNotificationService, FcmPushNotificationService>();
builder.Services.AddScoped<IAttendanceJobService, AttendanceJobService>();
builder.Services.AddHostedService<SessionLifecycleWorker>();

// ── Controllers ───────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        // Wrap model-validation 400s in the standard envelope (no ProblemDetails)
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var errors = ctx.ModelState
                .Where(kv => kv.Value is { Errors.Count: > 0 })
                .SelectMany(kv => kv.Value!.Errors.Select(e =>
                    string.IsNullOrEmpty(kv.Key) ? e.ErrorMessage : $"{kv.Key}: {e.ErrorMessage}"))
                .ToList();
            return new BadRequestObjectResult(ApiResponseBase<object>.BadRequest(
                errors.Count > 0 ? string.Join("; ", errors) : "Request body failed validation."));
        };
    });
builder.Services.AddOpenApi();

// ── CORS (config-driven; never AllowAnyOrigin in prod) ────────────────────
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()
     .WithExposedHeaders("X-Correlation-Id")));

// ── Health checks ─────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "postgres", tags: ["db"]);

// ── Rate limiter ──────────────────────────────────────────────────────────
// "auth-tight": register/login per IP, sized for a whole lecture hall behind one campus NAT address
// (per-account brute force is handled by Identity lockout). "checkin": attempts per student.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(
            new ApiResponseBase<object>(null, 429, "Too many requests. Please wait a minute and try again.", false),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), ct);
    };
    options.AddPolicy("auth-tight", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimits.AuthPerIpPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
            }));
    options.AddPolicy("checkin", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.User.FindFirst("sub")?.Value ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimits.CheckInPerStudentPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
            }));
});

// Trust X-Forwarded-For/Proto from the reverse proxy (App Service / Caddy / nginx). Any proxy is trusted, so in
// production Kestrel must listen on localhost only (ASPNETCORE_URLS=http://127.0.0.1:5080); otherwise a client
// could send its own X-Forwarded-For and dodge the per-IP limit.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownProxies.Clear();
    o.KnownIPNetworks.Clear();
});

// ── Swagger ───────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Pinpoint API", Version = "v1" });
    c.CustomSchemaIds(type => type.FullName?.Replace('+', '.'));   // avoid same-name DTO collisions
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Enter 'Bearer' [space] then your token.",
        Name = "Authorization", In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey, Scheme = "Bearer", BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = []
    });
});

var app = builder.Build();

// ── Startup migration + seeding (idempotent) ──────────────────────────────
await DbSeeder.RunAsync(app.Services);

// Swagger stays on for the demo host via Swagger:Enabled (BRD §4); off by default outside Development.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── Pipeline: ORDER MATTERS ───────────────────────────────────────────────
app.UseForwardedHeaders();                       // 1. real client IP before anyone reads it
app.UseHttpMetrics();                            // 2. Prometheus
app.UseMiddleware<LoggingContextMiddleware>();   // 3. CorrelationId/UserId into LogContext
app.UseSerilogRequestLogging();                  // 4. one summary line per request
app.UseMiddleware<ExceptionLoggingMiddleware>(); // 5. catch-all → 500 envelope
app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseRateLimiter();                            // after auth so the "checkin" policy can partition by user
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapMetrics("/metrics");                      // gate at ingress

app.Run();

public partial class Program;   // for WebApplicationFactory in tests
