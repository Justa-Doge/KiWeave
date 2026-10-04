using System;
using System.IO;
using System.Text;

namespace FunctionRowRemapper
{
    internal static class StartupGuard
    {
        internal const int RecoveryThreshold = 2;
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "startup-state.txt"); } }
        internal static int NextAttempts(int attempts) { return Math.Max(0, Math.Min(RecoveryThreshold + 1, attempts)) + 1; }
        internal static bool ShouldOfferRecovery(int attempts) { return attempts >= RecoveryThreshold; }
        internal static bool RecordStart()
        {
            int attempts = 0; try { if (File.Exists(Path)) Int32.TryParse(File.ReadAllText(Path, Encoding.ASCII).Trim(), out attempts); } catch { }
            attempts = NextAttempts(attempts); try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)); File.WriteAllText(Path, attempts.ToString(), Encoding.ASCII); } catch { }
            return ShouldOfferRecovery(attempts);
        }
        internal static void MarkClean() { try { if (File.Exists(Path)) File.Delete(Path); } catch { } }
    }
}
