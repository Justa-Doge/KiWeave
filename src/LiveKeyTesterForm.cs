using System;
using System.Drawing;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class LiveKeyTesterForm : Form
    {
        readonly KeyboardEngine engine;
        readonly Func<string> profileName;
        readonly Label key = Value(), source = Value(), modifiers = Value(), decision = Value(), context = Value(), action = Value();

        public LiveKeyTesterForm(KeyboardEngine keyboardEngine, Func<string> activeProfile) : this(keyboardEngine, activeProfile, false) { }
        internal LiveKeyTesterForm(KeyboardEngine keyboardEngine, Func<string> activeProfile, bool preview)
        {
            if (!preview && keyboardEngine == null) throw new ArgumentNullException("keyboardEngine"); engine = keyboardEngine; profileName = activeProfile ?? (() => "Default");
            Text = "KiWeave live key tester"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Design.DarkTitlebar(this);
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(760, 610); MinimumSize = new Size(680, 560); MaximizeBox = false;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 4 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); Controls.Add(root);
            var intro = UiStyle.Stack(); intro.Controls.Add(UiStyle.Text("Live key tester", 22, true));
            intro.Controls.Add(UiStyle.Text("Press a key to see what KiWeave receives and how it resolves the input.", 10, false)); root.Controls.Add(intro, 0, 0);
            var card = new DesignCard { Dock = DockStyle.Fill, Margin = new Padding(0, 20, 0, 14) }; root.Controls.Add(card, 0, 1);
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, BackColor = Color.Transparent };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddRow(grid, 0, "Latest key event", key); AddRow(grid, 1, "Input source", source); AddRow(grid, 2, "Modifiers", modifiers);
            AddRow(grid, 3, "KiWeave decision", decision); AddRow(grid, 4, "Profile and layer", context); AddRow(grid, 5, "Resolved action", action); card.Controls.Add(grid);
            key.Text = "Waiting for input…"; source.Text = modifiers.Text = decision.Text = context.Text = action.Text = "—";
            var privacy = UiStyle.Text("Private by design: only the latest event exists in memory while this window is open. KiWeave does not save, log, or build a history of pressed keys. Assigned actions still run normally.", 9, false);
            privacy.ForeColor = UiStyle.Muted; privacy.MaximumSize = new Size(680, 0); root.Controls.Add(privacy, 0, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
            buttons.Controls.Add(UiStyle.Button("Close tester", delegate { Close(); }, true)); root.Controls.Add(buttons, 0, 3);
            Shown += delegate { if (engine != null) engine.KeyObserved += Observe; };
            FormClosed += delegate { if (engine != null) engine.KeyObserved -= Observe; ClearValues(); };
        }
        static Label Value() { return new DesignLabel { AutoSize = false, Dock = DockStyle.Fill, ForeColor = UiStyle.Ink, Font = new Font("Segoe UI", 10, FontStyle.Bold), Padding = new Padding(0, 8, 0, 6) }; }
        static void AddRow(TableLayoutPanel grid, int row, string label, Label value)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 16.666f));
            var caption = UiStyle.Text(label, 9, true); caption.Dock = DockStyle.Fill; caption.Padding = new Padding(0, 9, 10, 6); grid.Controls.Add(caption, 0, row); grid.Controls.Add(value, 1, row);
        }
        void Observe(KeyDiagnostic item)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)delegate {
                if (IsDisposed) return;
                item.ProfileName = profileName(); key.Text = item.KeyName + "  (VK 0x" + item.VirtualKey.ToString("X2") + ")";
                source.Text = item.Source; modifiers.Text = item.Modifiers; decision.Text = item.Suppressed ? "Suppressed by KiWeave" : "Passed through";
                context.Text = item.ProfileName + "  •  " + item.LayerName; action.Text = item.ResolvedAction;
            }); } catch (InvalidOperationException) { }
        }
        void ClearValues() { key.Text = source.Text = modifiers.Text = decision.Text = context.Text = action.Text = ""; }
    }
}
