using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FunctionRowRemapper
{
    internal sealed class HealthFinding
    {
        internal readonly string Title, Detail;
        internal HealthFinding(string title, string detail) { Title = title; Detail = detail; }
    }

    internal sealed class ConfigurationHealthReport
    {
        internal static readonly ConfigurationHealthReport Empty = new ConfigurationHealthReport(new HealthFinding[0]);
        internal readonly HealthFinding[] Findings;
        internal bool HasWarnings { get { return Findings.Length > 0; } }
        internal string Summary { get { return HasWarnings ? Findings.Length + (Findings.Length == 1 ? " configuration issue needs attention." : " configuration issues need attention.") : "No configuration problems found."; } }
        internal ConfigurationHealthReport(HealthFinding[] findings) { Findings = findings ?? new HealthFinding[0]; }
    }

    // Read-only local validation. It never launches targets, contacts endpoints, or changes settings.
    internal static class ConfigurationHealth
    {
        internal static ConfigurationHealthReport Scan(Configuration configuration, ProfileCollection profiles, DdcMonitor[] detected)
        {
            var findings = new List<HealthFinding>();
            if (configuration != null) ScanConfiguration(configuration, "Default", findings, detected);
            if (profiles != null) foreach (var profile in profiles.Profiles ?? new KeyWeaveProfile[0]) {
                if (profile == null || profile.Configuration == null) continue;
                ScanConfiguration(profile.Configuration, "Profile " + (String.IsNullOrWhiteSpace(profile.Name) ? "(unnamed)" : profile.Name), findings, detected);
            }
            var distinct = findings.GroupBy(f => f.Title + "\n" + f.Detail, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).Take(12).ToArray();
            return new ConfigurationHealthReport(distinct);
        }

        static void ScanConfiguration(Configuration configuration, string owner, List<HealthFinding> findings, DdcMonitor[] detected)
        {
            if (configuration.Mappings != null) for (int i = 0; i < configuration.Mappings.Length; i++) ScanMapping(configuration.Mappings[i], owner + " · F" + (i + 1), findings, detected);
            if (configuration.Layers != null) foreach (var layer in configuration.Layers ?? new ModifierLayer[0]) {
                if (layer == null || layer.Mappings == null) continue;
                for (int i = 0; i < layer.Mappings.Length; i++) ScanMapping(layer.Mappings[i], owner + " · " + layer.Name + " · F" + (i + 1), findings, detected);
            }
            if (configuration.CustomHotkeys != null) foreach (var hotkey in configuration.CustomHotkeys ?? new CustomHotkey[0])
                if (hotkey != null) ScanMapping(hotkey.Action, owner + " · " + (String.IsNullOrWhiteSpace(hotkey.Shortcut) ? "custom hotkey" : hotkey.Shortcut), findings, detected);
        }

        static void ScanMapping(Mapping mapping, string owner, List<HealthFinding> findings, DdcMonitor[] detected)
        {
            if (mapping == null) return;
            if (mapping.Kind == ActionKind.Application || mapping.Kind == ActionKind.FileOrFolder || mapping.Kind == ActionKind.Command || mapping.Kind == ActionKind.Python) {
                if (!String.IsNullOrWhiteSpace(mapping.Target) && !File.Exists(mapping.Target) && !Directory.Exists(mapping.Target))
                    findings.Add(new HealthFinding("Missing target", owner + " refers to a file or program that is not currently available."));
            }
            if (mapping.Kind == ActionKind.Monitor && (detected == null || !detected.Any(m => m != null && String.Equals(m.Id, mapping.MonitorId, StringComparison.OrdinalIgnoreCase))))
                findings.Add(new HealthFinding("Monitor unavailable", owner + " refers to a monitor that was not detected during the last scan."));
            if (mapping.Kind == ActionKind.Sequence) {
                try { foreach (var step in SequenceCodec.Parse(mapping.Target) ?? new List<SequenceStep>()) if (step != null && !step.IsWait) ScanMapping(step.Action, owner + " · sequence step", findings, detected); }
                catch { findings.Add(new HealthFinding("Invalid sequence", owner + " contains a sequence that could not be read.")); }
            }
            if (mapping.Kind == ActionKind.Conditional) {
                try { var rule = ConditionalCodec.Parse(mapping.Target); if (rule != null) { ScanMapping(rule.WhenMatched, owner + " · matching branch", findings, detected); ScanMapping(rule.Otherwise, owner + " · fallback branch", findings, detected); } }
                catch { findings.Add(new HealthFinding("Invalid condition", owner + " contains a condition that could not be read.")); }
            }
        }
    }
}
