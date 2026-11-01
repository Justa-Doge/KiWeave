using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace FunctionRowRemapper
{
    internal static class ShortcutSuspension
    {
        sealed class ForegroundSnapshot
        {
            readonly string[] processNames;
            readonly bool settingsUnavailable, foregroundUnavailable;
            readonly string foregroundProcess;
            internal ForegroundSnapshot(IEnumerable<string> names, bool settingsUnavailable, string foregroundProcess, bool foregroundUnavailable)
            {
                processNames = names == null ? new string[0] : names.ToArray();
                this.settingsUnavailable = settingsUnavailable; this.foregroundProcess = foregroundProcess; this.foregroundUnavailable = foregroundUnavailable;
            }
            internal bool IsSuspended { get { return settingsUnavailable || foregroundUnavailable || foregroundProcess == null || (foregroundProcess.Length > 0 && processNames.Any(name => String.Equals(name, foregroundProcess, StringComparison.OrdinalIgnoreCase))); } }
        }
        static readonly object Gate = new object(); static HashSet<string> names; static bool unavailable, recovered;
        static string foregroundProcessName; static bool foregroundProcessKnown, foregroundProcessUnavailable;
        static ForegroundSnapshot foregroundSnapshot;
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "shortcut-suspension.txt"); } }
        internal static string[] Load()
        {
            lock (Gate) { EnsureLoadedLocked(); return names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(); }
        }
        static void EnsureLoadedLocked() { if (names == null) { string[] loaded = LoadFromPath(Path, out recovered, out unavailable); names = new HashSet<string>(loaded, StringComparer.OrdinalIgnoreCase); PublishSnapshotLocked(); } }
        internal static string Warning { get { Load(); lock (Gate) { return unavailable ? "Shortcut suspension settings could not be read; all remapping is paused until they are restored or explicitly replaced." : recovered ? "Shortcut suspension settings were recovered from the previous save; save to replace the damaged file." : ""; } } }
        internal static string[] LoadFromPath(string path, out bool wasRecovered, out bool wasUnavailable)
        {
            wasRecovered = false; wasUnavailable = false;
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return new string[0];
            try { return ReadNames(path); }
            catch { try { string[] backup = ReadNames(path + ".bak"); wasRecovered = true; return backup; } catch { wasUnavailable = true; return new string[0]; } }
        }
        static string[] ReadNames(string path)
        {
            string text = AtomicFile.ReadAllTextBounded(path, 4096, new UTF8Encoding(true, true));
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)) {
                if (String.IsNullOrWhiteSpace(line)) continue;
                string name = Normalize(line); if (name.Length == 0) throw new InvalidDataException("Shortcut suspension contains an invalid process name.");
                result.Add(name); if (result.Count > 32) throw new InvalidDataException("Shortcut suspension contains too many process names.");
            }
            return result.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        internal static void Save(IEnumerable<string> values)
        {
            var next = new HashSet<string>(NormalizeNames(values), StringComparer.OrdinalIgnoreCase);
            lock (Gate) { WriteNames(Path, next); names = next; unavailable = recovered = false; PublishSnapshotLocked(); }
        }
        internal static string[] NormalizeNames(IEnumerable<string> values)
        {
            var next = new HashSet<string>((values ?? Enumerable.Empty<string>()).Select(Normalize).Where(x => x.Length > 0), StringComparer.OrdinalIgnoreCase);
            if (next.Count > 32) throw new ArgumentException("Choose up to 32 applications.");
            return next.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        internal static void SaveToPath(string path, IEnumerable<string> values) { WriteNames(path, new HashSet<string>(NormalizeNames(values), StringComparer.OrdinalIgnoreCase)); }
        static void WriteNames(string path, IEnumerable<string> values) { AtomicFile.WriteAllText(path, String.Join(Environment.NewLine, values.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)), new UTF8Encoding(false), ".bak"); }
        internal static bool IsSuspended()
        {
            bool refresh;
            lock (Gate) { EnsureLoadedLocked(); refresh = !foregroundProcessKnown; }
            if (refresh) RefreshForegroundState();
            return IsSuspendedCached();
        }
        // The keyboard hook uses this lock-free snapshot only. Disk reads and foreground-process
        // queries are refreshed by the UI/runtime outside the low-level hook callback.
        internal static bool IsSuspendedCached()
        {
            ForegroundSnapshot current = Volatile.Read(ref foregroundSnapshot);
            return current == null || current.IsSuspended;
        }
        static void PublishSnapshotLocked()
        {
            Volatile.Write(ref foregroundSnapshot, names == null ? null : new ForegroundSnapshot(names, unavailable, foregroundProcessKnown ? foregroundProcessName : null, !foregroundProcessKnown || foregroundProcessUnavailable));
        }
        internal static bool RefreshForegroundState()
        {
            string processName;
            bool available = Native.TryForegroundProcessName(out processName);
            lock (Gate)
            {
                EnsureLoadedLocked();
                foregroundProcessName = available ? Normalize(processName) : null;
                foregroundProcessUnavailable = !available;
                foregroundProcessKnown = true;
                PublishSnapshotLocked();
                return unavailable || foregroundProcessUnavailable || foregroundProcessName == null || (foregroundProcessName.Length > 0 && names.Contains(foregroundProcessName));
            }
        }
        internal static bool IsSuspendedForForeground(string processName, IEnumerable<string> suspendedNames, bool settingsUnavailable)
        {
            return settingsUnavailable || processName == null || IsProcessSuspended(processName, suspendedNames);
        }
        internal static bool IsSuspendedSafely(Func<string[]> loadNames, Func<string> foregroundName, Func<bool> settingsUnavailable)
        {
            try {
                if (loadNames == null || foregroundName == null || settingsUnavailable == null) return true;
                string[] current = loadNames();
                if (settingsUnavailable()) return true;
                string foreground = foregroundName();
                return foreground == null || IsProcessSuspended(foreground, current);
            } catch { return true; }
        }
        internal static bool IsProcessSuspended(string processName, IEnumerable<string> suspendedNames)
        {
            string process = Normalize(processName);
            if (process.Length == 0 || suspendedNames == null) return false;
            foreach (string value in suspendedNames) { string name = Normalize(value); if (String.Equals(name, process, StringComparison.OrdinalIgnoreCase)) return true; }
            return false;
        }
        internal static bool IsProcessSuspended(string processName, IEnumerable<string> suspendedNames, bool settingsUnavailable) { return settingsUnavailable || IsProcessSuspended(processName, suspendedNames); }
        static string Normalize(string value) { string text = (value ?? "").Trim(); if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) text = text.Substring(0, text.Length - 4); return text.Length > 64 || text.Any(Char.IsControl) || text.Any(c => c == '\\' || c == '/' || c == ':') ? "" : text; }
    }
}
