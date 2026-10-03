using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class DiagnosticsForm : Form
    {
        public DiagnosticsForm(string report, bool allowSave = false)
        {
            Text = "KiWeave diagnostics"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Design.DarkTitlebar(this);
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(780, 560); MinimumSize = new Size(680, 480);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); Controls.Add(root);
            var intro = UiStyle.Stack(); intro.Controls.Add(UiStyle.Text("Diagnostics", 20, true)); intro.Controls.Add(UiStyle.Text("A read-only snapshot for troubleshooting. It does not include your action targets, arguments, or personal file paths.", 9, false)); root.Controls.Add(intro, 0, 0);
            var text = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, WordWrap = false, Text = report, BackColor = UiStyle.Surface, ForeColor = UiStyle.Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9.5f), Margin = new Padding(0, 14, 0, 10) }; root.Controls.Add(text, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
            buttons.Controls.Add(UiStyle.Button("Close", delegate { Close(); }, true));
            if (allowSave) buttons.Controls.Add(UiStyle.Button("Save redacted report", delegate {
                using (var dialog = new SaveFileDialog { Filter = "Text report|*.txt", FileName = "KiWeave-safe-diagnostics.txt", DefaultExt = "txt", AddExtension = true })
                    if (dialog.ShowDialog(this) == DialogResult.OK) try { File.WriteAllText(dialog.FileName, report, new UTF8Encoding(false)); } catch (Exception ex) { MessageBox.Show(this, "The report could not be saved: " + ex.Message, "KiWeave diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }));
            buttons.Controls.Add(UiStyle.Button("Copy report", delegate { try { Clipboard.SetText(report); } catch { } })); root.Controls.Add(buttons, 0, 2);
        }
    }
}
