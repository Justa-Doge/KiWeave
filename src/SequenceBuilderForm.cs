using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class SequenceBuilderForm : Form
    {
        readonly List<SequenceStep> steps;
        readonly ListBox list = new DesignListBox(), actions = new DesignListBox();
        readonly TextBox search = new DesignTextBox();
        readonly Label summary = UiStyle.Text("", 10, false);
        readonly NumericUpDown wait = new DesignNumericUpDown { Minimum = 0.001M, Maximum = 60, Value = 1, Increment = 0.25M, DecimalPlaces = 3, Width = 90 };
        readonly MainForm.SpecificChoice[] catalog = ActionPickerForm.Catalog(false);
        public List<SequenceStep> Result { get { return steps.Select(s => s.Copy()).ToList(); } }
        public SequenceBuilderForm(IEnumerable<SequenceStep> initial)
        {
            steps = (initial ?? Enumerable.Empty<SequenceStep>()).Select(s => s.Copy()).ToList();
            Text = "Sequence builder"; Font = new Font("Segoe UI", 10F); AutoScaleMode = AutoScaleMode.Dpi;
            Icon = Program.AppIcon();
            ClientSize = new Size(1000, 720); MinimumSize = new Size(900, 660);
            StartPosition = FormStartPosition.CenterParent; BackColor = UiStyle.Canvas; ShowInTaskbar = false; Design.DarkTitlebar(this);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 3 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72)); Controls.Add(root);
            var heading = UiStyle.Stack(); heading.Controls.Add(UiStyle.Text("One shortcut. A whole routine.", 23, true));
            heading.Controls.Add(UiStyle.Text("Add actions and pauses. Your steps run from top to bottom.", 10, false)); root.Controls.Add(heading, 0, 0);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57)); root.Controls.Add(body, 0, 1);
            var library = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = UiStyle.Surface, Padding = new Padding(16), ColumnCount = 1, RowCount = 4, Margin = new Padding(0, 0, 16, 0) };
            library.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); library.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            library.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); library.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); library.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            library.Controls.Add(UiStyle.Text("Search action library", 9, true), 0, 0); search.AccessibleName = "Search sequence actions"; search.Dock = DockStyle.Top; library.Controls.Add(new InputFrame(search), 0, 1);
            actions.Dock = DockStyle.Fill; actions.BorderStyle = BorderStyle.None; actions.IntegralHeight = false; actions.ItemHeight = 42; actions.BackColor = UiStyle.Surface; actions.ForeColor = UiStyle.Ink; actions.DrawMode = DrawMode.OwnerDrawFixed;
            Design.DarkNative(actions);
            actions.DrawItem += DrawLibraryItem; library.Controls.Add(actions, 0, 2);
            var add = UiStyle.Button("+ Add action", delegate { AddAction(); }); add.Margin = new Padding(0, 10, 0, 0); library.Controls.Add(add, 0, 3); body.Controls.Add(library, 0, 0);
            var chain = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = UiStyle.Surface, Padding = new Padding(16), ColumnCount = 1, RowCount = 5 };
            chain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); chain.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            chain.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); chain.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            chain.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); chain.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            chain.Controls.Add(UiStyle.Text("Your sequence", 9, true), 0, 0);
            list.Dock = DockStyle.Fill; list.BorderStyle = BorderStyle.None; list.IntegralHeight = false; list.DrawMode = DrawMode.OwnerDrawFixed; list.ItemHeight = 74; list.BackColor = UiStyle.Surface; list.DrawItem += DrawStep; chain.Controls.Add(list, 0, 1);
            Design.DarkNative(list);
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
            tools.Controls.Add(UiStyle.Button("Up", delegate { MoveStep(-1); })); tools.Controls.Add(UiStyle.Button("Down", delegate { MoveStep(1); }));
            tools.Controls.Add(UiStyle.Button("Edit", delegate { EditStep(); })); tools.Controls.Add(UiStyle.Button("Duplicate", delegate { DuplicateStep(); })); tools.Controls.Add(UiStyle.Button("Remove", delegate { if (list.SelectedIndex >= 0) { int i = list.SelectedIndex; steps.RemoveAt(i); RefreshList(i); } }));
            foreach (Button b in tools.Controls) { b.MinimumSize = new Size(55, 34); b.Padding = new Padding(7, 4, 7, 4); b.Margin = new Padding(0, 0, 6, 0); }
            chain.Controls.Add(tools, 0, 2); chain.Controls.Add(UiStyle.Text("Add a pause", 9, true), 0, 3);
            var waitRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            wait.BackColor = UiStyle.Input; wait.ForeColor = UiStyle.Ink; wait.BorderStyle = BorderStyle.FixedSingle; wait.Margin = new Padding(0, 10, 0, 0); waitRow.Controls.Add(wait); var seconds = UiStyle.Text("seconds", 10, false); seconds.Margin = new Padding(6, 6, 14, 0); waitRow.Controls.Add(seconds);
            waitRow.Controls.Add(UiStyle.Button("+ Add wait", delegate { AddStep(new SequenceStep { WaitMilliseconds = (int)(wait.Value * 1000) }); }));
            chain.Controls.Add(waitRow, 0, 4); body.Controls.Add(chain, 1, 0);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 16, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); summary.Margin = new Padding(0, 8, 0, 0); footer.Controls.Add(summary, 0, 0);
            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            var cancel = UiStyle.Button("Cancel", delegate { DialogResult = DialogResult.Cancel; Close(); });
            var save = UiStyle.Button("Use sequence", delegate { try { SequenceCodec.Validate(steps); SequenceCodec.Serialize(steps); DialogResult = DialogResult.OK; Close(); } catch (Exception ex) { summary.Text = ex.Message; summary.ForeColor = Color.FromArgb(255, 151, 153); } }, true);
            buttons.Controls.Add(cancel); buttons.Controls.Add(save); footer.Controls.Add(buttons, 1, 0); root.Controls.Add(footer, 0, 2); CancelButton = cancel;
            search.TextChanged += delegate { Filter(); }; actions.DoubleClick += delegate { AddAction(); }; list.DoubleClick += delegate { EditStep(); }; Filter(); RefreshList(0);
        }
        void Filter()
        {
            var terms = search.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); actions.BeginUpdate(); actions.Items.Clear();
            foreach (var c in catalog) if (terms.All(t => c.SearchText.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)) actions.Items.Add(c);
            actions.EndUpdate(); if (actions.Items.Count > 0) actions.SelectedIndex = 0;
        }
        void AddAction()
        {
            var c = actions.SelectedItem as MainForm.SpecificChoice; if (c == null) return;
            Mapping m = c.Mapping.Copy();
            if (String.IsNullOrEmpty(m.Target) && m.Kind != ActionKind.LockThenSleep) {
                using (var d = new StepDetailsForm(m)) { if (d.ShowDialog(this) != DialogResult.OK) return; m = d.Result; }
            }
            AddStep(new SequenceStep { Action = m });
        }
        void AddStep(SequenceStep step)
        {
            if (steps.Count >= SequenceCodec.MaxSteps) { summary.Text = "A sequence supports up to " + SequenceCodec.MaxSteps + " steps."; return; }
            steps.Add(step); RefreshList(steps.Count - 1);
        }
        void EditStep()
        {
            int i = list.SelectedIndex; if (i < 0) return;
            using (var d = steps[i].IsWait ? new StepDetailsForm(steps[i].WaitMilliseconds) : new StepDetailsForm(steps[i].Action)) {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                steps[i] = d.Result == null ? new SequenceStep { WaitMilliseconds = d.WaitMilliseconds } : new SequenceStep { Action = d.Result }; RefreshList(i);
            }
        }
        void RefreshList(int selected)
        {
            list.BeginUpdate(); list.Items.Clear(); foreach (var s in steps) list.Items.Add(s); list.EndUpdate();
            if (steps.Count > 0) list.SelectedIndex = Math.Min(Math.Max(0, selected), steps.Count - 1);
            summary.ForeColor = UiStyle.Muted; summary.Text = steps.Count == 0 ? "Add an action to start your routine." : ActionInsights.Sequence(steps).Compact;
        }
        void DuplicateStep()
        {
            int i = list.SelectedIndex; if (i < 0) return;
            if (steps.Count >= SequenceCodec.MaxSteps) { summary.ForeColor = Color.FromArgb(255, 151, 153); summary.Text = "A sequence supports up to " + SequenceCodec.MaxSteps + " steps."; return; }
            steps.Insert(i + 1, steps[i].Copy()); RefreshList(i + 1);
        }
        void MoveStep(int amount)
        {
            int i = list.SelectedIndex, n = i + amount; if (i < 0 || n < 0 || n >= steps.Count) return;
            var item = steps[i]; steps.RemoveAt(i); steps.Insert(n, item); RefreshList(n);
        }
        void DrawLibraryItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return; bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(selected ? UiStyle.Soft : UiStyle.Surface)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, actions.Items[e.Index].ToString(), Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height), selected ? UiStyle.Blue : UiStyle.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        void DrawStep(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return; var step = (SequenceStep)list.Items[e.Index]; bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(UiStyle.Surface)) e.Graphics.FillRectangle(b, e.Bounds);
            var rect = new Rectangle(e.Bounds.X, e.Bounds.Y + 3, e.Bounds.Width - 2, e.Bounds.Height - 6);
            Color color = step.IsWait ? Color.FromArgb(240, 192, 112) : UiStyle.Blue;
            Color fill = step.IsWait ? Color.FromArgb(59, 47, 32) : UiStyle.Soft;
            Design.Box(e.Graphics, rect, fill, selected ? color : UiStyle.Border, 10);
            var number = new Rectangle(rect.X + 12, rect.Y + 18, 32, 32);
            Design.Box(e.Graphics, number, UiStyle.Surface, UiStyle.Border, 8);
            TextRenderer.DrawText(e.Graphics, (e.Index + 1).ToString("00"), Font, new Rectangle(rect.X + 12, rect.Y, 34, rect.Height), color, TextFormatFlags.VerticalCenter);
            var preset = step.IsWait ? null : catalog.FirstOrDefault(c => c.Matches(step.Action));
            string caption = step.IsWait ? "Wait " + (step.WaitMilliseconds / 1000.0).ToString("0.###") + "s" : preset == null ? step.Summary : preset.Label;
            TextRenderer.DrawText(e.Graphics, caption, Font, new Rectangle(rect.X + 50, rect.Y, rect.Width - 58, rect.Height), UiStyle.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class StepDetailsForm : Form
    {
        internal Mapping Result; internal int WaitMilliseconds;
        internal StepDetailsForm(int milliseconds) : this(null, milliseconds) { }
        internal StepDetailsForm(Mapping mapping) : this(mapping, 0) { }
        StepDetailsForm(Mapping mapping, int milliseconds)
        {
            Text = mapping == null ? "Edit pause" : "Edit action"; Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
            Icon = Program.AppIcon();
            ClientSize = new Size(540, 560); MinimumSize = new Size(500, 500); AutoScroll = true; StartPosition = FormStartPosition.CenterParent; BackColor = UiStyle.Canvas; ShowInTaskbar = false; Design.DarkTitlebar(this);
            var root = UiStyle.Stack(); root.Padding = new Padding(24); Controls.Add(root);
            root.Controls.Add(UiStyle.Text(Text, 20, true));
            var target = new DesignTextBox { Text = mapping == null ? "" : mapping.Target };
            var args = new DesignTextBox { Text = mapping == null ? "" : mapping.Arguments };
            var work = new DesignTextBox { Text = mapping == null ? "" : mapping.WorkingDirectory };
            var delay = new DesignNumericUpDown { Minimum = 0.001M, Maximum = 60, DecimalPlaces = 3, Value = Math.Max(0.001M, milliseconds / 1000M) };
            if (mapping == null) root.Controls.Add(UiStyle.Field("Seconds", delay));
            else {
                bool launch = (mapping.Kind >= ActionKind.Application && mapping.Kind <= ActionKind.Command) || mapping.Kind == ActionKind.Python;
                if (mapping.Kind != ActionKind.LockThenSleep) root.Controls.Add(UiStyle.Field(launch ? "File path" : "Key or shortcut", target));
                if (launch) {
                    var browse = UiStyle.Button("Browse...", delegate { using (var d = new OpenFileDialog { Filter = mapping.Kind == ActionKind.Python ? "Python scripts|*.py" : "All files|*.*", DereferenceLinks = false }) if (d.ShowDialog(this) == DialogResult.OK) target.Text = d.FileName; });
                    root.Controls.Add(browse);
                }
                if (mapping.Kind == ActionKind.Application || mapping.Kind == ActionKind.Command || mapping.Kind == ActionKind.Python) { root.Controls.Add(UiStyle.Field("Arguments (optional)", args)); root.Controls.Add(UiStyle.Field("Working folder (optional)", work)); }
            }
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 16, 0, 0) };
            var ok = UiStyle.Button("Use step", delegate {
                try {
                    if (mapping == null) WaitMilliseconds = (int)(delay.Value * 1000);
                    else { Result = mapping.Copy(); Result.Target = target.Text.Trim(); Result.Arguments = args.Text; Result.WorkingDirectory = work.Text.Trim(); ConfigStore.Validate(Result, false); }
                    DialogResult = DialogResult.OK; Close();
                } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Check this step", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            }, true);
            var cancel = UiStyle.Button("Cancel", delegate { DialogResult = DialogResult.Cancel; Close(); });
            buttons.Controls.Add(ok); buttons.Controls.Add(cancel); root.Controls.Add(buttons); AcceptButton = ok; CancelButton = cancel;
        }
    }
}
