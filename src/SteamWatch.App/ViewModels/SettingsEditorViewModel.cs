using System.ComponentModel;
using System.Runtime.CompilerServices;
using SteamWatch.App.Presentation;
using SteamWatch.Core.Security;
using SteamWatch.Core.Settings;

namespace SteamWatch.App;

public sealed record SettingsSaveResult(AppSettings? Settings = null, bool Cancelled = false, string? Error = null)
{
    public bool Succeeded => Settings is not null;
}

/// <summary>Owns drafts and serializes settings commands without depending on UI controls.</summary>
public sealed class SettingsEditorViewModel : INotifyPropertyChanged
{
    private readonly Func<AppSettings> _read;
    private readonly Func<AppSettings, Task<AppSettings>> _save;
    private readonly Func<string, Task<bool>> _authorize;
    private readonly Action<Exception> _reportError;
    private AppSettings _baseline = new();
    private ReminderSettingsEdit _reminder = ReminderSettingsEdit.From(new AppSettings());
    private bool _authenticationEnabled;
    private bool _hasPasswordDraft;
    private bool _isBusy;

    public SettingsEditorViewModel(Func<AppSettings> read, Func<AppSettings, Task<AppSettings>> save,
        Func<string, Task<bool>> authorize, Action<Exception> reportError)
    {
        _read = read; _save = save; _authorize = authorize; _reportError = reportError;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsBusy => _isBusy;
    public ReminderSettingsEdit Reminder
    {
        get => _reminder;
        set { _reminder = value; NotifyDrafts(); }
    }
    public bool AuthenticationEnabled
    {
        get => _authenticationEnabled;
        set { _authenticationEnabled = value; NotifyDrafts(); }
    }
    public bool HasPasswordDraft
    {
        get => _hasPasswordDraft;
        set { _hasPasswordDraft = value; NotifyDrafts(); }
    }
    public bool HasReminderDraft => Reminder != ReminderSettingsEdit.From(_baseline);
    public bool HasAuthenticationDraft => AuthenticationEnabled != _baseline.RequireAuthenticationForSensitiveActions || HasPasswordDraft;
    public AppSettings CurrentSettings => _read();

    public void Reset(AppSettings settings)
    {
        _baseline = settings;
        Reminder = ReminderSettingsEdit.From(settings);
        AuthenticationEnabled = settings.RequireAuthenticationForSensitiveActions;
        HasPasswordDraft = false;
    }

    public void DiscardReminder() => AcceptReminder(_read());
    public void DiscardAuthentication() => AcceptAuthentication(_read());

    public Task<SettingsSaveResult> SaveGeneralAsync(GeneralSettingsEdit edit) =>
        SaveAsync(() => edit.Merge, "保存常规设置", _ => { });

    public Task<SettingsSaveResult> SaveReminderAsync()
    {
        var draft = Reminder;
        return SaveAsync(() => draft.Merge, "保存设置", AcceptReminder);
    }

    public Task<SettingsSaveResult> SaveAuthenticationAsync(string password, string confirmation)
    {
        var enabled = AuthenticationEnabled;
        // Passwords are call-local only; persistent draft state stores presence, never plaintext.
        return SaveAsync(() =>
        {
            PasswordCredential? replacement = null;
            if (enabled && (password.Length > 0 || confirmation.Length > 0))
            {
                if (password.Length < 4) throw new ArgumentException("认证密码至少需要 4 位。");
                if (password != confirmation) throw new ArgumentException("两次输入的认证密码不一致。");
                replacement = PasswordHasher.Create(password);
            }
            return current =>
            {
                var credential = enabled ? replacement ?? current.AuthenticationCredential : null;
                if (enabled && credential is null) throw new ArgumentException("开启认证时需要设置密码。");
                return current with { RequireAuthenticationForSensitiveActions = enabled, AuthenticationCredential = credential };
            };
        }, "保存设置", AcceptAuthentication);
    }

    private async Task<SettingsSaveResult> SaveAsync(Func<Func<AppSettings, AppSettings>> prepare,
        string action, Action<AppSettings> accept)
    {
        if (_isBusy) return new(Error: "设置正在保存，请稍后重试。");
        _isBusy = true;
        OnPropertyChanged(nameof(IsBusy));
        try
        {
            var merge = prepare();
            _ = merge(_read()); // Invalid drafts do not trigger authentication.
            if (!await _authorize(action)) return new(Cancelled: true);
            var settings = await _save(merge(_read())); // Retain unrelated changes made while authorization awaited.
            accept(settings);
            return new(Settings: settings);
        }
        catch (ArgumentException ex) { return new(Error: ex.Message); }
        catch (Exception ex)
        {
            _reportError(ex);
            return new(Error: ex is IOException or UnauthorizedAccessException
                ? "无法写入设置，请检查文件占用或目录权限后重试。"
                : "无法保存设置，请重试或导出诊断包。");
        }
        finally { _isBusy = false; OnPropertyChanged(nameof(IsBusy)); }
    }

    private void AcceptReminder(AppSettings settings)
    {
        _baseline = ReminderSettingsEdit.From(settings).Merge(_baseline);
        Reminder = ReminderSettingsEdit.From(settings);
    }

    private void AcceptAuthentication(AppSettings settings)
    {
        _baseline = _baseline with
        {
            RequireAuthenticationForSensitiveActions = settings.RequireAuthenticationForSensitiveActions,
            AuthenticationCredential = settings.AuthenticationCredential
        };
        AuthenticationEnabled = settings.RequireAuthenticationForSensitiveActions;
        HasPasswordDraft = false;
    }

    private void NotifyDrafts()
    {
        OnPropertyChanged(nameof(HasReminderDraft));
        OnPropertyChanged(nameof(HasAuthenticationDraft));
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
