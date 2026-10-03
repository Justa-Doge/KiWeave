using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    internal static class UpdateChecker
    {
        // Bump this when publishing a matching vX.Y.Z tag for an installed build.
        internal const string CurrentVersion = "1.0.0-beta.1";
        internal const string ReleasesApi = "https://api.github.com/repos/Justa-Doge/KeyWeave/releases";
        static readonly Regex TagPattern = new Regex(@"^v(\d{1,5})\.(\d{1,5})\.(\d{1,5})$", RegexOptions.CultureInvariant);

        internal static string NewestUpdate(string lsRemoteOutput, string installedVersion)
        {
            bool installedPrerelease = installedVersion != null && installedVersion.IndexOf('-') >= 0;
            Version installed;
            if (!Version.TryParse((installedVersion ?? "").Split('-')[0], out installed)) return null;
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
                if (candidate > newest || (installedPrerelease && candidate == installed)) { newest = candidate; newestTag = tag; installedPrerelease = false; }
            }
            return newestTag;
        }

        internal static void CheckInBackground(Action<string> onUpdate)
        {
            if (!NetworkPolicy.Enabled) return;
            Task.Run(delegate {
                try {
                    string newest = NewestUpdate(ReadReleaseTags(), CurrentVersion);
                    if (newest != null) onUpdate(newest);
                } catch { /* Offline PC, unavailable repository, or missing Git: leave startup unaffected. */ }
            });
        }

        internal static string TagsFromReleaseJson(string json)
        {
            if (json == null || json.Length > 262144) return "";
            var releases = new JavaScriptSerializer { MaxJsonLength = 262144, RecursionLimit = 16 }.DeserializeObject(json) as object[]; if (releases == null) return "";
            var output = new StringBuilder();
            foreach (object item in releases) {
                var release = item as Dictionary<string, object>; object tag, draft, prerelease;
                if (release == null || !release.TryGetValue("tag_name", out tag) || !(tag is string)) continue;
                if (release.TryGetValue("draft", out draft) && draft is bool && (bool)draft) continue;
                if (release.TryGetValue("prerelease", out prerelease) && prerelease is bool && (bool)prerelease) continue;
                output.Append("release refs/tags/").Append((string)tag).AppendLine();
            }
            return output.ToString();
        }

        internal static void CheckNow(Action<string, Exception> completed)
        {
            if (!NetworkPolicy.Enabled) { completed(null, new InvalidOperationException("Network access is turned off in KiWeave Settings.")); return; }
            Task.Run(delegate {
                try { completed(NewestUpdate(ReadReleaseTags(), CurrentVersion), null); }
                catch (Exception ex) { completed(null, ex); }
            });
        }
        static string ReadReleaseTags()
        {
            var request = (HttpWebRequest)WebRequest.Create(ReleasesApi + "?per_page=30"); request.Method = "GET"; request.UserAgent = "KiWeave/" + CurrentVersion;
            request.Accept = "application/vnd.github+json"; request.Timeout = 8000; request.ReadWriteTimeout = 8000;
            using (var response = (HttpWebResponse)request.GetResponse()) using (var stream = response.GetResponseStream()) using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                string json = reader.ReadToEnd(); return TagsFromReleaseJson(json);
            }
        }
    }
}
