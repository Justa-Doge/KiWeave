using System;
using System.Drawing;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class ProfileStatusForm : Form
    {
        internal bool TogglePinRequested { get; private set; }

        internal ProfileStatusForm(string profile, string reason, bool pinned, bool automaticEnabled, string[] appRules)
        {
            Text = "KiWeave profile"; Icon = Program.AppIcon(); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            Font = new Font("Segoe UI", 10F); AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(520, 475);
            MinimumSize = new Size(470, 500); StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.Sizable;
            Design.DarkTitlebar(this);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26), RowCount = 3, ColumnCount = 1, BackColor = UiStyle.Canvas };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(root);

            var head = UiStyle.Stack(); head.Controls.Add(UiStyle.Text("Active profile", 10, false)); head.Controls.Add(UiStyle.Text(profile, 24, true)); root.Controls.Add(head, 0, 0);
            var card = new DesignCard { Dock = DockStyle.Fill, Margin = new Padding(0, 18, 0, 18) }; root.Controls.Add(card, 0, 1);
            var details = UiStyle.Stack(); card.Controls.Add(details);
            details.Controls.Add(UiStyle.Text("Why it is active", 11, true));
            var why = UiStyle.Text(reason, 10, false); why.Margin = new Padding(0, 0, 0, 20); details.Controls.Add(why);
            details.Controls.Add(UiStyle.Text("Automatic switching", 11, true));
            var state = pinned ? "Paused while this profile is pinned for this app session." : automaticEnabled ? "Ready to use the first matching foreground-app rule while KiWeave is in the background." : "Turned off in Settings.";
            var stateLabel = UiStyle.Text(state, 10, false); stateLabel.Margin = new Padding(0, 0, 0, 20); details.Controls.Add(stateLabel);
            details.Controls.Add(UiStyle.Text("App rules for this profile", 11, true));
            details.Controls.Add(UiStyle.Text(appRules == null || appRules.Length == 0 ? "None. This profile can still be selected manually." : String.Join(", ", appRules), 10, false));

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty };
            var close = UiStyle.Button("Close", delegate { DialogResult = DialogResult.Cancel; Close(); });
            var toggle = UiStyle.Button(pinned ? "Resume automatic switching" : "Pin for this session", delegate { TogglePinRequested = true; DialogResult = DialogResult.OK; Close(); }, true);
            buttons.Controls.Add(close); buttons.Controls.Add(toggle); root.Controls.Add(buttons, 0, 2);
        }
    }
}
