using System;
using System.Collections.Generic;

namespace FunctionRowRemapper
{
    internal static class BackupPrivacy
    {
        internal static string Review(string json)
        {
            var findings = new List<string>(); string value = json ?? "";
            if (value.IndexOf("http://", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("https://", StringComparison.OrdinalIgnoreCase) >= 0) findings.Add("network URLs");
            if (value.IndexOf("\\\\", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf(":\\", StringComparison.OrdinalIgnoreCase) >= 0) findings.Add("local file paths");
            if (value.IndexOf("\"command\"", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("\"arguments\"", StringComparison.OrdinalIgnoreCase) >= 0) findings.Add("commands or arguments");
            if (value.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("webhook", StringComparison.OrdinalIgnoreCase) >= 0) findings.Add("credential-like text");
            if (findings.Count == 0) return "No obvious URLs, paths, commands, or credential-like field names were detected.";
            return "This backup may contain: " + String.Join(", ", findings) + ".\r\nValues are intentionally hidden from this review. Keep the backup private.";
        }
    }
}
