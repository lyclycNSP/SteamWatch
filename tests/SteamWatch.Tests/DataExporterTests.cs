using SteamWatch.Core.Export;
using SteamWatch.Core.Limits;
using SteamWatch.Infrastructure.Export;

namespace SteamWatch.Tests;

[TestClass]
public sealed class DataExporterTests
{
    [TestMethod]
    public void GetWeeklySummaries_GroupsByMondayWeekAndGame()
    {
        var snapshot = CreateSnapshot();

        var summaries = snapshot.GetWeeklySummaries();

        Assert.HasCount(2, summaries);
        Assert.AreEqual(new DateOnly(2026, 4, 27), summaries[0].WeekStart);
        Assert.AreEqual(new DateOnly(2026, 5, 3), summaries[0].WeekEnd);
        Assert.AreEqual(730, summaries[0].AppId);
        Assert.AreEqual(150, summaries[0].Minutes);
    }

    [TestMethod]
    public void CreatePlaytimeCsv_WritesExpectedChineseHeadersAndRows()
    {
        var csv = new DataExporter().CreatePlaytimeCsv(CreateSnapshot());

        StringAssert.Contains(csv, "日期,周起始日,AppID,游戏名,分钟数");
        StringAssert.Contains(csv, "2026-04-29,2026-04-27,730,Counter-Strike 2,90");
    }

    [TestMethod]
    public void CreateLimitRulesCsv_WritesLimitConfiguration()
    {
        var csv = new DataExporter().CreateLimitRulesCsv(CreateSnapshot());

        StringAssert.Contains(csv, "范围,周期,AppID,名称,限额分钟,执行策略");
        StringAssert.Contains(csv, "Game,Day,730,Counter-Strike 2,120,ForceClose");
    }

    [TestMethod]
    public void CsvWriter_EscapesCommaAndQuotes()
    {
        var csv = new CsvWriter().Write(
            ["名称"],
            [new object?[] { "A \"quoted\", name" }]);

        StringAssert.Contains(csv, "\"A \"\"quoted\"\", name\"");
    }

    [TestMethod]
    public async Task ExportAsync_WritesJsonAndCsvFiles()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "SteamWatch.Tests", Guid.NewGuid().ToString("N"));
        var files = await new DataExporter().ExportAsync(CreateSnapshot(), outputDirectory);

        Assert.IsTrue(File.Exists(files.JsonPath));
        Assert.IsTrue(File.Exists(files.PlaytimeRecordsCsvPath));
        Assert.IsTrue(File.Exists(files.WeeklySummaryCsvPath));
        Assert.IsTrue(File.Exists(files.LimitRulesCsvPath));

        var json = await File.ReadAllTextAsync(files.JsonPath);
        StringAssert.Contains(json, "Counter-Strike 2");
    }

    private static ExportSnapshot CreateSnapshot()
    {
        return new ExportSnapshot(
            new DateTimeOffset(2026, 4, 29, 22, 0, 0, TimeSpan.Zero),
            [new ExportGame(730, "Counter-Strike 2", 1000)],
            [
                new ExportPlaytimeRecord(new DateOnly(2026, 4, 29), 730, "Counter-Strike 2", 90),
                new ExportPlaytimeRecord(new DateOnly(2026, 4, 30), 730, "Counter-Strike 2", 60),
                new ExportPlaytimeRecord(new DateOnly(2026, 4, 29), 570, "Dota 2", 45)
            ],
            [
                new ExportLimitRule(LimitScope.Game, LimitPeriod.Day, 120, EnforcementMode.ForceClose, 730, "Counter-Strike 2"),
                new ExportLimitRule(LimitScope.Global, LimitPeriod.Week, 600, EnforcementMode.NotifyOnly)
            ],
            new ExportSettings("最小化到托盘", true, true, true));
    }
}
