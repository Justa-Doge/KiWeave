using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class AboutForm : Form
    {
        public AboutForm()
        {
            Text = "About KiWeave"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Design.DarkTitlebar(this);
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(610, 430); MinimumSize = new Size(600, 470); MaximizeBox = false;
            var card = new DesignCard { Dock = DockStyle.Fill, Margin = new Padding(24) }; Controls.Add(card); var stack = UiStyle.Stack(); card.Controls.Add(stack);
            var mark = new Label { Text = "F", AutoSize = false, Size = new Size(58, 58), TextAlign = ContentAlignment.MiddleCenter, BackColor = UiStyle.AccentFill, ForeColor = Color.White, Font = new Font("Segoe UI", 27, FontStyle.Bold), Margin = new Padding(0, 0, 0, 14) }; stack.Controls.Add(mark);
            stack.Controls.Add(UiStyle.Text("KiWeave", 24, true)); stack.Controls.Add(UiStyle.Text("Version " + UpdateChecker.CurrentVersion, 10, false));
            var credit = UiStyle.Text("Created by Justa-Doge\n\nFunction keys, custom hotkeys, profiles, and Windows integrations woven into one place.", 10, false); credit.MaximumSize = new Size(500, 0); credit.Margin = new Padding(0, 18, 0, 20); stack.Controls.Add(credit);
            stack.Controls.Add(UiStyle.Text("Apache License 2.0\nKiWeave © 2026 Justa-Doge", 9, true));
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 22, 0, 0), WrapContents = true };
            buttons.Controls.Add(UiStyle.Button("GitHub", delegate { Open("https://github.com/Justa-Doge/KeyWeave"); }, true));
            buttons.Controls.Add(UiStyle.Button("View license", delegate { Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LICENSE")); }));
            buttons.Controls.Add(UiStyle.Button("Close", delegate { Close(); })); stack.Controls.Add(buttons);
        }
        void Open(string target)
        {
            try { if (!target.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !File.Exists(target)) throw new FileNotFoundException("The license file is missing."); Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "KiWeave", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }
    }
}
