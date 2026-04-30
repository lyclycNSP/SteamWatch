using System.Diagnostics;
using System.Runtime.Versioning;

namespace SteamWatch.Infrastructure.Enforcement;

[SupportedOSPlatform("windows")]
public sealed class WindowsProcessTerminatorBackend : IProcessTerminatorBackend
{
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
