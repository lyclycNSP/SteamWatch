using System.Security.Cryptography;
using System.Text;

namespace SteamWatch.App.Services;

internal sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private RegisteredWaitHandle? _wait;
    private bool _disposed;
    public bool IsPrimary { get; }

    public SingleInstanceService(string dataDirectory)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dataDirectory).ToUpperInvariant())));
        _mutex = new(false, $"Local\\SteamWatch-{key}");
        _activation = new(false, EventResetMode.AutoReset, $"Local\\SteamWatch-Activate-{key}");
        try { IsPrimary = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsPrimary = true; }
        if (!IsPrimary) _activation.Set();
    }

    public void Listen(Action activate) => _wait = ThreadPool.RegisterWaitForSingleObject(
        _activation, (_, _) => activate(), null, Timeout.Infinite, false);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wait?.Unregister(null);
        _activation.Dispose();
        if (IsPrimary) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
