using System.Text.Json;
using Microsoft.Win32;
using SteamWatch.Installer;

internal static class TransactionFixture
{
    private sealed record Value(string Name, RegistryValueKind Kind, JsonElement Data);
    private sealed record KeyState(string Path, bool Exists, Value[] Values);
    private const string Uninstall = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\";

    public static int Run(string sandbox, string mode)
    {
        var identityPath = Path.Combine(sandbox, "identity.txt");
        var install = Path.Combine(sandbox, "program");
        if (mode == "initialize")
        {
            if (Directory.Exists(sandbox)) throw new IOException("Use a new transaction fixture directory.");
            Directory.CreateDirectory(sandbox);
            var id = Guid.NewGuid().ToString("N");
            File.WriteAllText(identityPath, id);
            var stageName = "SteamWatch.staging-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(Path.Combine(sandbox, "stage.txt"), stageName);
            Directory.CreateDirectory(install);
            Directory.CreateDirectory(Path.Combine(sandbox, stageName));
            File.WriteAllText(Path.Combine(install, "application.txt"), "old");
            File.WriteAllText(Path.Combine(install, "legacy-data.txt"), "keep-legacy");
            File.WriteAllText(Path.Combine(sandbox, stageName, "application.txt"), "new");
            File.WriteAllText(Path.Combine(sandbox, "independent-data.txt"), "keep-independent");
            using var legacy = Registry.CurrentUser.CreateSubKey(Paths(id)[0]);
            legacy.SetValue("DisplayVersion", "old");
            legacy.SetValue("UninstallString", "isolated-old-uninstall");
            legacy.SetValue("UnknownBinary", new byte[] { 0, 128, 255 }, RegistryValueKind.Binary);
            legacy.SetValue("UnknownMultiString", new[] { "one", "two" }, RegistryValueKind.MultiString);
            legacy.SetValue("UnknownQWord", long.MaxValue - 5, RegistryValueKind.QWord);
            // Existing empty new key allows a SetValue-only ACL denial fixture later.
            using var next = Registry.CurrentUser.CreateSubKey(Paths(id)[1]);
            File.WriteAllText(Path.Combine(sandbox, "registration-before.json"), Capture(id));
            using var transaction = new DirectoryUpgradeTransaction(install);
            transaction.Prepare(Path.Combine(sandbox, stageName), Capture(id));
            return 0;
        }

        var identity = File.ReadAllText(identityPath);
        var paths = Paths(identity); // Only this fixture's two random HKCU keys are allowed.
        if (mode == "cleanup")
        {
            foreach (var path in paths) Registry.CurrentUser.DeleteSubKey(path, throwOnMissingSubKey: false);
            return 0;
        }
        using var upgrade = new DirectoryUpgradeTransaction(install);
        if (mode == "recover") { upgrade.Rollback(json => Restore(identity, json)); return 0; }
        if (mode == "observe")
        {
            File.WriteAllText(Path.Combine(sandbox, "registration-after.json"), Capture(identity));
            File.WriteAllText(Path.Combine(sandbox, "phase.txt"), upgrade.Phase);
            return 0;
        }
        if (mode is not ("prepared" or "old-renamed" or "new-renamed" or "registered" or "normal" or "registration-failure" or "registry-denied"))
            throw new ArgumentException("Unknown transaction fixture mode.");
        if (mode == "prepared") Environment.Exit(73);
        try
        {
            upgrade.SwitchDirectories(point => { if (point == mode) Environment.Exit(73); });
            using (var key = Registry.CurrentUser.OpenSubKey(paths[1], writable: true)
                ?? throw new IOException("New registration fixture key missing."))
            {
                key.SetValue("DisplayVersion", "new");
                if (mode == "registration-failure") throw new IOException("Injected failure after partial registration write.");
                key.SetValue("UninstallString", "isolated-new-uninstall");
            }
            Registry.CurrentUser.DeleteSubKey(paths[0]);
            if (mode == "registered") Environment.Exit(73);
            upgrade.Commit();
            return 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            upgrade.Rollback(json => Restore(identity, json));
            throw;
        }
    }

    internal static string[] Paths(string identity, bool shortInnoIdentity = false)
    {
        if (!Guid.TryParseExact(identity, "N", out _)) throw new InvalidDataException("Invalid fixture registry identity.");
        return new[] { Uninstall + "SteamWatch-Validation-" + identity + "-legacy",
            Uninstall + (shortInnoIdentity ? "SWV-" + identity : "SteamWatch-Validation-" + identity + "-new") + "_is1" };
    }

    private static KeyState Read(string path)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        if (key is null) return new(path, false, []);
        if (key.SubKeyCount != 0) throw new InvalidDataException("Nested fixture registry keys are not supported; nothing removed.");
        return new(path, true, key.GetValueNames().Order(StringComparer.Ordinal).Select(name =>
            new Value(name, key.GetValueKind(name), JsonSerializer.SerializeToElement(key.GetValue(name, null,
                RegistryValueOptions.DoNotExpandEnvironmentNames)))).ToArray());
    }

    internal static string Capture(string id, bool shortInnoIdentity = false) => JsonSerializer.Serialize(Paths(id, shortInnoIdentity).Select(Read).ToArray());

    internal static void Restore(string identity, string json, bool shortInnoIdentity = false)
    {
        var expectedPaths = Paths(identity, shortInnoIdentity);
        var states = JsonSerializer.Deserialize<KeyState[]>(json) ?? throw new InvalidDataException("Empty registry snapshot.");
        if (states.Length != 2 || !states.Select(state => state.Path).SequenceEqual(expectedPaths))
            throw new InvalidDataException("Registry snapshot escapes this fixture identity.");
        foreach (var state in states)
        {
            if (JsonSerializer.Serialize(Read(state.Path)) == JsonSerializer.Serialize(state)) continue;
            if (!state.Exists) { Registry.CurrentUser.DeleteSubKey(state.Path, false); continue; }
            using var key = Registry.CurrentUser.CreateSubKey(state.Path);
            foreach (var name in key.GetValueNames())
                if (!state.Values.Any(value => value.Name == name)) key.DeleteValue(name);
            foreach (var value in state.Values)
            {
                object data = value.Kind switch
                {
                    RegistryValueKind.String or RegistryValueKind.ExpandString => value.Data.GetString()!,
                    RegistryValueKind.DWord => value.Data.GetInt32(),
                    RegistryValueKind.QWord => value.Data.GetInt64(),
                    RegistryValueKind.Binary or RegistryValueKind.None => value.Data.GetBytesFromBase64(),
                    RegistryValueKind.MultiString => value.Data.Deserialize<string[]>()!,
                    _ => throw new InvalidDataException("Unsupported registry snapshot value kind.")
                };
                key.SetValue(value.Name, data, value.Kind);
            }
        }
    }
}
