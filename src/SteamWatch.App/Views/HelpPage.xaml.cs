using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.App.Views;

public sealed partial class HelpPage : UserControl
{
    public event EventHandler<string>? SettingsRequested;

    public HelpPage()
    {
        InitializeComponent();
        VersionText.Text = $"版本 {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "未知"}";
        var installed = InstalledApplication.IsInstalled();
        ModeText.Text = installed ? "安装版 · 数据独立保存在当前用户目录" : "绿色版 · 数据保存在解压根目录 data";
    }

    private void HelpView_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (HelpContent is not null) HelpContent.Width = Math.Min(760, args.NewSize.Width);
    }

    private void SettingsLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string section }) SettingsRequested?.Invoke(this, section);
    }
}
