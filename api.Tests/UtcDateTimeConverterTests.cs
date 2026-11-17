using System.Text.Json;
using SeatCharter.Api.Helpers;

namespace SeatCharter.Api.Tests;

/// <summary>Database-read timestamps (Kind=Unspecified) must still reach clients as UTC with a "Z".</summary>
public class UtcDateTimeConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new UtcDateTimeConverter() } };

    [Fact]
    public void Write_UnspecifiedKind_TreatedAsUtc() =>
        Assert.Equal("\"2026-11-15T11:56:28.9Z\"", JsonSerializer.Serialize(new DateTime(2026, 11, 15, 11, 56, 28, 900, DateTimeKind.Unspecified), Options));

    [Fact]
    public void Write_UtcKind_KeepsZ() =>
        Assert.Equal("\"2026-11-15T15:00:00Z\"", JsonSerializer.Serialize(new DateTime(2026, 11, 15, 15, 0, 0, DateTimeKind.Utc), Options));

    [Fact]
    public void Write_Nullable_UsesConverter() =>
        Assert.Equal("{\"At\":\"2026-11-15T15:00:00Z\"}", JsonSerializer.Serialize(new { At = (DateTime?)new DateTime(2026, 11, 15, 15, 0, 0) }, Options));

    [Fact]
    public void Read_OffsetTime_ConvertedToUtc()
    {
        var d = JsonSerializer.Deserialize<DateTime>("\"2026-11-15T12:00:00+01:00\"", Options);
        Assert.Equal(DateTimeKind.Utc, d.Kind);
        Assert.Equal(11, d.Hour);
    }
}
