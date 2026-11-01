using System;
using System.Collections.Generic;
using System.IO;

namespace FunctionRowRemapper
{
    internal sealed class ReleaseCheck { internal string Name, Status, Detail; internal bool Passed; }
    internal static class ReleaseReadiness
    {
        internal static List<ReleaseCheck> Run(Configuration configuration, ProfileCollection profiles)
        {
            var checks = new List<ReleaseCheck>();
            checks.Add(Check("Configuration schema", delegate { ConfigStore.Validate(configuration ?? new Configuration(), false); }, "Saved mappings validate without executing targets."));
            checks.Add(Check("Profile schema", delegate { ProfileStore.Validate(profiles ?? new ProfileCollection(), false); }, "Profiles, inheritance, and app ownership validate locally."));
            checks.Add(Check("Local storage", delegate { CheckLocalStorage(AppStorage.DataFolder); }, "KiWeave can safely create and remove a unique temporary file in its data folder."));
            checks.Add(new ReleaseCheck { Name = "Network default", Passed = !NetworkPolicy.Enabled, Status = NetworkPolicy.Enabled ? "Review" : "Ready", Detail = "Network access should remain blocked until explicitly approved." });
            checks.Add(Check("Experimental kill switch", delegate {
                var stable = new Mapping { Kind = ActionKind.SendKey, Target = "A" };
                var experimental = new Mapping { Kind = ActionKind.Command, Target = Path.Combine(Environment.SystemDirectory, "cmd.exe") };
                if (!FeatureFlags.Allows(stable, false) || FeatureFlags.Allows(experimental, false) || !FeatureFlags.Allows(experimental, true))
                    throw new InvalidOperationException("The dispatcher kill-switch contract did not hold.");
            }, "The runtime gate allows stable actions and blocks experimental actions when disabled."));
            checks.Add(Check("Safe startup", delegate {
                if (!SafeStartupPolicy.SafeModeRequested(new[] { "--safe-mode" }, false)) throw new InvalidOperationException("The explicit Safe Mode launch switch is not recognized.");
                if (SafeStartupPolicy.SafeModeRequested(new[] { "--normal-mode" }, true)) throw new InvalidOperationException("The normal restart can be captured by the held Shift key.");
                if (StartupRecoveryPolicy.ShouldOfferRecovery(StartupRecoveryPolicy.RecoveryThreshold - 1) || !StartupRecoveryPolicy.ShouldOfferRecovery(StartupRecoveryPolicy.RecoveryThreshold))
                    throw new InvalidOperationException("Repeated-crash recovery threshold is inconsistent.");
            }, "Safe Mode launch, deliberate normal restart, and repeated-crash recovery thresholds are wired."));
            return checks;
        }
        internal static void CheckLocalStorage(string folder)
        {
            if (String.IsNullOrWhiteSpace(folder)) throw new ArgumentException("The data folder is unavailable.");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, ".readiness-check-" + Guid.NewGuid().ToString("N"));
            bool created = false;
            try {
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                    created = true;
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes("KiWeave readiness probe");
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
            } finally { if (created && File.Exists(path)) File.Delete(path); }
        }
        internal static ReleaseCheck Check(string name, Action action, string detail)
        {
            try { action(); return new ReleaseCheck { Name = name, Passed = true, Status = "Ready", Detail = detail }; }
            catch { return new ReleaseCheck { Name = name, Passed = false, Status = "Review", Detail = "This check could not finish; review local settings and permissions, then retry." }; }
        }
    }
}
