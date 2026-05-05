using SteamWatch.Core.Enforcement;
using SteamWatch.Core.Limits;
using SteamWatch.Core.Notifications;
using SteamWatch.Core.Reminders;
using SteamWatch.Core.Settings;
using SteamWatch.Core.Steam;
using SteamWatch.Core.Tracking;
using SteamWatch.Infrastructure.Enforcement;
using SteamWatch.Infrastructure.Notifications;
using SteamWatch.Infrastructure.Startup;
using SteamWatch.Infrastructure.Steam;
using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.App.Services;

public sealed class SteamWatchAppService
{
    private readonly SteamPathResolver _pathResolver;
    private readonly PlaytimeRecordStore _playtimeStore;
    private readonly LimitRuleStore _limitRuleStore;
    private readonly AppSettingsStore _settingsStore;
    private readonly IStartupManager _startupManager;
    private readonly INotificationService _notificationService;
    private readonly IGameProcessTerminator _processTerminator;
    private RuntimeLimitCoordinator _limitCoordinator = new();
    private readonly HashSet<int> _announcedForceCloseCountdownRuleIds = [];
    private readonly Dictionary<int, GameRowViewModel> _games = [];
    private readonly Dictionary<int, RunningGameSession> _sessions = [];
    private IReadOnlyList<LimitRule> _limitRules = [];
    private AppSettings _settings = new();
    private PlaytimeRecordBook _playtimeRecords = new();
    private SteamProcessMonitor? _monitor;
    private string? _steamPath;

    public event EventHandler<NotificationMessage>? UserNotificationRaised;

    public SteamWatchAppService(
        SteamPathResolver? pathResolver = null,
        PlaytimeRecordStore? playtimeStore = null,
        LimitRuleStore? limitRuleStore = null,
        AppSettingsStore? settingsStore = null,
        IStartupManager? startupManager = null,
        INotificationService? notificationService = null,
        IGameProcessTerminator? processTerminator = null)
    {
        _pathResolver = pathResolver ?? new SteamPathResolver();
        var defaultStore = new JsonFileStore(Path.Combine(AppContext.BaseDirectory, "data"));
        _playtimeStore = playtimeStore ?? new PlaytimeRecordStore(
            defaultStore);
        _limitRuleStore = limitRuleStore ?? new LimitRuleStore(defaultStore);
        _settingsStore = settingsStore ?? new AppSettingsStore(defaultStore);
        _startupManager = startupManager ?? new WindowsStartupManager(new WindowsStartupRegistry());
        _notificationService = notificationService ?? new WindowsAppNotificationService();
        _processTerminator = processTerminator ?? new GameProcessTerminator();
    }

    public async Task<GameListSnapshot> LoadGameListAsync(CancellationToken cancellationToken = default)
    {
        var steamPath = _pathResolver.Resolve();
        _steamPath = steamPath;
        var reader = new SteamCacheReader(steamPath);
        var userId = await reader.GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
        var games = await reader.ReadGamesAsync(userId, cancellationToken).ConfigureAwait(false);
        _playtimeRecords = await _playtimeStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        _limitRules = await _limitRuleStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        _settings = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        ApplyRuntimeSettings(_settings);
        var today = DateOnly.FromDateTime(DateTimeOffset.Now.LocalDateTime);

        var rows = games
            .Select(game => new GameRowViewModel(
                game.AppId,
                game.Name,
                game.PlaytimeForeverMinutes,
                "未运行",
                $"今日 {_playtimeRecords.GetGameMinutes(today, game.AppId)} 分钟",
                $"本周 {_playtimeRecords.GetWeekGameMinutes(today, game.AppId)} 分钟",
                game.IconPath))
            .ToList();

        _games.Clear();
        foreach (var row in rows)
        {
            _games[row.AppId] = row;
        }

        _monitor = new SteamProcessMonitor(
            new WindowsProcessSnapshotProvider(),
            new SteamGameProcessIdentifier(games, steamPath));

        var status = userId is null
            ? $"已找到 Steam：{steamPath}，但未找到当前用户"
            : $"已找到 Steam：{steamPath}，用户 {userId}，游戏 {rows.Count} 个";

        return new GameListSnapshot(steamPath, userId, status, rows);
    }

    public async Task<GameStatusSnapshot> PollRuntimeStatusAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (_monitor is null || _games.Count == 0)
        {
            return new GameStatusSnapshot("监控尚未初始化，请先刷新游戏列表。", _games.Values.ToList());
        }

        var today = DateOnly.FromDateTime(now.LocalDateTime);
        var changes = _monitor.Poll();
        foreach (var started in changes.Started)
        {
            _sessions[started.AppId] = new RunningGameSession(
                started.AppId,
                started.ProcessId,
                ResolveGameName(started),
                now);
        }

