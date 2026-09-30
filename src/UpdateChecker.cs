using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FunctionRowRemapper
{
    internal static class UpdateChecker
    {
        // Bump this when publishing a matching vX.Y.Z tag for an installed build.
        internal const string CurrentVersion = "0.1.3";
        internal const string Repository = "https://github.com/Justa-Doge/KeyWeave.git";
        static readonly Regex TagPattern = new Regex(@"^v(\d{1,5})\.(\d{1,5})\.(\d{1,5})$", RegexOptions.CultureInvariant);

        internal static string NewestUpdate(string lsRemoteOutput, string installedVersion)
        {
            Version installed;
            if (!Version.TryParse(installedVersion, out installed)) return null;
            Version newest = installed;
            string newestTag = null;
            foreach (string line in lsRemoteOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                int separator = line.LastIndexOf("refs/tags/v", StringComparison.Ordinal);
                if (separator < 0) continue;
                string tag = line.Substring(separator + "refs/tags/".Length).Trim();
                var match = TagPattern.Match(tag);
                if (!match.Success) continue;
                Version candidate;
                if (!Version.TryParse(match.Groups[1].Value + "." + match.Groups[2].Value + "." + match.Groups[3].Value, out candidate)) continue;
                if (candidate > newest) { newest = candidate; newestTag = tag; }
            }
            return newestTag;
        }

        internal static void CheckInBackground(Action<string> onUpdate)
        {
            Task.Run(delegate {
                try {
                    string newest = NewestUpdate(ReadRemoteTags(), CurrentVersion);
                    if (newest != null) onUpdate(newest);
                } catch { /* Private repo, offline PC, or Git unavailable: leave startup unaffected. */ }
            });
        }

        static string ReadRemoteTags()
        {
            string bundledGit = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache", "codex-runtimes", "codex-primary-runtime", "dependencies", "native", "git", "cmd", "git.exe");
            string git = File.Exists(bundledGit) ? bundledGit : "git";
            var start = new ProcessStartInfo(git, "-c credential.interactive=never ls-remote --tags --refs " + Repository) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            start.EnvironmentVariables["GCM_INTERACTIVE"] = "never";
            using (var process = new Process { StartInfo = start }) {
                var output = new StringBuilder();
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (output) { if (output.Length < 65536) output.AppendLine(e.Data); } };
                process.ErrorDataReceived += delegate { }; // Do not expose credential-helper output in the UI.
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                if (!process.WaitForExit(8000)) { try { process.Kill(); } catch { } return ""; }
                process.WaitForExit();
                if (process.ExitCode != 0) return "";
                lock (output) return output.ToString();
            }
        }
    }
}
