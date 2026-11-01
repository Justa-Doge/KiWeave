using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    internal sealed class NotificationPreferences
    {
        internal bool Updates = true, Health = true, Safety = true;
        internal bool LoadWarning;
        internal string Severity = "All";
        internal bool AllowsWarning { get { return String.Equals(Severity, "All", StringComparison.OrdinalIgnoreCase) || String.Equals(Severity, "Warnings and above", StringComparison.OrdinalIgnoreCase); } }
        internal static string Path { get { return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ConfigStore.DefaultPath), "notification-preferences.json"); } }
        internal static NotificationPreferences Load()
        {
            return Load(Path);
        }
        internal static NotificationPreferences Load(string path)
        {
            try {
                string json = AtomicFile.ReadAllTextBounded(path, 4096, new UTF8Encoding(false, true));
                JsonSyntax.Check(json);
                var d = new JavaScriptSerializer { MaxJsonLength = 4096 }.DeserializeObject(json) as System.Collections.Generic.Dictionary<string, object>;
                if (d == null || !d.ContainsKey("version") || !(d["version"] is int) || !d.ContainsKey("updates") || !(d["updates"] is bool) || !d.ContainsKey("health") || !(d["health"] is bool) || !d.ContainsKey("safety") || !(d["safety"] is bool)) throw new ArgumentException();
                string severity = d.ContainsKey("severity") && d["severity"] is string ? (string)d["severity"] : "All";
                if ((int)d["version"] == 1 && d.Count == 4) return new NotificationPreferences { Updates = (bool)d["updates"], Health = (bool)d["health"], Safety = (bool)d["safety"] };
                if ((int)d["version"] != 2 || d.Count != 5 || !d.ContainsKey("severity") || Array.IndexOf(new[] { "All", "Warnings and above", "Critical only" }, severity) < 0) throw new ArgumentException();
                return new NotificationPreferences { Updates = (bool)d["updates"], Health = (bool)d["health"], Safety = (bool)d["safety"], Severity = severity };
            } catch (FileNotFoundException) { return new NotificationPreferences(); }
            catch (DirectoryNotFoundException) { return new NotificationPreferences(); }
            catch { return new NotificationPreferences { Severity = "Critical only", LoadWarning = true }; }
        }
        internal void Save()
        {
            Save(Path);
        }
        internal void Save(string path)
        {
            if (Array.IndexOf(new[] { "All", "Warnings and above", "Critical only" }, Severity) < 0) throw new ArgumentException("Choose a supported notification severity.");
            string json = "{\r\n  \"version\": 2,\r\n  \"updates\": " + (Updates ? "true" : "false") + ",\r\n  \"health\": " + (Health ? "true" : "false") + ",\r\n  \"safety\": " + (Safety ? "true" : "false") + ",\r\n  \"severity\": \"" + Severity + "\"\r\n}\r\n";
            AtomicFile.WriteAllText(path, json, new UTF8Encoding(false), ".bak");
            LoadWarning = false;
        }
    }
}
