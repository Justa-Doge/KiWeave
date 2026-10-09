using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
            "Discord setup|If Discord approval limits the bundled KiWeave app, use the Discord setup guide to prepare your own developer application. KiWeave keeps authorization local and never asks you to share a client secret.",
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
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 8, 0, 0) };
            actions.Controls.Add(UiStyle.Button("Open Discord setup", OpenDiscordSetup, false));
            var close = UiStyle.Button("Done", delegate { Close(); }, true); close.Anchor = AnchorStyles.Right; actions.Controls.Add(close); root.Controls.Add(actions, 0, 3); AcceptButton = close;
            search.TextChanged += delegate { RefreshText(); }; RefreshText();
        }
        void OpenDiscordSetup(object sender, EventArgs e) { OpenDiscordSetupGuide(this); }
        internal static void OpenDiscordSetupGuide(IWin32Window owner)
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KiWeave");
            string path = Path.Combine(directory, "discord-setup.html");
            const string html = "<!doctype html><html><head><meta charset=\"utf-8\"><title>KiWeave Discord setup</title><style>body{font-family:Segoe UI,Arial,sans-serif;max-width:760px;margin:40px auto;padding:0 24px;line-height:1.55;color:#202124}h1{color:#5865f2}code{background:#f1f3f4;padding:2px 5px;border-radius:4px}li{margin:10px 0}.note{padding:12px 16px;background:#f3f5ff;border-left:4px solid #5865f2}</style></head><body><h1>KiWeave Discord setup</h1><p>This offline guide explains the custom Discord application route for users who need their own app identity.</p><ol><li>Open the <a href=\"https://discord.com/developers/applications\">Discord Developer Portal</a> and choose <b>New Application</b>.</li><li>Give the application a name, then open its OAuth2 settings.</li><li>Use KiWeave’s documented loopback callback when configuring OAuth, and keep the client secret private.</li><li>Request only the scopes Discord grants to your application. Voice RPC scopes may require Discord approval.</li><li>Return to KiWeave, connect Discord, and test mute and deafen from a non-Discord foreground window.</li></ol><p class=\"note\"><b>Security:</b> Never paste a client secret, refresh token, or authorization code into chat, logs, backups, or screenshots.</p><p>KiWeave’s bundled app remains the default. This guide is for preparing a separate app identity when Discord requires it.</p></body></html>";
            try { Directory.CreateDirectory(directory); File.WriteAllText(path, html, System.Text.Encoding.UTF8); Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(owner, "Could not open the Discord setup guide: " + ex.Message, "Discord setup", MessageBoxButtons.OK, MessageBoxIcon.Information); }
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
