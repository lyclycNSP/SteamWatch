using SteamWatch.App.Presentation;

namespace SteamWatch.Tests;

[TestClass]
public sealed class ExitPreparationTests
{
    [TestMethod]
    public async Task ContinueEditingSkipsAuthenticationAndShutdown()
    {
        var exit = new ExitPreparation();
        Assert.IsFalse(await exit.TryPrepareAsync(() => Task.FromResult(false),
            () => throw new AssertFailedException("Authentication must not run."),
            () => throw new AssertFailedException("Shutdown must not run."),
            () => Assert.Fail("Runtime was never stopped.")));
        Assert.IsFalse(exit.IsActive);
    }

    [TestMethod]
    public async Task AuthenticationUsesConfigurationSavedByDraft()
    {
        var exit = new ExitPreparation();
        var protectionEnabled = false;
        var calls = new List<string>();
        Assert.IsTrue(await exit.TryPrepareAsync(() =>
        {
            calls.Add("save"); protectionEnabled = true; return Task.FromResult(true);
        }, () =>
        {
            Assert.IsTrue(protectionEnabled); calls.Add("authenticate"); return Task.FromResult(true);
        }, () => { calls.Add("shutdown"); return Task.CompletedTask; }, () => Assert.Fail()));
        CollectionAssert.AreEqual(new[] { "save", "authenticate", "shutdown" }, calls);
        Assert.IsTrue(exit.IsActive);
        exit.Reset();
        Assert.IsFalse(exit.IsActive);
    }

    [TestMethod]
    public async Task AuthenticationCancellationKeepsDraftAndAllowsRetry()
    {
        var exit = new ExitPreparation();
        var draft = "unsaved";
        Assert.IsFalse(await exit.TryPrepareAsync(() => Task.FromResult(draft.Length > 0),
            () => Task.FromResult(false), () => throw new AssertFailedException(), () => Assert.Fail()));
        Assert.AreEqual("unsaved", draft);
        Assert.IsFalse(exit.IsActive);
        Assert.IsTrue(await exit.TryPrepareAsync(() => Task.FromResult(true), () => Task.FromResult(true),
            () => Task.CompletedTask, () => Assert.Fail()));
    }

    [TestMethod]
    public async Task FailedDraftSaveNeverStopsRuntime()
    {
        var editor = new SteamWatch.App.SettingsEditorViewModel(() => new(),
            _ => throw new IOException("locked"), _ => Task.FromResult(true), _ => { });
        editor.Reset(new());
        editor.Reminder = new(120, 60, 80, 95);
        var exit = new ExitPreparation();
        Assert.IsFalse(await exit.TryPrepareAsync(async () => (await editor.SaveReminderAsync()).Succeeded,
            () => throw new AssertFailedException(), () => throw new AssertFailedException(), () => Assert.Fail()));
        Assert.IsTrue(editor.HasReminderDraft);
        Assert.IsFalse(exit.IsActive);
    }

    [TestMethod]
    public async Task ConcurrentAndAlreadyPreparedRequestsAreRejected()
    {
        var exit = new ExitPreparation();
        var decision = new TaskCompletionSource<bool>();
        var first = exit.TryPrepareAsync(() => decision.Task, () => Task.FromResult(true),
            () => Task.CompletedTask, () => Assert.Fail());
        Task<bool> Duplicate() => exit.TryPrepareAsync(() => throw new AssertFailedException(),
            () => throw new AssertFailedException(), () => throw new AssertFailedException(), () => Assert.Fail());
        Assert.IsFalse(await Duplicate());
        decision.SetResult(true);
        Assert.IsTrue(await first);
        Assert.IsFalse(await Duplicate());
    }

    [TestMethod]
    public async Task ShutdownFailureResumesRuntimeAndReleasesGuard()
    {
        var exit = new ExitPreparation();
        var resumed = 0;
        await Assert.ThrowsAsync<IOException>(() => exit.TryPrepareAsync(() => Task.FromResult(true),
            () => Task.FromResult(true), () => throw new IOException("locked"), () => resumed++));
        Assert.AreEqual(1, resumed);
        Assert.IsFalse(exit.IsActive);
    }

    [TestMethod]
    public async Task CancelledUpgradeAfterShutdownResumesRuntime()
    {
        var exit = new ExitPreparation();
        using var cancellation = new CancellationTokenSource();
        var resumed = false;
        await Assert.ThrowsAsync<OperationCanceledException>(() => exit.TryPrepareAsync(() => Task.FromResult(true),
            () => Task.FromResult(true), () => { cancellation.Cancel(); return Task.CompletedTask; },
            () => resumed = true, cancellation.Token));
        Assert.IsTrue(resumed);
        Assert.IsFalse(exit.IsActive);
    }

    [TestMethod]
    public async Task CancelledUpgradeDuringDraftDecisionDoesNotShutdown()
    {
        var exit = new ExitPreparation();
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => exit.TryPrepareAsync(() =>
        {
            cancellation.Cancel(); return Task.FromResult(true);
        }, () => throw new AssertFailedException(), () => throw new AssertFailedException(),
            () => Assert.Fail(), cancellation.Token));
        Assert.IsFalse(exit.IsActive);
    }
}
