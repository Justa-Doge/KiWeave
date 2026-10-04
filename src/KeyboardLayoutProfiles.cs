using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    internal static class KeyboardLayoutProfiles
    {
        static Dictionary<string, string> map;
        static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "keyboard-layout-profiles.json"); } }
        static void Ensure() { if (map != null) return; map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); try { var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path, Encoding.UTF8)) as Dictionary<string, object>; if (d != null) foreach (var pair in d) if (pair.Value is string) map[pair.Key] = (string)pair.Value; } catch { } }
        internal static string ForProfile(string profile) { Ensure(); string value; return profile != null && map.TryGetValue(profile, out value) ? value : ""; }
        internal static void Set(string profile, string layout) { Ensure(); if (String.IsNullOrWhiteSpace(layout)) map.Remove(profile); else map[profile] = layout.Trim(); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)); File.WriteAllText(Path, new JavaScriptSerializer().Serialize(map) + Environment.NewLine, Encoding.UTF8); }
        internal static string CurrentLayoutId() { string snapshot = KeyboardLayoutDrift.Snapshot(); int split = snapshot.IndexOf('|'); return split < 0 ? snapshot : snapshot.Substring(0, split); }
        internal static KeyWeaveProfile Match(ProfileCollection profiles) { string current = CurrentLayoutId(); return (profiles == null ? new KeyWeaveProfile[0] : profiles.Profiles).FirstOrDefault(p => String.Equals(ForProfile(p.Name), current, StringComparison.OrdinalIgnoreCase)); }
    }
}
