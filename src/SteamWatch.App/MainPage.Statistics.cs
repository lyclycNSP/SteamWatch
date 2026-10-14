using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SteamWatch.App.Presentation;

namespace SteamWatch.App;

public sealed partial class MainPage
{
    private async void StatisticsEditor_EstimatedDetailsRequested(object? sender, DateOnly date)
        => await RunUserOperationAsync(() => ShowEstimatedDetailsAsync(date));

    private async Task ShowEstimatedDetailsAsync(DateOnly date)
    {
        try
        {
            var snapshot = _appService.CaptureReadModel().Source;
            string GameName(int id) => snapshot.GameNames.GetValueOrDefault(id) ?? $"AppID {id}";
            var entries = snapshot.EstimatedEntries.Where(entry => entry.Date == date).ToList();
            var appIds = entries.Select(entry => entry.AppId).Distinct().ToList();
            var expected = appIds.ToDictionary(id => id, id => snapshot.EstimatedRecords.FirstOrDefault(day => day.Date == date)?.GetGameMinutes(id) ?? 0);
            var game = new ComboBox { Header = "要修正的游戏", ItemsSource = appIds.Select(id => $"{GameName(id)} · AppID {id}").ToList(), SelectedIndex = 0 };
            var minutes = new NumberBox { Header = "修正后的估算分钟（不含实测）", Minimum = 0, Maximum = int.MaxValue,
                Value = appIds.Count > 0 ? expected[appIds[0]] : 0 };
            game.SelectionChanged += (_, _) =>
            {
                if (game.SelectedIndex >= 0) minutes.Value = expected[appIds[game.SelectedIndex]];
            };
            var reason = new TextBox { Header = "修正原因（必填）", MaxLength = 500, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
            var content = new StackPanel { Spacing = 12, MinWidth = 320, MaxWidth = 480 };
            content.Children.Add(new TextBlock { Text = entries.Count == 0 ? "该日期没有估算补记。" : "明细按记录顺序显示。修正只改变估算；实测和限额不受影响。", TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new ScrollViewer { MaxHeight = 220, Content = new ItemsControl
            {
                ItemsSource = entries.Select(entry =>
                {
                    var paragraphs = DialogText.Paragraphs(GameName(entry.AppId), entry.Summary, entry.Explanation);
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(paragraphs.Children[0], "EstimateEntryName");
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(paragraphs.Children[1], "EstimateEntrySummary");
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(paragraphs.Children[2], "EstimateEntryExplanation");
                    paragraphs.Margin = new Thickness(0, 0, 0, 20);
                    return paragraphs;
                }).ToList()
            } });
            if (entries.Count > 0)
            {
                content.Children.Add(game);
                content.Children.Add(minutes);
                content.Children.Add(reason);
            }
            var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = $"{date:yyyy-MM-dd} 估算补记",
                Content = new ScrollViewer { Content = content, MaxHeight = 440,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
                PrimaryButtonText = entries.Count > 0 ? "修正估算" : "", CloseButtonText = "关闭",
                DefaultButton = ContentDialogButton.Close };
            if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary) return;
            if (game.SelectedIndex < 0 || !double.IsFinite(minutes.Value) || minutes.Value != Math.Truncate(minutes.Value)
                || minutes.Value < 0 || minutes.Value > int.MaxValue || string.IsNullOrWhiteSpace(reason.Text))
                throw new ArgumentException("请选择游戏，填写非负整数分钟和修正原因。");
            var appId = appIds[game.SelectedIndex];
            var newMinutes = (int)minutes.Value;
            if (newMinutes == expected[appId]) throw new ArgumentException("估算分钟没有变化。");
            if (!await AuthorizeSensitiveActionAsync("修正估算时长")) return;
            var confirmation = new ContentDialog { XamlRoot = XamlRoot, Title = "确认修正估算",
                Content = $"{date:yyyy-MM-dd} · AppID {appId}\n估算 {expected[appId]} → {newMinutes} 分钟\n原因：{reason.Text.Trim()}\n保存前会备份当前数据。",
                PrimaryButtonText = "确认修正", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
            if (await ShowDialogAsync(confirmation) != ContentDialogResult.Primary) return;
            await _appService.CorrectEstimatedMinutesAsync(date, appId, expected[appId], newMinutes, reason.Text);
        }
        catch (Exception ex)
        {
            StatusMessage = $"估算修正失败：{ex.Message}";
            await ShowDialogAsync(new ContentDialog { XamlRoot = XamlRoot, Title = "估算修正未完成", Content = ex.Message, CloseButtonText = "知道了" });
        }
    }

}
