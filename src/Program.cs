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
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int virtualKey);
        internal static bool SafeModeRequested(string[] args, bool shiftHeld) { string[] values = args ?? new string[0]; if (Array.IndexOf(values, "--normal-mode") >= 0) return false; return shiftHeld || Array.IndexOf(values, "--safe-mode") >= 0; }
        internal static Icon AppIcon()
        {
            using (var stream = typeof(Program).Assembly.GetManifestResourceStream("KiWeave.AppIcon"))
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
            try { AppStorage.MigrateLegacy(); } catch (Exception ex) { AppLog.Record("Legacy data migration", ex); }
            bool safeMode = SafeModeRequested(args, (GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0), restartNormally = false;
            bool created; string wakeName = @"Local\KiWeave-Show-" + Environment.UserName;
            using (var mutex = new Mutex(true, @"Local\KiWeave-" + Environment.UserName, out created)) {
                if (!created) {
                    if (safeMode) { MessageBox.Show("Exit the running KiWeave session before starting Safe Mode. Safe Mode never runs beside active hooks or hotkeys.", "KiWeave Safe Mode", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                    try {
                        using (var wake = EventWaitHandle.OpenExisting(wakeName)) {
                            using (var current = Process.GetCurrentProcess()) foreach (var p in Process.GetProcessesByName(current.ProcessName)) using (p) if (p.Id != current.Id && p.SessionId == current.SessionId) AllowSetForegroundWindow((uint)p.Id);
                            wake.Set();
                        }
                    } catch (WaitHandleCannotBeOpenedException) { MessageBox.Show("An older copy of KiWeave is running. Exit it, then open this updated app.", "Older copy running"); }
                    return;
                }
                Design.EnableDarkAppMode(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                try {
                    if (safeMode) using (var form = new SafeModeForm()) { Application.Run(form); restartNormally = form.RestartNormallyRequested; }
                    else using (var wake = new EventWaitHandle(false, EventResetMode.AutoReset, wakeName))
                        using (var form = new MainForm(Array.IndexOf(args, "--tray") >= 0)) {
                            var handle = form.Handle;
                            var listener = ThreadPool.RegisterWaitForSingleObject(wake, delegate(object state, bool timeout) { form.RequestShow(); }, null, Timeout.Infinite, false);
                            try { Application.Run(form); } finally { listener.Unregister(null); }
                        }
                }
                catch (Exception ex) { AppLog.Record("Fatal application error", ex); MessageBox.Show(safeMode ? "KiWeave Safe Mode must close. No hooks or hotkeys were loaded.\n\n" + ex.Message : "KiWeave must close. Its hook will be released.\n\n" + ex.Message, safeMode ? "KiWeave Safe Mode" : "KiWeave", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
            if (restartNormally) try { Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--normal-mode") { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show("KiWeave could not restart normally.\n\n" + ex.Message, "KiWeave Safe Mode", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
