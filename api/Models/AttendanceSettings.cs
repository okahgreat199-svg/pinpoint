namespace SeatCharter.Api.Models;

/// <summary>Business-rule thresholds. Bound from the "Attendance" config section.</summary>
public class AttendanceSettings
{
    /// <summary>BR-8: flag when further than this from the lecturer's anchor.</summary>
    public double AnchorFlagDistanceM { get; set; } = 75;
    /// <summary>BR-9: accuracy worse than this marks the location unverified.</summary>
    public double PoorAccuracyM { get; set; } = 50;
    /// <summary>BR-7: max metres that reported accuracy can widen the campus fence.</summary>
    public double MaxCampusAllowanceM { get; set; } = 100;
    /// <summary>BR-11: outlier threshold is max(OutlierMinDistanceM, OutlierMadMultiplier × MAD).</summary>
    public double OutlierMinDistanceM { get; set; } = 50;
    public double OutlierMadMultiplier { get; set; } = 3;
    /// <summary>BR-12: prompt fires at this fraction of the session; students have PromptResponseMinutes to answer.</summary>
    public double PromptAtFraction { get; set; } = 0.7;
    public int PromptResponseMinutes { get; set; } = 10;
    /// <summary>Flow 2: radius of the room geofence the app registers around the anchor after check-in.</summary>
    public double RoomGeofenceRadiusM { get; set; } = 100;
    /// <summary>A lecturer's anchor fix must be at least this accurate to start a session.</summary>
    public double MaxAnchorAccuracyM { get; set; } = 100;
    public int QrStepSeconds { get; set; } = 15;
    public int DefaultSessionMinutes { get; set; } = 60;
    /// <summary>Starting a timetabled slot this many minutes early or late doesn't ask the lecturer to confirm.</summary>
    public int ScheduleGraceMinutes { get; set; } = 15;
    /// <summary>Raw coordinates are deleted this many days after session close.</summary>
    public int LocationRetentionDays { get; set; } = 30;
    public int WorkerIntervalSeconds { get; set; } = 15;
    /// <summary>Schedule wall-clock times are interpreted in this zone.</summary>
    public string TimeZoneId { get; set; } = "Europe/London";
    /// <summary>Required to self-register as a lecturer. Empty = lecturer self-registration disabled.</summary>
    public string LecturerInviteCode { get; set; } = string.Empty;
}

/// <summary>Request limits. Bound from the "RateLimit" config section.</summary>
public class RateLimitSettings
{
    /// <summary>
    /// Login/register attempts per IP per minute. A lecture hall on campus Wi-Fi shares one public IP, so this
    /// must cover a whole class signing in at once; guessing one account's password is stopped separately by
    /// Identity lockout (5 failures → 15 minutes).
    /// </summary>
    public int AuthPerIpPerMinute { get; set; } = 300;
    /// <summary>Check-in attempts per student per minute.</summary>
    public int CheckInPerStudentPerMinute { get; set; } = 10;
}

/// <summary>Startup migration + demo seed. Bound from the "Seed" config section.</summary>
public class SeedSettings
{
    public bool MigrateOnStartup { get; set; } = false;
    public bool Enabled { get; set; } = false;
    /// <summary>"Demo" (default) or "Dundee". See DbSeeder.</summary>
    public string Profile { get; set; } = "Demo";
    /// <summary>Password for every seeded account. Required when Enabled.</summary>
    public string DefaultPassword { get; set; } = string.Empty;
    /// <summary>The admin account's password; falls back to DefaultPassword when empty.</summary>
    public string AdminPassword { get; set; } = string.Empty;
}

/// <summary>FCM. Push is a logged no-op when neither value is set.</summary>
public class FirebaseSettings
{
    /// <summary>Path to a service-account JSON file.</summary>
    public string CredentialsPath { get; set; } = string.Empty;
    /// <summary>Service-account JSON inline (handy for App Service settings).</summary>
    public string CredentialsJson { get; set; } = string.Empty;
}

/// <summary>Deep-link QR codes + Android App Links. Bound from the "DeepLink" config section.</summary>
public class DeepLinkSettings
{
    /// <summary>Public HTTPS origin of this API, e.g. https://seatcharter.azurewebsites.net. Empty = QR carries the bare token.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
    /// <summary>Android applicationId, for /.well-known/assetlinks.json.</summary>
    public string AndroidPackageName { get; set; } = string.Empty;
    /// <summary>SHA-256 signing-cert fingerprints ("AB:CD:..."), debug and release.</summary>
    public string[] AndroidCertFingerprints { get; set; } = [];
    /// <summary>Where the landing page sends people who don't have the app (APK link or store page).</summary>
    public string AppDownloadUrl { get; set; } = string.Empty;
}

/// <summary>Firebase App Check (Play Integrity). Bound from the "AppCheck" config section.</summary>
public class AppCheckSettings
{
    /// <summary>Off by default so Swagger/curl work in dev. Turn on for the demo host.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>Firebase project number (Project settings → General), not the project id.</summary>
    public string ProjectNumber { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    /// <summary>Allowed Firebase app ids ("1:123:android:abc"). Empty = any app in the project.</summary>
    public string[] AppIds { get; set; } = [];
}
