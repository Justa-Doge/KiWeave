using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    public enum ActionKind { PassThrough, Unbound, SendKey, SendShortcut, Media, Application, FileOrFolder, WindowsShortcut, Command, Monitor, Python, LockThenSleep, Sequence }

    public sealed class Mapping
    {
        public ActionKind Kind;
        public string Target = "", Arguments = "", WorkingDirectory = "";
        public string MonitorId = "", MonitorControl = "";
        public int MonitorStep = 5;
        public Mapping Copy() { return (Mapping)MemberwiseClone(); }
        public bool Repeats { get { return Kind == ActionKind.Monitor || (Kind == ActionKind.Media && (Target == "VolumeUp" || Target == "VolumeDown")); } }
        public string Summary
        {
            get
            {
                if (Kind == ActionKind.PassThrough) return "Pass through normally";
                if (Kind == ActionKind.Unbound) return "Unbound (do nothing)";
                if (Kind == ActionKind.Media) return Shortcuts.MediaLabels[Target];
                if (Kind == ActionKind.LockThenSleep) return "Lock Windows, then sleep";
                if (Kind == ActionKind.Sequence) return "Action sequence (" + SequenceCodec.Parse(Target).Count + " steps)";
                if (Kind == ActionKind.Monitor) return "DDC/CI: " + (DdcOperation.Find(MonitorControl) == null ? "Choose a control" : DdcOperation.Find(MonitorControl).Label) + " (" + MonitorStep + "%)";
                if (Kind == ActionKind.SendKey || Kind == ActionKind.SendShortcut) return Target;
                return Labels[(int)Kind] + ": " + Path.GetFileName(Target.TrimEnd('\\'));
            }
        }
        public static readonly string[] Labels = { "Pass through", "Unbound", "Send a key", "Send a shortcut", "Media / system", "Open an application", "Open a file or folder", "Run a Windows shortcut", "Run a command or script", "Monitor (DDC/CI)", "Run a Python script", "Lock Windows, then sleep", "Action sequence" };
    }

    public sealed class CustomHotkey
    {
        public string Shortcut = "";
        public Mapping Action = new Mapping();
        public CustomHotkey Copy() { return new CustomHotkey { Shortcut = Shortcut, Action = Action == null ? null : Action.Copy() }; }
        public string Summary { get { return Action == null ? "Choose an action" : Action.Summary; } }
    }

    public sealed class Configuration
    {
        public bool Enabled;
        public Mapping[] Mappings = Enumerable.Range(0, 12).Select(i => new Mapping()).ToArray();
        public CustomHotkey[] CustomHotkeys = new CustomHotkey[0];
        public Configuration Copy() { return new Configuration { Enabled = Enabled, Mappings = Mappings.Select(m => m.Copy()).ToArray(), CustomHotkeys = (CustomHotkeys ?? new CustomHotkey[0]).Select(h => h.Copy()).ToArray() }; }
    }

    public static class Shortcuts
    {
        public static readonly Dictionary<string, string> MediaLabels = new Dictionary<string, string> {
            { "VolumeDown", "Volume down" }, { "VolumeUp", "Volume up" }, { "VolumeMute", "Mute / unmute" },
            { "MediaPlayPause", "Play / pause" }, { "MediaNextTrack", "Next track" }, { "MediaPreviousTrack", "Previous track" }
        };
        public static int[] Parse(string text, bool single)
        {
            if (String.IsNullOrWhiteSpace(text)) throw new ArgumentException("Enter a key or shortcut.");
            string[] tokens = text.Split('+');
            if (tokens.Length > 5 || (single && tokens.Length != 1)) throw new ArgumentException("Use one key, or up to four modifiers plus one key for a shortcut.");
            var result = new List<int>();
            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i].Trim(); int vk;
                if (i < tokens.Length - 1)
                {
                    switch (t.ToLowerInvariant()) {
                        case "ctrl": case "control": vk = 0x11; break;
                        case "alt": vk = 0x12; break;
                        case "shift": vk = 0x10; break;
                        case "win": vk = 0x5B; break;
                        default: throw new ArgumentException("Use Ctrl, Alt, Shift or Win before the final key.");
                    }
                }
                else
                {
                    if (t.Length == 1 && t[0] >= '0' && t[0] <= '9') t = "D" + t;
                    if (t.Equals("Esc", StringComparison.OrdinalIgnoreCase)) t = "Escape";
                    if (t.Equals("Plus", StringComparison.OrdinalIgnoreCase)) t = "Oemplus";
                    Keys k;
                    if (!Enum.TryParse<Keys>(t, true, out k) || !Enum.IsDefined(typeof(Keys), k) || t.Length == 0 || Char.IsDigit(t[0]))
                        throw new ArgumentException("Unknown key. Examples: A, F5, Enter, Tab, Escape, Left, Space, Oemplus.");
                    vk = (int)k;
                    if (vk < 8 || vk > 254 || vk == 0x10 || vk == 0x11 || vk == 0x12 || vk == 0x5B || vk == 0x5C || (vk >= 0xA0 && vk <= 0xA5))
                        throw new ArgumentException("The final key must be a regular key, not a modifier or mouse button.");
                }
                if (result.Contains(vk)) throw new ArgumentException("A shortcut cannot contain duplicate keys.");
                result.Add(vk);
            }
            if (result.Contains(0x11) && result.Contains(0x12) && result.Last() == 0x2E)
                throw new ArgumentException("Ctrl+Alt+Delete is reserved by Windows and cannot be sent.");
            return result.ToArray();
        }
    }

    public static class ConfigStore
    {
        public const int MaxBytes = 65536;
        public static string DefaultPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FunctionRowRemapper", "config.json"); } }
        public static void Validate(Mapping m, bool checkExists)
        {
            if (m == null || !Enum.IsDefined(typeof(ActionKind), m.Kind)) throw new ArgumentException("Unknown action type.");
            foreach (string s in new[] { m.Target, m.Arguments, m.WorkingDirectory, m.MonitorId, m.MonitorControl })
                if (s == null || s.Length > 4096 || s.Any(Char.IsControl)) throw new ArgumentException("Fields must be text under 4097 characters with no control characters.");
            if (m.Kind == ActionKind.Monitor) {
                if (m.Target.Length != 0 || m.Arguments.Length != 0 || m.WorkingDirectory.Length != 0) throw new ArgumentException("Monitor actions cannot contain a launch target or arguments.");
                if (!System.Text.RegularExpressions.Regex.IsMatch(m.MonitorId, "\\A[0-9a-f]{64}\\z") || DdcOperation.Find(m.MonitorControl) == null || m.MonitorStep < 1 || m.MonitorStep > 20) throw new ArgumentException("Choose a detected monitor, a supported control, and a step from 1 to 20 percent.");
                return;
            }
            if (m.Kind == ActionKind.Sequence) {
                if (m.Arguments.Length != 0 || m.WorkingDirectory.Length != 0 || m.MonitorId.Length != 0 || m.MonitorControl.Length != 0 || m.MonitorStep != 5) throw new ArgumentException("Action sequences cannot contain launch or monitor fields.");
                SequenceCodec.Parse(m.Target); return;
            }
            if (m.MonitorId.Length != 0 || m.MonitorControl.Length != 0 || m.MonitorStep != 5) throw new ArgumentException("This action cannot contain monitor settings.");
            bool launch = (m.Kind >= ActionKind.Application && m.Kind <= ActionKind.Command) || m.Kind == ActionKind.Python;
            if (!launch && (m.Arguments.Length != 0 || m.WorkingDirectory.Length != 0)) throw new ArgumentException("This action does not accept arguments or a working directory.");
            if ((m.Kind == ActionKind.PassThrough || m.Kind == ActionKind.Unbound || m.Kind == ActionKind.LockThenSleep) && m.Target.Length != 0) throw new ArgumentException("This action does not accept a target.");
            if (m.Kind == ActionKind.SendKey || m.Kind == ActionKind.SendShortcut) Shortcuts.Parse(m.Target, m.Kind == ActionKind.SendKey);
            if (m.Kind == ActionKind.Media && !Shortcuts.MediaLabels.ContainsKey(m.Target)) throw new ArgumentException("Choose a supported media action.");
            if (launch)
            {
                ValidatePath(m.Target);
                string ext = Path.GetExtension(m.Target).ToLowerInvariant();
                if (m.Kind == ActionKind.Application && ext != ".exe") throw new ArgumentException("Choose an .exe application.");
                if (m.Kind == ActionKind.WindowsShortcut && ext != ".lnk") throw new ArgumentException("Choose a .lnk Windows shortcut.");
                if (m.Kind == ActionKind.Command && !new[] { ".exe", ".cmd", ".bat", ".ps1" }.Contains(ext)) throw new ArgumentException("Choose an .exe, .cmd, .bat or .ps1 command/script. For a shell command, choose cmd.exe and enter arguments.");
                if (m.Kind == ActionKind.Python && ext != ".py") throw new ArgumentException("Choose a .py Python script.");
                if ((m.Kind == ActionKind.FileOrFolder || m.Kind == ActionKind.WindowsShortcut) && (m.Arguments.Length > 0 || m.WorkingDirectory.Length > 0)) throw new ArgumentException("This action uses the file or shortcut's own settings; arguments must be empty.");
                if (m.WorkingDirectory.Length > 0) ValidatePath(m.WorkingDirectory);
                if (checkExists && !File.Exists(m.Target) && !(m.Kind == ActionKind.FileOrFolder && Directory.Exists(m.Target))) throw new ArgumentException("Target no longer exists: " + m.Target);
                if (checkExists && m.WorkingDirectory.Length > 0 && !Directory.Exists(m.WorkingDirectory)) throw new ArgumentException("Working directory does not exist.");
            }
        }
        static void ValidatePath(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || path.Contains("\"") || path.Contains("%"))
                throw new ArgumentException("Choose an absolute local path without quotes or environment variables.");
            // Local drive paths only: no network shares, URLs, device paths or alternate data streams.
            if (path.Length < 3 || !Char.IsLetter(path[0]) || path[1] != ':' || path[2] != '\\' || path.Substring(2).Contains(":"))
                throw new ArgumentException("Use a full local drive path, such as C:\\Tools\\app.exe. Network and device paths are not supported.");
            if (path.IndexOfAny(new[] { '*', '?', '<', '>', '|' }) >= 0) throw new ArgumentException("A path cannot contain wildcard or reserved characters.");
            Path.GetFullPath(path);
        }
        public static void Validate(Configuration c, bool checkExists)
        {
            if (c == null || c.Mappings == null || c.Mappings.Length != 12) throw new ArgumentException("Exactly twelve mappings are required.");
            for (int i = 0; i < 12; i++) try { Validate(c.Mappings[i], checkExists); } catch (Exception e) { throw new ArgumentException("F" + (i + 1) + ": " + e.Message); }
            if (c.CustomHotkeys == null || c.CustomHotkeys.Length > 32) throw new ArgumentException("Use up to 32 custom hotkeys.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < c.CustomHotkeys.Length; i++) {
                CustomHotkey h = c.CustomHotkeys[i];
                if (h == null || String.IsNullOrWhiteSpace(h.Shortcut) || h.Shortcut.Length > 80) throw new ArgumentException("Custom hotkey " + (i + 1) + " is incomplete.");
                string normalized = HotkeyChord.Normalize(h.Shortcut);
                if (!seen.Add(normalized)) throw new ArgumentException("Custom hotkeys must be unique.");
                if (h.Action == null || h.Action.Kind == ActionKind.PassThrough || h.Action.Kind == ActionKind.Unbound) throw new ArgumentException("Custom hotkey " + (i + 1) + " needs a real action.");
                try { Validate(h.Action, checkExists); } catch (Exception e) { throw new ArgumentException("Custom hotkey " + (i + 1) + ": " + e.Message); }
            }
        }
        static Dictionary<string, object> Object(object x, string[] fields)
        {
            var d = x as Dictionary<string, object>;
            if (d == null || d.Count != fields.Length || fields.Any(f => !d.ContainsKey(f))) throw new ArgumentException("Configuration contains missing or unknown fields.");
            return d;
        }
        static string Text(object x) { if (!(x is string)) throw new ArgumentException("Expected a text field."); return (string)x; }
        public static Configuration Parse(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("Configuration exceeds the 64 KB limit.");
            JsonSyntax.Check(json);
            var serializer = new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 12 };
            var rawRoot = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (rawRoot == null || !(rawRoot.ContainsKey("version")) || !(rawRoot["version"] is int)) throw new ArgumentException("Configuration version is missing or invalid.");
            int version = (int)rawRoot["version"];
            var root = Object(rawRoot, version == 3 ? new[] { "version", "enabled", "mappings", "customHotkeys" } : new[] { "version", "enabled", "mappings" });
            if (version != 1 && version != 2 && version != 3) throw new ArgumentException("Unsupported configuration version. Expected version 1, 2 or 3.");
            if (!(root["enabled"] is bool)) throw new ArgumentException("Enabled must be true or false.");
            var entries = root["mappings"] as object[];
            if (entries == null || entries.Length != 12) throw new ArgumentException("Exactly twelve mappings are required.");
            var c = new Configuration { Enabled = (bool)root["enabled"] }; var seen = new HashSet<int>();
            foreach (object item in entries)
            {
                var raw = item as Dictionary<string, object>;
                bool isMonitor = raw != null && raw.ContainsKey("action") && raw["action"] is string && (string)raw["action"] == "Monitor";
                var d = Object(item, isMonitor ? new[] { "key", "action", "monitorId", "control", "step" } : new[] { "key", "action", "target", "arguments", "workingDirectory" });
                string key = Text(d["key"]); int index;
                if (!key.StartsWith("F") || !Int32.TryParse(key.Substring(1), out index) || index < 1 || index > 12 || key != "F" + index || !seen.Add(index)) throw new ArgumentException("Keys must be unique F1 through F12.");
                ActionKind kind; string action = Text(d["action"]);
                if (!Enum.TryParse<ActionKind>(action, false, out kind) || !Enum.IsDefined(typeof(ActionKind), kind) || kind.ToString() != action) throw new ArgumentException("Unknown action: " + action);
                if (isMonitor) {
                    if (version < 2 || !(d["step"] is int)) throw new ArgumentException("Monitor mappings require version 2 and an integer step.");
                    c.Mappings[index - 1] = new Mapping { Kind = kind, MonitorId = Text(d["monitorId"]), MonitorControl = Text(d["control"]), MonitorStep = (int)d["step"] };
                } else c.Mappings[index - 1] = new Mapping { Kind = kind, Target = Text(d["target"]), Arguments = Text(d["arguments"]), WorkingDirectory = Text(d["workingDirectory"]) };
            }
            if (version == 3) {
                var hotkeys = root["customHotkeys"] as object[];
                if (hotkeys == null || hotkeys.Length > 32) throw new ArgumentException("Custom hotkeys must be an array of up to 32 entries.");
                c.CustomHotkeys = hotkeys.Select(item => {
                    var d = Object(item, new[] { "shortcut", "action", "target", "arguments", "workingDirectory" });
                    ActionKind kind; string action = Text(d["action"]);
                    if (!Enum.TryParse<ActionKind>(action, false, out kind) || kind == ActionKind.Monitor || !Enum.IsDefined(typeof(ActionKind), kind)) throw new ArgumentException("Unknown custom hotkey action.");
                    return new CustomHotkey { Shortcut = Text(d["shortcut"]), Action = new Mapping { Kind = kind, Target = Text(d["target"]), Arguments = Text(d["arguments"]), WorkingDirectory = Text(d["workingDirectory"]) } };
                }).ToArray();
            }
            Validate(c, false); return c;
        }
        public static string Serialize(Configuration c)
        {
            Validate(c, false);
            var s = new JavaScriptSerializer();
            int version = c.CustomHotkeys != null && c.CustomHotkeys.Length > 0 ? 3 : (c.Mappings.Any(m => m.Kind == ActionKind.Monitor) ? 2 : 1);
            var b = new StringBuilder("{\r\n  \"version\": " + version + ",\r\n  \"enabled\": " + (c.Enabled ? "true" : "false") + ",\r\n  \"mappings\": [\r\n");
            for (int i = 0; i < 12; i++) {
                Mapping m = c.Mappings[i];
                if (m.Kind == ActionKind.Monitor) b.Append("    " + s.Serialize(new { key = "F" + (i + 1), action = "Monitor", monitorId = m.MonitorId, control = m.MonitorControl, step = m.MonitorStep }));
                else b.Append("    " + s.Serialize(new { key = "F" + (i + 1), action = m.Kind.ToString(), target = m.Target, arguments = m.Arguments, workingDirectory = m.WorkingDirectory }));
                b.Append(i == 11 ? "\r\n" : ",\r\n");
            }
            b.Append("  ]");
            if (version == 3) {
                b.Append(",\r\n  \"customHotkeys\": [\r\n");
                for (int i = 0; i < c.CustomHotkeys.Length; i++) {
                    var h = c.CustomHotkeys[i];
                    b.Append("    " + s.Serialize(new { shortcut = HotkeyChord.Normalize(h.Shortcut), action = h.Action.Kind.ToString(), target = h.Action.Target, arguments = h.Action.Arguments, workingDirectory = h.Action.WorkingDirectory }));
                    b.Append(i == c.CustomHotkeys.Length - 1 ? "\r\n" : ",\r\n");
                }
                b.Append("  ]");
            }
            string json = b.Append("\r\n}\r\n").ToString();
            if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("Configuration exceeds the 64 KB limit. Shorten the fields before saving.");
            return json;
        }
        public static Configuration Load(string path)
        {
            if (new FileInfo(path).Length > MaxBytes) throw new ArgumentException("Configuration exceeds 64 KB.");
            return Parse(File.ReadAllText(path, Encoding.UTF8));
        }
        public static void Save(string path, Configuration c)
        {
            string text = Serialize(c); string dir = Path.GetDirectoryName(Path.GetFullPath(path)); Directory.CreateDirectory(dir);
            string temp = Path.Combine(dir, ".remapper-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                using (var f = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(text); f.Write(bytes, 0, bytes.Length); f.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
