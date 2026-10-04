using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class IntegrationHealthForm : Form
    {
        readonly DdcMonitor[] monitors;
        readonly ConfigurationHealthReport health;
        readonly Configuration configuration;
        readonly TableLayoutPanel rows = new TableLayoutPanel();

        internal IntegrationHealthForm(int detectedMonitors, ConfigurationHealthReport report) : this(Enumerable.Range(0, Math.Max(0, detectedMonitors)).Select(i => new DdcMonitor { Id = "preview-" + i, Name = "Preview monitor", Codes = new byte[0] }).ToArray(), report) { }
        internal IntegrationHealthForm(DdcMonitor[] detectedMonitors, ConfigurationHealthReport report) : this(detectedMonitors, report, new Configuration()) { }
        internal IntegrationHealthForm(DdcMonitor[] detectedMonitors, ConfigurationHealthReport report, Configuration sourceConfiguration)
        {
            monitors = detectedMonitors ?? new DdcMonitor[0]; health = report ?? ConfigurationHealthReport.Empty; configuration = sourceConfiguration == null ? new Configuration() : sourceConfiguration.Copy();
            Text = "KiWeave integrations"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            ClientSize = new Size(700, 620); MinimumSize = new Size(620, 500); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var scroll = new DesignScrollPanel { Dock = DockStyle.Fill, Padding = new Padding(28) }; scroll.EnableKeyboardFocus(); Controls.Add(scroll);
            var root = UiStyle.Stack(); scroll.Controls.Add(root);
            root.Controls.Add(UiStyle.Text("Integrations", 24, true));
            root.Controls.Add(UiStyle.Text("Read-only availability checks. KiWeave does not install, launch, authorize, or modify integrations from this page; it only records a local observation for drift warnings.", 10, false));
            rows.Dock = DockStyle.Top; rows.AutoSize = true; rows.ColumnCount = 3; rows.Margin = new Padding(0, 20, 0, 16);
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.Controls.Add(rows);
            RefreshRows();
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 8, 0, 0) };
            buttons.Controls.Add(UiStyle.Button("Refresh", delegate { RefreshRows(); }, true)); buttons.Controls.Add(UiStyle.Button("Done", delegate { Close(); })); root.Controls.Add(buttons);
            Shown += delegate { try { BeginInvoke((Action)delegate { ActiveControl = scroll; scroll.Focus(); scroll.AutoScrollPosition = Point.Empty; }); } catch { } };
        }
        void RefreshRows()
        {
            rows.SuspendLayout(); rows.Controls.Clear(); rows.RowStyles.Clear(); rows.RowCount = 0;
            AddHeader("Integration", "Status", "What KiWeave checks");
            string audio = "Unavailable"; try { string snapshot = AudioDevices.Snapshot(); int count = String.IsNullOrEmpty(snapshot) ? 0 : snapshot.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length; audio = (count == 0 ? "No active playback devices" : count + " active playback device" + (count == 1 ? "" : "s")) + " · " + AudioDrift.Observe(snapshot); } catch { }
            Add("PowerToys", PowerToysStatus(), "Reads supported local Keyboard Manager shortcuts and compares them with the last local observation; it never edits PowerToys here."); Add("Discord", DiscordIntegration.Status, "Uses local IPC/RPC and only reconnects when network access and authorization allow it."); Add("OBS Studio", ProcessExists("obs64") || ProcessExists("obs32") ? "Running" : "Not running", "OBS actions require OBS to be installed and running when triggered."); Add("Spotify", ProcessExists("Spotify") ? "Running" : "Not running", "Spotify mappings use Windows media keys and do not read account data."); Add("DDC/CI monitors", monitors.Length == 0 ? "None detected" : monitors.Length + " detected · " + MonitorDrift.Observe(monitors), "Monitor capabilities are probed read-only and compared with the last local observation; KiWeave does not change hardware here."); Add("Audio devices", audio, "Reads active playback-device identities only; it never records audio or changes the default device here."); Add("Configuration", health.HasWarnings ? health.Findings.Length + " issue(s) found" : "Healthy", "Local validation only. No targets, commands, or URLs are executed by this scan.");
            Add("Keyboard layout", KeyboardLayoutDrift.Snapshot() + " · " + KeyboardLayoutDrift.Observe(), "Reads the active Windows keyboard layout identifier and warns when it differs from the last local observation.");
            Add("Session context", SessionAwareness.CurrentDescription(), "Read-only context used to explain automatic profile switching; KiWeave does not change remote-session or virtual-machine settings.");
            Add("Display topology", MonitorTopology.Describe(), "Reads the current Windows display layout so monitor-specific actions can be reviewed against connected screens.");
            Add("Monitor mappings", MonitorMappingScope.Describe(configuration), "Lists only redacted monitor identifiers referenced by saved mappings; it never writes hardware settings from this page.");
            rows.ResumeLayout(true);
        }
        void AddHeader(string name, string status, string explanation)
        {
            int row = rows.RowCount++; rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var first = UiStyle.Text(name.ToUpperInvariant(), 8, true); first.ForeColor = UiStyle.Blue;
            var second = UiStyle.Text(status.ToUpperInvariant(), 8, true); second.ForeColor = UiStyle.Blue;
            var third = UiStyle.Text(explanation.ToUpperInvariant(), 8, true); third.ForeColor = UiStyle.Blue;
            first.Margin = second.Margin = third.Margin = new Padding(0, 0, 12, 10);
            rows.Controls.Add(first, 0, row); rows.Controls.Add(second, 1, row); rows.Controls.Add(third, 2, row);
        }
        void Add(string name, string value, string explanation)
        {
            int row = rows.RowCount++; rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label = UiStyle.Text(name, 10, true); label.Margin = new Padding(0, 8, 12, 10); label.MaximumSize = new Size(125, 0);
            var status = UiStyle.Text(value ?? "Unavailable", 9.5f, false); status.Margin = new Padding(0, 8, 12, 10); status.MaximumSize = new Size(173, 0); status.ForeColor = value == "Healthy" || value == "Connected" || value == "Authorized; waiting for Discord" ? Color.FromArgb(127, 214, 169) : UiStyle.Ink;
            var detail = UiStyle.Text(explanation, 8.5f, false); detail.Margin = new Padding(0, 8, 0, 10); detail.MaximumSize = new Size(300, 0);
            rows.Controls.Add(label, 0, row); rows.Controls.Add(status, 1, row); rows.Controls.Add(detail, 2, row);
        }
        static string PowerToysStatus()
        {
            try { var shortcuts = PowerToysIntegration.Load(); string drift = PowerToysDrift.Observe(shortcuts); return (shortcuts.Count == 0 ? "Not detected or no shortcuts" : shortcuts.Count + " shortcuts detected") + " · " + drift; } catch { return "Unavailable"; }
        }
        static bool ProcessExists(string name) { try { return Process.GetProcessesByName(name).Length > 0; } catch { return false; } }
    }
}
