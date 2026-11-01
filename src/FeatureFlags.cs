using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    internal sealed class FeatureFlags
    {
        public bool ExperimentalEnabled = true;
        public bool DeveloperMode;
        internal bool LoadWarning;
        const int MaxFileBytes = 4096;
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "feature-flags.json"); } }
        internal static FeatureFlags Load() { return Load(Path); }
        internal static FeatureFlags Load(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A feature-settings path is required.", "path");
            try {
                string json = AtomicFile.ReadAllTextBounded(path, MaxFileBytes, StrictUtf8);
                JsonSyntax.Check(json);
                var d = new JavaScriptSerializer { MaxJsonLength = MaxFileBytes }.DeserializeObject(json) as System.Collections.Generic.Dictionary<string, object>;
                if (d == null || d.Count != 3 || !d.ContainsKey("version") || !(d["version"] is int) || (int)d["version"] != 1 || !(d["experimentalEnabled"] is bool) || !(d["developerMode"] is bool)) throw new ArgumentException();
                return new FeatureFlags { ExperimentalEnabled = (bool)d["experimentalEnabled"], DeveloperMode = (bool)d["developerMode"] };
            }
            catch (FileNotFoundException) { return new FeatureFlags(); }
            catch (DirectoryNotFoundException) { return new FeatureFlags(); }
            catch { return new FeatureFlags { ExperimentalEnabled = false, DeveloperMode = false, LoadWarning = true }; }
        }
        internal void Save() { Save(Path); }
        internal void Save(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A feature-settings path is required.", "path");
            string json = "{\r\n  \"version\": 1,\r\n  \"experimentalEnabled\": " + (ExperimentalEnabled ? "true" : "false") + ",\r\n  \"developerMode\": " + (DeveloperMode ? "true" : "false") + "\r\n}\r\n";
            AtomicFile.WriteAllText(path, json, new UTF8Encoding(false), ".bak");
        }
        internal static bool IsExperimental(Mapping mapping)
        {
            if (mapping == null) return false;
            if (mapping.Kind == ActionKind.HttpRequest || mapping.Kind == ActionKind.Command || mapping.Kind == ActionKind.Python || mapping.Kind == ActionKind.Monitor) return true;
            if (mapping.Kind == ActionKind.Sequence) { try { foreach (var step in SequenceCodec.Parse(mapping.Target)) if (!step.IsWait && IsExperimental(step.Action)) return true; } catch { } }
            if (mapping.Kind == ActionKind.Conditional) { try { var rule = ConditionalCodec.Parse(mapping.Target); return IsExperimental(rule.WhenMatched) || IsExperimental(rule.Otherwise); } catch { } }
            return false;
        }
        internal static bool Allows(Mapping mapping, bool experimentalEnabled)
        {
            return experimentalEnabled || !IsExperimental(mapping);
        }
        internal static bool Allows(Mapping mapping, Func<bool> experimentalEnabled)
        {
            if (!IsExperimental(mapping)) return true;
            return experimentalEnabled != null && experimentalEnabled();
        }
    }
}
