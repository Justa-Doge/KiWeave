using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class ActionPickerForm : Form
    {
        readonly TextBox search = new TextBox();
        readonly ComboBox category = new DesignComboBox();
        readonly ListBox results = new ListBox();
        readonly Label count = UiStyle.Text("", 9, false);
        readonly MainForm.SpecificChoice[] catalog;
        readonly Button choose;
        internal Mapping SelectedAction { get; private set; }
        internal static MainForm.SpecificChoice[] Catalog(bool allowSequence)
        {
            return new[] { 0, 1, 2, 3 }.SelectMany(g => MainForm.ChoicesFor(g, true))
                .Where(c => c.Label != "Choose an action" && (allowSequence || c.Mapping.Kind != ActionKind.Sequence))
                .GroupBy(c => c.Label).Select(g => g.First()).OrderBy(c => c.Label).ToArray();
        }
        public ActionPickerForm(bool allowSequence)
        {
            Text = "Action library"; Font = new Font("Segoe UI", 10);
            Icon = Program.AppIcon();
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(660, 780); MinimumSize = new Size(560, 650);
            StartPosition = FormStartPosition.CenterParent; BackColor = UiStyle.Canvas; ShowInTaskbar = false;
            Design.DarkTitlebar(this);
            catalog = Catalog(allowSequence);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 6 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            Controls.Add(root);
            var header = UiStyle.Stack(); header.Controls.Add(UiStyle.Text("Find your action", 22, true));
            header.Controls.Add(UiStyle.Text("Search the library, then choose an action.", 10, false)); root.Controls.Add(header, 0, 0);
            search.AccessibleName = "Search actions"; root.Controls.Add(UiStyle.Field("Search actions", search), 0, 1);
            UiStyle.Combo(category); category.AccessibleName = "Action category filter"; category.Items.Add("All categories");
            category.Items.AddRange(catalog.Select(c => c.Category).Distinct().OrderBy(c => c).ToArray()); category.SelectedIndex = 0;
            root.Controls.Add(UiStyle.Field("Category", category), 0, 2);
            results.Dock = DockStyle.Fill; results.BorderStyle = BorderStyle.None; results.BackColor = UiStyle.Surface;
            Design.DarkNative(results);
            results.DrawMode = DrawMode.OwnerDrawFixed; results.ItemHeight = 62; results.IntegralHeight = false;
            results.DrawItem += DrawAction; root.Controls.Add(results, 0, 3);
            count.Margin = new Padding(0, 8, 0, 0); count.AutoSize = false; count.Dock = DockStyle.Fill; root.Controls.Add(count, 0, 4);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            choose = UiStyle.Button("Use action", delegate { SelectAction(); }, true);
            var cancel = UiStyle.Button("Cancel", delegate { DialogResult = DialogResult.Cancel; Close(); });
            buttons.Controls.Add(choose); buttons.Controls.Add(cancel); root.Controls.Add(buttons, 0, 5);
            CancelButton = cancel; AcceptButton = choose;
            search.TextChanged += delegate { Filter(); }; results.DoubleClick += delegate { SelectAction(); };
            results.SelectedIndexChanged += delegate { choose.Enabled = results.SelectedIndex >= 0; DescribeSelection(); };
            category.SelectedIndexChanged += delegate { Filter(); };
            search.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Down && results.Items.Count > 0) { results.Focus(); e.Handled = true; } };
            Shown += delegate { search.Focus(); }; Filter();
        }
        void Filter()
        {
            string[] terms = search.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            results.BeginUpdate(); results.Items.Clear();
            foreach (var c in catalog) {
                if (category.SelectedIndex > 0 && c.Category != category.SelectedItem.ToString()) continue;
                if (terms.All(t => c.SearchText.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)) results.Items.Add(c);
            }
            results.EndUpdate(); if (results.Items.Count > 0) results.SelectedIndex = 0;
            choose.Enabled = results.Items.Count > 0;
            DescribeSelection();
        }
        void DescribeSelection()
        {
            var selected = results.SelectedItem as MainForm.SpecificChoice;
            count.Text = results.Items.Count == 0 ? "No matches. Try another word or category." : results.Items.Count + " / " + catalog.Length + " actions";
            if (selected != null) count.Text += "\n" + (String.IsNullOrEmpty(selected.Context) ? "Runs only when you press the saved hotkey. Keyboard actions use the focused app." : selected.Context);
        }
        void SelectAction()
        {
            var c = results.SelectedItem as MainForm.SpecificChoice;
            if (c == null) return; SelectedAction = c.Mapping.Copy(); DialogResult = DialogResult.OK; Close();
        }
        void DrawAction(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return; bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var brush = new SolidBrush(UiStyle.Surface)) e.Graphics.FillRectangle(brush, e.Bounds);
            if (selected) Design.Box(e.Graphics, new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 3, e.Bounds.Width - 5, e.Bounds.Height - 6), UiStyle.Soft, UiStyle.Soft, 10);
            var c = (MainForm.SpecificChoice)results.Items[e.Index];
            var icon = new Rectangle(e.Bounds.X + 12, e.Bounds.Y + 13, 36, 36);
            Design.Box(e.Graphics, icon, selected ? UiStyle.Input : UiStyle.Canvas, UiStyle.Border, 9);
            Design.Glyph(e.Graphics, ActionGlyph(c.Mapping.Kind), icon, UiStyle.Blue);
            var area = new Rectangle(e.Bounds.X + 62, e.Bounds.Y + 9, e.Bounds.Width - 75, 25);
            TextRenderer.DrawText(e.Graphics, c.Label, Font, area, selected ? UiStyle.Blue : UiStyle.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var small = new Font("Segoe UI", 8.5f)) TextRenderer.DrawText(e.Graphics, c.Category + " · " + ActionType(c.Mapping.Kind), small, new Rectangle(area.X, e.Bounds.Y + 34, area.Width, 18), UiStyle.Muted, TextFormatFlags.EndEllipsis);
        }
        internal static string ActionGlyph(ActionKind kind) { return kind == ActionKind.Media ? "\uE8D6" : kind == ActionKind.LockThenSleep ? "\uE72E" : kind == ActionKind.Sequence ? "\uE8FD" : kind == ActionKind.Python || kind == ActionKind.Command ? "\uE943" : kind == ActionKind.SendKey || kind == ActionKind.SendShortcut ? "\uE765" : "\uE8A7"; }
        internal static string ActionType(ActionKind kind) { return kind == ActionKind.Media ? "Media and sound" : kind == ActionKind.LockThenSleep ? "Power action" : kind == ActionKind.Sequence ? "Multiple steps" : kind == ActionKind.Python ? "Python script" : kind == ActionKind.Command ? "Command or script" : kind == ActionKind.SendKey || kind == ActionKind.SendShortcut ? "Keyboard action" : "Open or launch"; }
    }
}
