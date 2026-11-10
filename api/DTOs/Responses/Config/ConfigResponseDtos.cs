namespace SeatCharter.Api.DTOs.Responses.Config;

public class CampusConfigDto
{
    public List<CampusZoneDto> Zones { get; set; } = [];
    /// <summary>Thresholds the app can use for its own pre-check (the server still decides).</summary>
    public double MaxCampusAllowanceM { get; set; }
    public double AnchorFlagDistanceM { get; set; }
    public double PoorAccuracyM { get; set; }
    public double RoomGeofenceRadiusM { get; set; }
}

public class CampusZoneDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<double[]> Polygon { get; set; } = [];
}
