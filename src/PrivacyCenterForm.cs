using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal static class PrivacyData
    {
        internal static string DataFolder { get { return Path.GetDirectoryName(ConfigStore.DefaultPath); } }
        internal static long FileBytes(string path) { try { return File.Exists(path) ? new FileInfo(path).Length : 0; } catch { return 0; } }
        internal static long FolderBytes(string path)
        {
            try { if (!Directory.Exists(path)) return 0; long total = 0; foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) try { total += new FileInfo(file).Length; } catch { } return total; } catch { return 0; }
        }
        internal static string Size(long bytes)
        {
            if (bytes < 1024) return bytes + " B"; if (bytes < 1024 * 1024) return (bytes / 1024d).ToString("0.0") + " KB"; return (bytes / (1024d * 1024d)).ToString("0.0") + " MB";
        }
        internal static string Summary()
        {
            long active = FileBytes(ConfigStore.DefaultPath) + FileBytes(ProfileStore.DefaultPath) + FileBytes(UserPreferences.DefaultPath);
            long backups = FolderBytes(Path.Combine(DataFolder, "Backups"));
            long history = FolderBytes(ConfigurationHistory.DefaultFolder);
            long powerToys = FolderBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KiWeave", "PowerToysBackups"));
            return "Active configuration and preferences: " + Size(active) + "\r\n" +
                "KiWeave backups: " + Size(backups) + "\r\n" +
                "Undo history: " + Size(history) + "\r\n" +
                "PowerToys safety backups: " + Size(powerToys) + "\r\n" +
                "Private-safe logs: " + Size(AppLog.TotalBytes()) + "\r\n" +
                "Cache: none";
        }
    }

    internal sealed class PrivacyCenterForm : Form
    {
        readonly string diagnostics;
        readonly Label storage = UiStyle.Text("", 9, false);

        internal PrivacyCenterForm(bool networkEnabled, string safeDiagnostics)
        {
            diagnostics = safeDiagnostics ?? "";
            Text = "KiWeave privacy center"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            ClientSize = new Size(880, 650); MinimumSize = new Size(760, 570); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var scroll = new DesignScrollPanel { Dock = DockStyle.Fill, Padding = new Padding(28) }; Controls.Add(scroll);
            var root = UiStyle.Stack(); scroll.Controls.Add(root);
            root.Controls.Add(UiStyle.Text("Privacy center", 24, true));
            var intro = UiStyle.Text("KiWeave has no account, analytics, advertising ID, cloud sync, typing history, or automatic uploads. Core remapping works offline.", 10, false); intro.Margin = new Padding(0, 0, 0, 20); root.Controls.Add(intro);

            root.Controls.Add(Card("Network access", networkEnabled ? "Allowed by your master setting" : "Blocked by your master setting",
                networkEnabled ? Color.FromArgb(127, 214, 169) : Color.FromArgb(236, 174, 105),
                "When allowed, only two features can use the network:\r\n• GitHub release checks, when update checks are also enabled\r\n• HTTP actions, only when you physically trigger their saved mapping\r\n\r\nPowerToys, profiles, layers, backups, diagnostics, DDC/CI, and ordinary actions stay local."));

            var data = new DesignCard { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 0, 0, 16), Padding = new Padding(22) };
            var dataStack = UiStyle.Stack(); data.Controls.Add(dataStack); dataStack.Controls.Add(UiStyle.Text("Local data", 15, true));
            var location = UiStyle.Text("Stored under %LOCALAPPDATA%\\KiWeave. Full backups can contain private mappings, paths, commands, URLs, and notes.", 9, false); dataStack.Controls.Add(location);
            storage.Text = PrivacyData.Summary(); storage.Margin = new Padding(0, 6, 0, 16); dataStack.Controls.Add(storage);
            var dataButtons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
            dataButtons.Controls.Add(UiStyle.Button("Open data folder", delegate { OpenFolder(PrivacyData.DataFolder); }));
            dataButtons.Controls.Add(UiStyle.Button("Clear local logs", delegate { ClearLogs(); })); dataStack.Controls.Add(dataButtons); root.Controls.Add(data);

            var diagnostic = new DesignCard { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 0, 0, 16), Padding = new Padding(22) };
            var diagnosticStack = UiStyle.Stack(); diagnostic.Controls.Add(diagnosticStack); diagnosticStack.Controls.Add(UiStyle.Text("Safe diagnostics", 15, true));
            diagnosticStack.Controls.Add(UiStyle.Text("The safe report contains version and feature status counts. It excludes mapping targets, commands, arguments, paths, URLs, request bodies, typed text, key history, and custom profile names.", 9, false));
            var diagnosticButtons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 8, 0, 0) };
            diagnosticButtons.Controls.Add(UiStyle.Button("Preview diagnostics", delegate { using (var form = new DiagnosticsForm(diagnostics)) form.ShowDialog(this); }, true));
            diagnosticButtons.Controls.Add(UiStyle.Button("Preview and export", delegate { using (var form = new DiagnosticsForm(diagnostics, true)) form.ShowDialog(this); })); diagnosticStack.Controls.Add(diagnosticButtons); root.Controls.Add(diagnostic);

            var close = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
            close.Controls.Add(UiStyle.Button("Done", delegate { Close(); }, true)); root.Controls.Add(close);
            scroll.SizeChanged += delegate { UiStyle.Wrap(root); };
            SizeChanged += delegate { if (IsHandleCreated) try { BeginInvoke((Action)delegate { scroll.AutoScrollPosition = Point.Empty; }); } catch { } };
        }

        static DesignCard Card(string title, string status, Color statusColor, string body)
        {
            var card = new DesignCard { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 0, 0, 16), Padding = new Padding(22) };
            var stack = UiStyle.Stack(); card.Controls.Add(stack); stack.Controls.Add(UiStyle.Text(title, 15, true));
            var state = UiStyle.Text(status, 10, true); state.ForeColor = statusColor; stack.Controls.Add(state); stack.Controls.Add(UiStyle.Text(body, 9, false)); return card;
        }
        void ClearLogs()
        {
            if (MessageBox.Show(this, "Delete KiWeave's current and rotated local diagnostic logs? Active mappings, profiles, backups, and preferences will not be changed.", "Clear local logs", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try { AppLog.Clear(); storage.Text = PrivacyData.Summary(); MessageBox.Show(this, "Local diagnostic logs were removed.", "Privacy center", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            catch (Exception ex) { MessageBox.Show(this, "Logs could not be cleared: " + ex.Message, "Privacy center", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        void OpenFolder(string folder)
        {
            try { Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, "The folder could not be opened: " + ex.Message, "Privacy center", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
