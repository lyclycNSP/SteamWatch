using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using SteamWatch.App.Presentation;

namespace SteamWatch.App.Controls;

public sealed partial class WeeklyTrendView : UserControl
{
    private IReadOnlyList<DayStatisticsPresentation> _days = [];
    public event EventHandler? ValuesRequested;

    public WeeklyTrendView() => InitializeComponent();

    public void SetDays(IReadOnlyList<DayStatisticsPresentation> days)
    {
        if (_days.SequenceEqual(days)) return;
        _days = days.ToArray();
        ChartGrid.Children.Clear();
        ChartGrid.ColumnDefinitions.Clear();
        var maximum = Math.Max(1, days.Select(day => day.TotalMinutes).DefaultIfEmpty(0).Max());
        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            ChartGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var column = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 4 };
            AutomationProperties.SetName(column, day.AccessibleSummary);
            ToolTipService.SetToolTip(column, day.AccessibleSummary);
            column.Children.Add(new TextBlock { Text = day.TotalMinutes.ToString(), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center });
            var bottom = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            var estimated = new Border { Height = 60d * day.EstimatedMinutes / maximum, Style = (Style)Resources["EstimatedBarStyle"] };
            var measured = new Border { Height = 60d * day.MeasuredMinutes / maximum, Style = (Style)Resources["MeasuredBarStyle"] };
            if (day.EstimatedMinutes > 0) bottom.Children.Add(estimated);
            if (day.MeasuredMinutes > 0) bottom.Children.Add(measured);
            // A Grid anchors short bars to the same baseline rather than stretching them.
            var frame = new Grid { Height = 60, Width = 24 };
            frame.Children.Add(bottom);
            column.Children.Add(frame);
            column.Children.Add(new TextBlock { Text = day.DateText, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center });
            Grid.SetColumn(column, index);
            ChartGrid.Children.Add(column);
        }
        ChartDescription.Text = days.All(day => day.TotalMinutes == 0)
            ? "这一周暂无记录。单位：分钟；未补造历史记录。"
            : "单位：分钟 · 实心为实测，描边为估算 · 估算不参与限额";
    }

    private void ShowValues_Click(object sender, RoutedEventArgs e) => ValuesRequested?.Invoke(this, EventArgs.Empty);
}
