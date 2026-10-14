using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SteamWatch.App.Presentation;
using SteamWatch.App.Services;

namespace SteamWatch.App.Views;

public sealed partial class StatisticsPage : UserControl
{
    private SteamWatchAppService? _appService;
    private Func<ContentDialog, Task<ContentDialogResult>>? _showDialog;
    private RuntimeReadModel? _readModel;
    private bool _isInitializing = true;
    public StatisticsViewModel ViewModel { get; } = new(DateOnly.FromDateTime(DateTime.Now));
    public ObservableCollection<PlaytimeStatRowViewModel> DailyStatRows { get; } = [];
    public ObservableCollection<PlaytimeStatRowViewModel> WeeklyStatRows { get; } = [];
    public event EventHandler<string>? SubtitleChanged;
    public event EventHandler<DateOnly>? EstimatedDetailsRequested;

    public StatisticsPage()
    {
        InitializeComponent();
        DailyStatsDatePicker.Date = new DateTimeOffset(ViewModel.DailyDate.ToDateTime(TimeOnly.MinValue));
        WeeklyStatsDatePicker.Date = new DateTimeOffset(ViewModel.WeeklyDate.ToDateTime(TimeOnly.MinValue));
        _isInitializing = false;
    }

    public void Configure(SteamWatchAppService service, Func<ContentDialog, Task<ContentDialogResult>> showDialog)
    { _appService = service; _showDialog = showDialog; }

    private Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog) =>
        (_showDialog ?? throw new InvalidOperationException("统计页尚未连接对话框宿主。"))(dialog);

    private void DailyStatisticsTab_Click(object sender, RoutedEventArgs e) => ShowStatisticsPeriod(false);
    private void WeeklyStatisticsTab_Click(object sender, RoutedEventArgs e) => ShowStatisticsPeriod(true);
    private void ShowStatisticsPeriod(bool weekly)
    {
        ViewModel.SelectPeriod(weekly);
        DailyStatisticsTab.IsChecked = !weekly;
        WeeklyStatisticsTab.IsChecked = weekly;
        DailyStatisticsPanel.Visibility = weekly ? Visibility.Collapsed : Visibility.Visible;
        WeeklyStatisticsPanel.Visibility = weekly ? Visibility.Visible : Visibility.Collapsed;
        Refresh();
        SubtitleChanged?.Invoke(this, ViewModel.Subtitle);
    }

    private void EstimatedDetailsButton_Click(object sender, RoutedEventArgs e) =>
        EstimatedDetailsRequested?.Invoke(this, ViewModel.DailyDate);

    private void DailyStatsDatePicker_DateChanged(object sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_isInitializing) return;
        ViewModel.SelectDailyDate(GetSelectedDate(DailyStatsDatePicker));
        Refresh();
    }

    private void WeeklyStatsDatePicker_DateChanged(object sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_isInitializing) return;
        ViewModel.SelectWeeklyDate(GetSelectedDate(WeeklyStatsDatePicker));
        Refresh();
    }

    public void Refresh(RuntimeReadModel? model = null)
    {
        _readModel = model ?? _readModel ?? _appService?.CaptureReadModel();
        if (_readModel is null) return;
        ReplaceStatRows(DailyStatRows, MapRows(_readModel.DailyStats(ViewModel.DailyDate), $"{ViewModel.DailyDate:yyyy-MM-dd}"));
        ReplaceStatRows(WeeklyStatRows, MapRows(_readModel.WeeklyStats(ViewModel.WeekStart), $"{ViewModel.WeekStart:yyyy-MM-dd} - {ViewModel.WeekEnd:yyyy-MM-dd}"));
        ViewModel.UpdateTotals(Totals(DailyStatRows), Totals(WeeklyStatRows));
        DailyStatsTotalText.Text = ViewModel.DailySummary;
        WeeklyStatsTotalText.Text = ViewModel.WeeklySummary;
        DailyStatsBreakdown.Text = ViewModel.DailyTotals.Breakdown;
        WeeklyStatsBreakdown.Text = ViewModel.WeeklyTotals.Breakdown;
        DailyStatsEmpty.Visibility = ViewModel.DailyTotals.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        WeeklyStatsEmpty.Visibility = ViewModel.WeeklyTotals.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        WeeklyTrend.SetDays(_readModel.WeekStatistics(ViewModel.WeekStart));
    }

    private static IEnumerable<PlaytimeStatRowViewModel> MapRows(IEnumerable<StatisticsRow> rows, string period) =>
        rows.Select(row => new PlaytimeStatRowViewModel(period, row.AppId, row.Name, row.TotalMinutes, row.IconPath)
            { EstimatedMinutes = row.EstimatedMinutes });

    private static StatisticsTotals Totals(IEnumerable<PlaytimeStatRowViewModel> rows) =>
        new(rows.Sum(row => row.MeasuredMinutes), rows.Sum(row => row.EstimatedMinutes), rows.Count());

    private static DateOnly GetSelectedDate(CalendarDatePicker picker) =>
        DateOnly.FromDateTime((picker.Date ?? DateTimeOffset.Now).LocalDateTime);

    private static void ReplaceStatRows(ObservableCollection<PlaytimeStatRowViewModel> target,
        IEnumerable<PlaytimeStatRowViewModel> source)
    {
        target.Clear();
        foreach (var row in source) target.Add(row);
    }

    private async void WeeklyTrend_ValuesRequested(object? sender, EventArgs e)
    {
        var rows = _readModel!.WeekStatistics(ViewModel.WeekStart);
        Grid ValueRow(params string[] values)
        {
            var row = new Grid { ColumnSpacing = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            for (var column = 1; column < 4; column++)
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 48 });
            for (var column = 0; column < values.Length; column++)
            {
                var text = DialogText.Body(values[column]);
                text.HorizontalAlignment = column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                Grid.SetColumn(text, column);
                row.Children.Add(text);
            }
            return row;
        }
        var content = new StackPanel { Spacing = 12, MinWidth = 320, MaxWidth = 480,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        var header = ValueRow("日期", "实测", "估算", "合计");
        for (var column = 0; column < header.Children.Count; column++)
        {
            ((TextBlock)header.Children[column]).FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(header.Children[column], $"DailyValueHeader-{column}");
        }
        content.Children.Add(header);
        var values = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var day in rows)
        {
            var row = ValueRow($"{day.Date:yyyy-MM-dd}", day.MeasuredMinutes.ToString(), day.EstimatedMinutes.ToString(), day.TotalMinutes.ToString());
            // Keep a complete accessible summary while presenting actual aligned cells.
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(row.Children[0], day.AccessibleSummary);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(row.Children[0], $"DailyValue-{day.Date:yyyy-MM-dd}");
            for (var column = 1; column < row.Children.Count; column++)
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(row.Children[column], $"DailyValueCell-{day.Date:yyyy-MM-dd}-{column}");
            values.Children.Add(row);
        }
        content.Children.Add(new ScrollViewer { MaxHeight = 360, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = values });
        await ShowDialogAsync(new ContentDialog
        {
            XamlRoot = XamlRoot, Title = "每日数值 · 分钟", CloseButtonText = "关闭",
            Content = content
        });
    }

}
