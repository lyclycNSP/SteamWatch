using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
using SteamWatch.Infrastructure.Runtime;

namespace SteamWatch.Installer;

internal sealed class InstallationEngine(InstallationContext context)
{
    internal async Task PrepareAsync(string management, string payload, string hash)
    {
        context.ValidateManagement(management);
        await ConfirmExitAsync();
        using var tx = new DirectoryUpgradeTransaction(context.ProgramDirectory);
        Resolve(tx);
        ValidateExistingRegistration();
        PathSafety.NoLinks(payload);
        using (var input = File.OpenRead(payload))
            if (hash.Length != 64 || !string.Equals(Convert.ToHexString(SHA256.HashData(input)), hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Embedded payload hash mismatch.");
        var stage = Path.Combine(Path.GetDirectoryName(context.ProgramDirectory)!, "SteamWatch.staging-" + Guid.NewGuid().ToString("N"));
        PayloadExtractor.Extract(payload, stage);
        tx.Prepare(stage, JsonSerializer.Serialize(RegistrationSnapshot.Capture(context)));
    }

    internal void FinalizeInstall(string management)
    {
        context.ValidateManagement(management);
        using var tx = new DirectoryUpgradeTransaction(context.ProgramDirectory);
        try
        {
            VerifyActive(management);
            CopyLegacyData();
            tx.SwitchDirectories();
            var exe = Path.Combine(context.ProgramDirectory, "SteamWatch.exe");
            WriteLink(context.Links[0], exe, context.ProgramDirectory);
            WriteLink(context.Links[1], exe, context.ProgramDirectory);
            WriteLink(context.Links[2], Path.Combine(management, "unins000.exe"), management);
            WriteLink(context.Links[3], Path.Combine(context.ProgramDirectory, "\u64CD\u4F5C\u6307\u5357.txt"), context.ProgramDirectory);
            using (var startup = Registry.CurrentUser.OpenSubKey(context.StartupKey, writable: true))
            {
                if (startup?.GetValue("SteamWatch") is not null)
                {
                    if (startup.GetValueKind("SteamWatch") is not (RegistryValueKind.String or RegistryValueKind.ExpandString))
                        throw new InvalidDataException("Unexpected startup registration type; upgrade cancelled.");
                    startup.SetValue("SteamWatch", "\"" + exe + "\"");
                }
            }
            // The old script is never executed: its key and links are handed over atomically.
            Registry.CurrentUser.DeleteSubKey(context.LegacyKey, false);
            tx.Commit();
            RefreshShell();
        }
        catch
        {
            if (tx.Phase is not ("None" or "Committed" or "RolledBack")) tx.Rollback(Restore);
            throw;
        }
    }

    internal void Rollback()
    {
        using var tx = new DirectoryUpgradeTransaction(context.ProgramDirectory);
        if (tx.Phase is not ("None" or "Committed" or "RolledBack")) tx.Rollback(Restore);
    }

    internal void VerifyActive(string management)
    {
        context.ValidateManagement(management);
        using var key = Registry.CurrentUser.OpenSubKey(context.InnoKey);
        var command = key?.GetValue("UninstallString") as string;
        if (!string.Equals(command, "\"" + Path.Combine(management, "unins000.exe") + "\"", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This uninstall entry has been superseded. Use Windows Settings to uninstall SteamWatch.");
    }

    internal async Task UninstallAsync(string management)
    {
        VerifyActive(management);
        await ConfirmExitAsync();
        using var tx = new DirectoryUpgradeTransaction(context.ProgramDirectory);
        Resolve(tx);
        CopyLegacyData();
        // The same durable switch also compensates failed uninstall registration/link operations.
        var stage = Path.Combine(Path.GetDirectoryName(context.ProgramDirectory)!, "SteamWatch.staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        tx.Prepare(stage, JsonSerializer.Serialize(RegistrationSnapshot.Capture(context)));
        try
        {
            tx.SwitchDirectories();
            foreach (var path in context.Links)
            {
                PathSafety.NoLinks(path);
                if (File.Exists(path)) File.Delete(path);
            }
            using (var startup = Registry.CurrentUser.OpenSubKey(context.StartupKey, writable: true))
                startup?.DeleteValue("SteamWatch", false);
            tx.Commit();
            // Recovery copies, including legacy data, remain outside the live program directory.
            Directory.Move(context.ProgramDirectory, Path.Combine(Path.GetDirectoryName(context.ProgramDirectory)!,
                "SteamWatch.removed-" + Guid.NewGuid().ToString("N")));
            RefreshShell();
        }
        catch
        {
            if (tx.Phase is not ("Committed" or "RolledBack")) tx.Rollback(Restore);
            throw;
        }
    }

    private void Resolve(DirectoryUpgradeTransaction tx)
    {
        if (tx.Phase == "None") return;
        if (tx.Phase is not ("Committed" or "RolledBack")) tx.Rollback(Restore);
        tx.ArchiveResolved();
    }

    private void ValidateExistingRegistration()
    {
        using var legacy = Registry.CurrentUser.OpenSubKey(context.LegacyKey);
        if (legacy is not null && (legacy.GetValue("InstallLocation") is not string location ||
            !PathSafety.Same(location, context.ProgramDirectory)))
            throw new InvalidDataException("Legacy uninstall identity belongs to an unexpected installation. Nothing replaced.");
        using var current = Registry.CurrentUser.OpenSubKey(context.InnoKey);
        if (current is not null)
        {
            var command = current.GetValue("UninstallString") as string;
            if (command is null || command.Length < 3 || command[0] != '"' || command[^1] != '"' ||
                !Path.GetFileName(command[1..^1]).Equals("unins000.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Existing Inno uninstall identity is invalid. Nothing replaced.");
            context.ValidateManagement(Path.GetDirectoryName(command[1..^1])!);
        }
    }

    private void Restore(string json) => (JsonSerializer.Deserialize<RegistrationSnapshot>(json)
        ?? throw new InvalidDataException("Missing installation snapshot.")).Restore(context);

    private async Task ConfirmExitAsync()
    {
        foreach (var process in Process.GetProcessesByName("SteamWatch"))
        {
            using (process)
            {
                if (!PathSafety.Same(process.MainModule?.FileName ?? "", Path.Combine(context.ProgramDirectory, "SteamWatch.exe"))) continue;
                if (await UpgradeExitClient.RequestAsync(context.DataDirectory, TimeSpan.FromSeconds(95)) != UpgradeExitResult.Prepared)
                    throw new IOException("SteamWatch did not authorize normal exit. Cancelled, save failed, or older version without upgrade protocol. Close it normally and retry.");
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
    }

    private void CopyLegacyData()
    {
        PathSafety.NoLinks(context.DataDirectory, tree: true);
        var legacy = Path.Combine(context.ProgramDirectory, "data");
        PathSafety.NoLinks(legacy, tree: true);
        if (!Directory.Exists(legacy)) return;
        Directory.CreateDirectory(context.DataDirectory);
        foreach (var file in Directory.EnumerateFiles(legacy))
        {
            var target = Path.Combine(context.DataDirectory, Path.GetFileName(file));
            if (!File.Exists(target)) File.Copy(file, target);
        }
    }

    private static void WriteLink(string path, string target, string workingDirectory)
    {
        PathSafety.NoLinks(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link = shell.CreateShortcut(path);
        try
        {
            link.TargetPath = target;
            link.Arguments = "";
            link.WorkingDirectory = workingDirectory;
            link.Save();
        }
        finally { Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
    private static void RefreshShell() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
}

internal static class InstallationCommands
{
    internal static int Run(InstallationContext context, string[] args)
    {
        try
        {
            if (args.Length < 2) throw new ArgumentException("Installation operation and management directory are required.");
            context.ValidateManagement(args[1]);
            var engine = new InstallationEngine(context);
            switch (args[0])
            {
                case "prepare" when args.Length == 4: engine.PrepareAsync(args[1], args[2], args[3]).GetAwaiter().GetResult(); break;
                case "finalize" when args.Length == 2: engine.FinalizeInstall(args[1]); break;
                case "rollback" when args.Length == 2: engine.Rollback(); break;
                case "active" when args.Length == 2: engine.VerifyActive(args[1]); break;
                case "uninstall" when args.Length == 2: engine.UninstallAsync(args[1]).GetAwaiter().GetResult(); break;
                default: throw new ArgumentException("Unknown installation operation.");
            }
            return 0;
        }
        catch (Exception ex)
        {
            // A process boundary: preserve the failure for the wizard, including rollback errors.
            var log = Path.Combine(Path.GetDirectoryName(context.ProgramDirectory)!, "SteamWatch.installation.log");
            try
            {
                PathSafety.NoLinks(log);
                Directory.CreateDirectory(Path.GetDirectoryName(log)!);
                File.AppendAllText(log, $"{DateTimeOffset.Now:o} {args.FirstOrDefault()}: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}");
            }
            catch (Exception logging) when (logging is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
            return 1;
        }
    }
}
