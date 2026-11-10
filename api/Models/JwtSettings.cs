namespace SeatCharter.Api.Models;

public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "SeatCharter.Api";
    public string Audience { get; set; } = "SeatCharter.Api.Client";
    public int MinutesToExpiration { get; set; } = 15;
    public int RefreshTokenExpiryDays { get; set; } = 7;
    public int MfaPartialTokenMinutes { get; set; } = 5;
}
