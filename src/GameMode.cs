using System;
using System.IO;
using System.Linq;
using System.Text;

namespace FunctionRowRemapper
{
    internal static class GameMode
    {
        static volatile bool enabledCache;
        static volatile bool enabledCacheInitialized;
        static volatile bool fullscreenCache;
        static volatile bool fullscreenCacheInitialized;
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "game-mode.flag"); } }
        internal static bool Enabled
        {
            get
            {
                if (!enabledCacheInitialized) RefreshEnabledState();
                return enabledCache;
            }
        }
        internal static bool EnabledCached { get { return enabledCacheInitialized && enabledCache; } }
        internal static bool RefreshEnabledState()
        {
            bool enabled = RefreshEnabledState(Path);
            RefreshFullscreenForeground();
            return enabled;
        }
        internal static bool RefreshEnabledState(string path)
        {
            bool enabled = ReadEnabled(path);
            enabledCache = enabled;
            enabledCacheInitialized = true;
            return enabled;
        }
        internal static bool ReadEnabled(string path)
        {
            try { return String.Equals(AtomicFile.ReadAllTextBounded(path, 32, Encoding.ASCII).Trim(), "enabled", StringComparison.Ordinal); }
            catch { return false; }
        }
        internal static void Set(bool enabled) { Set(Path, enabled); }
        internal static void Set(string path, bool enabled) { Write(path, enabled); enabledCache = enabled; enabledCacheInitialized = true; }
        internal static void Write(string path, bool enabled)
        {
            if (enabled) AtomicFile.WriteAllText(path, "enabled", Encoding.ASCII);
            else try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
        internal static bool IsFullscreenForeground()
        {
            if (!fullscreenCacheInitialized) RefreshFullscreenForeground();
            return fullscreenCache;
        }
        internal static bool IsFullscreenForegroundCached { get { return fullscreenCacheInitialized && fullscreenCache; } }
        static void RefreshFullscreenForeground()
        {
            bool fullscreen = false;
            try { var window = Native.GetForegroundWindow(); Native.Rect rect; Native.MonitorInfo monitor = new Native.MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.MonitorInfo)) }; fullscreen = window != IntPtr.Zero && Native.GetWindowRect(window, out rect) && Native.GetMonitorInfo(Native.MonitorFromWindow(window, 2), ref monitor) && rect.Left <= monitor.Monitor.Left && rect.Top <= monitor.Monitor.Top && rect.Right >= monitor.Monitor.Right && rect.Bottom >= monitor.Monitor.Bottom; } catch { }
            fullscreenCache = fullscreen;
            fullscreenCacheInitialized = true;
        }
        internal static bool AllowsControl(Configuration configuration, int virtualKey)
        {
            if (HotkeyChord.IsHardwareMediaKey(virtualKey)) return true;
            // Fullscreen mode must not bypass the engine for a configured function
            // key. The low-level hook needs to see every F-key first so custom
            // bindings, layers, and explicit Unbound mappings keep precedence over
            // the active application's own function-key handling.
            if (virtualKey >= 0x70 && virtualKey <= 0x7B && configuration != null && configuration.Mappings != null && configuration.Mappings.Length == 12) return true;
            if (configuration == null || !HotkeyChord.IsStandaloneKeyboardKey(virtualKey)) return false;
            return (configuration.CustomHotkeys ?? new CustomHotkey[0]).Any(hotkey => {
                HotkeyChord chord;
                try { chord = HotkeyChord.Parse(hotkey.Shortcut); } catch { return false; }
                return chord.Modifiers == 0 && chord.Key == virtualKey;
            });
        }
    }
}
