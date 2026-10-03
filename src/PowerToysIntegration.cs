using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class PowerToysShortcut
    {
        public string Module, Action, Chord, FilePath, FileHash, DscModule;
        public string[] PropertyPath;
        public bool ModuleEnabled, IsProfileRemap;
        public bool CanEdit { get { return !IsProfileRemap && DscModule != null; } }
    }

    internal static class PowerToysIntegration
    {
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024, RecursionLimit = 64 };
        static readonly Dictionary<string, string> DscNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "AdvancedPaste", "AdvancedPaste" }, { "AlwaysOnTop", "AlwaysOnTop" }, { "Awake", "Awake" },
            { "ColorPicker", "ColorPicker" }, { "CropAndLock", "CropAndLock" }, { "EnvironmentVariables", "EnvironmentVariables" },
            { "FancyZones", "FancyZones" }, { "FindMyMouse", "FindMyMouse" }, { "Hosts", "Hosts" },
            { "Image Resizer", "ImageResizer" }, { "Keyboard Manager", "KeyboardManager" }, { "Measure Tool", "MeasureTool" },
            { "MouseHighlighter", "MouseHighlighter" }, { "MouseJump", "MouseJump" }, { "MousePointerCrosshairs", "MousePointerCrosshairs" },
            { "Peek", "Peek" }, { "QuickAccent", "PowerAccent" }, { "PowerRename", "PowerRename" },
            { "RegistryPreview", "RegistryPreview" }, { "Shortcut Guide", "ShortcutGuide" },
            { "TextExtractor", "PowerOCR" }, { "Workspaces", "Workspaces" }, { "ZoomIt", "ZoomIt" }
        };
        public static string Root { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys"); } }
        static string DscPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerToys", "PowerToys.DSC.exe"); } }
        public static bool Available { get { return Directory.Exists(Root); } }

        static Dictionary<string, object> ReadObject(string file)
        {
            if (new FileInfo(file).Length > 1024 * 1024) throw new InvalidDataException("PowerToys settings file is unexpectedly large.");
            var data = Json.DeserializeObject(File.ReadAllText(file, Encoding.UTF8)) as Dictionary<string, object>;
            if (data == null) throw new InvalidDataException("PowerToys settings must be a JSON object.");
            return data;
        }
        static Dictionary<string, object> Child(Dictionary<string, object> parent, string key)
        {
            object value; return parent.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }
        static bool Bool(Dictionary<string, object> data, string key)
        {
            object value; return data != null && data.TryGetValue(key, out value) && value is bool && (bool)value;
        }
        static int Code(Dictionary<string, object> data)
        {
            object value; if (!data.TryGetValue("code", out value)) return 0;
            try { return Convert.ToInt32(value); } catch { return 0; }
        }
        static bool IsHotkey(Dictionary<string, object> data)
        {
            return data.ContainsKey("win") && data.ContainsKey("ctrl") && data.ContainsKey("alt") && data.ContainsKey("shift") && data.ContainsKey("code");
        }
        static string Humanize(string key)
        {
            string text = Regex.Replace(key.Replace('_', ' ').Replace('-', ' '), "([a-z])([A-Z])", "$1 $2");
            text = Regex.Replace(text, "(?i)\\b(default|shortcut|hotkey)\\b", "").Trim();
            return String.IsNullOrWhiteSpace(text) ? key : Char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
        static string ActionName(string[] path)
        {
            var names = path.Where(x => x != "properties" && x != "value" && x != "additional-actions" && !Regex.IsMatch(x, "^[0-9]+$"))
                .Select(Humanize).Where(x => !String.IsNullOrWhiteSpace(x)).ToList();
            if (names.Count == 0) return path.Last();
            if (names.Last().Equals("shortcut", StringComparison.OrdinalIgnoreCase) || names.Last().Equals("hotkey", StringComparison.OrdinalIgnoreCase)) names.RemoveAt(names.Count - 1);
            if (names.Count == 0) return "Toggle";
            return String.Join(" • ", names);
        }
        static string Hash(string file)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(file)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
        internal static string FormatHotkey(Dictionary<string, object> data)
        {
            int code = Code(data); if (code == 0) return "Unassigned";
            var parts = new List<string>();
            if (Bool(data, "ctrl")) parts.Add("Ctrl"); if (Bool(data, "alt")) parts.Add("Alt");
            if (Bool(data, "shift")) parts.Add("Shift"); if (Bool(data, "win")) parts.Add("Win");
            string key = ((Keys)code).ToString();
            if (key.StartsWith("D") && key.Length == 2 && Char.IsDigit(key[1])) key = key.Substring(1);
            if (key == "Escape") key = "Esc";
            parts.Add(key); return String.Join("+", parts);
        }
        static void Scan(object node, string[] path, string module, bool enabled, string file, string hash, string dsc, List<PowerToysShortcut> found)
        {
            var data = node as Dictionary<string, object>;
            if (data != null) {
                if (IsHotkey(data) && path.Length > 0 && !path.Last().StartsWith("Default", StringComparison.OrdinalIgnoreCase)) {
                    found.Add(new PowerToysShortcut { Module = module, Action = ActionName(path), Chord = FormatHotkey(data),
                        ModuleEnabled = enabled, FilePath = file, FileHash = hash, DscModule = dsc, PropertyPath = path });
                    return;
                }
                foreach (var entry in data) Scan(entry.Value, path.Concat(new[] { entry.Key }).ToArray(), module, enabled, file, hash, dsc, found);
            } else {
                var array = node as object[];
                if (array != null) for (int i = 0; i < array.Length; i++) Scan(array[i], path.Concat(new[] { i.ToString() }).ToArray(), module, enabled, file, hash, dsc, found);
            }
        }
        public static List<PowerToysShortcut> Load(string root)
        {
            var found = new List<PowerToysShortcut>();
            string general = Path.Combine(root, "settings.json");
            if (!File.Exists(general)) return found;
            var generalData = ReadObject(general); var enabledMap = Child(generalData, "enabled");
            Scan(generalData, new string[0], "PowerToys", true, general, Hash(general), File.Exists(DscPath) ? "App" : null, found);
            foreach (string dir in Directory.GetDirectories(root)) {
                string file = Path.Combine(dir, "settings.json"); if (!File.Exists(file)) continue;
                string module = Path.GetFileName(dir); string dsc;
                if (!DscNames.TryGetValue(module, out dsc) || !File.Exists(DscPath)) dsc = null;
                try { Scan(ReadObject(file), new string[0], module, Bool(enabledMap, module), file, Hash(file), dsc, found); }
                catch (Exception) { /* One malformed module must not hide the others. */ }
            }
            // Keyboard Manager remaps have a separate profile file and schema. Show them, but edit in PowerToys' own editor.
            string kbm = Path.Combine(root, "Keyboard Manager"); string kbmSettings = Path.Combine(kbm, "settings.json");
            if (File.Exists(kbmSettings)) try {
                var properties = Child(ReadObject(kbmSettings), "properties"); var active = Child(properties, "activeConfiguration");
                object profileName; if (active != null && active.TryGetValue("value", out profileName) && profileName is string) {
                    string name = (string)profileName;
                    if (Regex.IsMatch(name, "^[A-Za-z0-9_-]{1,80}$")) {
                        string profile = Path.Combine(kbm, name + ".json");
                        if (File.Exists(profile)) {
                            var remaps = Child(ReadObject(profile), "remapShortcuts");
                            if (remaps != null) foreach (string scope in new[] { "global", "appSpecific" }) {
                                object rows; if (!remaps.TryGetValue(scope, out rows)) continue;
                                var array = rows as object[]; if (array == null) continue;
                                foreach (object row in array) {
                                    var item = row as Dictionary<string, object>; if (item == null) continue;
                                    object original, destination; if (!item.TryGetValue("originalKeys", out original) || !item.TryGetValue("newRemapKeys", out destination)) continue;
                                    string app = ""; object target; if (item.TryGetValue("targetApp", out target) && target is string) app = " (" + target + ")";
                                    found.Add(new PowerToysShortcut { Module = "Keyboard Manager", Action = scope == "global" ? "Remap to " + destination : "App remap" + app + " to " + destination,
                                        Chord = VirtualKeysToText(Convert.ToString(original)), ModuleEnabled = Bool(enabledMap, "Keyboard Manager"), FilePath = profile, IsProfileRemap = true });
                                }
                            }
                        }
                    }
                }
            } catch (Exception) { }
            return found.OrderBy(x => x.Module).ThenBy(x => x.Action).ToList();
        }
        static string VirtualKeysToText(string codes)
        {
            var parts = new List<string>(); foreach (string token in (codes ?? "").Split(';')) {
                int n; if (!Int32.TryParse(token, out n)) continue;
                if (n == 0xA2 || n == 0xA3 || n == 0x11) parts.Add("Ctrl");
                else if (n == 0xA4 || n == 0xA5 || n == 0x12) parts.Add("Alt");
                else if (n == 0xA0 || n == 0xA1 || n == 0x10) parts.Add("Shift");
                else if (n == 0x5B || n == 0x5C) parts.Add("Win");
                else parts.Add(((Keys)n).ToString());
            } return String.Join("+", parts);
        }
        public static List<PowerToysShortcut> Load() { return Load(Root); }

        static string Quote(string value)
        {
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value) {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { result.Append('\\', slashes * 2 + 1); result.Append('"'); slashes = 0; continue; }
                result.Append('\\', slashes); slashes = 0; result.Append(c);
            }
            result.Append('\\', slashes * 2); result.Append('"'); return result.ToString();
        }
        static string DscCall(string verb, string module, Dictionary<string, object> settings)
        {
            if (!File.Exists(DscPath)) throw new FileNotFoundException("PowerToys configuration tool is unavailable.", DscPath);
            var input = new Dictionary<string, object> { { "settings", settings } };
            var start = new ProcessStartInfo(DscPath, verb + " --resource settings --module " + Quote(module) + " --input " + Quote(Json.Serialize(input))) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = Process.Start(start)) {
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync(), errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(10000)) { try { process.Kill(); } catch { } throw new TimeoutException("PowerToys did not finish applying the setting."); }
                string error = errorTask.Result, output = outputTask.Result;
                if (process.ExitCode != 0) throw new InvalidOperationException("PowerToys rejected the change: " + (error.Length > 0 ? error : output));
                return output;
            }
        }
        static void DscSet(string module, Dictionary<string, object> settings) { DscCall("set", module, settings); }
        internal static string TestCurrentDscInput(string module, string settingsFile) { return DscCall("test", module, ReadObject(settingsFile)); }
        static string Backup(string path)
        {
            string folder = Path.Combine(AppStorage.DataFolder, "PowerToysBackups");
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, Path.GetFileName(Path.GetDirectoryName(path)) + "-" + Path.GetFileName(path) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".bak");
            File.Copy(path, target, false); return target;
        }
        public static string Save(PowerToysShortcut selected, string chord, bool enableModule)
        {
            if (selected == null || !selected.CanEdit) throw new InvalidOperationException("This PowerToys shortcut must be edited in PowerToys Settings.");
            if (!File.Exists(selected.FilePath) || Hash(selected.FilePath) != selected.FileHash) throw new InvalidOperationException("PowerToys settings changed since this list loaded. Refresh and try again.");
            HotkeyChord parsed = HotkeyChord.Parse(chord); if (parsed.Key == (int)Keys.L) {
                if ((parsed.Modifiers & HotkeyChord.Win) != 0 && (parsed.Modifiers & (HotkeyChord.Ctrl | HotkeyChord.Alt | HotkeyChord.Shift)) == 0)
                    throw new ArgumentException("Win+L is reserved by Windows.");
            }
            string normalized = HotkeyChord.Normalize(chord);
            var conflict = Load().FirstOrDefault(x => x.ModuleEnabled && x.Chord == normalized &&
                !(x.FilePath == selected.FilePath && x.PropertyPath != null && selected.PropertyPath != null && x.PropertyPath.SequenceEqual(selected.PropertyPath)));
            if (conflict != null) throw new ArgumentException(normalized + " is already used by " + conflict.Module + " (" + conflict.Action + ").");
            var settings = ReadObject(selected.FilePath); var current = settings;
            foreach (string key in selected.PropertyPath) {
                var child = Child(current, key); if (child == null) throw new InvalidDataException("PowerToys changed this shortcut format. Refresh the list."); current = child;
            }
            if (!IsHotkey(current)) throw new InvalidDataException("PowerToys changed this shortcut format.");
            current["win"] = (parsed.Modifiers & HotkeyChord.Win) != 0; current["ctrl"] = (parsed.Modifiers & HotkeyChord.Ctrl) != 0;
            current["alt"] = (parsed.Modifiers & HotkeyChord.Alt) != 0; current["shift"] = (parsed.Modifiers & HotkeyChord.Shift) != 0;
            current["code"] = parsed.Key; current["key"] = "";
            string backup = Backup(selected.FilePath);
            DscSet(selected.DscModule, settings);
            var persisted = ReadObject(selected.FilePath);
            foreach (string key in selected.PropertyPath) {
                persisted = Child(persisted, key); if (persisted == null) throw new IOException("PowerToys did not retain the shortcut change. Backup: " + backup);
            }
            if (FormatHotkey(persisted) != normalized) throw new IOException("PowerToys did not retain the shortcut change. Backup: " + backup);
            if (enableModule && !selected.ModuleEnabled) {
                string generalFile = Path.Combine(Root, "settings.json"); var general = ReadObject(generalFile); var enabled = Child(general, "enabled");
                if (enabled == null || !enabled.ContainsKey(selected.Module)) throw new InvalidDataException("PowerToys no longer lists this module. Its shortcut was saved, but the module was not enabled.");
                Backup(generalFile); enabled[selected.Module] = true;
                try { DscSet("App", general); }
                catch (Exception ex) { throw new InvalidOperationException("Shortcut saved, but PowerToys could not enable the module: " + ex.Message); }
                if (!Bool(Child(ReadObject(generalFile), "enabled"), selected.Module)) throw new IOException("Shortcut saved, but PowerToys did not retain the module enable setting.");
            }
            return backup;
        }
        public static void OpenSettings()
        {
            string exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerToys", "WinUI3Apps", "PowerToys.Settings.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("PowerToys Settings is unavailable.", exe);
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }
    }
}
