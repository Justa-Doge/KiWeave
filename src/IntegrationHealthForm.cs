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
        readonly int monitors;
        readonly ConfigurationHealthReport health;
        readonly TableLayoutPanel rows = new TableLayoutPanel();

        internal IntegrationHealthForm(int detectedMonitors, ConfigurationHealthReport report)
        {
            monitors = detectedMonitors; health = report ?? ConfigurationHealthReport.Empty;
            Text = "KiWeave integrations"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            ClientSize = new Size(700, 620); MinimumSize = new Size(620, 500); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var scroll = new DesignScrollPanel { Dock = DockStyle.Fill, Padding = new Padding(28) }; Controls.Add(scroll);
            var root = UiStyle.Stack(); scroll.Controls.Add(root);
            root.Controls.Add(UiStyle.Text("Integrations", 24, true));
            root.Controls.Add(UiStyle.Text("Read-only availability checks. KiWeave does not install, launch, authorize, or modify anything from this page.", 10, false));
            rows.Dock = DockStyle.Top; rows.AutoSize = true; rows.ColumnCount = 2; rows.Margin = new Padding(0, 20, 0, 16); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68)); root.Controls.Add(rows);
            RefreshRows();
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 8, 0, 0) };
            buttons.Controls.Add(UiStyle.Button("Refresh", delegate { RefreshRows(); }, true)); buttons.Controls.Add(UiStyle.Button("Done", delegate { Close(); })); root.Controls.Add(buttons);
        }
        void RefreshRows()
        {
            rows.SuspendLayout(); rows.Controls.Clear();
            Add("PowerToys", PowerToysStatus()); Add("Discord", DiscordIntegration.Status); Add("OBS Studio", ProcessExists("obs64") || ProcessExists("obs32") ? "Running" : "Not running"); Add("Spotify", ProcessExists("Spotify") ? "Running" : "Not running"); Add("DDC/CI monitors", monitors == 0 ? "None detected" : monitors + " detected"); Add("Audio devices", "Windows audio available to KiWeave"); Add("Configuration", health.HasWarnings ? health.Findings.Length + " issue(s) found" : "Healthy");
            rows.ResumeLayout(true);
        }
        void Add(string name, string value)
        {
            var label = UiStyle.Text(name, 10, true); label.Margin = new Padding(0, 6, 12, 6);
            var status = UiStyle.Text(value ?? "Unavailable", 10, false); status.Margin = new Padding(0, 6, 0, 6); status.ForeColor = value == "Healthy" || value == "Connected" || value == "Authorized; waiting for Discord" ? Color.FromArgb(127, 214, 169) : UiStyle.Ink;
            rows.Controls.Add(label); rows.Controls.Add(status);
        }
        static string PowerToysStatus()
        {
            try { int count = PowerToysIntegration.Load().Count; return count == 0 ? "Not detected or no shortcuts" : count + " shortcuts detected"; } catch { return "Unavailable"; }
        }
        static bool ProcessExists(string name) { try { return Process.GetProcessesByName(name).Length > 0; } catch { return false; } }
    }
}
