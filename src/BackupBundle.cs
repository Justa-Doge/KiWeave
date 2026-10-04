using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Linq;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    internal sealed class KeyWeaveBackup
    {
        public Configuration Configuration;
        public ProfileCollection Profiles;
        public UserPreferences Preferences;
        public bool StartWithWindows;
        public DateTime CreatedUtc;
        public PowerToysBackupItem[] PowerToys = new PowerToysBackupItem[0];
    }
    internal sealed class PowerToysBackupItem { public string Module, Action, Chord; public bool Enabled; }

    internal static class BackupBundle
    {
        internal const int MaxBytes = 3 * 1024 * 1024;
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 32 };
        static readonly byte[] BackupEntropy = Encoding.UTF8.GetBytes("KiWeave encrypted backup v1");

        internal static string Describe(KeyWeaveBackup backup)
        {
            return backup.Configuration.CustomHotkeys.Length + " custom hotkeys, " + backup.Configuration.Layers.Length + " modifier layers, " + backup.Profiles.Profiles.Length +
                " profiles, " + backup.PowerToys.Length + " PowerToys shortcuts recorded, network " + (backup.Preferences.NetworkAccess ? "allowed" : "blocked") + ", tray " + (backup.Preferences.UseTray ? "on" : "off") + ", startup " + (backup.StartWithWindows ? "on" : "off") +
                ". Created " + backup.CreatedUtc.ToLocalTime().ToString("g") + ".";
        }

        internal static string Serialize(Configuration configuration, ProfileCollection profiles, UserPreferences preferences, bool startup)
        {
            return Serialize(configuration, profiles, preferences, startup, true);
        }
        internal static string Serialize(Configuration configuration, ProfileCollection profiles, UserPreferences preferences, bool startup, bool includePowerToys)
        {
            ConfigStore.Validate(configuration, false); ProfileStore.Validate(profiles, false);
            PowerToysBackupItem[] powerToys; try { powerToys = includePowerToys ? PowerToysIntegration.Load().Take(256).Select(x => new PowerToysBackupItem { Module = x.Module, Action = x.Action, Chord = x.Chord, Enabled = x.ModuleEnabled }).ToArray() : new PowerToysBackupItem[0]; } catch { powerToys = new PowerToysBackupItem[0]; }
            var payload = new Dictionary<string, object> {
                { "format", "KeyWeave Backup" }, { "version", 1 }, { "createdUtc", DateTime.UtcNow.ToString("o") },
                { "configuration", ConfigStore.Serialize(configuration) }, { "profiles", ProfileStore.Serialize(profiles) },
                { "preferences", UserPreferences.Serialize(preferences) }, { "startWithWindows", startup },
                { "powerToys", powerToys.Select(x => new Dictionary<string, object> { { "module", x.Module }, { "action", x.Action }, { "shortcut", x.Chord }, { "enabled", x.Enabled } }).ToArray() }
            };
            string json = Json.Serialize(payload);
            if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("This backup exceeds the 3 MB safety limit.");
            return json;
        }

        internal static KeyWeaveBackup Parse(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("Invalid or oversized KeyWeave backup.");
            JsonSyntax.Check(json); var root = Json.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null || root.Count != 8 || !root.ContainsKey("format") || !root.ContainsKey("version") || !root.ContainsKey("createdUtc") ||
                !root.ContainsKey("configuration") || !root.ContainsKey("profiles") || !root.ContainsKey("preferences") || !root.ContainsKey("startWithWindows") ||
                !root.ContainsKey("powerToys") || !(root["powerToys"] is object[]) ||
                !(root["format"] is string) || (string)root["format"] != "KeyWeave Backup" || !(root["version"] is int) || (int)root["version"] != 1 ||
                !(root["createdUtc"] is string) || !(root["configuration"] is string) || !(root["profiles"] is string) || !(root["preferences"] is string) || !(root["startWithWindows"] is bool))
                throw new ArgumentException("This is not a supported KeyWeave backup.");
            DateTime created; if (!DateTime.TryParse((string)root["createdUtc"], null, System.Globalization.DateTimeStyles.RoundtripKind, out created)) throw new ArgumentException("Backup creation time is invalid.");
            var powerToys = ((object[])root["powerToys"]).Select(item => {
                var d = item as Dictionary<string, object>;
                if (d == null || d.Count != 4 || !(d.ContainsKey("module") && d["module"] is string) || !(d.ContainsKey("action") && d["action"] is string) || !(d.ContainsKey("shortcut") && d["shortcut"] is string) || !(d.ContainsKey("enabled") && d["enabled"] is bool)) throw new ArgumentException("Invalid PowerToys inventory in backup.");
                string module = (string)d["module"], action = (string)d["action"], chord = (string)d["shortcut"];
                if (module.Length > 200 || action.Length > 300 || chord.Length > 100 || module.Any(Char.IsControl) || action.Any(Char.IsControl) || chord.Any(Char.IsControl)) throw new ArgumentException("Invalid PowerToys inventory in backup.");
                return new PowerToysBackupItem { Module = module, Action = action, Chord = chord, Enabled = (bool)d["enabled"] };
            }).ToArray();
            if (powerToys.Length > 256) throw new ArgumentException("PowerToys inventory is too large.");
            return new KeyWeaveBackup { Configuration = ConfigStore.Parse((string)root["configuration"]), Profiles = ProfileStore.Parse((string)root["profiles"]),
                Preferences = UserPreferences.Parse((string)root["preferences"]), StartWithWindows = (bool)root["startWithWindows"], CreatedUtc = created, PowerToys = powerToys };
        }

        internal static KeyWeaveBackup Load(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length > MaxBytes) throw new ArgumentException("Invalid or oversized KeyWeave backup.");
            return Parse(File.ReadAllText(path, Encoding.UTF8));
        }

        internal static void Save(string path, Configuration configuration, ProfileCollection profiles, UserPreferences preferences, bool startup)
        {
            Save(path, configuration, profiles, preferences, startup, true);
        }
        internal static void Save(string path, Configuration configuration, ProfileCollection profiles, UserPreferences preferences, bool startup, bool includePowerToys)
        {
            string json = Serialize(configuration, profiles, preferences, startup, includePowerToys); string full = Path.GetFullPath(path), dir = Path.GetDirectoryName(full);
            Directory.CreateDirectory(dir); string temp = Path.Combine(dir, ".keyweave-" + Guid.NewGuid().ToString("N") + ".tmp");
            try { File.WriteAllText(temp, json, new UTF8Encoding(false)); if (File.Exists(full)) File.Replace(temp, full, full + ".bak", true); else File.Move(temp, full); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        internal static void SaveEncrypted(string path, Configuration configuration, ProfileCollection profiles, UserPreferences preferences, bool startup)
        {
            byte[] plain = Encoding.UTF8.GetBytes(Serialize(configuration, profiles, preferences, startup, true)); byte[] cipher = ProtectedData.Protect(plain, BackupEntropy, DataProtectionScope.CurrentUser); string full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)); File.WriteAllBytes(full, cipher);
        }
        internal static KeyWeaveBackup LoadEncrypted(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length > MaxBytes * 2) throw new ArgumentException("Invalid or oversized encrypted backup.");
            try { return Parse(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), BackupEntropy, DataProtectionScope.CurrentUser))); } catch (CryptographicException) { throw new ArgumentException("This encrypted backup belongs to a different Windows account or is damaged."); }
        }

        internal static string Restore(KeyWeaveBackup backup)
        {
            if (backup == null) throw new ArgumentNullException("backup");
            string folder = Path.Combine(Path.GetDirectoryName(ConfigStore.DefaultPath), "Backups", DateTime.Now.ToString("yyyyMMdd-HHmmssfff")); Directory.CreateDirectory(folder);
            Snapshot(ConfigStore.DefaultPath, Path.Combine(folder, "config.json")); Snapshot(ProfileStore.DefaultPath, Path.Combine(folder, "profiles.json")); Snapshot(UserPreferences.DefaultPath, Path.Combine(folder, "preferences.json"));
            bool originalStartup = Startup.Enabled;
            try { ConfigStore.Save(ConfigStore.DefaultPath, backup.Configuration); ProfileStore.Save(ProfileStore.DefaultPath, backup.Profiles); UserPreferences.Save(UserPreferences.DefaultPath, backup.Preferences); Startup.Set(backup.StartWithWindows); }
            catch { RestoreSnapshot(Path.Combine(folder, "config.json"), ConfigStore.DefaultPath); RestoreSnapshot(Path.Combine(folder, "profiles.json"), ProfileStore.DefaultPath); RestoreSnapshot(Path.Combine(folder, "preferences.json"), UserPreferences.DefaultPath); try { Startup.Set(originalStartup); } catch { } throw; }
            return folder;
        }
        static void Snapshot(string source, string target) { if (File.Exists(source)) File.Copy(source, target, false); else File.WriteAllBytes(target + ".missing", new byte[0]); }
        static void RestoreSnapshot(string snapshot, string target) { if (File.Exists(snapshot)) File.Copy(snapshot, target, true); else if (File.Exists(snapshot + ".missing") && File.Exists(target)) File.Delete(target); }
    }
}
