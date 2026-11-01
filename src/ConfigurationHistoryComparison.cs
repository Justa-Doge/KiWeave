using System;
using System.Collections.Generic;
using System.Linq;

namespace FunctionRowRemapper
{
    internal static class ConfigurationHistoryComparison
    {
        internal static string Compare(KeyWeaveBackup older, Configuration current, ProfileCollection profiles, UserPreferences preferences, bool startup)
        {
            if (older == null) return "Select a history entry.";
            var lines = new List<string>(); int changed = 0;
            for (int i = 0; i < 12; i++) if (!Same(older.Configuration.Mappings[i], current.Mappings[i])) { changed++; lines.Add("F" + (i + 1) + ": " + Safe(older.Configuration.Mappings[i]) + " → " + Safe(current.Mappings[i])); }
            if (older.Configuration.CustomHotkeys.Length != current.CustomHotkeys.Length) lines.Add("Custom hotkeys: " + older.Configuration.CustomHotkeys.Length + " → " + current.CustomHotkeys.Length);
            else if (!SameHotkeys(older.Configuration.CustomHotkeys, current.CustomHotkeys)) lines.Add("Custom hotkeys: shortcuts or actions changed.");
            if (older.Configuration.Layers.Length != current.Layers.Length) lines.Add("Modifier layers: " + older.Configuration.Layers.Length + " → " + current.Layers.Length);
            else if (!SameLayers(older.Configuration.Layers, current.Layers)) lines.Add("Modifier layers: activation keys or mappings changed.");
            if (older.Profiles.Profiles.Length != profiles.Profiles.Length) lines.Add("Profiles: " + older.Profiles.Profiles.Length + " → " + profiles.Profiles.Length);
            else if (!String.Equals(ProfileStore.Serialize(older.Profiles), ProfileStore.Serialize(profiles), StringComparison.Ordinal)) lines.Add("Profiles: rules or mappings changed.");
            if (older.Configuration.Enabled != current.Enabled) lines.Add("Shortcuts enabled: " + Yes(older.Configuration.Enabled) + " → " + Yes(current.Enabled));
            if (older.Preferences.UseTray != preferences.UseTray) lines.Add("Tray mode: " + Yes(older.Preferences.UseTray) + " → " + Yes(preferences.UseTray));
            if (older.Preferences.CheckUpdates != preferences.CheckUpdates) lines.Add("Update checks: " + Yes(older.Preferences.CheckUpdates) + " → " + Yes(preferences.CheckUpdates));
            if (older.Preferences.AutomaticProfiles != preferences.AutomaticProfiles) lines.Add("Automatic profiles: " + Yes(older.Preferences.AutomaticProfiles) + " → " + Yes(preferences.AutomaticProfiles));
            if (older.Preferences.NetworkAccess != preferences.NetworkAccess) lines.Add("Network access: " + Yes(older.Preferences.NetworkAccess) + " → " + Yes(preferences.NetworkAccess));
            if (!String.Equals(older.Preferences.Theme, preferences.Theme, StringComparison.Ordinal) || !String.Equals(older.Preferences.CustomAccent, preferences.CustomAccent, StringComparison.Ordinal)) lines.Add("Theme or accent: changed.");
            if (older.Preferences.HistoryRetention != preferences.HistoryRetention) lines.Add("History retention: " + older.Preferences.HistoryRetention + " → " + preferences.HistoryRetention + " snapshots");
            if (older.Preferences.SmoothScrolling != preferences.SmoothScrolling) lines.Add("Smooth scrolling: " + Yes(older.Preferences.SmoothScrolling) + " → " + Yes(preferences.SmoothScrolling));
            if (!String.Equals(older.Preferences.ToastStyle, preferences.ToastStyle, StringComparison.Ordinal)) lines.Add("Toast style: changed.");
            if (older.StartWithWindows != startup) lines.Add("Start with Windows: " + Yes(older.StartWithWindows) + " → " + Yes(startup));
            if (lines.Count == 0) return "No visible differences from the current saved setup.";
            if (changed > 8) { lines = lines.Take(8).ToList(); lines.Add("…and " + (changed - 8) + " more function-key changes."); }
            return String.Join("\r\n", lines);
        }

        static bool Same(Mapping a, Mapping b) { return a.Kind == b.Kind && a.Target == b.Target && a.Arguments == b.Arguments && a.WorkingDirectory == b.WorkingDirectory && a.MonitorId == b.MonitorId && a.MonitorControl == b.MonitorControl && a.MonitorStep == b.MonitorStep; }
        static bool SameHotkeys(CustomHotkey[] left, CustomHotkey[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] == null || right[i] == null || !String.Equals(left[i].Shortcut, right[i].Shortcut, StringComparison.OrdinalIgnoreCase) || left[i].Action == null || right[i].Action == null || !Same(left[i].Action, right[i].Action)) return false;
            return true;
        }
        static bool SameLayers(ModifierLayer[] left, ModifierLayer[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) {
                if (left[i] == null || right[i] == null || left[i].Name != right[i].Name || left[i].ActivationKey != right[i].ActivationKey || left[i].Mappings.Length != right[i].Mappings.Length) return false;
                for (int key = 0; key < left[i].Mappings.Length; key++) if (left[i].Mappings[key] == null || right[i].Mappings[key] == null || !Same(left[i].Mappings[key], right[i].Mappings[key])) return false;
            }
            return true;
        }
        static string Safe(Mapping m)
        {
            if (m.Kind == ActionKind.Media && Shortcuts.MediaLabels.ContainsKey(m.Target)) return Shortcuts.MediaLabels[m.Target];
            if (m.Kind == ActionKind.SystemAction && Mapping.SystemActionLabels.ContainsKey(m.Target)) return Mapping.SystemActionLabels[m.Target];
            return Mapping.Labels[(int)m.Kind];
        }
        static string Yes(bool value) { return value ? "on" : "off"; }
    }
}
