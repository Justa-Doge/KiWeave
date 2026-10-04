using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    internal static class UpdateChannels
    {
        internal static readonly string[] Names = { "Stable", "Beta", "Alpha" };
        internal static string Path { get { return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ConfigStore.DefaultPath), "update-channel.json"); } }
        internal static string Load()
        {
            try {
                if (!File.Exists(Path)) return Names[0];
                var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path, Encoding.UTF8)) as System.Collections.Generic.Dictionary<string, object>;
                string value = d == null || !d.ContainsKey("channel") ? "" : d["channel"] as string;
                return Array.IndexOf(Names, value) >= 0 ? value : Names[0];
            } catch { return Names[0]; }
        }
        internal static void Save(string channel)
        {
            if (Array.IndexOf(Names, channel) < 0) throw new ArgumentException("Unknown update channel.");
            string dir = System.IO.Path.GetDirectoryName(Path); Directory.CreateDirectory(dir);
            File.WriteAllText(Path, "{\r\n  \"version\": 1,\r\n  \"channel\": \"" + channel + "\"\r\n}\r\n", new UTF8Encoding(false));
        }
    }
}
