using System;
using System.Drawing;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FunctionRowRemapper
{
    internal static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "KiWeave", LegacyName = "FunctionRowRemapper";
        public static bool Enabled {
            get { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && (k.GetValue(Name) != null || k.GetValue(LegacyName) != null); }
        }
        public static bool IsCurrent { get { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && String.Equals(k.GetValue(Name) as string, "\"" + Application.ExecutablePath + "\" --tray", StringComparison.OrdinalIgnoreCase); } }
        public static void Set(bool enabled)
        {
            if (!enabled && !Enabled) return;
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey)) {
                if (enabled) { k.SetValue(Name, "\"" + Application.ExecutablePath + "\" --tray", RegistryValueKind.String); k.DeleteValue(LegacyName, false); }
                else { k.DeleteValue(Name, false); k.DeleteValue(LegacyName, false); }
            }
        }
    }

    public sealed partial class MainForm : Form
    {
        internal sealed class SpecificChoice
        {
            public readonly string Label; public readonly Mapping Mapping;
            public readonly string Category, Context;
            public string SearchText { get { return Label + " " + Category + " " + Context + " " + Mapping.Target + " " + Mapping.Arguments; } }
            public SpecificChoice(string label, Mapping mapping, string category = "General", string context = "") { Label = label; Mapping = mapping; Category = category; Context = context; }
            public override string ToString() { return Label; }
            public bool Matches(Mapping other) { return other != null && Mapping.Kind == other.Kind && Mapping.Target == other.Target && Mapping.Arguments == other.Arguments && Mapping.WorkingDirectory == other.WorkingDirectory; }
        }
        static SpecificChoice Choice(string label, ActionKind kind) { return new SpecificChoice(label, new Mapping { Kind = kind }); }
        static readonly SpecificChoice ChooseAction = Choice("Choose an action", ActionKind.Unbound);
        static SpecificChoice Preset(string label, ActionKind kind, string target, string arguments) { return new SpecificChoice(label, new Mapping { Kind = kind, Target = target, Arguments = arguments }); }
        static SpecificChoice Shortcut(string label, string target) { return Preset(label, ActionKind.SendShortcut, target, ""); }
        static readonly string[] FunctionGroups = { "Normal behavior", "Disable this key", "Keyboard input", "Media and sound", "Open or run something", "Monitor controls", "Custom action" };
        static readonly string[] CustomGroups = { "Keyboard input", "Media and sound", "Open or run something", "Custom action" };
        readonly Color ink = UiStyle.Ink, accent = UiStyle.Blue, muted = UiStyle.Muted;
        readonly ListView list = new DesignListView();
        readonly ListView customList = new DesignListView();
        readonly ComboBox kind = new ComboBox(), media = new ComboBox(), simpleKind = new DesignComboBox(), specificKind = new DesignComboBox();
        readonly ComboBox customSimpleKind = new DesignComboBox(), customSpecificKind = new DesignComboBox();
        readonly ComboBox monitor = new DesignComboBox(), monitorControl = new DesignComboBox();
        readonly NumericUpDown monitorStep = new DesignNumericUpDown { Minimum = 1, Maximum = 20, Value = 5 };
        readonly Button detect = new DesignButton();
        readonly Label monitorStatus = new Label();
        readonly TableLayoutPanel monitorPanel = new TableLayoutPanel();
        DdcMonitor[] detected = new DdcMonitor[0];
        bool scanning;
        readonly TextBox target = new DesignTextBox(), arguments = new DesignTextBox(), working = new DesignTextBox();
        readonly TextBox customShortcut = new DesignTextBox(), customTarget = new DesignTextBox(), customArguments = new DesignTextBox(), customWorking = new DesignTextBox();
        readonly TextBox functionNote = new DesignTextBox { Multiline = true, Height = 62, ScrollBars = ScrollBars.Vertical };
        readonly TextBox customNote = new DesignTextBox { Multiline = true, Height = 62, ScrollBars = ScrollBars.Vertical };
        Dictionary<string, string> mappingNotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly ComboBox customKind = new ComboBox(), customMedia = new ComboBox();
        readonly List<SequenceStep> sequenceSteps = new List<SequenceStep>();
        Button customBrowse;
        readonly CheckBox enabled = new DesignToggle(), startup = new DesignCheckBox(), useTray = new DesignCheckBox(), checkUpdates = new DesignCheckBox(), automaticProfiles = new DesignCheckBox(), networkAccess = new DesignCheckBox(), experimentalFeatures = new DesignCheckBox(), developerMode = new DesignCheckBox(), gameMode = new DesignCheckBox(), notifyUpdates = new DesignCheckBox(), notifyHealth = new DesignCheckBox(), notifySafety = new DesignCheckBox();
        readonly ComboBox themeChoice = new DesignComboBox();
        readonly ComboBox notificationSeverity = new DesignComboBox();
        readonly ComboBox updateChannel = new DesignComboBox();
        readonly NumericUpDown historyRetention = new DesignNumericUpDown { Minimum = 5, Maximum = 100, Increment = 5, Value = 20 };
        Button hideToTray;
        readonly ToolTip tips = new ToolTip();
        UserPreferences preferences;
        NotificationPreferences notificationPreferences;
        FeatureFlags featureFlags;
        bool exitRequested;
        readonly Label editorTitle = new DesignLabel(), hint = new DesignLabel(), status = new DesignLabel(), feedback = new DesignLabel(), targetLabel = new DesignLabel(), argumentsLabel = new DesignLabel(), workingLabel = new DesignLabel();
        readonly Button browse = new DesignButton(), folder = new DesignButton(), workBrowse = new DesignButton();
        readonly NotifyIcon tray = new NotifyIcon();
        UpdateNotification updateNotice;
        readonly ToolStripMenuItem trayToggle = new ToolStripMenuItem("Enable remapping");
        readonly ToolStripMenuItem trayProfiles = new ToolStripMenuItem("Profiles");
        readonly ToolStripMenuItem trayWhyProfile = new ToolStripMenuItem("Why this profile?");
        readonly ToolStripMenuItem trayPinProfile = new ToolStripMenuItem("Pin current profile");
        readonly ToolStripMenuItem trayLayer = new ToolStripMenuItem("Layer: Base");
        readonly System.Windows.Forms.Timer statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        readonly System.Windows.Forms.Timer scheduleTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        readonly System.Windows.Forms.Timer draftTimer = new System.Windows.Forms.Timer { Interval = 750 };
        readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer { Interval = UpdateChecker.CheckIntervalMilliseconds };
        readonly bool startInTray;
        readonly bool isPreview;
        readonly bool showWelcome;
        KeyboardEngine engine;
        StreamDeckBridge streamDeckBridge;
        readonly ActionDispatcher customDispatcher;
        readonly System.Collections.Generic.Dictionary<int, CustomHotkey> registeredHotkeys = new System.Collections.Generic.Dictionary<int, CustomHotkey>();
        Configuration saved, draft;
        ProfileCollection profiles = new ProfileCollection();
        ConfigurationHealthReport healthReport = ConfigurationHealthReport.Empty;
        string currentProfile = "Default";
        bool automaticProfileActive;
        string pinnedProfile = "";
        string profileReason = "Default profile at launch.";
        string automaticProfileProcess = "";
        int selected, selectedLayer = -1, customSelected = -1; bool loading, dirty, readOnlyMode;
        string initialError;

        public MainForm(bool startInTray) : this(startInTray, false) { }
        internal MainForm(bool startInTray, bool preview)
        {
            this.startInTray = startInTray;
            isPreview = preview;
            customDispatcher = new ActionDispatcher(new WindowsActionSink(RequestProfileActivation));
            showWelcome = !preview && !File.Exists(ConfigStore.DefaultPath) && !File.Exists(UserPreferences.DefaultPath) && !File.Exists(FirstRun.SeenPath);
            Text = "KiWeave"; Font = new Font("Segoe UI", 10F); ForeColor = ink; BackColor = UiStyle.Canvas;
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(1200, 900); MinimumSize = new Size(1200, 900); MaximumSize = new Size(1200, 900); FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen; DoubleBuffered = true;
            Icon = Program.AppIcon();
            saved = new Configuration();
            try { preferences = UserPreferences.Load(UserPreferences.DefaultPath); }
            catch (Exception ex) { preferences = new UserPreferences { UseTray = false }; initialError = "Tray preference could not be loaded; the window will stay accessible. " + ex.Message; }
            UiStyle.ApplyTheme(preferences.Theme);
            notificationPreferences = NotificationPreferences.Load();
            featureFlags = FeatureFlags.Load();
            UiStyle.ApplyAccent(preferences.CustomAccent);
            Design.GlassBackdrop(this, String.Equals(preferences.Theme, "Glass", StringComparison.OrdinalIgnoreCase));
            NetworkPolicy.Enabled = preferences.NetworkAccess;
            DiscordIntegration.Start(NetworkPolicy.Enabled);
            try { if (File.Exists(ConfigStore.DefaultPath)) saved = ConfigStore.Load(ConfigStore.DefaultPath); }
            catch (Exception ex) { initialError = "Saved configuration could not be loaded. Remapping is off; the original file is untouched. " + ex.Message; }
            try { profiles = ProfileStore.Load(ProfileStore.DefaultPath); }
            catch (Exception ex) { initialError = "Profiles could not be loaded; the original file is untouched. " + ex.Message; }
            try { mappingNotes = MappingNoteStore.Load(); }
            catch (Exception ex) { mappingNotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); initialError = "Private mapping notes could not be loaded; the notes file is untouched. " + ex.Message; }
            draft = saved.Copy();
            BuildUi();
            Shown += delegate { SelectPage(0); PerformLayout(); Invalidate(true); };
            RefreshLayerView(); PopulateList(); PopulateCustomList(); LoadEditor(0);
            if (draft.CustomHotkeys.Length > 0) LoadCustomEditor(0); else SetCustomEditorState(false);
            if (preview) {
                loading = true; enabled.Checked = saved.Enabled; useTray.Checked = preferences.UseTray; checkUpdates.Checked = preferences.CheckUpdates; automaticProfiles.Checked = preferences.AutomaticProfiles; networkAccess.Checked = preferences.NetworkAccess; experimentalFeatures.Checked = featureFlags.ExperimentalEnabled; developerMode.Checked = featureFlags.DeveloperMode; checkUpdates.Enabled = preferences.NetworkAccess; startup.Checked = Startup.Enabled;
                Text = "KiWeave - Design preview"; hideToTray.Enabled = false; status.Text = "Editor preview"; loading = false; return;
            }
            try {
                engine = new KeyboardEngine(RequestProfileActivation);
                engine.Error += message => Ui(delegate { AppLog.Record("Mapped action failed"); if (notificationPreferences.Safety && notificationPreferences.AllowsWarning) SetFeedback(message, true); if (preferences.UseTray && notificationPreferences.Safety && notificationPreferences.AllowsWarning) tray.ShowBalloonTip(4000, "Action could not run", message, ToolTipIcon.Warning); });
                engine.EmergencyDisabled += () => Ui(EmergencyOff);
                engine.Apply(saved);
            } catch (Exception ex) { saved.Enabled = draft.Enabled = false; initialError = ex.Message; }
            if (engine != null) try { ApplyHotkeys(saved); } catch (Exception ex) { initialError = "Function-key remapping is still available, but a custom hotkey could not register. " + ex.Message; }
            loading = true; enabled.Checked = saved.Enabled;
            useTray.Checked = preferences.UseTray;
            checkUpdates.Checked = preferences.CheckUpdates;
            automaticProfiles.Checked = preferences.AutomaticProfiles;
            networkAccess.Checked = preferences.NetworkAccess;
            experimentalFeatures.Checked = featureFlags.ExperimentalEnabled; developerMode.Checked = featureFlags.DeveloperMode; gameMode.Checked = GameMode.Enabled;
            checkUpdates.Enabled = preferences.NetworkAccess;
            try { startup.Checked = Startup.Enabled; } catch (Exception ex) { initialError = "Cannot read startup setting: " + ex.Message; }
            loading = false;
            SetupTray(); UpdateStatus();
            if (FirstPartyExtensionCatalog.IsEnabled("streamdeck")) streamDeckBridge = new StreamDeckBridge(command => Ui(delegate { if (command == "show-settings") ShowSettings(); else if (command.StartsWith("activate-profile:", StringComparison.Ordinal)) RequestProfileActivation(command.Substring("activate-profile:".Length)); }));
            statusTimer.Tick += delegate { CheckAutomaticProfile(); UpdateStatus(); }; statusTimer.Start();
            scheduleTimer.Tick += delegate { CheckScheduledProfile(); }; scheduleTimer.Start();
            draftTimer.Tick += delegate { draftTimer.Stop(); SaveRecoveryDraft(); };
            updateTimer.Tick += delegate { RunAutomaticUpdateCheck(); };
            UpdateAutomaticCheckTimer();
            Shown += delegate {
                OfferDraftRecovery();
                DetectMonitors();
                if (initialError != null) SetFeedback(initialError, true);
                else CheckMissingTargets();
                if (startInTray && preferences.UseTray && initialError == null) Hide();
                if (showWelcome) try { using (var welcome = new WelcomeForm()) welcome.ShowDialog(this); FirstRun.MarkSeen(); } catch (Exception ex) { SetFeedback("Welcome setup could not be saved: " + ex.Message, true); }
                try {
                    DateTime lastNotice; var reminder = BackupReminder.Inspect(Path.Combine(AppStorage.DataFolder, "Backups"), DateTime.UtcNow, BackupReminder.TryReadLastNotified(out lastNotice) ? (DateTime?)lastNotice : null);
                    if (reminder.ShouldNotify && notificationPreferences.Safety && notificationPreferences.AllowsWarning) { SetFeedback(reminder.Message, true); if (preferences.UseTray) tray.ShowBalloonTip(5000, "Backup reminder", reminder.Message, ToolTipIcon.Info); BackupReminder.MarkNotified(DateTime.UtcNow); }
                } catch { }
                if (preferences.NetworkAccess && preferences.CheckUpdates) UpdateChecker.CheckInBackground(tag => Ui(delegate { if (tag != null && notificationPreferences.Updates) updateNotice = new UpdateNotification(tag); }), UpdateChannels.Load());
            };
            FormClosing += OnClosing;
        }
        void Ui(Action a) { if (!IsDisposed && IsHandleCreated) try { BeginInvoke(a); } catch (InvalidOperationException) { } }
        void CheckScheduledProfile()
        {
            if (isPreview || !preferences.AutomaticProfiles || pinnedProfile.Length > 0 || Visible || dirty || engine == null) return;
            string scheduled = ProfileScheduleStore.ActiveProfile(DateTime.Now);
            if (scheduled.Length > 0 && !String.Equals(scheduled, currentProfile, StringComparison.OrdinalIgnoreCase)) ActivateProfile(scheduled, false, true);
        }
        Label LabelText(string text, float size, Color color) { return new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", size), ForeColor = color, Margin = new Padding(0, 0, 0, 6) }; }
        Button ButtonText(string text, EventHandler click)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(90, 35), FlatStyle = FlatStyle.Flat, BackColor = UiStyle.Input, ForeColor = UiStyle.Ink, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(7, 2, 7, 2) };
            b.FlatAppearance.BorderColor = UiStyle.Border; b.Click += click; return b;
        }
        static int FunctionGroup(ActionKind kind) { if (kind == ActionKind.PassThrough) return 0; if (kind == ActionKind.Unbound) return 1; if (kind == ActionKind.SendKey || kind == ActionKind.SendShortcut) return 2; if (kind == ActionKind.Media) return 3; if (kind == ActionKind.Monitor) return 5; if (kind == ActionKind.LockThenSleep || kind == ActionKind.SystemAction || kind == ActionKind.Conditional) return 6; return 4; }
        static int CustomGroup(ActionKind kind) { if (kind == ActionKind.SendKey || kind == ActionKind.SendShortcut) return 0; if (kind == ActionKind.Media) return 1; if (kind == ActionKind.LockThenSleep || kind == ActionKind.Sequence || kind == ActionKind.SystemAction || kind == ActionKind.Conditional) return 3; return 2; }
        static int GroupFor(Mapping mapping, bool custom)
        {
            if (ChoicesFor(3, true).Any(c => c.Matches(mapping))) return custom ? 3 : 6;
            return custom ? CustomGroup(mapping.Kind) : FunctionGroup(mapping.Kind);
        }
        internal static SpecificChoice[] ChoicesFor(int group, bool custom)
        {
            string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (!custom) {
                if (group == 0) return new[] { Choice("Pass through normally", ActionKind.PassThrough) };
                if (group == 1) return new[] { Choice("Do nothing", ActionKind.Unbound) };
                if (group == 5) return new[] { Choice("Monitor brightness, contrast, or volume", ActionKind.Monitor) };
            }
            int normalized = custom ? group : group - 2;
            if (normalized == 0) return new[] { Choice("Send one key", ActionKind.SendKey), Choice("Send a keyboard shortcut", ActionKind.SendShortcut) };
            if (normalized == 1) return new[] { Preset("Volume up", ActionKind.Media, "VolumeUp", ""), Preset("Volume down", ActionKind.Media, "VolumeDown", ""), Preset("Mute or unmute", ActionKind.Media, "VolumeMute", ""), Preset("Play or pause", ActionKind.Media, "MediaPlayPause", ""), Preset("Next track", ActionKind.Media, "MediaNextTrack", ""), Preset("Previous track", ActionKind.Media, "MediaPreviousTrack", "") };
            if (normalized == 2) return new[] { Choice("Open an application", ActionKind.Application), Choice("Open a file or folder", ActionKind.FileOrFolder), Choice("Run a Windows shortcut", ActionKind.WindowsShortcut), Choice("Run a command or script", ActionKind.Command), Choice("Run a Python script", ActionKind.Python), Choice("Call an HTTP endpoint", ActionKind.HttpRequest) };
            var presets = new List<SpecificChoice> { Choice("Lock Windows, then sleep", ActionKind.LockThenSleep), Shortcut("Lock Windows", "Win+L"), Shortcut("Open file explorer", "Win+E"), Shortcut("Open Windows settings", "Win+I"), Shortcut("Open task manager", "Ctrl+Shift+Escape"), Shortcut("Open clipboard history", "Win+V"), Shortcut("Open notification center", "Win+N"), Shortcut("Open quick settings", "Win+A"), Shortcut("Open emoji picker", "Win+OemPeriod"), Shortcut("Open task view", "Win+Tab"), Shortcut("Open run dialog", "Win+R"), Shortcut("Open power-user menu", "Win+X"), Shortcut("Take a screen snip", "Win+Shift+S"), Shortcut("Show desktop", "Win+D"), Shortcut("Switch apps", "Alt+Tab"), Shortcut("Snap window left", "Win+Left"), Shortcut("Snap window right", "Win+Right"), Shortcut("Maximize window", "Win+Up"), Shortcut("Minimize window", "Win+Down"), Shortcut("New virtual desktop", "Win+Ctrl+D"), Shortcut("Close virtual desktop", "Win+Ctrl+F4"), Shortcut("Next virtual desktop", "Win+Ctrl+Right"), Shortcut("Previous virtual desktop", "Win+Ctrl+Left"), Shortcut("Copy", "Ctrl+C"), Shortcut("Paste", "Ctrl+V"), Shortcut("Cut", "Ctrl+X"), Shortcut("Undo", "Ctrl+Z"), Shortcut("Redo", "Ctrl+Y"), Shortcut("Select all", "Ctrl+A"), Shortcut("Save", "Ctrl+S"), Shortcut("Open", "Ctrl+O"), Shortcut("New", "Ctrl+N"), Shortcut("Find", "Ctrl+F"), Shortcut("Print", "Ctrl+P"), Shortcut("Refresh", "F5"), Shortcut("Close current window", "Alt+F4"), Shortcut("Toggle full screen", "F11"), Preset("Open calculator", ActionKind.Application, Path.Combine(sys, "calc.exe"), ""), Preset("Open notepad", ActionKind.Application, Path.Combine(sys, "notepad.exe"), ""), Preset("Open paint", ActionKind.Application, Path.Combine(sys, "mspaint.exe"), ""), Preset("Open control panel", ActionKind.Application, Path.Combine(sys, "control.exe"), ""), Preset("Open device manager", ActionKind.Application, Path.Combine(sys, "mmc.exe"), "devmgmt.msc"), Preset("Sleep", ActionKind.Application, Path.Combine(sys, "rundll32.exe"), "powrprof.dll,SetSuspendState 0,1,0"), Preset("Sign out", ActionKind.Application, Path.Combine(sys, "shutdown.exe"), "/l"), Preset("Restart", ActionKind.Application, Path.Combine(sys, "shutdown.exe"), "/r /t 0"), Preset("Shut down", ActionKind.Application, Path.Combine(sys, "shutdown.exe"), "/s /t 0") };
            presets.AddRange(ExpandedActions.All);
            presets.Insert(0, ChooseAction);
            presets.Insert(1, Choice("Build a conditional action...", ActionKind.Conditional));
            if (custom) presets.Insert(2, Choice("Build a step-by-step sequence...", ActionKind.Sequence)); return presets.ToArray();
        }
        void PopulateChoices(ComboBox box, int group, bool custom, Mapping selectedMapping)
        {
            box.Items.Clear(); foreach (var choice in ChoicesFor(group, custom)) box.Items.Add(choice);
            if (box.Items.Cast<SpecificChoice>().Any(c => ReferenceEquals(c, ChooseAction))) {
                box.Items.Add(Preset("Activate and pin profile: Default", ActionKind.SystemAction, SystemActions.ActivateProfilePrefix + "Default", ""));
                foreach (var profile in profiles.Profiles)
                    box.Items.Add(Preset("Activate and pin profile: " + profile.Name, ActionKind.SystemAction, SystemActions.ActivateProfilePrefix + profile.Name, ""));
            }
            int selectedChoice = 0;
            if (selectedMapping != null) {
                var choices = box.Items.Cast<SpecificChoice>().ToArray();
                int exact = Array.FindIndex(choices, c => c.Matches(selectedMapping));
                int sameKind = Array.FindIndex(choices, c => c.Mapping.Kind == selectedMapping.Kind);
                selectedChoice = exact >= 0 ? exact : Math.Max(0, sameKind);
            }
            box.SelectedIndex = selectedChoice;
        }
        void ApplyFunctionChoice()
        {
            if (loading || specificKind.SelectedItem == null) return; var choice = (SpecificChoice)specificKind.SelectedItem;
            if (ReferenceEquals(choice, ChooseAction)) { SetFeedback("Choose an action before saving this function key.", false); return; }
            if (choice.Mapping.Kind == ActionKind.Conditional) { OpenConditionalBuilder(false); return; }
            loading = true; kind.SelectedIndex = (int)choice.Mapping.Kind; target.Text = choice.Mapping.Target; arguments.Text = choice.Mapping.Arguments; working.Text = choice.Mapping.WorkingDirectory; media.SelectedIndex = Array.IndexOf(Shortcuts.MediaLabels.Keys.ToArray(), choice.Mapping.Target); ConfigureFields(false); loading = false; Edited();
        }
        void ApplyCustomChoice()
        {
            if (loading || customSpecificKind.SelectedItem == null) return; var choice = (SpecificChoice)customSpecificKind.SelectedItem;
            if (ReferenceEquals(choice, ChooseAction)) { SetFeedback("Choose an action before saving this custom hotkey.", false); return; }
            if (choice.Mapping.Kind == ActionKind.Conditional) { OpenConditionalBuilder(true); return; }
            loading = true; customKind.SelectedIndex = (int)choice.Mapping.Kind; customTarget.Text = choice.Mapping.Target; customArguments.Text = choice.Mapping.Arguments; customWorking.Text = choice.Mapping.WorkingDirectory; customMedia.SelectedIndex = Array.IndexOf(Shortcuts.MediaLabels.Keys.ToArray(), choice.Mapping.Target); ConfigureCustomFields(false); loading = false; if (choice.Mapping.Kind == ActionKind.Sequence) OpenSequenceBuilder(); else CustomEdited();
        }
        void PopulateCustomList()
        {
            loading = true; customList.BeginUpdate(); customList.Items.Clear();
            for (int i = 0; i < draft.CustomHotkeys.Length; i++) { var h = draft.CustomHotkeys[i]; var item = new ListViewItem(h.Shortcut); item.SubItems.Add(Summary(h.Action)); customList.Items.Add(item); }
            if (customSelected >= 0 && customSelected < customList.Items.Count) customList.Items[customSelected].Selected = true;
            customList.EndUpdate(); loading = false;
        }
        void OpenSequenceBuilder()
        {
            if (customSelected < 0 || customSelected >= draft.CustomHotkeys.Length) return;
            using (var dialog = new SequenceBuilderForm(sequenceSteps)) {
                if (dialog.ShowDialog(this) != DialogResult.OK) { LoadCustomEditor(customSelected); return; }
                sequenceSteps.Clear(); sequenceSteps.AddRange(dialog.Result);
                var h = draft.CustomHotkeys[customSelected];
                h.Action = new Mapping { Kind = ActionKind.Sequence, Target = SequenceCodec.Serialize(sequenceSteps) };
                LoadCustomEditor(customSelected); CustomEdited();
            }
        }
        void AddCustomHotkey()
        {
            var all = draft.CustomHotkeys.Concat(new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+K", Action = new Mapping { Kind = ActionKind.SendShortcut, Target = "" } } }).ToArray();
            draft.CustomHotkeys = all; customSelected = all.Length - 1; PopulateCustomList(); LoadCustomEditor(customSelected); MarkDirty();
        }
        void DuplicateCustomHotkey()
        {
            if (customSelected < 0 || customSelected >= draft.CustomHotkeys.Length) return;
            var copy = draft.CustomHotkeys[customSelected].Copy(); copy.Shortcut = "Ctrl+Alt+K";
            draft.CustomHotkeys = draft.CustomHotkeys.Concat(new[] { copy }).ToArray(); customSelected = draft.CustomHotkeys.Length - 1;
            PopulateCustomList(); LoadCustomEditor(customSelected); MarkDirty(); SetFeedback("Duplicated the action as a template. Choose a new shortcut before saving.", false);
        }
        void RemoveCustomHotkey()
        {
            if (customSelected < 0 || customSelected >= draft.CustomHotkeys.Length) return;
            draft.CustomHotkeys = draft.CustomHotkeys.Where((h, i) => i != customSelected).ToArray(); customSelected = draft.CustomHotkeys.Length - 1; PopulateCustomList(); if (customSelected >= 0) LoadCustomEditor(customSelected); else SetCustomEditorState(false); MarkDirty();
        }
        void LoadCustomEditor(int index)
        {
            if (index < 0 || index >= draft.CustomHotkeys.Length) return; loading = true; customSelected = index; var h = draft.CustomHotkeys[index];
            foreach (ListViewItem item in customList.Items) item.Selected = item.Index == index;
            SetCustomEditorState(true); customTitle.Text = String.IsNullOrWhiteSpace(h.Shortcut) ? "New shortcut" : h.Shortcut;
            sequenceSteps.Clear(); if (h.Action.Kind == ActionKind.Sequence) try { sequenceSteps.AddRange(SequenceCodec.Parse(h.Action.Target)); } catch { }
            customShortcut.Text = h.Shortcut; customKind.SelectedIndex = (int)h.Action.Kind; customTarget.Text = h.Action.Target; customArguments.Text = h.Action.Arguments; customWorking.Text = h.Action.WorkingDirectory; customMedia.SelectedIndex = Array.IndexOf(Shortcuts.MediaLabels.Keys.ToArray(), h.Action.Target); customSimpleKind.SelectedIndex = GroupFor(h.Action, true); PopulateChoices(customSpecificKind, customSimpleKind.SelectedIndex, true, h.Action); ConfigureCustomFields(false); customNote.Text = NoteForCustom(index); loading = false;
        }
        void ConfigureCustomFields(bool reset)
        {
            ActionKind k = (ActionKind)Math.Max(0, customKind.SelectedIndex);
            bool launch = (k >= ActionKind.Application && k <= ActionKind.Command) || k == ActionKind.Python;
            bool args = k == ActionKind.Application || k == ActionKind.Command || k == ActionKind.Python || k == ActionKind.HttpRequest;
            customTargetField.Visible = launch || k == ActionKind.SendKey || k == ActionKind.SendShortcut || k == ActionKind.HttpRequest;
            customArgsField.Visible = customWorkField.Visible = args;
            customArguments.Enabled = customWorking.Enabled = args;
            customBrowse.Visible = launch;
            customActionRecord.Visible = k == ActionKind.SendKey || k == ActionKind.SendShortcut;
            sequenceField.Visible = k == ActionKind.Sequence;
            customConditionalButton.Visible = k == ActionKind.Conditional;
            sequenceSummary.Text = sequenceSteps.Count == 0 ? "Add actions and waits in the sequence builder." :
                String.Join("\n", sequenceSteps.Select((s, i) => (i + 1) + ".  " + s.Summary));
            customHelp.Text = k == ActionKind.LockThenSleep ? "Locks Windows, then puts the computer to sleep." :
                k == ActionKind.Sequence ? "Steps run from top to bottom. Open Build sequence to edit them." :
                k == ActionKind.Conditional ? "Checks local application state only when this hotkey is pressed, then runs one reviewed outcome." :
                k == ActionKind.Python ? "Choose a .py file. It runs with your normal account when this hotkey is pressed." :
                k == ActionKind.SystemAction ? "Runs this Windows or app integration when the hotkey is pressed." :
                k == ActionKind.HttpRequest ? "Calls this URL. Leave the body empty for GET, or enter a JSON body for POST." :
                k == ActionKind.SendKey || k == ActionKind.SendShortcut ? "Enter a key or shortcut, like Enter or Ctrl+Shift+S." :
                k == ActionKind.Media ? "This shortcut controls your media or Windows volume." :
                "Choose a local file. Save changes to activate this shortcut.";
            if (reset) { customTarget.Text = ""; customArguments.Text = ""; customWorking.Text = ""; customMedia.SelectedIndex = 0; }
            UiStyle.Wrap(customStack);
        }
        void CustomEdited()
        {
            if (loading || customSelected < 0 || customSelected >= draft.CustomHotkeys.Length || customKind.SelectedIndex < 0) return;
            ActionKind k = (ActionKind)customKind.SelectedIndex;
            string targetValue = k == ActionKind.Media ? Shortcuts.MediaLabels.Keys.ElementAt(Math.Max(0, customMedia.SelectedIndex)) : (k == ActionKind.Sequence ? (sequenceSteps.Count == 0 ? "" : SequenceCodec.Serialize(sequenceSteps)) : (k == ActionKind.PassThrough || k == ActionKind.Unbound || k == ActionKind.LockThenSleep ? "" : customTarget.Text.Trim()));
            draft.CustomHotkeys[customSelected] = new CustomHotkey { Shortcut = customShortcut.Text.Trim(), Action = new Mapping { Kind = k, Target = targetValue, Arguments = customArguments.Enabled ? customArguments.Text : "", WorkingDirectory = customWorking.Enabled ? customWorking.Text.Trim() : "" } };
            if (customSelected < customList.Items.Count) { customList.Items[customSelected].Text = customShortcut.Text.Trim(); customList.Items[customSelected].SubItems[1].Text = Summary(draft.CustomHotkeys[customSelected].Action); }
            MarkDirty();
        }
        void BrowseCustomTarget(object sender, EventArgs e)
        {
            using (var d = new OpenFileDialog { CheckFileExists = true }) { ActionKind k = (ActionKind)customKind.SelectedIndex; d.Filter = k == ActionKind.Python ? "Python scripts (*.py)|*.py" : k == ActionKind.Application ? "Applications (*.exe)|*.exe" : k == ActionKind.WindowsShortcut ? "Windows shortcuts (*.lnk)|*.lnk" : k == ActionKind.Command ? "Commands and scripts|*.exe;*.cmd;*.bat;*.ps1|All files|*.*" : "All files|*.*"; if (d.ShowDialog(this) == DialogResult.OK) customTarget.Text = d.FileName; }
        }
        void PopulateList()
        {
            loading = true; list.BeginUpdate(); list.Items.Clear();
            Mapping[] mappings = CurrentMappings();
            for (int i = 0; i < 12; i++) { var item = new ListViewItem("F" + (i + 1)); item.SubItems.Add(Summary(mappings[i])); list.Items.Add(item); }
            list.Items[selected].Selected = true; list.EndUpdate(); loading = false;
        }
        void RecordShortcut(TextBox destination, bool requireModifier)
        {
            using (var dialog = new ShortcutCaptureForm(requireModifier)) if (dialog.ShowDialog(this) == DialogResult.OK) destination.Text = dialog.Result;
        }
        Mapping[] CurrentMappings() { return selectedLayer < 0 || selectedLayer >= draft.Layers.Length ? draft.Mappings : draft.Layers[selectedLayer].Mappings; }
        void RefreshLayerView()
        {
            bool old = loading; loading = true; layerView.Items.Clear(); layerView.Items.Add("Base layer");
            foreach (var layer in draft.Layers) layerView.Items.Add(layer.Name + "  ·  hold " + LayerKeys.Label(layer.ActivationKey));
            if (selectedLayer >= draft.Layers.Length) selectedLayer = -1; layerView.SelectedIndex = selectedLayer + 1; loading = old;
        }
        void SelectLayerView(int layerIndex)
        {
            if (loading) return; selectedLayer = layerIndex >= 0 && layerIndex < draft.Layers.Length ? layerIndex : -1; PopulateList(); LoadEditor(selected);
            SetFeedback(selectedLayer < 0 ? "Editing the base function row." : "Editing " + draft.Layers[selectedLayer].Name + ". Hold " + LayerKeys.Label(draft.Layers[selectedLayer].ActivationKey) + " to use it.", false);
        }
        void ManageLayers()
        {
            using (var dialog = new LayerManagerForm(draft.Layers)) if (dialog.ShowDialog(this) == DialogResult.OK) {
                draft.Layers = dialog.Result; if (selectedLayer >= draft.Layers.Length) selectedLayer = draft.Layers.Length - 1;
                RefreshLayerView(); PopulateList(); LoadEditor(selected); MarkDirty();
            }
        }
        void OpenConditionalBuilder(bool custom)
        {
            Mapping current = custom ? (customSelected >= 0 && customSelected < draft.CustomHotkeys.Length ? draft.CustomHotkeys[customSelected].Action : null) : CurrentMappings()[selected];
            using (var dialog = new ConditionalActionForm(current)) {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null) { if (custom && customSelected >= 0) LoadCustomEditor(customSelected); else if (!custom) LoadEditor(selected); return; }
                if (custom) { draft.CustomHotkeys[customSelected].Action = dialog.Result.Copy(); LoadCustomEditor(customSelected); CustomEdited(); }
                else { CurrentMappings()[selected] = dialog.Result.Copy(); LoadEditor(selected); Edited(); }
            }
        }
        string Summary(Mapping m) { try { return m.Summary; } catch { return "Choose action details"; } }
        internal static string ExplainShortcut(string shortcut, Mapping mapping, string context)
        {
            if (mapping == null) return shortcut + "\r\nNo action is configured.";
            var text = new System.Text.StringBuilder();
            text.AppendLine("Explain " + shortcut);
            if (!String.IsNullOrWhiteSpace(context)) text.AppendLine(context);
            text.AppendLine();
            text.AppendLine("Action: " + SummaryForExplanation(mapping));
            string maturity = ActionInsights.Maturity(mapping);
            text.AppendLine("Maturity: " + maturity);
            text.AppendLine(ActionInsights.MaturityExplanation(maturity));
            text.AppendLine("Dependencies: " + ActionInsights.Dependencies(mapping));
            text.AppendLine("Permission: " + ActionPrivacy.Risk(mapping));
            if (mapping.Kind == ActionKind.HttpRequest) text.AppendLine("Network policy: checked when the action is triggered.");
            if (mapping.Kind == ActionKind.Sequence) {
                try { text.AppendLine("Safety: " + ActionInsights.Sequence(SequenceCodec.Parse(mapping.Target)).Compact); }
                catch { text.AppendLine("Safety: sequence details could not be decoded safely."); }
            }
            return text.ToString().TrimEnd();
        }
        static string SummaryForExplanation(Mapping mapping) { try { return mapping.Summary; } catch { return "Choose action details"; } }
        void LoadEditor(int index)
        {
            loading = true; selected = index; Mapping m = CurrentMappings()[index]; editorTitle.Text = "F" + (index + 1); kind.SelectedIndex = (int)m.Kind;
            foreach (ListViewItem item in list.Items) item.Selected = item.Index == index;
            target.Text = m.Target; arguments.Text = m.Arguments; working.Text = m.WorkingDirectory; media.SelectedIndex = Array.IndexOf(Shortcuts.MediaLabels.Keys.ToArray(), m.Target); simpleKind.SelectedIndex = GroupFor(m, false); PopulateChoices(specificKind, simpleKind.SelectedIndex, false, m);
            LoadMonitorChoices(m.MonitorId, m.MonitorControl); monitorStep.Value = Math.Max(1, Math.Min(20, m.MonitorStep));
            ConfigureFields(false); functionNote.Text = NoteForFunction(index); loading = false;
        }
        string NoteForFunction(int index)
        {
            string layer = selectedLayer < 0 ? "Base" : draft.Layers[selectedLayer].Name; string value;
            return mappingNotes.TryGetValue(MappingNoteStore.FunctionKey(currentProfile, layer, index), out value) ? value : "";
        }
        string NoteForCustom(int index)
        {
            string value; return mappingNotes.TryGetValue(MappingNoteStore.CustomKey(currentProfile, index), out value) ? value : "";
        }
        void SaveFunctionNote(object sender, EventArgs e)
        {
            if (loading) return; string layer = selectedLayer < 0 ? "Base" : draft.Layers[selectedLayer].Name;
            SaveNote(MappingNoteStore.FunctionKey(currentProfile, layer, selected), functionNote.Text);
        }
        void SaveCustomNote(object sender, EventArgs e)
        {
            if (loading || customSelected < 0) return; SaveNote(MappingNoteStore.CustomKey(currentProfile, customSelected), customNote.Text);
        }
        void SaveNote(string key, string value)
        {
            try { MappingNoteStore.Set(mappingNotes, key, value); MappingNoteStore.Save(MappingNoteStore.Path, mappingNotes); }
            catch (Exception ex) { SetFeedback("Private note was not saved: " + ex.Message, true); }
        }
        void ConfigureFields(bool reset)
        {
            bool wasLoading = loading; loading = true;
            ActionKind k = (ActionKind)Math.Max(0, kind.SelectedIndex);
            bool isMonitor = k == ActionKind.Monitor;
            bool launch = (k >= ActionKind.Application && k <= ActionKind.Command) || k == ActionKind.Python;
            bool canArgs = k == ActionKind.Application || k == ActionKind.Command || k == ActionKind.Python || k == ActionKind.HttpRequest;
            monitorPanel.Visible = isMonitor;
            functionTargetField.Visible = launch || k == ActionKind.SendKey || k == ActionKind.SendShortcut || k == ActionKind.HttpRequest;
            functionArgsField.Visible = functionWorkField.Visible = canArgs;
            arguments.Enabled = working.Enabled = canArgs;
            browse.Visible = launch; folder.Visible = k == ActionKind.FileOrFolder;
            functionRecord.Visible = k == ActionKind.SendKey || k == ActionKind.SendShortcut;
            functionConditionalButton.Visible = k == ActionKind.Conditional;
            if (reset) { target.Text = ""; arguments.Text = ""; working.Text = ""; media.SelectedIndex = 0; }
            if (reset && isMonitor) { LoadMonitorChoices("", ""); monitorStep.Value = 5; }
            hint.Text = k == ActionKind.PassThrough ? "This key keeps its normal Windows and app behavior." :
                k == ActionKind.Unbound ? "This key does nothing while shortcuts are enabled." :
                k == ActionKind.LockThenSleep ? "Locks Windows, then puts the computer to sleep." :
                k == ActionKind.Media ? "Controls your media or Windows volume. Hold volume keys to repeat." :
                k == ActionKind.Monitor ? "Adjusts the selected monitor directly. Hold the key to repeat." :
                k == ActionKind.Python ? "Choose a .py file. It runs with your normal account." :
                k == ActionKind.SystemAction ? "Runs this Windows or app integration." :
                k == ActionKind.HttpRequest ? "Calls this URL. Leave the body empty for GET, or enter a JSON body for POST." :
                k == ActionKind.Conditional ? "Checks local application state only when this key is pressed, then runs one reviewed outcome." :
                k == ActionKind.SendKey || k == ActionKind.SendShortcut ? "Enter a key or shortcut, like Enter or Ctrl+Shift+S." :
                "Choose a local file. Save changes to activate this action.";
            UiStyle.Wrap(functionStack); loading = wasLoading;
        }
        void Edited()
        {
            if (loading || kind.SelectedIndex < 0) return;
            var k = (ActionKind)kind.SelectedIndex;
            if (k == ActionKind.Monitor) {
                var device = monitor.SelectedItem as DdcMonitor; var operation = monitorControl.SelectedItem as DdcOperation;
                CurrentMappings()[selected] = new Mapping { Kind = k, MonitorId = device == null ? "" : device.Id, MonitorControl = operation == null ? "" : operation.Id, MonitorStep = (int)monitorStep.Value };
            } else CurrentMappings()[selected] = new Mapping { Kind = k, Target = k == ActionKind.Media ? Shortcuts.MediaLabels.Keys.ElementAt(Math.Max(0, media.SelectedIndex)) : (k == ActionKind.PassThrough || k == ActionKind.Unbound || k == ActionKind.LockThenSleep ? "" : target.Text.Trim()), Arguments = arguments.Enabled ? arguments.Text : "", WorkingDirectory = working.Enabled ? working.Text.Trim() : "" };
            list.Items[selected].SubItems[1].Text = Summary(CurrentMappings()[selected]); MarkDirty();
        }
        void BuildMonitorEditor()
        {
            monitorPanel.AutoSize = true; monitorPanel.Dock = DockStyle.Top; monitorPanel.ColumnCount = 1; monitorPanel.Margin = new Padding(0, 0, 0, 8);
            monitorPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            UiStyle.Combo(monitor); monitor.AccessibleName = "DDC monitor"; monitorPanel.Controls.Add(UiStyle.Field("Monitor", monitor));
            UiStyle.Combo(monitorControl); monitorControl.AccessibleName = "Monitor control"; monitorPanel.Controls.Add(UiStyle.Field("Control", monitorControl));
            var steps = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false, Margin = new Padding(0) };
            steps.Controls.Add(LabelText("Step (%)", 10, muted)); monitorStep.Width = 58; monitorStep.AccessibleName = "Monitor adjustment percent"; steps.Controls.Add(monitorStep);
            detect.Text = "Detect monitors"; detect.AutoSize = true; detect.Margin = new Padding(12, 0, 0, 0); detect.Click += delegate { DetectMonitors(); }; steps.Controls.Add(detect); monitorPanel.Controls.Add(steps);
            monitorStatus.AutoSize = true; monitorStatus.Dock = DockStyle.Top; monitorStatus.ForeColor = muted; monitorStatus.Margin = new Padding(0, 8, 0, 4); monitorPanel.Controls.Add(monitorStatus);
            monitor.SelectedIndexChanged += delegate { if (!loading) { bool old = loading; loading = true; LoadMonitorControls(""); loading = old; Edited(); } };
            monitorControl.SelectedIndexChanged += delegate { Edited(); }; monitorStep.ValueChanged += delegate { Edited(); };
        }
        void LoadMonitorChoices(string id, string controlId)
        {
            monitor.Items.Clear(); monitor.Items.AddRange(detected);
            var chosen = detected.FirstOrDefault(m => m.Id == id);
            if (chosen == null && !String.IsNullOrEmpty(id)) {
                chosen = new DdcMonitor { Id = id, Name = "Saved monitor", Status = "Saved monitor is unavailable. Reconnect it and Detect monitors, or choose a connected monitor." }; monitor.Items.Add(chosen);
            }
            if (chosen == null && String.IsNullOrEmpty(id)) chosen = detected.FirstOrDefault(m => m.Codes.Length > 0);
            if (chosen != null) monitor.SelectedItem = chosen;
            LoadMonitorControls(controlId);
        }
        void LoadMonitorControls(string id)
        {
            monitorControl.Items.Clear(); var device = monitor.SelectedItem as DdcMonitor;
            if (device != null) monitorControl.Items.AddRange(DdcOperation.All.Where(o => device.Codes.Contains(o.Code)).ToArray());
            var choice = monitorControl.Items.Cast<DdcOperation>().FirstOrDefault(o => o.Id == id);
            // Preserve existing unavailable mappings instead of silently changing their action.
            if (choice == null && DdcOperation.Find(id) != null) { var original = DdcOperation.Find(id); choice = new DdcOperation { Id = id, Label = original.Label + " (unavailable)", Code = original.Code, Direction = original.Direction }; monitorControl.Items.Add(choice); }
            if (choice != null) monitorControl.SelectedItem = choice; else if (monitorControl.Items.Count > 0) monitorControl.SelectedIndex = 0;
            monitorStatus.Text = scanning ? "Detecting monitors..." : device == null ? "No supported monitor detected. Check DDC/CI in the monitor menu, then Detect monitors." : device.Status;
        }
        async void DetectMonitors()
        {
            if (scanning) return; scanning = true; detect.Enabled = false; monitorStatus.Text = "Detecting monitors...";
            try {
                var devices = await Task.Run(() => DdcService.Shared.Scan());
                if (IsDisposed) return; detected = devices; scanning = false;
                loading = true; var m = CurrentMappings()[selected]; LoadMonitorChoices(m.MonitorId, m.MonitorControl); loading = false;
                if ((ActionKind)kind.SelectedIndex == ActionKind.Monitor && String.IsNullOrEmpty(m.MonitorId)) Edited();
            } catch (Exception ex) { if (!IsDisposed) monitorStatus.Text = "Detection failed: " + ex.Message; }
            finally { scanning = false; if (!IsDisposed) { detect.Enabled = true; RunConfigurationHealthCheck(); } }
        }
        void MarkDirty() { dirty = true; Text = "KiWeave *"; SetFeedback("Unsaved changes. Save to apply them. The enable switch uses your saved mappings.", false); if (!isPreview) { draftTimer.Stop(); draftTimer.Start(); } }
        void SetFeedback(string text, bool error) { feedback.Text = text; feedback.ForeColor = error ? Color.FromArgb(255, 151, 153) : muted; }
        void BrowseTarget(object sender, EventArgs e)
        {
            using (var d = new OpenFileDialog { CheckFileExists = true, DereferenceLinks = false }) {
                ActionKind k = (ActionKind)kind.SelectedIndex;
                d.Filter = k == ActionKind.Application ? "Applications (*.exe)|*.exe" : k == ActionKind.WindowsShortcut ? "Windows shortcuts (*.lnk)|*.lnk" : k == ActionKind.Command ? "Commands and scripts|*.exe;*.cmd;*.bat;*.ps1|All files|*.*" : "All files|*.*";
                if (d.ShowDialog(this) == DialogResult.OK) target.Text = d.FileName;
            }
        }
        void ToggleEnabled(object sender, EventArgs e)
        {
            if (loading) return;
            if (engine == null || !engine.Installed) { loading = true; enabled.Checked = false; loading = false; SetFeedback("The keyboard hook is unavailable. Exit and reopen the app to retry.", true); return; }
            bool value = enabled.Checked;
            // Disable immediately even if saving the preference fails.
            if (!value) engine.SetEnabled(false);
            Configuration next = saved.Copy(); next.Enabled = value;
            try { CaptureHistory("enable setting"); PersistCurrent(next); saved = next; draft.Enabled = value; engine.SetEnabled(value); ApplyHotkeys(saved); SetFeedback(value ? "Remapping and custom hotkeys enabled using saved rules." : "Remapping and custom hotkeys are off.", false); }
            catch (Exception ex) { loading = true; enabled.Checked = engine.Enabled; loading = false; SetFeedback("Could not save enable setting: " + ex.Message, true); }
            UpdateStatus();
        }
        void EmergencyOff()
        {
            loading = true; enabled.Checked = false; loading = false; saved.Enabled = draft.Enabled = false;
            try { CaptureHistory("emergency bypass"); PersistCurrent(saved); SetFeedback("Emergency bypass activated. Remapping is off. Release any held function keys.", false); }
            catch (Exception ex) { SetFeedback("Remapping is off, but the preference could not be saved: " + ex.Message, true); }
            UpdateStatus(); if (preferences.UseTray && notificationPreferences.Safety && notificationPreferences.AllowsWarning) tray.ShowBalloonTip(3000, "Remapping is off", "Emergency bypass activated.", ToolTipIcon.Info);
        }
        bool Save()
        {
            if (readOnlyMode) { SetFeedback("Read-only mode is active. Unlock editing before saving.", true); return false; }
            if (isPreview) { SetFeedback("Design preview only. Nothing was saved or activated.", false); return true; }
            if ((functionPage.Visible && simpleKind.SelectedIndex == 6 && ReferenceEquals(specificKind.SelectedItem, ChooseAction)) ||
                (customPage.Visible && customHotkeyView.Visible && customSimpleKind.SelectedIndex == 3 && ReferenceEquals(customSpecificKind.SelectedItem, ChooseAction))) {
                SetFeedback("Choose an action first. Nothing was changed.", true); return false;
            }
            Configuration previous = saved.Copy(); bool persisted = false;
            try {
                ConfigStore.Validate(draft, true); draft.Enabled = engine != null && engine.Enabled;
                if (dirty) CaptureHistory("mapping save"); PersistCurrent(draft); saved = draft.Copy();
                persisted = true;
                if (engine != null) engine.Apply(saved); ApplyHotkeys(saved); PopulateCustomList();
                dirty = false; Text = "KiWeave";
                try { if (startup.Checked != Startup.Enabled || (startup.Checked && !Startup.IsCurrent)) Startup.Set(startup.Checked); }
                catch (Exception ex) { dirty = true; SetFeedback("Mappings saved, but startup setting failed: " + ex.Message, true); return false; }
                RecoveryStore.DeleteDraft();
                try { RecoveryStore.SaveKnownGoodCurrent(); } catch (Exception ex) { AppLog.Record("KnownGoodRecovery", ex); }
                AuditTrail.Record("configuration-save"); SetFeedback("Saved. " + (saved.Enabled ? "Your mappings are active." : "Turn on Shortcuts enabled when you are ready."), false); return true;
            } catch (Exception ex) {
                if (persisted) try { PersistCurrent(previous); saved = previous.Copy(); draft = previous.Copy(); if (engine != null) { engine.Apply(previous); ApplyHotkeys(previous); } dirty = false; } catch (Exception rollback) { AppLog.Record("ConfigurationRollback", rollback); }
                SetFeedback(persisted ? "Activation failed; KiWeave rolled back to the previous saved configuration. " + ex.Message : "Could not save: " + ex.Message, true); return false;
            }
        }
        void Bulk(bool unbound)
        {
            for (int i = 0; i < 12; i++) CurrentMappings()[i] = new Mapping { Kind = unbound ? ActionKind.Unbound : ActionKind.PassThrough };
            PopulateList(); LoadEditor(selected); MarkDirty();
        }
        void Export(object sender, EventArgs e)
        {
            try {
                ConfigStore.Validate(draft, false);
                using (var d = new SaveFileDialog { Filter = "JSON configuration|*.json", FileName = "function-row.json", DefaultExt = "json", AddExtension = true })
                    if (d.ShowDialog(this) == DialogResult.OK) { ConfigStore.Save(d.FileName, draft); AuditTrail.Record("configuration-export"); SetFeedback("Exported your draft mappings. Export does not activate changes or change Windows startup.", false); }
            } catch (Exception ex) { SetFeedback("Export failed: " + ex.Message, true); }
        }
        void Import(object sender, EventArgs e)
        {
            using (var d = new OpenFileDialog { Filter = "JSON configuration|*.json", CheckFileExists = true }) {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try {
                    string migration = ConfigStore.MigrationPreview(d.FileName);
                    if (!migration.StartsWith("This configuration is already", StringComparison.Ordinal) && MessageBox.Show(this, migration + "\r\n\r\nContinue to preview the imported configuration?", "Migration preview", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
                    Configuration imported = ConfigStore.Load(d.FileName);
                    string importDiff = ConfigurationHistory.Compare(new KeyWeaveBackup { Configuration = imported, Profiles = profiles, Preferences = preferences, StartWithWindows = Startup.Enabled }, saved, profiles, preferences, Startup.Enabled);
                    if (MessageBox.Show(this, "Redacted comparison with the current saved setup:\r\n\r\n" + importDiff + "\r\n\r\nContinue to the full import review?", "Import comparison", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
                    using (var review = new ImportReviewForm(imported, d.FileName)) if (review.ShowDialog(this) != DialogResult.OK || !review.Approved) { SetFeedback("Import cancelled. Your editor and active mappings are unchanged.", false); return; } else imported = review.SelectedConfiguration;
                    // Imported enabled state never changes the live toggle, and imports cannot add startup entries.
                    imported.Enabled = saved.Enabled; draft = imported; selectedLayer = -1; customSelected = -1; RefreshLayerView(); PopulateList(); PopulateCustomList(); LoadEditor(selected);
                    if (draft.CustomHotkeys.Length > 0) LoadCustomEditor(0); else SetCustomEditorState(false);
                    MarkDirty();
                    AuditTrail.Record("configuration-import-staged"); SetFeedback("Imported into the editor. Review all targets and commands, then Save to apply. Nothing has been run.", false); CheckMissingTargets();
                } catch (Exception ex) { SetFeedback("Import rejected; current mappings are unchanged. " + ex.Message, true); }
            }
        }
        void ExportActionPack(object sender, EventArgs e)
        {
            try {
                ConfigStore.Validate(draft, false);
                var pack = new ActionPack { Id = "local.kiweave-pack", Name = "KiWeave local action pack", Publisher = "Local user", Description = "Exported from this KiWeave setup.", Configuration = draft.Copy(), Profiles = profiles.Copy() };
                using (var d = new SaveFileDialog { Filter = "KiWeave action pack|*.kiweavepack", FileName = "kiweave-action-pack.kiweavepack", DefaultExt = "kiweavepack", AddExtension = true })
                    if (d.ShowDialog(this) == DialogResult.OK) { ActionPackStore.Save(d.FileName, pack); AuditTrail.Record("action-pack-export"); SetFeedback("Exported a declarative action pack. It contains no scripts or command actions.", false); }
            } catch (Exception ex) { SetFeedback("Action-pack export failed: " + ex.Message, true); }
        }
        void ImportActionPack(object sender, EventArgs e)
        {
            using (var d = new OpenFileDialog { Filter = "KiWeave action packs|*.kiweavepack|All files|*.*", CheckFileExists = true }) {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try {
                    ActionPack pack = ActionPackStore.Load(d.FileName);
                    if (MessageBox.Show(this, "Action-pack trust label: Unverified declarative pack\r\n\r\nPublisher identity is informational and is not proof that the pack is harmless. KiWeave will review the visible actions before staging them.\r\n\r\nContinue?", "Action-pack trust", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    string packDiff = ConfigurationHistory.Compare(new KeyWeaveBackup { Configuration = pack.Configuration, Profiles = pack.Profiles, Preferences = preferences, StartWithWindows = Startup.Enabled }, saved, profiles, preferences, Startup.Enabled);
                    if (MessageBox.Show(this, "Redacted comparison with the current saved setup:\r\n\r\n" + packDiff + "\r\n\r\nContinue to the action-pack safety review?", "Action-pack comparison", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
                    using (var review = new ImportReviewForm(pack.Configuration, d.FileName)) if (review.ShowDialog(this) != DialogResult.OK || !review.Approved) { SetFeedback("Action-pack import cancelled. Active mappings are unchanged.", false); return; } else pack.Configuration = review.SelectedConfiguration;
                    bool importProfiles = false;
                    if (pack.Profiles != null && pack.Profiles.Profiles.Length > 0) {
                        using (var wizard = new ProfileConflictWizardForm(profiles, pack.Profiles)) if (wizard.ShowDialog(this) == DialogResult.OK) { ProfileCollection nextProfiles = wizard.Result; CaptureHistory("action pack profiles"); ProfileStore.Save(ProfileStore.DefaultPath, nextProfiles); profiles = nextProfiles; importProfiles = true; currentProfile = "Default"; pinnedProfile = ""; automaticProfileActive = false; RefreshTrayProfiles(); }
                    }
                    pack.Configuration.Enabled = saved.Enabled; draft = pack.Configuration.Copy(); selectedLayer = -1; customSelected = -1; RefreshLayerView(); PopulateList(); PopulateCustomList(); LoadEditor(selected);
                    if (draft.CustomHotkeys.Length > 0) LoadCustomEditor(0); else SetCustomEditorState(false);
                    AuditTrail.Record("action-pack-import-staged"); MarkDirty(); SetFeedback(importProfiles ? "Action pack mappings staged and reviewed profiles imported. Nothing was executed.": "Action pack staged for review. Nothing was executed or activated.", false); CheckMissingTargets();
                } catch (Exception ex) { SetFeedback("Action-pack import rejected; current mappings are unchanged. " + ex.Message, true); }
            }
        }
        void OpenScriptWorkspace(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = "Supported scripts|*.py;*.ps1;*.cmd;*.bat;*.js;*.lua;*.rb;*.ahk|All files|*.*", CheckFileExists = true, Title = "Open a script for review" })
                if (dialog.ShowDialog(this) == DialogResult.OK) try { ScriptWorkspace.Open(dialog.FileName); SetFeedback("Opened the script in VS Code or your default editor. KiWeave did not execute it.", false); } catch (Exception ex) { SetFeedback("The script could not be opened: " + ex.Message, true); }
        }
        void ShowActionExplanation()
        {
            MessageBox.Show(this, ActionExplanation.Latest, "Why did this action run?", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        void EnablePortableData(object sender, EventArgs e)
        {
            if (AppStorage.IsPortable) { SetFeedback("Portable data mode is already active. Restart KiWeave to use the portable folder.", false); return; }
            if (MessageBox.Show(this, "Copy KiWeave's current local data beside the executable and use that folder after restart? The original AppData copy will be preserved.", "Portable data mode", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
            try { AppStorage.EnablePortableMode(); SetFeedback("Portable data prepared. Restart KiWeave to switch locations.", false); }
            catch (Exception ex) { SetFeedback("Portable data could not be prepared: " + ex.Message, true); }
        }
        void OpenPathMigration(object sender, EventArgs e)
        {
            var candidates = PathMigration.Find(draft); if (candidates.Count == 0) { SetFeedback("No path-bearing mappings were found in the current draft.", false); return; }
            using (var oldDialog = new FolderBrowserDialog { Description = "Choose the old app or scripts folder" }) if (oldDialog.ShowDialog(this) == DialogResult.OK)
                using (var newDialog = new FolderBrowserDialog { Description = "Choose the new app or scripts folder" }) if (newDialog.ShowDialog(this) == DialogResult.OK) {
                    var preview = candidates.Where(x => x.Value.StartsWith(oldDialog.SelectedPath, StringComparison.OrdinalIgnoreCase)).Take(12).Select(x => x.Location + "\r\n  " + x.Value).ToArray(); if (preview.Length == 0) { SetFeedback("No saved paths start with that old folder.", true); return; }
                    if (MessageBox.Show(this, "KiWeave will update " + candidates.Count(x => x.Value.StartsWith(oldDialog.SelectedPath, StringComparison.OrdinalIgnoreCase)) + " saved path value(s). Preview:\r\n\r\n" + String.Join("\r\n", preview) + (preview.Length == 12 ? "\r\n…" : "") + "\r\n\r\nStage these changes in the editor?", "Migrate app paths", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
                    int changed = PathMigration.Replace(draft, oldDialog.SelectedPath, newDialog.SelectedPath); PopulateList(); PopulateCustomList(); LoadEditor(selected); MarkDirty(); SetFeedback("Staged " + changed + " migrated path value(s). Save when you are ready.", false);
                }
        }
        void CheckMissingTargets()
        {
            for (int i = 0; i < 12; i++) try { ConfigStore.Validate(draft.Mappings[i], true); } catch (Exception ex) { SetFeedback("Check F" + (i + 1) + ": " + ex.Message, true); return; }
            foreach (var layer in draft.Layers) for (int i = 0; i < 12; i++) try { ConfigStore.Validate(layer.Mappings[i], true); } catch (Exception ex) { SetFeedback("Check " + layer.Name + " F" + (i + 1) + ": " + ex.Message, true); return; }
        }
        void RunConfigurationHealthCheck()
        {
            if (isPreview) return;
            Configuration configuration = draft.Copy(); ProfileCollection profileCopy = profiles.Copy(); DdcMonitor[] monitorCopy = detected == null ? new DdcMonitor[0] : detected.ToArray();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate {
                ConfigurationHealthReport report = ConfigurationHealth.Scan(configuration, profileCopy, monitorCopy);
                Ui(delegate { healthReport = report; if (report.HasWarnings && notificationPreferences.Health && notificationPreferences.AllowsWarning) SetFeedback("Configuration health: " + report.Summary, true); });
            });
        }
        void TestAction(Mapping mapping)
        {
            if (!featureFlags.DeveloperMode) { SetFeedback("Test actions are disabled until Developer mode is enabled in Settings.", true); return; }
            if (mapping == null || mapping.Kind == ActionKind.PassThrough || mapping.Kind == ActionKind.Unbound) { SetFeedback("Choose an action with an observable result before testing.", true); return; }
            try { ConfigStore.Validate(mapping, true); }
            catch (Exception ex) { SetFeedback("Cannot test this action: " + ex.Message, true); return; }
            if (MessageBox.Show(this, "Run this action once now?\n\n" + Summary(mapping) + "\n\nIt may open an app, send keys, contact a configured URL, change a device, or run every step in a sequence.", "Test action", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            Mapping copy = mapping.Copy(); System.Threading.ThreadPool.QueueUserWorkItem(delegate {
                try { customDispatcher.Execute(copy); Ui(delegate { SetFeedback("Test action completed.", false); }); }
                catch (Exception ex) { Ui(delegate { SetFeedback("Test action failed: " + ex.Message, true); }); }
            });
        }
        void ShowActionInfo(Mapping mapping)
        {
            if (mapping == null) return;
            string maturity = ActionInsights.Maturity(mapping);
            var text = new System.Text.StringBuilder(); text.AppendLine(mapping.Summary); text.AppendLine(); text.AppendLine("Maturity: " + maturity); text.AppendLine(ActionInsights.MaturityExplanation(maturity)); text.AppendLine(); text.AppendLine("Dependencies: " + ActionInsights.Dependencies(mapping)); text.AppendLine(); text.AppendLine("Permission: " + ActionPrivacy.Risk(mapping));
            if (mapping.Kind == ActionKind.HttpRequest) text.AppendLine("Master network access: " + (preferences.NetworkAccess ? "allowed" : "blocked"));
            if (mapping.Kind == ActionKind.Sequence) {
                try {
                    var steps = SequenceCodec.Parse(mapping.Target); text.AppendLine(ActionInsights.Sequence(steps).Details);
                    text.AppendLine("This view does not run or validate the sequence through side effects.");
                } catch (Exception ex) { text.AppendLine("Sequence details are invalid: " + ex.Message); }
            } else if (mapping.Kind == ActionKind.Conditional) {
                try {
                    var rule = ConditionalCodec.Parse(mapping.Target);
                    text.AppendLine(); text.AppendLine("Condition: " + (rule.Condition == ConditionKind.ForegroundApplication ? "Foreground application is " : "Application is running: ") + rule.Application);
                    text.AppendLine("When matched: " + rule.WhenMatched.Summary + "  [" + ActionPrivacy.Risk(rule.WhenMatched) + "]");
                    text.AppendLine("Otherwise: " + rule.Otherwise.Summary + "  [" + ActionPrivacy.Risk(rule.Otherwise) + "]");
                    text.AppendLine("The application state is checked only when the assigned key or hotkey is pressed.");
                } catch (Exception ex) { text.AppendLine("Conditional details are invalid: " + ex.Message); }
                text.AppendLine("This view does not run either outcome.");
            } else text.AppendLine("This view does not run, launch, send, or contact anything.");
            MessageBox.Show(this, text.ToString(), "Action information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        void ApplyHotkeys(Configuration config)
        {
            if (!config.Enabled) {
                foreach (int id in registeredHotkeys.Keys.ToArray()) Native.UnregisterHotKey(Handle, id);
                registeredHotkeys.Clear(); return;
            }
            var keep = new HashSet<int>();
            for (int i = 0; i < config.CustomHotkeys.Length; i++) {
                int id = 3000 + i; CustomHotkey old;
                if (registeredHotkeys.TryGetValue(id, out old) && String.Equals(HotkeyChord.Normalize(old.Shortcut), HotkeyChord.Normalize(config.CustomHotkeys[i].Shortcut), StringComparison.OrdinalIgnoreCase)) {
                    registeredHotkeys[id] = config.CustomHotkeys[i].Copy(); keep.Add(id);
                }
            }
            foreach (int id in registeredHotkeys.Keys.ToArray()) if (!keep.Contains(id)) { Native.UnregisterHotKey(Handle, id); registeredHotkeys.Remove(id); }
            try {
                for (int i = 0; i < config.CustomHotkeys.Length; i++) {
                    int id = 3000 + i; if (keep.Contains(id)) continue;
                    HotkeyChord chord = HotkeyChord.Parse(config.CustomHotkeys[i].Shortcut);
                    if (!Native.RegisterHotKey(Handle, id, (uint)(chord.Modifiers | HotkeyChord.NoRepeat), (uint)chord.Key)) {
                        int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                        string shortcut = HotkeyChord.Normalize(config.CustomHotkeys[i].Shortcut);
                        if (error == 1409) throw new ArgumentException("Could not register " + shortcut + ". Windows or another app already owns this shortcut. Choose a different combination or close the conflicting app.");
                        throw new System.ComponentModel.Win32Exception(error, "Could not register " + shortcut + ". Windows rejected this shortcut.");
                    }
                    registeredHotkeys.Add(id, config.CustomHotkeys[i].Copy());
                }
            } catch {
                foreach (int id in registeredHotkeys.Keys.ToArray()) Native.UnregisterHotKey(Handle, id);
                registeredHotkeys.Clear(); throw;
            }
        }
        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY) {
                CustomHotkey h;
                if (registeredHotkeys.TryGetValue(m.WParam.ToInt32(), out h) && engine != null && engine.Enabled)
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate { try { customDispatcher.Execute(h.Action); } catch (Exception ex) { Ui(delegate { SetFeedback("Custom hotkey could not run: " + ex.Message, true); }); } });
                return;
            }
            base.WndProc(ref m);
        }
        void SetupTray()
        {
            var menu = Design.DarkMenu(Font);
            menu.Items.Add("Open settings", null, delegate { ShowSettings(); });
            trayToggle.Click += delegate { enabled.Checked = !enabled.Checked; }; menu.Items.Add(trayToggle);
            trayWhyProfile.Click += delegate { OpenProfileStatus(); }; menu.Items.Add(trayWhyProfile);
            trayPinProfile.Click += delegate { ToggleProfilePin(); }; menu.Items.Add(trayPinProfile);
            trayLayer.Enabled = false; menu.Items.Add(trayLayer);
            menu.Items.Add(trayProfiles); menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Exit", null, delegate { ExitApp(); });
            tray.Icon = Program.TrayIcon(); tray.Text = "KiWeave"; tray.ContextMenuStrip = menu; tray.Visible = preferences.UseTray; tray.DoubleClick += delegate { ShowSettings(); };
            hideToTray.Enabled = preferences.UseTray; RefreshTrayProfiles();
        }
        void ToggleTray(object sender, EventArgs e)
        {
            if (loading) return;
            if (isPreview) return;
            var next = NewPreferencesFromUi();
            try {
                UserPreferences.Save(UserPreferences.DefaultPath, next); preferences = next;
                tray.Visible = next.UseTray; hideToTray.Enabled = next.UseTray;
                if (!next.UseTray && !Visible) ShowSettings();
                SetFeedback(next.UseTray ? "Tray enabled. Closing this window keeps remapping running. Use Exit to quit." : "Tray disabled. Closing this window exits the app and stops remapping.", false);
            } catch (Exception ex) { loading = true; useTray.Checked = preferences.UseTray; loading = false; SetFeedback("Could not save tray preference: " + ex.Message, true); }
        }
        UserPreferences NewPreferencesFromUi()
        {
            return new UserPreferences { UseTray = useTray.Checked, CheckUpdates = checkUpdates.Checked, AutomaticProfiles = automaticProfiles.Checked, NetworkAccess = networkAccess.Checked, Theme = UiStyle.ThemeName, CustomAccent = preferences.CustomAccent, HistoryRetention = (int)historyRetention.Value };
        }
        void ToggleBackgroundPreference(object sender, EventArgs e)
        {
            if (loading || isPreview) return;
            var next = NewPreferencesFromUi();
            if (next.NetworkAccess != preferences.NetworkAccess && !Program.RequestElevatedNetworkChange(next.NetworkAccess)) {
                loading = true; networkAccess.Checked = preferences.NetworkAccess; loading = false; SetFeedback("Administrator approval is required to change the master network switch.", true); return;
            }
            try {
                UserPreferences.Save(UserPreferences.DefaultPath, next); preferences = next;
                NetworkPolicy.Enabled = next.NetworkAccess;
                DiscordIntegration.SetNetworkAccess(NetworkPolicy.Enabled);
                checkUpdates.Enabled = next.NetworkAccess;
                UpdateAutomaticCheckTimer();
                if (!next.AutomaticProfiles) { automaticProfileActive = false; UpdateStatus(); }
                SetFeedback(next.NetworkAccess ? "Settings updated. Approved network features may connect when triggered." : "Network access blocked. Local remapping remains available.", false);
            } catch (Exception ex) {
                loading = true; checkUpdates.Checked = preferences.CheckUpdates; automaticProfiles.Checked = preferences.AutomaticProfiles; networkAccess.Checked = preferences.NetworkAccess; checkUpdates.Enabled = preferences.NetworkAccess; loading = false;
                SetFeedback("Could not save settings: " + ex.Message, true);
            }
        }
        void ToggleFeatureFlag(object sender, EventArgs e)
        {
            if (loading || isPreview) return;
            try { featureFlags.ExperimentalEnabled = experimentalFeatures.Checked; featureFlags.DeveloperMode = developerMode.Checked; featureFlags.Save(); SetFeedback("Feature safety settings saved.", false); }
            catch (Exception ex) { loading = true; experimentalFeatures.Checked = featureFlags.ExperimentalEnabled; developerMode.Checked = featureFlags.DeveloperMode; loading = false; SetFeedback("Feature safety settings could not be saved: " + ex.Message, true); }
        }
        void ToggleGameMode(object sender, EventArgs e)
        {
            if (loading || isPreview) return; try { GameMode.Set(gameMode.Checked); SetFeedback(gameMode.Checked ? "Game mode enabled. Fullscreen foreground windows receive normal keys." : "Game mode disabled.", false); } catch (Exception ex) { loading = true; gameMode.Checked = GameMode.Enabled; loading = false; SetFeedback("Game mode could not be saved: " + ex.Message, true); }
        }
        void ToggleNotificationPreference(object sender, EventArgs e)
        {
            if (loading || isPreview || notificationPreferences == null) return;
            notificationPreferences.Updates = notifyUpdates.Checked; notificationPreferences.Health = notifyHealth.Checked; notificationPreferences.Safety = notifySafety.Checked; notificationPreferences.Severity = notificationSeverity.SelectedItem == null ? "All" : notificationSeverity.SelectedItem.ToString();
            try { notificationPreferences.Save(); SetFeedback("Notification preferences saved.", false); }
            catch (Exception ex) { SetFeedback("Notification preferences could not be saved: " + ex.Message, true); }
        }
        void ToggleUpdateChannel(object sender, EventArgs e)
        {
            if (loading || isPreview || updateChannel.SelectedItem == null) return;
            try { UpdateChannels.Save(updateChannel.SelectedItem.ToString()); SetFeedback("Update channel set to " + updateChannel.SelectedItem + ".", false); }
            catch (Exception ex) { SetFeedback("Update channel could not be saved: " + ex.Message, true); }
        }
        void ToggleStartup(object sender, EventArgs e)
        {
            if (loading || isPreview) return;
            try { Startup.Set(startup.Checked); SetFeedback(startup.Checked ? "KiWeave will start with Windows." : "KiWeave will no longer start with Windows.", false); }
            catch (Exception ex) {
                loading = true; try { startup.Checked = Startup.Enabled; } catch { startup.Checked = !startup.Checked; } loading = false;
                SetFeedback("Could not change the Windows startup setting: " + ex.Message, true);
            }
        }
        void CheckForUpdatesNow()
        {
            if (!preferences.NetworkAccess) { SetFeedback("Network access is blocked. Enable it in Settings before checking GitHub.", true); return; }
            SetFeedback("Checking for a KiWeave update...", false);
            UpdateChecker.CheckNow((tag, error) => Ui(delegate {
                if (error != null) { SetFeedback("Could not check for updates. Check your connection and try again.", true); return; }
                if (tag == null) { SetFeedback("You're up to date on the " + UpdateChannels.Load() + " channel. No newer public release was found.", false); return; }
                if (updateNotice != null) updateNotice.Dispose(); updateNotice = new UpdateNotification(tag);
                SetFeedback("KiWeave " + tag + " is available.", false);
            }), UpdateChannels.Load());
        }
        void UpdateAutomaticCheckTimer() { updateTimer.Enabled = !isPreview && preferences.NetworkAccess && preferences.CheckUpdates; }
        void RunAutomaticUpdateCheck()
        {
            if (!preferences.NetworkAccess || !preferences.CheckUpdates) return;
            RunConfigurationHealthCheck();
            UpdateChecker.CheckInBackground(tag => Ui(delegate { if (tag != null && notificationPreferences.Updates) { if (updateNotice != null) updateNotice.Dispose(); updateNotice = new UpdateNotification(tag); } }), UpdateChannels.Load());
        }
        void OpenDataFolder()
        {
            try {
                string folder = Path.GetDirectoryName(ConfigStore.DefaultPath); Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            } catch (Exception ex) { SetFeedback("Could not open the data folder: " + ex.Message, true); }
        }
        void ExitApp() { exitRequested = true; Close(); }
        void RestartApp()
        {
            if (dirty && !Save()) return;
            try { exitRequested = true; Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true }); Close(); }
            catch (Exception ex) { SetFeedback("KiWeave could not restart: " + ex.Message, true); exitRequested = false; }
        }
        internal void RequestShow() { Ui(ShowSettings); }
        internal void RequestAutomationCommand(string command)
        {
            Ui(delegate {
                if (command == "show-settings") { ShowSettings(); return; }
                if (command == "open-discord") using (var dialog = new DiscordConnectionForm(this)) dialog.ShowDialog(this);
            });
        }
        void ShowSettings() { Show(); WindowState = FormWindowState.Normal; Activate(); CheckMissingTargets(); }
        void UpdateStatus()
        {
            bool active = engine != null && engine.Installed;
            status.Text = (!active ? "Unavailable" : engine.Enabled ? "Active in the background" : "Shortcuts paused") + " · " + currentProfile + (pinnedProfile.Length > 0 ? " (pinned)" : automaticProfileActive ? " (automatic)" : "");
            status.ForeColor = active && engine.Enabled ? Color.FromArgb(127, 214, 169) : muted;
            profileBadge.Text = "Profile: " + currentProfile + (pinnedProfile.Length > 0 ? " · pinned" : automaticProfileActive ? " · auto" : "");
            profileBadge.Primary = pinnedProfile.Length > 0; profileBadge.Invalidate();
            tips.SetToolTip(profileBadge, status.Text + "\n" + profileReason + "\nClick for profile details.");
            trayToggle.Text = engine != null && engine.Enabled ? "Disable remapping" : "Enable remapping";
            trayWhyProfile.Text = "Active: " + currentProfile + " · Why?";
            trayPinProfile.Text = pinnedProfile.Length > 0 ? "Resume automatic switching" : "Pin current profile for this session";
            trayPinProfile.Checked = pinnedProfile.Length > 0;
            trayLayer.Text = "Layer: " + (engine == null ? "Base" : engine.ActiveLayerName);
            RefreshTrayProfileChecks();
        }
        void ExportBackup(object sender, EventArgs e)
        {
            if (dirty) { SetFeedback("Save or discard your edits before creating a full backup.", true); return; }
            if (MessageBox.Show(this, "A full backup contains your mappings, action targets, arguments, URLs, profiles, and preferences. Keep it private if any action contains personal or secret information.\n\nCreate the backup?", "Back up KiWeave", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
            try {
                BackupPrivacyPreset privacyPreset; using (var preset = new BackupPrivacyForm()) if (preset.ShowDialog(this) != DialogResult.OK) return; else privacyPreset = preset.Preset;
                using (var d = new SaveFileDialog { Filter = "KiWeave backup|*.keyweave", FileName = "KiWeave-" + DateTime.Now.ToString("yyyy-MM-dd") + ".keyweave", DefaultExt = "keyweave", AddExtension = true })
                    if (d.ShowDialog(this) == DialogResult.OK) {
                        string review = BackupPrivacy.Review(BackupBundle.Serialize(saved, profiles, preferences, Startup.Enabled), privacyPreset);
                        if (MessageBox.Show(this, "Privacy review\r\n\r\n" + review + "\r\n\r\nCreate this private backup?", "Review backup privacy", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
                        BackupBundle.Save(d.FileName, saved, profiles, preferences, Startup.Enabled); AuditTrail.Record("backup-export"); SetFeedback("Full backup created. PowerToys shortcuts were recorded as a read-only inventory.", false);
                    }
            } catch (Exception ex) { SetFeedback("Backup failed: " + ex.Message, true); }
        }
        void ImportBackup(object sender, EventArgs e)
        {
            if (readOnlyMode) { SetFeedback("Read-only mode is active. Unlock editing before restoring a backup.", true); return; }
            if (dirty) { SetFeedback("Save or discard your edits before restoring a backup.", true); return; }
            using (var d = new OpenFileDialog { Filter = "KiWeave backup|*.keyweave", CheckFileExists = true }) {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try {
                    KeyWeaveBackup backup = BackupBundle.Load(d.FileName);
                    string message = BackupBundle.Describe(backup) + "\n\nThis replaces the Default mappings, profiles, tray preference, and startup preference. PowerToys is not changed. KiWeave will create a local rollback copy first.\n\nContinue?";
                    if (MessageBox.Show(this, message, "Restore KiWeave backup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    CaptureHistory("backup restore"); string rollback = BackupBundle.Restore(backup); ApplyRestoredBackup(backup); AuditTrail.Record("backup-restore"); SetFeedback("Backup restored. Rollback copy: " + rollback, false);
                } catch (Exception ex) { SetFeedback("Restore failed; the current setup was kept or rolled back. " + ex.Message, true); }
            }
        }

        void CaptureHistory(string reason)
        {
            if (isPreview) return;
            try { ConfigurationHistory.CaptureCurrent(reason); }
            catch (Exception ex) { AppLog.Record("ConfigurationHistory", ex); }
        }
        void ApplyRestoredBackup(KeyWeaveBackup backup)
        {
            saved = backup.Configuration.Copy(); draft = saved.Copy(); profiles = backup.Profiles.Copy(); preferences = new UserPreferences { UseTray = backup.Preferences.UseTray, CheckUpdates = backup.Preferences.CheckUpdates, AutomaticProfiles = backup.Preferences.AutomaticProfiles, NetworkAccess = backup.Preferences.NetworkAccess, Theme = backup.Preferences.Theme, CustomAccent = backup.Preferences.CustomAccent, HistoryRetention = backup.Preferences.HistoryRetention }; UiStyle.ApplyTheme(preferences.Theme); UiStyle.ApplyAccent(preferences.CustomAccent); NetworkPolicy.Enabled = preferences.NetworkAccess; currentProfile = "Default"; automaticProfileActive = false; pinnedProfile = ""; automaticProfileProcess = ""; profileReason = "Restored backup selected the Default profile."; selectedLayer = -1; customSelected = -1;
            if (engine != null) engine.Apply(saved); ApplyHotkeys(saved); RefreshLayerView(); PopulateList(); PopulateCustomList(); LoadEditor(selected);
            if (draft.CustomHotkeys.Length > 0) LoadCustomEditor(0); else SetCustomEditorState(false);
            loading = true; enabled.Checked = saved.Enabled; useTray.Checked = preferences.UseTray; checkUpdates.Checked = preferences.CheckUpdates; automaticProfiles.Checked = preferences.AutomaticProfiles; networkAccess.Checked = preferences.NetworkAccess; checkUpdates.Enabled = preferences.NetworkAccess; startup.Checked = backup.StartWithWindows; loading = false;
            tray.Visible = preferences.UseTray; hideToTray.Enabled = preferences.UseTray; RefreshTrayProfiles(); dirty = false; Text = "KiWeave"; UpdateStatus();
        }
        void OpenHistory()
        {
            if (readOnlyMode) { SetFeedback("Read-only mode is active. Unlock editing before restoring history.", true); return; }
            if (dirty) { SetFeedback("Save or discard your edits before restoring history.", true); return; }
            using (var dialog = new ConfigurationHistoryForm(saved, profiles, preferences, Startup.Enabled)) {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedEntry == null) return;
                var entry = dialog.SelectedEntry;
                string prompt = "Restore the selected local snapshot?\n\n" + entry + "\n\n" + ConfigurationHistory.Compare(entry.Backup, saved, profiles, preferences, Startup.Enabled) + "\n\nKiWeave will first preserve the current setup in history and create a rollback folder.";
                if (MessageBox.Show(this, prompt, "Restore KiWeave history", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try { CaptureHistory("history restore"); string rollback = BackupBundle.Restore(entry.Backup); ApplyRestoredBackup(entry.Backup); AuditTrail.Record("history-restore"); SetFeedback("History restored. Rollback copy: " + rollback, false); }
                catch (Exception ex) { SetFeedback("History restore failed; the current setup was kept or rolled back. " + ex.Message, true); }
            }
        }

        void PersistCurrent(Configuration configuration)
        {
            if (String.Equals(currentProfile, "Default", StringComparison.OrdinalIgnoreCase)) { ConfigStore.Save(ConfigStore.DefaultPath, configuration); return; }
            var profile = profiles.Find(currentProfile); if (profile == null) throw new InvalidOperationException("The active profile no longer exists.");
            Configuration defaultConfiguration = File.Exists(ConfigStore.DefaultPath) ? ConfigStore.Load(ConfigStore.DefaultPath) : new Configuration();
            ProfileStore.SetEffectiveConfiguration(profiles, profile, configuration, defaultConfiguration); ProfileStore.Save(ProfileStore.DefaultPath, profiles); RefreshTrayProfiles();
        }

        internal void OpenProfiles()
        {
            if (readOnlyMode) { SetFeedback("Read-only mode is active. Unlock editing before changing profiles.", true); return; }
            if (dirty) { SetFeedback("Save or discard the current edits before switching profiles.", true); return; }
            Configuration defaultConfiguration = File.Exists(ConfigStore.DefaultPath) ? ConfigStore.Load(ConfigStore.DefaultPath) : new Configuration();
            using (var dialog = new ProfileManagerForm(profiles, draft, defaultConfiguration)) if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedProfile != null) {
                profiles = ProfileStore.Load(ProfileStore.DefaultPath); SelectProfileManually(dialog.SelectedProfile.Name);
            } else try { profiles = ProfileStore.Load(ProfileStore.DefaultPath); if (pinnedProfile.Length > 0 && profiles.Find(pinnedProfile) == null && !String.Equals(pinnedProfile, "Default", StringComparison.OrdinalIgnoreCase)) pinnedProfile = ""; RefreshTrayProfiles(); UpdateStatus(); } catch { }
        }

        void RefreshTrayProfiles()
        {
            trayProfiles.DropDownItems.Clear();
            var def = trayProfiles.DropDownItems.Add("Default"); def.Click += delegate { SelectProfileManually("Default"); };
            foreach (var item in profiles.Profiles) { string name = item.Name; var menu = trayProfiles.DropDownItems.Add(name); menu.Click += delegate { SelectProfileManually(name); }; }
            trayProfiles.DropDownItems.Add(new ToolStripSeparator()); trayProfiles.DropDownItems.Add("Manage profiles...", null, delegate { ShowSettings(); OpenProfiles(); });
            RefreshTrayProfileChecks();
        }

        void RefreshTrayProfileChecks()
        {
            foreach (ToolStripItem item in trayProfiles.DropDownItems) {
                var menu = item as ToolStripMenuItem;
                if (menu != null && menu.Text != "Manage profiles...") menu.Checked = String.Equals(menu.Text, currentProfile, StringComparison.OrdinalIgnoreCase);
            }
        }

        void SelectProfileManually(string name)
        {
            if (!ActivateProfile(name, true, false)) return;
            if (pinnedProfile.Length > 0) pinnedProfile = currentProfile;
            profileReason = pinnedProfile.Length > 0 ? "Selected manually while profile pinning is active." : "Selected manually.";
            UpdateStatus();
        }

        bool ActivateProfile(string name, bool announce, bool automatic)
        {
            if (dirty) { if (announce) SetFeedback("Save or discard the current edits before switching profiles.", true); return false; }
            try {
                Configuration next;
                if (String.Equals(name, "Default", StringComparison.OrdinalIgnoreCase)) next = File.Exists(ConfigStore.DefaultPath) ? ConfigStore.Load(ConfigStore.DefaultPath) : new Configuration();
                else { var profile = profiles.Find(name); if (profile == null) throw new ArgumentException("Profile not found: " + name); Configuration defaultConfiguration = File.Exists(ConfigStore.DefaultPath) ? ConfigStore.Load(ConfigStore.DefaultPath) : new Configuration(); next = profiles.Resolve(profile.Name, defaultConfiguration); }
                saved = next.Copy(); draft = next.Copy(); currentProfile = name; automaticProfileActive = automatic; selectedLayer = -1; customSelected = -1;
                if (automatic) profileReason = (automaticProfileProcess.Length == 0 ? "Selected by an automatic app rule." : "Automatically matched " + automaticProfileProcess + ".") + " Session: " + SessionAwareness.CurrentDescription() + ".";
                if (engine != null) engine.Apply(saved); ApplyHotkeys(saved); RefreshLayerView(); PopulateList(); PopulateCustomList(); LoadEditor(selected);
                if (draft.CustomHotkeys.Length > 0) LoadCustomEditor(0); else SetCustomEditorState(false);
                loading = true; enabled.Checked = saved.Enabled; loading = false; dirty = false; Text = "KiWeave"; UpdateStatus();
                AuditTrail.Record("profile-activated"); if (announce) SetFeedback("Using profile " + name + ".", false);
                return true;
            } catch (Exception ex) { if (announce) SetFeedback("Could not switch profile: " + ex.Message, true); return false; }
        }

        void CheckAutomaticProfile()
        {
            if (isPreview || !preferences.AutomaticProfiles || pinnedProfile.Length > 0 || Visible || dirty || engine == null || profiles.Profiles.Length == 0) return;
            string process = Native.ForegroundProcessName();
            var match = profiles.ForApplication(process);
            var layoutMatch = KeyboardLayoutProfiles.Match(profiles);
            if (SessionAwareness.IsRemoteDesktop || SessionAwareness.IsVirtualMachine) match = layoutMatch; else if (match == null) match = layoutMatch;
            if (match != null) {
                automaticProfileProcess = String.IsNullOrWhiteSpace(process) ? "" : process + ".exe";
                if (!String.Equals(match.Name, currentProfile, StringComparison.OrdinalIgnoreCase) || !automaticProfileActive) ActivateProfile(match.Name, false, true);
            } else if (automaticProfileActive && !String.Equals(currentProfile, "Default", StringComparison.OrdinalIgnoreCase)) {
                automaticProfileProcess = ""; ActivateProfile("Default", false, true); profileReason = "No automatic app rule matched, so KiWeave returned to Default."; UpdateStatus();
            }
        }

        void RequestProfileActivation(string name)
        {
            Ui(delegate {
                if (!ActivateProfile(name, true, false)) return;
                pinnedProfile = currentProfile; automaticProfileActive = false;
                profileReason = "Activated by a mapped shortcut and pinned for this app session.";
                SetFeedback("Using and pinning profile " + currentProfile + " for this session.", false); UpdateStatus();
            });
        }

        void ToggleProfilePin()
        {
            if (pinnedProfile.Length > 0) {
                pinnedProfile = ""; automaticProfileActive = false; profileReason = "Profile pin removed. Automatic switching may resume when KiWeave is in the background.";
                SetFeedback("Automatic profile switching resumed for this session.", false);
            } else {
                pinnedProfile = currentProfile; automaticProfileActive = false; profileReason = "Pinned manually for this app session. Automatic switching is paused.";
                SetFeedback("Pinned profile " + currentProfile + " until KiWeave exits.", false);
            }
            UpdateStatus();
        }

        void OpenProfileStatus()
        {
            KeyWeaveProfile profile = String.Equals(currentProfile, "Default", StringComparison.OrdinalIgnoreCase) ? null : profiles.Find(currentProfile);
            string[] rules = profile == null ? new string[0] : profile.Applications;
            using (var dialog = new ProfileStatusForm(currentProfile, profileReason, pinnedProfile.Length > 0, preferences.AutomaticProfiles, rules)) {
                dialog.ShowDialog(Visible ? this : null);
                if (dialog.TogglePinRequested) ToggleProfilePin();
            }
        }

        internal void OpenDiagnostics()
        {
            using (var dialog = new DiagnosticsForm(BuildSafeDiagnostics())) dialog.ShowDialog(this);
        }
        internal void OpenSupportBundle()
        {
            using (var dialog = new DiagnosticsForm(BuildSupportBundle(), true)) dialog.ShowDialog(this);
        }
        void OpenLiveKeyTester()
        {
            if (engine == null) { SetFeedback("The live key tester needs the keyboard hook, which is not available right now.", true); return; }
            using (var dialog = new LiveKeyTesterForm(engine, () => currentProfile)) dialog.ShowDialog(this);
        }
        void OpenConflictCenter()
        {
            Configuration defaults;
            try { defaults = File.Exists(ConfigStore.DefaultPath) ? ConfigStore.Load(ConfigStore.DefaultPath) : new Configuration(); }
            catch (Exception ex) { SetFeedback("Conflict scan could not read Default: " + ex.Message, true); return; }
            using (var dialog = new ConflictCenterForm(defaults, profiles)) if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedIssue != null) OpenConflictIssue(dialog.SelectedIssue);
        }
        void OpenMappingSearch()
        {
            Configuration defaults;
            try { defaults = String.Equals(currentProfile, "Default", StringComparison.OrdinalIgnoreCase) ? draft.Copy() : (File.Exists(ConfigStore.DefaultPath) ? ConfigStore.Load(ConfigStore.DefaultPath) : new Configuration()); }
            catch (Exception ex) { SetFeedback("Mapping search could not read Default: " + ex.Message, true); return; }
            ProfileCollection searchableProfiles = profiles.Copy();
            if (!String.Equals(currentProfile, "Default", StringComparison.OrdinalIgnoreCase)) {
                var active = searchableProfiles.Find(currentProfile); if (active != null) try { ProfileStore.SetEffectiveConfiguration(searchableProfiles, active, draft, defaults); } catch { }
            }
            using (var dialog = new MappingSearchForm(defaults, searchableProfiles)) if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedResult != null) OpenMappingSearchResult(dialog.SelectedResult);
        }
        void OpenMappingSearchResult(MappingSearchResult result)
        {
            if (!String.Equals(result.Profile, currentProfile, StringComparison.OrdinalIgnoreCase)) {
                if (dirty) { SetFeedback("Save or discard the current edits before opening a result from another profile.", true); return; }
                if (!ActivateProfile(result.Profile, true, false)) return;
            }
            if (result.CustomIndex >= 0) { SelectPage(1); SelectCustomSection(false); if (result.CustomIndex < draft.CustomHotkeys.Length) LoadCustomEditor(result.CustomIndex); }
            else if (result.FunctionIndex >= 0) { SelectPage(0); selectedLayer = result.LayerIndex >= 0 && result.LayerIndex < draft.Layers.Length ? result.LayerIndex : -1; RefreshLayerView(); PopulateList(); LoadEditor(Math.Min(11, result.FunctionIndex)); }
            SetFeedback("Opened " + result.Location + ".", false);
        }
        void SaveRecoveryDraft()
        {
            if (isPreview || !dirty) return;
            try {
                Configuration defaults = String.Equals(currentProfile, "Default", StringComparison.OrdinalIgnoreCase) ? saved.Copy() : (File.Exists(ConfigStore.DefaultPath) ? ConfigStore.Load(ConfigStore.DefaultPath) : new Configuration());
                RecoveryStore.SaveDraft(defaults, profiles, preferences, Startup.Enabled, currentProfile, draft);
            } catch (Exception ex) { AppLog.Record("DraftRecovery", ex); }
        }
        void OfferDraftRecovery()
        {
            if (isPreview || dirty) return;
            KeyWeaveBackup recovery; string profile;
            try { if (!RecoveryStore.TryLoadDraft(out recovery, out profile)) return; }
            catch (Exception ex) { AppLog.Record("DraftRecoveryRead", ex); return; }
            Configuration defaults; try { defaults = File.Exists(ConfigStore.DefaultPath) ? ConfigStore.Load(ConfigStore.DefaultPath) : new Configuration(); } catch { defaults = new Configuration(); }
            using (var dialog = new DraftRecoveryForm(recovery, profile, defaults, profiles, preferences, Startup.Enabled)) {
                dialog.ShowDialog(this);
                if (dialog.Choice == DraftRecoveryChoice.Discard) { RecoveryStore.DeleteDraft(); SetFeedback("Recovery draft discarded. Saved mappings were unchanged.", false); return; }
                if (dialog.Choice != DraftRecoveryChoice.Restore) return;
            }
            try {
                profiles = recovery.Profiles.Copy(); currentProfile = profile;
                draft = String.Equals(profile, "Default", StringComparison.OrdinalIgnoreCase) ? recovery.Configuration.Copy() : profiles.Resolve(profile, recovery.Configuration);
                selectedLayer = -1; customSelected = -1; RefreshLayerView(); PopulateList(); PopulateCustomList(); LoadEditor(0);
                if (draft.CustomHotkeys.Length > 0) LoadCustomEditor(0); else SetCustomEditorState(false);
                MarkDirty(); UpdateStatus(); SetFeedback("Recovered the private draft into the editor. Review it, then Save changes to activate it.", false);
            } catch (Exception ex) { SetFeedback("The recovery draft could not be opened: " + ex.Message, true); }
        }
        void OpenConflictIssue(ConflictIssue issue)
        {
            if (issue.Area == ConflictArea.Profiles || !String.Equals(issue.ProfileName, currentProfile, StringComparison.OrdinalIgnoreCase)) { OpenProfiles(); return; }
            if (issue.Area == ConflictArea.CustomHotkeys && issue.CustomHotkeyIndex >= 0 && issue.CustomHotkeyIndex < draft.CustomHotkeys.Length) {
                SelectPage(1); SelectCustomSection(false); LoadCustomEditor(issue.CustomHotkeyIndex); SetFeedback("Opened the KiWeave shortcut involved in the conflict.", false); return;
            }
            if (issue.Area == ConflictArea.PowerToys) { SelectPage(1); SelectCustomSection(true); return; }
            SelectPage(0);
        }
        void OpenPrivacyCenter()
        {
            using (var dialog = new PrivacyCenterForm(preferences.NetworkAccess, BuildSafeDiagnostics(), healthReport)) dialog.ShowDialog(this);
        }
        void OpenIntegrationHealth()
        {
            using (var dialog = new IntegrationHealthForm(detected, healthReport, saved)) dialog.ShowDialog(this);
        }
        void OpenFirstPartyExtensions()
        {
            using (var dialog = new FirstPartyExtensionsForm(this)) dialog.ShowDialog(this);
        }
        void ToggleReadOnlyMode()
        {
            bool next = !readOnlyMode;
            string prompt = next ? "Lock KiWeave editing? Active mappings will keep running, but editor controls, imports, restores, and saving will be disabled until you unlock them." : "Unlock KiWeave editing? Configuration changes and saving will be available again.";
            if (MessageBox.Show(this, prompt, next ? "Lock editing" : "Unlock editing", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            readOnlyMode = next; functionPage.Enabled = !readOnlyMode; customPage.Enabled = !readOnlyMode; saveChangesButton.Enabled = !readOnlyMode;
            if (readOnlyButton != null) readOnlyButton.Text = readOnlyMode ? "Unlock editing" : "Lock editing";
            SetFeedback(readOnlyMode ? "Read-only mode is active. Mappings continue running; editing is locked." : "Editing unlocked.", false);
        }
        void ThemeChanged(object sender, EventArgs e)
        {
            if (loading || themeChoice.SelectedItem == null) return;
            preferences.Theme = themeChoice.SelectedItem.ToString();
            try { UserPreferences.Save(UserPreferences.DefaultPath, preferences); SetFeedback("Theme saved. Restart KiWeave to apply it to every window.", false); }
            catch (Exception ex) { SetFeedback("Theme could not be saved: " + ex.Message, true); }
        }
        void ChooseAccentColor(object sender, EventArgs e)
        {
            using (var dialog = new ColorDialog { FullOpen = true, Color = UiStyle.AccentFill }) if (dialog.ShowDialog(this) == DialogResult.OK) {
                string value = "#" + dialog.Color.R.ToString("X2") + dialog.Color.G.ToString("X2") + dialog.Color.B.ToString("X2");
                string safe = UiStyle.SafeAccent(value); if (safe.Length == 0) { MessageBox.Show(this, "Choose a medium-brightness color so text and buttons remain readable.", "Accent color", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                preferences.CustomAccent = safe; UiStyle.ApplyAccent(safe); RefreshVisualTheme();
                try { UserPreferences.Save(UserPreferences.DefaultPath, preferences); SetFeedback("Accent color updated.", false); } catch (Exception ex) { SetFeedback("Accent color could not be saved: " + ex.Message, true); }
            }
        }
        void RefreshVisualTheme()
        {
            RefreshVisualTheme(this); Invalidate(true);
        }
        void RefreshVisualTheme(Control control)
        {
            // Accent changes are consumed by owner-drawn controls at paint time.
            // Do not flatten existing background and text roles here: doing so turns
            // sidebar panels into canvas panels and muted copy into bright headings.
            control.Invalidate();
            foreach (Control child in control.Controls) RefreshVisualTheme(child);
        }
        string BuildSafeDiagnostics()
        {
            int powerToys = 0; try { powerToys = PowerToysIntegration.Load().Count; } catch { }
            string report = "KiWeave diagnostics\r\n" +
                "Version: " + UpdateChecker.CurrentVersion + "\r\n" +
                "Keyboard hook: " + (engine != null && engine.Installed ? "ready" : "unavailable") + "\r\n" +
                "Shortcuts: " + (engine != null && engine.Enabled ? "enabled" : "paused") + "\r\n" +
                "Active profile: " + (currentProfile == "Default" ? "Default" : "Custom profile active") + "\r\n" +
                "Saved profiles: " + profiles.Profiles.Length + "\r\n" +
                "Modifier layers: " + draft.Layers.Length + "\r\n" +
                "Custom hotkeys: " + draft.CustomHotkeys.Length + "\r\n" +
                "Registered hotkeys: " + registeredHotkeys.Count + "\r\n" +
                "PowerToys shortcuts found: " + powerToys + "\r\n" +
                "Detected DDC/CI monitors: " + detected.Length + "\r\n" +
                "Start with Windows: " + (startup.Checked ? "yes" : "no") + "\r\n" +
                "Tray mode: " + (preferences.UseTray ? "yes" : "no") + "\r\n" +
                "Automatic profiles: " + (preferences.AutomaticProfiles ? "yes" : "no") + "\r\n" +
                "Master network access: " + (preferences.NetworkAccess ? "allowed" : "blocked") + "\r\n" +
                "Update checks: " + (preferences.CheckUpdates ? "yes" : "no") + "\r\n" +
                "Configuration health: " + (healthReport.HasWarnings ? healthReport.Findings.Length + " issue(s)" : "clear") + "\r\n" +
                "Recent private-safe log entries: " + AppLog.RecentCount() + "\r\n" +
                "Unsaved edits: " + (dirty ? "yes" : "no") + "\r\n";
            return report;
        }
        string BuildSupportBundle()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("KiWeave redacted support bundle");
            report.AppendLine("Generated: " + DateTime.UtcNow.ToString("u"));
            report.AppendLine("This preview is local-only. It contains no mapping targets, arguments, paths, URLs, request bodies, typed text, key history, or profile names.");
            report.AppendLine();
            report.Append(BuildSafeDiagnostics());
            report.AppendLine();
            report.AppendLine("Health findings: " + (healthReport.HasWarnings ? healthReport.Findings.Length.ToString() : "0"));
            foreach (var finding in healthReport.Findings) report.AppendLine("- " + finding.Title + ": " + finding.Detail);
            report.AppendLine();
            report.AppendLine("Feature maturity summary:");
            report.AppendLine("- Stable built-in actions: available");
            report.AppendLine("- Experimental integrations: " + (DiscordIntegration.HasAuthorization ? "connected or authorized" : "not connected"));
            report.AppendLine("- Hardware-dependent actions: DDC/CI monitors detected " + detected.Length);
            report.AppendLine();
            report.AppendLine("Integrity state: not included in this bundle by design.");
            return report.ToString();
        }
        void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (streamDeckBridge != null) { streamDeckBridge.Dispose(); streamDeckBridge = null; }
            if (e.CloseReason == CloseReason.UserClosing && preferences.UseTray && !exitRequested) { e.Cancel = true; Hide(); return; }
            if (e.CloseReason == CloseReason.UserClosing && dirty) {
                DialogResult r = MessageBox.Show(this, "Save your edited mappings before exiting?", "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel || (r == DialogResult.Yes && !Save())) { e.Cancel = true; exitRequested = false; return; }
                if (r == DialogResult.No) RecoveryStore.DeleteDraft();
            }
            foreach (int id in registeredHotkeys.Keys.ToArray()) Native.UnregisterHotKey(Handle, id); registeredHotkeys.Clear(); statusTimer.Stop(); scheduleTimer.Stop(); tray.Visible = false; tray.Dispose(); if (engine != null) engine.Dispose(); DiscordIntegration.Disconnect();
        }
        protected override void Dispose(bool disposing) { if (disposing) { if (engine != null) engine.Dispose(); if (updateNotice != null) updateNotice.Dispose(); tray.Dispose(); tips.Dispose(); statusTimer.Dispose(); scheduleTimer.Dispose(); draftTimer.Dispose(); updateTimer.Dispose(); if (list.SmallImageList != null) list.SmallImageList.Dispose(); if (customList.SmallImageList != null) customList.SmallImageList.Dispose(); } base.Dispose(disposing); }
    }
}
