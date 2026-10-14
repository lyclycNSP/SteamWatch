using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SteamWatch.App.Presentation;
using SteamWatch.Core.Limits;

namespace SteamWatch.App.Views;

public sealed partial class GamePage
{
    private bool _isEditingRule;
    public bool IsRuleEditorAvailable => !_isEditingRule;
    public bool IsEditingRule => _isEditingRule;
    private bool _compactBudgetSummary;

    private async void SetLimitForSelectedButton_Click(object sender, RoutedEventArgs e) =>
        await EditRuleAsync(null, GetActionGame(sender));

    private async void ForceClosePolicyButton_Click(object sender, RoutedEventArgs e) =>
        await EditRuleAsync(null, GetActionGame(sender), forceClose: true);

    private async void EditDailyGlobalLimit_Click(object sender, RoutedEventArgs e) => await EditGlobalRuleAsync(LimitPeriod.Day);
    private async void EditWeeklyGlobalLimit_Click(object sender, RoutedEventArgs e) => await EditGlobalRuleAsync(LimitPeriod.Week);

    private Task EditGlobalRuleAsync(LimitPeriod period) => EditRuleAsync(
        _appService.GetLimitRules().FirstOrDefault(rule => rule.Scope == LimitScope.Global && rule.Period == period),
        globalPeriod: period);

    private GameRowViewModel? GetActionGame(object sender) =>
        sender is FrameworkElement { Tag: GameRowViewModel game } ? game : GamesListView.SelectedItem as GameRowViewModel;

