namespace SeatCharter.Api.DTOs.Responses.Devices;

public class DeviceDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool HasPushToken { get; set; }
    public DateTime BoundAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

/// <summary>Row in the lecturer's device-request queue.</summary>
public class PendingDeviceDto
{
    public string Id { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string? MatricNo { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime BoundAt { get; set; }
    /// <summary>The device this one would replace, if the student has one.</summary>
    public string? CurrentDeviceModel { get; set; }
}
