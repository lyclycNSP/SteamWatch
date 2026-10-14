using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
using SteamWatch.Infrastructure.Runtime;
using SteamWatch.Installer;

// Complete-payload experiment, bound to one compiled sandbox and two random uninstall keys.
internal static class BundleFixture
{
    private sealed record Configuration(string ArtifactRoot, string Sandbox, string Identity, string BuildId,
        string PayloadSha256, string FailureMode);
    private sealed record Snapshot(string Registration, Dictionary<string, byte[]?> Links, bool StartupKeyExisted, string? Startup);
    private sealed record Target(int ProcessId, long StartedUtcTicks, string Executable);
    private static readonly string[] LinkNames = ["desktop.lnk", "start-menu.lnk", "uninstall.lnk", "guide.lnk"];

    public static async Task<int> RunAsync(string root, string sandbox, string mode, string configurationPath, string? payload)
    {
        NoLinks(sandbox);
        try { return await RunCoreAsync(root, sandbox, mode, configurationPath, payload); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or
            ArgumentException or System.Security.SecurityException or COMException or TimeoutException or OperationCanceledException or JsonException)
        {
            File.AppendAllText(Path.Combine(sandbox, "bundle-helper.log"), $"{DateTimeOffset.Now:o} {mode}: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}");
            throw;
        }
    }

    private static async Task<int> RunCoreAsync(string root, string sandbox, string mode, string configurationPath, string? payload)
    {
        var config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(configurationPath))
            ?? throw new InvalidDataException("Missing bundle configuration.");
        if (!Same(config.ArtifactRoot, root) || !Same(config.Sandbox, sandbox) ||
            !Guid.TryParseExact(config.Identity, "N", out _) || !Guid.TryParseExact(config.BuildId, "N", out _) ||
            config.PayloadSha256.Length != 64 || config.FailureMode is not ("none" or "after-switch" or "after-shortcut" or
                "crash-prepared" or "crash-old-renamed" or "crash-new-renamed" or "crash-registered" or "crash-committed"))
            throw new InvalidDataException("Bundle configuration is outside its compiled scope.");
        NoLinks(sandbox);
        var install = Path.Combine(sandbox, "app");
        var management = Path.Combine(sandbox, "installers", config.BuildId);
        var links = Path.Combine(sandbox, "links");
        var startupPath = @"Software\SteamWatch\Validation\" + config.Identity + @"\Run";
        var keys = TransactionFixture.Paths(config.Identity, shortInnoIdentity: true);

        if (mode == "exit") { await ConfirmExitAsync(root, sandbox, install); return 0; }

