using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace FunctionRowRemapper
{
    internal static class AppLog
    {
        static readonly object Gate = new object();
        internal static string DefaultPath { get { return Path.Combine(Path.GetDirectoryName(ConfigStore.DefaultPath), "keyweave.log"); } }
        internal static void Record(string component, Exception exception = null) { try { Write(DefaultPath, component, exception); } catch { } }
        internal static void Write(string path, string component, Exception exception)
        {
            string safe = String.IsNullOrWhiteSpace(component) ? "Unknown" : component.Replace("\r", " ").Replace("\n", " "); if (safe.Length > 80) safe = safe.Substring(0, 80);
            string detail = exception == null ? "event" : exception.GetType().Name + " 0x" + exception.HResult.ToString("X8");
            lock (Gate) {
                string dir = Path.GetDirectoryName(Path.GetFullPath(path)); Directory.CreateDirectory(dir);
                if (File.Exists(path) && new FileInfo(path).Length >= 256 * 1024) { string old = path + ".old"; if (File.Exists(old)) File.Delete(old); File.Move(path, old); }
                File.AppendAllText(path, DateTime.UtcNow.ToString("o") + " " + safe + " " + detail + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        internal static int RecentCount()
        {
            try { lock (Gate) return File.Exists(DefaultPath) ? File.ReadLines(DefaultPath).TakeLastCompat(200).Count : 0; } catch { return 0; }
        }
        internal static void OpenFolder()
        {
            string folder = Path.GetDirectoryName(DefaultPath); Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        internal static long TotalBytes()
        {
            try { lock (Gate) { long total = 0; if (File.Exists(DefaultPath)) total += new FileInfo(DefaultPath).Length; if (File.Exists(DefaultPath + ".old")) total += new FileInfo(DefaultPath + ".old").Length; return total; } } catch { return 0; }
        }
        internal static void Clear()
        {
            lock (Gate) { if (File.Exists(DefaultPath)) File.Delete(DefaultPath); if (File.Exists(DefaultPath + ".old")) File.Delete(DefaultPath + ".old"); }
        }
    }
    internal static class EnumerableCompatibility
    {
        internal static System.Collections.Generic.List<string> TakeLastCompat(this System.Collections.Generic.IEnumerable<string> source, int count)
        {
            var queue = new System.Collections.Generic.Queue<string>(); foreach (string item in source) { queue.Enqueue(item); if (queue.Count > count) queue.Dequeue(); } return new System.Collections.Generic.List<string>(queue);
        }
    }
}
