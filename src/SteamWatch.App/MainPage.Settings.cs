using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.App;

public sealed partial class MainPage
{
    private async void HelpEditor_SettingsRequested(object? sender, string section)
    {
        NavigateToSettings();
        if (Equals(_currentNavigationItem, RootNavigation.SettingsItem))
            await SettingsEditor.OpenCategoryAsync(section);
    }

    private async void SettingsEditor_MaintenanceRequested(object? sender, string action)
    {
        try
        {
            // Uninstall enters the exit coordinator itself; it must not own a command lease.
            if (action == "Uninstall") { await UninstallAsync(); return; }
            await RunUserOperationAsync(async () =>
            {
                switch (action)
                {
                    case "ExportBackup": await ExportBackupAsync(); break;
                    case "RestoreBackup": await RestoreBackupAsync(); break;
                    case "OpenDataFolder": OpenDataFolder(); break;
                    case "ExportDiagnostics": await ExportDiagnosticsAsync(); break;
                }
            });
        }
        catch (Exception ex)
        {
            _appService.RecordDiagnostic("settings-maintenance", ex);
            SettingsEditor.ShowMessage("无法完成维护操作，请重试或导出诊断包。", InfoBarSeverity.Error);
        }
    }

    private async Task ExportBackupAsync()
    {
        try
        {
            if (!await AuthorizeSensitiveActionAsync("导出数据备份")) return;
            var picker = new Windows.Storage.Pickers.FileSavePicker { SuggestedFileName = $"SteamWatch-backup-{DateTime.Now:yyyyMMdd}" };
            picker.FileTypeChoices.Add("SteamWatch 数据备份", new List<string> { ".json" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            var backup = await _appService.CreateBackupAsync();
            await File.WriteAllTextAsync(file.Path, backup);
            SettingsEditor.ShowMessage("已导出完整备份（包含设置、认证配置、规则和时长）。", InfoBarSeverity.Success);
        }
        catch (Exception ex) { SettingsEditor.ShowMessage($"备份失败：{ex.Message}", InfoBarSeverity.Error); }
    }

    private async Task RestoreBackupAsync()
    {
        try
        {
            if (!await AuthorizeSensitiveActionAsync("恢复数据备份")) return;
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            if (new FileInfo(file.Path).Length > 64 * 1024 * 1024) throw new InvalidDataException("备份超过 64 MiB 限制。");
            var confirmation = new ContentDialog { XamlRoot = XamlRoot, Title = "恢复备份",
                Content = "将替换当前设置、认证配置、规则和时长。当前数据会保留备份；恢复后按导入规则继续监控。",
                PrimaryButtonText = "恢复", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
            if (await ShowDialogAsync(confirmation) != ContentDialogResult.Primary) return;
            await _appService.RestoreBackupAsync(await File.ReadAllTextAsync(file.Path));
            SettingsEditor.ApplySettings(_appService.GetSettings());
            SettingsEditor.ShowMessage("备份已恢复。", InfoBarSeverity.Success);
        }
        catch (Exception ex) { SettingsEditor.ShowMessage($"恢复失败：{ex.Message}", InfoBarSeverity.Error); }
    }

    private void OpenDataFolder()
    {
        try
        {
            var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            start.ArgumentList.Add(_appService.DataDirectory);
            Process.Start(start);
        }
        catch (Exception ex) { SettingsEditor.ShowMessage($"无法打开目录：{ex.Message}", InfoBarSeverity.Error); }
    }

    private async Task ExportDiagnosticsAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker { SuggestedFileName = $"SteamWatch-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}" };
            picker.FileTypeChoices.Add("诊断 ZIP", new List<string> { ".zip" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            await File.WriteAllBytesAsync(file.Path, await _appService.CreateDiagnosticsAsync());
            SettingsEditor.ShowMessage("诊断包已导出，仅包含运行摘要和脱敏错误类型。", InfoBarSeverity.Success);
        }
        catch (Exception ex) { SettingsEditor.ShowMessage($"诊断导出失败：{ex.Message}", InfoBarSeverity.Error); }
    }

    private async Task UninstallAsync()
    {
        var command = InstalledApplication.GetUninstallCommand();
        if (command is null)
        {
            SettingsEditor.ShowMessage(InstalledApplication.IsInstalled()
                ? "安装信息不完整，请重新运行安装包修复卸载入口。"
                : "绿色版无需卸载；可在备份 data 后移除程序目录。", InfoBarSeverity.Warning);
            return;
        }
        try
        {
            if (command.IsInno)
            {
                // Remain running through the confirmation. The uninstaller requests the normal
                // authenticated/save-aware exit only after the user confirms removal.
                Process.Start(command.CreateStartInfo());
                return;
            }
            if (!await PrepareForExitAsync("卸载 SteamWatch")) return;
            Process.Start(command.CreateStartInfo());
            App.ExitApplication();
        }
        catch (Exception ex)
        {
            ResumeAfterExitFailure();
            SettingsEditor.ShowMessage($"无法启动卸载：{ex.Message}", InfoBarSeverity.Error);
        }
    }

}
