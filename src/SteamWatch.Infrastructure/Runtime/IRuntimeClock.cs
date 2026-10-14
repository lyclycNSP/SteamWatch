using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SteamWatch.Infrastructure.Runtime;

public interface IRuntimeClock
{
    DateTimeOffset Now { get; }
    long ActiveTicks { get; }
}

public sealed class WindowsRuntimeClock : IRuntimeClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
    public long ActiveTicks
    {
        get
        {
            if (!QueryUnbiasedInterruptTime(out var ticks)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return checked((long)ticks);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryUnbiasedInterruptTime(out ulong unbiasedTime);
}
