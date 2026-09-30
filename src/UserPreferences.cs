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
        public static string DefaultPath { get { return Path.Combine(Path.GetDirectoryName(ConfigStore.DefaultPath), "preferences.json"); } }
        public static UserPreferences Parse(string json)
        {
            if (json == null || json.Length > 1024) throw new ArgumentException("Invalid preferences size.");
            JsonSyntax.Check(json);
            var d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (d == null || d.Count != 2 || !d.ContainsKey("version") || !d.ContainsKey("useTray") || !(d["version"] is int) || (int)d["version"] != 1 || !(d["useTray"] is bool)) throw new ArgumentException("Invalid preferences. Expected version 1 and a useTray boolean.");
            return new UserPreferences { UseTray = (bool)d["useTray"] };
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
                byte[] bytes = Encoding.UTF8.GetBytes("{\r\n  \"version\": 1,\r\n  \"useTray\": " + (preferences.UseTray ? "true" : "false") + "\r\n}\r\n");
                using (var f = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { f.Write(bytes, 0, bytes.Length); f.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true); else File.Move(temp, path);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
