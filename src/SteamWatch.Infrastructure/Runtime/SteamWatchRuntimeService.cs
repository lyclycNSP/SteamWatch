using SteamWatch.Core.Enforcement;
using SteamWatch.Core.Limits;
using SteamWatch.Core.Notifications;
using SteamWatch.Core.Reminders;
using SteamWatch.Core.Security;
using SteamWatch.Core.Settings;
using SteamWatch.Core.Steam;
using SteamWatch.Core.Tracking;
using SteamWatch.Infrastructure.Enforcement;
using SteamWatch.Infrastructure.Notifications;
using SteamWatch.Infrastructure.Startup;
using SteamWatch.Infrastructure.Steam;
using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.Infrastructure.Runtime;

/// <summary>One writer owns runtime state. Public snapshots are immutable copies for the UI.</summary>
public sealed class SteamWatchRuntimeService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonFileStore _store;
    private readonly PlaytimeLedgerStore _ledgerStore;
    private readonly AppSettingsStore _settingsStore;
    private readonly LimitRuleStore _rulesStore;
    private readonly ISteamCatalogProvider _catalogProvider;
    private readonly IProcessSnapshotProvider _processes;
    private readonly IRuntimeClock _clock;
    private readonly DateTimeOffset _policyOrigin;
    private readonly long _policyOriginTicks;
    private DateTimeOffset PolicyNow => _policyOrigin.AddTicks(Math.Max(0, _clock.ActiveTicks - _policyOriginTicks));
    private readonly IStartupManager _startup;
    private readonly IGameProcessTerminator _terminator;
    private readonly INotificationService _notifications;
    private readonly Action<string, Exception>? _diagnostics;
    private string? _reconciliationNotice;
    private readonly RuntimeLimitCoordinator _limits = new();
    private readonly Dictionary<int, Session> _sessions = [];
    private readonly Dictionary<int, DateTimeOffset> _announcedDeadlines = [];
    private PlaytimeLedger _ledger = new();
    private AppSettings _settings = new();
    private IReadOnlyList<LimitRule> _rules = [];
    private Dictionary<int, SteamAppCategory> _categories = [];
    private SteamCatalog? _catalog;
    private SteamGameProcessIdentifier? _identifier;
    private bool _initialized;
    private bool _dirty;
    private bool _paused;
    private volatile RuntimeSnapshot _current = RuntimeSnapshot.Empty;
    private RuntimeLimitCheckResult _lastLimits = new([]);
    private string _status = "尚未初始化";

    public SteamWatchRuntimeService(JsonFileStore store, ISteamCatalogProvider catalogProvider,
        IProcessSnapshotProvider processes, IRuntimeClock clock, IStartupManager startup,
        IGameProcessTerminator terminator, INotificationService notifications, Action<string, Exception>? diagnostics = null)
    {
        _store = store;
        _ledgerStore = new(store);
        _settingsStore = new(store);
        _rulesStore = new(store);
        _catalogProvider = catalogProvider;
        _processes = processes;
        _clock = clock;
        _policyOrigin = clock.Now;
        _policyOriginTicks = clock.ActiveTicks;
        _startup = startup;
        _terminator = terminator;
        _notifications = notifications;
        _diagnostics = diagnostics;
    }

    public event EventHandler<NotificationMessage>? UserNotificationRaised;
    public RuntimeSnapshot Current => _current;
    public bool IsAuthenticationRequired => _current.Settings.RequireAuthenticationForSensitiveActions
        && _current.Settings.AuthenticationCredential is not null;
    public bool VerifyAuthentication(string password) => PasswordHasher.Verify(password, _current.Settings.AuthenticationCredential);

    public Task<RuntimeSnapshot> InitializeAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            await InitializeLockedAsync(cancellationToken).ConfigureAwait(false);
            return Publish();
        }, cancellationToken);

    private async Task InitializeLockedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;
        var pendingRestore = await _store.LoadAsync<SteamWatchBackup>("workspace-restore", cancellationToken).ConfigureAwait(false);
        if (pendingRestore is not null) await CompleteRestoreAsync(pendingRestore, cancellationToken).ConfigureAwait(false);
        // Do not partially publish default settings when authentication data cannot be read.
        var settings = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        ValidateSettings(settings);
        var rules = await _rulesStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var ledger = await _ledgerStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var categories = await _store.LoadAsync<Dictionary<int, SteamAppCategory>>("game-categories", cancellationToken).ConfigureAwait(false) ?? [];
        ValidateCategories(categories);
        _settings = settings with { StartWithWindows = _startup.GetState().IsEnabled };
        _rules = rules;
        _ledger = ledger;
        _categories = categories;
        ConfigureLimits();
        _initialized = true;
        _status = _store.RecoveryWarning ?? "设置和时长数据已加载";
    }

    public Task<RuntimeSnapshot> RefreshGamesAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            await InitializeLockedAsync(cancellationToken).ConfigureAwait(false);
            CheckpointSessions(_clock.Now, _clock.ActiveTicks);
            await SaveLedgerAsync(cancellationToken).ConfigureAwait(false);
            var catalog = await _catalogProvider.LoadAsync(cancellationToken).ConfigureAwait(false);
            // Only reconcile outside live sessions. A live cache may already include
            // the same unsampled seconds that will be measured by the next poll.
            var added = _sessions.Count == 0
                ? _ledger.ObserveSteam(catalog.UserId, catalog.Games, DateOnly.FromDateTime(_clock.Now.DateTime), _clock.Now) : 0;
            if (_catalog?.UserId != catalog.UserId && _sessions.Count > 0)
                _ledger.ObserveSteam(catalog.UserId, catalog.Games, DateOnly.FromDateTime(_clock.Now.DateTime), _clock.Now);
            _catalog = catalog;
            _identifier = new(catalog.Games, catalog.SteamPath);
            _dirty = true;
            await SaveLedgerAsync(cancellationToken).ConfigureAwait(false);
            _status = catalog.Games.Count == 0 ? "未发现已安装游戏，请检查 Steam 路径及游戏库。"
                : $"已读取 {catalog.Games.Count} 个游戏。";
            if (added > 0) _reconciliationNotice = $"本次已估算补记 {added} 分钟（不计入限额）。";
            if (_store.RecoveryWarning is not null) _status += " " + _store.RecoveryWarning;
            return Publish();
        }, cancellationToken);

    public Task<RuntimeSnapshot> PollAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            await InitializeLockedAsync(cancellationToken).ConfigureAwait(false);
            if (_identifier is null) return Publish();
            var snapshots = await Task.Run(_processes.GetProcesses, cancellationToken).ConfigureAwait(false);
            var running = _identifier.Identify(snapshots);
            var now = _clock.Now;
            var ticks = _clock.ActiveTicks;
            CheckpointSessions(now, ticks);
            var ids = running.Select(g => g.AppId).ToHashSet();
            foreach (var id in _sessions.Keys.Where(id => !ids.Contains(id)).ToList()) _sessions.Remove(id);
            foreach (var game in running)
                _sessions[game.AppId] = new(game, now, ticks);
            // Persistence must succeed before any destructive action is attempted.
            await SaveLedgerAsync(cancellationToken).ConfigureAwait(false);
            _status = _paused ? "限制已暂停，仍在记录游玩时长。"
                : running.Count == 0 ? "监控中：未检测到运行中的 Steam 游戏。" : $"监控中：{running.Count} 个游戏正在运行。";
            if (!_paused)
            {
                var records = _ledger.GetRecords(includeEstimates: false);
                _lastLimits = _limits.Evaluate(_rules, records, DateOnly.FromDateTime(now.DateTime), ids, PolicyNow);
                Notify(_lastLimits);
                foreach (var target in GetCloseTargets(_lastLimits, running).DistinctBy(g => g.ProcessId))
                {
                    var result = await _terminator.TerminateAsync(target, cancellationToken).ConfigureAwait(false);
                    _status += result.IsSuccess ? " 已确认关闭游戏。" : $" 游戏关闭失败：{result.ErrorMessage ?? "未确认退出"}。";
                }
            }
            return Publish();
        }, cancellationToken);

    public Task<RuntimeSnapshot> SetPausedAsync(bool paused, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            await InitializeLockedAsync(cancellationToken).ConfigureAwait(false);
            _paused = paused;
            _limits.Reset();
            _announcedDeadlines.Clear();
            _lastLimits = new([]);
            _status = paused ? "限制已暂停，继续计时；恢复后重新给予强退宽限期。" : "限制已恢复。";
            return Publish();
        }, cancellationToken);

    public Task<RuntimeSnapshot> FlushAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            if (!_initialized) return Current;
            CheckpointSessions(_clock.Now, _clock.ActiveTicks);
            await SaveLedgerAsync(cancellationToken).ConfigureAwait(false);
            return Publish();
        }, cancellationToken);

    public Task<AppSettings> SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            await InitializeLockedAsync(cancellationToken).ConfigureAwait(false);
            ValidateSettings(settings);
            var previousStartup = _startup.GetState();
            try
            {
                if (settings.StartWithWindows) _startup.Enable(AppDataDirectory.ResolveExecutablePath());
                else _startup.Disable();
                await _settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                if (previousStartup.IsEnabled && previousStartup.Command is not null) _startup.Enable(previousStartup.Command);
                else _startup.Disable();
                throw;
            }
            _settings = settings;
            ConfigureLimits(); // Configure policy without discarding existing deadlines.
            RefreshLimitDecisions();
            Publish();
            return settings;
        }, cancellationToken);

    public Task<IReadOnlyList<LimitRule>> SaveRuleAsync(LimitRule rule, LimitRule? previous = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
        {
            await InitializeLockedAsync(cancellationToken).ConfigureAwait(false);
            rule = rule.Normalize();
            var rules = _rules.Where(r => r.Identity != rule.Identity && r.Identity != previous?.Identity).Append(rule).ToList();
            await _rulesStore.SaveAsync(rules, cancellationToken).ConfigureAwait(false);
            _rules = rules;
            RefreshLimitDecisions();
            Publish();
            return (IReadOnlyList<LimitRule>)rules;
        }, cancellationToken);

    public Task<IReadOnlyList<LimitRule>> DeleteRuleAsync(LimitRule rule, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            await InitializeLockedAsync(cancellationToken).ConfigureAwait(false);
            var rules = _rules.Where(r => r.Identity != rule.Identity).ToList();
            await _rulesStore.SaveAsync(rules, cancellationToken).ConfigureAwait(false);
            _rules = rules;
            RefreshLimitDecisions();
            Publish();
            return (IReadOnlyList<LimitRule>)rules;
        }, cancellationToken);

    public Task<RuntimeSnapshot> CorrectEstimatedMinutesAsync(DateOnly date, int appId, int expectedMinutes,
        int newMinutes, string reason, CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
    {
        await InitializeLockedAsync(cancellationToken).ConfigureAwait(false);
        CheckpointSessions(_clock.Now, _clock.ActiveTicks);
        await SaveLedgerAsync(cancellationToken).ConfigureAwait(false);
        var candidate = new PlaytimeLedger(_ledger.Export());
        candidate.CorrectEstimatedMinutes(date, appId, expectedMinutes, newMinutes, reason, _clock.Now);
        await _store.SaveAsync($"workspace-before-correction-{Guid.NewGuid():N}",
            BuildBackup(), cancellationToken).ConfigureAwait(false);
        await _ledgerStore.SaveAsync(candidate, cancellationToken).ConfigureAwait(false);
        _ledger = candidate;
        _status = "估算时长已修正；实测时长、限额与 Steam 基线未改变。";
        return Publish();
    }, cancellationToken);

    public Task<string> CreateBackupAsync(CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
    {
        await InitializeLockedAsync(cancellationToken).ConfigureAwait(false);
        CheckpointSessions(_clock.Now, _clock.ActiveTicks);
        await SaveLedgerAsync(cancellationToken).ConfigureAwait(false);
        return System.Text.Json.JsonSerializer.Serialize(BuildBackup(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true });
    }, cancellationToken);

    public Task<RuntimeSnapshot> RestoreBackupAsync(string json, CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
    {
        await InitializeLockedAsync(cancellationToken).ConfigureAwait(false);
        if (json.Length > 64 * 1024 * 1024) throw new InvalidDataException("备份超过 64 MiB 限制。");
        var backup = System.Text.Json.JsonSerializer.Deserialize<SteamWatchBackup>(json,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("备份为空。");
        ValidateBackup(backup);
        CheckpointSessions(_clock.Now, _clock.ActiveTicks);
        await SaveLedgerAsync(cancellationToken).ConfigureAwait(false);
        // This durable journal makes a multi-file restore restartable after a crash.
        await _store.SaveAsync($"workspace-before-restore-{Guid.NewGuid():N}",
            BuildBackup(), cancellationToken).ConfigureAwait(false);
        await _store.SaveAsync("workspace-restore", backup, cancellationToken).ConfigureAwait(false);
        _initialized = false;
        await InitializeLockedAsync(cancellationToken).ConfigureAwait(false);
        _sessions.Clear();
        _limits.Reset();
        _announcedDeadlines.Clear();
        _lastLimits = new([]);
        _status = "备份已恢复；此前完整数据保留在 workspace-before-restore 备份中。";
        return Publish();
    }, cancellationToken);

    private static void ValidateBackup(SteamWatchBackup backup)
    {
        if (backup.SchemaVersion != 1 || backup.Ledger is null || backup.Settings is null || backup.Rules is null)
            throw new InvalidDataException("不支持此备份格式。");
        _ = new PlaytimeLedger(backup.Ledger);
        ValidateSettings(backup.Settings);
        foreach (var rule in backup.Rules) _ = rule.Normalize();
        ValidateCategories(backup.CategoryOverrides ?? []);
    }

    private async Task CompleteRestoreAsync(SteamWatchBackup backup, CancellationToken cancellationToken)
    {
        ValidateBackup(backup);
        await _ledgerStore.SaveAsync(new PlaytimeLedger(backup.Ledger), cancellationToken).ConfigureAwait(false);
        await _rulesStore.SaveAsync(backup.Rules.Select(r => r.Normalize()).GroupBy(r => r.Identity).Select(g => g.Last()).ToList(), cancellationToken).ConfigureAwait(false);
        await _settingsStore.SaveAsync(backup.Settings, cancellationToken).ConfigureAwait(false);
        await _store.SaveAsync("game-categories", backup.CategoryOverrides ?? [], cancellationToken).ConfigureAwait(false);
        if (backup.Settings.StartWithWindows) _startup.Enable(AppDataDirectory.ResolveExecutablePath());
        else _startup.Disable();
        _store.Delete("workspace-restore");
        _sessions.Clear();
        _dirty = false;
        _limits.Reset();
        _announcedDeadlines.Clear();
        _lastLimits = new([]);
    }

    private void CheckpointSessions(DateTimeOffset now, long ticks)
    {
        foreach (var (id, session) in _sessions.ToArray())
        {
            var elapsed = TimeSpan.FromTicks(Math.Max(0, ticks - session.ActiveTicks));
            _ledger.RecordInterval(id, session.Game.GameName ?? session.Game.ProcessName, _catalog?.UserId,
                session.WallTime, now, elapsed);
            _sessions[id] = session with { WallTime = now, ActiveTicks = ticks };
            _dirty |= elapsed > TimeSpan.Zero;
        }
    }

    private async Task SaveLedgerAsync(CancellationToken cancellationToken)
    {
        if (!_dirty) return;
        await _ledgerStore.SaveAsync(_ledger, cancellationToken).ConfigureAwait(false);
        _dirty = false;
    }

    private void ConfigureLimits() => _limits.Configure(new ReminderPolicy(new(
        _settings.FirstReminderThresholdPercent, _settings.SecondReminderThresholdPercent,
        _settings.FinalReminderThresholdPercent)), new ForceClosePolicy(TimeSpan.FromSeconds(_settings.ForceCloseCountdownSeconds)));

    private SteamWatchBackup BuildBackup() => new(1, _ledger.Export(), _settings, _rules, new(_categories));

    private static void ValidateCategories(Dictionary<int, SteamAppCategory> categories)
    {
        if (categories.Any(p => p.Key <= 0 || !Enum.IsDefined(p.Value)))
            throw new InvalidDataException("游戏分类数据无效。");
    }

    public Task<RuntimeSnapshot> SetGameCategoryAsync(int appId, SteamAppCategory category,
        CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
    {
        await InitializeLockedAsync(cancellationToken).ConfigureAwait(false);
        if (appId <= 0 || !Enum.IsDefined(category)) throw new ArgumentException("游戏分类无效。");
        var categories = new Dictionary<int, SteamAppCategory>(_categories) { [appId] = category };
        if (category == SteamAppCategory.Unknown) categories.Remove(appId);
        await _store.SaveAsync("game-categories", categories, cancellationToken).ConfigureAwait(false);
        _categories = categories;
        return Publish();
    }, cancellationToken);

    private void RefreshLimitDecisions()
    {
        _lastLimits = _paused ? new([]) : _limits.Evaluate(_rules, _ledger.GetRecords(includeEstimates: false),
            DateOnly.FromDateTime(_clock.Now.DateTime), _sessions.Keys.ToHashSet(), PolicyNow);
        if (!_paused) Notify(_lastLimits);
    }

    private static void ValidateSettings(AppSettings settings)
    {
        if (settings.AuthenticationCredential is { } credential)
        {
            try
            {
                if (credential.Iterations is < 10_000 or > 2_000_000 || credential.KeySize is < 16 or > 64
                    || Convert.FromBase64String(credential.SaltBase64).Length is < 16 or > 64
                    || Convert.FromBase64String(credential.HashBase64).Length != credential.KeySize)
                    throw new InvalidDataException("认证配置无效，请从备份恢复。");
            }
            catch (FormatException ex) { throw new InvalidDataException("认证配置编码无效。", ex); }
            catch (ArgumentNullException ex) { throw new InvalidDataException("认证配置缺少字段。", ex); }
        }
        if (!Enum.IsDefined(settings.CloseWindowAction) || settings.ForceCloseCountdownSeconds is < 5 or > 3600
            || settings.FirstReminderThresholdPercent <= 0
            || settings.FirstReminderThresholdPercent >= settings.SecondReminderThresholdPercent
            || settings.SecondReminderThresholdPercent >= settings.FinalReminderThresholdPercent
            || settings.FinalReminderThresholdPercent >= 100
            || (settings.RequireAuthenticationForSensitiveActions && settings.AuthenticationCredential is null))
            throw new InvalidDataException("设置无效：请检查提醒阈值、倒计时及认证配置。原文件不会被覆盖。");
    }

    private void Notify(RuntimeLimitCheckResult result)
    {
        foreach (var decision in result.Decisions)
        {
            var name = string.IsNullOrWhiteSpace(decision.Evaluation.Rule.Name) ? "游戏限额" : decision.Evaluation.Rule.Name;
            if (decision.Reminder.ShouldNotify)
                Raise(new("SteamWatch 限额提醒", $"{name} 已使用 {decision.Evaluation.UsedMinutes}/{decision.Evaluation.Rule.MaxMinutes} 分钟。", decision.Reminder.Level >= ReminderLevel.Exceeded ? NotificationSeverity.Critical : NotificationSeverity.Warning, _settings.PlayReminderSounds));
            if (decision.ForceClose.State == ForceCloseCountdownState.Waiting && decision.ForceClose.Deadline is { } deadline
                && _announcedDeadlines.GetValueOrDefault(decision.RuleId) != deadline)
            {
                _announcedDeadlines[decision.RuleId] = deadline;
                Raise(new("SteamWatch 强退倒计时", $"{name} 将在 {Math.Max(0, (int)(deadline - PolicyNow).TotalSeconds)} 秒后尝试关闭游戏，请保存进度。", NotificationSeverity.Critical, _settings.PlayReminderSounds));
            }
        }
    }

    private void Raise(NotificationMessage message)
    {
        try { _notifications.Show(message); }
        catch (Exception ex) { _diagnostics?.Invoke("notification", ex); _status += $" 系统通知不可用（{ex.GetType().Name}），请查看程序内提醒。"; }
        UserNotificationRaised?.Invoke(this, message);
    }

    private RuntimeSnapshot Publish()
    {
        var today = DateOnly.FromDateTime(_clock.Now.DateTime);
        var ledgerSnapshot = _ledger.CreateReadSnapshot();
        var records = ledgerSnapshot.Records;
        var book = new PlaytimeRecordBook(records);
        var games = (_catalog?.Games ?? []).Select(g => new RuntimeGame(g, _sessions.ContainsKey(g.AppId),
            book.GetGameMinutes(today, g.AppId), book.GetWeekGameMinutes(today, g.AppId), _categories.GetValueOrDefault(g.AppId))).ToList();
        var summary = _lastLimits.Decisions.Count == 0 ? "尚无生效规则（估算补记不计入限额）" : string.Join("；", _lastLimits.Decisions.Select(d =>
            $"{d.Evaluation.Rule.Name} {d.Evaluation.UsedMinutes}/{d.Evaluation.Rule.MaxMinutes} 分钟，剩余 {d.Evaluation.RemainingMinutes} 分钟"
            + (d.ForceClose.Deadline is { } deadline ? $"，强退剩余 {Math.Max(0,(int)(deadline-PolicyNow).TotalSeconds)} 秒" : "")));
        var earliest = _lastLimits.Decisions.Where(d => d.ForceClose.Deadline.HasValue
            && d.ForceClose.State is ForceCloseCountdownState.Waiting or ForceCloseCountdownState.ReadyToClose)
            .OrderBy(d => d.ForceClose.Deadline).FirstOrDefault();
        var earliestText = _paused ? "限制已暂停，暂无强退倒计时"
            : _sessions.Count == 0 ? "无运行目标，暂无强退倒计时"
            : earliest is null ? "暂无生效的强退倒计时（仅提醒或尚未超限）"
            : DescribeEarliestClose(earliest);
        if (_reconciliationNotice is not null) _status = _status.Replace(" " + _reconciliationNotice, "") + " " + _reconciliationNotice;
        _current = new(_catalog?.SteamPath ?? "", _catalog?.UserId, _status, games, records, _rules.ToList(), _settings,
            _paused, summary, ledgerSnapshot.GameNames, ledgerSnapshot.EstimatedRecords, ledgerSnapshot.EstimatedEntries, earliestText);
        return _current;
    }

    private string DescribeEarliestClose(RuntimeLimitDecision decision)
    {
        var remaining = TimeSpan.FromTicks(Math.Max(0, (decision.ForceClose.Deadline!.Value - PolicyNow).Ticks));
        var rule = decision.Evaluation.Rule;
        var target = rule.Scope == LimitScope.Global ? "全部运行游戏"
            : _ledger.GetGameName(rule.AppId!.Value) ?? $"AppID {rule.AppId}";
        var name = string.IsNullOrWhiteSpace(rule.Name) ? "游戏限额" : rule.Name;
        return $"最早强退：{target} · {name}\n剩余 {Math.Ceiling(remaining.TotalSeconds):0} 秒，预计 {_clock.Now.Add(remaining):HH:mm:ss}（休眠顺延）";
    }

    private static IEnumerable<RunningSteamGame> GetCloseTargets(RuntimeLimitCheckResult result, IReadOnlyList<RunningSteamGame> running) =>
        result.Decisions.Where(d => d.ForceClose.ShouldCloseNow).SelectMany(d =>
            running.Where(g => d.Evaluation.Rule.Scope == LimitScope.Global || d.Evaluation.Rule.AppId == g.AppId));

    private async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await action().ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException) { _diagnostics?.Invoke("runtime", ex); throw; }
        finally { _gate.Release(); }
    }

    private sealed record Session(RunningSteamGame Game, DateTimeOffset WallTime, long ActiveTicks);
}
