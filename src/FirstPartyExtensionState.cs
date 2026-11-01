using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    internal sealed class FirstPartyExtensionStateSnapshot
    {
        internal readonly Dictionary<string, bool> Values;
        internal readonly string Warning;
        internal FirstPartyExtensionStateSnapshot(Dictionary<string, bool> values, string warning) { Values = values; Warning = warning; }
    }

    internal static class FirstPartyExtensionStateStore
    {
        const int MaxBytes = 4096;
        internal static FirstPartyExtensionStateSnapshot Load(string path, IEnumerable<string> extensionIds)
        {
            string[] ids = ValidateIds(extensionIds);
            var values = ids.ToDictionary(id => id, id => true, StringComparer.OrdinalIgnoreCase);
            string json;
            try { json = AtomicFile.ReadAllTextBounded(path, MaxBytes, new UTF8Encoding(true, true)); }
            catch (FileNotFoundException) { return new FirstPartyExtensionStateSnapshot(values, ""); }
            catch (DirectoryNotFoundException) { return new FirstPartyExtensionStateSnapshot(values, ""); }
            catch { return FailClosed(values); }
            try {
                JsonSyntax.Check(json);
                var serializer = new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 2 };
                var data = serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (data == null) throw new ArgumentException("Extension settings must be a JSON object.");
                foreach (var item in data) {
                    if (!values.ContainsKey(item.Key) || !(item.Value is bool)) throw new ArgumentException("Extension settings contain an unknown extension or non-Boolean value.");
                    values[item.Key] = (bool)item.Value;
                }
                return new FirstPartyExtensionStateSnapshot(values, "");
            } catch { return FailClosed(values); }
        }
        internal static void Save(string path, IDictionary<string, bool> values, IEnumerable<string> extensionIds)
        {
            string[] ids = ValidateIds(extensionIds);
            if (values == null || values.Count != ids.Length || ids.Any(id => !values.ContainsKey(id))) throw new ArgumentException("Extension settings do not match the supported extension list.");
            var ordered = ids.ToDictionary(id => id, id => values[id], StringComparer.Ordinal);
            var serializer = new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 2 };
            string json = serializer.Serialize(ordered) + Environment.NewLine;
            if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("Extension settings exceed the size limit.");
            AtomicFile.WriteAllText(path, json, new UTF8Encoding(false), ".bak");
        }
        static FirstPartyExtensionStateSnapshot FailClosed(Dictionary<string, bool> values)
        {
            foreach (string key in values.Keys.ToArray()) values[key] = false;
            return new FirstPartyExtensionStateSnapshot(values, "Saved extension settings could not be read safely. Extensions are disabled for this session; the file was left untouched.");
        }
        static string[] ValidateIds(IEnumerable<string> extensionIds)
        {
            if (extensionIds == null) throw new ArgumentNullException("extensionIds");
            string[] ids = extensionIds.ToArray();
            if (ids.Length == 0 || ids.Any(String.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Length)
                throw new ArgumentException("A unique supported extension list is required.", "extensionIds");
            return ids;
        }
    }
}
