using SeatCharter.Api.Domain;

namespace SeatCharter.Api.Tests;

public class ScheduleRulesTests
{
    // Tue 10:00–11:00. 17 Nov 2026 is a Tuesday.
    private static string? At(int day, int hour, int minute) => ScheduleRules.OffScheduleWarning(
        DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(11, 0), new DateTime(2026, 11, day, hour, minute, 0), "CS32005", 15);

    [Theory]
    [InlineData(9, 45)]   // early, within grace
    [InlineData(10, 30)]
    [InlineData(11, 15)]  // late, within grace
    public void OnTime_NoWarning(int hour, int minute) => Assert.Null(At(17, hour, minute));

    [Fact]
    public void TooEarly_Warns() => Assert.Contains("isn't until 10:00", At(17, 9, 44));

    [Fact]
    public void TooLate_Warns() => Assert.Contains("finished at 11:00", At(17, 11, 16));

    [Fact]
    public void WrongDay_Warns() => Assert.Contains("Tue 10:00–11:00, not today", At(18, 10, 30));
}
