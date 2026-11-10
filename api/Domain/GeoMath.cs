namespace SeatCharter.Api.Domain;

public readonly record struct GeoPoint(double Lat, double Lng);

/// <summary>Plain-C# geo maths (no PostGIS): haversine, point-in-polygon, edge distance, median/MAD outliers.</summary>
public static class GeoMath
{
    private const double EarthRadiusM = 6_371_008.8;   // IUGG mean radius

    public static double HaversineM(GeoPoint a, GeoPoint b)
    {
        var dLat = ToRad(b.Lat - a.Lat);
        var dLng = ToRad(b.Lng - a.Lng);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(a.Lat)) * Math.Cos(ToRad(b.Lat)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * EarthRadiusM * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>Ray casting. Polygon vertices in order; closing vertex optional.</summary>
    public static bool IsInsidePolygon(GeoPoint p, IReadOnlyList<GeoPoint> polygon)
    {
        if (polygon.Count < 3) return false;
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Lat > p.Lat) != (b.Lat > p.Lat)
                && p.Lng < (b.Lng - a.Lng) * (p.Lat - a.Lat) / (b.Lat - a.Lat) + a.Lng)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>
    /// Shortest distance from p to the polygon boundary, in metres. Uses a local equirectangular projection
    /// centred on p, which is accurate to well under a metre at campus scale.
    /// </summary>
    public static double DistanceToBoundaryM(GeoPoint p, IReadOnlyList<GeoPoint> polygon)
    {
        if (polygon.Count == 0) return double.PositiveInfinity;
        var cosLat = Math.Cos(ToRad(p.Lat));
        (double X, double Y) Project(GeoPoint q) => (ToRad(q.Lng - p.Lng) * cosLat * EarthRadiusM, ToRad(q.Lat - p.Lat) * EarthRadiusM);

        var best = double.PositiveInfinity;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var (ax, ay) = Project(polygon[j]);
            var (bx, by) = Project(polygon[i]);
            double dx = bx - ax, dy = by - ay;
            var lenSq = dx * dx + dy * dy;
            var t = lenSq == 0 ? 0 : Math.Clamp(-(ax * dx + ay * dy) / lenSq, 0, 1);
            double cx = ax + t * dx, cy = ay + t * dy;
            best = Math.Min(best, Math.Sqrt(cx * cx + cy * cy));
        }
        return best;
    }

    /// <summary>BR-7: inside, or outside by no more than the (capped) reported accuracy.</summary>
    public static bool IsInsideWithAllowance(GeoPoint p, IReadOnlyList<GeoPoint> polygon, double allowanceM)
        => IsInsidePolygon(p, polygon) || DistanceToBoundaryM(p, polygon) <= allowanceM;

    public static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0) throw new ArgumentException("Median of an empty sequence.", nameof(values));
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    /// <summary>
    /// BR-11: a point is an outlier when its distance from the component-wise median point exceeds
    /// max(minThresholdM, madMultiplier × MAD), where MAD is the median of those distances.
    /// Needs at least 3 points to say anything.
    /// </summary>
    public static IReadOnlySet<TKey> FindClusterOutliers<TKey>(
        IReadOnlyCollection<(TKey Key, GeoPoint Point)> points, double minThresholdM, double madMultiplier)
        where TKey : notnull
    {
        if (points.Count < 3) return new HashSet<TKey>();

        var centre = new GeoPoint(Median(points.Select(p => p.Point.Lat)), Median(points.Select(p => p.Point.Lng)));
        var distances = points.Select(p => (p.Key, Distance: HaversineM(centre, p.Point))).ToList();
        var mad = Median(distances.Select(d => d.Distance));
        var threshold = Math.Max(minThresholdM, madMultiplier * mad);

        return distances.Where(d => d.Distance > threshold).Select(d => d.Key).ToHashSet();
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;
}
