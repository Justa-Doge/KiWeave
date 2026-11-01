using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace FunctionRowRemapper
{
    internal static class NetworkPolicy
    {
        static readonly object Gate = new object();
        static CancellationTokenSource activeRequests = new CancellationTokenSource();
        static bool enabled;
        internal static bool Enabled
        {
            get { lock (Gate) return enabled; }
            set
            {
                CancellationTokenSource cancel = null;
                lock (Gate) {
                    if (enabled == value) return;
                    enabled = value;
                    if (!value) { cancel = activeRequests; activeRequests = new CancellationTokenSource(); }
                }
                if (cancel != null) try { cancel.Cancel(); } catch { /* Turning networking off must not fail because an in-flight request ended concurrently. */ }
            }
        }
        internal static CancellationToken BeginRequest()
        {
            lock (Gate) {
                if (!enabled) throw new InvalidOperationException("Network access is turned off in KiWeave Settings.");
                return activeRequests.Token;
            }
        }
        internal static void Require() { if (!Enabled) throw new InvalidOperationException("Network access is turned off in KiWeave Settings."); }
    }
    public static class SystemActions
    {
        static bool mediaPlaying;
        internal const string ActivateProfilePrefix = "ActivateProfile:";
        internal static readonly string[] Names = { "CenterWindow", "ToggleAlwaysOnTop", "CycleAudioOutput", "DiscordMute", "DiscordDeafen", "SpotifyPlayPause", "SpotifyNext", "SpotifyPrevious", "ObsStartRecording", "ObsStartStreaming", "OpenPowerToys", "AdminDeviceManager", "AdminServices", "AdminEventViewer", "AdminFirewall", "AdminDiskManagement" };
        internal static bool TryProfile(string name, out string profile)
        {
            profile = "";
            if (name == null || !name.StartsWith(ActivateProfilePrefix, StringComparison.Ordinal)) return false;
            profile = name.Substring(ActivateProfilePrefix.Length);
            return profile.Length >= 1 && profile.Length <= 40 && !profile.Any(Char.IsControl);
        }
        internal static bool IsKnown(string name) { string profile; return Names.Contains(name, StringComparer.Ordinal) || TryProfile(name, out profile); }
        internal static bool ShouldSendDiscordShortcutFallback(bool discordFocused) { return discordFocused; }
        internal static void ExecuteMedia(string target, IActionSink sink)
        {
            ExecuteMedia(target, sink, null);
        }
        internal static void ExecuteMedia(string target, IActionSink sink, Action<bool> mediaToggleForSelfTest)
        {
#if WPF_REWRITE
            if (String.Equals(target, "MediaPlayPause", StringComparison.OrdinalIgnoreCase)) { (mediaToggleForSelfTest ?? WpfMediaSession.Toggle)(false); return; }
#endif
            if (!String.Equals(target, "MediaPlayPause", StringComparison.OrdinalIgnoreCase)) { sink.Send(Shortcuts.Parse(target, true)); return; }
            string iconPath;
            string name = MediaToastName(out iconPath);
            sink.Send(Shortcuts.Parse(target, true));
            if (name.Length > 0) { mediaPlaying = !mediaPlaying; StatusOverlay.ShowStyled(name, mediaPlaying ? "Playing" : "Paused", iconPath.Length == 0 ? "" : iconPath, mediaPlaying); }
        }
        internal static void Execute(string name, IActionSink sink)
        {
            Execute(name, sink, null);
        }
        internal static void Execute(string name, IActionSink sink, Action<bool> mediaToggleForSelfTest)
        {
            string profile;
            if (TryProfile(name, out profile)) {
                var target = sink as IProfileActionSink;
                if (target == null) throw new InvalidOperationException("Profile switching is unavailable in this KiWeave session.");
                target.ActivateProfile(profile); return;
            }
            string extension = FirstPartyExtensionCatalog.ActionExtension(name);
            if (extension != null && !FirstPartyExtensionCatalog.IsEnabled(extension))
                throw new InvalidOperationException("The " + extension + " first-party extension is disabled. Enable it from Extensions before using this mapping.");
            if (name == "CenterWindow") { CenterForegroundWindow(); return; }
            if (name == "ToggleAlwaysOnTop") { bool enabled = ToggleAlwaysOnTop(); StatusOverlay.ShowStyled("KiWeave", enabled ? "Always on top enabled" : "Always on top disabled", "", enabled); return; }
            if (name == "CycleAudioOutput") { AudioDevices.CycleDefaultOutput(); return; }
            if (name == "DiscordMute") { bool muted; bool discordFocused = IsDiscordForeground(); if (DiscordIntegration.TryToggleVoice("mute", out muted)) { if (!discordFocused) ShowDiscordToast(muted ? "Muted" : "Unmuted", "mute", muted); } else if (ShouldSendDiscordShortcutFallback(discordFocused)) sink.Send(Shortcuts.Parse("Ctrl+Shift+M", false)); return; }
            if (name == "DiscordDeafen") { bool deafened; bool discordFocused = IsDiscordForeground(); if (DiscordIntegration.TryToggleVoice("deaf", out deafened)) { if (!discordFocused) ShowDiscordToast(deafened ? "Deafened" : "Undeafened", "deaf", deafened); } else if (ShouldSendDiscordShortcutFallback(discordFocused)) sink.Send(Shortcuts.Parse("Ctrl+Shift+D", false)); return; }
            if (name == "SpotifyPlayPause") {
                ExecuteSpotifyPlayPause(sink, mediaToggleForSelfTest);
                return;
            }
            if (name == "SpotifyNext") { sink.Send(Shortcuts.Parse("MediaNextTrack", true)); return; }
            if (name == "SpotifyPrevious") { sink.Send(Shortcuts.Parse("MediaPreviousTrack", true)); return; }
            if (name.StartsWith("Obs", StringComparison.Ordinal)) { RunObs(name); return; }
            if (name == "OpenPowerToys") { OpenPowerToys(); return; }
            if (name.StartsWith("Admin", StringComparison.Ordinal)) { RunAdminTool(name); return; }
            throw new ArgumentException("Unknown system or integration action.");
        }
        internal static void ExecuteSpotifyPlayPause(IActionSink sink, Action<bool> mediaToggleForSelfTest)
        {
#if WPF_REWRITE
            (mediaToggleForSelfTest ?? WpfMediaSession.Toggle)(true);
#else
            ExecuteMedia("MediaPlayPause", sink);
#endif
        }
        static void CenterForegroundWindow()
        {
            IntPtr window = Native.GetForegroundWindow(); Native.Rect rect; Native.MonitorInfo monitor = new Native.MonitorInfo { Size = Marshal.SizeOf(typeof(Native.MonitorInfo)) };
            if (window == IntPtr.Zero || !Native.GetWindowRect(window, out rect) || !Native.GetMonitorInfo(Native.MonitorFromWindow(window, 2), ref monitor)) throw new InvalidOperationException("The active window could not be measured.");
            int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
            int x = monitor.Work.Left + Math.Max(0, (monitor.Work.Right - monitor.Work.Left - width) / 2);
            int y = monitor.Work.Top + Math.Max(0, (monitor.Work.Bottom - monitor.Work.Top - height) / 2);
            if (!Native.SetWindowPos(window, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010)) throw new InvalidOperationException("Windows would not move the active window.");
        }
        static bool IsDiscordForeground()
        {
            string process = Native.ForegroundProcessName();
            return String.Equals(process, "Discord", StringComparison.OrdinalIgnoreCase)
                || String.Equals(process, "DiscordPTB", StringComparison.OrdinalIgnoreCase)
                || String.Equals(process, "DiscordCanary", StringComparison.OrdinalIgnoreCase);
        }
        static void ShowDiscordToast(string message, string icon, bool active)
        {
            StatusOverlay.ShowStyled("Discord", message, icon, active);
        }
        static string MediaToastName(out string iconPath)
        {
            iconPath = "";
            try
            {
                if (ProcessProbe.AnyRunning("Spotify")) { iconPath = "site:spotify"; return "Spotify"; }
                foreach (string browser in new[] { "chrome", "msedge", "firefox", "brave", "opera" })
                {
                    Process[] candidates;
                    try { candidates = Process.GetProcessesByName(browser); }
                    catch { continue; }
                    try
                    {
                        foreach (var candidate in candidates)
                        {
                            try
                            {
                                string windowTitle = candidate.MainWindowTitle ?? "";
                                string lower = windowTitle.ToLowerInvariant();
                                if (lower.Contains("youtube")) { iconPath = "site:youtube"; return "YouTube"; }
                                if (lower.Contains("spotify")) { iconPath = "site:spotify"; return "Spotify"; }
                            }
                            catch { }
                        }
                    }
                    finally { foreach (var candidate in candidates) try { candidate.Dispose(); } catch { } }
                }
                IntPtr window = Native.GetForegroundWindow();
                string process = Native.ForegroundProcessName();
                iconPath = Native.ForegroundProcessPath();
                string title = Native.WindowTitle(window);
                string haystack = (title + " " + process).ToLowerInvariant();
                if (haystack.Contains("youtube")) { iconPath = "site:youtube"; return "YouTube"; }
                if (haystack.Contains("spotify")) { iconPath = "site:spotify"; return "Spotify"; }
                if (haystack.Contains("music.apple") || haystack.Contains("apple music")) { iconPath = "site:applemusic"; return "Apple Music"; }
                if (haystack.Contains("deezer")) return "Deezer";
                return "";
            }
            catch { return ""; }
        }
        static bool ToggleAlwaysOnTop()
        {
            IntPtr window = Native.GetForegroundWindow(); if (window == IntPtr.Zero) throw new InvalidOperationException("No active window is available.");
            bool top = (Native.GetWindowLongPtr(window, -20).ToInt64() & 0x8L) != 0;
            if (!Native.SetWindowPos(window, top ? new IntPtr(-2) : new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010)) throw new InvalidOperationException("Windows would not change always-on-top for the active window.");
            return !top;
        }
        static string FindObs()
        {
            string[] roots = { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) };
            return roots.Select(r => Path.Combine(r, "obs-studio", "bin", "64bit", "obs64.exe")).FirstOrDefault(File.Exists);
        }
        static void RunObs(string name)
        {
            string exe = FindObs(); if (exe == null) throw new FileNotFoundException("OBS Studio was not found in its standard installation folder.");
            string argument = name == "ObsStartRecording" ? "--startrecording" : "--startstreaming";
            using (Process p = Process.Start(new ProcessStartInfo(exe, argument) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) })) { }
            StatusOverlay.ShowStyled("OBS", name == "ObsStartRecording" ? "Recording started" : "Streaming started", "", true);
        }
        static void OpenPowerToys()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] paths = { Path.Combine(local, "PowerToys", "PowerToys.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerToys", "PowerToys.exe") };
            string exe = paths.FirstOrDefault(File.Exists); if (exe == null) throw new FileNotFoundException("PowerToys was not found.");
            using (Process p = Process.Start(new ProcessStartInfo(exe, "--open-settings") { UseShellExecute = true })) { }
        }
        static void RunAdminTool(string name)
        {
            string snapIn = name == "AdminDeviceManager" ? "devmgmt.msc" : name == "AdminServices" ? "services.msc" : name == "AdminEventViewer" ? "eventvwr.msc" : name == "AdminFirewall" ? "wf.msc" : name == "AdminDiskManagement" ? "diskmgmt.msc" : null;
            if (snapIn == null) throw new ArgumentException("Unknown administrator tool.");
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), snapIn);
            if (!File.Exists(path)) throw new FileNotFoundException("The Windows administrator tool was not found.", path);
            try { using (Process p = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "runas" })) { } }
            catch (System.ComponentModel.Win32Exception ex) { throw new InvalidOperationException("Administrator approval was cancelled or unavailable.", ex); }
        }
        public static void ExecuteAdministratorTool(string name) { RunAdminTool(name); }
        internal static void HttpRequest(Mapping mapping)
        {
            HttpRequest(mapping, null);
        }
        internal static void HttpRequest(Mapping mapping, Func<bool> stillActive)
        {
            using (var actionCancellation = new CancellationTokenSource())
            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(NetworkPolicy.BeginRequest(), actionCancellation.Token))
            using (var activityMonitor = stillActive == null ? null : new Timer(delegate {
                try { if (!stillActive()) actionCancellation.Cancel(); }
                catch { try { actionCancellation.Cancel(); } catch (ObjectDisposedException) { } }
            }, null, 0, 50))
            {
            var token = linkedCancellation.Token;
            var request = (HttpWebRequest)WebRequest.Create(mapping.Target); request.Timeout = 8000; request.ReadWriteTimeout = 8000; request.UserAgent = "KiWeave/1.0";
            using (token.Register(delegate { request.Abort(); })) {
                token.ThrowIfCancellationRequested();
                byte[] body = Encoding.UTF8.GetBytes(SecretVault.Expand(mapping.Arguments ?? "")); request.Method = body.Length == 0 ? "GET" : "POST";
                if (body.Length > 0) { request.ContentType = "application/json; charset=utf-8"; request.ContentLength = body.Length; using (var stream = request.GetRequestStream()) stream.Write(body, 0, body.Length); }
                using (var response = (HttpWebResponse)request.GetResponse()) if ((int)response.StatusCode >= 400) throw new InvalidOperationException("HTTP action returned " + (int)response.StatusCode + ".");
            }
            }
        }
    }

    internal static class AudioDevices
    {
        const int Active = 1;
        internal static string Snapshot()
        {
            var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))); IMMDeviceCollection collection = null;
            try {
                Check(enumerator.EnumAudioEndpoints(0, Active, out collection)); uint count; Check(collection.GetCount(out count)); var ids = new string[count];
                for (uint i = 0; i < count; i++) { IMMDevice device; Check(collection.Item(i, out device)); try { Check(device.GetId(out ids[i])); } finally { Marshal.ReleaseComObject(device); } }
                Array.Sort(ids, StringComparer.OrdinalIgnoreCase); return String.Join("\n", ids);
            } finally { if (collection != null) Marshal.ReleaseComObject(collection); Marshal.ReleaseComObject(enumerator); }
        }
        internal static void CycleDefaultOutput()
        {
            var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))); IMMDeviceCollection collection = null; IMMDevice current = null;
            try {
                Check(enumerator.EnumAudioEndpoints(0, Active, out collection)); uint count; Check(collection.GetCount(out count)); if (count < 2) throw new InvalidOperationException("Only one active audio output is available.");
                Check(enumerator.GetDefaultAudioEndpoint(0, 1, out current)); string currentId; Check(current.GetId(out currentId));
                var ids = new string[count]; for (uint i = 0; i < count; i++) { IMMDevice device; Check(collection.Item(i, out device)); try { Check(device.GetId(out ids[i])); } finally { Marshal.ReleaseComObject(device); } }
                int index = Array.FindIndex(ids, x => String.Equals(x, currentId, StringComparison.OrdinalIgnoreCase)); string next = ids[(index + 1 + ids.Length) % ids.Length];
                var policy = (IPolicyConfigVista)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9"))); try { for (int role = 0; role <= 2; role++) Check(policy.SetDefaultEndpoint(next, role)); } finally { Marshal.ReleaseComObject(policy); }
            } finally { if (current != null) Marshal.ReleaseComObject(current); if (collection != null) Marshal.ReleaseComObject(collection); Marshal.ReleaseComObject(enumerator); }
        }
        static void Check(int result) { if (result != 0) Marshal.ThrowExceptionForHR(result); }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        interface IMMDeviceEnumerator { int EnumAudioEndpoints(int flow, int stateMask, out IMMDeviceCollection devices); int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device); int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device); int RegisterEndpointNotificationCallback(IntPtr client); int UnregisterEndpointNotificationCallback(IntPtr client); }
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0BD7A1BE-7A1A-44DB-8397-C0A9981F1C0C")]
        interface IMMDeviceCollection { int GetCount(out uint count); int Item(uint index, out IMMDevice device); }
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        interface IMMDevice { int Activate(ref Guid iid, int context, IntPtr activation, out IntPtr value); int OpenPropertyStore(int access, out IntPtr properties); int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id); int GetState(out int state); }
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("294935CE-F637-4E7C-A41B-AB255460B862")]
        interface IPolicyConfigVista {
            int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr format); int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int defaultFormat, out IntPtr format); int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
            int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr endpointFormat, IntPtr mixFormat); int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int defaultPeriod, out long period, out long minimumPeriod); int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, ref long period);
            int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr mode); int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode); int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr key, out IntPtr value); int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr key, IntPtr value);
            int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role); int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
        }
    }
}
