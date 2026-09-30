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
