using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class PowerToysForm : Form
    {
        readonly TextBox search = new TextBox(), chord = new TextBox();
        readonly ListView list = new ListView();
        readonly Label detail = new Label(), feedback = new Label();
        readonly CheckBox enableModule = new CheckBox();
        readonly Button save = UiStyle.Button("Save in PowerToys", delegate { }, true), use = UiStyle.Button("Use on selected F key", delegate { });
        List<PowerToysShortcut> shortcuts = new List<PowerToysShortcut>();
        PowerToysShortcut selected;
        public string SelectedChord { get; private set; }

        public PowerToysForm(int functionIndex)
        {
            Text = "KeyWeave • PowerToys shortcuts"; Icon = Program.AppIcon(); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(940, 650); MinimumSize = new Size(760, 520);
            StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 5, ColumnCount = 1, BackColor = UiStyle.Canvas };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 154)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            Controls.Add(root);
            var title = UiStyle.Stack(); title.Controls.Add(UiStyle.Text("PowerToys shortcuts", 22, true));
            title.Controls.Add(UiStyle.Text("Browse module hotkeys. Edit supported ones, or use one with F" + (functionIndex + 1) + ".", 9, false)); root.Controls.Add(title, 0, 0);
            search.AccessibleDescription = "Search module, action, or shortcut";
            root.Controls.Add(UiStyle.Field("Search", search), 0, 1);
            list.Dock = DockStyle.Fill; list.View = View.Details; list.FullRowSelect = true; list.MultiSelect = false; list.HideSelection = false;
            list.BackColor = UiStyle.Surface; list.ForeColor = UiStyle.Ink; list.BorderStyle = BorderStyle.FixedSingle;
            list.Columns.Add("Module", 195); list.Columns.Add("PowerToys action", 310); list.Columns.Add("Shortcut", 180); list.Columns.Add("Status", 130);
            root.Controls.Add(list, 0, 2);
            var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Margin = new Padding(0, 12, 0, 0) };
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64)); editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
            detail.AutoSize = false; detail.Dock = DockStyle.Fill; detail.ForeColor = UiStyle.Muted; detail.Text = "Select a PowerToys shortcut.";
            editor.Controls.Add(detail, 0, 0); editor.SetColumnSpan(detail, 2);
            editor.Controls.Add(UiStyle.Field("PowerToys shortcut", chord), 0, 1);
            enableModule.Text = "Enable module if off"; enableModule.ForeColor = UiStyle.Ink; enableModule.Dock = DockStyle.Fill;
            editor.Controls.Add(enableModule, 1, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            var refresh = UiStyle.Button("Refresh", delegate { RefreshList(); });
            var settings = UiStyle.Button("Open PowerToys Settings", delegate { try { PowerToysIntegration.OpenSettings(); } catch (Exception ex) { SetFeedback(ex.Message, true); } });
            save.Click += delegate { SaveShortcut(); }; use.Click += delegate { UseShortcut(); };
            actions.Controls.Add(refresh); actions.Controls.Add(settings); actions.Controls.Add(save); actions.Controls.Add(use);
            editor.Controls.Add(actions, 0, 2); editor.SetColumnSpan(actions, 2); root.Controls.Add(editor, 0, 3);
            feedback.Dock = DockStyle.Fill; feedback.ForeColor = UiStyle.Muted; feedback.Text = "PowerToys owns these shortcuts. KeyWeave does not register them twice.";
            root.Controls.Add(feedback, 0, 4);
            list.SelectedIndexChanged += delegate { SelectShortcut(); }; search.TextChanged += delegate { PopulateList(); };
            RefreshList();
        }
        void SetFeedback(string message, bool error)
        {
            feedback.Text = message; feedback.ForeColor = error ? Color.FromArgb(255, 151, 153) : UiStyle.Muted;
        }
        void RefreshList()
        {
            try {
                shortcuts = PowerToysIntegration.Load(); PopulateList();
                SetFeedback(shortcuts.Count + " PowerToys shortcuts found. Changes to PowerToys may need a PowerToys restart to take effect.", false);
            } catch (Exception ex) { shortcuts = new List<PowerToysShortcut>(); PopulateList(); SetFeedback("Could not read PowerToys: " + ex.Message, true); }
        }
        void PopulateList()
        {
            selected = null; list.BeginUpdate(); list.Items.Clear();
            string query = search.Text.Trim();
            foreach (var item in shortcuts.Where(x => query.Length == 0 || (x.Module + " " + x.Action + " " + x.Chord).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)) {
                var row = new ListViewItem(item.Module); row.SubItems.Add(item.Action); row.SubItems.Add(item.Chord);
                row.SubItems.Add(item.ModuleEnabled ? "Enabled" : "Module off"); row.Tag = item; list.Items.Add(row);
            }
            list.EndUpdate(); SelectShortcut();
        }
        void SelectShortcut()
        {
            selected = list.SelectedItems.Count == 1 ? list.SelectedItems[0].Tag as PowerToysShortcut : null;
            detail.Text = selected == null ? "Select a PowerToys shortcut." : selected.Module + " • " + selected.Action + (selected.CanEdit ? "" : "  (edit in PowerToys Settings)");
            chord.Text = selected == null || selected.Chord == "Unassigned" ? "" : selected.Chord;
            chord.Enabled = selected != null && selected.CanEdit; save.Enabled = selected != null && selected.CanEdit;
            enableModule.Checked = selected != null; enableModule.Enabled = selected != null && selected.CanEdit && !selected.ModuleEnabled;
            use.Enabled = selected != null && selected.Chord != "Unassigned" && selected.ModuleEnabled;
        }
        void SaveShortcut()
        {
            if (selected == null) return;
            try {
                string backup = PowerToysIntegration.Save(selected, chord.Text.Trim(), enableModule.Checked);
                RefreshList(); SetFeedback("Saved in PowerToys with a backup. Restart PowerToys if the new shortcut is not active yet. Backup: " + backup, false);
            } catch (Exception ex) { SetFeedback("Could not complete: " + ex.Message, true); }
        }
        void UseShortcut()
        {
            if (selected == null || selected.Chord == "Unassigned" || !selected.ModuleEnabled) return;
            try { HotkeyChord.Parse(selected.Chord); SelectedChord = selected.Chord; DialogResult = DialogResult.OK; Close(); }
            catch (Exception ex) { SetFeedback("This shortcut cannot be sent by KeyWeave: " + ex.Message, true); }
        }
    }

}
