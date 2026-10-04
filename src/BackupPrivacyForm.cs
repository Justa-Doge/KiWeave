using System;
using System.Drawing;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class BackupPrivacyForm : Form
    {
        internal BackupPrivacyPreset Preset { get; private set; }
        readonly ComboBox choice = new DesignComboBox();
        internal BackupPrivacyForm()
        {
            Text = "Backup privacy preset"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; ClientSize = new Size(560, 280); MinimumSize = new Size(500, 240); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var root = UiStyle.Stack(); root.Padding = new Padding(24); Controls.Add(root); root.Controls.Add(UiStyle.Text("Choose a privacy review", 20, true)); root.Controls.Add(UiStyle.Text("This controls how the review is presented before export. It never hides that a full backup may contain private targets, paths, URLs, arguments, and profile data.", 9, false)); choice.Items.AddRange(new object[] { "Standard · category summary", "Strict · explicit category warning", "Metadata only · no values shown" }); choice.SelectedIndex = 0; root.Controls.Add(UiStyle.Field("Review preset", choice)); var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 14, 0, 0) }; buttons.Controls.Add(UiStyle.Button("Continue", delegate { Preset = (BackupPrivacyPreset)choice.SelectedIndex; DialogResult = DialogResult.OK; Close(); }, true)); buttons.Controls.Add(UiStyle.Button("Cancel", delegate { Close(); })); root.Controls.Add(buttons);
        }
    }
}
