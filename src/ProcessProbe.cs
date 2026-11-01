using System;
using System.Diagnostics;

namespace FunctionRowRemapper
{
    /// <summary>Small read-only process probes that always release Process handles.</summary>
    internal static class ProcessProbe
    {
        internal static bool AnyRunning(params string[] names)
        {
            foreach (string name in names ?? Array.Empty<string>())
            {
                if (String.IsNullOrWhiteSpace(name)) continue;
                Process[] processes = null;
                try
                {
                    processes = Process.GetProcessesByName(name);
                    if (processes.Length > 0) return true;
                }
                catch { }
                finally
                {
                    if (processes != null) foreach (Process process in processes) try { process.Dispose(); } catch { }
                }
            }
            return false;
        }

        internal static string CurrentExecutablePath()
        {
            try
            {
                using (Process current = Process.GetCurrentProcess())
                {
                    ProcessModule module = null;
                    try { module = current.MainModule; return module == null ? "" : (module.FileName ?? ""); }
                    finally { if (module != null) module.Dispose(); }
                }
            }
            catch { return ""; }
        }
    }
}
