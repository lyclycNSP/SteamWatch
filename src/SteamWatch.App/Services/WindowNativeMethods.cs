using System.Runtime.InteropServices;

namespace SteamWatch.App.Services;

internal static class WindowNativeMethods
{
    public const int SwHide = 0;
    public const int SwShow = 5;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
