using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SteamWatch.App.Services;
using SteamWatch.App.Presentation;
using SteamWatch.Core.Limits;
using SteamWatch.Core.Steam;

namespace SteamWatch.App.Views;

public sealed partial class GamePage : UserControl, INotifyPropertyChanged
{
    private SteamWatchAppService _appService = null!;
    private Func<string, Task<bool>>? _authorize;
    private Func<ContentDialog, Task<ContentDialogResult>>? _showDialog;
    private Func<Func<Task>, Task>? _runOperation;
    private IReadOnlyList<GameRowViewModel> _allGameRows = [];
    private string _statusMessage = "等待读取 Steam 缓存。";
    private bool _isUpdatingRows;
    private RuntimeReadModel? _readModel;
    public GameLibraryViewModel ViewModel { get; } = new();
    public ObservableCollection<GameRowViewModel> GameRows { get; } = [];
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<string>? StatusChanged;
    public string StatusMessage
    {
        get => _statusMessage;
        private set { ShowStatus(value); StatusChanged?.Invoke(this, value); }
    }
    public string Subtitle => $"游戏 {_allGameRows.Count} 个 · {(_readModel?.Source.IsPaused == true ? "限制已暂停 · 仍在计时" : "限制已启用")}";

    public GamePage() => InitializeComponent();
    public void Configure(SteamWatchAppService service, Func<string, Task<bool>> authorize,
        Func<ContentDialog, Task<ContentDialogResult>> showDialog, Func<Func<Task>, Task> runOperation)
    { _appService = service; _authorize = authorize; _showDialog = showDialog; _runOperation = runOperation; }
    private Task<bool> AuthorizeSensitiveActionAsync(string action) =>
        (_authorize ?? throw new InvalidOperationException("游戏页尚未连接认证宿主。"))(action);
    private Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog) =>
        (_showDialog ?? throw new InvalidOperationException("游戏页尚未连接对话框宿主。"))(dialog);
    private Task RunOperationAsync(Func<Task> action) =>
        (_runOperation ?? throw new InvalidOperationException("游戏页尚未连接命令宿主。"))(action);
    public void ShowStatus(string value)
    {
        SetProperty(ref _statusMessage, value, nameof(StatusMessage));
        if (StatusFeedback is not null)
            StatusFeedback.Visibility = value.StartsWith("监控中：", StringComparison.Ordinal) ? Visibility.Collapsed : Visibility.Visible;
    }
    public void ApplySnapshot(RuntimeReadModel model)
    {
        _readModel = model;
        ReplaceGameRows(SteamWatchAppService.MapGames(model));
        EarliestForceCloseText.Text = model.Source.EarliestForceCloseSummary;
        RefreshBudgetSummary();
    }
    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public void ReplaceGameRows(IEnumerable<GameRowViewModel> games)
    {
        _allGameRows = games.ToList();
        ApplyGameFilter();
    }

    private void GameSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyGameFilter();
    private void GameFilter_Changed(object sender, RoutedEventArgs e) => ApplyGameFilter();
    private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyGameFilter();

    private async void SetGameCategoryButton_Click(object sender, RoutedEventArgs e)
        => await RunOperationAsync(() => SetGameCategoryAsync(sender));

    private async Task SetGameCategoryAsync(object sender)
    {
        try
        {
            if (GetActionGame(sender) is not GameRowViewModel game)
                throw new InvalidOperationException("请先选择游戏或工具条目。");
            var category = new ComboBox { Header = "分类依据：用户明确设置", SelectedIndex = (int)game.Category,
                ItemsSource = new[] { "未知", "游戏", "运行库／工具" } };
            var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = $"分类：{game.Name}", Content = category,
                PrimaryButtonText = "保存分类", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
            if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary) return;
            if (!await AuthorizeSensitiveActionAsync("设置条目分类")) return;
            await _appService.SetGameCategoryAsync(game.AppId, (SteamAppCategory)category.SelectedIndex);
        }
        catch (Exception ex) { StatusMessage = $"分类未保存：{ex.Message}"; }
    }

    private void ApplyGameFilter()
    {
        if (GameSearchTextBox is null || GamesListView is null) return;
        if (_appService is null) return;
        ViewModel.SearchText = GameSearchTextBox.Text;
        ViewModel.RunningOnly = RunningOnlyCheckBox.IsChecked == true;
        ViewModel.RulesOnly = RulesOnlyCheckBox.IsChecked == true;
        ViewModel.Category = CategoryFilterComboBox?.SelectedIndex is not (> 0) ? null
            : CategoryFilterComboBox.SelectedIndex == 3 ? SteamAppCategory.Unknown : (SteamAppCategory)CategoryFilterComboBox.SelectedIndex;
        var ruleTargets = (_readModel?.Source.Rules ?? []).Where(rule => rule.Scope == LimitScope.Game).Select(rule => rule.AppId).ToHashSet();
        UpdateGameRows(_allGameRows.Where(game => ViewModel.Matches(game.AppId, game.Name,
            game.Status == "运行中", game.Category, ruleTargets.Contains(game.AppId))));
    }

    private void UpdateGameRows(IEnumerable<GameRowViewModel> games)
    {
        _isUpdatingRows = true;
        try
        {
            var nextRows = games.ToList();
            var existingByAppId = GameRows.ToDictionary(game => game.AppId);

            for (var index = 0; index < nextRows.Count; index++)
            {
                var incoming = nextRows[index];
                var row = existingByAppId.TryGetValue(incoming.AppId, out var existing)
                    ? existing
                    : incoming;

                if (!ReferenceEquals(row, incoming))
                {
                    row.UpdateFrom(incoming);
                }

                if (index >= GameRows.Count)
                {
                    GameRows.Add(row);
                    continue;
                }

                if (ReferenceEquals(GameRows[index], row))
                {
                    continue;
                }

                var existingIndex = GameRows.IndexOf(row);
                if (existingIndex >= 0)
                {
                    GameRows.Move(existingIndex, index);
                }
                else
                {
                    GameRows.Insert(index, row);
                }
            }

            EmptyGamesText.Visibility = nextRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            while (GameRows.Count > nextRows.Count)
            {
                GameRows.RemoveAt(GameRows.Count - 1);
            }

            var selectedAppId = ViewModel.ReconcileSelection(nextRows.Select(row => row.AppId));
            GamesListView.SelectedItem = GameRows.FirstOrDefault(game => game.AppId == selectedAppId);
        }
        finally { _isUpdatingRows = false; }
    }

}
