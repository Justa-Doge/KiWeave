using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class ShortcutSuspensionForm : Form
    {
        readonly TextBox apps = new DesignTextBox { Multiline = true, Height = 150, ScrollBars = ScrollBars.Vertical };
        internal bool Saved { get; private set; }
        internal ShortcutSuspensionForm()
        {
            Text = "Suspend shortcuts by app"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; ClientSize = new Size(560, 360); MinimumSize = new Size(500, 320); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var root = UiStyle.Stack(); root.Padding = new Padding(24); Controls.Add(root); root.Controls.Add(UiStyle.Text("Temporary shortcut suspension", 20, true)); root.Controls.Add(UiStyle.Text("One process name per line. When one is focused, KiWeave leaves function keys and layer keys alone. Nothing is closed or changed in that app.", 9, false)); apps.Text = String.Join(Environment.NewLine, ShortcutSuspension.Load()); root.Controls.Add(UiStyle.Field("Process names", apps)); var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 12, 0, 0) }; buttons.Controls.Add(UiStyle.Button("Save", delegate { try { ShortcutSuspension.Save(apps.Lines); Saved = true; DialogResult = DialogResult.OK; Close(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Suspension", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }, true)); buttons.Controls.Add(UiStyle.Button("Cancel", delegate { Close(); })); root.Controls.Add(buttons);
        }
    }
}
