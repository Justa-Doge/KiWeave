using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    internal sealed class NotificationPreferences
    {
        internal bool Updates = true, Health = true, Safety = true;
        internal static string Path { get { return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ConfigStore.DefaultPath), "notification-preferences.json"); } }
        internal static NotificationPreferences Load()
        {
            try {
                if (!File.Exists(Path)) return new NotificationPreferences();
                var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path, Encoding.UTF8)) as System.Collections.Generic.Dictionary<string, object>;
                if (d == null || d.Count != 4 || !(d["version"] is int) || (int)d["version"] != 1 || !(d["updates"] is bool) || !(d["health"] is bool) || !(d["safety"] is bool)) throw new ArgumentException();
                return new NotificationPreferences { Updates = (bool)d["updates"], Health = (bool)d["health"], Safety = (bool)d["safety"] };
            } catch { return new NotificationPreferences(); }
        }
        internal void Save()
        {
            string dir = System.IO.Path.GetDirectoryName(Path); Directory.CreateDirectory(dir);
            File.WriteAllText(Path, "{\r\n  \"version\": 1,\r\n  \"updates\": " + (Updates ? "true" : "false") + ",\r\n  \"health\": " + (Health ? "true" : "false") + ",\r\n  \"safety\": " + (Safety ? "true" : "false") + "\r\n}\r\n", new UTF8Encoding(false));
        }
    }
}