        foreach (var running in changes.Running)
        {
            if (!_sessions.TryGetValue(running.AppId, out var session))
            {
                _sessions[running.AppId] = new RunningGameSession(
                    running.AppId,
                    running.ProcessId,
                    ResolveGameName(running),
                    now);
                continue;
            }

            var increment = session.Checkpoint(now);
            await AddPlaytimeIncrementAsync(today, increment, cancellationToken).ConfigureAwait(false);
        }

        foreach (var stopped in changes.Stopped)
        {
            if (!_sessions.Remove(stopped.AppId, out var session))
            {
                continue;
            }

            var increment = session.Complete(now);
            await AddPlaytimeIncrementAsync(today, increment, cancellationToken).ConfigureAwait(false);
        }

        var runningAppIds = changes.Running.Select(game => game.AppId).ToHashSet();
        var limitResult = _limitCoordinator.Evaluate(
            _limitRules,
            _playtimeRecords.Records,
            today,
            runningAppIds,
            now);
        ShowReminderNotifications(limitResult);
        ShowForceCloseCountdownNotifications(limitResult, now);
        var forceCloseStatus = await ExecuteReadyForceCloseAsync(
            limitResult,
            changes.Running,
            now,
            cancellationToken).ConfigureAwait(false);

        var rows = _games.Values
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(game =>
            {
                var todayMinutes = _playtimeRecords.GetGameMinutes(today, game.AppId);
                var weekMinutes = _playtimeRecords.GetWeekGameMinutes(today, game.AppId);
                return game.WithRuntimeState(runningAppIds.Contains(game.AppId), todayMinutes, weekMinutes);
            })
            .ToList();

        var status = runningAppIds.Count == 0
            ? $"监控中：未检测到运行中的 Steam 游戏。Steam 路径：{_steamPath}"
            : $"监控中：{runningAppIds.Count} 个 Steam 游戏正在运行。";
        status = AppendLimitStatus(status, limitResult, now);
        if (!string.IsNullOrWhiteSpace(forceCloseStatus))
        {
            status = $"{status} {forceCloseStatus}";
        }

