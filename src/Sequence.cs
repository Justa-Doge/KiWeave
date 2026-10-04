using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FunctionRowRemapper
{
    public sealed class SequenceStep
    {
        public int WaitMilliseconds;
        public Mapping Action;
        public bool IsWait { get { return Action == null; } }
        public SequenceStep Copy() { return IsWait ? new SequenceStep { WaitMilliseconds = WaitMilliseconds } : new SequenceStep { Action = Action.Copy() }; }
        public string Summary { get { return IsWait ? "Wait " + (WaitMilliseconds / 1000.0).ToString("0.###") + " seconds" : Action.Summary; } }
    }
    internal sealed class SequenceSafetySummary
    {
        internal int Steps, WaitMilliseconds, Launches, NetworkRequests, HardwareOperations;
        internal string Compact
        {
            get { return Steps + " / " + SequenceCodec.MaxSteps + " steps  ·  " + (WaitMilliseconds / 1000d).ToString("0.###") + "s waits  ·  " + Launches + " launches  ·  " + NetworkRequests + " network  ·  " + HardwareOperations + " hardware"; }
        }
        internal string Details
        {
            get { return "Steps: " + Steps + " / " + SequenceCodec.MaxSteps + "\r\nConfigured wait time: " + (WaitMilliseconds / 1000d).ToString("0.###") + " seconds\r\nLaunches: " + Launches + "\r\nNetwork requests: " + NetworkRequests + "\r\nHardware operations: " + HardwareOperations; }
        }
    }
    internal static class ActionInsights
    {
        internal static SequenceSafetySummary Sequence(IEnumerable<SequenceStep> source)
        {
            var result = new SequenceSafetySummary();
            foreach (var step in source ?? Enumerable.Empty<SequenceStep>()) {
                result.Steps++;
                if (step == null) continue;
                if (step.IsWait) { result.WaitMilliseconds += step.WaitMilliseconds; continue; }
                CountPotentialEffects(result, step.Action);
            }
            return result;
        }
        static void CountPotentialEffects(SequenceSafetySummary result, Mapping m)
        {
            if (m == null) return;
            if (m.Kind == ActionKind.Conditional) {
                try { var rule = ConditionalCodec.Parse(m.Target); CountPotentialEffects(result, rule.WhenMatched); CountPotentialEffects(result, rule.Otherwise); } catch { }
                return;
            }
            if (m.Kind == ActionKind.Application || m.Kind == ActionKind.FileOrFolder || m.Kind == ActionKind.WindowsShortcut || m.Kind == ActionKind.Command || m.Kind == ActionKind.Python) result.Launches++;
            if (m.Kind == ActionKind.HttpRequest) result.NetworkRequests++;
            if (m.Kind == ActionKind.Monitor) result.HardwareOperations++;
        }
        internal static string Maturity(Mapping mapping)
        {
            if (mapping == null) return "Stable";
            if (mapping.Kind == ActionKind.HttpRequest) return "Experimental";
            if (mapping.Kind == ActionKind.Monitor) return "Hardware-dependent";
            if (mapping.Kind == ActionKind.Application || mapping.Kind == ActionKind.FileOrFolder || mapping.Kind == ActionKind.WindowsShortcut || mapping.Kind == ActionKind.Command || mapping.Kind == ActionKind.Python) return "App-dependent";
            if (mapping.Kind == ActionKind.SystemAction) {
                string target = mapping.Target ?? "";
                if (target.StartsWith("Obs", StringComparison.Ordinal) || target.StartsWith("Discord", StringComparison.Ordinal) || target.StartsWith("Spotify", StringComparison.Ordinal) || target == "OpenPowerToys") return "App-dependent";
            }
            if (mapping.Kind == ActionKind.Sequence) {
                try {
                    string[] labels = SequenceCodec.Parse(mapping.Target).Where(x => !x.IsWait).Select(x => Maturity(x.Action)).ToArray();
                    if (labels.Contains("Experimental")) return "Experimental";
                    if (labels.Contains("Hardware-dependent")) return "Hardware-dependent";
                    if (labels.Contains("App-dependent")) return "App-dependent";
                } catch { return "Experimental"; }
            }
            if (mapping.Kind == ActionKind.Conditional) {
                try {
                    var rule = ConditionalCodec.Parse(mapping.Target); string[] labels = { Maturity(rule.WhenMatched), Maturity(rule.Otherwise) };
                    if (labels.Contains("Experimental")) return "Experimental";
                    if (labels.Contains("Hardware-dependent")) return "Hardware-dependent";
                    if (labels.Contains("App-dependent")) return "App-dependent";
                } catch { return "Experimental"; }
            }
            return "Stable";
        }
        internal static string MaturityExplanation(string maturity)
        {
            if (maturity == "Experimental") return "May need extra review or manual testing before relying on it.";
            if (maturity == "Hardware-dependent") return "Availability and behavior depend on connected hardware and its driver support.";
            if (maturity == "App-dependent") return "Requires the selected file, application, or integration to remain available.";
            return "Uses KiWeave's established local action path.";
        }
        internal static string Dependencies(Mapping mapping)
        {
            if (mapping == null) return "No action selected.";
            if (mapping.Kind == ActionKind.Monitor) return "Requires the selected DDC/CI monitor and a supported hardware control.";
            if (mapping.Kind == ActionKind.HttpRequest) return "Requires network access to be allowed and the configured endpoint to be reachable when triggered.";
            if (mapping.Kind == ActionKind.Application || mapping.Kind == ActionKind.Command || mapping.Kind == ActionKind.Python) return "Requires the selected local program or script to remain available; it runs only when triggered.";
            if (mapping.Kind == ActionKind.FileOrFolder || mapping.Kind == ActionKind.WindowsShortcut) return "Requires the selected local file, folder, or shortcut to remain available.";
            if (mapping.Kind == ActionKind.SystemAction) return "Requires the target Windows or integration capability to be available.";
            if (mapping.Kind == ActionKind.Sequence) return "Depends on the applications, network, and hardware requirements of its individual steps.";
            if (mapping.Kind == ActionKind.Conditional) return "Depends on the current local foreground/running-process condition and the selected branch action.";
            return "No external application, network, or hardware dependency.";
        }
    }
    public static class SequenceCodec
    {
        public const int MaxSteps = 20;
        static string EncodeText(string text) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(text ?? "")).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
        static string DecodeText(string text) { string padded = text.Replace('-', '+').Replace('_', '/'); while (padded.Length % 4 != 0) padded += "="; return Encoding.UTF8.GetString(Convert.FromBase64String(padded)); }
        public static string Serialize(IEnumerable<SequenceStep> source)
        {
            var steps = source == null ? new List<SequenceStep>() : source.Select(s => s.Copy()).ToList(); Validate(steps);
            return String.Join(";", steps.Select(s => s.IsWait ? "W," + s.WaitMilliseconds : "A," + s.Action.Kind + "," + EncodeText(s.Action.Target) + "," + EncodeText(s.Action.Arguments) + "," + EncodeText(s.Action.WorkingDirectory)));
        }
        public static List<SequenceStep> Parse(string value)
        {
            try {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentException("Add at least one sequence step.");
                var result = new List<SequenceStep>();
                foreach (string record in value.Split(';')) {
                    string[] parts = record.Split(',');
                    if (parts.Length == 2 && parts[0] == "W") { int ms; if (!Int32.TryParse(parts[1], out ms)) throw new ArgumentException(); result.Add(new SequenceStep { WaitMilliseconds = ms }); }
                    else if (parts.Length == 5 && parts[0] == "A") { ActionKind kind; if (!Enum.TryParse<ActionKind>(parts[1], false, out kind)) throw new ArgumentException(); result.Add(new SequenceStep { Action = new Mapping { Kind = kind, Target = DecodeText(parts[2]), Arguments = DecodeText(parts[3]), WorkingDirectory = DecodeText(parts[4]) } }); }
                    else throw new ArgumentException();
                }
                Validate(result); return result;
            } catch (ArgumentException) { throw new ArgumentException("The action sequence is invalid."); }
              catch { throw new ArgumentException("The action sequence is invalid."); }
        }
        public static void Validate(IList<SequenceStep> steps)
        {
            if (steps == null || steps.Count < 1 || steps.Count > MaxSteps) throw new ArgumentException("Use between 1 and " + MaxSteps + " sequence steps.");
            int totalWait = 0;
            foreach (var step in steps) {
                if (step == null) throw new ArgumentException("A sequence step is missing.");
                if (step.IsWait) { if (step.WaitMilliseconds < 1 || step.WaitMilliseconds > 60000) throw new ArgumentException("Each wait must be from 1 ms to 60 seconds."); totalWait += step.WaitMilliseconds; }
                else { if (step.Action.Kind == ActionKind.Sequence || step.Action.Kind == ActionKind.Monitor || step.Action.Kind == ActionKind.PassThrough || step.Action.Kind == ActionKind.Unbound) throw new ArgumentException("That action cannot be inside a sequence."); ConfigStore.Validate(step.Action, false); }
            }
            if (totalWait > 300000) throw new ArgumentException("A sequence can wait for up to five minutes total.");
        }
    }
}
