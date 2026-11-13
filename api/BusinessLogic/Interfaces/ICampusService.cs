using SeatCharter.Api.Domain;
using SeatCharter.Api.DTOs.Requests.Config;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Config;

namespace SeatCharter.Api.BusinessLogic.Interfaces;

public interface ICampusService
{
    Task<ApiResponseBase<CampusConfigDto>> GetCampusAsync();
    Task<ApiResponseBase<CampusConfigDto>> UpdateCampusAsync(UpdateCampusRequest request);
    /// <summary>Parsed polygons for the rule engine (cached). Empty = no geofence configured.</summary>
    Task<IReadOnlyList<IReadOnlyList<GeoPoint>>> GetPolygonsAsync();
}
