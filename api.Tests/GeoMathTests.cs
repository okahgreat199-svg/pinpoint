using SeatCharter.Api.Domain;

namespace SeatCharter.Api.Tests;

public class GeoMathTests
{
    private static readonly GeoPoint[] Square =
    [
        new(56.4600, -2.9850), new(56.4600, -2.9750), new(56.4550, -2.9750), new(56.4550, -2.9850)
    ];

    [Fact]
    public void Haversine_LondonToParis_About344Km()
    {
        var d = GeoMath.HaversineM(new(51.5074, -0.1278), new(48.8566, 2.3522));
        Assert.InRange(d, 343_000, 345_000);
    }

    [Fact]
    public void Haversine_SamePoint_IsZero() => Assert.Equal(0, GeoMath.HaversineM(new(56.458, -2.98), new(56.458, -2.98)));

    [Fact]
    public void Haversine_OneThousandthDegreeLatitude_About111m()
        => Assert.InRange(GeoMath.HaversineM(new(56.458, -2.98), new(56.459, -2.98)), 110.5, 111.8);

    [Fact]
    public void PointInPolygon_Centre_IsInside() => Assert.True(GeoMath.IsInsidePolygon(new(56.4575, -2.9800), Square));

    [Fact]
    public void PointInPolygon_Outside_IsOutside() => Assert.False(GeoMath.IsInsidePolygon(new(56.4700, -2.9800), Square));

    [Fact]
    public void PointInPolygon_ConcaveNotch_IsOutside()
    {
        // U-shape: the notch between the arms is outside.
        GeoPoint[] u = [new(0, 0), new(0, 3), new(3, 3), new(3, 2), new(1, 2), new(1, 1), new(3, 1), new(3, 0)];
        Assert.False(GeoMath.IsInsidePolygon(new(2, 1.5), u));
        Assert.True(GeoMath.IsInsidePolygon(new(2, 0.5), u));
    }

    [Fact]
    public void PointInPolygon_DegeneratePolygon_IsOutside()
        => Assert.False(GeoMath.IsInsidePolygon(new(0, 0), [new(0, 0), new(1, 1)]));

    [Fact]
    public void DistanceToBoundary_JustNorthOfEdge_MatchesHaversine()
    {
        var p = new GeoPoint(56.4605, -2.9800);   // ~55 m north of the top edge
        var expected = GeoMath.HaversineM(p, new(56.4600, -2.9800));
        Assert.InRange(GeoMath.DistanceToBoundaryM(p, Square), expected - 1, expected + 1);
    }

    [Fact]
    public void InsideWithAllowance_OutsideByLessThanAccuracy_Passes()
    {
        var p = new GeoPoint(56.4605, -2.9800);   // ~55 m outside
        Assert.False(GeoMath.IsInsideWithAllowance(p, Square, 30));
        Assert.True(GeoMath.IsInsideWithAllowance(p, Square, 60));
    }

    [Theory]
    [InlineData(new[] { 3.0, 1.0, 2.0 }, 2.0)]
    [InlineData(new[] { 4.0, 1.0, 3.0, 2.0 }, 2.5)]
    [InlineData(new[] { 7.0 }, 7.0)]
    public void Median_OddEvenSingle(double[] values, double expected) => Assert.Equal(expected, GeoMath.Median(values));

    [Fact]
    public void ClusterOutliers_OnePointFarAway_IsFlagged()
    {
        var pts = new List<(string, GeoPoint)>
        {
            ("a", new(56.45800, -2.98000)), ("b", new(56.45805, -2.98005)), ("c", new(56.45798, -2.97995)),
            ("d", new(56.45802, -2.98003)), ("far", new(56.46000, -2.98000))   // ~220 m north
        };
        var outliers = GeoMath.FindClusterOutliers(pts, 50, 3);
        Assert.Equal(["far"], outliers);
    }

    [Fact]
    public void ClusterOutliers_TightCluster_NoneFlagged_BecauseOfFiftyMetreFloor()
    {
        var pts = new List<(string, GeoPoint)>
        {
            ("a", new(56.45800, -2.98000)), ("b", new(56.45830, -2.98000)), ("c", new(56.45770, -2.98000))  // ±33 m
        };
        Assert.Empty(GeoMath.FindClusterOutliers(pts, 50, 3));
    }

    [Fact]
    public void ClusterOutliers_FewerThanThreePoints_None()
        => Assert.Empty(GeoMath.FindClusterOutliers(new List<(string, GeoPoint)> { ("a", new(0, 0)), ("b", new(1, 1)) }, 50, 3));
}
