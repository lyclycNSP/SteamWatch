using SteamWatch.App.Presentation;
using SteamWatch.Core.Limits;
using SteamWatch.Core.Notifications;
using SteamWatch.Core.Settings;
using SteamWatch.Core.Steam;
using SteamWatch.Core.Tracking;
using SteamWatch.Infrastructure.Enforcement;
using SteamWatch.Infrastructure.Runtime;
using SteamWatch.Infrastructure.Startup;
using SteamWatch.Infrastructure.Steam;
using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.App.Services;

/// <summary>WinUI mapping and lifetime adapter; business state lives in the runtime service.</summary>
public sealed class SteamWatchAppService
{
    private readonly SteamWatchRuntimeService _runtime;
    private readonly DiagnosticLog _diagnostics;
    private readonly BackgroundMonitor _monitor;
    private volatile string? _runtimeError;
    public string DataDirectory { get; }

    public SteamWatchAppService()
    {
        DataDirectory = AppDataDirectory.Resolve();
        _diagnostics = new(DataDirectory);
        _runtime = new(new JsonFileStore(DataDirectory), new SteamCatalogProvider(),
            new WindowsProcessSnapshotProvider(), new WindowsRuntimeClock(),
            new WindowsStartupManager(new WindowsStartupRegistry()), new GameProcessTerminator(),
            new WindowsAppNotificationService(), _diagnostics.Record);
        _monitor = new(MonitorAsync);
        _runtime.UserNotificationRaised += (_, message) => UserNotificationRaised?.Invoke(this, message);
    }

    public event EventHandler<NotificationMessage>? UserNotificationRaised;
    public bool IsPaused => _runtime.Current.IsPaused;
    public int TotalTodayMinutes => new PlaytimeRecordBook(_runtime.Current.Records).GetTotalMinutes(DateOnly.FromDateTime(DateTime.Now));
    public int TotalWeekMinutes => new PlaytimeRecordBook(_runtime.Current.Records).GetWeekTotalMinutes(DateOnly.FromDateTime(DateTime.Now));
    public string LimitSummary => _runtime.Current.LimitSummary;
    public string EarliestForceCloseSummary => _runtime.Current.EarliestForceCloseSummary;
    public Task<RuntimeSnapshot> SetGameCategoryAsync(int appId, SteamAppCategory category) => _runtime.SetGameCategoryAsync(appId, category);
    public AppSettings GetSettings() => _runtime.Current.Settings;
    public IReadOnlyList<LimitRule> GetLimitRules() => _runtime.Current.Rules;
    public bool IsAuthenticationRequired() => _runtime.IsAuthenticationRequired;
    public bool VerifyAuthentication(string password) => _runtime.VerifyAuthentication(password);
    public Task<RuntimeSnapshot> InitializeAsync() => _runtime.InitializeAsync();

    public async Task<RuntimeReadModel> LoadGameListAsync(CancellationToken cancellationToken = default)
    {
        await _runtime.RefreshGamesAsync(cancellationToken);
        var snapshot = await _runtime.PollAsync(cancellationToken);
        _runtimeError = null;
        _monitor.Start();
        return new(snapshot, DateOnly.FromDateTime(DateTime.Now));
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try { await _runtime.PollAsync(cancellationToken); _runtimeError = null; }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _diagnostics.Record("monitor", ex); _runtimeError = $"监控失败：{ex.Message}。将重试；请保留数据文件。"; }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    public RuntimeReadModel CaptureReadModel() => new(_runtime.Current,
        DateOnly.FromDateTime(DateTime.Now), _runtimeError);

