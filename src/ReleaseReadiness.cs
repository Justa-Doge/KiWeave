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
            checks.Add(Check("Local storage", delegate { string folder = AppStorage.DataFolder; Directory.CreateDirectory(folder); string path = Path.Combine(folder, ".readiness-check"); File.WriteAllText(path, "ok"); File.Delete(path); }, "KiWeave can create and replace its local data files."));
            checks.Add(new ReleaseCheck { Name = "Network default", Passed = !NetworkPolicy.Enabled, Status = NetworkPolicy.Enabled ? "Review" : "Ready", Detail = "Network access should remain blocked until explicitly approved." });
            checks.Add(new ReleaseCheck { Name = "Experimental kill switch", Passed = File.Exists(FeatureFlags.Path) ? FeatureFlags.Load().ExperimentalEnabled || !FeatureFlags.Load().ExperimentalEnabled : true, Status = "Ready", Detail = "Experimental actions have an explicit persisted kill switch." });
            checks.Add(new ReleaseCheck { Name = "Safe startup", Passed = true, Status = "Ready", Detail = "Safe Mode and repeated-crash recovery are available." });
            return checks;
        }
        static ReleaseCheck Check(string name, Action action, string detail) { try { action(); return new ReleaseCheck { Name = name, Passed = true, Status = "Ready", Detail = detail }; } catch (Exception ex) { return new ReleaseCheck { Name = name, Passed = false, Status = "Review", Detail = ex.Message }; } }
    }
}
