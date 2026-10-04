using System;
using System.IO;
using System.Text;

namespace FunctionRowRemapper
{
    internal static class GameMode
    {
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "game-mode.flag"); } }
        internal static bool Enabled { get { try { return File.Exists(Path) && File.ReadAllText(Path, Encoding.ASCII).Trim() == "enabled"; } catch { return false; } } }
        internal static void Set(bool enabled) { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)); if (enabled) File.WriteAllText(Path, "enabled", Encoding.ASCII); else try { if (File.Exists(Path)) File.Delete(Path); } catch { } }
        internal static bool IsFullscreenForeground()
        {
            try { var window = Native.GetForegroundWindow(); Native.Rect rect; Native.MonitorInfo monitor = new Native.MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.MonitorInfo)) }; return window != IntPtr.Zero && Native.GetWindowRect(window, out rect) && Native.GetMonitorInfo(Native.MonitorFromWindow(window, 2), ref monitor) && rect.Left <= monitor.Monitor.Left && rect.Top <= monitor.Monitor.Top && rect.Right >= monitor.Monitor.Right && rect.Bottom >= monitor.Monitor.Bottom; } catch { return false; }
        }
    }
}
