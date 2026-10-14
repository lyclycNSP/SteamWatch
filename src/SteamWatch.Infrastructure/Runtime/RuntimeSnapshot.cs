using SteamWatch.Core.Limits;
using SteamWatch.Core.Settings;
using SteamWatch.Core.Steam;
using SteamWatch.Core.Tracking;

namespace SteamWatch.Infrastructure.Runtime;

public sealed record RuntimeGame(SteamGameInfo Info, bool IsRunning, int TodayMinutes, int WeekMinutes,
    SteamAppCategory Category = SteamAppCategory.Unknown);
public sealed record RuntimeSnapshot(string SteamPath, string? UserId, string Status,
    IReadOnlyList<RuntimeGame> Games, IReadOnlyList<DailyPlaytimeRecord> Records,
    IReadOnlyList<LimitRule> Rules, AppSettings Settings, bool IsPaused,
    string LimitSummary, IReadOnlyDictionary<int, string> GameNames, IReadOnlyList<DailyPlaytimeRecord> EstimatedRecords,
    IReadOnlyList<EstimatedPlaytimeEntry> EstimatedEntries, string EarliestForceCloseSummary)
{
    public static RuntimeSnapshot Empty { get; } = new("", null, "尚未初始化", [], [], [], new(), false, "尚无限额", new Dictionary<int,string>(), [], [], "尚无强退倒计时");
}

public sealed record SteamWatchBackup(int SchemaVersion, PlaytimeLedgerState Ledger,
    AppSettings Settings, IReadOnlyList<LimitRule> Rules, Dictionary<int, SteamAppCategory>? CategoryOverrides = null);
