using SteamWatch.Infrastructure.Runtime;

namespace SteamWatch.Tests;

[TestClass]
public sealed class UpgradeExitTests
{
    private static string Endpoint() => Path.Combine(Path.GetTempPath(), "SteamWatch.UpgradeTests", Guid.NewGuid().ToString("N"));

    [TestMethod]
    public async Task AcceptedRequestCommitsOnlyAfterPreparation()
    {
        var endpoint = Endpoint();
        var prepared = false;
        var committed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new UpgradeExitServer(endpoint, _ => { prepared = true; return Task.FromResult(true); },
            () => committed.TrySetResult(prepared), () => Assert.Fail("Unexpected abort."));
        Assert.AreEqual(UpgradeExitResult.Prepared, await UpgradeExitClient.RequestAsync(endpoint, TimeSpan.FromSeconds(5)));
        Assert.IsTrue(await committed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public async Task DeclinedAndFailedPreparationNeverCommitAndServerContinues()
    {
        var endpoint = Endpoint();
        var mode = 0;
        var commits = 0;
        using var server = new UpgradeExitServer(endpoint, _ => mode == 0
            ? Task.FromResult(false) : Task.FromException<bool>(new IOException("Save failed.")),
            () => Interlocked.Increment(ref commits), () => { });
        Assert.AreEqual(UpgradeExitResult.Declined, await UpgradeExitClient.RequestAsync(endpoint, TimeSpan.FromSeconds(5)));
        mode = 1;
        Assert.AreEqual(UpgradeExitResult.Failed, await UpgradeExitClient.RequestAsync(endpoint, TimeSpan.FromSeconds(5)));
        Assert.AreEqual(0, commits);
    }

    [TestMethod]
    public async Task ClientTimeoutDoesNotForceExit()
    {
        var endpoint = Endpoint();
        var commits = 0;
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var aborted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new UpgradeExitServer(endpoint, token => { started.TrySetResult(true); return release.Task.WaitAsync(token); },
            () => Interlocked.Increment(ref commits), () => aborted.TrySetResult(true));
        var request = UpgradeExitClient.RequestAsync(endpoint, TimeSpan.FromSeconds(2));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<OperationCanceledException>(() => request);
        release.TrySetResult(true);
        await aborted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, commits);
    }

    [TestMethod]
    public async Task DifferentDirectoryCannotReachServer()
    {
        var endpoint = Endpoint();
        var prepares = 0;
        using var server = new UpgradeExitServer(endpoint, _ => { Interlocked.Increment(ref prepares); return Task.FromResult(true); }, () => { }, () => { });
        // A different data directory must not reach this server.
        await Assert.ThrowsExactlyAsync<TimeoutException>(() => UpgradeExitClient.RequestAsync(Endpoint(), TimeSpan.FromSeconds(4)));
        Assert.AreEqual(0, prepares);
    }

    [TestMethod]
    public async Task CommitAndAbortFailureAreReportedAndNextRequestStillWorks()
    {
        var endpoint = Endpoint();
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var commits = 0;
        using var server = new UpgradeExitServer(endpoint, _ => Task.FromResult(true), () =>
        {
            if (Interlocked.Increment(ref commits) == 1) throw new IOException("Commit failed.");
        }, () => throw new InvalidOperationException("Abort failed."), ex =>
        {
            errors.Enqueue(ex);
            if (ex is InvalidOperationException) recovered.TrySetResult();
        });
        Assert.AreEqual(UpgradeExitResult.Prepared, await UpgradeExitClient.RequestAsync(endpoint, TimeSpan.FromSeconds(5)));
        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(UpgradeExitResult.Prepared, await UpgradeExitClient.RequestAsync(endpoint, TimeSpan.FromSeconds(5)));
        Assert.HasCount(2, errors);
        Assert.IsInstanceOfType<IOException>(errors.ElementAt(0));
        Assert.IsInstanceOfType<InvalidOperationException>(errors.ElementAt(1));
    }
}
