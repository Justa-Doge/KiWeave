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
        internal const string CurrentVersion = "1.0.0-beta.4";
        internal const int CheckIntervalMilliseconds = 12 * 60 * 60 * 1000;
        internal const string ReleasesApi = "https://api.github.com/repos/Justa-Doge/KeyWeave/releases";
        static readonly Regex TagPattern = new Regex(@"^v?(\d{1,5})\.(\d{1,5})\.(\d{1,5})(?:-(alpha|beta)\.(\d{1,5}))?$", RegexOptions.CultureInvariant);

        internal static string NewestUpdate(string lsRemoteOutput, string installedVersion)
        { return NewestUpdate(lsRemoteOutput, installedVersion, "Stable"); }
        internal static string NewestUpdate(string lsRemoteOutput, string installedVersion, string channel)
        {
            if (Array.IndexOf(UpdateChannels.Names, channel) < 0) channel = "Stable";
            ReleaseVersion installed = ReleaseVersion.Parse(installedVersion); if (installed == null) return null;
            ReleaseVersion newest = installed;
            string newestTag = null;
            foreach (string line in lsRemoteOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                int separator = line.LastIndexOf("refs/tags/v", StringComparison.Ordinal);
                if (separator < 0) continue;
                string tag = line.Substring(separator + "refs/tags/".Length).Trim();
                var match = TagPattern.Match(tag);
                if (!match.Success) continue;
                ReleaseVersion candidate = ReleaseVersion.Parse(tag); if (candidate == null || !candidate.Allowed(channel)) continue;
                if (candidate.CompareTo(newest) > 0) { newest = candidate; newestTag = tag; }
            }
            return newestTag;
        }

        internal static void CheckInBackground(Action<string> onUpdate)
        { CheckInBackground(onUpdate, "Stable"); }
        internal static void CheckInBackground(Action<string> onUpdate, string channel)
        {
            if (!NetworkPolicy.Enabled) return;
            Task.Run(delegate {
                try {
                    string newest = NewestUpdate(ReadReleaseTags(channel), CurrentVersion, channel);
                    if (newest != null) onUpdate(newest);
                } catch { /* Offline PC, unavailable repository, or missing Git: leave startup unaffected. */ }
            });
        }

        internal static string TagsFromReleaseJson(string json)
        { return TagsFromReleaseJson(json, "Stable"); }
        internal static string TagsFromReleaseJson(string json, string channel)
        {
            if (json == null || json.Length > 262144) return "";
            var releases = new JavaScriptSerializer { MaxJsonLength = 262144, RecursionLimit = 16 }.DeserializeObject(json) as object[]; if (releases == null) return "";
            var output = new StringBuilder();
            foreach (object item in releases) {
                var release = item as Dictionary<string, object>; object tag, draft, prerelease;
                if (release == null || !release.TryGetValue("tag_name", out tag) || !(tag is string)) continue;
                if (release.TryGetValue("draft", out draft) && draft is bool && (bool)draft) continue;
                string tagName = (string)tag; ReleaseVersion version = ReleaseVersion.Parse(tagName);
                if (version == null || !version.Allowed(channel)) continue;
                if (release.TryGetValue("prerelease", out prerelease) && prerelease is bool && (bool)prerelease && channel == "Stable") continue;
                output.Append("release refs/tags/").Append(tagName).AppendLine();
            }
            return output.ToString();
        }

        internal static void CheckNow(Action<string, Exception> completed)
        { CheckNow(completed, "Stable"); }
        internal static void CheckNow(Action<string, Exception> completed, string channel)
        {
            if (!NetworkPolicy.Enabled) { completed(null, new InvalidOperationException("Network access is turned off in KiWeave Settings.")); return; }
            Task.Run(delegate {
                try { completed(NewestUpdate(ReadReleaseTags(channel), CurrentVersion, channel), null); }
                catch (Exception ex) { completed(null, ex); }
            });
        }
        static string ReadReleaseTags(string channel)
        {
            var request = (HttpWebRequest)WebRequest.Create(ReleasesApi + "?per_page=30"); request.Method = "GET"; request.UserAgent = "KiWeave/" + CurrentVersion;
            request.Accept = "application/vnd.github+json"; request.Timeout = 8000; request.ReadWriteTimeout = 8000;
            using (var response = (HttpWebResponse)request.GetResponse()) using (var stream = response.GetResponseStream()) using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                string json = reader.ReadToEnd(); return TagsFromReleaseJson(json, channel);
            }
        }
        static string ReadReleaseTags() { return ReadReleaseTags("Stable"); }
        internal sealed class ReleaseVersion : IComparable<ReleaseVersion>
        {
            internal Version Base; internal string Pre; internal int PreNumber;
            internal static ReleaseVersion Parse(string value)
            {
                var m = TagPattern.Match((value ?? "").Trim()); if (!m.Success) { Version fallback; if (!Version.TryParse((value ?? "").Split('-')[0], out fallback)) return null; return new ReleaseVersion { Base = fallback.Build >= 0 ? new Version(fallback.Major, fallback.Minor, fallback.Build) : fallback, Pre = "" }; }
                return new ReleaseVersion { Base = new Version(m.Groups[1].Value + "." + m.Groups[2].Value + "." + m.Groups[3].Value), Pre = m.Groups[4].Value, PreNumber = m.Groups[5].Success ? Int32.Parse(m.Groups[5].Value) : 0 };
            }
            internal bool Allowed(string channel) { return channel == "Alpha" || channel == "Beta" && (Pre == "beta" || String.IsNullOrEmpty(Pre)) || channel == "Stable" && String.IsNullOrEmpty(Pre); }
            public int CompareTo(ReleaseVersion other) { int c = Base.CompareTo(other.Base); if (c != 0) return c; if (String.IsNullOrEmpty(Pre) && !String.IsNullOrEmpty(other.Pre)) return 1; if (!String.IsNullOrEmpty(Pre) && String.IsNullOrEmpty(other.Pre)) return -1; c = String.Compare(Pre, other.Pre, StringComparison.Ordinal); return c != 0 ? c : PreNumber.CompareTo(other.PreNumber); }
        }
    }
}
