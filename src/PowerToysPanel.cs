using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class PowerToysPanel : UserControl
    {
        readonly ComboBox modules = new DesignComboBox(), actions = new DesignComboBox();
        readonly TextBox chord = new TextBox();
        readonly CheckBox enableModule = new CheckBox();
        readonly Label detail = UiStyle.Text("Choose a PowerToys function to see its shortcut.", 10, false);
        readonly Label feedback = UiStyle.Text("PowerToys owns these shortcuts. Changes here are saved in PowerToys.", 9, false);
        readonly Button save = UiStyle.Button("Save in PowerToys", delegate { }, true);
        List<PowerToysShortcut> shortcuts = new List<PowerToysShortcut>();
        PowerToysShortcut selected;
        bool loading;

        public PowerToysPanel()
        {
            BackColor = UiStyle.Canvas;
            var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63));
            Controls.Add(columns);
            var left = new DesignCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 16, 0) };
            columns.Controls.Add(left, 0, 0);
            var leftStack = UiStyle.Stack(); left.Controls.Add(leftStack);
            leftStack.Controls.Add(UiStyle.Text("PowerToys library", 17, true));
            leftStack.Controls.Add(UiStyle.Text("Pick a module, then its function.", 9, false));
            UiStyle.Combo(modules); UiStyle.Combo(actions);
            leftStack.Controls.Add(UiStyle.Field("Module", modules));
            leftStack.Controls.Add(UiStyle.Field("Specific function", actions));
            var refresh = UiStyle.Button("Refresh shortcuts", delegate { RefreshShortcuts(true); });
            refresh.Margin = new Padding(0, 4, 0, 12);
            leftStack.Controls.Add(refresh);
            leftStack.Controls.Add(UiStyle.Text("PowerToys may need a restart after its shortcut changes.", 9, false));

            var right = new DesignCard { Dock = DockStyle.Fill, Margin = Padding.Empty };
            columns.Controls.Add(right, 1, 0);
            var rightStack = UiStyle.Stack(); right.Controls.Add(rightStack);
            rightStack.Controls.Add(UiStyle.Text("Shortcut settings", 19, true));
            rightStack.Controls.Add(detail);
            rightStack.Controls.Add(UiStyle.Field("PowerToys shortcut", chord));
            enableModule.Text = "Enable this PowerToys module if it is off";
            enableModule.ForeColor = UiStyle.Ink; enableModule.Dock = DockStyle.Top;
            enableModule.AutoSize = true; enableModule.Margin = new Padding(0, 0, 0, 20);
            rightStack.Controls.Add(enableModule);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0, 0, 0, 18) };
            save.Click += delegate { SaveShortcut(); };
            buttons.Controls.Add(save);
            buttons.Controls.Add(UiStyle.Button("Open PowerToys Settings", delegate {
                try { PowerToysIntegration.OpenSettings(); }
                catch (Exception ex) { SetFeedback(ex.Message, true); }
            }));
            rightStack.Controls.Add(buttons);
            feedback.MaximumSize = new Size(500, 0); rightStack.Controls.Add(feedback);
            modules.SelectedIndexChanged += delegate { if (!loading) PopulateActions(null); };
            actions.SelectedIndexChanged += delegate { if (!loading) SelectAction(); };
            RefreshShortcuts(false);
        }

        void SetFeedback(string message, bool error)
        {
            feedback.Text = message;
            feedback.ForeColor = error ? Color.FromArgb(255, 151, 153) : UiStyle.Muted;
        }

        void RefreshShortcuts(bool preserveSelection)
        {
            string module = preserveSelection ? modules.SelectedItem as string : null;
            string action = preserveSelection && selected != null ? selected.Action : null;
            try {
                shortcuts = PowerToysIntegration.Load();
                loading = true; modules.Items.Clear();
                foreach (string name in shortcuts.Select(x => x.Module).Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) modules.Items.Add(name);
                if (modules.Items.Count > 0) {
                    string initial = module ?? (shortcuts.Any(x => x.Module == "ColorPicker") ? "ColorPicker" : modules.Items[0] as string);
                    modules.SelectedItem = modules.Items.Contains(initial) ? initial : modules.Items[0];
                }
                loading = false; PopulateActions(action);
                SetFeedback(shortcuts.Count + " PowerToys shortcuts found. Select a module and function above.", false);
            } catch (Exception ex) {
                loading = true; shortcuts.Clear(); modules.Items.Clear(); actions.Items.Clear(); loading = false;
                SelectAction(); SetFeedback("Could not read PowerToys: " + ex.Message, true);
            }
        }

        void PopulateActions(string preferredAction)
        {
            loading = true; actions.Items.Clear();
            string module = modules.SelectedItem as string;
            foreach (var item in shortcuts.Where(x => x.Module == module).OrderBy(x => x.Action, StringComparer.OrdinalIgnoreCase))
                actions.Items.Add(new ActionOption(item));
            if (actions.Items.Count > 0) {
                var match = actions.Items.Cast<ActionOption>().FirstOrDefault(x => x.Shortcut.Action == preferredAction);
                actions.SelectedItem = match ?? actions.Items[0];
            }
            loading = false; SelectAction();
        }

        void SelectAction()
        {
            var option = actions.SelectedItem as ActionOption;
            selected = option == null ? null : option.Shortcut;
            detail.Text = selected == null ? "Choose a PowerToys function to see its shortcut." :
                selected.Module + "  /  " + selected.Action + (selected.ModuleEnabled ? "  •  Enabled" : "  •  Module off");
            chord.Text = selected == null || selected.Chord == "Unassigned" ? "" : selected.Chord;
            chord.Enabled = selected != null && selected.CanEdit;
            save.Enabled = selected != null && selected.CanEdit;
            enableModule.Checked = selected != null;
            enableModule.Enabled = selected != null && selected.CanEdit && !selected.ModuleEnabled;
            if (selected != null && !selected.CanEdit)
                SetFeedback("This function is view-only here. Change it in PowerToys Settings.", false);
            else if (selected != null)
                SetFeedback("Edit the shortcut, then use Save in PowerToys. KeyWeave backs up the original settings first.", false);
        }

        void SaveShortcut()
        {
            if (selected == null || !selected.CanEdit) return;
            try {
                string backup = PowerToysIntegration.Save(selected, chord.Text.Trim(), enableModule.Checked);
                RefreshShortcuts(true);
                SetFeedback("Saved in PowerToys. Backup: " + backup + ". Restart PowerToys if the new shortcut is not active yet.", false);
            } catch (Exception ex) { SetFeedback("Could not complete: " + ex.Message, true); }
        }

        sealed class ActionOption
        {
            public readonly PowerToysShortcut Shortcut;
            public ActionOption(PowerToysShortcut shortcut) { Shortcut = shortcut; }
            public override string ToString() { return Shortcut.Action + "  (" + Shortcut.Chord + ")"; }
        }
    }
}
