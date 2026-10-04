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
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "feature-flags.json"); } }
        internal static FeatureFlags Load()
        {
            try { if (!File.Exists(Path)) return new FeatureFlags(); string json = File.ReadAllText(Path, Encoding.UTF8); JsonSyntax.Check(json); var d = new JavaScriptSerializer().DeserializeObject(json) as System.Collections.Generic.Dictionary<string, object>; if (d == null || d.Count != 3 || !(d["version"] is int) || (int)d["version"] != 1 || !(d["experimentalEnabled"] is bool) || !(d["developerMode"] is bool)) throw new ArgumentException(); return new FeatureFlags { ExperimentalEnabled = (bool)d["experimentalEnabled"], DeveloperMode = (bool)d["developerMode"] }; }
            catch { return new FeatureFlags(); }
        }
        internal void Save()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)); string temp = Path + ".tmp"; File.WriteAllText(temp, "{\r\n  \"version\": 1,\r\n  \"experimentalEnabled\": " + (ExperimentalEnabled ? "true" : "false") + ",\r\n  \"developerMode\": " + (DeveloperMode ? "true" : "false") + "\r\n}\r\n", Encoding.UTF8); if (File.Exists(Path)) File.Replace(temp, Path, Path + ".bak", true); else File.Move(temp, Path);
        }
        internal static bool IsExperimental(Mapping mapping)
        {
            if (mapping == null) return false;
            if (mapping.Kind == ActionKind.HttpRequest || mapping.Kind == ActionKind.Command || mapping.Kind == ActionKind.Python || mapping.Kind == ActionKind.Monitor) return true;
            if (mapping.Kind == ActionKind.Sequence) { try { foreach (var step in SequenceCodec.Parse(mapping.Target)) if (!step.IsWait && IsExperimental(step.Action)) return true; } catch { } }
            if (mapping.Kind == ActionKind.Conditional) { try { var rule = ConditionalCodec.Parse(mapping.Target); return IsExperimental(rule.WhenMatched) || IsExperimental(rule.Otherwise); } catch { } }
            return false;
        }
    }
}
