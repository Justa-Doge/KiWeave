using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    // A signed repository index is metadata only. KiWeave never downloads or
    // executes a pack from this model; an explicit reviewed import is still
    // required. Pack signatures are checked independently when a pack loads.
    internal sealed class ActionPackRepositoryEntry
    {
        internal string Id = "", Name = "", Version = "", Publisher = "", Description = "", Source = "", Signature = "";
    }

    internal sealed class ActionPackRepositoryIndex
    {
        internal string Publisher = "", Version = "1", Trust = "", SignatureStatus = "";
        internal readonly List<ActionPackRepositoryEntry> Entries = new List<ActionPackRepositoryEntry>();
    }

    internal static class ActionPackRepository
    {
        const int MaxBytes = 256 * 1024;
        internal static ActionPackRepositoryIndex Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Action-pack repository index not found.");
            if (new FileInfo(path).Length > MaxBytes) throw new ArgumentException("Action-pack repository index is too large.");
            string json = File.ReadAllText(path, Encoding.UTF8);
            string signature = ActionPackSignatures.Status(path, json);
            var result = Parse(json);
            result.SignatureStatus = signature;
            return result;
        }

        internal static ActionPackRepositoryIndex Parse(string json)
        {
            if (String.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("Action-pack repository index is too large.");
            JsonSyntax.Check(json);
            var root = new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 12 }.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null || root.Count != 5 || Text(root, "format") != "kiweave-action-pack-repository" || !(root["version"] is int) || (int)root["version"] != 1)
                throw new ArgumentException("Unsupported action-pack repository format.");
            var index = new ActionPackRepositoryIndex { Publisher = Text(root, "publisher"), Trust = Text(root, "trust") };
            if (index.Publisher.Length < 1 || index.Publisher.Length > 120 || index.Trust.Length > 200) throw new ArgumentException("Repository metadata is invalid.");
            var values = root["entries"] as object[]; if (values == null || values.Length > 200) throw new ArgumentException("Repository entries are invalid.");
            foreach (object value in values) {
                var entry = value as Dictionary<string, object>; if (entry == null || entry.Count != 7) throw new ArgumentException("Repository entry is invalid.");
                var item = new ActionPackRepositoryEntry { Id = Text(entry, "id"), Name = Text(entry, "name"), Version = Text(entry, "version"), Publisher = Text(entry, "publisher"), Description = Text(entry, "description"), Source = Text(entry, "source"), Signature = Text(entry, "signature") };
                if (!System.Text.RegularExpressions.Regex.IsMatch(item.Id, "\\A[a-z0-9][a-z0-9.-]+\\z") || item.Name.Length < 1 || item.Name.Length > 80 || item.Publisher.Length > 120 || item.Description.Length > 1000 || item.Version.Length > 40 || item.Signature.Length > 200) throw new ArgumentException("Repository entry metadata is invalid.");
                if (item.Source.Length < 1 || item.Source.Length > 500 || !(item.Source.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || !item.Source.Contains("://"))) throw new ArgumentException("Repository entry source must be HTTPS or a relative path.");
                if (item.Signature != "detached-rsa" && item.Signature != "unsigned") throw new ArgumentException("Repository entry signature policy is invalid.");
                index.Entries.Add(item);
            }
            return index;
        }

        static string Text(Dictionary<string, object> root, string key)
        {
            object value; if (!root.TryGetValue(key, out value) || !(value is string)) throw new ArgumentException("Repository field '" + key + "' is invalid.");
            return (string)value;
        }
    }
}
