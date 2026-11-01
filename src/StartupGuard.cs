using System;
using System.IO;
using System.Text;

namespace FunctionRowRemapper
{
    internal static class StartupGuard
    {
        internal const int RecoveryThreshold = StartupRecoveryPolicy.RecoveryThreshold;
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "startup-state.txt"); } }
        internal static int NextAttempts(int attempts) { return StartupRecoveryPolicy.NextAttempts(attempts); }
        internal static bool ShouldOfferRecovery(int attempts) { return StartupRecoveryPolicy.ShouldOfferRecovery(attempts); }
        internal static bool RecordStart()
        {
            int attempts = 0; try { if (File.Exists(Path)) Int32.TryParse(AtomicFile.ReadAllTextBounded(Path, 16, Encoding.ASCII).Trim(), out attempts); } catch { }
            attempts = NextAttempts(attempts); try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)); File.WriteAllText(Path, attempts.ToString(), Encoding.ASCII); } catch { }
            return ShouldOfferRecovery(attempts);
        }
        internal static void MarkClean() { try { if (File.Exists(Path)) File.Delete(Path); } catch { } }
    }
}
