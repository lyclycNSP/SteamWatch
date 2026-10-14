using SteamWatch.App.Presentation;

namespace SteamWatch.Tests;

[TestClass]
public sealed class UiOperationCoordinatorTests
{
    [TestMethod]
    public async Task RuleDialogAuthenticationAndWriteShareOneBusyInterval()
    {
        var state = new UiOperationCoordinator();
        var dialog = new TaskCompletionSource();
        var authentication = new TaskCompletionSource();
        var writing = new TaskCompletionSource();
        var authenticated = new TaskCompletionSource();
        var writeStarted = new TaskCompletionSource();
        var command = state.TryRunAsync(async () =>
        {
            await dialog.Task;
            authenticated.SetResult();
            await authentication.Task;
            writeStarted.SetResult();
            await writing.Task;
        });
        dialog.SetResult(); await authenticated.Task;
        Assert.IsFalse(state.TrySuspend());
        Assert.IsFalse(await state.TryRunAsync(() => throw new AssertFailedException("second command ran")));
        authentication.SetResult(); await writeStarted.Task;
        Assert.IsFalse(state.TrySuspend());
        writing.SetResult();
        Assert.IsTrue(await command);
        Assert.IsTrue(state.TrySuspend());
    }

    [TestMethod]
    public async Task CancelledPickerReleasesLeaseForNextMaintenanceCommand()
    {
        var state = new UiOperationCoordinator();
        var picker = new TaskCompletionSource();
        var export = state.TryRunAsync(() => picker.Task);
        Assert.IsFalse(state.TrySuspend());
        picker.SetResult();
        Assert.IsTrue(await export);
        var writes = 0;
        Assert.IsTrue(await state.TryRunAsync(() => { writes++; return Task.CompletedTask; }));
        Assert.AreEqual(1, writes);
    }

    [TestMethod]
    public async Task PendingCommandRejectsDuplicateAndExitThenReleases()
    {
        var state = new UiOperationCoordinator();
        var pending = new TaskCompletionSource();
        var first = state.TryRunAsync(() => pending.Task);
        Assert.IsTrue(state.IsBusy);
        Assert.IsFalse(await state.TryRunAsync(() => throw new AssertFailedException("duplicate ran")));
        Assert.IsFalse(state.TrySuspend());
        pending.SetResult();
        Assert.IsTrue(await first);
        Assert.IsFalse(state.IsBusy);
        Assert.IsTrue(state.TrySuspend());
    }

    [TestMethod]
    public async Task ExitSuspensionBlocksCommandsUntilCancellationResumes()
    {
        var state = new UiOperationCoordinator();
        Assert.IsTrue(state.TrySuspend());
        Assert.IsFalse(state.TrySuspend());
        Assert.IsFalse(await state.TryRunAsync(() => throw new AssertFailedException("suspended command ran")));
        state.Resume();
        Assert.IsTrue(await state.TryRunAsync(() => Task.CompletedTask));
    }

    [TestMethod]
    public async Task FailureReleasesBusyAndAllowsRetry()
    {
        var state = new UiOperationCoordinator();
        await Assert.ThrowsAsync<IOException>(() => state.TryRunAsync(() => throw new IOException("fixture")));
        Assert.IsFalse(state.IsBusy);
        Assert.IsTrue(await state.TryRunAsync(() => Task.CompletedTask));
    }

    [TestMethod]
    public async Task CancellationReleasesBusyWithoutSuspendingFutureWork()
    {
        var state = new UiOperationCoordinator();
        await Assert.ThrowsAsync<OperationCanceledException>(() => state.TryRunAsync(() => throw new OperationCanceledException()));
        Assert.IsFalse(state.IsBusy);
        Assert.IsFalse(state.IsSuspended);
        Assert.IsTrue(state.TrySuspend());
    }
}
