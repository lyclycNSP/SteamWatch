using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SteamWatch.App.Presentation;
using SteamWatch.Core.Settings;
using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.App.Views;

public sealed partial class SettingsPage : UserControl
{
    private Func<string, Task<bool>>? _authorize;
    private Func<ContentDialog, Task<ContentDialogResult>>? _showDialog;
    private bool _isApplyingSettings = true;
    private string? _section;
    private bool _generalSaveQueued;
    private readonly DispatcherTimer _feedbackTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    public SettingsEditorViewModel ViewModel { get; }
    public bool IsNavigating { get; private set; }
    public bool HasPendingSave => _generalSaveQueued || ViewModel.IsBusy;
    public event EventHandler<string>? MaintenanceRequested;
    public event EventHandler<string>? SubtitleChanged;
    public event EventHandler<string>? StatusChanged;

    public SettingsPage()
    {
        ViewModel = new(() => App.Service.GetSettings(), settings => App.Service.SaveSettingsAsync(settings),
            action => _authorize?.Invoke(action) ?? Task.FromResult(false), ex => App.Service.RecordDiagnostic("settings-save", ex));
        InitializeComponent();
        _isApplyingSettings = false;
        _feedbackTimer.Tick += (_, _) => { _feedbackTimer.Stop(); SettingsSuccessMessage.Visibility = Visibility.Collapsed; };
        Unloaded += (_, _) => _feedbackTimer.Stop();
        var installed = InstalledApplication.GetUninstallCommand() is not null;
        UninstallButton.Visibility = installed ? Visibility.Visible : Visibility.Collapsed;
        PortableRemovalHint.Visibility = installed ? Visibility.Collapsed : Visibility.Visible;
        if (!installed && InstalledApplication.IsInstalled())
            PortableRemovalHint.Text = "安装信息不完整。请重新运行安装包修复卸载入口，现有数据会保留。";
        UpdateAuthenticationFields();
        ViewModel.PropertyChanged += (_, _) =>
        {
            SettingsView.IsEnabled = !ViewModel.IsBusy;
            UpdateDraftNotice();
            UpdateAuthenticationFields();
        };
    }

    public void Configure(Func<string, Task<bool>> authorize, Func<ContentDialog, Task<ContentDialogResult>> showDialog)
    { _authorize = authorize; _showDialog = showDialog; }

