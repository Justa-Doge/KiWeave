using System;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal static class Program
    {
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint processId);
        internal static Icon AppIcon()
        {
            using (var stream = typeof(Program).Assembly.GetManifestResourceStream("FunctionRowRemapper.AppIcon"))
            using (var icon = new Icon(stream)) return (Icon)icon.Clone();
        }
        internal static Icon TrayIcon()
        {
            using (var bitmap = new Bitmap(32, 32)) using (Graphics g = Graphics.FromImage(bitmap)) using (var font = new Font("Segoe UI", 17, FontStyle.Bold)) {
                g.Clear(UiStyle.AccentFill); Design.ShadowedF(g, font, 5, 1); IntPtr h = bitmap.GetHicon();
                try { return (Icon)Icon.FromHandle(h).Clone(); } finally { DestroyIcon(h); }
            }
        }
        [STAThread]
        static void Main(string[] args)
        {
            if (Array.IndexOf(args, "--exit-for-update") >= 0) { Environment.ExitCode = UpdateExit.Request(); return; }
            bool created; string wakeName = @"Local\FunctionRowRemapper-Show-" + Environment.UserName;
            using (var mutex = new Mutex(true, @"Local\FunctionRowRemapper-" + Environment.UserName, out created)) {
                if (!created) {
                    try {
                        using (var wake = EventWaitHandle.OpenExisting(wakeName)) {
                            using (var current = Process.GetCurrentProcess()) foreach (var p in Process.GetProcessesByName(current.ProcessName)) using (p) if (p.Id != current.Id && p.SessionId == current.SessionId) AllowSetForegroundWindow((uint)p.Id);
                            wake.Set();
                        }
                    } catch (WaitHandleCannotBeOpenedException) { MessageBox.Show("An older copy of KeyWeave is running. Exit it, then open this updated app.", "Older copy running"); }
                    return;
                }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                try {
                    using (var wake = new EventWaitHandle(false, EventResetMode.AutoReset, wakeName))
                    using (var form = new MainForm(Array.IndexOf(args, "--tray") >= 0)) {
                        var handle = form.Handle;
                        using (var updateExit = new UpdateExit(form)) {
                        var listener = ThreadPool.RegisterWaitForSingleObject(wake, delegate(object state, bool timeout) { form.RequestShow(); }, null, Timeout.Infinite, false);
                        try { Application.Run(form); } finally { listener.Unregister(null); }
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("KeyWeave must close. Its hook will be released.\n\n" + ex.Message, "KeyWeave", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }
}
