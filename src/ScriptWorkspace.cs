using System;
using System.Diagnostics;
using System.IO;

namespace FunctionRowRemapper
{
    internal static class ScriptWorkspace
    {
        internal static readonly string[] Extensions = { ".py", ".ps1", ".cmd", ".bat", ".js", ".lua", ".rb", ".ahk" };
        internal static bool IsSupported(string path) { string ext = Path.GetExtension(path ?? ""); return Array.IndexOf(Extensions, ext.ToLowerInvariant()) >= 0; }
        internal static void Open(string path)
        {
            if (!File.Exists(path) || !IsSupported(path)) throw new ArgumentException("Choose a supported script file.");
            try { using (var p = Process.Start(new ProcessStartInfo("code", "--reuse-window \"" + path.Replace("\"", "") + "\"") { UseShellExecute = false, CreateNoWindow = true })) { } }
            catch { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        }
    }
}
