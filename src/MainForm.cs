using System;
using System.Drawing;
using System.Collections.Generic;
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
        const string Name = "FunctionRowRemapper";
        public static bool Enabled {
            get { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(Name) != null; }
        }
        public static bool IsCurrent { get { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && String.Equals(k.GetValue(Name) as string, "\"" + Application.ExecutablePath + "\" --tray", StringComparison.OrdinalIgnoreCase); } }
        public static void Set(bool enabled)
        {
            if (!enabled && !Enabled) return;
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey)) {
                if (enabled) k.SetValue(Name, "\"" + Application.ExecutablePath + "\" --tray", RegistryValueKind.String);
                else k.DeleteValue(Name, false);
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
        readonly TextBox target = new TextBox(), arguments = new TextBox(), working = new TextBox();
        readonly TextBox customShortcut = new TextBox(), customTarget = new TextBox(), customArguments = new TextBox(), customWorking = new TextBox();
        readonly ComboBox customKind = new ComboBox(), customMedia = new ComboBox();
        readonly List<SequenceStep> sequenceSteps = new List<SequenceStep>();
        Button customBrowse;
        readonly CheckBox enabled = new DesignToggle(), startup = new CheckBox(), useTray = new CheckBox();
        Button hideToTray;
        readonly ToolTip tips = new ToolTip();
        UserPreferences preferences;
        bool exitRequested;
        readonly Label editorTitle = new DesignLabel(), hint = new DesignLabel(), status = new DesignLabel(), feedback = new DesignLabel(), targetLabel = new DesignLabel(), argumentsLabel = new DesignLabel(), workingLabel = new DesignLabel();
        readonly Button browse = new DesignButton(), folder = new DesignButton(), workBrowse = new DesignButton();
        readonly NotifyIcon tray = new NotifyIcon();
        UpdateNotification updateNotice;
        readonly ToolStripMenuItem trayToggle = new ToolStripMenuItem("Enable remapping");
        readonly System.Windows.Forms.Timer statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        readonly bool startInTray;
        readonly bool isPreview;
        KeyboardEngine engine;
        readonly ActionDispatcher customDispatcher = new ActionDispatcher(new WindowsActionSink());
        readonly System.Collections.Generic.Dictionary<int, CustomHotkey> registeredHotkeys = new System.Collections.Generic.Dictionary<int, CustomHotkey>();
        Configuration saved, draft;
        int selected, customSelected = -1; bool loading, dirty;
        string initialError;

        public MainForm(bool startInTray) : this(startInTray, false) { }
        internal MainForm(bool startInTray, bool preview)
        {
            this.startInTray = startInTray;
            isPreview = preview;
            Text = "Function Row Remapper"; Font = new Font("Segoe UI", 10F); ForeColor = ink; BackColor = Color.FromArgb(245, 247, 251);
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(1200, 820); MinimumSize = new Size(1080, 740); StartPosition = FormStartPosition.CenterScreen; DoubleBuffered = true;
            Icon = Program.AppIcon();
            saved = new Configuration();
            try { preferences = UserPreferences.Load(UserPreferences.DefaultPath); }
            catch (Exception ex) { preferences = new UserPreferences { UseTray = false }; initialError = "Tray preference could not be loaded; the window will stay accessible. " + ex.Message; }
            try { if (File.Exists(ConfigStore.DefaultPath)) saved = ConfigStore.Load(ConfigStore.DefaultPath); }
            catch (Exception ex) { initialError = "Saved configuration could not be loaded. Remapping is off; the original file is untouched. " + ex.Message; }
            draft = saved.Copy();
            BuildUi(); PopulateList(); PopulateCustomList(); LoadEditor(0);
            if (draft.CustomHotkeys.Length > 0) LoadCustomEditor(0); else SetCustomEditorState(false);
            if (preview) {
                loading = true; enabled.Checked = saved.Enabled; useTray.Checked = preferences.UseTray; startup.Checked = Startup.Enabled;
                Text = "Function Row Remapper - Design preview"; hideToTray.Enabled = false; status.Text = "Editor preview"; loading = false; return;
            }
            try {
                engine = new KeyboardEngine();
                engine.Error += message => Ui(delegate { SetFeedback(message, true); if (preferences.UseTray) tray.ShowBalloonTip(4000, "Action could not run", message, ToolTipIcon.Warning); });
                engine.EmergencyDisabled += () => Ui(EmergencyOff);
                engine.Apply(saved);
            } catch (Exception ex) { saved.Enabled = draft.Enabled = false; initialError = ex.Message; }
            if (engine != null) try { ApplyHotkeys(saved); } catch (Exception ex) { initialError = "Function-key remapping is still available, but a custom hotkey could not register. " + ex.Message; }
            loading = true; enabled.Checked = saved.Enabled;
            useTray.Checked = preferences.UseTray;
            try { startup.Checked = Startup.Enabled; } catch (Exception ex) { initialError = "Cannot read startup setting: " + ex.Message; }
            loading = false;
            SetupTray(); UpdateStatus();
            statusTimer.Tick += delegate { UpdateStatus(); }; statusTimer.Start();
            Shown += delegate {
                DetectMonitors();
                if (initialError != null) SetFeedback(initialError, true);
                else CheckMissingTargets();
                if (startInTray && preferences.UseTray && initialError == null) Hide();
                UpdateChecker.CheckInBackground(tag => Ui(delegate { updateNotice = new UpdateNotification(tag); }));
            };
            FormClosing += OnClosing;
        }
        void Ui(Action a) { if (!IsDisposed && IsHandleCreated) try { BeginInvoke(a); } catch (InvalidOperationException) { } }
        Label LabelText(string text, float size, Color color) { return new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", size), ForeColor = color, Margin = new Padding(0, 0, 0, 6) }; }
        Button ButtonText(string text, EventHandler click)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(90, 35), FlatStyle = FlatStyle.Flat, BackColor = Color.White, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(7, 2, 7, 2) };
            b.FlatAppearance.BorderColor = Color.FromArgb(207, 216, 231); b.Click += click; return b;
        }
        static int FunctionGroup(ActionKind kind) { if (kind == ActionKind.PassThrough) return 0; if (kind == ActionKind.Unbound) return 1; if (kind == ActionKind.SendKey || kind == ActionKind.SendShortcut) return 2; if (kind == ActionKind.Media) return 3; if (kind == ActionKind.Monitor) return 5; if (kind == ActionKind.LockThenSleep) return 6; return 4; }
        static int CustomGroup(ActionKind kind) { if (kind == ActionKind.SendKey || kind == ActionKind.SendShortcut) return 0; if (kind == ActionKind.Media) return 1; if (kind == ActionKind.LockThenSleep || kind == ActionKind.Sequence) return 3; return 2; }
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
            if (normalized == 2) return new[] { Choice("Open an application", ActionKind.Application), Choice("Open a file or folder", ActionKind.FileOrFolder), Choice("Run a Windows shortcut", ActionKind.WindowsShortcut), Choice("Run a command or script", ActionKind.Command), Choice("Run a Python script", ActionKind.Python) };
            var presets = new List<SpecificChoice> { Choice("Lock Windows, then sleep", ActionKind.LockThenSleep), Shortcut("Lock Windows", "Win+L"), Shortcut("Open file explorer", "Win+E"), Shortcut("Open Windows settings", "Win+I"), Shortcut("Open task manager", "Ctrl+Shift+Escape"), Shortcut("Open clipboard history", "Win+V"), Shortcut("Open notification center", "Win+N"), Shortcut("Open quick settings", "Win+A"), Shortcut("Open emoji picker", "Win+OemPeriod"), Shortcut("Open task view", "Win+Tab"), Shortcut("Open run dialog", "Win+R"), Shortcut("Open power-user menu", "Win+X"), Shortcut("Take a screen snip", "Win+Shift+S"), Shortcut("Show desktop", "Win+D"), Shortcut("Switch apps", "Alt+Tab"), Shortcut("Snap window left", "Win+Left"), Shortcut("Snap window right", "Win+Right"), Shortcut("Maximize window", "Win+Up"), Shortcut("Minimize window", "Win+Down"), Shortcut("New virtual desktop", "Win+Ctrl+D"), Shortcut("Close virtual desktop", "Win+Ctrl+F4"), Shortcut("Next virtual desktop", "Win+Ctrl+Right"), Shortcut("Previous virtual desktop", "Win+Ctrl+Left"), Shortcut("Copy", "Ctrl+C"), Shortcut("Paste", "Ctrl+V"), Shortcut("Cut", "Ctrl+X"), Shortcut("Undo", "Ctrl+Z"), Shortcut("Redo", "Ctrl+Y"), Shortcut("Select all", "Ctrl+A"), Shortcut("Save", "Ctrl+S"), Shortcut("Open", "Ctrl+O"), Shortcut("New", "Ctrl+N"), Shortcut("Find", "Ctrl+F"), Shortcut("Print", "Ctrl+P"), Shortcut("Refresh", "F5"), Shortcut("Close current window", "Alt+F4"), Shortcut("Toggle full screen", "F11"), Preset("Open calculator", ActionKind.Application, Path.Combine(sys, "calc.exe"), ""), Preset("Open notepad", ActionKind.Application, Path.Combine(sys, "notepad.exe"), ""), Preset("Open paint", ActionKind.Application, Path.Combine(sys, "mspaint.exe"), ""), Preset("Open control panel", ActionKind.Application, Path.Combine(sys, "control.exe"), ""), Preset("Open device manager", ActionKind.Application, Path.Combine(sys, "mmc.exe"), "devmgmt.msc"), Preset("Sleep", ActionKind.Application, Path.Combine(sys, "rundll32.exe"), "powrprof.dll,SetSuspendState 0,1,0"), Preset("Sign out", ActionKind.Application, Path.Combine(sys, "shutdown.exe"), "/l"), Preset("Restart", ActionKind.Application, Path.Combine(sys, "shutdown.exe"), "/r /t 0"), Preset("Shut down", ActionKind.Application, Path.Combine(sys, "shutdown.exe"), "/s /t 0") };
            presets.AddRange(ExpandedActions.All);
            presets.Insert(0, ChooseAction);
            if (custom) presets.Insert(1, Choice("Build a step-by-step sequence...", ActionKind.Sequence)); return presets.ToArray();
        }
        void PopulateChoices(ComboBox box, int group, bool custom, Mapping selectedMapping)
        {
            box.Items.Clear(); foreach (var choice in ChoicesFor(group, custom)) box.Items.Add(choice);
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
            loading = true; kind.SelectedIndex = (int)choice.Mapping.Kind; target.Text = choice.Mapping.Target; arguments.Text = choice.Mapping.Arguments; working.Text = choice.Mapping.WorkingDirectory; media.SelectedIndex = Array.IndexOf(Shortcuts.MediaLabels.Keys.ToArray(), choice.Mapping.Target); ConfigureFields(false); loading = false; Edited();
        }
        void ApplyCustomChoice()
        {
            if (loading || customSpecificKind.SelectedItem == null) return; var choice = (SpecificChoice)customSpecificKind.SelectedItem;
            if (ReferenceEquals(choice, ChooseAction)) { SetFeedback("Choose an action before saving this custom hotkey.", false); return; }
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
            customShortcut.Text = h.Shortcut; customKind.SelectedIndex = (int)h.Action.Kind; customTarget.Text = h.Action.Target; customArguments.Text = h.Action.Arguments; customWorking.Text = h.Action.WorkingDirectory; customMedia.SelectedIndex = Array.IndexOf(Shortcuts.MediaLabels.Keys.ToArray(), h.Action.Target); customSimpleKind.SelectedIndex = GroupFor(h.Action, true); PopulateChoices(customSpecificKind, customSimpleKind.SelectedIndex, true, h.Action); ConfigureCustomFields(false); loading = false;
        }
        void ConfigureCustomFields(bool reset)
        {
            ActionKind k = (ActionKind)Math.Max(0, customKind.SelectedIndex);
            bool launch = (k >= ActionKind.Application && k <= ActionKind.Command) || k == ActionKind.Python;
            bool args = k == ActionKind.Application || k == ActionKind.Command || k == ActionKind.Python;
            customTargetField.Visible = launch || k == ActionKind.SendKey || k == ActionKind.SendShortcut;
            customArgsField.Visible = customWorkField.Visible = args;
            customArguments.Enabled = customWorking.Enabled = args;
            customBrowse.Visible = launch;
            sequenceField.Visible = k == ActionKind.Sequence;
            sequenceSummary.Text = sequenceSteps.Count == 0 ? "Add actions and waits in the sequence builder." :
                String.Join("\n", sequenceSteps.Select((s, i) => (i + 1) + ".  " + s.Summary));
            customHelp.Text = k == ActionKind.LockThenSleep ? "Locks Windows, then puts the computer to sleep." :
                k == ActionKind.Sequence ? "Steps run from top to bottom. Open Build sequence to edit them." :
                k == ActionKind.Python ? "Choose a .py file. It runs with your normal account when this hotkey is pressed." :
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
            for (int i = 0; i < 12; i++) { var item = new ListViewItem("F" + (i + 1)); item.SubItems.Add(Summary(draft.Mappings[i])); list.Items.Add(item); }
            list.Items[selected].Selected = true; list.EndUpdate(); loading = false;
        }
        string Summary(Mapping m) { try { return m.Summary; } catch { return "Choose action details"; } }
        void LoadEditor(int index)
        {
            loading = true; selected = index; Mapping m = draft.Mappings[index]; editorTitle.Text = "F" + (index + 1); kind.SelectedIndex = (int)m.Kind;
            foreach (ListViewItem item in list.Items) item.Selected = item.Index == index;
            target.Text = m.Target; arguments.Text = m.Arguments; working.Text = m.WorkingDirectory; media.SelectedIndex = Array.IndexOf(Shortcuts.MediaLabels.Keys.ToArray(), m.Target); simpleKind.SelectedIndex = GroupFor(m, false); PopulateChoices(specificKind, simpleKind.SelectedIndex, false, m);
            LoadMonitorChoices(m.MonitorId, m.MonitorControl); monitorStep.Value = Math.Max(1, Math.Min(20, m.MonitorStep));
            ConfigureFields(false); loading = false;
        }
        void ConfigureFields(bool reset)
        {
            bool wasLoading = loading; loading = true;
            ActionKind k = (ActionKind)Math.Max(0, kind.SelectedIndex);
            bool isMonitor = k == ActionKind.Monitor;
            bool launch = (k >= ActionKind.Application && k <= ActionKind.Command) || k == ActionKind.Python;
            bool canArgs = k == ActionKind.Application || k == ActionKind.Command || k == ActionKind.Python;
            monitorPanel.Visible = isMonitor;
            functionTargetField.Visible = launch || k == ActionKind.SendKey || k == ActionKind.SendShortcut;
            functionArgsField.Visible = functionWorkField.Visible = canArgs;
            arguments.Enabled = working.Enabled = canArgs;
            browse.Visible = launch; folder.Visible = k == ActionKind.FileOrFolder;
            if (reset) { target.Text = ""; arguments.Text = ""; working.Text = ""; media.SelectedIndex = 0; }
            if (reset && isMonitor) { LoadMonitorChoices("", ""); monitorStep.Value = 5; }
            hint.Text = k == ActionKind.PassThrough ? "This key keeps its normal Windows and app behavior." :
                k == ActionKind.Unbound ? "This key does nothing while shortcuts are enabled." :
                k == ActionKind.LockThenSleep ? "Locks Windows, then puts the computer to sleep." :
                k == ActionKind.Media ? "Controls your media or Windows volume. Hold volume keys to repeat." :
                k == ActionKind.Monitor ? "Adjusts the selected monitor directly. Hold the key to repeat." :
                k == ActionKind.Python ? "Choose a .py file. It runs with your normal account." :
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
                draft.Mappings[selected] = new Mapping { Kind = k, MonitorId = device == null ? "" : device.Id, MonitorControl = operation == null ? "" : operation.Id, MonitorStep = (int)monitorStep.Value };
            } else draft.Mappings[selected] = new Mapping { Kind = k, Target = k == ActionKind.Media ? Shortcuts.MediaLabels.Keys.ElementAt(Math.Max(0, media.SelectedIndex)) : (k == ActionKind.PassThrough || k == ActionKind.Unbound || k == ActionKind.LockThenSleep ? "" : target.Text.Trim()), Arguments = arguments.Enabled ? arguments.Text : "", WorkingDirectory = working.Enabled ? working.Text.Trim() : "" };
            list.Items[selected].SubItems[1].Text = Summary(draft.Mappings[selected]); MarkDirty();
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
                loading = true; var m = draft.Mappings[selected]; LoadMonitorChoices(m.MonitorId, m.MonitorControl); loading = false;
                if ((ActionKind)kind.SelectedIndex == ActionKind.Monitor && String.IsNullOrEmpty(m.MonitorId)) Edited();
            } catch (Exception ex) { if (!IsDisposed) monitorStatus.Text = "Detection failed: " + ex.Message; }
            finally { scanning = false; if (!IsDisposed) detect.Enabled = true; }
        }
        void MarkDirty() { dirty = true; Text = "Function Row Remapper *"; SetFeedback("Unsaved changes. Save to apply them. The enable switch uses your saved mappings.", false); }
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
            try { ConfigStore.Save(ConfigStore.DefaultPath, next); saved = next; draft.Enabled = value; engine.SetEnabled(value); ApplyHotkeys(saved); SetFeedback(value ? "Remapping and custom hotkeys enabled using saved rules." : "Remapping and custom hotkeys are off.", false); }
            catch (Exception ex) { loading = true; enabled.Checked = engine.Enabled; loading = false; SetFeedback("Could not save enable setting: " + ex.Message, true); }
            UpdateStatus();
        }
        void EmergencyOff()
        {
            loading = true; enabled.Checked = false; loading = false; saved.Enabled = draft.Enabled = false;
            try { ConfigStore.Save(ConfigStore.DefaultPath, saved); SetFeedback("Emergency bypass activated. Remapping is off. Release any held function keys.", false); }
            catch (Exception ex) { SetFeedback("Remapping is off, but the preference could not be saved: " + ex.Message, true); }
            UpdateStatus(); if (preferences.UseTray) tray.ShowBalloonTip(3000, "Remapping is off", "Emergency bypass activated.", ToolTipIcon.Info);
        }
        bool Save()
        {
            if (isPreview) { SetFeedback("Design preview only. Nothing was saved or activated.", false); return true; }
            if ((functionPage.Visible && simpleKind.SelectedIndex == 6 && ReferenceEquals(specificKind.SelectedItem, ChooseAction)) ||
                (customPage.Visible && customHotkeyView.Visible && customSimpleKind.SelectedIndex == 3 && ReferenceEquals(customSpecificKind.SelectedItem, ChooseAction))) {
                SetFeedback("Choose an action first. Nothing was changed.", true); return false;
            }
            try {
                ConfigStore.Validate(draft, true); draft.Enabled = engine != null && engine.Enabled;
                ConfigStore.Save(ConfigStore.DefaultPath, draft); saved = draft.Copy();
                if (engine != null) engine.Apply(saved); ApplyHotkeys(saved); PopulateCustomList();
                dirty = false; Text = "Function Row Remapper";
                try { if (startup.Checked != Startup.Enabled || (startup.Checked && !Startup.IsCurrent)) Startup.Set(startup.Checked); }
                catch (Exception ex) { dirty = true; SetFeedback("Mappings saved, but startup setting failed: " + ex.Message, true); return false; }
                SetFeedback("Saved. " + (saved.Enabled ? "Your mappings are active." : "Turn on Shortcuts enabled when you are ready."), false); return true;
            } catch (Exception ex) { SetFeedback("Could not save: " + ex.Message, true); return false; }
        }
        void Bulk(bool unbound)
        {
            for (int i = 0; i < 12; i++) draft.Mappings[i] = new Mapping { Kind = unbound ? ActionKind.Unbound : ActionKind.PassThrough };
            PopulateList(); LoadEditor(selected); MarkDirty();
        }
        void Export(object sender, EventArgs e)
        {
            try {
                ConfigStore.Validate(draft, false);
                using (var d = new SaveFileDialog { Filter = "JSON configuration|*.json", FileName = "function-row.json", DefaultExt = "json", AddExtension = true })
                    if (d.ShowDialog(this) == DialogResult.OK) { ConfigStore.Save(d.FileName, draft); SetFeedback("Exported your draft mappings. Export does not activate changes or change Windows startup.", false); }
            } catch (Exception ex) { SetFeedback("Export failed: " + ex.Message, true); }
        }
        void Import(object sender, EventArgs e)
        {
            using (var d = new OpenFileDialog { Filter = "JSON configuration|*.json", CheckFileExists = true }) {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try {
                    Configuration imported = ConfigStore.Load(d.FileName);
                    // Imported enabled state never changes the live toggle, and imports cannot add startup entries.
                    imported.Enabled = saved.Enabled; draft = imported; customSelected = -1; PopulateList(); PopulateCustomList(); LoadEditor(selected);
                    if (draft.CustomHotkeys.Length > 0) LoadCustomEditor(0); else SetCustomEditorState(false);
                    MarkDirty();
                    SetFeedback("Imported into the editor. Review all targets and commands, then Save to apply. Nothing has been run.", false); CheckMissingTargets();
                } catch (Exception ex) { SetFeedback("Import rejected; current mappings are unchanged. " + ex.Message, true); }
            }
        }
        void CheckMissingTargets()
        {
            for (int i = 0; i < 12; i++) try { ConfigStore.Validate(draft.Mappings[i], true); } catch (Exception ex) { SetFeedback("Check F" + (i + 1) + ": " + ex.Message, true); break; }
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
                    if (!Native.RegisterHotKey(Handle, id, (uint)(chord.Modifiers | HotkeyChord.NoRepeat), (uint)chord.Key)) throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(), HotkeyChord.Normalize(config.CustomHotkeys[i].Shortcut) + " is already used by Windows or another app.");
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
            var menu = new ContextMenuStrip { Font = Font, BackColor = UiStyle.Surface, ForeColor = UiStyle.Ink, Renderer = new ToolStripProfessionalRenderer(new DesignMenuColors()) };
            menu.Items.Add("Open settings", null, delegate { ShowSettings(); });
            trayToggle.Click += delegate { enabled.Checked = !enabled.Checked; }; menu.Items.Add(trayToggle); menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Exit", null, delegate { ExitApp(); });
            tray.Icon = Program.TrayIcon(); tray.Text = "Function Row Remapper"; tray.ContextMenuStrip = menu; tray.Visible = preferences.UseTray; tray.DoubleClick += delegate { ShowSettings(); };
            hideToTray.Enabled = preferences.UseTray;
        }
        void ToggleTray(object sender, EventArgs e)
        {
            if (loading) return;
            if (isPreview) return;
            var next = new UserPreferences { UseTray = useTray.Checked };
            try {
                UserPreferences.Save(UserPreferences.DefaultPath, next); preferences = next;
                tray.Visible = next.UseTray; hideToTray.Enabled = next.UseTray;
                if (!next.UseTray && !Visible) ShowSettings();
                SetFeedback(next.UseTray ? "Tray enabled. Closing this window keeps remapping running. Use Exit to quit." : "Tray disabled. Closing this window exits the app and stops remapping.", false);
            } catch (Exception ex) { loading = true; useTray.Checked = preferences.UseTray; loading = false; SetFeedback("Could not save tray preference: " + ex.Message, true); }
        }
        void ExitApp() { exitRequested = true; Close(); }
        internal void RequestShow() { Ui(ShowSettings); }
        internal void RequestUpdateExit(Action<int> reply)
        {
            Ui(delegate {
                if (dirty) { reply(11); return; }
                if (Visible) { reply(10); return; }
                if (!Enabled || OwnedForms.Any(f => f.Visible)) { reply(12); return; }
                ExitApp(); reply(0);
            });
        }
        void ShowSettings() { Show(); WindowState = FormWindowState.Normal; Activate(); CheckMissingTargets(); }
        void UpdateStatus()
        {
            bool active = engine != null && engine.Installed;
            status.Text = !active ? "Unavailable" : engine.Enabled ? "Active in the background" : "Shortcuts paused";
            status.ForeColor = active && engine.Enabled ? Color.FromArgb(127, 214, 169) : muted;
            trayToggle.Text = engine != null && engine.Enabled ? "Disable remapping" : "Enable remapping";
        }
        void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && preferences.UseTray && !exitRequested) { e.Cancel = true; Hide(); return; }
            if (e.CloseReason == CloseReason.UserClosing && dirty) {
                DialogResult r = MessageBox.Show(this, "Save your edited mappings before exiting?", "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel || (r == DialogResult.Yes && !Save())) { e.Cancel = true; exitRequested = false; return; }
            }
            foreach (int id in registeredHotkeys.Keys.ToArray()) Native.UnregisterHotKey(Handle, id); registeredHotkeys.Clear(); statusTimer.Stop(); tray.Visible = false; tray.Dispose(); if (engine != null) engine.Dispose();
        }
        protected override void Dispose(bool disposing) { if (disposing) { if (engine != null) engine.Dispose(); if (updateNotice != null) updateNotice.Dispose(); tray.Dispose(); tips.Dispose(); statusTimer.Dispose(); if (list.SmallImageList != null) list.SmallImageList.Dispose(); if (customList.SmallImageList != null) customList.SmallImageList.Dispose(); } base.Dispose(disposing); }
    }
}
