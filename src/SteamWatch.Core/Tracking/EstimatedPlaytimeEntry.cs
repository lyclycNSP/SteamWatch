namespace SteamWatch.Core.Tracking;

public enum EstimatedPlaytimeSource { SteamDifference, LegacyAggregate, ManualCorrection }

public sealed record EstimatedPlaytimeEntry(
    Guid Id, DateOnly Date, int AppId, EstimatedPlaytimeSource Source,
    int PreviousMinutes, int CurrentMinutes, DateTimeOffset? RecordedAt,
    string Reason, string? AccountId = null, decimal? SteamExpectedMinutes = null,
    int? SteamObservedMinutes = null)
{
    public int ChangeMinutes => CurrentMinutes - PreviousMinutes;
    public string SourceText => Source switch
    {
        EstimatedPlaytimeSource.SteamDifference => "Steam 缓存差值",
        EstimatedPlaytimeSource.LegacyAggregate => "历史估算（无原始明细）",
        _ => "手动修正"
    };
    public string Summary => $"{Date:yyyy-MM-dd} · AppID {AppId} · {SourceText} · {ChangeMinutes:+0;-0;0} 分钟";
    public string Explanation => $"估算总量 {PreviousMinutes} → {CurrentMinutes} 分钟；{Reason}"
        + (SteamExpectedMinutes is { } expected && SteamObservedMinutes is { } observed
            ? $"；预期累计 {expected:0.###}，Steam 累计 {observed} 分钟" : "")
        + (RecordedAt is { } at ? $"；记录时间 {at:yyyy-MM-dd HH:mm:ss zzz}" : "；原始发生时间未知");
}
