using SteamWatch.App.Services;

namespace SteamWatch.Tests;

[TestClass]
public sealed class BackgroundMonitorTests
{
    [TestMethod]
    public async Task FailedFlushRestartsOnlyAfterOldLoopStopsAndSuccessfulRetryStaysStopped()
    {
        var starts = 0;
        var active = 0;
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var monitor = new BackgroundMonitor(async token =>
        {
            Assert.AreEqual(1, Interlocked.Increment(ref active));
            (Interlocked.Increment(ref starts) == 1 ? first : resumed).TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { Interlocked.Decrement(ref active); }
        });
        try
        {
            monitor.Start();
            await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<IOException>(() => monitor.StopAndFlushAsync(() =>
            {
                Assert.AreEqual(0, active);
                throw new IOException("locked active ledger");
            }));
            await resumed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            monitor.Start();
            await monitor.StopAndFlushAsync(() =>
            {
                Assert.AreEqual(0, active);
                return Task.CompletedTask;
            });
            Assert.AreEqual(2, starts);
            Assert.AreEqual(0, active);
        }
        finally { await monitor.StopAsync(); }
    }

    [TestMethod]
    public async Task DuplicateStartRunsOneLoopAndStopWaitsForCleanup()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var monitor = new BackgroundMonitor(async token =>
        {
            Interlocked.Increment(ref calls);
            entered.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cancelled.SetResult(); await cleanup.Task; }
        });
        monitor.Start(); monitor.Start();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stopping = monitor.StopAsync();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(stopping.IsCompleted);
        monitor.Start();
        Assert.AreEqual(1, calls);
        cleanup.SetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task RestartGetsFreshTokenAndDoesNotOverlapOldLoop()
    {
        var first = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var monitor = new BackgroundMonitor(async token =>
        {
            (Interlocked.Increment(ref calls) == 1 ? first : second).SetResult(token);
            await Task.Delay(Timeout.Infinite, token);
        });
        monitor.Start(); var oldToken = await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await monitor.StopAsync();
        monitor.Start(); var freshToken = await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(oldToken.IsCancellationRequested);
        Assert.IsFalse(freshToken.IsCancellationRequested);
        Assert.AreNotEqual(oldToken, freshToken);
        await monitor.StopAsync();
    }

    [TestMethod]
    public async Task StopBeforeStartIsHarmlessAndFaultCanBeRestarted()
    {
        var calls = 0;
        var monitor = new BackgroundMonitor(_ => Interlocked.Increment(ref calls) == 1
            ? Task.FromException(new IOException("fixture")) : Task.CompletedTask);
        await monitor.StopAsync();
        monitor.Start();
        await Assert.ThrowsAsync<IOException>(() => monitor.StopAsync());
        monitor.Start(); await monitor.StopAsync();
        Assert.AreEqual(2, calls);
    }
}
