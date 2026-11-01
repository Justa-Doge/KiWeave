using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FunctionRowRemapper
{
    internal static class ActionPrivacy
    {
        static readonly string[] RiskOrder = { "LOCAL ONLY", "SENDS INPUT", "CHANGES WINDOWS OR APP STATE", "CHANGES MONITOR HARDWARE", "OPENS OR RUNS LOCAL CONTENT", "RUNS MULTIPLE STEPS", "USES NETWORK" };
        internal static string Risk(Mapping mapping)
        {
            if (mapping == null) return "Invalid action";
            if (mapping.Kind == ActionKind.Conditional) {
                try { var rule = ConditionalCodec.Parse(mapping.Target); return "CONDITIONAL: " + Strongest(Risk(rule.WhenMatched), Risk(rule.Otherwise)); }
                catch { return "INVALID CONDITIONAL ACTION"; }
            }
            if (mapping.Kind == ActionKind.HttpRequest) return "USES NETWORK";
            if (mapping.Kind == ActionKind.Application || mapping.Kind == ActionKind.FileOrFolder || mapping.Kind == ActionKind.WindowsShortcut || mapping.Kind == ActionKind.Command || mapping.Kind == ActionKind.Python) return "OPENS OR RUNS LOCAL CONTENT";
            if (mapping.Kind == ActionKind.Monitor) return "CHANGES MONITOR HARDWARE";
            if (mapping.Kind == ActionKind.LockThenSleep || mapping.Kind == ActionKind.SystemAction) return "CHANGES WINDOWS OR APP STATE";
            if (mapping.Kind == ActionKind.SendKey || mapping.Kind == ActionKind.SendShortcut || mapping.Kind == ActionKind.Media) return "SENDS INPUT";
            if (mapping.Kind == ActionKind.Sequence) return "RUNS MULTIPLE STEPS";
            return "LOCAL ONLY";
        }
        static string Strongest(params string[] risks)
        {
            return risks.OrderByDescending(x => Array.IndexOf(RiskOrder, StripConditional(x))).FirstOrDefault() ?? "LOCAL ONLY";
        }
        static string StripConditional(string risk) { const string prefix = "CONDITIONAL: "; return risk != null && risk.StartsWith(prefix, StringComparison.Ordinal) ? risk.Substring(prefix.Length) : risk; }
        internal static IEnumerable<Mapping> Effects(Mapping mapping)
        {
            if (mapping == null) yield break;
            if (mapping.Kind == ActionKind.Conditional) {
                ConditionalRule rule; try { rule = ConditionalCodec.Parse(mapping.Target); } catch { yield break; }
                foreach (Mapping item in Effects(rule.WhenMatched)) yield return item;
                foreach (Mapping item in Effects(rule.Otherwise)) yield return item;
                yield break;
            }
            if (mapping.Kind == ActionKind.Sequence) {
                List<SequenceStep> steps; try { steps = SequenceCodec.Parse(mapping.Target); } catch { yield break; }
                foreach (SequenceStep step in steps.Where(x => x != null && !x.IsWait)) foreach (Mapping item in Effects(step.Action)) yield return item;
                yield break;
            }
            yield return mapping;
        }
        internal static IEnumerable<KeyValuePair<string, Mapping>> Mappings(Configuration configuration)
        {
            if (configuration == null) yield break;
            Mapping[] baseMappings = configuration.Mappings ?? new Mapping[0];
            for (int i = 0; i < Math.Min(12, baseMappings.Length); i++) yield return new KeyValuePair<string, Mapping>("Base F" + (i + 1), baseMappings[i]);
            foreach (ModifierLayer layer in configuration.Layers ?? new ModifierLayer[0])
                for (int i = 0; i < Math.Min(12, layer.Mappings == null ? 0 : layer.Mappings.Length); i++) yield return new KeyValuePair<string, Mapping>(layer.Name + " F" + (i + 1), layer.Mappings[i]);
            foreach (CustomHotkey hotkey in configuration.CustomHotkeys ?? new CustomHotkey[0]) if (hotkey != null) yield return new KeyValuePair<string, Mapping>("Hotkey " + hotkey.Shortcut, hotkey.Action);
        }
        internal static string Review(Configuration configuration, string sourceName)
        {
            var active = Mappings(configuration).Where(x => x.Value != null && x.Value.Kind != ActionKind.PassThrough && x.Value.Kind != ActionKind.Unbound).ToArray();
            Mapping[] effects = active.SelectMany(x => Effects(x.Value)).ToArray();
            int network = effects.Count(x => x.Kind == ActionKind.HttpRequest), executable = effects.Count(x => Risk(x) == "OPENS OR RUNS LOCAL CONTENT"), hardware = effects.Count(x => x.Kind == ActionKind.Monitor);
            var text = new StringBuilder(); text.AppendLine("IMPORT QUARANTINE"); text.AppendLine("Source file: " + Path.GetFileName(sourceName)); text.AppendLine();
            text.AppendLine("12 base keys, " + (configuration == null || configuration.Layers == null ? 0 : configuration.Layers.Length) + " layers, " + (configuration == null || configuration.CustomHotkeys == null ? 0 : configuration.CustomHotkeys.Length) + " custom hotkeys");
            text.AppendLine(active.Length + " non-default actions: " + network + " network, " + executable + " local launch/open, " + hardware + " hardware"); text.AppendLine();
            if (active.Length == 0) text.AppendLine("No non-default actions are present.");
            foreach (var item in active) {
                Mapping mapping = item.Value; text.AppendLine(item.Key + "  [" + Risk(mapping) + "]"); text.AppendLine("  " + mapping.Summary);
                if (mapping.Kind == ActionKind.Conditional) {
                    try { var rule = ConditionalCodec.Parse(mapping.Target); text.AppendLine("  Match: " + rule.WhenMatched.Summary + "  [" + Risk(rule.WhenMatched) + "]"); text.AppendLine("  Otherwise: " + rule.Otherwise.Summary + "  [" + Risk(rule.Otherwise) + "]"); }
                    catch { text.AppendLine("  The condition could not be decoded safely."); }
                }
                if (mapping.Kind == ActionKind.Application || mapping.Kind == ActionKind.FileOrFolder || mapping.Kind == ActionKind.WindowsShortcut || mapping.Kind == ActionKind.Command || mapping.Kind == ActionKind.Python || mapping.Kind == ActionKind.HttpRequest) {
                    if (!String.IsNullOrWhiteSpace(mapping.Target)) text.AppendLine("  Target: " + mapping.Target);
                    if (!String.IsNullOrWhiteSpace(mapping.Arguments)) text.AppendLine("  Arguments/body: " + mapping.Arguments);
                    if (!String.IsNullOrWhiteSpace(mapping.WorkingDirectory)) text.AppendLine("  Working directory: " + mapping.WorkingDirectory);
                }
                text.AppendLine();
            }
            return text.ToString();
        }
    }
}
