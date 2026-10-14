using SteamWatch.App;
using SteamWatch.App.Presentation;
using SteamWatch.Core.Security;
using SteamWatch.Core.Settings;

namespace SteamWatch.Tests;

[TestClass]
public sealed class SettingsEditorViewModelTests
{
    private sealed class Harness
    {
        public AppSettings Current = new();
        public int Saves;
        public int Authorizations;
        public readonly List<Exception> Errors = [];
        public Func<Task<bool>> Authorize = () => Task.FromResult(true);
        public bool FailWrite;
        public SettingsEditorViewModel Editor { get; }
        public Harness()
        {
            Editor = new(() => Current, settings =>
            {
                Saves++;
                if (FailWrite) throw new IOException("private-path");
                Current = settings;
                return Task.FromResult(settings);
            }, _ => { Authorizations++; return Authorize(); }, Errors.Add);
            Editor.Reset(Current);
        }
        public void EditReminder() => Editor.Reminder = new(120, 65, 80, 95);
    }

    [TestMethod]
    public async Task CancelledAuthenticationKeepsDraftWithoutWriting()
    {
        var h = new Harness { Authorize = () => Task.FromResult(false) };
        h.EditReminder();
        var result = await h.Editor.SaveReminderAsync();
        Assert.IsTrue(result.Cancelled);
        Assert.IsTrue(h.Editor.HasReminderDraft);
        Assert.IsFalse(h.Editor.IsBusy);
        Assert.AreEqual(0, h.Saves);
    }

    [TestMethod]
    public async Task WriteFailureRetainsDraftAndRetryClearsOnlySavedSection()
    {
        var h = new Harness { FailWrite = true };
        h.EditReminder();
        h.Editor.HasPasswordDraft = true;
        var failure = await h.Editor.SaveReminderAsync();
        Assert.IsFalse(failure.Succeeded);
        Assert.DoesNotContain("private-path", failure.Error!);
        Assert.HasCount(1, h.Errors);
        Assert.IsTrue(h.Editor.HasReminderDraft);
        Assert.IsFalse(h.Editor.IsBusy);
        h.FailWrite = false;
        Assert.IsTrue((await h.Editor.SaveReminderAsync()).Succeeded);
        Assert.IsFalse(h.Editor.HasReminderDraft);
        Assert.IsTrue(h.Editor.HasAuthenticationDraft);
        Assert.AreEqual(120, h.Current.ForceCloseCountdownSeconds);
    }

    [TestMethod]
    public async Task PendingAuthorizationRejectsDuplicateAndMergesLatestUnrelatedSettings()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var h = new Harness { Authorize = () => gate.Task };
        h.EditReminder();
        var pending = h.Editor.SaveReminderAsync();
        Assert.IsTrue(h.Editor.IsBusy);
        var duplicate = await h.Editor.SaveGeneralAsync(new(CloseWindowAction.ExitApplication, true, false));
        Assert.IsFalse(duplicate.Succeeded);
        Assert.AreEqual(1, h.Authorizations);
        h.Current = h.Current with { PlayReminderSounds = false, StartWithWindows = true };
        gate.SetResult(true);
        Assert.IsTrue((await pending).Succeeded);
        Assert.AreEqual(1, h.Saves);
        Assert.IsFalse(h.Current.PlayReminderSounds);
        Assert.IsTrue(h.Current.StartWithWindows);
    }

    [TestMethod]
    public async Task GeneralSavePreservesBothOtherDrafts()
    {
        var h = new Harness();
        h.EditReminder();
        h.Editor.AuthenticationEnabled = true;
        h.Editor.HasPasswordDraft = true;
        Assert.IsTrue((await h.Editor.SaveGeneralAsync(new(CloseWindowAction.ExitApplication, false, false))).Succeeded);
        Assert.IsTrue(h.Editor.HasReminderDraft);
        Assert.IsTrue(h.Editor.HasAuthenticationDraft);
        Assert.AreEqual(60, h.Current.ForceCloseCountdownSeconds);
        Assert.IsFalse(h.Current.RequireAuthenticationForSensitiveActions);
    }

    [TestMethod]
    public async Task InvalidReminderDoesNotRequestAuthentication()
    {
        var h = new Harness();
        h.Editor.Reminder = new(double.NaN, 70, 85, 95);
        Assert.IsFalse((await h.Editor.SaveReminderAsync()).Succeeded);
        Assert.AreEqual(0, h.Authorizations);
        Assert.AreEqual(0, h.Saves);
        Assert.IsTrue(h.Editor.HasReminderDraft);
    }

    [TestMethod]
    public async Task InvalidPasswordsKeepAuthenticationDraftWithoutAuthorization()
    {
        var h = new Harness();
        h.Editor.AuthenticationEnabled = true;
        Assert.IsFalse((await h.Editor.SaveAuthenticationAsync("", "")).Succeeded);
        h.Editor.HasPasswordDraft = true;
        Assert.IsFalse((await h.Editor.SaveAuthenticationAsync("abcd", "different")).Succeeded);
        Assert.AreEqual(0, h.Authorizations);
        Assert.AreEqual(0, h.Saves);
        Assert.IsTrue(h.Editor.HasAuthenticationDraft);
    }

    [TestMethod]
    public async Task EmptyPasswordRetainsCredentialChangedWhileAuthorizing()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var h = new Harness { Authorize = () => gate.Task };
        h.Current = h.Current with { AuthenticationCredential = PasswordHasher.Create("old-password") };
        h.Editor.Reset(h.Current);
        h.Editor.AuthenticationEnabled = true;
        var pending = h.Editor.SaveAuthenticationAsync("", "");
        var latest = PasswordHasher.Create("new-password");
        h.Current = h.Current with { AuthenticationCredential = latest };
        gate.SetResult(true);
        Assert.IsTrue((await pending).Succeeded);
        Assert.AreEqual(latest, h.Current.AuthenticationCredential);
    }

    [TestMethod]
    public async Task AuthenticationSaveClearsPasswordPresenceAndPreservesReminderDraft()
    {
        var h = new Harness();
        h.EditReminder();
        h.Editor.AuthenticationEnabled = true;
        h.Editor.HasPasswordDraft = true;
        Assert.IsTrue((await h.Editor.SaveAuthenticationAsync("fixture-password", "fixture-password")).Succeeded);
        Assert.IsFalse(h.Editor.HasPasswordDraft);
        Assert.IsFalse(h.Editor.HasAuthenticationDraft);
        Assert.IsTrue(h.Editor.HasReminderDraft);
        Assert.IsNotNull(h.Current.AuthenticationCredential);
    }
}
