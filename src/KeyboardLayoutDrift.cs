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
            string current = Snapshot(), previous = "";
            try { if (File.Exists(StatePath)) previous = File.ReadAllText(StatePath, Encoding.ASCII).Trim(); } catch { }
            try { Directory.CreateDirectory(Path.GetDirectoryName(StatePath)); string temp = StatePath + ".tmp"; File.WriteAllText(temp, current, Encoding.ASCII); File.Copy(temp, StatePath, true); File.Delete(temp); } catch { }
            return Compare(previous, current);
        }
    }
}
