using System.ComponentModel.DataAnnotations;

namespace SeatCharter.Api.DTOs.Requests.Devices;

public class RegisterDeviceRequest
{
    /// <summary>Base64 ECDSA P-256 public key (SPKI DER, or raw 65-byte uncompressed point).</summary>
    [Required, StringLength(512)]
    public string PublicKey { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Platform { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Model { get; set; } = string.Empty;

    /// <summary>OS install identifier (Android ID from device_info_plus). Strengthens BR-14.</summary>
    [StringLength(200)]
    public string? HardwareId { get; set; }

    [StringLength(4096)]
    public string? PushToken { get; set; }
}

public class UpdatePushTokenRequest
{
    [Required, StringLength(4096)]
    public string PushToken { get; set; } = string.Empty;
}
