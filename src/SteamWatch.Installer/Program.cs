namespace SteamWatch.Installer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args) => InstallationCommands.Run(InstallationContext.CurrentUser(), args);
}
