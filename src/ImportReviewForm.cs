using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal static class ActionPrivacy
    {
        static readonly string[] RiskOrder = { "LOCAL ONLY", "SENDS INPUT", "CHANGES WINDOWS OR APP STATE", "CHANGES MONITOR HARDWARE", "OPENS OR RUNS LOCAL CONTENT", "RUNS MULTIPLE STEPS", "USES NETWORK" };
        internal static string Risk(Mapping mapping)
        {
            if (mapping == null) return "Invalid action";
            if (mapping.Kind == ActionKind.Conditional) {
                try {
                    var rule = ConditionalCodec.Parse(mapping.Target);
                    return "CONDITIONAL: " + Strongest(Risk(rule.WhenMatched), Risk(rule.Otherwise));
                } catch { return "INVALID CONDITIONAL ACTION"; }
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
            for (int i = 0; i < 12; i++) yield return new KeyValuePair<string, Mapping>("Base F" + (i + 1), configuration.Mappings[i]);
            foreach (ModifierLayer layer in configuration.Layers ?? new ModifierLayer[0]) for (int i = 0; i < 12; i++) yield return new KeyValuePair<string, Mapping>(layer.Name + " F" + (i + 1), layer.Mappings[i]);
            foreach (CustomHotkey hotkey in configuration.CustomHotkeys ?? new CustomHotkey[0]) yield return new KeyValuePair<string, Mapping>("Hotkey " + hotkey.Shortcut, hotkey.Action);
        }
        internal static string Review(Configuration configuration, string sourceName)
        {
            var active = Mappings(configuration).Where(x => x.Value != null && x.Value.Kind != ActionKind.PassThrough && x.Value.Kind != ActionKind.Unbound).ToArray();
            Mapping[] effects = active.SelectMany(x => Effects(x.Value)).ToArray();
            int network = effects.Count(x => x.Kind == ActionKind.HttpRequest), executable = effects.Count(x => Risk(x) == "OPENS OR RUNS LOCAL CONTENT"), hardware = effects.Count(x => x.Kind == ActionKind.Monitor);
            var text = new StringBuilder(); text.AppendLine("IMPORT QUARANTINE"); text.AppendLine("Source file: " + Path.GetFileName(sourceName)); text.AppendLine();
            text.AppendLine("12 base keys, " + (configuration.Layers ?? new ModifierLayer[0]).Length + " layers, " + (configuration.CustomHotkeys ?? new CustomHotkey[0]).Length + " custom hotkeys");
            text.AppendLine(active.Length + " non-default actions: " + network + " network, " + executable + " local launch/open, " + hardware + " hardware"); text.AppendLine();
            if (active.Length == 0) text.AppendLine("No non-default actions are present.");
            foreach (var item in active) {
                Mapping m = item.Value; text.AppendLine(item.Key + "  [" + Risk(m) + "]"); text.AppendLine("  " + m.Summary);
                if (m.Kind == ActionKind.Conditional) {
                    try {
                        var rule = ConditionalCodec.Parse(m.Target);
                        text.AppendLine("  Match: " + rule.WhenMatched.Summary + "  [" + Risk(rule.WhenMatched) + "]");
                        text.AppendLine("  Otherwise: " + rule.Otherwise.Summary + "  [" + Risk(rule.Otherwise) + "]");
                    } catch { text.AppendLine("  The condition could not be decoded safely."); }
                }
                if (m.Kind == ActionKind.Application || m.Kind == ActionKind.FileOrFolder || m.Kind == ActionKind.WindowsShortcut || m.Kind == ActionKind.Command || m.Kind == ActionKind.Python || m.Kind == ActionKind.HttpRequest) {
                    if (!String.IsNullOrWhiteSpace(m.Target)) text.AppendLine("  Target: " + m.Target);
                    if (!String.IsNullOrWhiteSpace(m.Arguments)) text.AppendLine("  Arguments/body: " + m.Arguments);
                    if (!String.IsNullOrWhiteSpace(m.WorkingDirectory)) text.AppendLine("  Working directory: " + m.WorkingDirectory);
                }
                text.AppendLine();
            }
            return text.ToString();
        }
    }

    internal sealed class ImportReviewForm : Form
    {
        internal bool Approved { get; private set; }
        internal ImportReviewForm(Configuration configuration, string sourceName)
        {
            Text = "KiWeave import review"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(860, 650); MinimumSize = new Size(820, 540); Design.DarkTitlebar(this);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); Controls.Add(root);
            var heading = UiStyle.Stack(); heading.Controls.Add(UiStyle.Text("Review this private import", 21, true));
            heading.Controls.Add(UiStyle.Text("Nothing below has run or changed your active setup. Review every target, command, URL, and permission before staging it in the editor.", 9, false)); root.Controls.Add(heading, 0, 0);
            var details = new TextBox { Multiline = true, ReadOnly = true, TabStop = false, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, WordWrap = false, Text = ActionPrivacy.Review(configuration, sourceName), BackColor = UiStyle.Surface, ForeColor = UiStyle.Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9.25f), Margin = new Padding(0, 14, 0, 8) }; root.Controls.Add(details, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 10, 0, 0) };
            buttons.Controls.Add(UiStyle.Button("Stage reviewed import", delegate { Approved = true; DialogResult = DialogResult.OK; Close(); }, true));
            buttons.Controls.Add(UiStyle.Button("Cancel", delegate { DialogResult = DialogResult.Cancel; Close(); })); root.Controls.Add(buttons, 0, 2);
        }
    }
}
