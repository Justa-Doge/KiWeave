using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FunctionRowRemapper
{
    internal static class ShortcutSuspension
    {
        static readonly object Gate = new object(); static HashSet<string> names; 
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "shortcut-suspension.txt"); } }
        internal static string[] Load()
        {
            lock (Gate) { if (names == null) { try { names = new HashSet<string>(File.Exists(Path) ? File.ReadAllLines(Path, Encoding.UTF8).Select(Normalize).Where(x => x.Length > 0) : Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase); } catch { names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); } } return names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(); }
        }
        internal static void Save(IEnumerable<string> values)
        {
            var next = new HashSet<string>((values ?? Enumerable.Empty<string>()).Select(Normalize).Where(x => x.Length > 0), StringComparer.OrdinalIgnoreCase); if (next.Count > 32) throw new ArgumentException("Choose up to 32 applications.");
            lock (Gate) { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)); string temp = Path + ".tmp"; File.WriteAllLines(temp, next.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(), Encoding.UTF8); if (File.Exists(Path)) File.Replace(temp, Path, Path + ".bak", true); else File.Move(temp, Path); names = next; }
        }
        internal static bool IsSuspended() { try { string process = Normalize(Native.ForegroundProcessName()); return process.Length > 0 && Load().Contains(process); } catch { return false; } }
        static string Normalize(string value) { string text = (value ?? "").Trim(); if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) text = text.Substring(0, text.Length - 4); return text.Length > 64 || text.Any(Char.IsControl) || text.Any(c => c == '\\' || c == '/' || c == ':') ? "" : text; }
    }
}
