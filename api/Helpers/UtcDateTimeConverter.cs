using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeatCharter.Api.Helpers;

/// <summary>
/// Every DateTime the API stores is UTC, but values read back from "timestamp without time zone" columns arrive
/// with Kind=Unspecified and would serialise without a "Z", so clients would read them as local time.
/// This writes all DateTimes as UTC ISO-8601 with the "Z" and reads incoming ones as UTC.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTime.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture));
    }
}
