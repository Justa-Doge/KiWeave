using System;
using System.IO;
using System.Linq;

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

        internal static BackupReminderStatus Inspect(string folder, DateTime nowUtc, DateTime? lastNotifiedUtc)
        {
            var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.keyweave", SearchOption.AllDirectories) : new string[0];
            if (files.Length == 0) return new BackupReminderStatus { Message = "No KiWeave backup has been created yet." };
            DateTime oldest = files.Select(x => File.GetLastWriteTimeUtc(x)).Min();
            int age = Math.Max(0, (int)Math.Floor((nowUtc - oldest).TotalDays));
            bool stale = age >= ReminderAfterDays;
            bool notify = stale && (!lastNotifiedUtc.HasValue || (nowUtc - lastNotifiedUtc.Value).TotalDays >= RepeatAfterDays);
            return new BackupReminderStatus { HasBackup = true, OldestAgeDays = age, ShouldNotify = notify,
                Message = stale ? "Your oldest KiWeave backup is " + age + " days old. Consider creating a fresh backup." : "Your oldest KiWeave backup is " + age + " days old." };
        }

        internal static bool TryReadLastNotified(out DateTime value)
        {
            value = DateTime.MinValue;
            try { return DateTime.TryParse(File.ReadAllText(MarkerPath), null, System.Globalization.DateTimeStyles.RoundtripKind, out value); } catch { return false; }
        }
        internal static void MarkNotified(DateTime nowUtc)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath)); File.WriteAllText(MarkerPath, nowUtc.ToString("o"));
        }
    }
}
