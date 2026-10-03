using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class ConditionalActionForm : Form
    {
        readonly ComboBox condition = new DesignComboBox();
        readonly TextBox application = new TextBox();
        readonly Label matched = UiStyle.Text("Do nothing", 10, false), fallback = UiStyle.Text("Do nothing", 10, false);
        Mapping whenMatched = new Mapping { Kind = ActionKind.Unbound }, otherwise = new Mapping { Kind = ActionKind.Unbound };
        internal Mapping Result { get; private set; }

        internal ConditionalActionForm(Mapping existing)
        {
            int initialCondition = 0;
            Text = "Conditional action"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            ClientSize = new Size(720, 760); MinimumSize = new Size(650, 800); StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi; Design.DarkTitlebar(this);
            if (existing != null && existing.Kind == ActionKind.Conditional) try { var r = ConditionalCodec.Parse(existing.Target); initialCondition = (int)r.Condition; application.Text = r.Application; whenMatched = r.WhenMatched.Copy(); otherwise = r.Otherwise.Copy(); } catch { }
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 1, RowCount = 5 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 220)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62)); Controls.Add(root);
            var head = UiStyle.Stack(); head.Controls.Add(UiStyle.Text("Choose what happens, and when.", 22, true)); head.Controls.Add(UiStyle.Text("KiWeave checks this rule only when its assigned key or hotkey is pressed.", 10, false)); root.Controls.Add(head, 0, 0);
            var rule = new DesignCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14) }; var ruleStack = UiStyle.Stack(); rule.Controls.Add(ruleStack); ruleStack.Controls.Add(UiStyle.Text("Condition", 13, true));
            UiStyle.Combo(condition); condition.Items.AddRange(new[] { "Foreground application is", "Application is running" }); condition.SelectedIndex = initialCondition; ruleStack.Controls.Add(UiStyle.Field("Check", condition));
            var browseRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 }; browseRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); browseRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            browseRow.Controls.Add(new InputFrame(application), 0, 0); var browse = UiStyle.Button("Browse...", delegate { using (var d = new OpenFileDialog { Filter = "Applications|*.exe" }) if (d.ShowDialog(this) == DialogResult.OK) application.Text = Path.GetFileName(d.FileName); }); browse.Margin = new Padding(8, 0, 0, 0); browseRow.Controls.Add(browse, 1, 0); ruleStack.Controls.Add(UiStyle.Field("Application name · e.g. Discord.exe", browseRow)); root.Controls.Add(rule, 0, 1);
            root.Controls.Add(OutcomeCard("When it matches", matched, delegate { ChooseOutcome(true); }, false), 0, 2);
            root.Controls.Add(OutcomeCard("Otherwise", fallback, delegate { ChooseOutcome(false); }, true), 0, 3);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 12, 0, 0) };
            var save = UiStyle.Button("Use condition", delegate { SaveRule(); }, true); var cancel = UiStyle.Button("Cancel", delegate { DialogResult = DialogResult.Cancel; Close(); }); buttons.Controls.Add(save); buttons.Controls.Add(cancel); root.Controls.Add(buttons, 0, 4); AcceptButton = save; CancelButton = cancel; UpdateSummaries();
        }
        Control OutcomeCard(string title, Label summary, EventHandler choose, bool allowNone)
        {
            var card = new DesignCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14) }; var stack = UiStyle.Stack(); card.Controls.Add(stack); stack.Controls.Add(UiStyle.Text(title, 13, true)); summary.Margin = new Padding(0, 2, 0, 12); stack.Controls.Add(summary);
            var row = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false }; row.Controls.Add(UiStyle.Button("Choose action", choose));
            if (allowNone) row.Controls.Add(UiStyle.Button("Do nothing", delegate { otherwise = new Mapping { Kind = ActionKind.Unbound }; UpdateSummaries(); })); stack.Controls.Add(row); return card;
        }
        void ChooseOutcome(bool primary)
        {
            using (var picker = new ActionPickerForm(false, false)) {
                if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedAction == null) return; Mapping action = picker.SelectedAction.Copy();
                if (String.IsNullOrEmpty(action.Target) && action.Kind != ActionKind.LockThenSleep) using (var details = new StepDetailsForm(action)) { if (details.ShowDialog(this) != DialogResult.OK) return; action = details.Result; }
                if (primary) whenMatched = action; else otherwise = action; UpdateSummaries();
            }
        }
        void UpdateSummaries() { matched.Text = whenMatched.Kind == ActionKind.Unbound ? "Choose what runs when the condition matches." : whenMatched.Summary; fallback.Text = otherwise.Kind == ActionKind.Unbound ? "Do nothing" : otherwise.Summary; }
        void SaveRule()
        {
            try { var rule = new ConditionalRule { Condition = (ConditionKind)condition.SelectedIndex, Application = application.Text.Trim(), WhenMatched = whenMatched.Copy(), Otherwise = otherwise.Copy() }; Result = new Mapping { Kind = ActionKind.Conditional, Target = ConditionalCodec.Serialize(rule) }; ConfigStore.Validate(Result, false); DialogResult = DialogResult.OK; Close(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Check this condition", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }
    }
}
