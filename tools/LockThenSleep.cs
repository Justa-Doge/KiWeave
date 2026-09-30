using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

internal static class LockThenSleepOnce
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();

    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool SetSuspendState(
        [MarshalAs(UnmanagedType.U1)] bool hibernate,
        [MarshalAs(UnmanagedType.U1)] bool force,
        [MarshalAs(UnmanagedType.U1)] bool disableWakeEvents);

    private static void Main()
    {
        string log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lock-sleep.log");
        Thread.Sleep(10000);
        if (!LockWorkStation())
        {
            File.AppendAllText(log, DateTime.Now + " Lock request failed: " + Marshal.GetLastWin32Error() + Environment.NewLine);
            return;
        }
        Thread.Sleep(1500);
        File.AppendAllText(log, DateTime.Now + " Lock accepted; requesting sleep." + Environment.NewLine);
        bool result = SetSuspendState(false, false, false);
        File.AppendAllText(log, DateTime.Now + " Suspend call returned: " + result + Environment.NewLine);
    }
}
