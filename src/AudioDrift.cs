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
            string current = Fingerprint(snapshot), previous = "";
            try { if (File.Exists(StatePath)) previous = File.ReadAllText(StatePath, Encoding.ASCII).Trim(); } catch { }
            try { Directory.CreateDirectory(Path.GetDirectoryName(StatePath)); string temp = StatePath + ".tmp"; File.WriteAllText(temp, current, Encoding.ASCII); File.Copy(temp, StatePath, true); File.Delete(temp); } catch { }
            return Compare(previous, current);
        }
    }
}
