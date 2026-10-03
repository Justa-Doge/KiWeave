using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal static class FirstRun
    {
        internal static string SeenPath { get { return Path.Combine(Path.GetDirectoryName(ConfigStore.DefaultPath), "welcome.seen"); } }
        internal static void MarkSeen() { Directory.CreateDirectory(Path.GetDirectoryName(SeenPath)); File.WriteAllText(SeenPath, UpdateChecker.CurrentVersion); }
    }

    internal sealed class WelcomeForm : Form
    {
        public WelcomeForm()
        {
            Text = "Welcome to KiWeave"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Design.DarkTitlebar(this);
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(720, 560); MinimumSize = new Size(720, 600); MaximizeBox = false;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), RowCount = 4 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); Controls.Add(root);
            var intro = UiStyle.Stack(); intro.Controls.Add(UiStyle.Text("Welcome to KiWeave", 24, true)); intro.Controls.Add(UiStyle.Text("Your keys can do considerably more now.", 10, false)); root.Controls.Add(intro, 0, 0);
            var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0, 22, 0, 18) };
            cards.RowStyles.Add(new RowStyle(SizeType.Percent, 33)); cards.RowStyles.Add(new RowStyle(SizeType.Percent, 34)); cards.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
            cards.Controls.Add(Item("1", "Choose an action", "Pick a function key or create a global hotkey. Browse the library, build a sequence, or connect an integration."), 0, 0);
            cards.Controls.Add(Item("2", "Test, then save", "Test action always asks first. Save only when the mapping looks right; KiWeave starts with shortcuts paused."), 0, 1);
            cards.Controls.Add(Item("3", "Grow into profiles", "Create layouts for games or apps, switch them from the tray, and make a private .keyweave backup whenever you want."), 0, 2); root.Controls.Add(cards, 0, 1);
            var safety = UiStyle.Text("Emergency pause: hold Ctrl + Alt + Shift for 1.5 seconds.", 9, true); safety.ForeColor = UiStyle.Blue; root.Controls.Add(safety, 0, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
            buttons.Controls.Add(UiStyle.Button("Start weaving", delegate { DialogResult = DialogResult.OK; Close(); }, true)); root.Controls.Add(buttons, 0, 3);
        }
        Control Item(string number, string title, string description)
        {
            var card = new DesignCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10), Padding = new Padding(18, 14, 18, 12) };
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 }; row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); card.Controls.Add(row);
            var badge = new Label { Text = number, AutoSize = false, Size = new Size(34, 34), TextAlign = ContentAlignment.MiddleCenter, BackColor = UiStyle.AccentFill, ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold), Margin = new Padding(0, 4, 10, 0) }; row.Controls.Add(badge, 0, 0);
            var text = UiStyle.Stack(); text.Controls.Add(UiStyle.Text(title, 12, true)); var body = UiStyle.Text(description, 9, false); body.MaximumSize = new Size(560, 0); text.Controls.Add(body); row.Controls.Add(text, 1, 0); return card;
        }
    }
}
