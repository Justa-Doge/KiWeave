using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class ProfileManagerForm : Form
    {
        readonly ProfileCollection profiles;
        readonly Configuration current, defaultConfiguration;
        readonly ListBox list = new DesignListBox();
        readonly TextBox name = new DesignTextBox(), applications = new DesignTextBox();
        readonly TextBox accent = new DesignTextBox(), icon = new DesignTextBox();
        readonly TextBox layoutId = new DesignTextBox();
        readonly DesignComboBox inheritance = new DesignComboBox();
        readonly Label inheritanceTree = UiStyle.Text("", 9, false);
        readonly Label feedback = new Label();
        public KeyWeaveProfile SelectedProfile { get; private set; }

        public ProfileManagerForm(ProfileCollection source, Configuration currentConfiguration) : this(source, currentConfiguration, currentConfiguration) { }
        internal ProfileManagerForm(ProfileCollection source, Configuration currentConfiguration, Configuration defaultProfile)
        {
            profiles = source.Copy(); current = currentConfiguration.Copy(); defaultConfiguration = defaultProfile.Copy();
            Text = "KiWeave profiles"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Design.DarkTitlebar(this);
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(820, 630); MinimumSize = new Size(820, 610);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 2, RowCount = 1 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); Controls.Add(root);
            var left = new DesignCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 14, 0) }; root.Controls.Add(left, 0, 0);
            var leftRows = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 }; leftRows.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); leftRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); left.Controls.Add(leftRows);
            list.Dock = DockStyle.Fill; list.BackColor = UiStyle.Surface; list.ForeColor = UiStyle.Ink; list.BorderStyle = BorderStyle.None; list.SelectedIndexChanged += delegate { LoadSelected(); }; leftRows.Controls.Add(list, 0, 0);
            var add = UiStyle.Button("Add current", delegate { AddCurrent(); }, true); var remove = UiStyle.Button("Remove", delegate { RemoveSelected(); });
            add.MinimumSize = new Size(104, 42); remove.MinimumSize = new Size(78, 42); add.Padding = remove.Padding = new Padding(8);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0), WrapContents = false }; buttons.Controls.Add(add); buttons.Controls.Add(remove); leftRows.Controls.Add(buttons, 0, 1);
            var right = new DesignCard { Dock = DockStyle.Fill }; root.Controls.Add(right, 1, 0); var stack = UiStyle.Stack(); right.Controls.Add(stack);
            stack.Controls.Add(UiStyle.Text("Profiles", 20, true)); stack.Controls.Add(UiStyle.Text("Switch layouts manually or assign apps for automatic switching. Profiles are private to the current Windows user and stored under that user's local KiWeave data folder.", 9, false));
            stack.Controls.Add(UiStyle.Field("Profile name", name));
            stack.Controls.Add(UiStyle.Field("Accent color (optional #RRGGBB)", accent));
            stack.Controls.Add(UiStyle.Field("Icon label (optional, up to 4 characters)", icon));
            stack.Controls.Add(UiStyle.Field("Keyboard layout ID (optional)", layoutId));
            applications.Multiline = true; applications.Height = 92; applications.ScrollBars = ScrollBars.Vertical;
            stack.Controls.Add(UiStyle.Field("Automatic apps (one process name per line)", applications));
            stack.Controls.Add(UiStyle.Text("Examples: obs64.exe, Discord.exe, Spotify.exe. KiWeave switches only while its editor is closed and has no unsaved changes.", 8.5f, false));
            stack.Controls.Add(UiStyle.Field("Inherit unchanged mappings from", inheritance)); inheritanceTree.MaximumSize = new Size(450, 0); inheritanceTree.ForeColor = UiStyle.Blue; inheritanceTree.Margin = new Padding(0, 4, 0, 8); stack.Controls.Add(inheritanceTree);
            stack.Controls.Add(UiStyle.Text("Inherited profiles follow their base until you intentionally change a mapping, layer, enabled state, or custom-hotkey set.", 8.5f, false));
            feedback.AutoSize = true; feedback.ForeColor = UiStyle.Muted; feedback.Margin = new Padding(0, 12, 0, 10); stack.Controls.Add(feedback);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
            var save = UiStyle.Button("Save details", delegate { SaveDetails(); }); var duplicate = UiStyle.Button("Duplicate", delegate { DuplicateSelected(); }); var snapshots = UiStyle.Button("Open snapshots", delegate { OpenSnapshots(); });
            var use = UiStyle.Button("Use profile", delegate { UseSelected(); }, true); var close = UiStyle.Button("Close", delegate { DialogResult = DialogResult.Cancel; Close(); });
            save.MinimumSize = new Size(90, 42); duplicate.MinimumSize = new Size(82, 42); snapshots.MinimumSize = new Size(110, 42); use.MinimumSize = new Size(92, 42); close.MinimumSize = new Size(70, 42);
            save.Padding = duplicate.Padding = snapshots.Padding = use.Padding = close.Padding = new Padding(8); actions.Controls.Add(save); actions.Controls.Add(duplicate); actions.Controls.Add(snapshots); actions.Controls.Add(use); actions.Controls.Add(close); stack.Controls.Add(actions);
            RefreshList(); if (list.Items.Count > 0) list.SelectedIndex = 0;
        }
        void RefreshList()
        {
            int old = list.SelectedIndex; list.Items.Clear(); list.Items.AddRange(profiles.Profiles.Cast<object>().ToArray()); if (old >= 0 && old < list.Items.Count) list.SelectedIndex = old;
        }
        void LoadSelected()
        {
            var p = list.SelectedItem as KeyWeaveProfile; name.Text = p == null ? "" : p.Name; accent.Text = p == null ? "" : p.Accent; icon.Text = p == null ? "" : p.Icon; applications.Text = p == null ? "" : String.Join(Environment.NewLine, p.Applications);
            inheritance.Items.Clear(); inheritance.Items.Add("None (independent)"); inheritance.Items.Add("Default");
            foreach (var candidate in profiles.Profiles.Where(x => p == null || !ReferenceEquals(x, p))) inheritance.Items.Add(candidate.Name);
            string selected = p == null || String.IsNullOrWhiteSpace(p.InheritFrom) ? "None (independent)" : p.InheritFrom;
            int index = inheritance.Items.Cast<object>().Select(x => x.ToString()).ToList().FindIndex(x => String.Equals(x, selected, StringComparison.OrdinalIgnoreCase));
            inheritance.SelectedIndex = Math.Max(0, index);
            layoutId.Text = p == null ? "" : KeyboardLayoutProfiles.ForProfile(p.Name); inheritanceTree.Text = p == null ? "" : "Inheritance: " + profiles.InheritanceTree(p.Name); feedback.Text = p == null ? "Choose a profile." : String.IsNullOrWhiteSpace(p.InheritFrom) ? "Independent profile." : "Inherits from " + p.InheritFrom + " with " + p.OverrideKeys.Length + " intentional override" + (p.OverrideKeys.Length == 1 ? "." : "s.");
        }
        void AddCurrent()
        {
            int number = 1; string candidate; do candidate = "Profile " + number++; while (profiles.Find(candidate) != null);
            var p = new KeyWeaveProfile { Name = candidate, Configuration = current.Copy() };
            profiles.Profiles = profiles.Profiles.Concat(new[] { p }).ToArray(); RefreshList(); list.SelectedItem = p; feedback.Text = "Added a copy of the mappings currently in the editor.";
        }
        void OpenSnapshots()
        {
            var p = list.SelectedItem as KeyWeaveProfile; if (p == null) { feedback.Text = "Choose a profile first."; return; }
            try { Directory.CreateDirectory(ProfileSnapshots.Folder(p.Name)); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ProfileSnapshots.Folder(p.Name)) { UseShellExecute = true }); } catch (Exception ex) { feedback.Text = "Snapshots could not be opened: " + ex.Message; }
        }
        void RemoveSelected()
        {
            var p = list.SelectedItem as KeyWeaveProfile; if (p == null) return;
            var dependent = profiles.Profiles.FirstOrDefault(x => String.Equals(x.InheritFrom, p.Name, StringComparison.OrdinalIgnoreCase));
            if (dependent != null) { feedback.Text = "Cannot remove this profile while " + dependent.Name + " inherits from it."; return; }
            profiles.Profiles = profiles.Profiles.Where(x => !ReferenceEquals(x, p)).ToArray(); RefreshList(); if (list.Items.Count > 0) list.SelectedIndex = 0;
            try { CaptureHistory("profile remove"); ProfileStore.Save(ProfileStore.DefaultPath, profiles); feedback.Text = "Profile removed."; } catch (Exception ex) { feedback.Text = ex.Message; }
        }
        bool SaveDetails()
        {
            var p = list.SelectedItem as KeyWeaveProfile; if (p == null) { feedback.Text = "Choose a profile first."; return false; }
            string oldName = p.Name; string[] oldApps = p.Applications; string oldBase = p.InheritFrom; string oldAccent = p.Accent; string oldIcon = p.Icon; string[] oldOverrides = p.OverrideKeys.ToArray(); Configuration oldConfiguration = p.Configuration.Copy();
            Configuration effective;
            try { effective = profiles.Resolve(p.Name, defaultConfiguration); } catch (Exception ex) { feedback.Text = ex.Message; return false; }
            p.Name = name.Text.Trim(); p.Accent = accent.Text.Trim(); p.Icon = icon.Text.Trim(); p.Applications = applications.Lines.Select(ProfileStore.NormalizeProcess).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            string selectedBase = inheritance.SelectedItem == null ? "" : inheritance.SelectedItem.ToString(); p.InheritFrom = selectedBase == "None (independent)" ? "" : selectedBase;
            foreach (var item in profiles.Profiles.Where(x => !ReferenceEquals(x, p) && String.Equals(x.InheritFrom, oldName, StringComparison.OrdinalIgnoreCase))) item.InheritFrom = p.Name;
            try { ProfileStore.SetEffectiveConfiguration(profiles, p, effective, defaultConfiguration); ProfileStore.Validate(profiles, false); KeyboardLayoutProfiles.Set(p.Name, layoutId.Text); CaptureHistory("profile save"); ProfileStore.Save(ProfileStore.DefaultPath, profiles); ProfileSnapshots.Save(p, profiles); RefreshList(); list.SelectedItem = p; feedback.Text = String.IsNullOrWhiteSpace(p.InheritFrom) ? "Independent profile details saved." : "Inheritance saved with " + p.OverrideKeys.Length + " intentional overrides."; return true; }
            catch (Exception ex) {
                foreach (var item in profiles.Profiles.Where(x => !ReferenceEquals(x, p) && String.Equals(x.InheritFrom, p.Name, StringComparison.OrdinalIgnoreCase))) item.InheritFrom = oldName;
                p.Name = oldName; p.Applications = oldApps; p.InheritFrom = oldBase; p.Accent = oldAccent; p.Icon = oldIcon; p.OverrideKeys = oldOverrides; p.Configuration = oldConfiguration; feedback.Text = ex.Message; return false;
            }
        }
        void UseSelected()
        {
            if (!SaveDetails()) return; var p = list.SelectedItem as KeyWeaveProfile; if (p == null) return;
            try { ProfileStore.Validate(profiles, false); SelectedProfile = p.Copy(); DialogResult = DialogResult.OK; Close(); } catch (Exception ex) { feedback.Text = ex.Message; }
        }
        void DuplicateSelected()
        {
            var source = list.SelectedItem as KeyWeaveProfile; if (source == null) { feedback.Text = "Choose a profile first."; return; }
            int number = 2; string candidate = source.Name + " copy"; while (profiles.Find(candidate) != null) candidate = source.Name + " copy " + number++;
            var copy = source.Copy(); copy.Name = candidate; copy.Applications = new string[0]; profiles.Profiles = profiles.Profiles.Concat(new[] { copy }).ToArray();
            try { CaptureHistory("profile duplicate"); ProfileStore.Save(ProfileStore.DefaultPath, profiles); ProfileSnapshots.Save(copy, profiles); RefreshList(); list.SelectedItem = copy; feedback.Text = "Profile duplicated without automatic app assignments."; }
            catch (Exception ex) { profiles.Profiles = profiles.Profiles.Where(x => !ReferenceEquals(x, copy)).ToArray(); RefreshList(); feedback.Text = ex.Message; }
        }
        static void CaptureHistory(string reason)
        {
            try { ConfigurationHistory.CaptureCurrent(reason); }
            catch (Exception ex) { AppLog.Record("ConfigurationHistory", ex); }
        }
    }
}