        if (mode == "prepare")
        {
            await ConfirmExitAsync(root, sandbox, install);
            using var tx = new DirectoryUpgradeTransaction(install);
            if (tx.Phase != "None")
            {
                var priorPhase = tx.Phase;
                if (priorPhase is not ("Committed" or "RolledBack")) tx.Rollback(Restore);
                File.WriteAllText(Path.Combine(sandbox, "recovery-observed.json"), JsonSerializer.Serialize(new
                {
                    PriorPhase = priorPhase, ResolvedPhase = tx.Phase,
                    ProgramFiles = HashTree(install), Links = HashTree(links),
                    Registration = TransactionFixture.Capture(config.Identity, shortInnoIdentity: true),
                    Startup = ReadStartup(startupPath)
                }));
                tx.ArchiveResolved();
            }
            if (payload is null) throw new ArgumentException("Embedded payload is required.");
            using (var input = File.OpenRead(payload))
                if (!string.Equals(Convert.ToHexString(SHA256.HashData(input)), config.PayloadSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Embedded payload hash mismatch.");
            var stage = Path.Combine(sandbox, "SteamWatch.staging-" + Guid.NewGuid().ToString("N"));
            ExtractPayload(payload, stage);
            NoLinks(links, tree: true);
            using var startup = Registry.CurrentUser.OpenSubKey(startupPath);
            var snapshot = new Snapshot(TransactionFixture.Capture(config.Identity, shortInnoIdentity: true), LinkNames.ToDictionary(name => name,
                name => File.Exists(Path.Combine(links, name)) ? File.ReadAllBytes(Path.Combine(links, name)) : null),
                startup is not null, startup?.GetValue("SteamWatch") as string);
            tx.Prepare(stage, JsonSerializer.Serialize(snapshot));
            CrashAt("prepared");
            return 0;
        }

        if (mode == "uninstall")
        {
            VerifyActiveManagement();
            await ConfirmExitAsync(root, sandbox, install);
            CopyLegacyData(install, Path.Combine(sandbox, "data"));
            NoLinks(install, tree: true);
            if (Directory.Exists(install)) Directory.Move(install, Path.Combine(sandbox, "SteamWatch.uninstalled-" + Guid.NewGuid().ToString("N")));
            var removed = Path.Combine(sandbox, "removed-links-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(removed);
            foreach (var name in LinkNames)
                if (File.Exists(Path.Combine(links, name))) File.Move(Path.Combine(links, name), Path.Combine(removed, name));
            using var startup = Registry.CurrentUser.OpenSubKey(startupPath, writable: true);
            startup?.DeleteValue("SteamWatch", throwOnMissingValue: false);
            return 0;
        }

        if (mode == "active") { VerifyActiveManagement(); return 0; }
        using var upgrade = new DirectoryUpgradeTransaction(install);
        if (mode == "rollback")
        {
            if (upgrade.Phase is not ("None" or "Committed")) upgrade.Rollback(Restore);
            return 0;
        }
        if (mode != "finalize") throw new ArgumentException("Unknown bundle operation.");
        try
        {
            VerifyActiveManagement();
            CopyLegacyData(install, Path.Combine(sandbox, "data"));
            upgrade.SwitchDirectories(CrashAt);
            if (config.FailureMode == "after-switch") throw new IOException("Injected failure after directory switch.");
            Directory.CreateDirectory(links);
            WriteLink(Path.Combine(links, "desktop.lnk"), Path.Combine(install, "SteamWatch.exe"), install);
            if (config.FailureMode == "after-shortcut") throw new IOException("Injected failure after first shortcut update.");
            WriteLink(Path.Combine(links, "start-menu.lnk"), Path.Combine(install, "SteamWatch.exe"), install);
            WriteLink(Path.Combine(links, "uninstall.lnk"), Path.Combine(management, "unins000.exe"), management);
            var guide = Path.Combine(install, "\u64CD\u4F5C\u6307\u5357.txt");
            if (File.Exists(guide)) WriteLink(Path.Combine(links, "guide.lnk"), guide, install);
            using (var startup = Registry.CurrentUser.OpenSubKey(startupPath, writable: true))
                if (startup?.GetValue("SteamWatch") is string)
                    startup.SetValue("SteamWatch", "\"" + Path.Combine(install, "SteamWatch.exe") + "\"");
            Registry.CurrentUser.DeleteSubKey(keys[0], throwOnMissingSubKey: false);
            CrashAt("registered");
            upgrade.Commit();
            CrashAt("committed");
            return 0;
        }

        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or COMException)
        {
            upgrade.Rollback(Restore);
            throw;
        }

        void CrashAt(string point)
        {
            if (config.FailureMode == "crash-" + point) Environment.Exit(73);
        }

        void VerifyActiveManagement()
        {
            using var key = Registry.CurrentUser.OpenSubKey(keys[1]);
            var command = key?.GetValue("UninstallString") as string;
            if (command is null || !string.Equals(command.Trim('"'), Path.Combine(management, "unins000.exe"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Inactive management directory: actual [{command}], expected [{Path.Combine(management, "unins000.exe")}].");
        }

        void Restore(string json)
        {
            var snapshot = JsonSerializer.Deserialize<Snapshot>(json) ?? throw new InvalidDataException("Missing bundle snapshot.");
            if (snapshot.Links.Count != LinkNames.Length || !LinkNames.All(snapshot.Links.ContainsKey))
                throw new InvalidDataException("Shortcut snapshot escaped the fixed link list.");
            TransactionFixture.Restore(config.Identity, snapshot.Registration, shortInnoIdentity: true);
            Directory.CreateDirectory(links);
            foreach (var name in LinkNames)
            {
                var destination = Path.Combine(links, name);
                NoLinks(destination);
                if (snapshot.Links[name] is { } bytes) File.WriteAllBytes(destination, bytes);
                else if (File.Exists(destination)) File.Move(destination, Path.Combine(sandbox, "discarded-" + Guid.NewGuid().ToString("N") + ".lnk"));
            }
            using (var startup = Registry.CurrentUser.OpenSubKey(startupPath, writable: true))
            {
                if (snapshot.Startup is null) startup?.DeleteValue("SteamWatch", false);
                else
                {
                    using var target = Registry.CurrentUser.CreateSubKey(startupPath);
                    target.SetValue("SteamWatch", snapshot.Startup);
                }
            }
            if (!snapshot.StartupKeyExisted) Registry.CurrentUser.DeleteSubKey(startupPath, false);
        }
    }

    private static Dictionary<string, string> HashTree(string directory) => Directory.Exists(directory)
        ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(directory, path).Replace('\\', '/'),
            path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant())
        : [];

    private static string? ReadStartup(string path)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        return key?.GetValue("SteamWatch") as string;
    }

    private static async Task ConfirmExitAsync(string root, string sandbox, string install)
    {
        foreach (var process in Process.GetProcessesByName("SteamWatch"))
        {
            using (process)
            {
                if (!Same(process.MainModule?.FileName ?? "", Path.Combine(install, "SteamWatch.exe"))) continue;
                if (await UpgradeExitClient.RequestAsync(Path.Combine(sandbox, "data"), TimeSpan.FromSeconds(95)) != UpgradeExitResult.Prepared)
                    throw new IOException("Application did not authorize normal exit.");
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
        var descriptor = Path.Combine(sandbox, "target.json");
        if (!File.Exists(descriptor)) return;
        var target = JsonSerializer.Deserialize<Target>(File.ReadAllText(descriptor)) ?? throw new InvalidDataException("Missing fixture identity.");
        var expected = Path.Combine(sandbox, "fixture-tools", "SteamWatch.InstallationProbe.exe");
        if (!Same(expected, target.Executable)) throw new InvalidDataException("Fixture executable outside its fixed sandbox location.");
        NoLinks(expected);
        using (var a = File.OpenRead(Path.Combine(Path.GetDirectoryName(expected)!, "SteamWatch.InstallationProbe.dll")))
        using (var b = File.OpenRead(typeof(BundleFixture).Assembly.Location))
            if (!SHA256.HashData(a).SequenceEqual(SHA256.HashData(b))) throw new InvalidDataException("Fixture assembly does not match the embedded coordinator.");
        using var fixture = Process.GetProcessById(target.ProcessId);
        if (!Same(fixture.MainModule?.FileName ?? "", expected) || fixture.StartTime.ToUniversalTime().Ticks != target.StartedUtcTicks)
            throw new InvalidDataException("Fixture process identity changed.");
        if (await UpgradeExitClient.RequestAsync(Path.Combine(sandbox, "data"), TimeSpan.FromSeconds(2)) != UpgradeExitResult.Prepared)
            throw new IOException("Fixture declined or failed normal exit.");
        await fixture.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
    }

    internal static void ExtractPayload(string zipPath, string stage)
    {
        Directory.CreateDirectory(stage);
        using var zip = ZipFile.OpenRead(zipPath);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            const string prefix = "SteamWatch-win-x64/";
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Unexpected installer payload root.");
            name = name[prefix.Length..];
            if (name.Length == 0 || name == "portable.marker") continue;
            var parts = name.TrimEnd('/').Split('/');
            if (parts.Any(part => part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
                part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new InvalidDataException("Payload contains an ambiguous or invalid Windows file name.");
            var destination = Path.GetFullPath(Path.Combine(stage, name));
            if (!destination.StartsWith(stage + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                (entry.ExternalAttributes >> 16 & 0xF000) == 0xA000 || !names.Add(destination))
                throw new InvalidDataException("Payload traversal, link or duplicate entry.");
            if (name.EndsWith('/')) { Directory.CreateDirectory(destination); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = entry.Open();
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
        if (!File.Exists(Path.Combine(stage, "SteamWatch.exe"))) throw new InvalidDataException("Complete application entry missing.");
    }

    private static void CopyLegacyData(string install, string data)
    {
        NoLinks(data, tree: true);
        Directory.CreateDirectory(data);
        var legacy = Path.Combine(install, "data");
        if (!Directory.Exists(legacy)) return;
        NoLinks(legacy, tree: true);
        foreach (var file in Directory.EnumerateFiles(legacy))
        {
            var target = Path.Combine(data, Path.GetFileName(file));
            NoLinks(target);
            if (!File.Exists(target)) File.Copy(file, target);
        }
    }

    private static void WriteLink(string path, string target, string workingDirectory)
    {
        NoLinks(path);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link = shell.CreateShortcut(path);
        try { link.TargetPath = target; link.WorkingDirectory = workingDirectory; link.Save(); }
        finally { Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
    }

    private static bool Same(string first, string second) => string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
    private static void NoLinks(string path, bool tree = false)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Bundle paths cannot contain reparse points."); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        if (tree && Directory.Exists(path))
            foreach (var child in Directory.EnumerateFileSystemEntries(path)) NoLinks(child, Directory.Exists(child));
    }
}