    public Task<RuntimeSnapshot> RestoreBackupAsync(string json) => _runtime.RestoreBackupAsync(json);
    public Task<string> CreateBackupAsync() => _runtime.CreateBackupAsync();
    public Task<byte[]> CreateDiagnosticsAsync() => Task.Run(() => DiagnosticPackage.Create(DataDirectory, _runtime.Current));
    public void RecordDiagnostic(string category, Exception exception) => _diagnostics.Record(category, exception);
    public string GetGameName(int appId) => _runtime.Current.GameNames.GetValueOrDefault(appId) ?? $"AppID {appId}";
    public IReadOnlyList<EstimatedPlaytimeEntry> GetEstimatedEntries(DateOnly date) =>
        _runtime.Current.EstimatedEntries.Where(e => e.Date == date).ToList();
    public int GetEstimatedMinutes(DateOnly date, int appId) =>
        _runtime.Current.EstimatedRecords.FirstOrDefault(r => r.Date == date)?.GetGameMinutes(appId) ?? 0;
    public Task<RuntimeSnapshot> CorrectEstimatedMinutesAsync(DateOnly date, int appId, int expectedMinutes, int minutes, string reason) =>
        _runtime.CorrectEstimatedMinutesAsync(date, appId, expectedMinutes, minutes, reason);
    public Task<RuntimeSnapshot> SetPausedAsync(bool paused) => _runtime.SetPausedAsync(paused);
    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default) =>
        (await _runtime.InitializeAsync(cancellationToken)).Settings;
    public Task<AppSettings> SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        _runtime.SaveSettingsAsync(settings, cancellationToken);
    public Task<IReadOnlyList<LimitRule>> UpsertLimitRuleAsync(LimitRule rule) => _runtime.SaveRuleAsync(rule);
    public Task<IReadOnlyList<LimitRule>> ReplaceLimitRuleAsync(LimitRule oldRule, LimitRule newRule) => _runtime.SaveRuleAsync(newRule, oldRule);
    public Task<IReadOnlyList<LimitRule>> DeleteLimitRuleAsync(LimitRule rule) => _runtime.DeleteRuleAsync(rule);

    public void ResumeAfterExitFailure() => _monitor.Start();

    public Task ShutdownAsync() => _monitor.StopAndFlushAsync(async () => await _runtime.FlushAsync());

    public static IReadOnlyList<GameRowViewModel> MapGames(RuntimeReadModel model)
    {
        var snapshot = model.Source;
        var book = model.MeasuredBook;
        var date = model.Today;
        var rows = snapshot.Games.OrderByDescending(g => g.IsRunning).ThenBy(g => g.Info.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(g =>
            {
                var today = book.GetGameMinutes(date, g.Info.AppId);
                var week = book.GetWeekGameMinutes(date, g.Info.AppId);
                var rules = snapshot.Rules.Where(rule => rule.IsEnabled && rule.Scope == LimitScope.Game && rule.AppId == g.Info.AppId);
                return new GameRowViewModel(g.Info.AppId, g.Info.Name, g.Info.PlaytimeForeverMinutes,
                    g.IsRunning ? "运行中" : "未运行", $"{today} 分钟", $"{week} 分钟", g.Info.IconPath)
                {
                    TodayMinutes = today, WeekMinutes = week, Category = g.Category,
                    RuleText = RuntimePresentation.GameRuleSummary(rules)
                };
            }).ToList();
        var knownIds = rows.Select(row => row.AppId).ToHashSet();
        foreach (var group in snapshot.Rules.Where(rule => rule.Scope == LimitScope.Game && rule.AppId.HasValue)
            .GroupBy(rule => rule.AppId!.Value).Where(group => !knownIds.Contains(group.Key)))
        {
            var today = book.GetGameMinutes(date, group.Key);
            var week = book.GetWeekGameMinutes(date, group.Key);
            rows.Add(new GameRowViewModel(group.Key, group.First().Name, 0, "未在当前库中", $"{today} 分钟", $"{week} 分钟")
            {
                TodayMinutes = today, WeekMinutes = week,
                RuleText = RuntimePresentation.GameRuleSummary(group)
            });
        }
        return rows;
    }
}
