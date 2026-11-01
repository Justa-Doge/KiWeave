using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FunctionRowRemapper
{
    internal sealed class FirstPartyExtension
    {
        internal readonly string Id, Name, Description, Permissions, Maturity;
        internal FirstPartyExtension(string id, string name, string description, string permissions, string maturity)
        { Id = id; Name = name; Description = description; Permissions = permissions; Maturity = maturity; }
    }

    // Shared by both application shells so extension metadata, enablement and action
    // gating cannot drift between the UI and custom-keybinding execution paths.
    internal static class FirstPartyExtensionCatalog
    {
        static readonly FirstPartyExtension[] items = {
            new FirstPartyExtension("discord", "Discord", "Voice controls and connection status.", "Local Discord RPC/IPC; optional authorization", "App-dependent"),
            new FirstPartyExtension("spotify", "Spotify", "Media playback controls for Spotify and other media apps.", "Windows media keys only", "App-dependent"),
            new FirstPartyExtension("obs", "OBS Studio", "Start recording or streaming from a mapped shortcut.", "Local OBS control when configured", "App-dependent"),
            new FirstPartyExtension("powertoys", "PowerToys", "Use supported Keyboard Manager shortcuts and actions.", "Reads the local PowerToys configuration", "App-dependent"),
            new FirstPartyExtension("streamdeck", "Stream Deck", "Use Stream Deck buttons as KiWeave triggers when a first-party plugin is installed.", "Local plugin connection; never downloads automatically", "Experimental"),
            new FirstPartyExtension("controller-midi", "Controller + MIDI", "Use physical controller and MIDI input as shortcut triggers.", "Windows Gaming Input and Windows MIDI Services", "Hardware-dependent")
        };
        static readonly IReadOnlyList<FirstPartyExtension> itemView = Array.AsReadOnly(items);
        static readonly object gate = new object();
        static Dictionary<string, bool> enabled;
        static string loadWarning = "";
        internal static event Action<string, bool> EnabledChanged;
        internal static IReadOnlyList<FirstPartyExtension> Items { get { return itemView; } }
        static string StatePath { get { return Path.Combine(AppStorage.DataFolder, "first-party-extensions.json"); } }
        internal static string LoadWarning { get { lock (gate) { EnsureLoaded(); return loadWarning; } } }
        static void EnsureLoaded()
        {
            if (enabled != null) return;
            var loaded = FirstPartyExtensionStateStore.Load(StatePath, items.Select(x => x.Id));
            enabled = loaded.Values; loadWarning = loaded.Warning;
        }
        internal static bool IsEnabled(string id)
        { lock (gate) { EnsureLoaded(); bool value; return enabled.TryGetValue(id ?? "", out value) && value; } }
        internal static bool ShouldRunHardwareTriggerService(bool extensionEnabled, int configuredTriggerCount)
        { return extensionEnabled && configuredTriggerCount > 0; }
        internal static void SetEnabled(string id, bool value)
        {
            Action<string, bool> changed = null;
            lock (gate) {
                EnsureLoaded(); if (!enabled.ContainsKey(id ?? "")) throw new ArgumentException("Unknown first-party extension.");
                if (enabled[id] == value) return;
                var next = new Dictionary<string, bool>(enabled, StringComparer.OrdinalIgnoreCase); next[id] = value;
                FirstPartyExtensionStateStore.Save(StatePath, next, items.Select(x => x.Id));
                enabled = next; loadWarning = ""; changed = EnabledChanged;
            }
            if (changed != null) foreach (Action<string, bool> listener in changed.GetInvocationList()) try { listener(id, value); } catch (Exception error) { AppLog.Record("Extension state change", error); }
        }
        internal static string ActionExtension(string target)
        {
            if (String.IsNullOrEmpty(target)) return null;
            if (target.StartsWith("Discord", StringComparison.OrdinalIgnoreCase)) return "discord";
            if (target.StartsWith("Spotify", StringComparison.OrdinalIgnoreCase)) return "spotify";
            if (target.StartsWith("Obs", StringComparison.OrdinalIgnoreCase)) return "obs";
            if (String.Equals(target, "OpenPowerToys", StringComparison.OrdinalIgnoreCase)) return "powertoys";
            return null;
        }
        internal static string Status(FirstPartyExtension item)
        {
            if (item == null) return "Unavailable";
            if (item.Id == "discord") return DiscordIntegration.Status;
            if (item.Id == "spotify") return ProcessStatus("Spotify", "Spotify");
            if (item.Id == "obs") return ProcessStatus("OBS Studio", "obs64", "obs32");
            if (item.Id == "powertoys") { try { return PowerToysIntegration.Load().Count + " supported shortcut(s) detected"; } catch { return "PowerToys is unavailable"; } }
            if (item.Id == "streamdeck") return "Ready for the KiWeave first-party plugin";
            if (item.Id == "controller-midi") return "Hardware support is available when Windows exposes the device";
            return "Ready";
        }
        static string ProcessStatus(string label, params string[] processNames)
        {
            bool running = false;
            foreach (string processName in processNames) {
                Process[] processes;
                try { processes = Process.GetProcessesByName(processName); }
                catch { return label + " status unavailable"; }
                try { if (processes.Length > 0) running = true; }
                finally { foreach (Process process in processes) try { process.Dispose(); } catch { } }
            }
            return label + (running ? " is running" : " is not running");
        }
    }
}
