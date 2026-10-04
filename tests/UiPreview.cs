using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
namespace FunctionRowRemapper
{
    internal static class UiPreview
    {
        static T Field<T>(object o, string name) { return (T)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o); }
        static object Call(object o, string name, params object[] args)
        {
            var method = o.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Single(x => x.Name == name && x.GetParameters().Length == args.Length);
            return method.Invoke(o, args);
        }
        static void CheckDropdownLayout(Control control)
        {
            foreach (Control child in control.Controls) {
                var combo = child as DesignComboBox;
                if (combo != null && combo.Visible && (combo.Top < 0 || combo.Bottom > combo.Parent.ClientSize.Height)) throw new Exception("Dropdown border is clipped: " + combo.Text);
                CheckDropdownLayout(child);
            }
        }
        static void Prepare(Form form) { form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-22000, -22000); form.Show(); Application.DoEvents(); }
        static void Capture(Form form, string name) { form.PerformLayout(); Application.DoEvents(); CheckDropdownLayout(form); using (var b = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(b, new Rectangle(Point.Empty, form.Size)); b.Save(Path.Combine("bin-designed-ui", name + ".png")); } }
        static DesignScrollPanel FindScroll(Control root)
        {
            var panel = root as DesignScrollPanel;
            if (panel != null) return panel;
            foreach (Control child in root.Controls) { var found = FindScroll(child); if (found != null) return found; }
            return null;
        }
        static void CaptureBottom(Form form, string name)
        {
            var scroll = FindScroll(form);
            if (scroll != null) { scroll.ScrollToBottom(); Application.DoEvents(); }
            Capture(form, name);
        }
        [STAThread] static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            AppStorage.MigrateLegacy();
            if (args.Contains("--interactive")) { using (var f = new MainForm(false, true)) Application.Run(f); return 0; }
            string configBefore = File.ReadAllText(ConfigStore.DefaultPath);
            using (var f = new MainForm(false, true)) {
                var draft = Field<Configuration>(f, "draft"); draft.Layers = new[] { new ModifierLayer { Name = "Media layer", ActivationKey = "CapsLock" } }; draft.Mappings[8] = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+Shift+S" }; Call(f, "RefreshLayerView"); Call(f, "PopulateList");
                Prepare(f); Call(f, "LoadEditor", 8); Capture(f, "function");
                Call(f, "SelectPage", 1); Application.DoEvents();
                Call(f, "LoadCustomEditor", 0); Capture(f, "custom");
                f.Size = f.MinimumSize; Capture(f, "custom-minimum");
                Call(f, "SelectPage", 2); Application.DoEvents(); Capture(f, "settings");
                Call(f, "SelectPage", 1); Application.DoEvents();
                var customList = Field<ListView>(f, "customList");
                Console.WriteLine("Custom list width: " + customList.ClientSize.Width + "; columns: " + customList.Columns[0].Width + ", " + customList.Columns[1].Width);
                string original = ConfigStore.Serialize(draft);
                Call(f, "LoadCustomEditor", 0);
                if (ConfigStore.Serialize(draft) != original) throw new Exception("Opening the editor altered a mapping.");
                draft.CustomHotkeys[0].Action = new Mapping { Kind = ActionKind.Python, Target = @"C:\example.py" };
                Call(f, "LoadCustomEditor", 0); Capture(f, "python-minimum");
                if (Field<ComboBox>(f, "customSpecificKind").SelectedItem.ToString() != "Run a Python script") throw new Exception("Saved Python action selected incorrectly.");
                var scroll = Field<TableLayoutPanel>(f, "customStack").Parent as ScrollableControl;
                scroll.ScrollControlIntoView(Field<TextBox>(f, "customWorking")); Capture(f, "python-scrolled");
                draft.CustomHotkeys[0].Action = new Mapping { Kind = ActionKind.SendShortcut, Target = "Win+E" };
                Call(f, "LoadCustomEditor", 0);
                if (Field<ComboBox>(f, "customSpecificKind").SelectedItem.ToString() != "Open file explorer") throw new Exception("Saved preset selected incorrectly.");
                Call(f, "CustomEdited");
                if (draft.CustomHotkeys[0].Action.Target != "Win+E") throw new Exception("Loading a preset changed its shortcut.");
                while (draft.CustomHotkeys.Length > 0) Call(f, "RemoveCustomHotkey");
                if (Field<Panel>(f, "customEditorHost").Visible) throw new Exception("Empty custom editor stayed active.");
                Capture(f, "empty");
                Call(f, "AddCustomHotkey");
                if (!Field<Panel>(f, "customEditorHost").Visible) throw new Exception("Adding a hotkey did not open its editor.");
                f.Scale(new SizeF(1.5f, 1.5f)); Capture(f, "scaled-layout");
                Call(f, "Save");
            }
            using (var f = new MainForm(false, true)) { Prepare(f); UiStyle.ApplyAccent("#A457D2"); Call(f, "RefreshVisualTheme"); Capture(f, "accent-live"); f.Close(); }
            UiStyle.ApplyTheme("KiWeave Dark");
            using (var f = new FirstPartyExtensionsForm(null)) { Prepare(f); Capture(f, "extensions"); f.Size = f.MinimumSize; Capture(f, "extensions-minimum"); CaptureBottom(f, "extensions-minimum-scrolled"); f.Close(); }
            using (var f = new IntegrationHealthForm(1, ConfigurationHealthReport.Empty)) { Prepare(f); Capture(f, "integration-health"); f.Size = f.MinimumSize; Capture(f, "integration-health-minimum"); CaptureBottom(f, "integration-health-scrolled"); f.Close(); }
            using (var f = new ProfileSchedulesForm(new ProfileCollection { Profiles = new[] { new KeyWeaveProfile { Name = "Gaming" } } })) { Prepare(f); Capture(f, "profile-schedules"); f.Size = f.MinimumSize; Capture(f, "profile-schedules-minimum"); f.Close(); }
            using (var f = new LayerManagerForm(new[] { new ModifierLayer { Name = "Media layer", ActivationKey = "CapsLock" } })) { Prepare(f); Capture(f, "layers"); f.Size = f.MinimumSize; Capture(f, "layers-minimum"); f.Close(); }
            var profileDefault = new Configuration(); profileDefault.Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" };
            var inheritedProfile = new KeyWeaveProfile { Name = "Gaming", Applications = new[] { "game.exe" }, InheritFrom = "Default", OverrideKeys = new[] { "F2" }, Configuration = profileDefault.Copy() };
            inheritedProfile.Configuration.Mappings[1] = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" };
            using (var f = new ProfileManagerForm(new ProfileCollection { Profiles = new[] { inheritedProfile } }, inheritedProfile.Configuration, profileDefault)) { Prepare(f); Capture(f, "profiles-inheritance"); f.Size = f.MinimumSize; Capture(f, "profiles-inheritance-minimum"); f.Close(); }
            using (var f = new PrivacyCenterForm(false, "KiWeave diagnostics\r\nVersion: 1.0.0-beta.3\r\nMaster network access: blocked\r\n")) { Prepare(f); Capture(f, "privacy-center"); f.Size = f.MinimumSize; Capture(f, "privacy-center-minimum"); CaptureBottom(f, "privacy-center-minimum-scrolled"); f.Close(); }
            using (var f = new LiveKeyTesterForm(null, () => "Default", true)) { Prepare(f); Capture(f, "live-key-tester"); f.Size = f.MinimumSize; Capture(f, "live-key-tester-minimum"); f.Close(); }
            var conflictConfig = new Configuration { CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Win+L", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" } }, new CustomHotkey { Shortcut = "Ctrl+Alt+K", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } } } };
            var conflictPowerToys = new[] { new PowerToysShortcut { Module = "ColorPicker", Action = "Activation", Chord = "Ctrl+Alt+K", ModuleEnabled = true } };
            using (var f = new ConflictCenterForm(conflictConfig, new ProfileCollection(), conflictPowerToys)) { Prepare(f); Capture(f, "conflict-center"); f.Size = f.MinimumSize; Capture(f, "conflict-center-minimum"); f.Close(); }
            using (var f = new ShortcutCaptureForm(true, true)) { Prepare(f); Capture(f, "shortcut-capture"); f.Size = f.MinimumSize; Capture(f, "shortcut-capture-minimum"); f.Close(); }
            var historical = new Configuration(); historical.Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" };
            var historyBackup = new KeyWeaveBackup { Configuration = historical, Profiles = new ProfileCollection(), Preferences = new UserPreferences(), StartWithWindows = false, CreatedUtc = DateTime.UtcNow.AddMinutes(-12) };
            var historyEntry = new ConfigurationHistoryEntry { Path = "preview.keyweave", Reason = "Mapping save", CreatedUtc = historyBackup.CreatedUtc, Bytes = 4096, Backup = historyBackup };
            using (var f = new ConfigurationHistoryForm(new Configuration(), new ProfileCollection(), new UserPreferences(), false, new[] { historyEntry })) { Prepare(f); Capture(f, "history"); f.Size = f.MinimumSize; Capture(f, "history-minimum"); f.Close(); }
            var imported = new Configuration { CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+W", Action = new Mapping { Kind = ActionKind.HttpRequest, Target = "https://example.invalid/hook", Arguments = "{\"ok\":true}" } } } }; imported.Mappings[0] = new Mapping { Kind = ActionKind.Command, Target = @"C:\Tools\sample.cmd", Arguments = "--preview" };
            using (var f = new ImportReviewForm(imported, "example-import.json")) { Prepare(f); Capture(f, "import-review"); f.Size = f.MinimumSize; Capture(f, "import-review-minimum"); CaptureBottom(f, "import-review-scrolled"); f.Close(); }
            using (var f = new SequenceBuilderForm(new[] {
                new SequenceStep { Action = new Mapping { Kind = ActionKind.SendShortcut, Target = "Win+E" } },
                new SequenceStep { WaitMilliseconds = 1000 },
                new SequenceStep { Action = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+V" } }
            })) {
                Prepare(f); Capture(f, "sequence"); f.Size = f.MinimumSize; Capture(f, "sequence-minimum");
                Call(f, "MoveStep", 1); if (f.Result[1].IsWait) throw new Exception("Sequence move did not preserve the selected action.");
            }
            var conditionPreview = new ConditionalRule { Condition = ConditionKind.ForegroundApplication, Application = "Discord.exe", WhenMatched = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" }, Otherwise = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+Shift+M" } };
            using (var f = new ConditionalActionForm(new Mapping { Kind = ActionKind.Conditional, Target = ConditionalCodec.Serialize(conditionPreview) })) { Prepare(f); Capture(f, "conditional"); f.Size = f.MinimumSize; Capture(f, "conditional-minimum"); f.Close(); }
            using (var f = new SafeModeForm()) { Prepare(f); Capture(f, "safe-mode"); f.Size = f.MinimumSize; Capture(f, "safe-mode-minimum"); CaptureBottom(f, "safe-mode-minimum-scrolled"); f.Close(); }
            var searchConfig = new Configuration(); searchConfig.Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" }; searchConfig.CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+K", Action = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+Shift+S" } } };
            using (var f = new MappingSearchForm(searchConfig, new ProfileCollection())) { Prepare(f); Capture(f, "mapping-search"); f.Size = f.MinimumSize; Capture(f, "mapping-search-minimum"); f.Close(); }
            var recoveryPreview = new KeyWeaveBackup { Configuration = searchConfig, Profiles = new ProfileCollection(), Preferences = new UserPreferences(), StartWithWindows = false, CreatedUtc = DateTime.UtcNow.AddMinutes(-3) };
            using (var f = new DraftRecoveryForm(recoveryPreview, "Default", new Configuration(), new ProfileCollection(), new UserPreferences(), false)) { Prepare(f); Capture(f, "draft-recovery"); f.Size = f.MinimumSize; Capture(f, "draft-recovery-minimum"); f.Close(); }
            using (var f = new ActionPickerForm(true)) {
                Prepare(f); Field<TextBox>(f, "search").Text = "clipboard"; Capture(f, "search");
                var list = Field<ListBox>(f, "results");
                if (list.Items.Count < 2 || !list.Items.Cast<MainForm.SpecificChoice>().All(c => c.SearchText.IndexOf("clipboard", StringComparison.OrdinalIgnoreCase) >= 0)) throw new Exception("Contains search failed.");
                Field<TextBox>(f, "search").Text = "";
                Field<ComboBox>(f, "category").SelectedItem = "VS Code";
                if (list.Items.Count < 50 || !list.Items.Cast<MainForm.SpecificChoice>().All(c => c.Category == "VS Code")) throw new Exception("Category filtering failed.");
                Capture(f, "code-category");
                Field<ComboBox>(f, "category").SelectedIndex = 0;
                Field<TextBox>(f, "search").Text = "does-not-exist"; if (list.Items.Count != 0) throw new Exception("Empty results failed.");
            }
            using (var f = new StepDetailsForm(new Mapping { Kind = ActionKind.Python, Target = @"C:\example.py" })) {
                Prepare(f); Capture(f, "step-details");
            }
            if (File.ReadAllText(ConfigStore.DefaultPath) != configBefore) throw new Exception("Preview modified saved configuration.");
            Console.WriteLine("UI previews generated; loading, search, sequence order and config preservation verified."); return 0;
        }
    }
}