    private void SettingsView_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        // ActualWidth bindings can retain the old content width after a window resize.
        if (SettingsContent is not null) SettingsContent.Width = Math.Min(760, args.NewSize.Width);
    }

    private bool HasDraft => _section switch
    {
        "Reminder" => ViewModel.HasReminderDraft,
        "Authentication" => ViewModel.HasAuthenticationDraft,
        _ => false
    };

    private async void SettingsSectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string section }) await NavigateAsync(section, sender as Control);
    }
    private async void SettingsBackButton_Click(object sender, RoutedEventArgs e) => await NavigateAsync(null, sender as Control);
    private async Task NavigateAsync(string? section, Control? source)
    {
        if (IsNavigating) return;
        IsNavigating = true;
        try
        {
            if (await ConfirmNavigationAsync()) ShowSection(section);
            else source?.Focus(FocusState.Programmatic);
        }
        finally { IsNavigating = false; }
    }

    public Task<bool> ConfirmNavigationAsync() => ConfirmDraftAsync(false);
    public Task<bool> ConfirmExitAsync() => ConfirmDraftAsync(true);

    private async Task<bool> ConfirmDraftAsync(bool exiting)
    {
        if (HasPendingSave) return false;
        if (!HasDraft) return true;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = "尚有未保存的设置",
            Content = exiting ? "退出前可保存本分区的修改。若后续认证取消或退出失败，未保存的草稿仍会保留。"
                : "保存后离开，或放弃本分区的修改。继续编辑会保留当前草稿。",
            PrimaryButtonText = exiting ? "保存并退出" : "保存并离开",
            SecondaryButtonText = exiting ? "不保存并退出" : "放弃修改", CloseButtonText = "继续编辑",
            DefaultButton = ContentDialogButton.Close
        };
        var result = await (_showDialog ?? throw new InvalidOperationException("设置页尚未连接对话框宿主。"))(dialog);
        if (result == ContentDialogResult.Primary) return await SaveSectionAsync();
        if (result != ContentDialogResult.Secondary) return false;
        // Exit can still fail or be cancelled during authentication. Keep the draft until process exit.
        if (!exiting) DiscardSection();
        return true;
    }

    public void RestoreEditorFocus()
    {
        if (_section == "Reminder") ForceCloseCountdownNumberBox.Focus(FocusState.Programmatic);
        else if (_section == "Authentication") RequireAuthenticationSwitch.Focus(FocusState.Programmatic);
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e) => await SaveSectionAsync();
    private async Task<bool> SaveSectionAsync()
    {
        if (HasPendingSave || _section is not ("Reminder" or "Authentication")) return false;
        SyncDraft();
        var result = _section == "Reminder" ? await ViewModel.SaveReminderAsync()
            : await ViewModel.SaveAuthenticationAsync(AuthenticationPasswordBox.Password, ConfirmAuthenticationPasswordBox.Password);
        if (result.Settings is { } settings)
        {
            ApplyControls(() => { if (_section == "Reminder") ApplyReminder(settings); else ApplyAuthentication(settings); });
            ShowMessage("设置已保存。", InfoBarSeverity.Success);
            StatusChanged?.Invoke(this, "设置已保存。");
            return true;
        }
        ShowMessage(result.Cancelled ? "未通过认证，设置未保存，草稿已保留。" : $"保存设置失败：{result.Error} 草稿已保留。",
            result.Cancelled ? InfoBarSeverity.Warning : InfoBarSeverity.Error);
        return false;
    }

    private void ImmediateSettingsSwitch_Toggled(object sender, RoutedEventArgs e) => QueueGeneralSave();
    private void ImmediateSettingsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => QueueGeneralSave();
    private void QueueGeneralSave()
    {
        if (_isApplyingSettings || !IsLoaded || HasPendingSave) return;
        _generalSaveQueued = true;
        // Let the input control finish committing its selection before disabling the form.
        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            _generalSaveQueued = false;
            await SaveGeneralAsync();
        })) _generalSaveQueued = false;
    }
    private async Task SaveGeneralAsync()
    {
        if (_isApplyingSettings || !IsLoaded || ViewModel.IsBusy) return;
        var edit = new GeneralSettingsEdit(CloseWindowActionComboBox.SelectedIndex == 1
            ? CloseWindowAction.ExitApplication : CloseWindowAction.MinimizeToTray, StartWithWindowsSwitch.IsOn, PlayReminderSoundsSwitch.IsOn);
        var result = await ViewModel.SaveGeneralAsync(edit);
        ApplyControls(() => ApplyGeneral(result.Settings ?? ViewModel.CurrentSettings));
        var message = result.Succeeded ? "常规设置已保存。" : result.Cancelled ? "未通过认证，常规设置已恢复。" : $"保存常规设置失败：{result.Error}";
        ShowMessage(message, result.Succeeded ? InfoBarSeverity.Success : result.Cancelled ? InfoBarSeverity.Warning : InfoBarSeverity.Error);
        StatusChanged?.Invoke(this, message);
    }

    private void SettingsDraftNumber_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => SyncDraft();
    private void SettingsDraftSwitch_Toggled(object sender, RoutedEventArgs e) => SyncDraft();
    private void SettingsDraftPassword_Changed(object sender, RoutedEventArgs e) => SyncDraft();
    private void SyncDraft()
    {
        if (_isApplyingSettings) return;
        ViewModel.Reminder = new(ForceCloseCountdownNumberBox.Value, FirstReminderThresholdNumberBox.Value,
            SecondReminderThresholdNumberBox.Value, FinalReminderThresholdNumberBox.Value);
        ViewModel.AuthenticationEnabled = RequireAuthenticationSwitch.IsOn;
        ViewModel.HasPasswordDraft = AuthenticationPasswordBox.Password.Length > 0 || ConfirmAuthenticationPasswordBox.Password.Length > 0;
        UpdateDraftNotice();
    }
    private void UpdateDraftNotice() => SettingsDraftNotice.Visibility = HasDraft ? Visibility.Visible : Visibility.Collapsed;

    private void UpdateAuthenticationFields()
    {
        AuthenticationPasswordBox.IsEnabled = RequireAuthenticationSwitch.IsOn;
        ConfirmAuthenticationPasswordBox.IsEnabled = RequireAuthenticationSwitch.IsOn;
    }

    private void DiscardSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (HasPendingSave) return;
        DiscardSection();
        ShowMessage("已放弃本分区的修改。", InfoBarSeverity.Informational);
    }
    private void DiscardSection()
    {
        ApplyControls(() =>
        {
            if (_section == "Reminder") { ViewModel.DiscardReminder(); ApplyReminder(ViewModel.CurrentSettings); }
            else { ViewModel.DiscardAuthentication(); ApplyAuthentication(ViewModel.CurrentSettings); }
        });
    }

    public void ApplySettings(AppSettings settings)
    {
        ApplyControls(() => { ViewModel.Reset(settings); ApplyGeneral(settings); ApplyReminder(settings); ApplyAuthentication(settings); });
    }
    private void ApplyControls(Action apply)
    {
        _isApplyingSettings = true;
        try { apply(); }
        finally { _isApplyingSettings = false; UpdateDraftNotice(); }
    }
    private void ApplyGeneral(AppSettings settings)
    {
        CloseWindowActionComboBox.SelectedIndex = settings.CloseWindowAction == CloseWindowAction.ExitApplication ? 1 : 0;
        StartWithWindowsSwitch.IsOn = settings.StartWithWindows;
        PlayReminderSoundsSwitch.IsOn = settings.PlayReminderSounds;
    }
    private void ApplyReminder(AppSettings settings)
    {
        ForceCloseCountdownNumberBox.Value = settings.ForceCloseCountdownSeconds;
        FirstReminderThresholdNumberBox.Value = settings.FirstReminderThresholdPercent;
        SecondReminderThresholdNumberBox.Value = settings.SecondReminderThresholdPercent;
        FinalReminderThresholdNumberBox.Value = settings.FinalReminderThresholdPercent;
    }
    private void ApplyAuthentication(AppSettings settings)
    {
        RequireAuthenticationSwitch.IsOn = settings.RequireAuthenticationForSensitiveActions;
        AuthenticationPasswordBox.Password = string.Empty;
        ConfirmAuthenticationPasswordBox.Password = string.Empty;
        AuthenticationPasswordBox.PlaceholderText = settings.AuthenticationCredential is null ? "设置认证密码" : "留空则保留当前密码";
        UpdateAuthenticationFields();
    }
    public void ShowMessage(string message, InfoBarSeverity severity)
    {
        _feedbackTimer.Stop();
        SettingsSuccessMessage.Visibility = Visibility.Collapsed;
        if (severity is InfoBarSeverity.Success or InfoBarSeverity.Informational)
        {
            SettingsInfoBar.IsOpen = false;
            SettingsSuccessMessage.Text = message;
            SettingsSuccessMessage.Visibility = Visibility.Visible;
            _feedbackTimer.Start();
            return;
        }
        SettingsInfoBar.Title = message; SettingsInfoBar.Message = string.Empty;
        SettingsInfoBar.Severity = severity; SettingsInfoBar.IsOpen = true;
    }

    public void ShowRoot() => ShowSection(null);
    public Task OpenCategoryAsync(string section) => NavigateAsync(section, null);
    private void ShowSection(string? section)
    {
        _feedbackTimer.Stop();
        SettingsSuccessMessage.Visibility = Visibility.Collapsed;
        _section = section;
        SettingsRootPanel.Visibility = section is null ? Visibility.Visible : Visibility.Collapsed;
        GeneralSettingsPanel.Visibility = section == "General" ? Visibility.Visible : Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = section == "Reminder" ? Visibility.Visible : Visibility.Collapsed;
        AuthenticationSettingsPanel.Visibility = section == "Authentication" ? Visibility.Visible : Visibility.Collapsed;
        MaintenanceSettingsPanel.Visibility = section == "Maintenance" ? Visibility.Visible : Visibility.Collapsed;
        SubtitleChanged?.Invoke(this, section switch
        {
            "General" => "常规设置会立即生效", "Reminder" or "Authentication" => "修改后点击保存设置",
            "Maintenance" => "应用维护", _ => "选择设置类别"
        });
        UpdateDraftNotice();
    }

    private void ExportBackupButton_Click(object sender, RoutedEventArgs e) => MaintenanceRequested?.Invoke(this, "ExportBackup");
    private void RestoreBackupButton_Click(object sender, RoutedEventArgs e) => MaintenanceRequested?.Invoke(this, "RestoreBackup");
    private void OpenDataFolderButton_Click(object sender, RoutedEventArgs e) => MaintenanceRequested?.Invoke(this, "OpenDataFolder");
    private void ExportDiagnosticsButton_Click(object sender, RoutedEventArgs e) => MaintenanceRequested?.Invoke(this, "ExportDiagnostics");
    private void UninstallButton_Click(object sender, RoutedEventArgs e) => MaintenanceRequested?.Invoke(this, "Uninstall");
}
