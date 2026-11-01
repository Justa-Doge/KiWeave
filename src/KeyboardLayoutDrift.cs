using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FunctionRowRemapper
{
    internal static class KeyboardLayoutDrift
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetKeyboardLayoutName(StringBuilder name);
        internal static string StatePath { get { return Path.Combine(AppStorage.DataFolder, "keyboard-layout-observed.txt"); } }
        internal static string Snapshot()
        {
            try { var buffer = new StringBuilder(16); GetKeyboardLayoutName(buffer); return buffer + "|0x" + GetKeyboardLayout(0).ToInt64().ToString("X"); }
            catch { return "Unavailable"; }
        }
        internal static string Compare(string previous, string current)
        {
            if (String.IsNullOrWhiteSpace(previous)) return "Baseline recorded";
            return String.Equals(previous, current, StringComparison.OrdinalIgnoreCase) ? "No changes since last check" : "Changed since last check";
        }
        internal static string Observe()
        {
            return Observe(StatePath, Snapshot());
        }
        internal static string Observe(string path, string current)
        {
            return LocalObservationStore.CompareAndSave(path, current, Compare);
        }
    }
}
