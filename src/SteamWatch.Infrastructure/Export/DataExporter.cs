using System.Text;
using System.Text.Json;
using SteamWatch.Core.Export;

namespace SteamWatch.Infrastructure.Export;

public sealed class DataExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly CsvWriter _csvWriter;

    public DataExporter(CsvWriter? csvWriter = null)
    {
        _csvWriter = csvWriter ?? new CsvWriter();
    }

    public async Task<ExportFileSet> ExportAsync(
        ExportSnapshot snapshot,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);

        var jsonPath = Path.Combine(outputDirectory, "steamwatch-export.json");
        var playtimePath = Path.Combine(outputDirectory, "playtime_records.csv");
        var weeklyPath = Path.Combine(outputDirectory, "weekly_summary.csv");
        var limitsPath = Path.Combine(outputDirectory, "limits.csv");

        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(snapshot, JsonOptions), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(playtimePath, CreatePlaytimeCsv(snapshot), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(weeklyPath, CreateWeeklySummaryCsv(snapshot), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(limitsPath, CreateLimitRulesCsv(snapshot), Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        return new ExportFileSet(jsonPath, playtimePath, weeklyPath, limitsPath);
    }

    public string CreatePlaytimeCsv(ExportSnapshot snapshot)
    {
        return _csvWriter.Write(
            ["日期", "周起始日", "AppID", "游戏名", "分钟数"],
            snapshot.PlaytimeRecords
                .OrderBy(record => record.Date)
                .ThenBy(record => record.GameName, StringComparer.CurrentCultureIgnoreCase)
                .Select(record => new object?[]
                {
                    FormatDate(record.Date),
                    FormatDate(Core.Tracking.WeekCalculator.GetWeekStart(record.Date)),
                    record.AppId,
                    record.GameName,
                    record.Minutes
                }));
    }

    public string CreateWeeklySummaryCsv(ExportSnapshot snapshot)
    {
        return _csvWriter.Write(
            ["周起始日", "周结束日", "AppID", "游戏名", "分钟数"],
            snapshot.GetWeeklySummaries().Select(summary => new object?[]
            {
                FormatDate(summary.WeekStart),
                FormatDate(summary.WeekEnd),
                summary.AppId,
                summary.GameName,
                summary.Minutes
            }));
    }

    public string CreateLimitRulesCsv(ExportSnapshot snapshot)
    {
        return _csvWriter.Write(
            ["范围", "周期", "AppID", "名称", "限额分钟", "执行策略"],
            snapshot.LimitRules.Select(rule => new object?[]
            {
                rule.Scope,
                rule.Period,
                rule.AppId,
                rule.Name,
                rule.MaxMinutes,
                rule.Enforcement
            }));
    }

    private static string FormatDate(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd");
    }
}
