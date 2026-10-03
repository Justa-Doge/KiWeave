using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class PowerToysPanel : UserControl
    {
        readonly ListView activeList = new DesignListView();
        readonly ComboBox modules = new DesignComboBox(), actions = new DesignComboBox();
        readonly TextBox chord = new DesignTextBox();
        readonly CheckBox enableModule = new CheckBox();
        readonly Label title = UiStyle.Text("Select an active shortcut", 19, true);
        readonly Label summary = UiStyle.Text("Choose one from the list to edit it.", 9, false);
        readonly Label feedback = UiStyle.Text("PowerToys owns these shortcuts.", 9, false);
        readonly Button save = UiStyle.Button("Save in PowerToys", delegate { }, true);
        readonly Button cancel = UiStyle.Button("Cancel", delegate { });
        readonly Control moduleField, actionField, chordField;
        List<PowerToysShortcut> shortcuts = new List<PowerToysShortcut>();
        PowerToysShortcut selected;
        bool loading, adding;

        public PowerToysPanel(Action<ListView, string> prepareList)
        {
            BackColor = UiStyle.Canvas;
            var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63)); Controls.Add(columns);

            var left = new DesignCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 16, 0) }; columns.Controls.Add(left, 0, 0);
            var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            rows.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            rows.Controls.Add(UiStyle.Text("Active shortcuts", 9, true), 0, 0);
            prepareList(activeList, "HOTKEY"); activeList.ShowItemToolTips = true; rows.Controls.Add(activeList, 0, 1);
            var leftButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 12, 0, 0), Margin = Padding.Empty };
            leftButtons.Controls.Add(UiStyle.Button("Add shortcut", delegate { BeginAdd(); }));
            leftButtons.Controls.Add(UiStyle.Button("Refresh", delegate { RefreshShortcuts(true); })); rows.Controls.Add(leftButtons, 0, 2); left.Controls.Add(rows);

            var right = new DesignCard { Dock = DockStyle.Fill, Margin = Padding.Empty }; columns.Controls.Add(right, 1, 0);
            var stack = UiStyle.Stack(); right.Controls.Add(stack); stack.Controls.Add(title); stack.Controls.Add(summary);
            UiStyle.Combo(modules); UiStyle.Combo(actions);
            moduleField = UiStyle.Field("PowerToys module", modules); actionField = UiStyle.Field("Specific function", actions);
            chordField = UiStyle.Field("Shortcut", chord);
            stack.Controls.Add(moduleField, 0, 2); stack.Controls.Add(actionField, 0, 3); stack.Controls.Add(chordField, 0, 4);
            enableModule.Text = "Enable this PowerToys module"; enableModule.ForeColor = UiStyle.Ink; enableModule.AutoSize = true;
            enableModule.Dock = DockStyle.Top; enableModule.Margin = new Padding(0, 0, 0, 20); stack.Controls.Add(enableModule);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0, 0, 0, 18) };
            save.Click += delegate { SaveShortcut(); }; cancel.Click += delegate { EndAdd(); };
            buttons.Controls.Add(save); buttons.Controls.Add(cancel);
            buttons.Controls.Add(UiStyle.Button("Open PowerToys Settings", delegate { try { PowerToysIntegration.OpenSettings(); } catch (Exception ex) { SetFeedback(ex.Message, true); } }));
            stack.Controls.Add(buttons); feedback.MaximumSize = new Size(500, 0); stack.Controls.Add(feedback);

            activeList.SelectedIndexChanged += delegate { if (!loading && activeList.SelectedItems.Count == 1) SelectActive(); };
            modules.SelectedIndexChanged += delegate { if (!loading && adding) PopulateAddActions(); };
            actions.SelectedIndexChanged += delegate { if (!loading && adding) SelectAddAction(); };
            RefreshShortcuts(false);
        }

        internal static bool IsActive(PowerToysShortcut item)
        {
            return item.ModuleEnabled && !String.IsNullOrWhiteSpace(item.Chord) && item.Chord != "Unassigned";
        }

        void SetFeedback(string message, bool error) { feedback.Text = message; feedback.ForeColor = error ? Color.FromArgb(255, 151, 153) : UiStyle.Muted; }

        void RefreshShortcuts(bool preserve)
        {
            string keepModule = preserve && selected != null ? selected.Module : null, keepAction = preserve && selected != null ? selected.Action : null;
            try {
                shortcuts = PowerToysIntegration.Load(); loading = true; activeList.BeginUpdate(); activeList.Items.Clear();
                foreach (var item in shortcuts.Where(IsActive).OrderBy(x => x.Module, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Action, StringComparer.OrdinalIgnoreCase)) {
                    var row = new ListViewItem(item.Chord); row.SubItems.Add(item.Module + "  •  " + item.Action); row.Tag = item;
                    row.ToolTipText = item.Module + " / " + item.Action; activeList.Items.Add(row);
                }
                activeList.EndUpdate(); loading = false; adding = false;
                ListViewItem match = activeList.Items.Cast<ListViewItem>().FirstOrDefault(x => { var i = x.Tag as PowerToysShortcut; return i != null && i.Module == keepModule && i.Action == keepAction; });
                if (match == null && activeList.Items.Count > 0) match = activeList.Items[0];
                if (match != null) { match.Selected = true; match.Focused = true; SelectActive(); } else ShowEmpty();
                SetFeedback(activeList.Items.Count + " active PowerToys shortcuts. Off and unassigned functions are available through Add shortcut.", false);
            } catch (Exception ex) { loading = true; activeList.Items.Clear(); loading = false; shortcuts.Clear(); ShowEmpty(); SetFeedback("Could not read PowerToys: " + ex.Message, true); }
        }

        void SelectActive()
        {
            adding = false; selected = activeList.SelectedItems.Count == 1 ? activeList.SelectedItems[0].Tag as PowerToysShortcut : null;
            title.Text = selected == null ? "Select an active shortcut" : selected.Action;
            summary.Text = selected == null ? "Choose one from the list to edit it." : selected.Module + "  •  Enabled";
            moduleField.Visible = actionField.Visible = cancel.Visible = false; chordField.Visible = true; chord.Enabled = selected != null && selected.CanEdit;
            chord.Text = selected == null ? "" : selected.Chord; enableModule.Visible = false; save.Enabled = selected != null && selected.CanEdit;
            if (selected != null && !selected.CanEdit) SetFeedback("This shortcut is view-only here. Change it in PowerToys Settings.", false);
        }

        void BeginAdd()
        {
            adding = true; selected = null; loading = true; activeList.SelectedItems.Clear(); modules.Items.Clear();
            foreach (string module in shortcuts.Where(x => !IsActive(x)).Select(x => x.Module).Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) modules.Items.Add(module);
            if (modules.Items.Count > 0) modules.SelectedIndex = 0; loading = false;
            title.Text = "Add PowerToys shortcut"; summary.Text = "Choose an off or unassigned function, then give it a shortcut.";
            moduleField.Visible = actionField.Visible = cancel.Visible = true; enableModule.Visible = true; save.Enabled = false; PopulateAddActions();
            if (modules.Items.Count == 0) SetFeedback("Every discovered PowerToys function is already active.", false);
        }

        void PopulateAddActions()
        {
            loading = true; actions.Items.Clear(); string module = modules.SelectedItem as string;
            foreach (var item in shortcuts.Where(x => !IsActive(x) && x.Module == module).OrderBy(x => x.Action, StringComparer.OrdinalIgnoreCase)) actions.Items.Add(new ActionOption(item));
            if (actions.Items.Count > 0) actions.SelectedIndex = 0; loading = false; SelectAddAction();
        }

        void SelectAddAction()
        {
            var option = actions.SelectedItem as ActionOption; selected = option == null ? null : option.Shortcut;
            chord.Text = selected == null || selected.Chord == "Unassigned" ? "" : selected.Chord;
            chord.Enabled = selected != null && selected.CanEdit; enableModule.Checked = selected != null && !selected.ModuleEnabled;
            enableModule.Enabled = selected != null && selected.CanEdit && !selected.ModuleEnabled; save.Enabled = selected != null && selected.CanEdit;
            if (selected != null && !selected.CanEdit) SetFeedback("This function can only be configured in PowerToys Settings.", false);
        }

        void EndAdd()
        {
            adding = false; if (activeList.Items.Count > 0) { activeList.Items[0].Selected = true; SelectActive(); } else ShowEmpty();
        }

        void ShowEmpty()
        {
            selected = null; adding = false; title.Text = "No active PowerToys shortcuts"; summary.Text = "Choose Add shortcut to create one.";
            moduleField.Visible = actionField.Visible = enableModule.Visible = cancel.Visible = false; chordField.Visible = true; chord.Text = ""; chord.Enabled = save.Enabled = false;
        }

        void SaveShortcut()
        {
            if (selected == null || !selected.CanEdit) return;
            if (String.IsNullOrWhiteSpace(chord.Text)) { SetFeedback("Enter a shortcut before saving.", true); return; }
            try {
                string backup = PowerToysIntegration.Save(selected, chord.Text.Trim(), adding && (!selected.ModuleEnabled || enableModule.Checked));
                RefreshShortcuts(true); SetFeedback("Saved in PowerToys. Original settings backed up to " + backup, false);
            } catch (Exception ex) { SetFeedback("Could not complete: " + ex.Message, true); }
        }

        sealed class ActionOption
        {
            public readonly PowerToysShortcut Shortcut; public ActionOption(PowerToysShortcut shortcut) { Shortcut = shortcut; }
            public override string ToString() { return Shortcut.Action; }
        }
    }
}
