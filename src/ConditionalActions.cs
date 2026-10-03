using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    internal enum ConditionKind { ForegroundApplication, ApplicationRunning }

    internal sealed class ConditionalRule
    {
        internal ConditionKind Condition;
        internal string Application = "";
        internal Mapping WhenMatched = new Mapping { Kind = ActionKind.Unbound };
        internal Mapping Otherwise = new Mapping { Kind = ActionKind.Unbound };
        internal string Summary { get { return (Condition == ConditionKind.ForegroundApplication ? "When foreground app is " : "When app is running: ") + Application; } }
    }

    internal static class ConditionalCodec
    {
        static readonly string[] MappingFields = { "action", "target", "arguments", "workingDirectory", "monitorId", "monitorControl", "monitorStep" };
        internal static string Serialize(ConditionalRule rule)
        {
            ValidateShape(rule);
            var serializer = new JavaScriptSerializer();
            return serializer.Serialize(new {
                version = 1,
                condition = rule.Condition.ToString(),
                application = rule.Application.Trim(),
                whenMatched = Shape(rule.WhenMatched),
                otherwise = Shape(rule.Otherwise)
            });
        }
        static object Shape(Mapping m)
        {
            return new { action = m.Kind.ToString(), target = m.Target, arguments = m.Arguments, workingDirectory = m.WorkingDirectory, monitorId = m.MonitorId, monitorControl = m.MonitorControl, monitorStep = m.MonitorStep };
        }
        internal static ConditionalRule Parse(string json)
        {
            try {
                if (String.IsNullOrWhiteSpace(json) || json.Length > 4096) throw new ArgumentException();
                JsonSyntax.Check(json); var root = Object(new JavaScriptSerializer().DeserializeObject(json), new[] { "version", "condition", "application", "whenMatched", "otherwise" });
                if (!(root["version"] is int) || (int)root["version"] != 1) throw new ArgumentException();
                ConditionKind kind; string condition = Text(root["condition"]);
                if (!Enum.TryParse<ConditionKind>(condition, false, out kind) || kind.ToString() != condition) throw new ArgumentException();
                var rule = new ConditionalRule { Condition = kind, Application = Text(root["application"]), WhenMatched = ReadMapping(root["whenMatched"]), Otherwise = ReadMapping(root["otherwise"]) };
                ValidateShape(rule); return rule;
            } catch (ArgumentException) { throw new ArgumentException("The conditional action is invalid."); }
            catch { throw new ArgumentException("The conditional action is invalid."); }
        }
        static Mapping ReadMapping(object value)
        {
            var d = Object(value, MappingFields); string action = Text(d["action"]); ActionKind kind;
            if (!Enum.TryParse<ActionKind>(action, false, out kind) || kind.ToString() != action) throw new ArgumentException();
            if (!(d["monitorStep"] is int)) throw new ArgumentException();
            return new Mapping { Kind = kind, Target = Text(d["target"]), Arguments = Text(d["arguments"]), WorkingDirectory = Text(d["workingDirectory"]), MonitorId = Text(d["monitorId"]), MonitorControl = Text(d["monitorControl"]), MonitorStep = (int)d["monitorStep"] };
        }
        static Dictionary<string, object> Object(object value, string[] fields)
        {
            var d = value as Dictionary<string, object>; if (d == null || d.Count != fields.Length || fields.Any(x => !d.ContainsKey(x))) throw new ArgumentException(); return d;
        }
        static string Text(object value) { var text = value as string; if (text == null) throw new ArgumentException(); return text; }
        static void ValidateShape(ConditionalRule rule)
        {
            if (rule == null || rule.WhenMatched == null || rule.Otherwise == null) throw new ArgumentException("Choose both outcomes.");
            string app = (rule.Application ?? "").Trim();
            if (app.Length < 1 || app.Length > 128 || app.Any(Char.IsControl) || Path.GetFileName(app) != app || !app.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Enter an application name such as Discord.exe.");
            if (rule.WhenMatched.Kind == ActionKind.PassThrough || rule.WhenMatched.Kind == ActionKind.Unbound || rule.WhenMatched.Kind == ActionKind.Conditional) throw new ArgumentException("Choose a real matching action. Conditional actions cannot be nested directly.");
            if (rule.Otherwise.Kind == ActionKind.PassThrough || rule.Otherwise.Kind == ActionKind.Conditional) throw new ArgumentException("Choose a fallback action or Do nothing.");
        }
    }

    internal static class ConditionalActions
    {
        internal static bool Matches(ConditionalRule rule)
        {
            string wanted = Normalize(rule.Application);
            if (rule.Condition == ConditionKind.ForegroundApplication) return String.Equals(Normalize(Native.ForegroundProcessName()), wanted, StringComparison.OrdinalIgnoreCase);
            try {
                Process[] matches = Process.GetProcessesByName(wanted);
                try { return matches.Length > 0; }
                finally { foreach (Process process in matches) process.Dispose(); }
            } catch { return false; }
        }
        internal static bool Matches(ConditionalRule rule, string foreground, Func<string, bool> running)
        {
            string wanted = Normalize(rule.Application);
            return rule.Condition == ConditionKind.ForegroundApplication ? String.Equals(Normalize(foreground), wanted, StringComparison.OrdinalIgnoreCase) : running != null && running(wanted);
        }
        static string Normalize(string value) { string name = (value ?? "").Trim(); return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name; }
    }
}
