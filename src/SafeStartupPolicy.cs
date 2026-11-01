using System;

namespace FunctionRowRemapper
{
    // Shared by the WinForms and WPF hosts so recovery behavior cannot drift.
    internal static class SafeStartupPolicy
    {
        internal static bool SafeModeRequested(string[] args, bool shiftHeld)
        {
            string[] values = args ?? new string[0];
            if (Array.Exists(values, value => String.Equals(value, "--normal-mode", StringComparison.OrdinalIgnoreCase))) return false;
            return shiftHeld || Array.Exists(values, value => String.Equals(value, "--safe-mode", StringComparison.OrdinalIgnoreCase));
        }
    }

    internal static class StartupRecoveryPolicy
    {
        internal const int RecoveryThreshold = 2;
        internal static int NextAttempts(int attempts) { return Math.Max(0, Math.Min(RecoveryThreshold + 1, attempts)) + 1; }
        internal static bool ShouldOfferRecovery(int attempts) { return attempts >= RecoveryThreshold; }
    }
}
