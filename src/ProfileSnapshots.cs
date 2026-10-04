using System;
using System.IO;
using System.Linq;

namespace FunctionRowRemapper
{
    internal static class ProfileSnapshots
    {
        internal static string Root { get { return Path.Combine(Path.GetDirectoryName(ConfigStore.DefaultPath), "Backups", "Profiles"); } }
        internal static string Folder(string profileName)
        {
            string slug = new string((profileName ?? "profile").ToLowerInvariant().Select(c => Char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-'); if (slug.Length == 0) slug = "profile"; if (slug.Length > 40) slug = slug.Substring(0, 40); return Path.Combine(Root, slug);
        }
        internal static string Save(KeyWeaveProfile profile, ProfileCollection profiles)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            UserPreferences preferences = File.Exists(UserPreferences.DefaultPath) ? UserPreferences.Load(UserPreferences.DefaultPath) : new UserPreferences();
            string folder = Folder(profile.Name); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + ".keyweave");
            BackupBundle.Save(path, profile.Configuration, profiles, preferences, Startup.Enabled, false); Trim(folder, Math.Max(5, preferences.HistoryRetention)); return path;
        }
        static void Trim(string folder, int limit)
        {
            foreach (string path in Directory.GetFiles(folder, "*.keyweave").OrderByDescending(Path.GetFileName).Skip(limit)) try { File.Delete(path); } catch { }
        }
    }
}
