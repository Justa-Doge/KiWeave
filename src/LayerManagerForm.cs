using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class LayerManagerForm : Form
    {
        readonly ListBox list = new DesignListBox();
        readonly TextBox name = new DesignTextBox();
        readonly ComboBox key = new DesignComboBox();
        readonly Label feedback = UiStyle.Text("", 9, false);
        ModifierLayer[] layers;
        bool loading;
        internal ModifierLayer[] Result { get; private set; }

        internal LayerManagerForm(ModifierLayer[] existing)
        {
            layers = (existing ?? new ModifierLayer[0]).Select(l => l.Copy()).ToArray();
            Text = "KiWeave layers"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            ClientSize = new Size(760, 520); MinimumSize = new Size(680, 520); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, RowCount = 2 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); Controls.Add(root);
            var left = new DesignCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 14, 0) }; root.Controls.Add(left, 0, 0);
            var leftRows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            leftRows.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); leftRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); left.Controls.Add(leftRows);
            list.Dock = DockStyle.Fill; list.BackColor = UiStyle.Surface; list.ForeColor = UiStyle.Ink; list.BorderStyle = BorderStyle.None; list.Font = Font; list.SelectedIndexChanged += delegate { LoadSelected(); }; leftRows.Controls.Add(list, 0, 0);
            var listButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 10, 0, 0) };
            listButtons.Controls.Add(UiStyle.Button("Add", delegate { AddLayer(); }, true)); listButtons.Controls.Add(UiStyle.Button("Remove", delegate { RemoveLayer(); })); leftRows.Controls.Add(listButtons, 0, 1);
            var right = new DesignCard { Dock = DockStyle.Fill }; root.Controls.Add(right, 1, 0);
            var stack = UiStyle.Stack(); right.Controls.Add(stack); stack.Controls.Add(UiStyle.Text("Modifier layer", 19, true));
            var intro = UiStyle.Text("Hold the activation key while pressing F1–F12. The activation key is suppressed while the layer is active.", 9, false); intro.Margin = new Padding(0, 0, 0, 20); stack.Controls.Add(intro);
            stack.Controls.Add(UiStyle.Field("Layer name", name)); name.TextChanged += delegate { SaveDraft(); };
            UiStyle.Combo(key); foreach (string item in LayerKeys.Names) key.Items.Add(new LayerKeyChoice(item)); stack.Controls.Add(UiStyle.Field("Hold key", key)); key.SelectedIndexChanged += delegate { SaveDraft(); };
            var note = UiStyle.Text("Caps Lock, Menu, Scroll Lock, Pause, Insert, right-side modifiers, and F13–F24 are supported. Choose a key you do not need for its normal behavior.", 9, false); note.Margin = new Padding(0, 6, 0, 0); stack.Controls.Add(note);
            feedback.Dock = DockStyle.Fill; feedback.Padding = new Padding(0, 14, 10, 0); root.Controls.Add(feedback, 0, 1);
            var done = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 10, 0, 0) };
            done.Controls.Add(UiStyle.Button("Save layers", delegate { Finish(); }, true)); done.Controls.Add(UiStyle.Button("Cancel", delegate { DialogResult = DialogResult.Cancel; Close(); })); root.Controls.Add(done, 1, 1);
            RefreshList(); if (list.Items.Count > 0) list.SelectedIndex = 0; else SetEditor(false);
        }

        sealed class LayerKeyChoice
        {
            internal readonly string Name; internal LayerKeyChoice(string name) { Name = name; }
            public override string ToString() { return LayerKeys.Label(Name); }
        }
        void RefreshList()
        {
            int selected = list.SelectedIndex; list.Items.Clear(); foreach (var layer in layers) list.Items.Add(layer.Name + "  ·  " + LayerKeys.Label(layer.ActivationKey));
            if (list.Items.Count > 0) list.SelectedIndex = Math.Max(0, Math.Min(selected, list.Items.Count - 1));
        }
        void LoadSelected()
        {
            loading = true; bool valid = list.SelectedIndex >= 0 && list.SelectedIndex < layers.Length; SetEditor(valid);
            if (valid) { var layer = layers[list.SelectedIndex]; name.Text = layer.Name; key.SelectedIndex = Array.IndexOf(LayerKeys.Names, layer.ActivationKey); }
            else { name.Text = ""; key.SelectedIndex = -1; }
            loading = false;
        }
        void SetEditor(bool enabled) { name.Enabled = key.Enabled = enabled; }
        void SaveDraft()
        {
            if (loading || list.SelectedIndex < 0 || list.SelectedIndex >= layers.Length) return;
            layers[list.SelectedIndex].Name = name.Text; var choice = key.SelectedItem as LayerKeyChoice; if (choice != null) layers[list.SelectedIndex].ActivationKey = choice.Name;
            int selected = list.SelectedIndex; RefreshList(); list.SelectedIndex = selected;
        }
        void AddLayer()
        {
            if (layers.Length >= 4) { feedback.Text = "KiWeave supports up to four layers."; return; }
            string activation = LayerKeys.Names.FirstOrDefault(k => !layers.Any(l => l.ActivationKey == k));
            if (activation == null) { feedback.Text = "No unused layer key is available."; return; }
            string baseName = "Layer " + (layers.Length + 1); layers = layers.Concat(new[] { new ModifierLayer { Name = baseName, ActivationKey = activation } }).ToArray(); RefreshList(); list.SelectedIndex = layers.Length - 1; name.Focus(); name.SelectAll();
        }
        void RemoveLayer()
        {
            if (list.SelectedIndex < 0) return; int index = list.SelectedIndex; layers = layers.Where((l, i) => i != index).ToArray(); RefreshList(); if (layers.Length == 0) SetEditor(false);
        }
        void Finish()
        {
            try { var c = new Configuration { Layers = layers.Select(l => l.Copy()).ToArray() }; ConfigStore.Validate(c, false); Result = c.Layers; DialogResult = DialogResult.OK; Close(); }
            catch (Exception ex) { feedback.Text = ex.Message; }
        }
    }
}
