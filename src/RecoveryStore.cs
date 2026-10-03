using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal static class RecoveryStore
    {
        internal static string BackupsFolder { get { return Path.Combine(Path.GetDirectoryName(ConfigStore.DefaultPath), "Backups"); } }
        internal static string KnownGoodPath { get { return Path.Combine(BackupsFolder, "known-good.keyweave"); } }
        internal static string DraftFolder { get { return Path.Combine(Path.GetDirectoryName(ConfigStore.DefaultPath), "Drafts"); } }
        internal static string DraftPath { get { return Path.Combine(DraftFolder, "recovery-draft.keyweave"); } }
        internal static string DraftProfilePath { get { return Path.Combine(DraftFolder, "recovery-profile.txt"); } }

        internal static void SaveDraft(Configuration defaults, ProfileCollection profiles, UserPreferences preferences, bool startup, string currentProfile, Configuration draft)
        { SaveDraft(DraftPath, DraftProfilePath, defaults, profiles, preferences, startup, currentProfile, draft); }
        internal static void SaveDraft(string draftPath, string profilePath, Configuration defaults, ProfileCollection profiles, UserPreferences preferences, bool startup, string currentProfile, Configuration draft)
        {
            Configuration draftDefaults = defaults.Copy(); ProfileCollection draftProfiles = profiles.Copy(); string profile = String.IsNullOrWhiteSpace(currentProfile) ? "Default" : currentProfile;
            if (String.Equals(profile, "Default", StringComparison.OrdinalIgnoreCase)) draftDefaults = draft.Copy();
            else { var active = draftProfiles.Find(profile); if (active == null) throw new InvalidOperationException("The draft profile no longer exists."); ProfileStore.SetEffectiveConfiguration(draftProfiles, active, draft, draftDefaults); }
            Directory.CreateDirectory(Path.GetDirectoryName(draftPath)); BackupBundle.Save(draftPath, draftDefaults, draftProfiles, preferences, startup, false);
            File.WriteAllText(profilePath, profile, new UTF8Encoding(false));
        }

        internal static bool TryLoadDraft(out KeyWeaveBackup backup, out string profile)
        { return TryLoadDraft(DraftPath, DraftProfilePath, out backup, out profile); }
        internal static bool TryLoadDraft(string draftPath, string profilePath, out KeyWeaveBackup backup, out string profile)
        {
            backup = null; profile = "Default"; if (!File.Exists(draftPath)) return false; backup = BackupBundle.Load(draftPath);
            if (File.Exists(profilePath)) { string value = File.ReadAllText(profilePath, Encoding.UTF8).Trim(); if (value.Length > 0 && value.Length <= 40 && value.IndexOfAny(new[] { '\r', '\n' }) < 0) profile = value; }
            if (!String.Equals(profile, "Default", StringComparison.OrdinalIgnoreCase) && backup.Profiles.Find(profile) == null) profile = "Default"; return true;
        }
        internal static void DeleteDraft() { try { if (File.Exists(DraftPath)) File.Delete(DraftPath); } catch { } try { if (File.Exists(DraftProfilePath)) File.Delete(DraftProfilePath); } catch { } }
        internal static void SaveKnownGoodCurrent()
        {
            Configuration configuration = File.Exists(ConfigStore.DefaultPath) ? ConfigStore.Load(ConfigStore.DefaultPath) : new Configuration();
            ProfileCollection profiles = File.Exists(ProfileStore.DefaultPath) ? ProfileStore.Load(ProfileStore.DefaultPath) : new ProfileCollection();
            UserPreferences preferences = File.Exists(UserPreferences.DefaultPath) ? UserPreferences.Load(UserPreferences.DefaultPath) : new UserPreferences();
            Directory.CreateDirectory(BackupsFolder); BackupBundle.Save(KnownGoodPath, configuration, profiles, preferences, Startup.Enabled, false);
        }
    }

    internal enum DraftRecoveryChoice { Later, Restore, Discard }
    internal sealed class DraftRecoveryForm : Form
    {
        readonly KeyWeaveBackup backup; internal DraftRecoveryChoice Choice { get; private set; }
        internal DraftRecoveryForm(KeyWeaveBackup draft, string draftProfile, Configuration current, ProfileCollection profiles, UserPreferences preferences, bool startup)
        {
            backup = draft; Choice = DraftRecoveryChoice.Later; Text = "KiWeave · Recover unsaved edits"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; ClientSize = new Size(760, 560); MinimumSize = new Size(620, 460); StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi; Design.DarkTitlebar(this);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26), ColumnCount = 1, RowCount = 4 }; root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(root);
            var header = UiStyle.Stack(); header.Controls.Add(UiStyle.Text("Unsaved edits were found", 22, true)); header.Controls.Add(UiStyle.Text("KiWeave kept a private local recovery draft after the previous session ended unexpectedly. Nothing in it has been activated.", 9, false)); root.Controls.Add(header);
            string detail = "Draft profile: " + (String.Equals(draftProfile, "Default", StringComparison.OrdinalIgnoreCase) ? "Default" : "Custom profile") + "\r\nCreated: " + backup.CreatedUtc.ToLocalTime().ToString("g") + "\r\n\r\n" + ConfigurationHistory.Compare(backup, current, profiles, preferences, startup);
            root.Controls.Add(new DesignTextBox { Multiline = true, ReadOnly = true, TabStop = false, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, WordWrap = true, Text = detail, BackColor = UiStyle.Surface, ForeColor = UiStyle.Ink, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 16, 0, 10) });
            root.Controls.Add(UiStyle.Text("Restore opens the draft in the editor as unsaved changes. Discard removes only this recovery draft. Export creates a private full backup for manual review.", 9, false));
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, Margin = new Padding(0, 14, 0, 0) };
            buttons.Controls.Add(UiStyle.Button("Restore to editor", delegate { Choice = DraftRecoveryChoice.Restore; DialogResult = DialogResult.OK; Close(); }, true));
            buttons.Controls.Add(UiStyle.Button("Discard draft", delegate { if (MessageBox.Show(this, "Discard this unsaved recovery draft? Active saved mappings are not affected.", "Discard recovery draft", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) { Choice = DraftRecoveryChoice.Discard; DialogResult = DialogResult.OK; Close(); } }));
            buttons.Controls.Add(UiStyle.Button("Export private copy", Export)); buttons.Controls.Add(UiStyle.Button("Decide later", delegate { Close(); })); root.Controls.Add(buttons);
        }
        void Export(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog { Filter = "KiWeave backup|*.keyweave", FileName = "KiWeave-recovered-draft.keyweave", DefaultExt = "keyweave", AddExtension = true }) if (dialog.ShowDialog(this) == DialogResult.OK)
                try { BackupBundle.Save(dialog.FileName, backup.Configuration, backup.Profiles, backup.Preferences, backup.StartWithWindows, false); MessageBox.Show(this, "Private draft copy exported.", "Draft recovery", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception ex) { MessageBox.Show(this, "The draft could not be exported. " + ex.Message, "Draft recovery", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
