using System;
using System.Collections.Generic;
using System.Linq;

namespace FunctionRowRemapper
{
    internal static class MonitorMappingScope
    {
        internal static string Describe(Configuration configuration)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase); if (configuration != null) foreach (var mapping in All(configuration)) if (mapping != null && mapping.Kind == ActionKind.Monitor && !String.IsNullOrWhiteSpace(mapping.MonitorId)) ids.Add(mapping.MonitorId);
            return ids.Count == 0 ? "No monitor-specific mappings" : ids.Count + " saved monitor target" + (ids.Count == 1 ? "" : "s") + " · " + String.Join(", ", ids.Select(Redact).ToArray());
        }
        static IEnumerable<Mapping> All(Configuration c)
        { foreach (var m in c.Mappings ?? new Mapping[0]) yield return m; foreach (var l in c.Layers ?? new ModifierLayer[0]) foreach (var m in l.Mappings ?? new Mapping[0]) yield return m; foreach (var h in c.CustomHotkeys ?? new CustomHotkey[0]) yield return h.Action; }
        static string Redact(string id) { return id.Length <= 10 ? id : id.Substring(0, 8) + "…"; }
    }
}
