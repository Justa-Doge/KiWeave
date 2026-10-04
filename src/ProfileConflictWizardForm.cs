using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class ProfileConflictWizardForm : Form
    {
        internal ProfileCollection Result { get; private set; }
        readonly ProfileCollection current, incoming;
        readonly ListBox list = new ListBox();
        readonly ComboBox decision = new ComboBox();
        readonly Label detail = UiStyle.Text("", 9, false);
        readonly string[] choices = { "Replace existing", "Import as new", "Skip" };
        readonly System.Collections.Generic.Dictionary<string, int> decisions = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        bool loading;
        internal ProfileConflictWizardForm(ProfileCollection existing, ProfileCollection imported)
        {
            current = (existing ?? new ProfileCollection()).Copy(); incoming = (imported ?? new ProfileCollection()).Copy();
            Text = "KiWeave profile import"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; ClientSize = new Size(760, 540); MinimumSize = new Size(680, 480); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, RowCount = 2 }; root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62)); Controls.Add(root);
            var left = UiStyle.Stack(); left.Controls.Add(UiStyle.Text("Choose each profile", 20, true)); left.Controls.Add(UiStyle.Text("Conflicts are shown individually. Non-conflicting profiles are selected automatically.", 9, false)); list.Dock = DockStyle.Fill; list.Margin = new Padding(0, 14, 14, 0); list.BackColor = UiStyle.Surface; list.ForeColor = UiStyle.Ink; list.BorderStyle = BorderStyle.None; list.SelectedIndexChanged += delegate { LoadSelected(); }; left.Controls.Add(list); root.Controls.Add(left, 0, 0);
            var right = UiStyle.Stack(); right.Controls.Add(UiStyle.Text("Profile decision", 18, true)); right.Controls.Add(detail); decision.DropDownStyle = ComboBoxStyle.DropDownList; decision.Items.AddRange(choices); decision.SelectedIndexChanged += delegate { SaveDecision(); }; right.Controls.Add(UiStyle.Field("For the selected profile", decision)); right.Controls.Add(UiStyle.Text("Replace updates the matching local profile. Import as new keeps both with a generated name. Skip leaves the local profile unchanged. KiWeave validates inheritance and duplicate app ownership before accepting the result.", 9, false)); root.Controls.Add(right, 1, 0);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 10, 0, 0) }; buttons.Controls.Add(UiStyle.Button("Apply profile choices", delegate { Finish(); }, true)); buttons.Controls.Add(UiStyle.Button("Cancel", delegate { DialogResult = DialogResult.Cancel; Close(); })); root.Controls.Add(buttons, 1, 1);
            foreach (var profile in incoming.Profiles) { decisions[profile.Name] = current.Find(profile.Name) == null ? -1 : 0; list.Items.Add(profile); }
            if (list.Items.Count > 0) list.SelectedIndex = 0; else decision.Enabled = false;
        }
        void LoadSelected()
        {
            loading = true; var profile = list.SelectedItem as KeyWeaveProfile; if (profile == null) { decision.SelectedIndex = -1; detail.Text = ""; } else { int choice = decisions[profile.Name]; decision.SelectedIndex = choice < 0 ? 1 : choice; detail.Text = (current.Find(profile.Name) == null ? "No local profile has this name." : "A local profile with this name already exists.") + "\r\n\r\n" + profile.Name + " · " + profile.Applications.Length + " automatic app match(es)."; } loading = false;
        }
        void SaveDecision() { if (loading || list.SelectedItem == null) return; int choice = decision.SelectedIndex; decisions[((KeyWeaveProfile)list.SelectedItem).Name] = choice; }
        void Finish()
        {
            try {
                var result = current.Copy();
                foreach (var profile in incoming.Profiles) {
                    int choice = decisions[profile.Name]; if (choice < 0) choice = 1;
                    var existing = result.Find(profile.Name);
                    if (existing != null && choice == 2) continue;
                    if (existing != null && choice == 0) { result.Profiles = result.Profiles.Where(p => !String.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase)).ToArray(); result.Profiles = result.Profiles.Concat(new[] { profile.Copy() }).ToArray(); }
                    else if (existing == null) result.Profiles = result.Profiles.Concat(new[] { profile.Copy() }).ToArray();
                    else result = ProfileCollection.Merge(result, new ProfileCollection { Profiles = new[] { profile.Copy() } });
                }
                ProfileStore.Validate(result, false); Result = result; DialogResult = DialogResult.OK; Close();
            } catch (Exception ex) { MessageBox.Show(this, "These profile choices cannot be applied safely yet.\r\n\r\n" + ex.Message, "Profile import", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}
