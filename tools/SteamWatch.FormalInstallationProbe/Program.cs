using SteamWatch.Installer;
using SteamWatch.Infrastructure.Runtime;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 3 || !Guid.TryParseExact(args[1], "N", out _)) return 90;
        var root = Path.GetFullPath(args[0]);
        if (File.ReadAllText(Path.Combine(root, "validation.scope")).Trim() != args[1]) return 91;
        var context = new InstallationContext(Path.Combine(root, "Programs", "SteamWatch"), Path.Combine(root, "SteamWatch"),
            Path.Combine(root, "Programs", "SteamWatch.Installers"), Path.Combine(root, "Desktop", "SteamWatch.lnk"),
            Path.Combine(root, "StartMenu", "SteamWatch"), InstallationContext.UninstallRoot + "SWT-" + args[1] + "-old",
            InstallationContext.UninstallRoot + "SWT-" + args[1] + "_is1", @"Software\SteamWatch\Validation\" + args[1] + @"\Run");
        if (args[2] != "serve") return InstallationCommands.Run(context, args[2..]);
        // A separate, bounded process fixture; no production helper exposes this command.
        var mode = args[3];
        using var server = mode == "no-pipe" ? null : new UpgradeExitServer(context.DataDirectory,
            _ => mode == "save-failed" ? throw new IOException("Controlled save failure") : Task.FromResult(mode == "accept"),
            () => Environment.Exit(0), () => { });
        File.WriteAllText(Path.Combine(root, "server-ready.txt"), Environment.ProcessId.ToString());
        Thread.Sleep(TimeSpan.FromSeconds(20));
        return 0;
    }
}
