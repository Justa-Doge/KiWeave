using System;
using System.Text;

namespace FunctionRowRemapper
{
    internal static class ShortcutExplanation
    {
        internal static string Build(string shortcut, Mapping mapping, string context)
        {
            if (mapping == null) return shortcut + Environment.NewLine + "No action is configured.";
            var text = new StringBuilder();
            text.AppendLine("Explain " + shortcut);
            if (!String.IsNullOrWhiteSpace(context)) text.AppendLine(context);
            text.AppendLine();
            text.AppendLine("Action: " + Summary(mapping));
            string maturity = ActionInsights.Maturity(mapping);
            text.AppendLine("Maturity: " + maturity);
            text.AppendLine(ActionInsights.MaturityExplanation(maturity));
            text.AppendLine("Dependencies: " + ActionInsights.Dependencies(mapping));
            text.AppendLine("Permission: " + ActionPrivacy.Risk(mapping));
            if (mapping.Kind == ActionKind.HttpRequest) text.AppendLine("Network policy: checked when the action is triggered.");
            if (mapping.Kind == ActionKind.Sequence)
            {
                try { text.AppendLine("Safety: " + ActionInsights.Sequence(SequenceCodec.Parse(mapping.Target)).Compact); }
                catch { text.AppendLine("Safety: sequence details could not be decoded safely."); }
            }
            return text.ToString().TrimEnd();
        }

        static string Summary(Mapping mapping)
        {
            try { return mapping.Summary; }
            catch { return "Choose action details"; }
        }
    }
}
