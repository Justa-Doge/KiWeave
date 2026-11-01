using System;

namespace FunctionRowRemapper
{
    // UI-neutral action choice shared by the searchable library, sequence builder,
    // and both shortcut editors.
    internal sealed class ActionPickerChoice
    {
        internal readonly string Label;
        internal readonly Mapping Mapping;
        internal readonly string Category, Context;
        internal string SearchText { get { return Label + " " + Category + " " + Context + " " + Mapping.Target + " " + Mapping.Arguments; } }

        internal ActionPickerChoice(string label, Mapping mapping, string category = "General", string context = "")
        { Label = label; Mapping = mapping; Category = category; Context = context; }

        public override string ToString() { return Label; }
        internal bool Matches(Mapping other)
        {
            if (other == null || Mapping.Kind != other.Kind) return false;
            if (Mapping.Kind == ActionKind.Monitor)
                return String.IsNullOrWhiteSpace(Mapping.MonitorControl) || DdcOperation.SameControl(Mapping.MonitorControl, other.MonitorControl);
            return String.Equals(Mapping.Target ?? "", other.Target ?? "", StringComparison.OrdinalIgnoreCase)
                && String.Equals(Mapping.Arguments ?? "", other.Arguments ?? "", StringComparison.Ordinal)
                && String.Equals(Mapping.WorkingDirectory ?? "", other.WorkingDirectory ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }
}
