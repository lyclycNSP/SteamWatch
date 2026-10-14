namespace SteamWatch.App.Services;

/// <summary>Owns one monitor task and its cancellation source through stop and restart.</summary>
public sealed class BackgroundMonitor(Func<CancellationToken, Task> run)
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cancellation;
    private Task? _task;
    private bool _isStopping;

    public void Start()
    {
        lock (_gate)
        {
            if (_isStopping || _task is not null) return;
            _cancellation = new();
            var token = _cancellation.Token;
            _task = Task.Run(() => run(token));
        }
    }

    public async Task StopAsync()
    {
        Task? task;
        CancellationTokenSource? cancellation;
        lock (_gate)
        {
            _isStopping = true;
            task = _task;
            cancellation = _cancellation;
            cancellation?.Cancel();
        }
        try
        {
            if (task is not null) await task;
        }
        catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true) { }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_cancellation, cancellation))
                {
                    _task = null;
                    _cancellation = null;
                    cancellation?.Dispose();
                    _isStopping = false;
                }
            }
        }
    }

    public async Task StopAndFlushAsync(Func<Task> flush)
    {
        await StopAsync();
        try { await flush(); }
        catch { Start(); throw; }
    }
}
