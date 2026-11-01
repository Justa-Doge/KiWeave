using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FunctionRowRemapper
{
    internal static class AudioDrift
    {
        internal static string StatePath { get { return Path.Combine(AppStorage.DataFolder, "audio-observed.sha256"); } }
        internal static string Fingerprint(string snapshot)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(snapshot ?? ""))).Replace("-", "");
        }
        internal static string Compare(string previous, string current)
        {
            if (String.IsNullOrWhiteSpace(previous)) return "Baseline recorded";
            return String.Equals(previous, current, StringComparison.OrdinalIgnoreCase) ? "No changes since last check" : "Changed since last check";
        }
        internal static string Observe(string snapshot)
        {
            return Observe(StatePath, snapshot);
        }
        internal static string Observe(string path, string snapshot)
        {
            return LocalObservationStore.CompareAndSave(path, Fingerprint(snapshot), Compare);
        }
    }
}
