using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class DocumentationForm : Form
    {
        readonly TextBox search = new DesignTextBox();
        readonly RichTextBox text = new RichTextBox();
        readonly string[] topics = {
            "Getting started|Choose a function key or create a global hotkey, test the action, then save it. Build profiles for games or apps as your setup grows. Shortcuts start paused until you enable them.",
            "Function keys|F1-F12 mappings run through the low-level keyboard hook. Pass through leaves the key unchanged; Unbound consumes it without an action.",
            "Custom hotkeys|Global keyboard hotkeys require Ctrl, Alt, Shift, or Win. Dedicated volume and playback buttons can be assigned by themselves. Capture records only the combination being reviewed and never keeps a key history.",
            "Modifier layers|Hold the configured layer key while pressing a function key. Release pairing is preserved and layers remain local to KiWeave.",
            "Profiles|Profiles can match foreground process names, inherit Default or another profile, and be pinned for the current session.",
            "Sequences|Steps run top to bottom. Waits are bounded, and Preview dry run lists the routine without executing or saving it.",
            "Privacy|Core remapping, profiles, history, diagnostics, and recovery work offline. Network access is limited to explicitly enabled release checks, integrations, or triggered HTTP actions.",
            "Recovery|Safe Mode can inspect health, restore a reviewed backup, restore known-good state, and export redacted diagnostics without installing hooks.",
            "Action safety|Imported configuration and action packs are reviewed before staging. Scripts and command actions are rejected from declarative action packs.",
            "Emergency pause|Hold Ctrl+Alt+Shift for 1.5 seconds to disable remapping immediately. Turn it back on from the main window or tray.",
            "Themes and accessibility|Settings includes readable dark palettes and a High contrast theme. Keyboard focus and standard Tab navigation remain available."
        };
        internal DocumentationForm()
        {
            Text = "KiWeave offline guide"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; ClientSize = new Size(820, 620); MinimumSize = new Size(680, 500); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 4 }; root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); Controls.Add(root);
            var header = UiStyle.Stack(); header.Controls.Add(UiStyle.Text("KiWeave offline guide", 22, true)); header.Controls.Add(UiStyle.Text("A local reference for mappings, profiles, safety, privacy, and recovery. No network request is made.", 9, false)); root.Controls.Add(header, 0, 0);
            search.Text = ""; search.Dock = DockStyle.Fill; search.AccessibleName = "Search offline guide"; search.Margin = new Padding(0, 8, 0, 8); root.Controls.Add(search, 0, 1);
            text.Dock = DockStyle.Fill; text.ReadOnly = true; text.BorderStyle = BorderStyle.FixedSingle; text.BackColor = UiStyle.Surface; text.ForeColor = UiStyle.Ink; text.Font = new Font("Segoe UI", 10); text.DetectUrls = false; text.ScrollBars = RichTextBoxScrollBars.Vertical; root.Controls.Add(text, 0, 2);
            var close = UiStyle.Button("Done", delegate { Close(); }, true); close.Anchor = AnchorStyles.Right; root.Controls.Add(close, 0, 3); AcceptButton = close;
            search.TextChanged += delegate { RefreshText(); }; RefreshText();
        }
        void RefreshText()
        {
            string filter = search.Text.Trim(); text.Clear();
            foreach (string item in topics) { string[] parts = item.Split(new[] { '|' }, 2); if (filter.Length > 0 && item.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue; text.SelectionFont = new Font("Segoe UI", 10, FontStyle.Bold); text.AppendText(parts[0] + "\r\n"); text.SelectionFont = new Font("Segoe UI", 10); text.AppendText(parts[1] + "\r\n\r\n"); }
            if (text.TextLength == 0) text.Text = "No matching topics.";
            text.SelectionStart = 0; text.SelectionLength = 0;
        }
    }
}
