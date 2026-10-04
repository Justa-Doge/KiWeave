using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FunctionRowRemapper
{
    internal static class PowerToysDrift
    {
        internal static string StatePath { get { return Path.Combine(AppStorage.DataFolder, "powertoys-observed.sha256"); } }
        internal static string Fingerprint(IEnumerable<PowerToysShortcut> shortcuts)
        {
            string text = String.Join("\n", (shortcuts ?? Enumerable.Empty<PowerToysShortcut>()).Select(x => (x.Module ?? "") + "|" + (x.Action ?? "") + "|" + (x.Chord ?? "") + "|" + x.ModuleEnabled).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }
        internal static string Compare(string previous, string current)
        {
            if (String.IsNullOrWhiteSpace(previous)) return "Baseline recorded";
            return String.Equals(previous, current, StringComparison.OrdinalIgnoreCase) ? "No changes since last check" : "Changed since last check";
        }
        internal static string Observe(IEnumerable<PowerToysShortcut> shortcuts)
        {
            string current = Fingerprint(shortcuts), previous = "";
            try { if (File.Exists(StatePath)) previous = File.ReadAllText(StatePath, Encoding.ASCII).Trim(); } catch { }
            try { Directory.CreateDirectory(Path.GetDirectoryName(StatePath)); string temp = StatePath + ".tmp"; File.WriteAllText(temp, current, Encoding.ASCII); File.Copy(temp, StatePath, true); File.Delete(temp); } catch { }
            return Compare(previous, current);
        }
    }
}
