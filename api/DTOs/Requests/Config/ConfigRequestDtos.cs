using System.ComponentModel.DataAnnotations;

namespace SeatCharter.Api.DTOs.Requests.Config;

public class UpdateCampusRequest
{
    [Required, MinLength(1)]
    public List<CampusZoneInput> Zones { get; set; } = [];
}

public class CampusZoneInput
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>[[lat, lng], ...] in order, at least 3 points. geojson.io exports [lng, lat]: swap before sending.</summary>
    [Required, MinLength(3)]
    public List<double[]> Polygon { get; set; } = [];
}
