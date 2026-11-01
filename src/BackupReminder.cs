using System;
using System.IO;
using System.Linq;
using System.Text;

namespace FunctionRowRemapper
{
    internal sealed class BackupReminderStatus
    {
        internal bool HasBackup;
        internal int OldestAgeDays;
        internal string Message;
        internal bool ShouldNotify;
    }

    internal static class BackupReminder
    {
        const int ReminderAfterDays = 30;
        const int RepeatAfterDays = 7;
        internal static string MarkerPath { get { return Path.Combine(AppStorage.DataFolder, "backup-reminder-seen.txt"); } }

        internal static BackupReminderStatus Inspect(string folder, DateTime nowUtc, DateTime? lastNotifiedUtc, Func<string, bool> isValidBackup)
        {
            if (isValidBackup == null) throw new ArgumentNullException("isValidBackup");
            var files = AppStorage.EnumerateFilesWithoutFollowingReparsePoints(folder, "*.kiweave")
                .Concat(AppStorage.EnumerateFilesWithoutFollowingReparsePoints(folder, "*.keyweave"))
                .Concat(AppStorage.EnumerateFilesWithoutFollowingReparsePoints(folder, "*.kiweave.enc"))
                .Concat(AppStorage.EnumerateFilesWithoutFollowingReparsePoints(folder, "*.keyweave.enc"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => { try { return new { Path = path, LastWriteUtc = File.GetLastWriteTimeUtc(path) }; } catch { return null; } })
                .Where(file => file != null)
                .OrderBy(file => file.LastWriteUtc)
                .ToArray();
            DateTime? oldest = null;
            foreach (var file in files)
            {
                try
                {
                    if (!isValidBackup(file.Path)) continue;
                    oldest = file.LastWriteUtc;
                    break;
                }
                catch { /* Ignore malformed files; only valid full backups count toward the reminder. */ }
            }
            if (!oldest.HasValue) return new BackupReminderStatus { Message = "No KiWeave backup has been created yet." };
            int age = Math.Max(0, (int)Math.Floor((nowUtc - oldest.Value).TotalDays));
            bool stale = age >= ReminderAfterDays;
            bool notify = stale && (!lastNotifiedUtc.HasValue || (nowUtc - lastNotifiedUtc.Value).TotalDays >= RepeatAfterDays);
            return new BackupReminderStatus { HasBackup = true, OldestAgeDays = age, ShouldNotify = notify,
                Message = stale ? "Your oldest KiWeave backup is " + age + " days old. Consider creating a fresh backup." : "Your oldest KiWeave backup is " + age + " days old." };
        }

        internal static bool TryReadLastNotified(out DateTime value)
        {
            return TryReadLastNotified(MarkerPath, out value);
        }
        internal static bool TryReadLastNotified(string path, out DateTime value)
        {
            value = DateTime.MinValue;
            try { return DateTime.TryParse(AtomicFile.ReadAllTextBounded(path, 128, Encoding.ASCII), null, System.Globalization.DateTimeStyles.RoundtripKind, out value); } catch { return false; }
        }
        internal static void MarkNotified(DateTime nowUtc)
        {
            MarkNotified(MarkerPath, nowUtc);
        }
        internal static void MarkNotified(string path, DateTime nowUtc)
        {
            AtomicFile.WriteAllText(path, nowUtc.ToString("o"), Encoding.ASCII);
        }
    }
}
