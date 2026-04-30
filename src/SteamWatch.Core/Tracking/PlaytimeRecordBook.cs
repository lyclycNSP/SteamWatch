namespace SteamWatch.Core.Tracking;

public sealed class PlaytimeRecordBook
{
    private readonly Dictionary<DateOnly, Dictionary<int, int>> _records;

    public PlaytimeRecordBook(IEnumerable<DailyPlaytimeRecord>? records = null)
    {
        _records = [];

        if (records is null)
        {
            return;
        }

        foreach (var record in records)
        {
            _records[record.Date] = record.GameMinutes
                .Where(item => item.Value > 0)
                .ToDictionary(item => item.Key, item => item.Value);
        }
    }

    public IReadOnlyList<DailyPlaytimeRecord> Records => _records
        .OrderBy(item => item.Key)
        .Select(item => new DailyPlaytimeRecord(
            item.Key,
            item.Value
                .OrderBy(minutes => minutes.Key)
                .ToDictionary(minutes => minutes.Key, minutes => minutes.Value)))
        .ToList();

    public void Add(DateOnly date, PlaytimeIncrement increment)
    {
        if (increment.Minutes <= 0)
        {
            return;
        }

        if (increment.AppId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(increment), "AppId must be positive.");
        }

        if (!_records.TryGetValue(date, out var gameMinutes))
        {
            gameMinutes = [];
            _records[date] = gameMinutes;
        }

        gameMinutes[increment.AppId] = gameMinutes.GetValueOrDefault(increment.AppId) + increment.Minutes;
    }

    public int GetGameMinutes(DateOnly date, int appId)
    {
        return _records.TryGetValue(date, out var gameMinutes)
            ? gameMinutes.GetValueOrDefault(appId)
            : 0;
    }

    public int GetWeekGameMinutes(DateOnly date, int appId)
    {
        var start = WeekCalculator.GetWeekStart(date);
        var end = WeekCalculator.GetWeekEnd(date);

        return _records
            .Where(item => item.Key >= start && item.Key <= end)
            .Sum(item => item.Value.GetValueOrDefault(appId));
    }

    public int GetTotalMinutes(DateOnly date)
    {
        return _records.TryGetValue(date, out var gameMinutes)
            ? gameMinutes.Values.Sum()
            : 0;
    }

    public int GetWeekTotalMinutes(DateOnly date)
    {
        var start = WeekCalculator.GetWeekStart(date);
        var end = WeekCalculator.GetWeekEnd(date);

        return _records
            .Where(item => item.Key >= start && item.Key <= end)
            .Sum(item => item.Value.Values.Sum());
    }
}
