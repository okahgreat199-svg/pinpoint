using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.Data;
using SeatCharter.Api.Domain;
using SeatCharter.Api.DTOs.Requests.Config;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Config;
using SeatCharter.Api.Models;

namespace SeatCharter.Api.BusinessLogic.Services;

public class CampusService : ICampusService
{
    private const string PolygonsCacheKey = "campus:polygons";

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly AttendanceSettings _settings;
    private readonly ILogger<CampusService> _logger;

    public CampusService(ApplicationDbContext db, IMemoryCache cache, AttendanceSettings settings, ILogger<CampusService> logger)
    {
        _db = db;
        _cache = cache;
        _settings = settings;
        _logger = logger;
    }

    private ApiResponseBase<T> Fail500<T>(Exception ex, [CallerMemberName] string method = "")
    {
        _logger.LogError(ex, "{Method} failed", method);
        return ApiResponseBase<T>.ServerError("An error occurred. Please try again.");
    }

    public async Task<ApiResponseBase<CampusConfigDto>> GetCampusAsync()
    {
        try
        {
            return ApiResponseBase<CampusConfigDto>.Ok(await BuildDtoAsync(), "Campus configuration retrieved.");
        }
        catch (Exception ex) { return Fail500<CampusConfigDto>(ex); }
    }

    public async Task<ApiResponseBase<CampusConfigDto>> UpdateCampusAsync(UpdateCampusRequest request)
    {
        try
        {
            foreach (var zone in request.Zones)
            {
                if (zone.Polygon.Count < 3)
                    return ApiResponseBase<CampusConfigDto>.BadRequest($"Zone \"{zone.Name}\" needs at least 3 points.");
                if (zone.Polygon.Any(p => p.Length != 2 || p[0] is < -90 or > 90 || p[1] is < -180 or > 180))
                    return ApiResponseBase<CampusConfigDto>.BadRequest($"Zone \"{zone.Name}\" has a point that isn't a valid [lat, lng] pair.");
            }

            await using var tx = await _db.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            foreach (var old in await _db.CampusZones.ToListAsync()) old.DeletedAt = now;
            foreach (var zone in request.Zones)
            {
                _db.CampusZones.Add(new CampusZone
                {
                    Name = zone.Name.Trim(),
                    PolygonJson = JsonSerializer.Serialize(zone.Polygon)
                });
            }
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            _cache.Remove(PolygonsCacheKey);

            return ApiResponseBase<CampusConfigDto>.Ok(await BuildDtoAsync(), "Campus geofence updated.");
        }
        catch (Exception ex) { return Fail500<CampusConfigDto>(ex); }
    }

    public async Task<IReadOnlyList<IReadOnlyList<GeoPoint>>> GetPolygonsAsync()
    {
        if (_cache.TryGetValue(PolygonsCacheKey, out IReadOnlyList<IReadOnlyList<GeoPoint>>? cached) && cached is not null)
            return cached;

        var zones = await _db.CampusZones.AsNoTracking().Select(z => z.PolygonJson).ToListAsync();
        IReadOnlyList<IReadOnlyList<GeoPoint>> polygons = zones
            .Select(json => (IReadOnlyList<GeoPoint>)Parse(json).Select(p => new GeoPoint(p[0], p[1])).ToList())
            .Where(p => p.Count >= 3)
            .ToList();

        if (polygons.Count == 0) _logger.LogWarning("No campus zones configured; BR-7 campus check is skipped");
        _cache.Set(PolygonsCacheKey, polygons, TimeSpan.FromMinutes(10));
        return polygons;
    }

    private async Task<CampusConfigDto> BuildDtoAsync()
    {
        var zones = await _db.CampusZones.AsNoTracking().OrderBy(z => z.Name).ToListAsync();
        return new CampusConfigDto
        {
            Zones = zones.Select(z => new CampusZoneDto { Id = z.Id, Name = z.Name, Polygon = Parse(z.PolygonJson) }).ToList(),
            MaxCampusAllowanceM = _settings.MaxCampusAllowanceM,
            AnchorFlagDistanceM = _settings.AnchorFlagDistanceM,
            PoorAccuracyM = _settings.PoorAccuracyM,
            RoomGeofenceRadiusM = _settings.RoomGeofenceRadiusM
        };
    }

    private static List<double[]> Parse(string json) => JsonSerializer.Deserialize<List<double[]>>(json) ?? [];
}
