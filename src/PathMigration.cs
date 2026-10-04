using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FunctionRowRemapper
{
    internal sealed class PathMigrationCandidate { internal string Location, Value; }
    internal static class PathMigration
    {
        internal static List<PathMigrationCandidate> Find(Configuration configuration)
        {
            var result = new List<PathMigrationCandidate>(); if (configuration == null) return result;
            Visit(configuration.Mappings, "Base F", result); foreach (var layer in configuration.Layers ?? new ModifierLayer[0]) Visit(layer.Mappings, layer.Name + " F", result); foreach (var hotkey in configuration.CustomHotkeys ?? new CustomHotkey[0]) Visit(new[] { hotkey.Action }, "Hotkey " + hotkey.Shortcut, result); return result;
        }
        static void Visit(IEnumerable<Mapping> mappings, string prefix, List<PathMigrationCandidate> result)
        {
            int i = 0; foreach (var mapping in mappings ?? Enumerable.Empty<Mapping>()) { Visit(mapping, prefix + (prefix.StartsWith("Hotkey", StringComparison.Ordinal) ? "" : (i + 1).ToString()), result); i++; }
        }
        static void Visit(Mapping mapping, string location, List<PathMigrationCandidate> result)
        {
            if (mapping == null) return;
            if (mapping.Kind == ActionKind.Application || mapping.Kind == ActionKind.FileOrFolder || mapping.Kind == ActionKind.Command || mapping.Kind == ActionKind.Python || mapping.Kind == ActionKind.WindowsShortcut) { if (!String.IsNullOrWhiteSpace(mapping.Target)) result.Add(new PathMigrationCandidate { Location = location + " target", Value = mapping.Target }); if (!String.IsNullOrWhiteSpace(mapping.WorkingDirectory)) result.Add(new PathMigrationCandidate { Location = location + " working folder", Value = mapping.WorkingDirectory }); }
            if (mapping.Kind == ActionKind.Sequence) { try { int i = 0; foreach (var step in SequenceCodec.Parse(mapping.Target)) if (!step.IsWait) Visit(step.Action, location + " step " + (++i), result); } catch { } }
            if (mapping.Kind == ActionKind.Conditional) { try { var rule = ConditionalCodec.Parse(mapping.Target); Visit(rule.WhenMatched, location + " matched", result); Visit(rule.Otherwise, location + " fallback", result); } catch { } }
        }
        internal static int Replace(Configuration configuration, string oldRoot, string newRoot)
        {
            if (configuration == null || String.IsNullOrWhiteSpace(oldRoot) || String.IsNullOrWhiteSpace(newRoot)) return 0; oldRoot = Normalize(oldRoot); newRoot = Normalize(newRoot); int count = 0;
            VisitReplace(configuration.Mappings, ref count, oldRoot, newRoot); foreach (var layer in configuration.Layers ?? new ModifierLayer[0]) VisitReplace(layer.Mappings, ref count, oldRoot, newRoot); foreach (var hotkey in configuration.CustomHotkeys ?? new CustomHotkey[0]) VisitReplace(new[] { hotkey.Action }, ref count, oldRoot, newRoot); return count;
        }
        static void VisitReplace(IEnumerable<Mapping> mappings, ref int count, string oldRoot, string newRoot) { foreach (var mapping in mappings ?? Enumerable.Empty<Mapping>()) ReplaceMapping(mapping, ref count, oldRoot, newRoot); }
        static void ReplaceMapping(Mapping mapping, ref int count, string oldRoot, string newRoot)
        {
            if (mapping == null) return;
            if (mapping.Kind == ActionKind.Application || mapping.Kind == ActionKind.FileOrFolder || mapping.Kind == ActionKind.Command || mapping.Kind == ActionKind.Python || mapping.Kind == ActionKind.WindowsShortcut) { mapping.Target = ReplaceValue(mapping.Target, oldRoot, newRoot, ref count); mapping.WorkingDirectory = ReplaceValue(mapping.WorkingDirectory, oldRoot, newRoot, ref count); }
            if (mapping.Kind == ActionKind.Sequence) try { var steps = SequenceCodec.Parse(mapping.Target); foreach (var step in steps) if (!step.IsWait) ReplaceMapping(step.Action, ref count, oldRoot, newRoot); mapping.Target = SequenceCodec.Serialize(steps); } catch { }
            if (mapping.Kind == ActionKind.Conditional) try { var rule = ConditionalCodec.Parse(mapping.Target); ReplaceMapping(rule.WhenMatched, ref count, oldRoot, newRoot); ReplaceMapping(rule.Otherwise, ref count, oldRoot, newRoot); mapping.Target = ConditionalCodec.Serialize(rule); } catch { }
        }
        static string ReplaceValue(string value, string oldRoot, string newRoot, ref int count)
        {
            if (String.IsNullOrWhiteSpace(value)) return value; string full; try { full = Path.GetFullPath(value); } catch { return value; }
            if (!full.Equals(oldRoot, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(oldRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return value;
            count++; return newRoot + full.Substring(oldRoot.Length);
        }
        static string Normalize(string value) { return Path.GetFullPath(value.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)); }
    }
}
