namespace SteamWatch.Infrastructure.Export;

public sealed record ExportFileSet(
    string JsonPath,
    string PlaytimeRecordsCsvPath,
    string WeeklySummaryCsvPath,
    string LimitRulesCsvPath);
