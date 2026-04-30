using System.Runtime.InteropServices;

namespace SteamWatch.App.Services;

internal sealed class TrayIconService : IDisposable
{
    private const int OpenCommandId = 1001;
    private const int PauseCommandId = 1002;
    private const int SettingsCommandId = 1003;
    private const int ExitCommandId = 1004;

    private readonly Action _open;
    private readonly Action _togglePause;
    private readonly Action _settings;
    private readonly Action _exit;
    private readonly Func<bool> _isPaused;
    private readonly NativeMethods.WndProc _wndProc;
    private readonly IntPtr _windowHandle;
    private readonly IntPtr _iconHandle;
    private bool _disposed;

    public TrayIconService(
        string tooltip,
        string iconPath,
        Action open,
        Action togglePause,
        Action settings,
        Action exit,
        Func<bool> isPaused)
    {
        _open = open;
        _togglePause = togglePause;
        _settings = settings;
        _exit = exit;
        _isPaused = isPaused;
        _wndProc = WndProc;

        var className = $"SteamWatchTrayWindow-{Guid.NewGuid():N}";
        NativeMethods.RegisterMessageWindowClass(className, _wndProc);
        _windowHandle = NativeMethods.CreateMessageWindow(className);
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        _iconHandle = NativeMethods.LoadIconFromFile(iconPath);
        NativeMethods.AddTrayIcon(_windowHandle, _iconHandle, tooltip);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        NativeMethods.DeleteTrayIcon(_windowHandle);
        if (_iconHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_iconHandle);
        }

        if (_windowHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_windowHandle);
        }

        _disposed = true;
    }

    private IntPtr WndProc(IntPtr hWnd, uint message, UIntPtr wParam, IntPtr lParam)
    {
        if (message == NativeMethods.TrayCallbackMessage)
        {
            var mouseMessage = lParam.ToInt32();
            if (mouseMessage == NativeMethods.WmLButtonDblClk)
            {
                _open();
            }
            else if (mouseMessage == NativeMethods.WmRButtonUp)
            {
                ShowContextMenu(hWnd);
            }

            return IntPtr.Zero;
        }

        if (message == NativeMethods.WmCommand)
        {
            var commandId = (int)(wParam.ToUInt32() & 0xffff);
            switch (commandId)
            {
                case OpenCommandId:
                    _open();
                    break;
                case PauseCommandId:
                    _togglePause();
                    break;
                case SettingsCommandId:
                    _settings();
                    break;
                case ExitCommandId:
                    _exit();
                    break;
            }

            return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, message, wParam, lParam);
    }

    private void ShowContextMenu(IntPtr hWnd)
    {
        var menu = NativeMethods.CreatePopupMenu();
        NativeMethods.AppendMenu(menu, NativeMethods.MfString, OpenCommandId, "打开 SteamWatch");
        NativeMethods.AppendMenu(menu, NativeMethods.MfString, PauseCommandId, _isPaused() ? "恢复监控" : "暂停监控");
        NativeMethods.AppendMenu(menu, NativeMethods.MfString, SettingsCommandId, "设置");
        NativeMethods.AppendMenu(menu, NativeMethods.MfSeparator, 0, string.Empty);
        NativeMethods.AppendMenu(menu, NativeMethods.MfString, ExitCommandId, "退出");

        NativeMethods.GetCursorPos(out var point);
        NativeMethods.SetForegroundWindow(hWnd);
        NativeMethods.TrackPopupMenu(menu, NativeMethods.TpmRightButton, point.X, point.Y, 0, hWnd, IntPtr.Zero);
        NativeMethods.DestroyMenu(menu);
    }

    private static class NativeMethods
    {
        public const uint TrayCallbackMessage = 0x8000 + 1;
        public const int WmCommand = 0x0111;
        public const int WmRButtonUp = 0x0205;
        public const int WmLButtonDblClk = 0x0203;
        public const uint MfString = 0x0000;
        public const uint MfSeparator = 0x0800;
        public const uint TpmRightButton = 0x0002;

        private const uint NifMessage = 0x00000001;
        private const uint NifIcon = 0x00000002;
        private const uint NifTip = 0x00000004;
        private const uint NimAdd = 0x00000000;
        private const uint NimDelete = 0x00000002;
        private const int ImageIcon = 1;
        private const int LrLoadFromFile = 0x0010;

        public delegate IntPtr WndProc(IntPtr hWnd, uint message, UIntPtr wParam, IntPtr lParam);

        public static void RegisterMessageWindowClass(string className, WndProc wndProc)
        {
            var windowClass = new WndClass
            {
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
                lpszClassName = className
            };

            RegisterClass(ref windowClass);
        }

        public static IntPtr CreateMessageWindow(string className)
        {
            return CreateWindowEx(
                0,
                className,
                string.Empty,
                0,
                0,
                0,
                0,
                0,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);
        }

        public static IntPtr LoadIconFromFile(string iconPath)
        {
            if (!File.Exists(iconPath))
            {
                return IntPtr.Zero;
            }

            return LoadImage(IntPtr.Zero, iconPath, ImageIcon, 0, 0, LrLoadFromFile);
        }

        public static void AddTrayIcon(IntPtr windowHandle, IntPtr iconHandle, string tooltip)
        {
            var data = CreateNotifyIconData(windowHandle, iconHandle, tooltip);
            Shell_NotifyIcon(NimAdd, ref data);
        }

        public static void DeleteTrayIcon(IntPtr windowHandle)
        {
            var data = new NotifyIconData
            {
                cbSize = Marshal.SizeOf<NotifyIconData>(),
                hWnd = windowHandle,
                uID = 1
            };
            Shell_NotifyIcon(NimDelete, ref data);
        }

        private static NotifyIconData CreateNotifyIconData(IntPtr windowHandle, IntPtr iconHandle, string tooltip)
        {
            return new NotifyIconData
            {
                cbSize = Marshal.SizeOf<NotifyIconData>(),
                hWnd = windowHandle,
                uID = 1,
                uFlags = NifMessage | NifIcon | NifTip,
                uCallbackMessage = TrayCallbackMessage,
                hIcon = iconHandle,
                szTip = tooltip
            };
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClass(ref WndClass lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll")]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadImage(IntPtr hInst, string name, int type, int cx, int cy, int fuLoad);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool AppendMenu(IntPtr hMenu, uint uFlags, int uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        public static extern bool TrackPopupMenu(
            IntPtr hMenu,
            uint uFlags,
            int x,
            int y,
            int nReserved,
            IntPtr hWnd,
            IntPtr prcRect);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WndClass
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NotifyIconData
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }
    }
}
