using SteamWatch.Core.Steam;

namespace SteamWatch.Core.Tracking;

public sealed record DailyPlaytimeDuration(
    DateOnly Date, Dictionary<int, long> MeasuredTicks, Dictionary<int, int> EstimatedMinutes);

public sealed record PlaytimeLedgerState(
    int SchemaVersion,
    List<DailyPlaytimeDuration> Days,
    Dictionary<string, Dictionary<int, decimal>> SteamBaselines,
    Dictionary<int, string> GameNames,
    List<EstimatedPlaytimeEntry>? EstimatedEntries = null);

public sealed record PlaytimeLedgerReadSnapshot(
    IReadOnlyList<DailyPlaytimeRecord> Records,
    IReadOnlyDictionary<int, string> GameNames,
    IReadOnlyList<DailyPlaytimeRecord> EstimatedRecords,
    IReadOnlyList<EstimatedPlaytimeEntry> EstimatedEntries);

/// <summary>Exact local durations and Steam reconciliation cursors share one durable state.</summary>
public sealed class PlaytimeLedger
{
    public const int CurrentSchemaVersion = 2;
    private readonly Dictionary<DateOnly, DailyPlaytimeDuration> _days;
    private readonly Dictionary<string, Dictionary<int, decimal>> _baselines;
    private readonly Dictionary<int, string> _names;
    private readonly List<EstimatedPlaytimeEntry> _estimatedEntries;

    public PlaytimeLedger(PlaytimeLedgerState? state = null)
    {
        if (state is not null && state.SchemaVersion is not (1 or CurrentSchemaVersion))
            throw new InvalidDataException("不支持此时长数据版本，请保留文件并使用匹配版本的 SteamWatch。");
        if (state is not null && (state.Days is null || state.SteamBaselines is null || state.GameNames is null
            || state.Days.Any(d => d is null || d.MeasuredTicks is null || d.EstimatedMinutes is null)
            || state.SteamBaselines.Values.Any(value => value is null)))
            throw new InvalidDataException("时长数据结构不完整，原文件已保留。");
        _days = state?.Days.ToDictionary(d => d.Date, d => new DailyPlaytimeDuration(
            d.Date, new(d.MeasuredTicks), new(d.EstimatedMinutes))) ?? [];
        _baselines = state?.SteamBaselines.ToDictionary(p => p.Key, p => new Dictionary<int, decimal>(p.Value)) ?? [];
        _names = state is null ? [] : new(state.GameNames);
        if (state is { SchemaVersion: CurrentSchemaVersion, EstimatedEntries: null })
            throw new InvalidDataException("时长账本缺少补记明细，请恢复备份。");
        _estimatedEntries = state?.SchemaVersion == 1
            ? _days.Values.SelectMany(d => d.EstimatedMinutes.Where(p => p.Value > 0).Select(p =>
                new EstimatedPlaytimeEntry(Guid.NewGuid(), d.Date, p.Key, EstimatedPlaytimeSource.LegacyAggregate,
                    0, p.Value, null, "从 v1 聚合估算迁移，缺少原始补记明细。"))).ToList()
            : state?.EstimatedEntries?.ToList() ?? [];
        if (_days.Values.Any(d => d.MeasuredTicks.Any(p => p.Key <= 0 || p.Value < 0)
            || d.EstimatedMinutes.Any(p => p.Key <= 0 || p.Value < 0))
            || _baselines.Values.Any(b => b.Any(p => p.Key <= 0 || p.Value < 0)))
            throw new InvalidDataException("时长数据包含无效值，请从备份恢复。");
        ValidateEstimatedEntries();
    }

