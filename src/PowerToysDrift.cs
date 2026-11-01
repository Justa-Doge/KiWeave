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
            return Observe(StatePath, shortcuts);
        }
        internal static string Observe(string path, IEnumerable<PowerToysShortcut> shortcuts)
        {
            return LocalObservationStore.CompareAndSave(path, Fingerprint(shortcuts), Compare);
        }
    }
}
