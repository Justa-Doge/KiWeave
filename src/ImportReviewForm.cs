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
        internal Configuration SelectedConfiguration { get; private set; }
        readonly Configuration source;
        readonly CheckedListBox choices = new CheckedListBox();
        sealed class Choice { internal int Layer = -1, Slot = -1, Hotkey = -1; internal Mapping Mapping; public override string ToString() { return Mapping == null ? "" : Mapping.Summary; } }
        internal ImportReviewForm(Configuration configuration, string sourceName)
        {
            source = configuration == null ? new Configuration() : configuration.Copy();
            Text = "KiWeave import review"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(860, 650); MinimumSize = new Size(820, 540); Design.DarkTitlebar(this);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); Controls.Add(root);
            var heading = UiStyle.Stack(); heading.Controls.Add(UiStyle.Text("Review this private import", 21, true));
            heading.Controls.Add(UiStyle.Text("Nothing below has run or changed your active setup. Review every target, command, URL, and permission before staging it in the editor.", 9, false)); root.Controls.Add(heading, 0, 0);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 14, 0, 8) }; body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            var details = new DesignTextBox { Multiline = true, ReadOnly = true, TabStop = false, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, WordWrap = false, Text = ActionPrivacy.Review(source, sourceName), BackColor = UiStyle.Surface, ForeColor = UiStyle.Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9.25f), Margin = new Padding(0) }; body.Controls.Add(details, 0, 0);
            choices.Dock = DockStyle.Fill; choices.CheckOnClick = true; choices.BackColor = UiStyle.Surface; choices.ForeColor = UiStyle.Ink; choices.BorderStyle = BorderStyle.FixedSingle; choices.Margin = new Padding(12, 0, 0, 0); body.Controls.Add(choices, 1, 0); PopulateChoices(); root.Controls.Add(body, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 10, 0, 0) };
            buttons.Controls.Add(UiStyle.Button("Stage selected actions", delegate { Approved = true; SelectedConfiguration = BuildSelection(); DialogResult = DialogResult.OK; Close(); }, true));
            buttons.Controls.Add(UiStyle.Button("Cancel", delegate { DialogResult = DialogResult.Cancel; Close(); })); root.Controls.Add(buttons, 0, 2);
        }
        void PopulateChoices()
        {
            choices.Items.Add("Select individual actions to stage:", false);
            for (int i = 0; i < 12; i++) AddChoice("Base F" + (i + 1), -1, i, -1, source.Mappings[i]);
            for (int l = 0; l < (source.Layers ?? new ModifierLayer[0]).Length; l++) for (int i = 0; i < 12; i++) AddChoice(source.Layers[l].Name + " F" + (i + 1), l, i, -1, source.Layers[l].Mappings[i]);
            for (int h = 0; h < (source.CustomHotkeys ?? new CustomHotkey[0]).Length; h++) AddChoice("Hotkey " + source.CustomHotkeys[h].Shortcut, -1, -1, h, source.CustomHotkeys[h].Action);
            for (int i = 1; i < choices.Items.Count; i++) choices.SetItemChecked(i, true);
        }
        void AddChoice(string label, int layer, int slot, int hotkey, Mapping mapping)
        {
            if (mapping == null || mapping.Kind == ActionKind.PassThrough || mapping.Kind == ActionKind.Unbound) return;
            choices.Items.Add(new ChoiceLabel(label, new Choice { Layer = layer, Slot = slot, Hotkey = hotkey, Mapping = mapping }), true);
        }
        sealed class ChoiceLabel
        {
            internal readonly string Label; internal readonly Choice Choice;
            internal ChoiceLabel(string label, Choice choice) { Label = label; Choice = choice; }
            public override string ToString() { return Label + "  ·  " + Choice.Mapping.Summary; }
        }
        Configuration BuildSelection()
        {
            var selected = source.Copy();
            for (int i = 0; i < 12; i++) if (!IsChecked(-1, i, -1)) selected.Mappings[i] = new Mapping { Kind = ActionKind.PassThrough };
            for (int l = 0; l < selected.Layers.Length; l++) for (int i = 0; i < 12; i++) if (!IsChecked(l, i, -1)) selected.Layers[l].Mappings[i] = new Mapping { Kind = ActionKind.PassThrough };
            selected.CustomHotkeys = selected.CustomHotkeys.Where((h, i) => IsChecked(-1, -1, i)).Select(h => h.Copy()).ToArray();
            return selected;
        }
        bool IsChecked(int layer, int slot, int hotkey)
        {
            for (int i = 1; i < choices.Items.Count; i++) { var item = choices.Items[i] as ChoiceLabel; if (item != null && item.Choice.Layer == layer && item.Choice.Slot == slot && item.Choice.Hotkey == hotkey) return choices.GetItemChecked(i); }
            return false;
        }
    }
}
