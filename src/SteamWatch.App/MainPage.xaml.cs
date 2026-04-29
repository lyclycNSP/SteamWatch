using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SteamWatch.App;

public sealed partial class MainPage : Page
{
    public IReadOnlyList<GameRow> GameRows { get; } =
    [
        new("等待 Steam", "监控服务尚未连接", "今日 0 分钟", "本周 0 分钟")
    ];

    public MainPage()
    {
        InitializeComponent();
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ShowSettings();
            return;
        }

        if (args.SelectedItemContainer?.Tag is not string tag)
        {
            return;
        }

        ShowMainContent(tag);
    }

    private void ShowMainContent(string tag)
    {
        SettingsView.Visibility = Visibility.Collapsed;
        GamesView.Visibility = Visibility.Visible;

        PageTitle.Text = tag switch
        {
            "Statistics" => "统计",
            "ActivityLog" => "日志",
            _ => "游戏"
        };
        PageSubtitle.Text = "今日 0 分钟 / 本周 0 分钟";
    }

    private void ShowSettings()
    {
        GamesView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Visible;
        PageTitle.Text = "设置";
        PageSubtitle.Text = "托盘、提醒和自启动";
    }
}

public sealed record GameRow(string Name, string Status, string TodayText, string WeekText);
