using System;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
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
        internal static bool IsElevated()
        {
            try { using (var identity = WindowsIdentity.GetCurrent()) { var principal = new WindowsPrincipal(identity); return principal.IsInRole(WindowsBuiltInRole.Administrator); } }
            catch { return false; }
        }
        internal static bool RequestElevatedNetworkChange(bool enabled)
        {
            try { using (var p = Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--elevated-network " + (enabled ? "on" : "off")) { UseShellExecute = true, Verb = "runas" })) { p.WaitForExit(); return p.ExitCode == 0; } }
            catch { return false; }
        }
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
            if (args != null && Array.IndexOf(args, "--elevated-network") >= 0) {
                int index = Array.IndexOf(args, "--elevated-network"); if (index + 1 >= args.Length) return;
                string mode = args[index + 1];
                if (!IsElevated() || (!String.Equals(mode, "on", StringComparison.OrdinalIgnoreCase) && !String.Equals(mode, "off", StringComparison.OrdinalIgnoreCase))) { Environment.ExitCode = 2; return; }
                try { var preferences = UserPreferences.Load(UserPreferences.DefaultPath); preferences.NetworkAccess = String.Equals(mode, "on", StringComparison.OrdinalIgnoreCase); UserPreferences.Save(UserPreferences.DefaultPath, preferences); return; } catch { Environment.ExitCode = 1; return; }
            }
            try { AppStorage.MigrateLegacy(); } catch (Exception ex) { AppLog.Record("Legacy data migration", ex); }
            bool safeMode = SafeModeRequested(args, (GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0), restartNormally = false;
            bool created; string wakeName = @"Local\KiWeave-Show-" + Environment.UserName;
            using (var mutex = new Mutex(true, @"Local\KiWeave-" + Environment.UserName, out created)) {
                if (!created) {
                    if (safeMode) { MessageBox.Show("Exit the running KiWeave session before starting Safe Mode. Safe Mode never runs beside active hooks or hotkeys.", "KiWeave Safe Mode", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                    try {
                        int controlIndex = Array.IndexOf(args, "--control"); if (controlIndex >= 0 && controlIndex + 1 < args.Length) AppStorage.QueueControlCommand(args[controlIndex + 1]);
                        using (var wake = EventWaitHandle.OpenExisting(wakeName)) {
                            using (var current = Process.GetCurrentProcess()) foreach (var p in Process.GetProcessesByName(current.ProcessName)) using (p) if (p.Id != current.Id && p.SessionId == current.SessionId) AllowSetForegroundWindow((uint)p.Id);
                            wake.Set();
                        }
                    } catch (WaitHandleCannotBeOpenedException) { MessageBox.Show("An older copy of KiWeave is running. Exit it, then open this updated app.", "Older copy running"); }
                    return;
                }
                Design.EnableDarkAppMode(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                bool cleanExit = false;
                try {
                    if (!safeMode && StartupGuard.RecordStart()) {
                        DialogResult recoveryChoice = MessageBox.Show("KiWeave did not close cleanly twice in a row. Open Safe Mode to review recovery tools before loading keyboard hooks?", "KiWeave recovery", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (recoveryChoice == DialogResult.Yes) safeMode = true;
                        else StartupGuard.MarkClean();
                    }
                    if (safeMode) using (var form = new SafeModeForm()) { Application.Run(form); restartNormally = form.RestartNormallyRequested; }
                    else using (var wake = new EventWaitHandle(false, EventResetMode.AutoReset, wakeName))
                        using (var form = new MainForm(Array.IndexOf(args, "--tray") >= 0)) {
                            var handle = form.Handle;
                            var listener = ThreadPool.RegisterWaitForSingleObject(wake, delegate(object state, bool timeout) { string command = AppStorage.TakeControlCommand(); if (String.IsNullOrEmpty(command)) form.RequestShow(); else form.RequestAutomationCommand(command); }, null, Timeout.Infinite, false);
                            try { Application.Run(form); } finally { listener.Unregister(null); }
                        }
                    cleanExit = true;
                }
                catch (Exception ex) { AppLog.Record("Fatal application error", ex); MessageBox.Show(safeMode ? "KiWeave Safe Mode must close. No hooks or hotkeys were loaded.\n\n" + ex.Message : "KiWeave must close. Its hook will be released.\n\n" + ex.Message, safeMode ? "KiWeave Safe Mode" : "KiWeave", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally { if (cleanExit) StartupGuard.MarkClean(); }
            }
            if (restartNormally) try { Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--normal-mode") { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show("KiWeave could not restart normally.\n\n" + ex.Message, "KiWeave Safe Mode", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
