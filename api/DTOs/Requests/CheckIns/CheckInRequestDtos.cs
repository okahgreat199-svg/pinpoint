using System.ComponentModel.DataAnnotations;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.DTOs.Requests.CheckIns;

/// <summary>
/// Signed check-in. Signature = ECDSA P-256 over
/// "token|lat(6dp)|lng(6dp)|accuracyM(1dp)|isMocked|clientTime|deviceId" (see Domain/DeviceSignature).
/// </summary>
public class CheckInRequest
{
    /// <summary>What was scanned (deep link or bare token), or the 6-digit manual code. Signed exactly as sent.</summary>
    [Required, StringLength(256)]
    public string Token { get; set; } = string.Empty;

    [Required, Range(-90, 90)]
    public double Lat { get; set; }

    [Required, Range(-180, 180)]
    public double Lng { get; set; }

    [Required, Range(0, 100000)]
    public double AccuracyM { get; set; }

    public bool IsMocked { get; set; }

    /// <summary>ISO-8601 string, signed verbatim.</summary>
    [Required, StringLength(64)]
    public string ClientTime { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string DeviceId { get; set; } = string.Empty;

    [Required, StringLength(512)]
    public string Signature { get; set; } = string.Empty;
}

public class PresenceReportRequest
{
    [Required]
    public PresenceCheckKind Kind { get; set; }

    [Range(-90, 90)]
    public double? Lat { get; set; }

    [Range(-180, 180)]
    public double? Lng { get; set; }

    [Range(0, 100000)]
    public double? AccuracyM { get; set; }
}

public class ReviewCheckInRequest
{
    [Required]
    public ReviewDecision Decision { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}
