namespace SteamWatch.Core.Tracking;

public static class WeekCalculator
{
    public static DateOnly GetWeekStart(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    public static DateOnly GetWeekEnd(DateOnly date)
    {
        return GetWeekStart(date).AddDays(6);
    }
}
