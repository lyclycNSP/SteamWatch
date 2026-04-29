using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using SteamWatch.Core.Steam;

namespace SteamWatch.Infrastructure.Steam;

[SupportedOSPlatform("windows")]
public sealed class WindowsProcessSnapshotProvider : IProcessSnapshotProvider
{
    public IReadOnlyList<SteamProcessSnapshot> GetProcesses()
    {
        var metadata = ReadProcessMetadata();
        var snapshots = new List<SteamProcessSnapshot>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var processId = process.Id;
                var name = process.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? process.ProcessName
                    : $"{process.ProcessName}.exe";

                metadata.TryGetValue(processId, out var item);
                snapshots.Add(new SteamProcessSnapshot(
                    processId,
                    item?.ParentProcessId ?? 0,
                    name,
                    item?.CommandLine ?? [SafeMainModulePath(process)]));
            }
            catch
            {
                // Some protected processes deny access. Monitoring should continue with the rest.
            }
            finally
            {
                process.Dispose();
            }
        }

        return snapshots;
    }

    private static Dictionary<int, ProcessMetadata> ReadProcessMetadata()
    {
        var result = new Dictionary<int, ProcessMetadata>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, ParentProcessId, CommandLine FROM Win32_Process");

        foreach (var item in searcher.Get().Cast<ManagementObject>())
        {
            try
            {
                var processId = Convert.ToInt32(item["ProcessId"]);
                var parentProcessId = Convert.ToInt32(item["ParentProcessId"]);
                var commandLine = SplitCommandLine(item["CommandLine"] as string);
                result[processId] = new ProcessMetadata(parentProcessId, commandLine);
            }
            catch
            {
                // Ignore malformed WMI rows and keep monitoring.
            }
            finally
            {
                item.Dispose();
            }
        }

        return result;
    }

    private static IReadOnlyList<string> SplitCommandLine(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return [];
        }

        return CommandLineSplitter.Split(commandLine);
    }

    private static string SafeMainModulePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? process.ProcessName;
        }
        catch
        {
            return process.ProcessName;
        }
    }

    private sealed record ProcessMetadata(int ParentProcessId, IReadOnlyList<string> CommandLine);
}
