using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class UninstallKiWeave
{
    const string InstallFolder = "KiWeave";
    static string InstallDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", InstallFolder); } }
    static string ProgramsDir { get { return Environment.GetFolderPath(Environment.SpecialFolder.Programs); } }
    static void RemoveStartup()
    {
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) if (key != null) { key.DeleteValue("KiWeave", false); key.DeleteValue("FunctionRowRemapper", false); }
    }
    static void DeleteShortcuts()
    {
        foreach (string name in new[] { "KiWeave.lnk", "KiWeave Safe Mode.lnk", "Function Row Remapper.lnk" }) try { File.Delete(Path.Combine(ProgramsDir, name)); } catch { }
    }
    static void ScheduleSelfDelete()
    {
        string self = Process.GetCurrentProcess().MainModule.FileName;
        string command = "/c ping 127.0.0.1 -n 2 > nul & del /f /q \"" + self + "\" & rmdir /s /q \"" + InstallDir + "\"";
        Process.Start(new ProcessStartInfo { FileName = Environment.GetEnvironmentVariable("ComSpec"), Arguments = command, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, UseShellExecute = false });
    }
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if (Process.GetProcessesByName("KiWeave").Length > 0) { MessageBox.Show("Close KiWeave before uninstalling it. Your settings will remain safe.", "KiWeave uninstall", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (MessageBox.Show("Remove the KiWeave app, shortcuts, and startup entry?\n\nYour mappings, profiles, backups, and preferences will not be deleted.", "Uninstall KiWeave", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { RemoveStartup(); DeleteShortcuts(); ScheduleSelfDelete(); MessageBox.Show("KiWeave is being removed. Your saved data was preserved.", "KiWeave uninstall", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        catch (Exception ex) { MessageBox.Show("KiWeave could not be fully removed: " + ex.Message, "KiWeave uninstall", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
