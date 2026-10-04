using System;

namespace FunctionRowRemapper
{
    internal static class ActionExplanation
    {
        static readonly object Gate = new object();
        static string latest = "No mapped action has run in this session.";
        internal static string Latest { get { lock (Gate) return latest; } }
        internal static void Record(Mapping mapping, string source)
        {
            if (mapping == null) return;
            string detail = mapping.Kind == ActionKind.SendKey || mapping.Kind == ActionKind.SendShortcut || mapping.Kind == ActionKind.Media ? mapping.Summary : mapping.Kind.ToString();
            lock (Gate) latest = "Source: " + (String.IsNullOrEmpty(source) ? "mapped input" : source) + "\r\nAction: " + detail + "\r\nRecorded: " + DateTime.Now.ToString("g") + "\r\n\r\nOnly this latest explanation is retained in memory. No key history is stored.";
        }
    }
}
