using System.Diagnostics;
using System.Runtime.Versioning;

namespace SteamWatch.Infrastructure.Enforcement;

[SupportedOSPlatform("windows")]
public sealed class WindowsProcessTerminatorBackend : IProcessTerminatorBackend
{
    public string? GetIdentity(int processId)
    {
        using var process = Process.GetProcessById(processId);
        return $"{process.StartTime.ToUniversalTime().Ticks}:{process.MainModule?.FileName}";
    }

    public bool IsSameProcess(int processId, string? identity) => GetIdentity(processId) == identity;

    public async Task<bool> WaitForExitAsync(int processId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ArgumentException) { return true; }
        catch (TimeoutException) { return false; }
    }

    public bool TryCloseMainWindow(int processId)
    {
        using var process = Process.GetProcessById(processId);
        return process.CloseMainWindow();
    }

    public void KillProcessTree(int processId)
    {
        using var process = Process.GetProcessById(processId);
        process.Kill(entireProcessTree: true);
    }
}
