using System;
using System.Collections.Generic;

namespace FunctionRowRemapper
{
    internal enum BackupPrivacyPreset { Standard, Strict, MetadataOnly }
    internal static class BackupPrivacy
    {
        internal static string Review(string json) { return Review(json, BackupPrivacyPreset.Standard); }
        internal static string Review(string json, BackupPrivacyPreset preset)
        {
            var findings = new List<string>(); string value = json ?? "";
            if (preset == BackupPrivacyPreset.MetadataOnly) return "Metadata-only review selected. Values and field contents are not displayed; keep the resulting backup private and share only a redacted support bundle.";
            if (value.IndexOf("http://", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("https://", StringComparison.OrdinalIgnoreCase) >= 0) findings.Add("network URLs");
            if (value.IndexOf("\\\\", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf(":\\", StringComparison.OrdinalIgnoreCase) >= 0) findings.Add("local file paths");
            if (value.IndexOf("\"command\"", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("\"arguments\"", StringComparison.OrdinalIgnoreCase) >= 0) findings.Add("commands or arguments");
            if (value.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("webhook", StringComparison.OrdinalIgnoreCase) >= 0) findings.Add("credential-like text");
            if (findings.Count == 0) return (preset == BackupPrivacyPreset.Strict ? "Strict review found no obvious private categories." : "No obvious URLs, paths, commands, or credential-like field names were detected.");
            return (preset == BackupPrivacyPreset.Strict ? "Strict privacy review found: " : "This backup may contain: ") + String.Join(", ", findings) + ".\r\nValues are intentionally hidden from this review. Keep the backup private.";
        }
    }
}
