using SteamWatch.Core.Tracking;

namespace SteamWatch.Tests;

[TestClass]
public sealed class PlaytimeRecordBookTests
{
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
