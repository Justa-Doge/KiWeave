using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class ReleaseReadinessForm : Form
    {
        internal ReleaseReadinessForm(Configuration configuration, ProfileCollection profiles)
        {
            Text = "KiWeave release readiness"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; ClientSize = new Size(720, 540); MinimumSize = new Size(620, 440); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var root = UiStyle.Stack(); root.Padding = new Padding(24); Controls.Add(root); root.Controls.Add(UiStyle.Text("Release readiness", 22, true)); root.Controls.Add(UiStyle.Text("Local pre-release checks only. This does not sign packages, publish releases, or replace the excluded integrity checker.", 9, false));
            var rows = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 18, 0, 12) }; rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.Controls.Add(rows);
            foreach (var check in ReleaseReadiness.Run(configuration, profiles)) { int row = rows.RowCount++; rows.RowStyles.Add(new RowStyle(SizeType.AutoSize)); var name = UiStyle.Text(check.Name, 10, true); var state = UiStyle.Text(check.Status, 9, true); state.ForeColor = check.Passed ? Color.FromArgb(127, 214, 169) : Color.FromArgb(255, 191, 112); var detail = UiStyle.Text(check.Detail, 9, false); name.Margin = state.Margin = detail.Margin = new Padding(0, 7, 12, 7); rows.Controls.Add(name, 0, row); rows.Controls.Add(state, 1, row); rows.Controls.Add(detail, 2, row); }
            var close = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft }; close.Controls.Add(UiStyle.Button("Done", delegate { Close(); }, true)); root.Controls.Add(close);
        }
    }
}