    private void GamesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUpdatingRows) ViewModel.SelectGame((GamesListView.SelectedItem as GameRowViewModel)?.AppId);
    }
    private void GameRow_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GameRowViewModel game }) GamesListView.SelectedItem = game;
    }
    private async void GameRow_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GameRowViewModel game }) return;
        e.Handled = true;
        GamesListView.SelectedItem = game;
        await EditRuleAsync(null, game);
    }

    private Task EditRuleAsync(LimitRule? original, GameRowViewModel? game = null, LimitPeriod? globalPeriod = null, bool forceClose = false) =>
        RunOperationAsync(() => EditRuleCoreAsync(original, game, globalPeriod, forceClose));

    private async Task EditRuleCoreAsync(LimitRule? original, GameRowViewModel? game, LimitPeriod? globalPeriod, bool forceClose)
    {
        if (_isEditingRule) return;
        SetProperty(ref _isEditingRule, true, nameof(IsRuleEditorAvailable));
        try
        {
            var editor = new LimitRuleEditorViewModel(_appService.GetLimitRules, original,
                game is null ? null : new RuleTarget(game.AppId, game.Name), globalPeriod, forceClose);
            var isGlobal = editor.IsGlobal;
            var targets = _allGameRows.ToList();
            if (original is { Scope: LimitScope.Game, AppId: int orphanId } && targets.All(row => row.AppId != orphanId))
                targets.Add(new GameRowViewModel(orphanId, original.Name, 0, "未运行", "", ""));
            var target = new ComboBox { Header = "目标游戏", ItemsSource = targets, DisplayMemberPath = "Name",
                SelectedItem = targets.FirstOrDefault(row => row.AppId == (original?.AppId ?? game?.AppId)),
                IsEnabled = original is null && game is null, HorizontalAlignment = HorizontalAlignment.Stretch };
            var period = new ComboBox { Header = "周期", ItemsSource = new[] { "每日", "每周" },
                SelectedIndex = (globalPeriod ?? original?.Period ?? LimitPeriod.Day) == LimitPeriod.Day ? 0 : 1,
                IsEnabled = !isGlobal, HorizontalAlignment = HorizontalAlignment.Stretch };
            var minutes = new NumberBox { Header = "限额分钟", Minimum = 1, Maximum = 10080,
                Value = original?.MaxMinutes ?? 60, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            var enforcement = new ComboBox { Header = "超限策略", ItemsSource = new[] { "仅提醒", "强制退出" },
                SelectedIndex = forceClose || original?.Enforcement == EnforcementMode.ForceClose ? 1 : 0,
                HorizontalAlignment = HorizontalAlignment.Stretch };
            var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var content = new StackPanel { Spacing = 12, MinWidth = 320, MaxWidth = 440 };
            content.Children.Add(new TextBlock { Text = isGlobal ? "全部游戏共用此额度，单游戏规则同时生效。" : "切换每日／每周会加载对应的已保存规则。此规则与全部游戏总限额同时生效。", TextWrapping = TextWrapping.Wrap });
            if (!isGlobal) content.Children.Add(target);
            content.Children.Add(period); content.Children.Add(minutes); content.Children.Add(enforcement); content.Children.Add(error);
            var dialog = new ContentDialog { XamlRoot = XamlRoot,
                Title = isGlobal ? (period.SelectedIndex == 0 ? "每日总限额" : "每周总限额") : original is null ? "新建单游戏规则" : $"编辑规则：{original.Name}",
                Content = content, PrimaryButtonText = "保存规则", SecondaryButtonText = original is null ? "" : "删除此规则",
                CloseButtonText = "取消", DefaultButton = ContentDialogButton.Primary };
            // The game library owns both periods. Changing the period loads its existing rule.
            if (!isGlobal)
            {
                void LoadGameRule()
                {
                    var selected = target.SelectedItem as GameRowViewModel;
                    editor.SelectGame(selected is null ? null : new RuleTarget(selected.AppId, selected.Name),
                        period.SelectedIndex == 0 ? LimitPeriod.Day : LimitPeriod.Week);
                    original = editor.Original;
                    minutes.Value = editor.Minutes;
                    enforcement.SelectedIndex = editor.Enforcement == EnforcementMode.ForceClose ? 1 : 0;
                    dialog.Title = selected is null ? "设置单游戏限额" : $"{selected.Name} · 限额设置";
                    dialog.SecondaryButtonText = original is null ? "" : "删除此规则";
                    error.Text = string.Empty;
                }
                period.SelectionChanged += (_, _) => LoadGameRule();
                target.SelectionChanged += (_, _) => LoadGameRule();
                LoadGameRule();
            }
            LimitRule? candidate = null;
            dialog.PrimaryButtonClick += (_, args) =>
            {
                try
                {
                    candidate = editor.CreateCandidate(minutes.Value,
                        enforcement.SelectedIndex == 0 ? EnforcementMode.NotifyOnly : EnforcementMode.ForceClose);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                { error.Text = ex.Message; args.Cancel = true; }

            };
            while (true)
            {
                var result = await ShowDialogAsync(dialog);
                if (result == ContentDialogResult.Secondary && original is not null)
                { await DeleteRuleAsync(original); return; }
                if (result != ContentDialogResult.Primary) return;
                if (candidate is null) continue;
                if (!await AuthorizeSensitiveActionAsync("保存限额规则"))
                { error.Text = "认证未通过或已取消，规则未保存。草稿已保留。"; continue; }
                // Candidate and original were captured before authentication; selection changes cannot retarget this command.
                if (editor.HasConflict(candidate))
                { error.Text = "规则已发生变化，请重新检查目标和周期。"; continue; }
                try
                {
                    _ = original is null ? await _appService.UpsertLimitRuleAsync(candidate)
                        : await _appService.ReplaceLimitRuleAsync(original, candidate);
                    StatusMessage = "限额规则已保存。";
                    return;
                }
                catch (Exception ex) { error.Text = $"保存失败：{ex.Message}。草稿已保留，可重试。"; }
            }
        }
        catch (Exception ex) { StatusMessage = $"无法编辑限额：{ex.Message}"; }
        finally { SetProperty(ref _isEditingRule, false, nameof(IsRuleEditorAvailable)); }
    }

    private async Task DeleteRuleAsync(LimitRule rule)
    {
        try
        {
            var confirm = new ContentDialog { XamlRoot = XamlRoot, Title = "删除限额规则？",
                Content = new LimitRuleRowViewModel(rule).SummaryText, PrimaryButtonText = "删除规则", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
            if (await ShowDialogAsync(confirm) != ContentDialogResult.Primary || !await AuthorizeSensitiveActionAsync("删除限额规则")) return;
            await _appService.DeleteLimitRuleAsync(rule);
            StatusMessage = "限额规则已删除。";
        }
        catch (Exception ex) { StatusMessage = $"删除限额失败：{ex.Message}"; }
    }

    private void RefreshBudgetSummary()
    {
        if (_readModel is null) return;
        var budgets = _readModel.Budgets;
        DailyBudgetUsage.Text = budgets.Day.Usage; DailyBudgetDetail.Text = budgets.Day.Detail; DailyBudgetProgress.Value = budgets.Day.Progress;
        WeeklyBudgetUsage.Text = budgets.Week.Usage; WeeklyBudgetDetail.Text = budgets.Week.Detail; WeeklyBudgetProgress.Value = budgets.Week.Progress;
        DailyBudgetProgress.Visibility = budgets.Day.IsConfigured ? Visibility.Visible : Visibility.Collapsed;
        WeeklyBudgetProgress.Visibility = budgets.Week.IsConfigured ? Visibility.Visible : Visibility.Collapsed;
        DailyBudgetDetail.Visibility = !_compactBudgetSummary || budgets.Day.IsConfigured ? Visibility.Visible : Visibility.Collapsed;
        WeeklyBudgetDetail.Visibility = !_compactBudgetSummary || budgets.Week.IsConfigured ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void GameDetails_Click(object sender, RoutedEventArgs e)
    {
        if (GetActionGame(sender) is not GameRowViewModel game) { StatusMessage = "请先选择条目。"; return; }
        var rules = (_readModel?.Source.Rules ?? []).Where(rule => rule.IsEnabled && (rule.Scope == LimitScope.Global || rule.AppId == game.AppId));
        var content = new StackPanel { Spacing = 12, MinWidth = 320, MaxWidth = 440 };
        foreach (var line in new[] { game.AppIdText, game.CategoryText, game.SteamPlaytimeText,
            $"今日实测 {game.TodayMinutes} 分钟 · 本周实测 {game.WeekMinutes} 分钟" })
            content.Children.Add(new TextBlock { Text = line, LineHeight = 24, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = "适用规则", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        var descriptions = rules.Select(rule => new LimitRuleRowViewModel(rule)).Select(row => $"{row.SummaryText} · {row.EnforcementText}").ToList();
        foreach (var line in descriptions.Count > 0 ? descriptions : new List<string> { "暂无适用规则。" })
            content.Children.Add(new TextBlock { Text = line, LineHeight = 24, TextWrapping = TextWrapping.Wrap });
        await ShowDialogAsync(new ContentDialog { XamlRoot = XamlRoot, Title = game.Name,
            Content = new ScrollViewer { Content = content, MaxHeight = 440, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, CloseButtonText = "关闭" });
    }

    public void UpdateViewport(double width, double height)
    {
        if (ForceCloseCard is null) return;
        var narrow = width < 720 || height < 600;
        _compactBudgetSummary = narrow;
        DailyBudgetCard.Padding = WeeklyBudgetCard.Padding = ForceCloseCard.Padding = new Thickness(narrow ? 8 : 16, 4, narrow ? 8 : 16, 4);
        DailyBudgetUsage.FontSize = WeeklyBudgetUsage.FontSize = narrow ? 18 : 22;
        EarliestForceCloseText.FontSize = narrow ? 14 : 18;
        ForceCloseHint.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        RefreshBudgetSummary();
    }
}
