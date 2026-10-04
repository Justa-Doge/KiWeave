using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    internal sealed class NotificationPreferences
    {
        internal bool Updates = true, Health = true, Safety = true;
        internal string Severity = "All";
        internal bool AllowsWarning { get { return String.Equals(Severity, "All", StringComparison.OrdinalIgnoreCase) || String.Equals(Severity, "Warnings and above", StringComparison.OrdinalIgnoreCase); } }
        internal static string Path { get { return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ConfigStore.DefaultPath), "notification-preferences.json"); } }
        internal static NotificationPreferences Load()
        {
            try {
                if (!File.Exists(Path)) return new NotificationPreferences();
                var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path, Encoding.UTF8)) as System.Collections.Generic.Dictionary<string, object>;
                if (d == null || !(d["version"] is int) || !(d["updates"] is bool) || !(d["health"] is bool) || !(d["safety"] is bool)) throw new ArgumentException();
                string severity = d.ContainsKey("severity") && d["severity"] is string ? (string)d["severity"] : "All";
                if ((int)d["version"] == 1 && d.Count == 4) return new NotificationPreferences { Updates = (bool)d["updates"], Health = (bool)d["health"], Safety = (bool)d["safety"] };
                if ((int)d["version"] != 2 || d.Count != 5 || Array.IndexOf(new[] { "All", "Warnings and above", "Critical only" }, severity) < 0) throw new ArgumentException();
                return new NotificationPreferences { Updates = (bool)d["updates"], Health = (bool)d["health"], Safety = (bool)d["safety"], Severity = severity };
            } catch { return new NotificationPreferences(); }
        }
        internal void Save()
        {
            string dir = System.IO.Path.GetDirectoryName(Path); Directory.CreateDirectory(dir);
            File.WriteAllText(Path, "{\r\n  \"version\": 2,\r\n  \"updates\": " + (Updates ? "true" : "false") + ",\r\n  \"health\": " + (Health ? "true" : "false") + ",\r\n  \"safety\": " + (Safety ? "true" : "false") + ",\r\n  \"severity\": \"" + Severity + "\"\r\n}\r\n", new UTF8Encoding(false));
        }
    }
}
