using SteamWatch.Core.Tracking;

namespace SteamWatch.Tests;

[TestClass]
public sealed class PlaytimeRecordBookTests
{
    [TestMethod]
    public void WeeklyQueries_AfterAdditionAcrossYearBoundary_IncludeOnlyCurrentWeek()
    {
        var today = new DateOnly(2027, 1, 1);
        var book = new PlaytimeRecordBook([
            new(today.AddDays(-5), new Dictionary<int, int> { [730] = 999 }),
            new(today.AddDays(-4), new Dictionary<int, int> { [730] = 10, [570] = 20 }),
            new(today.AddDays(2), new Dictionary<int, int> { [730] = 30 }),
            new(today.AddDays(3), new Dictionary<int, int> { [730] = 999 })]);
        Assert.AreEqual(40, book.GetWeekGameMinutes(today, 730));
        Assert.AreEqual(60, book.GetWeekTotalMinutes(today));
        book.Add(today, new PlaytimeIncrement(570, "Dota", 5, false));
        Assert.AreEqual(25, book.GetWeekGameMinutes(today, 570));
        Assert.AreEqual(65, book.GetWeekTotalMinutes(today));
        Assert.AreEqual(0, book.GetWeekGameMinutes(today, 999));
    }

    [TestMethod]
    public void WeeklyQueries_Overflow_RemainsAnError()
    {
        var today = new DateOnly(2026, 10, 14);
        var book = new PlaytimeRecordBook([
            new(today, new Dictionary<int, int> { [730] = int.MaxValue }),
            new(today.AddDays(1), new Dictionary<int, int> { [730] = 1 })]);
        Assert.ThrowsExactly<OverflowException>(() => book.GetWeekGameMinutes(today, 730));
        Assert.ThrowsExactly<OverflowException>(() => book.GetWeekTotalMinutes(today));
    }

    [TestMethod]
    public void Add_NewGameForDate_CreatesRecord()
    {
        var book = new PlaytimeRecordBook();

        book.Add(new DateOnly(2026, 4, 30), new PlaytimeIncrement(730, "Counter-Strike 2", 5, false));

        Assert.AreEqual(5, book.GetGameMinutes(new DateOnly(2026, 4, 30), 730));
        Assert.AreEqual(5, book.GetTotalMinutes(new DateOnly(2026, 4, 30)));
    }

    [TestMethod]
    public void Add_ExistingGameForDate_MergesMinutes()
    {
        var book = new PlaytimeRecordBook([
            new DailyPlaytimeRecord(new DateOnly(2026, 4, 30), new Dictionary<int, int> { [730] = 10 })
        ]);

        book.Add(new DateOnly(2026, 4, 30), new PlaytimeIncrement(730, "Counter-Strike 2", 7, false));

        Assert.AreEqual(17, book.GetGameMinutes(new DateOnly(2026, 4, 30), 730));
    }

    [TestMethod]
    public void GetWeekGameMinutes_UsesMondayThroughSunday()
    {
        var book = new PlaytimeRecordBook([
            new DailyPlaytimeRecord(new DateOnly(2026, 4, 26), new Dictionary<int, int> { [730] = 999 }),
            new DailyPlaytimeRecord(new DateOnly(2026, 4, 27), new Dictionary<int, int> { [730] = 20 }),
            new DailyPlaytimeRecord(new DateOnly(2026, 4, 30), new Dictionary<int, int> { [730] = 30 }),
            new DailyPlaytimeRecord(new DateOnly(2026, 5, 3), new Dictionary<int, int> { [730] = 40 }),
            new DailyPlaytimeRecord(new DateOnly(2026, 5, 4), new Dictionary<int, int> { [730] = 999 })
        ]);

        var minutes = book.GetWeekGameMinutes(new DateOnly(2026, 4, 30), 730);

        Assert.AreEqual(90, minutes);
    }

    [TestMethod]
    public void Add_ZeroMinuteIncrement_IgnoresIncrement()
    {
        var book = new PlaytimeRecordBook();

        book.Add(new DateOnly(2026, 4, 30), new PlaytimeIncrement(730, "Counter-Strike 2", 0, false));

        Assert.IsEmpty(book.Records);
    }
}
