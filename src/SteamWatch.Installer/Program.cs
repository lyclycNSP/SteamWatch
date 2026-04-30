using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SteamWatch.Installer;

internal static class Program
{
    private const string AppName = "SteamWatch";
    private const string PayloadResourceName = "SteamWatchPayload.zip";
    private const string PayloadRootDirectory = "SteamWatch-win-x64/";
    private const string ExecutableName = "SteamWatch.exe";
    private const string InstalledInstallerName = "SteamWatchSetup.exe";
    private const string GuideName = "操作指南.txt";
    private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SteamWatch";

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Any(arg => string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                Uninstall();
                return 0;
            }

            Install();
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"SteamWatch 安装失败：{ex.Message}",
                "SteamWatch 安装器",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void Install()
    {
        var installDirectory = GetInstallDirectory();
        Directory.CreateDirectory(installDirectory);

        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName)
            ?? throw new InvalidOperationException("安装包内缺少 SteamWatch 程序文件。请重新构建安装器。");
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            var relativePath = GetInstallRelativePath(entry.FullName);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var installRoot = Path.GetFullPath(installDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var destinationPath = Path.GetFullPath(Path.Combine(installDirectory, relativePath));
            if (!destinationPath.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal)
                || entry.FullName.EndsWith("\\", StringComparison.Ordinal)
                || string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, overwrite: true);
        }

        CopyInstallerToInstallDirectory(installDirectory);
        CreateShortcuts(installDirectory);
        RegisterUninstallEntry(installDirectory);
        RefreshShellIconCache();

        MessageBox.Show(
            "SteamWatch 已安装完成。\n\n桌面和开始菜单中已创建快捷方式。\n开始菜单中也可以打开操作指南。",
            "SteamWatch 安装器",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void Uninstall()
    {
        var installDirectory = GetInstallDirectory();
        DeleteShortcut(GetDesktopShortcutPath());
        DeleteShortcut(GetStartMenuShortcutPath());
        DeleteShortcut(GetStartMenuGuideShortcutPath());
        DeleteShortcut(GetUninstallShortcutPath());

        using (var key = Registry.CurrentUser.OpenSubKey(
                   @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
                   writable: true))
        {
            key?.DeleteSubKeyTree(AppName, throwOnMissingSubKey: false);
        }

        ScheduleDirectoryRemoval(installDirectory);
        MessageBox.Show(
            "SteamWatch 已卸载。",
            "SteamWatch 安装器",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static string? GetInstallRelativePath(string zipEntryName)
    {
        var normalized = zipEntryName.Replace('\\', '/');
        if (normalized.StartsWith(PayloadRootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return normalized[PayloadRootDirectory.Length..].Replace('/', Path.DirectorySeparatorChar);
        }

        return Path.GetFileName(normalized) == "操作指南.txt"
            ? "操作指南.txt"
            : null;
    }

    private static void CopyInstallerToInstallDirectory(string installDirectory)
    {
        var currentProcessPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentProcessPath))
        {
            return;
        }

        File.Copy(
            currentProcessPath,
            Path.Combine(installDirectory, InstalledInstallerName),
            overwrite: true);
    }

    private static void CreateShortcuts(string installDirectory)
    {
        var executablePath = Path.Combine(installDirectory, ExecutableName);
        var installedInstallerPath = Path.Combine(installDirectory, InstalledInstallerName);
        var guidePath = Path.Combine(installDirectory, GuideName);
        var shortcutIconPath = GetShortcutIconPath(installDirectory);

        CreateShortcut(GetDesktopShortcutPath(), executablePath, installDirectory, shortcutIconPath, null);
        CreateShortcut(GetStartMenuShortcutPath(), executablePath, installDirectory, shortcutIconPath, null);
        if (File.Exists(guidePath))
        {
            CreateShortcut(GetStartMenuGuideShortcutPath(), guidePath, installDirectory, shortcutIconPath, null);
        }

        CreateShortcut(
            GetUninstallShortcutPath(),
            installedInstallerPath,
            installDirectory,
            shortcutIconPath,
            "--uninstall");
    }

    private static string GetShortcutIconPath(string installDirectory)
    {
        var appIconPath = Path.Combine(installDirectory, "Assets", "AppIcon.ico");
        return File.Exists(appIconPath)
            ? appIconPath
            : Path.Combine(installDirectory, ExecutableName);
    }

    private static void RegisterUninstallEntry(string installDirectory)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath);
        var executablePath = Path.Combine(installDirectory, ExecutableName);
        var installedInstallerPath = Path.Combine(installDirectory, InstalledInstallerName);

        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", "1.0.0");
        key.SetValue("Publisher", "SteamWatch");
        key.SetValue("InstallLocation", installDirectory);
        key.SetValue("DisplayIcon", executablePath);
        key.SetValue("UninstallString", $"\"{installedInstallerPath}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void CreateShortcut(
        string shortcutPath,
        string targetPath,
        string workingDirectory,
        string iconPath,
        string? arguments)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        DeleteShortcut(shortcutPath);

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("当前系统不支持创建 Windows 快捷方式。");
        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("无法创建 Windows 快捷方式服务。");
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.IconLocation = $"{iconPath},0";
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            shortcut.Arguments = arguments;
        }

        shortcut.Save();
        Marshal.FinalReleaseComObject(shortcut);
        Marshal.FinalReleaseComObject(shell);
    }

    private static void DeleteShortcut(string shortcutPath)
    {
        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }

    private static void ScheduleDirectoryRemoval(string installDirectory)
    {
        if (!Directory.Exists(installDirectory))
        {
            return;
        }

        var command = $"/c timeout /t 2 /nobreak > nul & rmdir /s /q \"{installDirectory}\"";
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = command,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static void RefreshShellIconCache()
    {
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }

    private static string GetInstallDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            AppName);
    }

    private static string GetDesktopShortcutPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"{AppName}.lnk");
    }

    private static string GetStartMenuShortcutPath()
    {
        return Path.Combine(GetStartMenuDirectory(), $"{AppName}.lnk");
    }

    private static string GetUninstallShortcutPath()
    {
        return Path.Combine(GetStartMenuDirectory(), $"卸载 {AppName}.lnk");
    }

    private static string GetStartMenuGuideShortcutPath()
    {
        return Path.Combine(GetStartMenuDirectory(), "操作指南.lnk");
    }

    private static string GetStartMenuDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            AppName);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(
        int eventId,
        uint flags,
        IntPtr item1,
        IntPtr item2);
}
