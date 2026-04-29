using Microsoft.Win32;
using System.Runtime.Versioning;

namespace SteamWatch.Infrastructure.Startup;

[SupportedOSPlatform("windows")]
public sealed class WindowsStartupRegistry : IStartupRegistry
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? Read(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(name) as string;
    }

    public void Write(string name, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(name, command, RegistryValueKind.String);
    }

    public void Delete(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