    private void ValidateEstimatedEntries()
    {
        var ids = new HashSet<Guid>();
        var totals = new Dictionary<(DateOnly, int), int>();
        foreach (var entry in _estimatedEntries)
        {
            if (entry is null || entry.Id == Guid.Empty || !ids.Add(entry.Id) || entry.AppId <= 0
                || !Enum.IsDefined(entry.Source) || entry.PreviousMinutes < 0 || entry.CurrentMinutes < 0
                || string.IsNullOrWhiteSpace(entry.Reason) || entry.Reason.Length > 500
                || totals.GetValueOrDefault((entry.Date, entry.AppId)) != entry.PreviousMinutes
                || (entry.Source != EstimatedPlaytimeSource.ManualCorrection && entry.CurrentMinutes < entry.PreviousMinutes)
                || (entry.Source == EstimatedPlaytimeSource.SteamDifference
                    && (string.IsNullOrWhiteSpace(entry.AccountId) || entry.SteamExpectedMinutes is null or < 0
                        || entry.SteamObservedMinutes is null or < 0 || entry.RecordedAt is null))
                || (entry.Source == EstimatedPlaytimeSource.ManualCorrection && entry.RecordedAt is null))
                throw new InvalidDataException("补记明细无效或修改链不连续，请恢复备份。");
            totals[(entry.Date, entry.AppId)] = entry.CurrentMinutes;
        }
        foreach (var day in _days.Values)
            foreach (var pair in day.EstimatedMinutes)
                if (totals.GetValueOrDefault((day.Date, pair.Key)) != pair.Value)
                    throw new InvalidDataException("补记明细与每日汇总不一致。");
        if (totals.Any(p => !_days.TryGetValue(p.Key.Item1, out var day)
            || day.EstimatedMinutes.GetValueOrDefault(p.Key.Item2) != p.Value))
            throw new InvalidDataException("补记明细缺少每日汇总。");
    }

    public PlaytimeLedgerState Export() => new(CurrentSchemaVersion,
        _days.Values.OrderBy(d => d.Date).Select(d => new DailyPlaytimeDuration(
            d.Date, new(d.MeasuredTicks), new(d.EstimatedMinutes))).ToList(),
        _baselines.ToDictionary(p => p.Key, p => new Dictionary<int, decimal>(p.Value)), new(_names), _estimatedEntries.ToList());

    /// <summary>Detached display data without copying exact durations or reconciliation cursors.</summary>
    public PlaytimeLedgerReadSnapshot CreateReadSnapshot()
    {
        var days = _days.Values.OrderBy(d => d.Date).ToArray();
        return new(days.Select(d => ProjectRecord(d, includeEstimates: true)).ToList(), new Dictionary<int, string>(_names),
            days.Where(d => d.EstimatedMinutes.Count > 0)
                .Select(d => new DailyPlaytimeRecord(d.Date, new Dictionary<int, int>(d.EstimatedMinutes))).ToList(),
            _estimatedEntries.ToList());
    }

    public int GetEstimatedMinutes(DateOnly date, int appId) =>
        _days.TryGetValue(date, out var day) ? day.EstimatedMinutes.GetValueOrDefault(appId) : 0;

    public void CorrectEstimatedMinutes(DateOnly date, int appId, int expectedMinutes,
        int newMinutes, string reason, DateTimeOffset recordedAt)
    {
        if (appId <= 0 || newMinutes < 0) throw new ArgumentOutOfRangeException(nameof(newMinutes));
        reason = reason.Trim();
        if (reason.Length is < 1 or > 500) throw new ArgumentException("请填写 1–500 字的修正原因。", nameof(reason));
        if (!_days.TryGetValue(date, out var day) || !_estimatedEntries.Any(e => e.Date == date && e.AppId == appId))
            throw new InvalidOperationException("该日期和游戏没有可修正的估算记录。");
        var previous = GetEstimatedMinutes(date, appId);
        if (previous != expectedMinutes) throw new InvalidOperationException("估算数据已变化，请重新查看后修正。");
        if (previous == newMinutes) throw new ArgumentException("估算分钟没有变化。");
        _ = checked(newMinutes + (int)(day.MeasuredTicks.GetValueOrDefault(appId) / TimeSpan.TicksPerMinute));
        _estimatedEntries.Add(new(Guid.NewGuid(), date, appId, EstimatedPlaytimeSource.ManualCorrection,
            previous, newMinutes, recordedAt, reason));
        day.EstimatedMinutes[appId] = newMinutes;
    }

    public IReadOnlyList<DailyPlaytimeRecord> GetRecords(bool includeEstimates = true) => _days.Values
        .OrderBy(d => d.Date).Select(d => ProjectRecord(d, includeEstimates))
        .ToList();

