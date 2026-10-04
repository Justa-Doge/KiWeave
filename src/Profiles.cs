using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    public sealed class KeyWeaveProfile
    {
        public string Name = "";
        public string[] Applications = new string[0];
        public string InheritFrom = "";
        public string[] OverrideKeys = new string[0];
        public string Accent = "";
        public string Icon = "";
        public Configuration Configuration = new Configuration();
        public KeyWeaveProfile Copy() { return new KeyWeaveProfile { Name = Name, Applications = Applications.ToArray(), InheritFrom = InheritFrom, OverrideKeys = OverrideKeys.ToArray(), Accent = Accent, Icon = Icon, Configuration = Configuration.Copy() }; }
        public override string ToString() { return (String.IsNullOrEmpty(Icon) ? "" : Icon + "  ") + Name; }
    }

    public sealed class ProfileCollection
    {
        public KeyWeaveProfile[] Profiles = new KeyWeaveProfile[0];
        public ProfileCollection Copy() { return new ProfileCollection { Profiles = Profiles.Select(p => p.Copy()).ToArray() }; }
        public KeyWeaveProfile Find(string name) { return Profiles.FirstOrDefault(p => String.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)); }
        public KeyWeaveProfile ForApplication(string processName)
        {
            string normalized = ProfileStore.NormalizeProcess(processName);
            if (normalized.Length == 0) return null;
            return Profiles.FirstOrDefault(p => p.Applications.Any(a => String.Equals(ProfileStore.NormalizeProcess(a), normalized, StringComparison.OrdinalIgnoreCase)));
        }
        public Configuration Resolve(string name, Configuration defaultConfiguration)
        {
            if (String.Equals(name, "Default", StringComparison.OrdinalIgnoreCase)) return defaultConfiguration.Copy();
            KeyWeaveProfile profile = Find(name); if (profile == null) throw new ArgumentException("Profile not found: " + name);
            return Resolve(profile, defaultConfiguration, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
        Configuration Resolve(KeyWeaveProfile profile, Configuration defaultConfiguration, HashSet<string> chain)
        {
            if (!chain.Add(profile.Name)) throw new ArgumentException("Profile inheritance contains a loop involving " + profile.Name + ".");
            try {
                if (String.IsNullOrWhiteSpace(profile.InheritFrom)) return profile.Configuration.Copy();
                Configuration result;
                if (String.Equals(profile.InheritFrom, "Default", StringComparison.OrdinalIgnoreCase)) result = defaultConfiguration.Copy();
                else { KeyWeaveProfile basis = Find(profile.InheritFrom); if (basis == null) throw new ArgumentException(profile.Name + " inherits from a missing profile."); result = Resolve(basis, defaultConfiguration, chain); }
                ProfileStore.ApplyOverrides(result, profile.Configuration, profile.OverrideKeys); return result;
            } finally { chain.Remove(profile.Name); }
        }
    }

    public static class ProfileStore
    {
        public const int MaxBytes = 1024 * 1024;
        public static string DefaultPath { get { return Path.Combine(Path.GetDirectoryName(ConfigStore.DefaultPath), "profiles.json"); } }
        public static string NormalizeProcess(string value)
        {
            string text = (value ?? "").Trim();
            if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) text = text.Substring(0, text.Length - 4);
            return text;
        }
        public static void Validate(ProfileCollection collection, bool checkExists)
        {
            if (collection == null || collection.Profiles == null || collection.Profiles.Length > 24) throw new ArgumentException("Use up to 24 profiles.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var applications = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var profile in collection.Profiles) {
                if (profile == null || String.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 40 || profile.Name.Any(Char.IsControl)) throw new ArgumentException("Profile names must be 1 to 40 characters.");
                if (profile.Accent == null || profile.Accent.Length > 7 || profile.Accent.Length != 0 && !System.Text.RegularExpressions.Regex.IsMatch(profile.Accent, "\\A#[0-9a-fA-F]{6}\\z")) throw new ArgumentException(profile.Name + " has an invalid accent color.");
                if (profile.Icon == null || profile.Icon.Length > 4 || profile.Icon.Any(Char.IsControl)) throw new ArgumentException(profile.Name + " has an invalid icon label.");
                if (!names.Add(profile.Name.Trim())) throw new ArgumentException("Profile names must be unique.");
                if (profile.Applications == null || profile.Applications.Length > 24) throw new ArgumentException(profile.Name + " has too many automatic applications.");
                var local = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string app in profile.Applications) {
                    string normalized = NormalizeProcess(app);
                    if (normalized.Length == 0 || normalized.Length > 120 || normalized.Any(c => Char.IsControl(c) || c == '\\' || c == '/' || c == ':')) throw new ArgumentException(profile.Name + " contains an invalid application name. Use a process name such as obs64.exe.");
                    if (!local.Add(normalized)) throw new ArgumentException(profile.Name + " lists " + normalized + " more than once.");
                    string owner;
                    if (applications.TryGetValue(normalized, out owner)) throw new ArgumentException(normalized + " is already assigned to profile " + owner + ".");
                    applications[normalized] = profile.Name;
                }
                if (profile.InheritFrom == null || profile.OverrideKeys == null) throw new ArgumentException(profile.Name + " has invalid inheritance data.");
                ConfigStore.Validate(profile.Configuration, checkExists);
            }
            foreach (var profile in collection.Profiles) {
                if (String.IsNullOrWhiteSpace(profile.InheritFrom)) { if (profile.OverrideKeys.Length != 0) throw new ArgumentException(profile.Name + " is independent and cannot contain inherited overrides."); continue; }
                if (String.Equals(profile.Name, profile.InheritFrom, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(profile.Name + " cannot inherit from itself.");
                if (!String.Equals(profile.InheritFrom, "Default", StringComparison.OrdinalIgnoreCase) && collection.Find(profile.InheritFrom) == null) throw new ArgumentException(profile.Name + " inherits from a missing profile.");
                var allowed = new HashSet<string>(Enumerable.Range(1, 12).Select(i => "F" + i), StringComparer.OrdinalIgnoreCase) { "Enabled", "CustomHotkeys", "Layers" };
                var overrides = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string key in profile.OverrideKeys) if (key == null || !allowed.Contains(key) || !overrides.Add(key)) throw new ArgumentException(profile.Name + " contains an invalid inherited override.");
                collection.Resolve(profile.Name, new Configuration());
            }
        }
        internal static void ApplyOverrides(Configuration target, Configuration source, IEnumerable<string> keys)
        {
            foreach (string key in keys) {
                if (key.Equals("Enabled", StringComparison.OrdinalIgnoreCase)) target.Enabled = source.Enabled;
                else if (key.Equals("CustomHotkeys", StringComparison.OrdinalIgnoreCase)) target.CustomHotkeys = source.CustomHotkeys.Select(x => x.Copy()).ToArray();
                else if (key.Equals("Layers", StringComparison.OrdinalIgnoreCase)) target.Layers = source.Layers.Select(x => x.Copy()).ToArray();
                else { int number; if (key.Length > 1 && Int32.TryParse(key.Substring(1), out number) && number >= 1 && number <= 12) target.Mappings[number - 1] = source.Mappings[number - 1].Copy(); }
            }
        }
        public static void SetEffectiveConfiguration(ProfileCollection collection, KeyWeaveProfile profile, Configuration effective, Configuration defaultConfiguration)
        {
            profile.Configuration = effective.Copy();
            if (String.IsNullOrWhiteSpace(profile.InheritFrom)) { profile.OverrideKeys = new string[0]; return; }
            Configuration basis = String.Equals(profile.InheritFrom, "Default", StringComparison.OrdinalIgnoreCase) ? defaultConfiguration.Copy() : collection.Resolve(profile.InheritFrom, defaultConfiguration);
            var keys = new List<string>();
            if (basis.Enabled != effective.Enabled) keys.Add("Enabled");
            for (int i = 0; i < 12; i++) if (!Same(basis.Mappings[i], effective.Mappings[i])) keys.Add("F" + (i + 1));
            if (!Same(basis.CustomHotkeys, effective.CustomHotkeys)) keys.Add("CustomHotkeys");
            if (!Same(basis.Layers, effective.Layers)) keys.Add("Layers");
            profile.OverrideKeys = keys.ToArray();
        }
        static bool Same(Mapping a, Mapping b) { return a.Kind == b.Kind && a.Target == b.Target && a.Arguments == b.Arguments && a.WorkingDirectory == b.WorkingDirectory && a.MonitorId == b.MonitorId && a.MonitorControl == b.MonitorControl && a.MonitorStep == b.MonitorStep; }
        static bool Same(CustomHotkey[] a, CustomHotkey[] b) { return a.Length == b.Length && a.Zip(b, (x, y) => String.Equals(x.Shortcut, y.Shortcut, StringComparison.OrdinalIgnoreCase) && Same(x.Action, y.Action)).All(x => x); }
        static bool Same(ModifierLayer[] a, ModifierLayer[] b) { return a.Length == b.Length && a.Zip(b, (x, y) => x.Name == y.Name && x.ActivationKey == y.ActivationKey && x.Mappings.Length == y.Mappings.Length && x.Mappings.Zip(y.Mappings, Same).All(z => z)).All(x => x); }
        public static ProfileCollection Load(string path)
        {
            if (!File.Exists(path)) return new ProfileCollection();
            if (new FileInfo(path).Length > MaxBytes) throw new ArgumentException("Profiles file is too large.");
            return Parse(File.ReadAllText(path, Encoding.UTF8));
        }
        public static ProfileCollection Parse(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("Profiles file is too large.");
            JsonSyntax.Check(json);
            var root = new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 20 }.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null || root.Count != 2 || !(root.ContainsKey("version")) || !(root["version"] is int) || ((int)root["version"] != 1 && (int)root["version"] != 2 && (int)root["version"] != 3) || !root.ContainsKey("profiles")) throw new ArgumentException("Invalid profiles file.");
            int version = (int)root["version"];
            var rawProfiles = root["profiles"] as object[]; if (rawProfiles == null) throw new ArgumentException("Profiles must be an array.");
            var result = new ProfileCollection { Profiles = rawProfiles.Select(item => {
                var d = item as Dictionary<string, object>;
                string[] fields = version == 1 ? new[] { "name", "applications", "configuration" } : version == 2 ? new[] { "name", "applications", "inheritFrom", "overrideKeys", "configuration" } : new[] { "name", "applications", "inheritFrom", "overrideKeys", "accent", "icon", "configuration" };
                if (d == null || d.Count != fields.Length || fields.Any(f => !d.ContainsKey(f)) || !(d["name"] is string) || !(d["configuration"] is string)) throw new ArgumentException("Invalid profile entry.");
                var apps = d["applications"] as object[]; if (apps == null || apps.Any(a => !(a is string))) throw new ArgumentException("Profile applications must be text.");
                var overrides = version == 1 ? new object[0] : d["overrideKeys"] as object[];
                if (overrides == null || overrides.Any(x => !(x is string)) || (version >= 2 && !(d["inheritFrom"] is string))) throw new ArgumentException("Invalid profile inheritance entry.");
                string accent = version >= 3 && d["accent"] is string ? (string)d["accent"] : ""; string icon = version >= 3 && d["icon"] is string ? (string)d["icon"] : "";
                return new KeyWeaveProfile { Name = (string)d["name"], Applications = apps.Cast<string>().ToArray(), InheritFrom = version == 1 ? "" : (string)d["inheritFrom"], OverrideKeys = overrides.Cast<string>().ToArray(), Accent = accent, Icon = icon, Configuration = ConfigStore.Parse((string)d["configuration"]) };
            }).ToArray() };
            Validate(result, false); return result;
        }
        public static string Serialize(ProfileCollection collection)
        {
            Validate(collection, false);
            var s = new JavaScriptSerializer { MaxJsonLength = MaxBytes };
            var payload = new { version = 3, profiles = collection.Profiles.Select(p => new { name = p.Name.Trim(), applications = p.Applications.Select(NormalizeProcess).ToArray(), inheritFrom = p.InheritFrom.Trim(), overrideKeys = p.OverrideKeys, accent = p.Accent ?? "", icon = p.Icon ?? "", configuration = ConfigStore.Serialize(p.Configuration) }).ToArray() };
            string json = s.Serialize(payload); if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("Profiles exceed the 1 MB limit."); return json;
        }
        public static void Save(string path, ProfileCollection collection)
        {
            string json = Serialize(collection);
            string dir = Path.GetDirectoryName(Path.GetFullPath(path)); Directory.CreateDirectory(dir);
            string temp = Path.Combine(dir, ".profiles-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true); else File.Move(temp, path);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
