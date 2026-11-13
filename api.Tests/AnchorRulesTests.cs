using SeatCharter.Api.Domain;

namespace SeatCharter.Api.Tests;

/// <summary>The lecturer's anchor must be a real, on-campus fix (and in the room when the slot has one).</summary>
public class AnchorRulesTests
{
    private static readonly IReadOnlyList<IReadOnlyList<GeoPoint>> Campus =
        [[new(56.4612, -2.9880), new(56.4612, -2.9725), new(56.4560, -2.9725), new(56.4545, -2.9800), new(56.4555, -2.9880)]];
    private static readonly GeoPoint InRoom = new(56.4580, -2.9800);

    private static string? Run(GeoPoint anchor, double accuracyM = 10, bool mocked = false, GeoPoint? room = null) =>
        AnchorRules.Validate(anchor, accuracyM, mocked, Campus, room, 100, "Dalhousie 2G11", maxAccuracyM: 100, maxCampusAllowanceM: 100);

    [Fact]
    public void Validate_OnCampus_Ok() => Assert.Null(Run(InRoom));

    [Fact]
    public void Validate_OffCampus_Refused() => Assert.Contains("on campus", Run(new GeoPoint(56.4689, -2.9874)));

    [Fact]
    public void Validate_Mocked_Refused() => Assert.Contains("simulated", Run(InRoom, mocked: true));

    [Fact]
    public void Validate_PoorAccuracy_Refused() => Assert.Contains("accurate", Run(InRoom, accuracyM: 250));

    [Fact]
    public void Validate_FarFromScheduledRoom_Refused() =>
        Assert.Contains("Dalhousie 2G11", Run(new GeoPoint(56.4600, -2.9750), room: InRoom));

    [Fact]
    public void Validate_InScheduledRoom_Ok() => Assert.Null(Run(new GeoPoint(56.45805, -2.98005), room: InRoom));

    [Fact]
    public void Validate_NoCampusConfigured_OnlyOtherRulesApply() =>
        Assert.Null(AnchorRules.Validate(new GeoPoint(51.5, -0.12), 10, false, [], null, 100, null, 100, 100));
}