    private static DailyPlaytimeRecord ProjectRecord(DailyPlaytimeDuration day, bool includeEstimates)
    {
        var minutes = new Dictionary<int, int>(Math.Max(day.MeasuredTicks.Count, day.EstimatedMinutes.Count));
        foreach (var pair in day.MeasuredTicks)
            minutes[pair.Key] = checked((int)(pair.Value / TimeSpan.TicksPerMinute));
        foreach (var pair in day.EstimatedMinutes)
            minutes[pair.Key] = minutes.GetValueOrDefault(pair.Key) + (includeEstimates ? pair.Value : 0);
        return new(day.Date, minutes);
    }

    public string? GetGameName(int appId) => _names.GetValueOrDefault(appId);

    public void ImportLegacy(IEnumerable<DailyPlaytimeRecord> records)
    {
        foreach (var record in records)
            foreach (var pair in record.GameMinutes.Where(p => p.Key > 0 && p.Value > 0))
                Day(record.Date).MeasuredTicks[pair.Key] = checked((long)pair.Value * TimeSpan.TicksPerMinute);
    }

    public int ObserveSteam(string? accountId, IReadOnlyList<SteamGameInfo> games, DateOnly date,
        DateTimeOffset? recordedAt = null)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return 0;
        if (!_baselines.TryGetValue(accountId, out var baseline))
            _baselines[accountId] = baseline = [];
        var added = 0;
        foreach (var game in games.Where(g => g.AppId > 0 && g.PlaytimeForeverMinutes >= 0 && g.HasPlaytimeObservation))
        {
            _names[game.AppId] = game.Name;
            if (baseline.TryGetValue(game.AppId, out var expected))
            {
                var delta = Math.Max(0, decimal.ToInt32(decimal.Floor(game.PlaytimeForeverMinutes - expected)));
                if (delta > 0)
                {
                    var day = Day(date);
                    var previous = day.EstimatedMinutes.GetValueOrDefault(game.AppId);
                    var current = checked(previous + delta);
                    day.EstimatedMinutes[game.AppId] = current;
                    _estimatedEntries.Add(new(Guid.NewGuid(), date, game.AppId, EstimatedPlaytimeSource.SteamDifference,
                        previous, current, recordedAt ?? DateTimeOffset.Now, "Steam 累计时长高于已对账基线，按完整分钟估算。",
                        accountId, expected, game.PlaytimeForeverMinutes));
                    added = checked(added + delta);
                }
                // A stale/missing Steam cache must never move the cursor backwards.
                baseline[game.AppId] = Math.Max(expected, game.PlaytimeForeverMinutes);
            }
            else baseline[game.AppId] = game.PlaytimeForeverMinutes;
        }
        return added;
    }

    public void RecordInterval(int appId, string gameName, string? accountId,
        DateTimeOffset from, DateTimeOffset to, TimeSpan activeElapsed)
    {
        if (appId <= 0) throw new ArgumentOutOfRangeException(nameof(appId));
        if (activeElapsed <= TimeSpan.Zero) return;
        _names[appId] = gameName;
        // Sleep and clock corrections are excluded by the active clock. Their exact
        // placement inside a missed sample is unknowable; allocate active time at the end.
        var wallElapsed = to - from;
        var segmentStart = wallElapsed > TimeSpan.Zero && Math.Abs((wallElapsed - activeElapsed).TotalSeconds) < 2
            ? from : to - activeElapsed;
        var remaining = activeElapsed.Ticks;
        while (remaining > 0)
        {
            var date = DateOnly.FromDateTime(segmentStart.DateTime);
            var midnight = new DateTimeOffset(date.AddDays(1).ToDateTime(TimeOnly.MinValue), segmentStart.Offset);
            var ticks = Math.Min(remaining, Math.Max(1, (midnight - segmentStart).Ticks));
            var day = Day(date);
            day.MeasuredTicks[appId] = checked(day.MeasuredTicks.GetValueOrDefault(appId) + ticks);
            remaining -= ticks;
            segmentStart = segmentStart.AddTicks(ticks);
        }
        if (accountId is not null && _baselines.TryGetValue(accountId, out var baseline)
            && baseline.ContainsKey(appId))
            baseline[appId] += (decimal)activeElapsed.Ticks / TimeSpan.TicksPerMinute;
    }

    private DailyPlaytimeDuration Day(DateOnly date)
    {
        if (!_days.TryGetValue(date, out var day))
            _days[date] = day = new(date, [], []);
        return day;
    }
}
