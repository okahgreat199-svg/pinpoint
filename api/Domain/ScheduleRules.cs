namespace SeatCharter.Api.Domain;

/// <summary>
/// Is a timetabled slot on right now? Starting outside it isn't wrong (catch-up classes, running early),
/// so this only produces a warning the lecturer confirms past; it never blocks.
/// </summary>
public static class ScheduleRules
{
    /// <summary>Returns a user-facing warning when <paramref name="nowLocal"/> is outside the slot plus grace, or null when on time.</summary>
    public static string? OffScheduleWarning(DayOfWeek day, TimeOnly start, TimeOnly end, DateTime nowLocal, string courseCode, int graceMinutes)
    {
        var slot = $"{day.ToString()[..3]} {start:HH\\:mm}–{end:HH\\:mm}";
        if (nowLocal.DayOfWeek != day)
            return $"{courseCode} is timetabled for {slot}, not today. Start the session anyway?";

        // Minutes of the day, so the grace window can't wrap around midnight.
        var now = nowLocal.Hour * 60 + nowLocal.Minute;
        var from = start.Hour * 60 + start.Minute - graceMinutes;
        var to = end.Hour * 60 + end.Minute + graceMinutes;
        if (now < from) return $"{courseCode} isn't until {start:HH\\:mm} ({slot}). Start the session anyway?";
        if (now > to) return $"{courseCode} finished at {end:HH\\:mm} ({slot}). Start the session anyway?";
        return null;
    }
}
