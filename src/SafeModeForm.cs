using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class SafeModeState
    {
        internal Configuration Configuration = new Configuration();
        internal ProfileCollection Profiles = new ProfileCollection();
        internal UserPreferences Preferences = new UserPreferences { UseTray = false, CheckUpdates = false, AutomaticProfiles = false, NetworkAccess = false };
        internal bool Startup;
        internal readonly List<string> Findings = new List<string>();
        internal bool ConfigurationReadable = true, ProfilesReadable = true, PreferencesReadable = true;
        internal bool FullyReadable { get { return ConfigurationReadable && ProfilesReadable && PreferencesReadable; } }
    }

    internal sealed class SafeModeForm : Form
    {
        readonly Label stateText = UiStyle.Text("", 10, false), detailText = UiStyle.Text("", 9, false);
        readonly DesignScrollPanel scroll = new DesignScrollPanel();
        Button backupButton;
        SafeModeState state;
        internal bool RestartNormallyRequested { get; private set; }

        internal SafeModeForm()
        {
            NetworkPolicy.Enabled = false;
            Text = "KiWeave Safe Mode"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            ClientSize = new Size(920, 720); MinimumSize = new Size(780, 650); StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi; Design.DarkTitlebar(this);
            BuildUi(); ReloadState();
        }

        void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(30), ColumnCount = 1, RowCount = 4 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); Controls.Add(root);
            var header = UiStyle.Stack(); header.Controls.Add(UiStyle.Text("KiWeave Safe Mode", 26, true));
            var intro = UiStyle.Text("Recovery-only startup. Keyboard hooks, global hotkeys, automatic profiles, integrations, mapped actions, tray hiding, and every network feature are inactive.", 10, false); intro.Margin = new Padding(0, 0, 0, 18); header.Controls.Add(intro); root.Controls.Add(header, 0, 0);

            scroll.Dock = DockStyle.Fill; scroll.Padding = new Padding(0, 0, 10, 0); root.Controls.Add(scroll, 0, 1);
            var stack = UiStyle.Stack(); scroll.Controls.Add(stack);
            var status = Card("Recovery status"); status.Controls[0].Controls.Add(stateText); status.Controls[0].Controls.Add(detailText); stack.Controls.Add(status);

            var recovery = Card("Restore or preserve your setup"); var recoveryStack = (TableLayoutPanel)recovery.Controls[0];
            recoveryStack.Controls.Add(UiStyle.Text("Restore a reviewed full backup or a local history snapshot. KiWeave creates a rollback folder before replacing active files.", 9, false));
            var recoveryButtons = Buttons(); recoveryButtons.Controls.Add(UiStyle.Button("Restore backup", RestoreBackup, true)); recoveryButtons.Controls.Add(UiStyle.Button("Restore known-good", RestoreKnownGood)); recoveryButtons.Controls.Add(UiStyle.Button("Undo and history", OpenHistory));
            backupButton = UiStyle.Button("Back up readable setup", ExportBackup); recoveryButtons.Controls.Add(backupButton); recoveryStack.Controls.Add(recoveryButtons); stack.Controls.Add(recovery);

            var inspect = Card("Inspect without activating anything"); var inspectStack = (TableLayoutPanel)inspect.Controls[0];
            inspectStack.Controls.Add(UiStyle.Text("Diagnostics are redacted. Opening the data folder does not load mappings or run actions.", 9, false));
            var inspectButtons = Buttons(); inspectButtons.Controls.Add(UiStyle.Button("Safe diagnostics", ShowDiagnostics, true)); inspectButtons.Controls.Add(UiStyle.Button("Privacy center", ShowPrivacy)); inspectButtons.Controls.Add(UiStyle.Button("Open data folder", OpenDataFolder)); inspectStack.Controls.Add(inspectButtons); stack.Controls.Add(inspect);
            scroll.SizeChanged += delegate { UiStyle.Wrap(stack); };

            var note = UiStyle.Text("Safe Mode never fixes or deletes configuration automatically. Restart normally only after you have reviewed or restored the setup.", 9, false); note.Margin = new Padding(0, 14, 0, 4); root.Controls.Add(note, 0, 2);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 12, 0, 0) };
            footer.Controls.Add(UiStyle.Button("Restart normally", delegate { if (MessageBox.Show(this, "Leave recovery mode and start normal KiWeave? Saved enabled mappings and hotkeys may become active after normal startup.", "Restart normally", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) { RestartNormallyRequested = true; Close(); } }, true)); footer.Controls.Add(UiStyle.Button("Close", delegate { Close(); })); root.Controls.Add(footer, 0, 3);
        }

        static DesignCard Card(string title)
        {
            var card = new DesignCard { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(22), Margin = new Padding(0, 0, 0, 16) };
            var stack = UiStyle.Stack(); stack.Controls.Add(UiStyle.Text(title, 15, true)); card.Controls.Add(stack); return card;
        }
        static FlowLayoutPanel Buttons() { return new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 10, 0, 0) }; }

        internal static SafeModeState ReadState()
        {
            var result = new SafeModeState();
            try { result.Configuration = File.Exists(ConfigStore.DefaultPath) ? ConfigStore.Load(ConfigStore.DefaultPath) : new Configuration(); if (!File.Exists(ConfigStore.DefaultPath)) result.Findings.Add("Default configuration is missing; normal mode would use safe defaults."); }
            catch (Exception ex) { result.ConfigurationReadable = false; result.Findings.Add("Default configuration cannot be read: " + ex.Message); }
            try { result.Profiles = File.Exists(ProfileStore.DefaultPath) ? ProfileStore.Load(ProfileStore.DefaultPath) : new ProfileCollection(); if (!File.Exists(ProfileStore.DefaultPath)) result.Findings.Add("No separate profile file is present."); }
            catch (Exception ex) { result.ProfilesReadable = false; result.Findings.Add("Profiles cannot be read: " + ex.Message); }
            try { result.Preferences = File.Exists(UserPreferences.DefaultPath) ? UserPreferences.Load(UserPreferences.DefaultPath) : new UserPreferences(); if (!File.Exists(UserPreferences.DefaultPath)) result.Findings.Add("Preferences are missing; normal defaults would be used."); }
            catch (Exception ex) { result.PreferencesReadable = false; result.Findings.Add("Preferences cannot be read: " + ex.Message); }
            try { result.Startup = Startup.Enabled; } catch (Exception ex) { result.Findings.Add("Windows startup state cannot be read: " + ex.Message); }
            return result;
        }

        void ReloadState()
        {
            state = ReadState(); NetworkPolicy.Enabled = false;
            stateText.Text = state.FullyReadable ? "Saved setup is readable. Recovery protections remain active." : "One or more saved files need attention.";
            stateText.ForeColor = state.FullyReadable ? Color.FromArgb(127, 214, 169) : Color.FromArgb(236, 174, 105);
            detailText.Text = (state.Findings.Count == 0 ? "No configuration errors were found." : String.Join("\r\n", state.Findings.Select(x => "• " + x))) +
                "\r\n\r\nKeyboard hook: not loaded\r\nGlobal hotkeys: not registered\r\nNetwork access: blocked for this session";
            if (backupButton != null) { backupButton.Enabled = state.FullyReadable; backupButton.Text = state.FullyReadable ? "Back up readable setup" : "Backup unavailable until readable"; }
        }

        void RestoreBackup(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = "KiWeave backup|*.keyweave|All files|*.*" }) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try {
                    var backup = BackupBundle.Load(dialog.FileName);
                    string prompt = BackupBundle.Describe(backup) + "\n\nThis replaces the saved Default mappings, profiles, preferences, and startup preference. Nothing will run in Safe Mode. A rollback folder is created first.\n\nRestore this reviewed backup?";
                    if (MessageBox.Show(this, prompt, "Safe Mode restore", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    PreserveReadableState("safe mode backup restore"); string rollback = BackupBundle.Restore(backup); ReloadState();
                    MessageBox.Show(this, "Backup restored without activating mappings.\n\nRollback folder: " + rollback, "Safe Mode", MessageBoxButtons.OK, MessageBoxIcon.Information);
                } catch (Exception ex) { MessageBox.Show(this, "The backup was not restored. " + ex.Message, "Safe Mode", MessageBoxButtons.OK, MessageBoxIcon.Error); ReloadState(); }
            }
        }

        void OpenHistory(object sender, EventArgs e)
        {
            using (var dialog = new ConfigurationHistoryForm(state.Configuration, state.Profiles, state.Preferences, state.Startup)) {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedEntry == null) return;
                var entry = dialog.SelectedEntry; string prompt = entry + "\n\n" + ConfigurationHistory.Compare(entry.Backup, state.Configuration, state.Profiles, state.Preferences, state.Startup) + "\n\nRestore this snapshot without activating mappings? A rollback folder is created first.";
                if (MessageBox.Show(this, prompt, "Safe Mode history restore", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try { PreserveReadableState("safe mode history restore"); string rollback = BackupBundle.Restore(entry.Backup); ReloadState(); MessageBox.Show(this, "History restored without activating mappings.\n\nRollback folder: " + rollback, "Safe Mode", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                catch (Exception ex) { MessageBox.Show(this, "History was not restored. " + ex.Message, "Safe Mode", MessageBoxButtons.OK, MessageBoxIcon.Error); ReloadState(); }
            }
        }

        void RestoreKnownGood(object sender, EventArgs e)
        {
            if (!File.Exists(RecoveryStore.KnownGoodPath)) { MessageBox.Show(this, "No known-good recovery copy exists yet. KiWeave creates one after a mapping save is validated and applied successfully.", "Known-good recovery", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            try {
                var backup = BackupBundle.Load(RecoveryStore.KnownGoodPath);
                string prompt = BackupBundle.Describe(backup) + "\n\nThis is the last setup KiWeave saved and applied successfully. Restore it without activating mappings? A rollback folder is created first.";
                if (MessageBox.Show(this, prompt, "Restore known-good setup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                PreserveReadableState("safe mode known good restore"); string rollback = BackupBundle.Restore(backup); ReloadState();
                MessageBox.Show(this, "Known-good setup restored without activating mappings.\n\nRollback folder: " + rollback, "Known-good recovery", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception ex) { MessageBox.Show(this, "The known-good setup was not restored. " + ex.Message, "Known-good recovery", MessageBoxButtons.OK, MessageBoxIcon.Error); ReloadState(); }
        }

        void PreserveReadableState(string reason) { if (state.FullyReadable) try { ConfigurationHistory.Capture(ConfigurationHistory.DefaultFolder, reason, state.Configuration, state.Profiles, state.Preferences, state.Startup, ConfigurationHistory.Limit); } catch (Exception ex) { AppLog.Record("SafeModeHistory", ex); } }
        void ExportBackup(object sender, EventArgs e)
        {
            if (!state.FullyReadable) return;
            if (MessageBox.Show(this, "A full backup contains private mappings, targets, arguments, URLs, profiles, and preferences. Create it without loading integrations?", "Safe Mode backup", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
            using (var dialog = new SaveFileDialog { Filter = "KiWeave backup|*.keyweave", FileName = "KiWeave-safe-mode-backup.keyweave", DefaultExt = "keyweave", AddExtension = true }) if (dialog.ShowDialog(this) == DialogResult.OK) try { BackupBundle.Save(dialog.FileName, state.Configuration, state.Profiles, state.Preferences, state.Startup, false); MessageBox.Show(this, "Private backup created.", "Safe Mode", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception ex) { MessageBox.Show(this, "Backup failed: " + ex.Message, "Safe Mode", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        void ShowDiagnostics(object sender, EventArgs e) { using (var dialog = new DiagnosticsForm(Diagnostics(), true)) dialog.ShowDialog(this); }
        void ShowPrivacy(object sender, EventArgs e) { using (var dialog = new PrivacyCenterForm(false, Diagnostics())) dialog.ShowDialog(this); }
        void OpenDataFolder(object sender, EventArgs e) { try { Directory.CreateDirectory(PrivacyData.DataFolder); Process.Start(new ProcessStartInfo(PrivacyData.DataFolder) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(this, "The data folder could not be opened: " + ex.Message, "Safe Mode", MessageBoxButtons.OK, MessageBoxIcon.Error); } }
        internal string Diagnostics()
        {
            return "KiWeave safe diagnostics\r\nVersion: " + UpdateChecker.CurrentVersion + "\r\nSafe Mode: active\r\nKeyboard hook: not loaded\r\nGlobal hotkeys: not registered\r\nAutomatic profiles: inactive\r\nIntegrations: inactive\r\nNetwork access: blocked\r\nDefault configuration readable: " + (state.ConfigurationReadable ? "yes" : "no") + "\r\nProfiles readable: " + (state.ProfilesReadable ? "yes" : "no") + "\r\nPreferences readable: " + (state.PreferencesReadable ? "yes" : "no") + "\r\nSaved profiles: " + state.Profiles.Profiles.Length + "\r\nModifier layers: " + state.Configuration.Layers.Length + "\r\nCustom hotkeys: " + state.Configuration.CustomHotkeys.Length + "\r\nHistory snapshots: " + ConfigurationHistory.List(ConfigurationHistory.DefaultFolder).Count + "\r\nRecent private-safe log entries: " + AppLog.RecentCount() + "\r\n";
        }
    }
}