        return new GameStatusSnapshot(status, rows);
    }

    public IReadOnlyList<LimitRule> GetLimitRules()
    {
        return _limitRules;
    }

    public AppSettings GetSettings()
    {
        return _settings;
    }

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var startupState = _startupManager.GetState();
        _settings = _settings with { StartWithWindows = startupState.IsEnabled };
        ApplyRuntimeSettings(_settings);
        return _settings;
    }

    public async Task<AppSettings> SaveSettingsAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings.StartWithWindows)
        {
            _startupManager.Enable(Environment.ProcessPath ?? AppContext.BaseDirectory);
        }
        else
        {
            _startupManager.Disable();
        }

        await _settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        _settings = settings;
        ApplyRuntimeSettings(_settings);
        return _settings;
    }

    public async Task<IReadOnlyList<LimitRule>> LoadLimitRulesAsync(CancellationToken cancellationToken = default)
    {
        _limitRules = await _limitRuleStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return _limitRules;
    }

    public async Task<IReadOnlyList<LimitRule>> UpsertLimitRuleAsync(
        LimitRule rule,
        CancellationToken cancellationToken = default)
    {
        var rules = _limitRules
            .Where(existing => !IsSameRuleTarget(existing, rule))
            .Append(rule)
            .ToList();

        await _limitRuleStore.SaveAsync(rules, cancellationToken).ConfigureAwait(false);
        _limitRules = rules;
        return _limitRules;
    }

    public async Task<IReadOnlyList<LimitRule>> DeleteLimitRuleAsync(
        LimitRule rule,
        CancellationToken cancellationToken = default)
    {
        var rules = _limitRules
            .Where(existing => !IsSameRuleTarget(existing, rule))
            .ToList();

        await _limitRuleStore.SaveAsync(rules, cancellationToken).ConfigureAwait(false);
        _limitRules = rules;
        return _limitRules;
    }

    public async Task<IReadOnlyList<LimitRule>> ReplaceLimitRuleAsync(
        LimitRule oldRule,
        LimitRule newRule,
        CancellationToken cancellationToken = default)
    {
        var rules = _limitRules
            .Where(existing => !IsSameRuleTarget(existing, oldRule) && !IsSameRuleTarget(existing, newRule))
            .Append(newRule)
            .ToList();

        await _limitRuleStore.SaveAsync(rules, cancellationToken).ConfigureAwait(false);
        _limitRules = rules;
        return _limitRules;
    }

    public IReadOnlyList<PlaytimeStatRowViewModel> GetDailyStats()
    {
        return _playtimeRecords.Records
            .OrderByDescending(record => record.Date)
            .SelectMany(record => record.GameMinutes
                .OrderByDescending(item => item.Value)
                .Select(item => new PlaytimeStatRowViewModel(
                    record.Date.ToString("yyyy-MM-dd"),
                    item.Key,
                    ResolveGameName(item.Key),
                    item.Value,
                    ResolveGameIconPath(item.Key))))
            .ToList();
    }

    public IReadOnlyList<PlaytimeStatRowViewModel> GetWeeklyStats()
    {
        return _playtimeRecords.Records
            .SelectMany(record => record.GameMinutes.Select(item => new
            {
                WeekStart = WeekCalculator.GetWeekStart(record.Date),
                AppId = item.Key,
                Minutes = item.Value
            }))
            .GroupBy(item => new { item.WeekStart, item.AppId })
            .Select(group => new PlaytimeStatRowViewModel(
                $"{group.Key.WeekStart:yyyy-MM-dd} - {WeekCalculator.GetWeekEnd(group.Key.WeekStart):yyyy-MM-dd}",
                group.Key.AppId,
                ResolveGameName(group.Key.AppId),
                group.Sum(item => item.Minutes),
                ResolveGameIconPath(group.Key.AppId)))
            .OrderByDescending(row => row.PeriodText)
            .ThenByDescending(row => row.Minutes)
            .ToList();
    }

    private string ResolveGameName(RunningSteamGame game)
    {
        if (_games.TryGetValue(game.AppId, out var row))
        {
            return row.Name;
        }

        return game.GameName ?? game.ProcessName;
    }

    private string ResolveGameName(int appId)
    {
        return _games.TryGetValue(appId, out var row)
            ? row.Name
            : $"AppID {appId}";
    }

    private string? ResolveGameIconPath(int appId)
    {
        return _games.TryGetValue(appId, out var row)
            ? row.IconPath
            : null;
    }

    private async Task AddPlaytimeIncrementAsync(
        DateOnly date,
        PlaytimeIncrement increment,
        CancellationToken cancellationToken)
    {
        if (increment.Minutes <= 0)
        {
            return;
        }

        _playtimeRecords.Add(date, increment);
        await _playtimeStore.SaveAsync(_playtimeRecords, cancellationToken).ConfigureAwait(false);
    }

    private void ShowReminderNotifications(RuntimeLimitCheckResult limitResult)
    {
        foreach (var decision in limitResult.Decisions.Where(item => item.Reminder.ShouldNotify))
        {
            var message = CreateReminderMessage(decision, _settings.PlayReminderSounds);
            try
            {
                _notificationService.Show(message);
            }
            catch
            {
                // Notification failure must not stop monitoring or playtime persistence.
            }

            UserNotificationRaised?.Invoke(this, message);
        }
    }

    private void ShowForceCloseCountdownNotifications(RuntimeLimitCheckResult limitResult, DateTimeOffset now)
    {
        foreach (var decision in limitResult.Decisions)
        {
            if (decision.ForceClose.State != ForceCloseCountdownState.Waiting || decision.ForceClose.Deadline is null)
            {
                _announcedForceCloseCountdownRuleIds.Remove(decision.RuleId);
                continue;
            }

            if (!_announcedForceCloseCountdownRuleIds.Add(decision.RuleId))
            {
                continue;
            }

            var message = CreateForceCloseCountdownMessage(decision, now, _settings.PlayReminderSounds);
            try
            {
                _notificationService.Show(message);
            }
            catch
            {
                // Notification failure must not stop monitoring or playtime persistence.
            }

            UserNotificationRaised?.Invoke(this, message);
        }
    }

    private static NotificationMessage CreateReminderMessage(RuntimeLimitDecision decision, bool playSound)
    {
        var ruleName = string.IsNullOrWhiteSpace(decision.Evaluation.Rule.Name)
            ? GetRuleDisplayName(decision.Evaluation.Rule)
            : decision.Evaluation.Rule.Name;
        var percent = Math.Floor(decision.Evaluation.Progress * 100);
        var severity = decision.Reminder.Level >= ReminderLevel.Exceeded
            ? NotificationSeverity.Critical
            : NotificationSeverity.Warning;

        return new NotificationMessage(
            "SteamWatch 限额提醒",
            $"{ruleName} 已使用 {decision.Evaluation.UsedMinutes}/{decision.Evaluation.Rule.MaxMinutes} 分钟（{percent}%）。",
            severity,
            playSound);
    }

    private static NotificationMessage CreateForceCloseCountdownMessage(
        RuntimeLimitDecision decision,
        DateTimeOffset now,
        bool playSound)
    {
        var ruleName = string.IsNullOrWhiteSpace(decision.Evaluation.Rule.Name)
            ? GetRuleDisplayName(decision.Evaluation.Rule)
            : decision.Evaluation.Rule.Name;
        var deadline = decision.ForceClose.Deadline ?? now;
        var seconds = Math.Max(0, (int)Math.Ceiling((deadline - now).TotalSeconds));

        return new NotificationMessage(
            "SteamWatch 强退倒计时",
            $"{ruleName} 已超限，将在 {seconds} 秒后尝试关闭游戏。",
            NotificationSeverity.Critical,
            playSound);
    }

    private static string AppendLimitStatus(
        string status,
        RuntimeLimitCheckResult limitResult,
        DateTimeOffset now)
    {
        var readyToClose = limitResult.Decisions.FirstOrDefault(item => item.ForceClose.ShouldCloseNow);
        if (readyToClose is not null)
        {
            return $"{status} {GetRuleDisplayName(readyToClose.Evaluation.Rule)} 已达到强退条件。";
        }

        var countdown = limitResult.Decisions
            .Where(item => item.ForceClose.State == ForceCloseCountdownState.Waiting && item.ForceClose.Deadline is not null)
            .OrderBy(item => item.ForceClose.Deadline)
            .FirstOrDefault();
        if (countdown is not null)
        {
            var remaining = countdown.ForceClose.Deadline!.Value - now;
            var seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
            return $"{status} {GetRuleDisplayName(countdown.Evaluation.Rule)} 强退倒计时 {seconds} 秒。";
        }

        var exceeded = limitResult.Decisions.FirstOrDefault(item => item.Evaluation.IsExceeded);
        if (exceeded is not null)
        {
            return $"{status} {GetRuleDisplayName(exceeded.Evaluation.Rule)} 已超限。";
        }

        return status;
    }

    private async Task<string> ExecuteReadyForceCloseAsync(
        RuntimeLimitCheckResult limitResult,
        IReadOnlyList<RunningSteamGame> runningGames,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var targets = GetForceCloseTargets(limitResult, runningGames)
            .GroupBy(game => game.ProcessId)
            .Select(group => group.First())
            .ToList();
        if (targets.Count == 0)
        {
            return string.Empty;
        }

        var results = new List<GameProcessTerminationResult>();
        foreach (var target in targets)
        {
            if (_sessions.Remove(target.AppId, out var session))
            {
                var increment = session.Complete(now);
                await AddPlaytimeIncrementAsync(
                    DateOnly.FromDateTime(now.LocalDateTime),
                    increment,
                    cancellationToken).ConfigureAwait(false);
            }

            results.Add(_processTerminator.Terminate(target.ProcessId));
        }

        var succeeded = results.Count(item => item.IsSuccess);
        var failed = results.Count - succeeded;

        return failed == 0
            ? $"已请求关闭 {succeeded} 个游戏进程。"
            : $"已请求关闭 {succeeded} 个游戏进程，{failed} 个失败。";
    }

    private static IEnumerable<RunningSteamGame> GetForceCloseTargets(
        RuntimeLimitCheckResult limitResult,
        IReadOnlyList<RunningSteamGame> runningGames)
    {
        foreach (var decision in limitResult.Decisions.Where(item => item.ForceClose.ShouldCloseNow))
        {
            if (decision.Evaluation.Rule.Scope == LimitScope.Global)
            {
                foreach (var game in runningGames)
                {
                    yield return game;
                }

                continue;
            }

            if (decision.Evaluation.Rule.AppId is not int appId)
            {
                continue;
            }

            foreach (var game in runningGames.Where(item => item.AppId == appId))
            {
                yield return game;
            }
        }
    }

    private static string GetRuleDisplayName(LimitRule rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.Name))
        {
            return rule.Name;
        }

        return rule.Scope == LimitScope.Global
            ? "全部游戏"
            : $"AppID {rule.AppId}";
    }

    private static bool IsSameRuleTarget(LimitRule left, LimitRule right)
    {
        return left.Scope == right.Scope
            && left.Period == right.Period
            && left.AppId == right.AppId;
    }

    private void ApplyRuntimeSettings(AppSettings settings)
    {
        var countdownSeconds = Math.Max(5, settings.ForceCloseCountdownSeconds);
        _limitCoordinator = new RuntimeLimitCoordinator(
            reminderPolicy: new ReminderPolicy(new ReminderThresholdSettings(
                settings.FirstReminderThresholdPercent,
                settings.SecondReminderThresholdPercent,
                settings.FinalReminderThresholdPercent)),
            forceClosePolicy: new ForceClosePolicy(TimeSpan.FromSeconds(countdownSeconds)));
    }
}
