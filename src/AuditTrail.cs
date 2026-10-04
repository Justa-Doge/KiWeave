using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FunctionRowRemapper
{
    internal static class AuditTrail
    {
        internal const int MaxEntries = 256;
        internal const int MaxEventLength = 64;
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "audit-trail.log"); } }
        internal static void Record(string eventName) { Record(Path, eventName); }
        internal static void Record(string path, string eventName)
        {
            if (String.IsNullOrWhiteSpace(eventName) || eventName.Length > MaxEventLength || !Regex.IsMatch(eventName, "\\A[a-z0-9-]+\\z")) return;
            try {
                var lines = Read(path); lines.Add(DateTime.UtcNow.ToString("o") + "|" + eventName);
                string full = System.IO.Path.GetFullPath(path), dir = System.IO.Path.GetDirectoryName(full); Directory.CreateDirectory(dir); string temp = full + ".tmp";
                File.WriteAllLines(temp, lines.Skip(Math.Max(0, lines.Count - MaxEntries)).ToArray(), new UTF8Encoding(false));
                if (File.Exists(full)) File.Replace(temp, full, full + ".bak", true); else File.Move(temp, full);
            } catch { }
        }
        internal static List<string> Read() { return Read(Path); }
        internal static List<string> Read(string path)
        {
            try { if (!File.Exists(path)) return new List<string>(); var lines = File.ReadAllLines(path, Encoding.UTF8).Where(IsSafeLine).ToList(); return lines.Skip(Math.Max(0, lines.Count - MaxEntries)).ToList(); } catch { return new List<string>(); }
        }
        internal static void Clear() { try { if (File.Exists(Path)) File.Delete(Path); } catch { } }
        static bool IsSafeLine(string line) { if (String.IsNullOrWhiteSpace(line) || line.Length > 100) return false; string[] parts = line.Split('|'); DateTime ignored; return parts.Length == 2 && DateTime.TryParse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out ignored) && Regex.IsMatch(parts[1], "\\A[a-z0-9-]{1,64}\\z"); }
    }
}
