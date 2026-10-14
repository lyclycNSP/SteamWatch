using Microsoft.Win32;
using System.Text.Json;

namespace SteamWatch.Installer;

internal sealed record RegistryValueState(string Name, RegistryValueKind Kind, JsonElement Data)
{
    internal object Value => Kind switch
    {
        RegistryValueKind.String or RegistryValueKind.ExpandString => Data.GetString()!,
        RegistryValueKind.DWord => Data.GetInt32(),
        RegistryValueKind.QWord => Data.GetInt64(),
        RegistryValueKind.Binary or RegistryValueKind.None => Data.GetBytesFromBase64(),
        RegistryValueKind.MultiString => Data.Deserialize<string[]>()!,
        _ => throw new InvalidDataException("Unsupported registry snapshot value type.")
    };

    internal static RegistryValueState? Read(RegistryKey key, string name) => key.GetValueNames().Contains(name)
        ? new(name, key.GetValueKind(name), JsonSerializer.SerializeToElement(
            key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames))) : null;
}

internal sealed record RegistryKeyState(string Path, bool Exists, RegistryValueState[] Values)
{
    internal static RegistryKeyState Read(string path)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        if (key is null) return new(path, false, []);
        if (key.SubKeyCount != 0) throw new InvalidDataException("Unexpected nested uninstall key; nothing removed.");
        return new(path, true, key.GetValueNames().Order(StringComparer.Ordinal)
            .Select(name => RegistryValueState.Read(key, name)!).ToArray());
    }

    internal void Restore()
    {
        if (JsonSerializer.Serialize(Read(Path)) == JsonSerializer.Serialize(this)) return;
        if (!Exists) { Registry.CurrentUser.DeleteSubKey(Path, false); return; }
        using var key = Registry.CurrentUser.CreateSubKey(Path);
        foreach (var name in key.GetValueNames())
            if (!Values.Any(value => value.Name == name)) key.DeleteValue(name);
        foreach (var value in Values) key.SetValue(value.Name, value.Value, value.Kind);
    }
}

internal sealed record RegistrationSnapshot(RegistryKeyState Legacy, RegistryKeyState Inno,
    Dictionary<string, byte[]?> Links, RegistryValueState? Startup)
{
    internal static RegistrationSnapshot Capture(InstallationContext context)
    {
        foreach (var link in context.Links) PathSafety.NoLinks(link);
        using var key = Registry.CurrentUser.OpenSubKey(context.StartupKey);
        return new(RegistryKeyState.Read(context.LegacyKey), RegistryKeyState.Read(context.InnoKey),
            context.Links.ToDictionary(path => path, path => File.Exists(path) ? File.ReadAllBytes(path) : null),
            key is null ? null : RegistryValueState.Read(key, "SteamWatch"));
    }

    internal void Restore(InstallationContext context)
    {
        if (Legacy.Path != context.LegacyKey || Inno.Path != context.InnoKey ||
            Links.Count != context.Links.Length || !context.Links.All(Links.ContainsKey) ||
            Startup is { Name: not "SteamWatch" })
            throw new InvalidDataException("Snapshot escaped the fixed installation identity.");
        Legacy.Restore();
        Inno.Restore();
        foreach (var (path, bytes) in Links)
        {
            PathSafety.NoLinks(path);
            if (bytes is null) { if (File.Exists(path)) File.Delete(path); }
            else { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes); }
        }
        using var current = Registry.CurrentUser.OpenSubKey(context.StartupKey);
        var currentValue = current is null ? null : RegistryValueState.Read(current, "SteamWatch");
        if (JsonSerializer.Serialize(currentValue) == JsonSerializer.Serialize(Startup)) return;
        if (Startup is null)
        {
            using var key = Registry.CurrentUser.OpenSubKey(context.StartupKey, writable: true);
            key?.DeleteValue("SteamWatch", false);
        }
        else
        {
            using var key = Registry.CurrentUser.CreateSubKey(context.StartupKey);
            key.SetValue("SteamWatch", Startup.Value, Startup.Kind);
        }
    }
}
