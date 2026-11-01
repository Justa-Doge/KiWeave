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
        internal const int MaxFileBytes = 64 * 1024;
        internal const int MaxProfiles = 256;
        static readonly Encoding StrictUtf8 = new UTF8Encoding(true, true);
        static Dictionary<string, string> map;
        static Exception loadError;
        static bool preserveCorruptPrimary;
        static string loadWarning;
        internal static string DataPath { get { return System.IO.Path.Combine(AppStorage.DataFolder, "keyboard-layout-profiles.json"); } }
        static string Path { get { return DataPath; } }

        static void Ensure()
        {
            if (map != null) return;
            try
            {
                bool recovered;
                string warning;
                map = Load(Path, out recovered, out warning);
                preserveCorruptPrimary = recovered;
                loadWarning = warning;
            }
            catch (Exception error)
            {
                map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                loadError = error;
                loadWarning = "Keyboard-layout assignments could not be read and will not be overwritten: " + error.Message;
            }
        }

        internal static string LoadWarning { get { Ensure(); return loadWarning ?? ""; } }
        internal static bool IsReadable { get { Ensure(); return loadError == null; } }

        // The WPF profile manager can roll its persisted files back after a failed
        // runtime activation. Drop the cached map so subsequent profile matching
        // observes the restored file rather than the rejected draft.
        internal static void ReloadSavedAssignments()
        {
            map = null;
            loadError = null;
            preserveCorruptPrimary = false;
            loadWarning = null;
            Ensure();
        }

        internal static string ForProfile(string profile)
        {
            Ensure();
            string value;
            return profile != null && map.TryGetValue(profile, out value) ? value : "";
        }

        internal static Dictionary<string, string> Load(string path)
        {
            bool recovered;
            string warning;
            return Load(path, out recovered, out warning);
        }

        internal static Dictionary<string, string> Load(string path, out bool recovered, out string warning)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A file path is required.", "path");
            recovered = false;
            warning = "";
            string backup = path + ".bak";
            try { return Read(path); }
            catch (FileNotFoundException)
            {
                try
                {
                    var restored = Read(backup);
                    recovered = true;
                    warning = "The primary keyboard-layout assignment file is missing. Assignments were recovered from its backup; saving will preserve the backup and any existing primary file.";
                    return restored;
                }
                catch (FileNotFoundException) { return Empty(); }
                catch (DirectoryNotFoundException) { return Empty(); }
            }
            catch (DirectoryNotFoundException) { return Empty(); }
            catch (Exception primaryError)
            {
                try
                {
                    var restored = Read(backup);
                    recovered = true;
                    warning = "The primary keyboard-layout assignment file could not be read. Assignments were recovered from its backup; saving will preserve the damaged primary file and backup.";
                    return restored;
                }
                catch (FileNotFoundException) { throw new InvalidDataException("The keyboard-layout assignment file is unreadable and has no usable backup. It was left untouched.", primaryError); }
                catch (DirectoryNotFoundException) { throw new InvalidDataException("The keyboard-layout assignment file is unreadable and has no usable backup. It was left untouched.", primaryError); }
                catch (Exception backupError) { throw new InvalidDataException("Both the keyboard-layout assignment file and its backup are unreadable. They were left untouched. Primary: " + primaryError.Message + " Backup: " + backupError.Message, primaryError); }
            }
        }

        static Dictionary<string, string> Empty() { return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); }

        static Dictionary<string, string> Read(string path)
        {
            string json = AtomicFile.ReadAllTextBounded(path, MaxFileBytes, StrictUtf8);
            JsonSyntax.Check(json);
            var root = new JavaScriptSerializer { MaxJsonLength = MaxFileBytes, RecursionLimit = 4 }.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null || root.Count > MaxProfiles) throw new InvalidDataException("The keyboard-layout assignment file has an invalid shape or too many profiles.");
            var result = Empty();
            foreach (var pair in root)
            {
                if (String.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 128 || !(pair.Value is string) || ((string)pair.Value).Length > 128)
                    throw new InvalidDataException("The keyboard-layout assignment file contains an invalid profile or layout value.");
                if (result.ContainsKey(pair.Key.Trim())) throw new InvalidDataException("The keyboard-layout assignment file contains duplicate profile names that differ only by case.");
                string value = ((string)pair.Value).Trim();
                if (value.Length > 0) result[pair.Key.Trim()] = value;
            }
            return result;
        }

        internal static Dictionary<string, string> WithProfile(Dictionary<string, string> source, string oldProfile, string newProfile, string layout)
        {
            var result = new Dictionary<string, string>(source ?? Empty(), StringComparer.OrdinalIgnoreCase);
            if (!String.IsNullOrWhiteSpace(oldProfile)) result.Remove(oldProfile.Trim());
            if (!String.IsNullOrWhiteSpace(newProfile))
            {
                string name = newProfile.Trim();
                result.Remove(name);
                if (!String.IsNullOrWhiteSpace(layout)) result[name] = layout.Trim();
            }
            return result;
        }

        internal static void Set(string profile, string layout) { Rename(profile, profile, layout); }

        internal static void Rename(string oldProfile, string newProfile, string layout)
        {
            Ensure();
            if (loadError != null) throw new InvalidOperationException("Keyboard-layout assignments were not changed because their saved file could not be read. Open the data folder and repair or restore keyboard-layout-profiles.json before trying again.", loadError);
            var next = WithProfile(map, oldProfile, newProfile, layout);
            if (next.Count > MaxProfiles) throw new InvalidOperationException("Keyboard-layout assignments are limited to " + MaxProfiles + " profiles.");
            Save(Path, next, preserveCorruptPrimary);
            map = next;
            preserveCorruptPrimary = false;
            loadWarning = "";
        }

        internal static void Save(string path, IDictionary<string, string> values) { Save(path, values, false); }

        internal static void Save(string path, IDictionary<string, string> values, bool recovered)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A file path is required.", "path");
            var safe = Empty();
            foreach (var pair in values ?? new Dictionary<string, string>())
            {
                if (String.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 128 || pair.Value == null || pair.Value.Length > 128)
                    throw new InvalidDataException("The keyboard-layout assignment contains an invalid profile or layout value.");
                if (pair.Value.Trim().Length > 0) safe[pair.Key.Trim()] = pair.Value.Trim();
            }
            if (safe.Count > MaxProfiles) throw new InvalidDataException("Keyboard-layout assignments are limited to " + MaxProfiles + " profiles.");
            string json = new JavaScriptSerializer { MaxJsonLength = MaxFileBytes, RecursionLimit = 4 }.Serialize(safe) + Environment.NewLine;
            if (StrictUtf8.GetByteCount(json) > MaxFileBytes) throw new InvalidDataException("The keyboard-layout assignment file is too large.");

            string fullPath = System.IO.Path.GetFullPath(path);
            string archivedPrimary = null;
            if (recovered && File.Exists(fullPath))
            {
                archivedPrimary = fullPath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N");
                File.Move(fullPath, archivedPrimary);
            }
            try { AtomicFile.WriteAllText(fullPath, json, StrictUtf8, recovered ? null : ".bak"); }
            catch
            {
                if (archivedPrimary != null && File.Exists(archivedPrimary) && !File.Exists(fullPath)) File.Move(archivedPrimary, fullPath);
                throw;
            }
        }

        internal static string CurrentLayoutId() { string snapshot = KeyboardLayoutDrift.Snapshot(); int split = snapshot.IndexOf('|'); return split < 0 ? snapshot : snapshot.Substring(0, split); }
        internal static KeyWeaveProfile Match(ProfileCollection profiles) { string current = CurrentLayoutId(); return (profiles == null ? new KeyWeaveProfile[0] : profiles.Profiles).FirstOrDefault(p => String.Equals(ForProfile(p.Name), current, StringComparison.OrdinalIgnoreCase)); }
    }
}
