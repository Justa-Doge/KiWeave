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
            return Load(Path);
        }
        internal static string Load(string path)
        {
            try {
                string json = AtomicFile.ReadAllTextBounded(path, 4096, new UTF8Encoding(false, true));
                JsonSyntax.Check(json);
                var d = new JavaScriptSerializer { MaxJsonLength = 4096 }.DeserializeObject(json) as System.Collections.Generic.Dictionary<string, object>;
                if (d == null || !d.ContainsKey("channel") || !(d["channel"] is string)) return Names[0];
                if (d.Count == 2 && (!d.ContainsKey("version") || !(d["version"] is int) || (int)d["version"] != 1)) return Names[0];
                if (d.Count != 1 && d.Count != 2) return Names[0];
                string value = (string)d["channel"];
                return Array.IndexOf(Names, value) >= 0 ? value : Names[0];
            } catch (FileNotFoundException) { return Names[0]; }
            catch (DirectoryNotFoundException) { return Names[0]; }
            catch { return Names[0]; }
        }
        internal static void Save(string channel)
        {
            Save(Path, channel);
        }
        internal static void Save(string path, string channel)
        {
            if (Array.IndexOf(Names, channel) < 0) throw new ArgumentException("Unknown update channel.");
            string json = "{\r\n  \"version\": 1,\r\n  \"channel\": \"" + channel + "\"\r\n}\r\n";
            AtomicFile.WriteAllText(path, json, new UTF8Encoding(false), ".bak");
        }
    }
}
