using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    internal static class MappingNoteStore
    {
        internal const int MaxNoteLength = 1000;
        internal const int MaxNotes = 512;
        internal const int MaxBytes = 128 * 1024;
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "mapping-notes.json"); } }
        internal static string FunctionKey(string profile, string layer, int index) { return "function|" + Clean(profile) + "|" + Clean(layer) + "|F" + (index + 1); }
        internal static string CustomKey(string profile, int index) { return "custom|" + Clean(profile) + "|" + index; }
        internal static Dictionary<string, string> Load() { return Load(Path); }
        internal static Dictionary<string, string> Load(string path)
        {
            if (!File.Exists(path)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (new FileInfo(path).Length > MaxBytes) throw new ArgumentException("Private mapping notes are too large.");
            string json = File.ReadAllText(path, Encoding.UTF8); JsonSyntax.Check(json);
            var root = new JavaScriptSerializer { MaxJsonLength = MaxBytes }.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null || root.Count != 2 || !(root.ContainsKey("version")) || !(root["version"] is int) || (int)root["version"] != 1 || !(root["notes"] is Dictionary<string, object>)) throw new ArgumentException("Invalid private mapping notes.");
            var raw = (Dictionary<string, object>)root["notes"]; if (raw.Count > MaxNotes) throw new ArgumentException("Private mapping notes contain too many entries.");
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in raw) { if (String.IsNullOrWhiteSpace(item.Key) || item.Key.Length > 180 || !(item.Value is string) || ((string)item.Value).Length > MaxNoteLength) throw new ArgumentException("Invalid private mapping note."); if (((string)item.Value).Length > 0) result[item.Key] = (string)item.Value; }
            return result;
        }
        internal static void Set(string key, string note) { var notes = Load(); Set(notes, key, note); Save(Path, notes); }
        internal static void Set(IDictionary<string, string> notes, string key, string note)
        {
            if (String.IsNullOrWhiteSpace(key) || key.Length > 180) throw new ArgumentException("Invalid mapping-note key.");
            note = note ?? ""; if (note.Length > MaxNoteLength) throw new ArgumentException("Private mapping notes are limited to 1000 characters.");
            if (note.Length == 0) notes.Remove(key); else { if (!notes.ContainsKey(key) && notes.Count >= MaxNotes) throw new ArgumentException("Private mapping notes are limited to 512 entries."); notes[key] = note; }
        }
        internal static void Save(string path, IDictionary<string, string> notes)
        {
            var payload = new Dictionary<string, object> { { "version", 1 }, { "notes", notes ?? new Dictionary<string, string>() } };
            string json = new JavaScriptSerializer { MaxJsonLength = MaxBytes }.Serialize(payload); if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("Private mapping notes are too large.");
            string full = System.IO.Path.GetFullPath(path), dir = System.IO.Path.GetDirectoryName(full); Directory.CreateDirectory(dir); string temp = full + ".tmp";
            try { File.WriteAllText(temp, json, new UTF8Encoding(false)); if (File.Exists(full)) File.Replace(temp, full, full + ".bak", true); else File.Move(temp, full); } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        static string Clean(string value) { string text = String.IsNullOrWhiteSpace(value) ? "Default" : value.Trim().Replace("|", "/"); return text.Substring(0, Math.Min(60, text.Length)); }
    }
}
