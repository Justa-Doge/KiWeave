using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    public sealed class UserPreferences
    {
        public bool UseTray = true;
        public bool CheckUpdates = true;
        public bool AutomaticProfiles = true;
        public bool NetworkAccess = false;
        public string Theme = "KiWeave Dark";
        public string CustomAccent = "";
        public int HistoryRetention = 20;
        public static string DefaultPath { get { return Path.Combine(Path.GetDirectoryName(ConfigStore.DefaultPath), "preferences.json"); } }
        public static UserPreferences Parse(string json)
        {
            if (json == null || json.Length > 1024) throw new ArgumentException("Invalid preferences size.");
            JsonSyntax.Check(json);
            var d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (d == null || !d.ContainsKey("version") || !(d["version"] is int)) throw new ArgumentException("Invalid preferences version.");
            int version = (int)d["version"];
            if (version == 1) {
                if (d.Count != 2 || !d.ContainsKey("useTray") || !(d["useTray"] is bool)) throw new ArgumentException("Invalid version 1 preferences.");
                return new UserPreferences { UseTray = (bool)d["useTray"] };
            }
            if (version == 2) {
                if (d.Count != 4 || !d.ContainsKey("useTray") || !d.ContainsKey("checkUpdates") || !d.ContainsKey("automaticProfiles") ||
                    !(d["useTray"] is bool) || !(d["checkUpdates"] is bool) || !(d["automaticProfiles"] is bool))
                    throw new ArgumentException("Invalid version 2 preferences.");
                return new UserPreferences { UseTray = (bool)d["useTray"], CheckUpdates = (bool)d["checkUpdates"], AutomaticProfiles = (bool)d["automaticProfiles"], NetworkAccess = (bool)d["checkUpdates"] };
            }
            if (version == 3 && d.Count == 5 && d.ContainsKey("useTray") && d.ContainsKey("checkUpdates") && d.ContainsKey("automaticProfiles") && d.ContainsKey("networkAccess") &&
                d["useTray"] is bool && d["checkUpdates"] is bool && d["automaticProfiles"] is bool && d["networkAccess"] is bool)
                return new UserPreferences { UseTray = (bool)d["useTray"], CheckUpdates = (bool)d["checkUpdates"], AutomaticProfiles = (bool)d["automaticProfiles"], NetworkAccess = (bool)d["networkAccess"], Theme = "KiWeave Dark" };
            if (version == 4 && d.Count == 6 && d.ContainsKey("useTray") && d.ContainsKey("checkUpdates") && d.ContainsKey("automaticProfiles") && d.ContainsKey("networkAccess") && d.ContainsKey("theme") &&
                d["useTray"] is bool && d["checkUpdates"] is bool && d["automaticProfiles"] is bool && d["networkAccess"] is bool && d["theme"] is string)
                return new UserPreferences { UseTray = (bool)d["useTray"], CheckUpdates = (bool)d["checkUpdates"], AutomaticProfiles = (bool)d["automaticProfiles"], NetworkAccess = (bool)d["networkAccess"], Theme = (string)d["theme"] };
            if (version == 5 && (d.Count != 7 || !d.ContainsKey("useTray") || !d.ContainsKey("checkUpdates") || !d.ContainsKey("automaticProfiles") || !d.ContainsKey("networkAccess") || !d.ContainsKey("theme") || !d.ContainsKey("customAccent") ||
                !(d["useTray"] is bool) || !(d["checkUpdates"] is bool) || !(d["automaticProfiles"] is bool) || !(d["networkAccess"] is bool)))
                throw new ArgumentException("Invalid preferences. Expected version 5 settings.");
            if (version == 5) {
                if (!(d["theme"] is string) || Array.IndexOf(new[] { "KiWeave Dark", "Midnight Blue", "Plum", "Glass" }, (string)d["theme"]) < 0) throw new ArgumentException("Unknown KiWeave theme.");
                if (!(d["customAccent"] is string) || ((string)d["customAccent"]).Length > 7 || ((string)d["customAccent"]).Length != 0 && !System.Text.RegularExpressions.Regex.IsMatch((string)d["customAccent"], "\\A#[0-9a-fA-F]{6}\\z")) throw new ArgumentException("Invalid custom accent color.");
                return new UserPreferences { UseTray = (bool)d["useTray"], CheckUpdates = (bool)d["checkUpdates"], AutomaticProfiles = (bool)d["automaticProfiles"], NetworkAccess = (bool)d["networkAccess"], Theme = (string)d["theme"], CustomAccent = (string)d["customAccent"] };
            }
            if (version != 6 || d.Count != 8 || !d.ContainsKey("useTray") || !d.ContainsKey("checkUpdates") || !d.ContainsKey("automaticProfiles") || !d.ContainsKey("networkAccess") || !d.ContainsKey("theme") || !d.ContainsKey("customAccent") || !d.ContainsKey("historyRetention") ||
                !(d["useTray"] is bool) || !(d["checkUpdates"] is bool) || !(d["automaticProfiles"] is bool) || !(d["networkAccess"] is bool) || !(d["historyRetention"] is int) || (int)d["historyRetention"] < 5 || (int)d["historyRetention"] > 100)
                throw new ArgumentException("Invalid preferences. Expected version 6 settings.");
            if (!(d["theme"] is string) || Array.IndexOf(new[] { "KiWeave Dark", "Midnight Blue", "Plum", "Glass" }, (string)d["theme"]) < 0) throw new ArgumentException("Unknown KiWeave theme.");
            if (!(d["customAccent"] is string) || ((string)d["customAccent"]).Length > 7 || ((string)d["customAccent"]).Length != 0 && !System.Text.RegularExpressions.Regex.IsMatch((string)d["customAccent"], "\\A#[0-9a-fA-F]{6}\\z")) throw new ArgumentException("Invalid custom accent color.");
            return new UserPreferences { UseTray = (bool)d["useTray"], CheckUpdates = (bool)d["checkUpdates"], AutomaticProfiles = (bool)d["automaticProfiles"], NetworkAccess = (bool)d["networkAccess"], Theme = (string)d["theme"], CustomAccent = (string)d["customAccent"], HistoryRetention = (int)d["historyRetention"] };
        }
        public static string Serialize(UserPreferences preferences)
        {
            if (preferences == null) throw new ArgumentNullException("preferences");
            if (preferences.HistoryRetention < 5 || preferences.HistoryRetention > 100) throw new ArgumentException("History retention must be between 5 and 100 snapshots.");
            return "{\r\n  \"version\": 6,\r\n  \"useTray\": " + (preferences.UseTray ? "true" : "false") +
                ",\r\n  \"checkUpdates\": " + (preferences.CheckUpdates ? "true" : "false") +
                ",\r\n  \"automaticProfiles\": " + (preferences.AutomaticProfiles ? "true" : "false") +
                ",\r\n  \"networkAccess\": " + (preferences.NetworkAccess ? "true" : "false") +
                ",\r\n  \"theme\": \"" + (preferences.Theme ?? "KiWeave Dark").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"" +
                ",\r\n  \"customAccent\": \"" + (preferences.CustomAccent ?? "") + "\"" +
                ",\r\n  \"historyRetention\": " + preferences.HistoryRetention + "\r\n}\r\n";
        }
        public static UserPreferences Load(string path)
        {
            if (!File.Exists(path)) return new UserPreferences();
            if (new FileInfo(path).Length > 1024) throw new ArgumentException("Preferences file is too large.");
            return Parse(File.ReadAllText(path, Encoding.UTF8));
        }
        public static void Save(string path, UserPreferences preferences)
        {
            if (preferences == null) throw new ArgumentNullException("preferences");
            string dir = Path.GetDirectoryName(Path.GetFullPath(path)); Directory.CreateDirectory(dir);
            string temp = Path.Combine(dir, ".preferences-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                byte[] bytes = Encoding.UTF8.GetBytes(Serialize(preferences));
                using (var f = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { f.Write(bytes, 0, bytes.Length); f.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true); else File.Move(temp, path);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
