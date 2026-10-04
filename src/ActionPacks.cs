using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    // Declarative, reviewable action-pack container. Packs never contain code,
    // binaries, scripts, or install instructions: only KiWeave configuration.
    internal sealed class ActionPack
    {
        internal string Id = "", Name = "", Version = "1.0.0", Publisher = "", Description = "";
        internal Configuration Configuration = new Configuration();
        internal ProfileCollection Profiles = new ProfileCollection();
    }

    internal static class ActionPackStore
    {
        const int MaxBytes = 1024 * 1024;
        internal static ActionPack Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Action pack not found.");
            if (new FileInfo(path).Length > MaxBytes) throw new ArgumentException("Action pack is too large.");
            return Parse(File.ReadAllText(path, Encoding.UTF8));
        }
        internal static ActionPack Parse(string json)
        {
            if (String.IsNullOrEmpty(json) || Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("Action pack is too large.");
            JsonSyntax.Check(json);
            var root = new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 20 }.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null || (root.Count != 8 && root.Count != 9) || root["format"] as string != "kiweave-action-pack" || !(root["version"] is int) || (int)root["version"] != 1)
                throw new ArgumentException("Unsupported action-pack format.");
            string id = Text(root, "id"), name = Text(root, "name"), publisher = Text(root, "publisher"), description = Text(root, "description");
            string packVersion = root.ContainsKey("packVersion") ? Text(root, "packVersion") : "1.0.0";
            string configuration = Text(root, "configuration"), profiles = Text(root, "profiles");
            if (id.Length < 3 || id.Length > 80 || !System.Text.RegularExpressions.Regex.IsMatch(id, "\\A[a-z0-9][a-z0-9.-]+\\z")) throw new ArgumentException("Action-pack id is invalid.");
            if (name.Length < 1 || name.Length > 80 || publisher.Length > 120 || description.Length > 1000) throw new ArgumentException("Action-pack metadata is invalid.");
            var pack = new ActionPack { Id = id, Name = name, Version = packVersion, Publisher = publisher, Description = description, Configuration = ConfigStore.Parse(configuration), Profiles = ProfileStore.Parse(profiles) };
            Validate(pack); return pack;
        }
        internal static string Serialize(ActionPack pack)
        {
            Validate(pack);
            var serializer = new JavaScriptSerializer { MaxJsonLength = MaxBytes };
            string json = serializer.Serialize(new { format = "kiweave-action-pack", version = 1, id = pack.Id, packVersion = pack.Version, name = pack.Name, publisher = pack.Publisher ?? "", description = pack.Description ?? "", configuration = ConfigStore.Serialize(pack.Configuration), profiles = ProfileStore.Serialize(pack.Profiles) });
            if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("Action pack is too large.");
            return json;
        }
        internal static void Save(string path, ActionPack pack)
        {
            File.WriteAllText(path, Serialize(pack), new UTF8Encoding(false));
        }
        internal static string Review(ActionPack pack)
        {
            Validate(pack);
            return ActionPrivacy.Review(pack.Configuration, pack.Name) + "\r\nPACK ID: " + pack.Id + "\r\nPACK VERSION: " + pack.Version + "\r\nCOMPATIBILITY: " + Compatibility(pack) + "\r\nTRUST: Unverified declarative pack\r\nPUBLISHER: " + pack.Publisher + " (informational; not a safety guarantee)\r\nPROFILES: " + pack.Profiles.Profiles.Length;
        }
        internal static void Validate(ActionPack pack)
        {
            if (pack == null) throw new ArgumentNullException("pack");
            if (!System.Text.RegularExpressions.Regex.IsMatch(pack.Version ?? "", "\\A[0-9]+\\.[0-9]+\\.[0-9]+(?:-[A-Za-z0-9.-]+)?\\z")) throw new ArgumentException("Action-pack version must be semantic version text.");
            if (Compatibility(pack).StartsWith("Incompatible", StringComparison.Ordinal)) throw new ArgumentException(Compatibility(pack));
            ConfigStore.Validate(pack.Configuration, false); ProfileStore.Validate(pack.Profiles, false);
            foreach (var item in ActionPrivacy.Mappings(pack.Configuration).SelectMany(x => ActionPrivacy.Effects(x.Value)))
                if (item.Kind == ActionKind.Command || item.Kind == ActionKind.Python) throw new ArgumentException("Action packs cannot contain scripts or command actions.");
        }
        internal static string Compatibility(ActionPack pack)
        {
            int packMajor, currentMajor; string[] p = (pack == null ? "" : pack.Version ?? "").Split('.'); string[] c = UpdateChecker.CurrentVersion.Split('.');
            if (p.Length < 1 || !Int32.TryParse(p[0], out packMajor) || c.Length < 1 || !Int32.TryParse(c[0], out currentMajor)) return "Unknown version";
            return packMajor > currentMajor ? "Incompatible: requires a newer major KiWeave format." : "Compatible with this KiWeave major version.";
        }
        static string Text(Dictionary<string, object> root, string key)
        {
            object value; if (!root.TryGetValue(key, out value) || !(value is string)) throw new ArgumentException("Action pack field '" + key + "' is invalid.");
            return (string)value;
        }
    }
}
