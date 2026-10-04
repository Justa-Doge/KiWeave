using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class FakeSink : IActionSink, IProfileActionSink
    {
        public readonly List<int[]> Keys = new List<int[]>();
        public readonly List<ProcessStartInfo> Launches = new List<ProcessStartInfo>();
        public readonly List<string> Profiles = new List<string>();
        public void Send(int[] keys) { Keys.Add(keys); }
        public void Launch(ProcessStartInfo p) { Launches.Add(p); }
        void IProfileActionSink.ActivateProfile(string name) { Profiles.Add(name); }
    }
    internal static class Tests
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
        static int passed, failed; static string scratch;
        static void Assert(bool condition, string detail) { if (!condition) throw new Exception(detail); }
        static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } if (!rejected) throw new Exception("Expected rejection"); }
        static void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); } }
        static Configuration Active(ActionKind kind, string target) { var c = new Configuration { Enabled = true }; c.Mappings[4] = new Mapping { Kind = kind, Target = target }; return c; }
        static void CapturePreview(Form form, string name)
        {
            form.StartPosition = FormStartPosition.Manual; form.Location = new System.Drawing.Point(-20000, -20000); form.Show(); Application.DoEvents(); form.Refresh();
            using (var image = new System.Drawing.Bitmap(form.Width, form.Height)) {
                form.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, form.Size));
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name + ".png"); image.Save(path); Console.WriteLine(path);
            }
        }
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--probe") { File.AppendAllText(args[1], "launched\r\n"); return 0; }
            scratch = Path.Combine(Path.GetTempPath(), "FunctionRowRemapper-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch);
            try {
                if (args.Contains("--powertoys-detect")) {
                    foreach (var item in PowerToysIntegration.Load()) Console.WriteLine(item.Module + " | " + item.Action + " | " + item.Chord + " | " + (item.ModuleEnabled ? "enabled" : "off") + " | " + (item.CanEdit ? "editable" : "view only"));
                    return 0;
                }
                if (args.Contains("--powertoys-dsc-test")) {
                    Console.WriteLine(PowerToysIntegration.TestCurrentDscInput("ColorPicker", Path.Combine(PowerToysIntegration.Root, "ColorPicker", "settings.json")));
                    return 0;
                }
                if (args.Contains("--powertoys-save-noop")) {
                    var item = PowerToysIntegration.Load().First(x => x.Module == "ColorPicker" && x.Action == "Activation");
                    string original = item.Chord; Console.WriteLine("Backup: " + PowerToysIntegration.Save(item, original, false));
                    Assert(PowerToysIntegration.Load().First(x => x.Module == "ColorPicker" && x.Action == "Activation").Chord == original, "PowerToys shortcut changed");
                    Console.WriteLine("PowerToys no-op save verified; shortcut unchanged."); return 0;
                }
                if (args.Contains("--powertoys-preview")) {
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    using (var form = new MainForm(false, true)) {
                        form.StartPosition = FormStartPosition.Manual; form.Location = new System.Drawing.Point(-20000, -20000);
                        form.Show(); Application.DoEvents();
                        typeof(MainForm).GetMethod("SelectPage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(form, new object[] { 1 });
                        typeof(MainForm).GetMethod("SelectCustomSection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(form, new object[] { true });
                        Application.DoEvents(); form.Refresh();
                        using (var image = new System.Drawing.Bitmap(form.Width, form.Height)) {
                            form.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, form.Size));
                            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "powertoys-preview.png"); image.Save(path); Console.WriteLine(path);
                        }
                        var powerView = (Panel)typeof(MainForm).GetField("powerToysView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
                        var powerPanel = powerView.Controls.OfType<PowerToysPanel>().First();
                        typeof(PowerToysPanel).GetMethod("BeginAdd", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(powerPanel, null);
                        Application.DoEvents(); form.Refresh();
                        var moduleField = (Control)typeof(PowerToysPanel).GetField("moduleField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(powerPanel);
                        var actionField = (Control)typeof(PowerToysPanel).GetField("actionField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(powerPanel);
                        Assert(moduleField.Top < actionField.Top, "PowerToys module field must appear before specific function");
                        using (var image = new System.Drawing.Bitmap(form.Width, form.Height)) {
                            form.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, form.Size));
                            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "powertoys-add-preview.png"); image.Save(path); Console.WriteLine(path);
                        }
                        form.Close();
                    }
                    return 0;
                }
                if (args.Contains("--function-preview")) {
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    using (var form = new MainForm(false, true)) {
                        form.StartPosition = FormStartPosition.Manual; form.Location = new System.Drawing.Point(-20000, -20000);
                        form.Show(); Application.DoEvents();
                        var listField = typeof(MainForm).GetField("list", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        var list = (ListView)listField.GetValue(form);
                        Assert(list.Items.Count == 12 && list.Items.Cast<ListViewItem>().All(x => !String.IsNullOrEmpty(x.SubItems[1].Text)), "a function action label is empty");
                        for (int pass = 0; pass < 3; pass++) { form.Invalidate(true); form.Refresh(); Application.DoEvents(); }
                        using (var image = new System.Drawing.Bitmap(form.Width, form.Height)) {
                            form.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, form.Size));
                            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "function-preview.png"); image.Save(path); Console.WriteLine(path);
                        }
                        typeof(MainForm).GetMethod("SelectPage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(form, new object[] { 1 });
                        Application.DoEvents(); form.Refresh();
                        using (var image = new System.Drawing.Bitmap(form.Width, form.Height)) {
                            form.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, form.Size));
                            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom-preview.png"); image.Save(path); Console.WriteLine(path);
                        }
                        var customGroup = (ComboBox)typeof(MainForm).GetField("customSimpleKind", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
                        var customChoice = (ComboBox)typeof(MainForm).GetField("customSpecificKind", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
                        customGroup.SelectedIndex = 0; customGroup.SelectedIndex = 3;
                        Assert(customChoice.SelectedItem.ToString() == "Choose an action", "custom action did not start empty");
                        form.Close();
                    }
                    return 0;
                }
                if (args.Contains("--dialogs-preview")) {
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    using (var form = new ActionPickerForm(true)) { CapturePreview(form, "action-library-preview"); form.Size = form.MinimumSize; CapturePreview(form, "action-library-minimum-preview"); form.Close(); }
                    using (var form = new SequenceBuilderForm(new[] {
                        new SequenceStep { Action = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+C" } },
                        new SequenceStep { WaitMilliseconds = 1000 },
                        new SequenceStep { Action = new Mapping { Kind = ActionKind.Media, Target = "MediaPlayPause" } }
                    })) { CapturePreview(form, "sequence-preview"); form.Size = form.MinimumSize; CapturePreview(form, "sequence-minimum-preview"); form.Close(); }
                    using (var form = new StepDetailsForm(new Mapping { Kind = ActionKind.Python, Target = @"C:\example.py" })) { CapturePreview(form, "step-details-preview"); form.Close(); }
                    return 0;
                }
                if (args.Contains("--v1-preview")) {
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    var collection = new ProfileCollection { Profiles = new[] { new KeyWeaveProfile { Name = "Gaming", Applications = new[] { "game.exe", "obs64.exe" }, Configuration = new Configuration() } } };
                    using (var form = new ProfileManagerForm(collection, new Configuration())) { CapturePreview(form, "profiles-preview"); form.Size = form.MinimumSize; CapturePreview(form, "profiles-minimum-preview"); form.Close(); }
                    using (var form = new DiagnosticsForm("KeyWeave diagnostics\r\nVersion: 1.0.0\r\nKeyboard hook: ready\r\nShortcuts: enabled\r\nActive profile: Gaming\r\nSaved profiles: 1\r\nCustom hotkeys: 4\r\nRegistered hotkeys: 4\r\nPowerToys shortcuts found: 12\r\nDetected DDC/CI monitors: 1\r\nStart with Windows: yes\r\nTray mode: yes\r\nUnsaved edits: no\r\n")) { CapturePreview(form, "diagnostics-preview"); form.Size = form.MinimumSize; CapturePreview(form, "diagnostics-minimum-preview"); form.Close(); }
                    using (var form = new ProfileStatusForm("Gaming", "Automatically matched game.exe.", false, true, new[] { "game.exe", "obs64.exe" })) { CapturePreview(form, "profile-status-preview"); form.Size = form.MinimumSize; CapturePreview(form, "profile-status-minimum-preview"); form.Close(); }
                    using (var form = new AboutForm()) { CapturePreview(form, "about-preview"); form.Size = form.MinimumSize; CapturePreview(form, "about-minimum-preview"); form.Close(); }
                    using (var form = new WelcomeForm()) { CapturePreview(form, "welcome-preview"); form.Size = form.MinimumSize; CapturePreview(form, "welcome-minimum-preview"); form.Close(); }
                    return 0;
                }
                if (args.Contains("--ddc-detect") || args.Contains("--ddc-hardware")) {
                    foreach (var m in DdcService.Shared.Scan()) Console.WriteLine(m.Name + ": " + m.Status);
                    if (args.Contains("--ddc-hardware")) foreach (byte code in new byte[] {0x10,0x12,0x62}) Test("Hardware monitor control " + code.ToString("X2"), () => Console.WriteLine(DdcService.Shared.VerifyHardwareRoundTrip(code)));
                }
                else if (args.Contains("--native")) NativeTests(); else UnitTests();
                Console.WriteLine(passed + " passed; " + failed + " failed."); return failed == 0 ? 0 : 1;
            } finally { Directory.Delete(scratch, true); }
        }
        static void UnitTests()
        {
            Test("Every KiWeave window requests a dark title bar", delegate {
                var forms = new Form[] {
                    new MainForm(false, true), new ActionPickerForm(true), new AboutForm(), new DiagnosticsForm("Safe diagnostics"),
                    new ImportReviewForm(new Configuration(), "preview.json"), new LayerManagerForm(new ModifierLayer[0]),
                    new PrivacyCenterForm(false, "Safe diagnostics"), new ProfileManagerForm(new ProfileCollection(), new Configuration()),
                    new WelcomeForm(), new SequenceBuilderForm(new SequenceStep[0]), new StepDetailsForm(new Mapping()), new ConditionalActionForm(null), new SafeModeForm(), new LiveKeyTesterForm(null, () => "Default", true), new ConflictCenterForm(new Configuration(), new ProfileCollection(), new PowerToysShortcut[0]), new ShortcutCaptureForm(true, true), new ConfigurationHistoryForm(new Configuration(), new ProfileCollection(), new UserPreferences(), false, new ConfigurationHistoryEntry[0]), new ProfileStatusForm("Gaming", "Selected manually.", false, true, new[] { "game.exe" })
                };
                try {
                    foreach (var form in forms) { int dark; Assert(DwmGetWindowAttribute(form.Handle, 20, out dark, 4) == 0 && dark == 1, form.GetType().Name + " has a light title bar"); }
                } finally { foreach (var form in forms) form.Dispose(); }
            });
            Test("Top-bar and nested menus use the dark palette", delegate {
                using (var menu = Design.DarkMenu(System.Drawing.SystemFonts.MenuFont)) {
                    var parent = new ToolStripMenuItem("Profiles"); parent.DropDownItems.Add("Default"); menu.Items.Add(parent);
                    Design.RefreshDarkMenu(menu);
                    Assert(menu.BackColor == UiStyle.Surface && menu.ForeColor == UiStyle.Ink, "top-level menu is light");
                    Assert(parent.DropDown.BackColor == UiStyle.Surface && parent.DropDown.ForeColor == UiStyle.Ink, "nested menu is light");
                    Assert(Object.ReferenceEquals(parent.DropDown.Renderer, menu.Renderer), "nested menu lost the dark renderer");
                }
            });
            Test("Safe Mode launch detection is explicit and Shift-accessible", delegate {
                Assert(Program.SafeModeRequested(new[] { "--safe-mode" }, false), "argument ignored");
                Assert(Program.SafeModeRequested(new string[0], true), "Shift ignored");
                Assert(!Program.SafeModeRequested(new[] { "--tray" }, false), "ordinary launch entered Safe Mode");
                Assert(!Program.SafeModeRequested(new[] { "--normal-mode" }, true), "deliberate normal restart looped into Safe Mode");
            });
            Test("Safe Mode remains recovery-only and diagnostics stay redacted", delegate {
                NetworkPolicy.Enabled = true;
                using (var form = new SafeModeForm()) {
                    string report = form.Diagnostics();
                    Assert(!NetworkPolicy.Enabled && report.Contains("Safe Mode: active") && report.Contains("Keyboard hook: not loaded") && report.Contains("Global hotkeys: not registered") && report.Contains("Network access: blocked"), "recovery boundary missing");
                    Assert(!report.Contains(ConfigStore.DefaultPath) && !report.Contains(Environment.UserName), "private path or identity leaked");
                }
            });
            Test("Dark numeric spinner keeps working up and down buttons", delegate {
                using (var numeric = new DesignNumericUpDown { Minimum = 0, Maximum = 2, Value = 1, Increment = 0.25M, Width = 90 }) {
                    var handle = numeric.Handle;
                    var spinner = numeric.Controls.Cast<Control>().First(x => x.GetType().Name == "DarkSpinnerButtons");
                    Assert(spinner.Visible && spinner.Width >= 16, "custom spinner is not visible");
                    var mouseDown = spinner.GetType().GetMethod("OnMouseDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    mouseDown.Invoke(spinner, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 4, 2, 0) });
                    Assert(numeric.Value == 1.25M, "up button did not increment");
                    mouseDown.Invoke(spinner, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 4, spinner.Height - 2, 0) });
                    Assert(numeric.Value == 1M, "down button did not decrement");
                }
            });
            Test("Custom action menus begin with a non-executing choice", delegate {
                Assert(MainForm.ChoicesFor(3, true)[0].Label == "Choose an action", "custom hotkey placeholder");
                Assert(MainForm.ChoicesFor(6, false)[0].Label == "Choose an action", "function key placeholder");
                Assert(MainForm.ChoicesFor(3, true)[0].Mapping.Kind != ActionKind.LockThenSleep, "sleep cannot be the default");
                Assert(MainForm.ChoicesFor(3, true).Any(x => x.Mapping.Kind == ActionKind.Conditional), "custom conditional choice missing");
                Assert(MainForm.ChoicesFor(6, false).Any(x => x.Mapping.Kind == ActionKind.Conditional), "function conditional choice missing");
            });
            Test("Profile activation appears in both mapping editors", delegate {
                using (var form = new MainForm(false, true)) {
                    var method = typeof(MainForm).GetMethod("PopulateChoices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    foreach (string field in new[] { "specificKind", "customSpecificKind" }) {
                        var box = (ComboBox)typeof(MainForm).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
                        method.Invoke(form, new object[] { box, field == "specificKind" ? 6 : 3, field != "specificKind", new Mapping { Kind = ActionKind.SystemAction, Target = SystemActions.ActivateProfilePrefix + "Default" } });
                        Assert(box.Items.Cast<object>().Any(x => x.ToString() == "Activate and pin profile: Default") && box.SelectedItem.ToString() == "Activate and pin profile: Default", field);
                    }
                }
            });
            Test("Action library excludes its non-executing dropdown placeholder", delegate {
                Assert(!ActionPickerForm.Catalog(true).Any(x => x.Label == "Choose an action"), "placeholder leaked into library");
            });
            Test("PowerToys active list excludes off and unassigned shortcuts", delegate {
                Assert(PowerToysPanel.IsActive(new PowerToysShortcut { ModuleEnabled = true, Chord = "Shift+Win+C" }), "assigned enabled shortcut missing");
                Assert(!PowerToysPanel.IsActive(new PowerToysShortcut { ModuleEnabled = false, Chord = "Shift+Win+C" }), "off module shown");
                Assert(!PowerToysPanel.IsActive(new PowerToysShortcut { ModuleEnabled = true, Chord = "Unassigned" }), "unassigned shortcut shown");
            });
            Test("Update checker chooses only a newer stable version tag", delegate {
                string tags = "aaaa\trefs/tags/v0.1.0\n" +
                    "bbbb\trefs/tags/v0.2.0\n" +
                    "cccc\trefs/tags/v0.3.0-beta\n" +
                    "dddd\trefs/tags/v0.2.0^{}\n" +
                    "eeee\trefs/tags/v0.1.9\n";
                Assert(UpdateChecker.NewestUpdate(tags, "0.1.0") == "v0.2.0", "newest stable tag");
                Assert(UpdateChecker.NewestUpdate(tags, "0.2.0") == null, "current version is not an update");
                Assert(UpdateChecker.NewestUpdate("", "0.1.0") == null, "empty response");
                Assert(UpdateChecker.NewestUpdate(tags, "invalid") == null, "invalid installed version");
                string releases = "[{\"tag_name\":\"v1.2.0\",\"draft\":false,\"prerelease\":false},{\"tag_name\":\"v9.0.0\",\"draft\":false,\"prerelease\":true},{\"tag_name\":\"v8.0.0\",\"draft\":true,\"prerelease\":false}]";
                Assert(UpdateChecker.NewestUpdate(UpdateChecker.TagsFromReleaseJson(releases), "1.0.0") == "v1.2.0", "GitHub release parsing");
                Assert(UpdateChecker.NewestUpdate("release refs/tags/v1.0.0\n", "1.0.0-beta.1") == "v1.0.0", "final release should supersede beta");
            });
            Test("PowerToys shortcut scan includes live fields and skips defaults", delegate {
                string root = Path.Combine(scratch, "PowerToysFixture"), module = Path.Combine(root, "ColorPicker"); Directory.CreateDirectory(module);
                File.WriteAllText(Path.Combine(root, "settings.json"), "{\"enabled\":{\"ColorPicker\":true}}");
                File.WriteAllText(Path.Combine(module, "settings.json"), "{\"properties\":{\"DefaultActivationShortcut\":{\"win\":true,\"ctrl\":false,\"alt\":false,\"shift\":true,\"code\":67},\"ActivationShortcut\":{\"win\":true,\"ctrl\":false,\"alt\":false,\"shift\":true,\"code\":67},\"unused_hotkey\":{\"win\":false,\"ctrl\":true,\"alt\":false,\"shift\":false,\"code\":0}}}");
                var found = PowerToysIntegration.Load(root);
                Assert(found.Count == 2, "shortcut count " + found.Count);
                Assert(found.Any(x => x.Chord == "Shift+Win+C" && x.ModuleEnabled && x.Action == "Activation"), "active shortcut");
                Assert(found.Any(x => x.Chord == "Unassigned"), "unassigned shortcut");
            });
            Test("Expanded catalog has unique labels and over 300 actions", delegate {
                var all = ActionPickerForm.Catalog(true);
                Assert(all.Length > 300, "catalog size: " + all.Length);
                Assert(all.Select(c => c.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count() == all.Length, "duplicate labels");
                Console.WriteLine("Catalog: " + all.Length + " actions in " + all.Select(c => c.Category).Distinct().Count() + " categories");
            });
            Test("Every expanded preset validates and serializes without execution", delegate {
                foreach (var item in ExpandedActions.All) {
                    try {
                        ConfigStore.Validate(item.Mapping, false);
                        var c = new Configuration(); c.Mappings[0] = item.Mapping.Copy();
                        Assert(ConfigStore.Parse(ConfigStore.Serialize(c)).Mappings[0].Target == item.Mapping.Target, item.Label);
                    } catch (Exception e) { throw new Exception(item.Label + ": " + e.Message); }
                }
            });
            Test("Settings presets open pages through explorer without executing commands", delegate {
                foreach (var item in ExpandedActions.All.Where(c => c.Category == "Windows settings")) {
                    var p = ActionDispatcher.BuildLaunch(item.Mapping);
                    Assert(Path.GetFileName(p.FileName) == "explorer.exe" && p.Arguments.StartsWith("ms-settings:") && p.UseShellExecute, item.Label);
                }
            });
            Test("Safe defaults: twelve pass-through keys, off", delegate { var c = new Configuration(); Assert(!c.Enabled && c.Mappings.Length == 12 && c.Mappings.All(m => m.Kind == ActionKind.PassThrough), "defaults"); });
            Test("All nine action kinds round-trip in readable JSON", delegate {
                var c = new Configuration(); string exe = Process.GetCurrentProcess().MainModule.FileName;
                c.Mappings[0] = new Mapping { Kind = ActionKind.Unbound };
                c.Mappings[1] = new Mapping { Kind = ActionKind.SendKey, Target = "F2" };
                c.Mappings[2] = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+Shift+S" };
                c.Mappings[3] = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" };
                c.Mappings[4] = new Mapping { Kind = ActionKind.Application, Target = exe, Arguments = "--sample", WorkingDirectory = scratch };
                c.Mappings[5] = new Mapping { Kind = ActionKind.FileOrFolder, Target = scratch };
                c.Mappings[6] = new Mapping { Kind = ActionKind.WindowsShortcut, Target = Path.Combine(scratch, "sample.lnk") };
                c.Mappings[7] = new Mapping { Kind = ActionKind.Command, Target = Path.Combine(scratch, "sample.ps1"), Arguments = "-Name \"a b\"" };
                string json = ConfigStore.Serialize(c); Assert(ConfigStore.Serialize(ConfigStore.Parse(json)) == json, "round trip");
            });
            string valid = ConfigStore.Serialize(new Configuration());
            Test("Invalid JSON rejected", () => Reject(() => ConfigStore.Parse("{oops")));
            Test("Unknown version rejected", () => Reject(() => ConfigStore.Parse(valid.Replace("\"version\": 1", "\"version\": 3"))));
            Test("Unknown fields rejected", () => Reject(() => ConfigStore.Parse(valid.Replace("\"version\": 1", "\"evil\": true, \"version\": 1"))));
            Test("Wrong enabled type rejected", () => Reject(() => ConfigStore.Parse(valid.Replace("\"enabled\": false", "\"enabled\": \"false\""))));
            Test("Duplicate function keys rejected", () => Reject(() => ConfigStore.Parse(valid.Replace("\"F12\"", "\"F1\""))));
            Test("Duplicate JSON properties rejected", () => Reject(() => ConfigStore.Parse(valid.Replace("\"version\": 1", "\"version\": 1, \"version\": 1"))));
            Test("Numeric action names rejected", () => Reject(() => ConfigStore.Parse(valid.Replace("PassThrough", "0"))));
            Test("Null mappings rejected", () => Reject(() => ConfigStore.Parse("{\"version\":1,\"enabled\":false,\"mappings\":null}")));
            Test("Oversized JSON rejected", () => Reject(() => ConfigStore.Parse(new string(' ', ConfigStore.MaxBytes + 1))));
            Test("Oversized configuration cannot be written", delegate { var c = new Configuration(); foreach (var m in c.Mappings) { m.Kind = ActionKind.Command; m.Target = @"C:\cmd.exe"; m.Arguments = new string('x', 4096); m.WorkingDirectory = "C:\\" + new string('x', 4000); } Reject(() => ConfigStore.Save(Path.Combine(scratch, "large.json"), c)); Assert(!File.Exists(Path.Combine(scratch, "large.json")), "oversized save created a file"); });
            Test("Strict syntax rejects trailing commas/comments/single quotes", delegate { foreach (string s in new[] { "{\"a\":1,}", "[1,]", "{'a':1}", "{\"a\":01}", "true false", "/*x*/{}", "{\"a\":1,\"\\u0061\":2}" }) Reject(() => JsonSyntax.Check(s)); });
            Test("Shortcut parser supports modifiers and media", delegate { Assert(Shortcuts.Parse("Ctrl+Shift+S", false).SequenceEqual(new[] { 17, 16, 83 }), "shortcut"); Assert(Shortcuts.Parse("7", true)[0] == 55, "digit"); foreach (string media in Shortcuts.MediaLabels.Keys) Shortcuts.Parse(media, true); });
            Test("Uppercase single-key actions emit Shift", delegate { Assert(Shortcuts.ParseSendKey("R").SequenceEqual(new[] { (int)Keys.Shift, (int)Keys.R }), "uppercase R"); Assert(Shortcuts.ParseSendKey("r").SequenceEqual(new[] { (int)Keys.R }), "lowercase r"); });
            Test("Shortcut explanations include safety context", delegate { string explanation = MainForm.ExplainShortcut("F5", new Mapping { Kind = ActionKind.HttpRequest, Target = "https://example.invalid" }, "Base layer"); Assert(explanation.Contains("Explain F5") && explanation.Contains("Base layer") && explanation.Contains("Maturity: Experimental") && explanation.Contains("Dependencies:") && explanation.Contains("Permission: USES NETWORK"), "shortcut explanation"); });
            Test("Declarative action packs round-trip and reject scripts", delegate { var pack = new ActionPack { Id = "demo.pack", Name = "Demo pack", Publisher = "KiWeave", Description = "Safe demo", Configuration = new Configuration(), Profiles = new ProfileCollection() }; string json = ActionPackStore.Serialize(pack); Assert(ActionPackStore.Parse(json).Name == "Demo pack", "pack round trip"); var unsafePack = new ActionPack { Id = "unsafe.pack", Name = "Unsafe", Configuration = new Configuration() }; unsafePack.Configuration.Mappings[0] = new Mapping { Kind = ActionKind.Command, Target = @"C:\\cmd.exe" }; Reject(() => ActionPackStore.Serialize(unsafePack)); });
            Test("Malformed and reserved shortcuts rejected", delegate { foreach (string s in new[] { "", "Ctrl++A", "Ctrl+Ctrl+A", "Ctrl", "Ctrl+Alt+Delete", "Ctrl+Banana", "MouseButtons", "123", "Shift+Alt" }) Reject(() => Shortcuts.Parse(s, false)); Reject(() => Shortcuts.Parse("Ctrl+A", true)); });
            Test("Custom hotkeys normalize and require a modifier", delegate { Assert(HotkeyChord.Normalize("shift + win + l") == "Shift+Win+L", "normalize"); Reject(() => HotkeyChord.Parse("K")); });
            Test("Shortcut capture normalizes reviewed keys without retaining input", delegate {
                Assert(ShortcutCapture.Normalize(true, true, false, false, (int)Keys.K, true) == "Ctrl+Alt+K", "custom chord");
                Assert(ShortcutCapture.Normalize(false, false, false, false, (int)Keys.F5, false) == "F5", "plain action key");
                Assert(ShortcutCapture.Normalize(false, false, true, true, (int)Keys.D1, true) == "Shift+Win+1", "digit chord");
                Reject(() => ShortcutCapture.Normalize(false, false, false, false, (int)Keys.A, true));
                Reject(() => ShortcutCapture.Normalize(true, true, false, false, (int)Keys.Delete, true));
            });
            Test("Automatic update cadence is twelve hours", delegate { Assert(UpdateChecker.CheckIntervalMilliseconds == 43200000, "interval"); });
            Test("Custom hotkeys and Python scripts round-trip", delegate { var c = new Configuration { CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+P", Action = new Mapping { Kind = ActionKind.Python, Target = Path.Combine(scratch, "hello.py"), Arguments = "--fast", WorkingDirectory = scratch } }, new CustomHotkey { Shortcut = "Shift+Win+L", Action = new Mapping { Kind = ActionKind.LockThenSleep } } } }; string json = ConfigStore.Serialize(c); var loaded = ConfigStore.Parse(json); Assert(json.Contains("\"version\": 3") && loaded.CustomHotkeys[0].Action.Kind == ActionKind.Python && loaded.CustomHotkeys[1].Action.Kind == ActionKind.LockThenSleep, "v3"); });
            Test("Action sequences preserve order and waits", delegate { var steps = new[] { new SequenceStep { Action = new Mapping { Kind = ActionKind.SendShortcut, Target = "Win+E" } }, new SequenceStep { WaitMilliseconds = 750 }, new SequenceStep { Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" } } }; string text = SequenceCodec.Serialize(steps); var parsed = SequenceCodec.Parse(text); Assert(parsed.Count == 3 && parsed[1].WaitMilliseconds == 750 && parsed[2].Action.Target == "VolumeMute", "sequence"); });
            Test("Conditional actions round-trip with strict local rules", delegate {
                var rule = new ConditionalRule { Condition = ConditionKind.ForegroundApplication, Application = "Discord.exe", WhenMatched = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" }, Otherwise = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+Shift+M" } };
                string encoded = ConditionalCodec.Serialize(rule); var parsed = ConditionalCodec.Parse(encoded);
                Assert(parsed.Condition == ConditionKind.ForegroundApplication && parsed.Application == "Discord.exe" && parsed.WhenMatched.Target == "VolumeMute" && parsed.Otherwise.Target == "Ctrl+Shift+M", "conditional round trip");
                var c = new Configuration(); c.Mappings[0] = new Mapping { Kind = ActionKind.Conditional, Target = encoded }; var loaded = ConfigStore.Parse(ConfigStore.Serialize(c));
                Assert(ConditionalCodec.Parse(loaded.Mappings[0].Target).WhenMatched.Target == "VolumeMute", "configuration round trip");
                Reject(() => ConditionalCodec.Parse(encoded.Replace("\"version\":1", "\"version\":1,\"extra\":true")));
                Reject(() => ConditionalCodec.Serialize(new ConditionalRule { Application = @"C:\\private\\Discord.exe", WhenMatched = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" }, Otherwise = new Mapping { Kind = ActionKind.Unbound } }));
                Reject(() => ConditionalCodec.Serialize(new ConditionalRule { Application = "Discord.exe", WhenMatched = new Mapping { Kind = ActionKind.Conditional, Target = encoded }, Otherwise = new Mapping { Kind = ActionKind.Unbound } }));
            });
            Test("Conditional matching uses only current local application state", delegate {
                var foreground = new ConditionalRule { Condition = ConditionKind.ForegroundApplication, Application = "Discord.exe" };
                Assert(ConditionalActions.Matches(foreground, "discord", null) && !ConditionalActions.Matches(foreground, "notepad.exe", null), "foreground matching");
                var running = new ConditionalRule { Condition = ConditionKind.ApplicationRunning, Application = "Spotify.exe" };
                Assert(ConditionalActions.Matches(running, "", name => name == "Spotify") && !ConditionalActions.Matches(running, "", name => false), "running matching");
            });
            Test("Conditional dispatcher runs exactly one outcome", delegate {
                string process = Process.GetCurrentProcess().ProcessName + ".exe";
                var yesRule = new ConditionalRule { Condition = ConditionKind.ApplicationRunning, Application = process, WhenMatched = new Mapping { Kind = ActionKind.SendKey, Target = "F6" }, Otherwise = new Mapping { Kind = ActionKind.SendKey, Target = "F7" } };
                var noRule = new ConditionalRule { Condition = ConditionKind.ApplicationRunning, Application = "DefinitelyNotARealKiWeaveProcess.exe", WhenMatched = new Mapping { Kind = ActionKind.SendKey, Target = "F8" }, Otherwise = new Mapping { Kind = ActionKind.SendKey, Target = "F9" } };
                var sink = new FakeSink(); var dispatcher = new ActionDispatcher(sink); dispatcher.Execute(new Mapping { Kind = ActionKind.Conditional, Target = ConditionalCodec.Serialize(yesRule) }); dispatcher.Execute(new Mapping { Kind = ActionKind.Conditional, Target = ConditionalCodec.Serialize(noRule) });
                Assert(sink.Keys.Count == 2 && sink.Keys[0].SequenceEqual(new[] { (int)Keys.F6 }) && sink.Keys[1].SequenceEqual(new[] { (int)Keys.F9 }), "wrong branch routed");
            });
            Test("Sequence safety summary counts configured effects without executing", delegate {
                var steps = new[] { new SequenceStep { Action = new Mapping { Kind = ActionKind.Application, Target = @"C:\Tools\app.exe" } }, new SequenceStep { WaitMilliseconds = 1250 }, new SequenceStep { Action = new Mapping { Kind = ActionKind.HttpRequest, Target = "https://example.invalid/hook" } }, new SequenceStep { Action = new Mapping { Kind = ActionKind.Monitor, MonitorId = new string('a', 64), MonitorControl = "BrightnessUp", MonitorStep = 5 } } };
                var summary = ActionInsights.Sequence(steps); Assert(summary.Steps == 4 && summary.WaitMilliseconds == 1250 && summary.Launches == 1 && summary.NetworkRequests == 1 && summary.HardwareOperations == 1, summary.Compact);
            });
            Test("Sequence steps duplicate as independent copies", delegate {
                using (var form = new SequenceBuilderForm(new[] { new SequenceStep { Action = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+C" } } })) {
                    typeof(SequenceBuilderForm).GetMethod("DuplicateStep", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(form, null);
                    Assert(form.Result.Count == 2 && form.Result[1].Action.Target == "Ctrl+C" && !Object.ReferenceEquals(form.Result[0].Action, form.Result[1].Action), "duplicate step");
                }
            });
            Test("Action maturity labels distinguish stable and dependent actions", delegate {
                Assert(ActionInsights.Maturity(new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+C" }) == "Stable", "stable");
                Assert(ActionInsights.Maturity(new Mapping { Kind = ActionKind.HttpRequest, Target = "https://example.invalid/" }) == "Experimental", "experimental");
                Assert(ActionInsights.Maturity(new Mapping { Kind = ActionKind.Monitor }) == "Hardware-dependent", "hardware");
                Assert(ActionInsights.Maturity(new Mapping { Kind = ActionKind.Application, Target = @"C:\Tools\app.exe" }) == "App-dependent", "app");
                var conditional = new ConditionalRule { Application = "Discord.exe", WhenMatched = new Mapping { Kind = ActionKind.HttpRequest, Target = "https://example.invalid/" }, Otherwise = new Mapping { Kind = ActionKind.Unbound } };
                Assert(ActionInsights.Maturity(new Mapping { Kind = ActionKind.Conditional, Target = ConditionalCodec.Serialize(conditional) }) == "Experimental", "conditional maturity");
            });
            Test("Unsafe paths and control fields rejected", delegate {
                foreach (string p in new[] { @"\\server\share\app.exe", @"\\?\C:\app.exe", "https://example.com", "relative.exe", "C:\\app.exe:payload", "C:\\bad*.exe", "C:\\app.exe\n", "%WINDIR%\\notepad.exe" }) Reject(() => ConfigStore.Validate(new Mapping { Kind = ActionKind.Application, Target = p }, false));
                Reject(() => ConfigStore.Validate(new Mapping { Kind = ActionKind.Unbound, Arguments = "bad" }, false));
            });
            Test("Missing target accepted for import but blocked at execution", delegate { var m = new Mapping { Kind = ActionKind.Application, Target = Path.Combine(scratch, "missing.exe") }; ConfigStore.Validate(m, false); Reject(() => new ActionDispatcher(new FakeSink()).Execute(m)); });
            Test("Atomic save replaces and preserves previous version", delegate { string p = Path.Combine(scratch, "config.json"); var c = new Configuration(); ConfigStore.Save(p, c); c.Mappings[0].Kind = ActionKind.Unbound; ConfigStore.Save(p, c); Assert(ConfigStore.Load(p).Mappings[0].Kind == ActionKind.Unbound, "current"); Assert(ConfigStore.Load(p + ".bak").Mappings[0].Kind == ActionKind.PassThrough, "backup"); Assert(Directory.GetFiles(scratch, "*.tmp").Length == 0, "temp"); });
            Test("Rejected save does not replace valid configuration", delegate { string p = Path.Combine(scratch, "safe.json"); var c = new Configuration(); ConfigStore.Save(p, c); string before = File.ReadAllText(p); c.Mappings[0].Kind = ActionKind.Media; c.Mappings[0].Target = "bad"; Reject(() => ConfigStore.Save(p, c)); Assert(File.ReadAllText(p) == before, "unchanged"); });
            Test("F5 volume suppresses both edges and repeats", delegate { var m = new KeyStateMachine(); var c = Active(ActionKind.Media, "VolumeUp"); Assert(m.Process(0x74, true, false, c).Action != null, "down action"); var r = m.Process(0x74, true, false, c); Assert(r.Suppress && r.Action != null, "repeat"); r = m.Process(0x74, false, false, c); Assert(r.Suppress && r.Action == null, "up swallowed"); });
            Test("All twelve keys can be swallowed", delegate { var c = new Configuration { Enabled = true }; foreach (var a in c.Mappings) a.Kind = ActionKind.Unbound; var m = new KeyStateMachine(); for (int k = 0x70; k <= 0x7B; k++) { var d = m.Process(k, true, false, c); Assert(d.Suppress && d.Action == null, "unbound down"); Assert(m.Process(k, false, false, c).Suppress, "unbound up"); } });
            Test("Pass through and unrelated keys remain untouched", delegate { var c = new Configuration { Enabled = true }; var m = new KeyStateMachine(); for (int k = 0; k < 256; k++) { Assert(!m.Process(k, true, false, c).Suppress, "down"); Assert(!m.Process(k, false, false, c).Suppress, "up"); } });
            Test("Global disable restores new presses", delegate { var c = Active(ActionKind.Unbound, ""); c.Enabled = false; Assert(!new KeyStateMachine().Process(0x74, true, false, c).Suppress, "disabled"); });
            Test("Injected keys cannot recurse or consume physical state", delegate { var c = Active(ActionKind.SendKey, "F5"); var m = new KeyStateMachine(); m.Process(0x74, true, false, c); var d = m.Process(0x74, true, true, c); Assert(!d.Suppress && d.Action == null, "injected down"); Assert(!m.Process(0x74, false, true, c).Suppress, "injected up"); Assert(m.Process(0x74, false, false, c).Suppress, "physical up remains paired"); });
            Test("Launches, shortcuts and toggles run once per press", delegate { foreach (var kind in new[] { ActionKind.Application, ActionKind.Command, ActionKind.FileOrFolder, ActionKind.WindowsShortcut, ActionKind.SendKey, ActionKind.SendShortcut, ActionKind.Media }) { var c = Active(kind, "VolumeMute"); var m = new KeyStateMachine(); Assert(m.Process(0x74, true, false, c).Action != null, "initial"); for (int n = 0; n < 30; n++) Assert(m.Process(0x74, true, false, c).Action == null, "repeat blocked"); m.Process(0x74, false, false, c); Assert(m.Process(0x74, true, false, c).Action != null, "next press"); } });
            Test("Disable mid-press keeps release paired", delegate { var c = Active(ActionKind.Media, "VolumeUp"); var m = new KeyStateMachine(); m.Process(0x74, true, false, c); c.Enabled = false; m.CancelHeldActions(); var d = m.Process(0x74, true, false, c); Assert(d.Suppress && d.Action == null, "repeat off"); Assert(m.Process(0x74, false, false, c).Suppress, "matching up"); Assert(!m.Process(0x74, true, false, c).Suppress, "new pass"); });
            Test("Enable mid-press preserves original pass-through release", delegate { var c = Active(ActionKind.Unbound, ""); c.Enabled = false; var m = new KeyStateMachine(); m.Process(0x74, true, false, c); c.Enabled = true; Assert(!m.Process(0x74, true, false, c).Suppress && !m.Process(0x74, false, false, c).Suppress, "paired pass"); Assert(m.Process(0x74, true, false, c).Suppress, "next remap"); });
            Test("Emergency chord requires 1.5 seconds, fires once, rearms", delegate { var e = new EmergencyHold(); Assert(!e.Tick(true, 0) && !e.Tick(true, 1499) && e.Tick(true, 1500), "hold"); Assert(!e.Tick(true, 3000), "once"); e.Tick(false, 3100); Assert(!e.Tick(true, 3200) && e.Tick(true, 4700), "rearm"); });
            Test("Action dispatcher routes keyboard and all media actions", delegate { var sink = new FakeSink(); var d = new ActionDispatcher(sink); d.Execute(new Mapping { Kind = ActionKind.SendShortcut, Target = "Alt+Tab" }); foreach (string s in Shortcuts.MediaLabels.Keys) d.Execute(new Mapping { Kind = ActionKind.Media, Target = s }); d.Execute(new Mapping { Kind = ActionKind.Unbound }); Assert(sink.Keys.Count == 7 && sink.Keys[0].SequenceEqual(new[] { 18, 9 }) && sink.Launches.Count == 0, "routes"); });
            Test("App, file, folder, shortcut and command dispatch preserve details", delegate {
                string exe = Process.GetCurrentProcess().MainModule.FileName, file = Path.Combine(scratch, "sample.txt"), lnk = Path.Combine(scratch, "sample.lnk"); File.WriteAllText(file, "test"); File.WriteAllText(lnk, "placeholder; fake sink only");
                var sink = new FakeSink(); var d = new ActionDispatcher(sink);
                d.Execute(new Mapping { Kind = ActionKind.Application, Target = exe, Arguments = "one two", WorkingDirectory = scratch });
                d.Execute(new Mapping { Kind = ActionKind.FileOrFolder, Target = file }); d.Execute(new Mapping { Kind = ActionKind.FileOrFolder, Target = scratch }); d.Execute(new Mapping { Kind = ActionKind.WindowsShortcut, Target = lnk });
                d.Execute(new Mapping { Kind = ActionKind.Command, Target = exe, Arguments = "hello", WorkingDirectory = scratch });
                Assert(sink.Launches.Count == 5 && sink.Launches[0].Arguments == "one two" && sink.Launches[3].UseShellExecute && !sink.Launches[4].UseShellExecute, "dispatch");
            });
            Test("Script dispatch quotes paths without bypassing execution policy", delegate { var p = ActionDispatcher.BuildLaunch(new Mapping { Kind = ActionKind.Command, Target = @"C:\a b\test.ps1", Arguments = "-Name abc" }); Assert(p.Arguments.Contains("-File \"C:\\a b\\test.ps1\"") && !p.Arguments.Contains("Bypass"), "powershell"); p = ActionDispatcher.BuildLaunch(new Mapping { Kind = ActionKind.Command, Target = @"C:\a b\test.cmd", Arguments = "x" }); Assert(p.Arguments == "/d /s /c \"\"C:\\a b\\test.cmd\" x\"", "cmd quoting"); });
            Test("Real harmless command runs with arguments and working directory", delegate { string script = Path.Combine(scratch, "test command.cmd"); File.WriteAllText(script, "@echo off\r\necho %~1> command-result.txt\r\n"); new ActionDispatcher(new WindowsActionSink()).Execute(new Mapping { Kind = ActionKind.Command, Target = script, Arguments = "\"hello world\"", WorkingDirectory = scratch }); string output = Path.Combine(scratch, "command-result.txt"); WaitFor(() => File.Exists(output) && new FileInfo(output).Length > 0); Assert(File.ReadAllText(output).Trim() == "hello world", "command result"); });
            Test("Native INPUT layout and release/extended flags", delegate { Assert(Marshal.SizeOf(typeof(Native.Input)) == 40, "x64 INPUT layout"); Assert(Native.Key(0x27, true, Native.Tag).Data.Key.Flags == 3, "extended keyup"); Assert(Native.Key(0x41, false, Native.Tag).Data.Key.Flags == 0, "regular down"); });
            Test("Random malformed input never becomes executable configuration", delegate { var random = new Random(9); for (int i = 0; i < 1500; i++) { string s = new string(Enumerable.Range(0, random.Next(1, 100)).Select(n => (char)random.Next(0, 128)).ToArray()); try { ConfigStore.Parse(s); throw new Exception("Unexpected valid random config"); } catch (ArgumentException) { } } });
            Test("DDC config upgrades to version 2 and round-trips", delegate { var c = MonitorConfig(); string json = ConfigStore.Serialize(c); Assert(json.Contains("\"version\": 2"), "version"); var parsed = ConfigStore.Parse(json); Assert(parsed.Mappings[0].MonitorId == c.Mappings[0].MonitorId && parsed.Mappings[0].MonitorStep == 5 && parsed.Mappings[0].MonitorControl == "VolumeDown", "monitor fields"); Assert(ConfigStore.Serialize(parsed) == json, "round trip"); Reject(() => ConfigStore.Parse(json.Replace("\"version\": 2", "\"version\": 1"))); });
            Test("DDC rejects arbitrary VCP operations and invalid identities/steps", delegate { var m = MonitorConfig().Mappings[0]; m.MonitorControl = "PowerOff"; Reject(() => ConfigStore.Validate(m, false)); m.MonitorControl = "VolumeDown"; m.MonitorId = "primary"; Reject(() => ConfigStore.Validate(m, false)); m.MonitorId = new string('a',64); m.MonitorStep = 0; Reject(() => ConfigStore.Validate(m, false)); m.MonitorStep = 21; Reject(() => ConfigStore.Validate(m, false)); m.MonitorStep = 5; m.Arguments = "cmd"; Reject(() => ConfigStore.Validate(m, false)); });
            Test("DDC strict schema rejects extra launch fields and fractional step", delegate { string json = ConfigStore.Serialize(MonitorConfig()); Reject(() => ConfigStore.Parse(json.Replace("\"step\":5", "\"step\":5,\"target\":\"x\""))); Reject(() => ConfigStore.Parse(json.Replace("\"step\":5", "\"step\":1.5"))); });
            Test("DDC capability parser ignores nested values", delegate { var codes = DdcService.ParseVcpCodes("(prot(monitor)vcp(10 12 60(01 10 12 62) 62)mccs_ver(2.1))"); Assert(codes.SetEquals(new byte[] {16,18,96,98}), "codes"); codes = DdcService.ParseVcpCodes("(vcp(60(10 12 62)))"); Assert(codes.SetEquals(new byte[] {96}), "nested excluded"); Assert(DdcService.ParseVcpCodes("vcp(10") == null && DdcService.ParseVcpCodes(null) == null, "fallback"); });
            Test("DDC steps scale and clamp without overflow", delegate { Assert(DdcOperation.Next(98,100,5,1)==100 && DdcOperation.Next(2,100,5,-1)==0, "bounds"); Assert(DdcOperation.Next(50,255,5,1)==63, "scaled"); Assert(DdcOperation.Next(0,10,1,1)==1, "minimum step"); Assert(DdcOperation.Next(65530,65535,20,1)==65535, "overflow"); Reject(() => DdcOperation.Next(1,0,5,1)); Reject(() => DdcOperation.Next(101,100,5,1)); });
            Test("DDC dispatch is separate from keys and launches and propagates cancellation", delegate { var sink = new FakeSink(); var monitor = new FakeDdc(); var d = new ActionDispatcher(sink, monitor); var m = MonitorConfig().Mappings[0]; d.Execute(m, () => false); Assert(monitor.Calls == 1 && !monitor.Allowed && monitor.Last.MonitorControl == "VolumeDown" && sink.Keys.Count == 0 && sink.Launches.Count == 0, "routing"); });
            Test("DDC held key repeats and disable cancels repeat", delegate { var c = MonitorConfig(); c.Enabled = true; var m = new KeyStateMachine(); Assert(m.Process(0x70,true,false,c).Action != null && m.Process(0x70,true,false,c).Action != null, "repeat"); c.Enabled = false; m.CancelHeldActions(); Assert(m.Process(0x70,true,false,c).Action == null && m.Process(0x70,false,false,c).Suppress, "disabled pair"); });
            Test("Undetected monitor cannot dispatch native writes", delegate { var service = new DdcService(); bool rejected = false; try { service.Apply(MonitorConfig().Mappings[0], () => true); } catch (InvalidOperationException) { rejected = true; } Assert(rejected,"unsupported"); service.Apply(MonitorConfig().Mappings[0], () => false); });
            Test("Preferences default, migrate, persist and back up", delegate { string path = Path.Combine(scratch,"preferences.json"); var defaults = UserPreferences.Load(path); Assert(defaults.UseTray && defaults.CheckUpdates && defaults.AutomaticProfiles && !defaults.NetworkAccess,"privacy-first defaults"); var migrated = UserPreferences.Parse("{\"version\":1,\"useTray\":false}"); Assert(!migrated.UseTray && migrated.CheckUpdates && migrated.AutomaticProfiles && !migrated.NetworkAccess,"v1 migration"); var v2 = UserPreferences.Parse("{\"version\":2,\"useTray\":true,\"checkUpdates\":true,\"automaticProfiles\":true}"); Assert(v2.NetworkAccess,"v2 preserves enabled updates"); UserPreferences.Save(path,new UserPreferences {UseTray=false,CheckUpdates=false,AutomaticProfiles=true,NetworkAccess=true}); var loaded=UserPreferences.Load(path); Assert(!loaded.UseTray && !loaded.CheckUpdates && loaded.AutomaticProfiles && loaded.NetworkAccess,"saved"); UserPreferences.Save(path,new UserPreferences {UseTray=true,CheckUpdates=true,AutomaticProfiles=false,NetworkAccess=false}); Assert(UserPreferences.Load(path).UseTray && !UserPreferences.Load(path+".bak").UseTray,"backup"); });
            Test("Preferences reject malformed and ambiguous input", delegate { foreach (string json in new[] {"{}", "{\"version\":2,\"useTray\":true}", "{\"version\":2,\"useTray\":true,\"checkUpdates\":true,\"automaticProfiles\":true,\"extra\":false}", "{\"version\":3,\"useTray\":true,\"checkUpdates\":true,\"automaticProfiles\":true}", "{\"version\":1,\"useTray\":\"false\"}", "{\"version\":1,\"useTray\":true,\"useTray\":false}", "{\"version\":1,\"useTray\":true,\"command\":\"x\"}"}) Reject(() => UserPreferences.Parse(json)); });
            Test("Private mapping notes persist outside exported configuration", delegate { string path = Path.Combine(scratch, "mapping-notes.json"); var notes = new Dictionary<string, string>(); string key = MappingNoteStore.FunctionKey("Default", "Base", 4); MappingNoteStore.Set(notes, key, "Use this for streaming"); MappingNoteStore.Save(path, notes); var loaded = MappingNoteStore.Load(path); Assert(loaded[key] == "Use this for streaming", "note round trip"); MappingNoteStore.Set(loaded, key, ""); Assert(!loaded.ContainsKey(key), "note clear"); Assert(!ConfigStore.Serialize(new Configuration()).Contains("streaming"), "note leaked into config"); Reject(() => MappingNoteStore.Set(notes, key, new string('x', MappingNoteStore.MaxNoteLength + 1))); });
            Test("Custom hotkey copies preserve action templates", delegate { var source = new CustomHotkey { Shortcut = "Ctrl+Alt+R", Action = new Mapping { Kind = ActionKind.SendKey, Target = "R" } }; var copy = source.Copy(); copy.Shortcut = "Ctrl+Alt+K"; Assert(copy.Action.Target == "R" && copy.Shortcut != source.Shortcut && !Object.ReferenceEquals(copy.Action, source.Action), "template copy"); });
            Test("Discord authorization status explains refresh failure", delegate { Assert(DiscordIntegration.StatusText(true, true, true) == "Connected", "connected status"); Assert(DiscordIntegration.StatusText(false, true, false).Contains("Authorized"), "authorized status"); Assert(DiscordIntegration.StatusText(false, true, true).Contains("reconnect"), "refresh warning"); Assert(DiscordIntegration.StatusText(false, false, false) == "Not connected", "empty status"); });
            Test("PowerToys drift fingerprint is stable and descriptive", delegate { var a = new[] { new PowerToysShortcut { Module = "Keyboard Manager", Action = "Color", Chord = "Ctrl+Alt+C", ModuleEnabled = true }, new PowerToysShortcut { Module = "FancyZones", Action = "Layout", Chord = "Win+`", ModuleEnabled = false } }; var b = a.Reverse().ToArray(); Assert(PowerToysDrift.Fingerprint(a) == PowerToysDrift.Fingerprint(b), "order-sensitive fingerprint"); string first = PowerToysDrift.Fingerprint(a), changed = PowerToysDrift.Fingerprint(new[] { a[0] }); Assert(PowerToysDrift.Compare("", first) == "Baseline recorded" && PowerToysDrift.Compare(first, first).Contains("No changes") && PowerToysDrift.Compare(first, changed).Contains("Changed"), "drift comparison"); });
            Test("Monitor capability drift fingerprint is stable and descriptive", delegate { var a = new[] { new DdcMonitor { Id = "a", Name = "Desk", Codes = new byte[] { 0x12, 0x10 } } }; var b = new[] { new DdcMonitor { Id = "a", Name = "Desk", Codes = new byte[] { 0x10, 0x12 } } }; Assert(MonitorDrift.Fingerprint(a) == MonitorDrift.Fingerprint(b), "monitor order-sensitive fingerprint"); string first = MonitorDrift.Fingerprint(a), changed = MonitorDrift.Fingerprint(new DdcMonitor[0]); Assert(MonitorDrift.Compare("", first) == "Baseline recorded" && MonitorDrift.Compare(first, first).Contains("No changes") && MonitorDrift.Compare(first, changed).Contains("Changed"), "monitor drift comparison"); });
            Test("Audio availability drift fingerprint is descriptive", delegate { string first = AudioDrift.Fingerprint("speaker-a\nspeaker-b"), same = AudioDrift.Fingerprint("speaker-a\nspeaker-b"), changed = AudioDrift.Fingerprint("speaker-a"); Assert(first == same && first != changed && AudioDrift.Compare("", first) == "Baseline recorded" && AudioDrift.Compare(first, same).Contains("No changes") && AudioDrift.Compare(first, changed).Contains("Changed"), "audio drift comparison"); });
            Test("Keyboard layout drift comparison is descriptive", delegate { Assert(KeyboardLayoutDrift.Compare("", "00000409|0x409") == "Baseline recorded" && KeyboardLayoutDrift.Compare("00000409|0x409", "00000409|0x409").Contains("No changes") && KeyboardLayoutDrift.Compare("00000409|0x409", "00000407|0x407").Contains("Changed"), "keyboard layout drift comparison"); });
            Test("Script workspace only accepts supported script types", delegate { Assert(ScriptWorkspace.IsSupported("demo.py") && ScriptWorkspace.IsSupported("demo.ps1") && !ScriptWorkspace.IsSupported("demo.exe"), "script workspace extensions"); });
            Test("Feature flags classify experimental actions", delegate { Assert(FeatureFlags.IsExperimental(new Mapping { Kind = ActionKind.HttpRequest }) && FeatureFlags.IsExperimental(new Mapping { Kind = ActionKind.Command }) && !FeatureFlags.IsExperimental(new Mapping { Kind = ActionKind.SendKey }), "feature flag classification"); });
            Test("Session awareness describes remote and virtual contexts", delegate { Assert(SessionAwareness.Describe(false, false) == "Local Windows session" && SessionAwareness.Describe(true, false).Contains("Remote") && SessionAwareness.Describe(false, true).ToLowerInvariant().Contains("virtual"), "session context descriptions"); });
            Test("History private notes stay in sidecars", delegate { string path = Path.Combine(scratch, "restore-point.keyweave"); File.WriteAllText(path, "placeholder"); ConfigurationHistory.SaveNote(path, "Review before restoring"); Assert(ConfigurationHistory.LoadNote(path) == "Review before restoring" && File.Exists(ConfigurationHistory.NotePath(path)), "history note sidecar"); ConfigurationHistory.SaveNote(path, ""); Assert(!File.Exists(ConfigurationHistory.NotePath(path)), "history note clear"); });
            Test("Path migration updates nested mapping paths", delegate { var c = new Configuration(); c.Mappings[0] = new Mapping { Kind = ActionKind.Application, Target = Path.Combine(scratch, "old", "tool.exe"), WorkingDirectory = Path.Combine(scratch, "old") }; int changed = PathMigration.Replace(c, Path.Combine(scratch, "old"), Path.Combine(scratch, "new")); Assert(changed == 2 && c.Mappings[0].Target.EndsWith("new" + Path.DirectorySeparatorChar + "tool.exe"), "path migration"); });
            Test("Repeated crash guard offers Safe Mode without forcing it early", delegate { Assert(!StartupGuard.ShouldOfferRecovery(0) && !StartupGuard.ShouldOfferRecovery(1) && StartupGuard.ShouldOfferRecovery(2), "threshold"); Assert(StartupGuard.NextAttempts(0) == 1 && StartupGuard.NextAttempts(1) == 2 && StartupGuard.NextAttempts(99) == 4, "bounded attempts"); });
            Test("Audit trail stays redacted and bounded", delegate { string path = Path.Combine(scratch, "audit.log"); AuditTrail.Record(path, "configuration-save"); AuditTrail.Record(path, "configuration-import-staged"); AuditTrail.Record(path, "C:\\private\\secret.exe"); for (int i = 0; i < AuditTrail.MaxEntries + 8; i++) AuditTrail.Record(path, "event-" + (i % 10)); var lines = AuditTrail.Read(path); Assert(lines.Count == AuditTrail.MaxEntries && lines.All(x => !x.Contains("private") && !x.Contains("secret.exe")), "audit redaction/rotation"); });
            Test("Notification severity migrates and gates warnings", delegate { string path = NotificationPreferences.Path; string old = File.Exists(path) ? File.ReadAllText(path) : null; try { File.WriteAllText(path, "{\"version\":2,\"updates\":true,\"health\":true,\"safety\":true,\"severity\":\"Critical only\"}"); var strict = NotificationPreferences.Load(); Assert(!strict.AllowsWarning && strict.Severity == "Critical only", "critical severity"); } finally { if (old == null) { try { File.Delete(path); } catch { } } else File.WriteAllText(path, old); } });
            Test("Corrupt preferences remain untouched on load failure", delegate { string path=Path.Combine(scratch,"corrupt-preferences.json"); File.WriteAllText(path,"{broken"); Reject(() => UserPreferences.Load(path)); Assert(File.ReadAllText(path)=="{broken","preserved"); });
            Test("Profiles round-trip with automatic app matches", delegate {
                string path = Path.Combine(scratch, "profiles.json");
                var profile = new KeyWeaveProfile { Name = "Streaming", Applications = new[] { "obs64.exe", "Discord" }, Configuration = new Configuration() };
                var collection = new ProfileCollection { Profiles = new[] { profile } }; ProfileStore.Save(path, collection);
                var loaded = ProfileStore.Load(path); Assert(loaded.Profiles.Length == 1 && loaded.Find("streaming") != null, "profile missing");
                Assert(loaded.ForApplication("OBS64") != null && loaded.ForApplication("discord.exe") != null, "automatic match");
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                string legacy = serializer.Serialize(new { version = 1, profiles = new[] { new { name = "Legacy", applications = new[] { "old.exe" }, configuration = ConfigStore.Serialize(new Configuration()) } } });
                var migrated = ProfileStore.Parse(legacy); Assert(migrated.Find("Legacy") != null && migrated.Find("Legacy").InheritFrom == "" && migrated.Find("Legacy").OverrideKeys.Length == 0, "version 1 migration");
                var merged = ProfileCollection.Merge(collection, new ProfileCollection { Profiles = new[] { new KeyWeaveProfile { Name = "Streaming", Configuration = new Configuration() } } }); Assert(merged.Profiles.Length == 2 && merged.Find("Streaming (2)") != null, "profile merge rename");
            });
            Test("Profile inheritance resolves overrides and rejects loops", delegate {
                var defaults = new Configuration { Enabled = true }; defaults.Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" };
                var workEffective = defaults.Copy(); workEffective.Mappings[1] = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" };
                var work = new KeyWeaveProfile { Name = "Work", InheritFrom = "Default", Configuration = workEffective.Copy() };
                var childEffective = workEffective.Copy(); childEffective.Mappings[2] = new Mapping { Kind = ActionKind.Media, Target = "MediaPlayPause" };
                var child = new KeyWeaveProfile { Name = "Editing", InheritFrom = "Work", Configuration = childEffective.Copy() };
                var collection = new ProfileCollection { Profiles = new[] { work, child } };
                ProfileStore.SetEffectiveConfiguration(collection, work, workEffective, defaults); ProfileStore.SetEffectiveConfiguration(collection, child, childEffective, defaults);
                Assert(work.OverrideKeys.SequenceEqual(new[] { "F2" }) && child.OverrideKeys.SequenceEqual(new[] { "F3" }), "intentional overrides");
                defaults.Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeDown" };
                var resolved = collection.Resolve("Editing", defaults); Assert(resolved.Mappings[0].Target == "VolumeDown" && resolved.Mappings[1].Target == "VolumeMute" && resolved.Mappings[2].Target == "MediaPlayPause", "inheritance resolution");
                string json = ProfileStore.Serialize(collection); var loaded = ProfileStore.Parse(json); Assert(json.Contains("\"version\":3") && loaded.Resolve("Editing", defaults).Mappings[0].Target == "VolumeDown", "inheritance round trip");
                loaded.Find("Work").InheritFrom = "Editing"; Reject(() => ProfileStore.Validate(loaded, false));
            });
            Test("Conflict center explains Windows, PowerToys, layer and profile winners", delegate {
                var defaults = new Configuration { CustomHotkeys = new[] {
                    new CustomHotkey { Shortcut = "Win+L", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+K", Action = new Mapping { Kind = ActionKind.Command, Target = @"C:\private\secret.cmd" } },
                    new CustomHotkey { Shortcut = "Ctrl+CapsLock", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } }
                }, Layers = new[] { new ModifierLayer { Name = "Media", ActivationKey = "CapsLock" } } };
                var profiles = new ProfileCollection { Profiles = new[] {
                    new KeyWeaveProfile { Name = "First", Applications = new[] { "game.exe" } },
                    new KeyWeaveProfile { Name = "Second", Applications = new[] { "GAME" } }
                } };
                var pt = new[] { new PowerToysShortcut { Module = "ColorPicker", Action = "Activation", Chord = "Ctrl+Alt+K", ModuleEnabled = true } };
                var issues = ConflictScanner.Scan(defaults, profiles, pt); string report = String.Join("\n", issues.Select(x => x.Title + " " + x.Detail + " " + x.Winner));
                Assert(issues.Any(x => x.Title.Contains("owned by Windows") && x.Winner.Contains("Windows")), "Windows ownership");
                Assert(issues.Any(x => x.Title.Contains("PowerToys") && x.Winner.Contains("registered first")), "PowerToys ownership");
                Assert(issues.Any(x => x.Title.Contains("layer key") && x.Winner.Contains("layer rule wins")), "layer ownership");
                Assert(CollisionSimulator.Simulate("Ctrl+Alt+K", defaults, profiles, pt).Contains("PowerToys"), "collision simulation");
                Assert(issues.Any(x => x.Title.Contains("more than one profile") && x.Winner.Contains("First wins")), "profile ownership");
                Assert(!report.Contains("secret.cmd") && !report.Contains(@"C:\private"), "private target leaked");
                Assert(issues.All(x => !String.IsNullOrWhiteSpace(x.Suggestion)), "conflict suggestions missing");
            });
            Test("Profiles reject duplicate app ownership", delegate {
                var collection = new ProfileCollection { Profiles = new[] {
                    new KeyWeaveProfile { Name = "One", Applications = new[] { "game.exe" } },
                    new KeyWeaveProfile { Name = "Two", Applications = new[] { "GAME" } }
                } };
                Reject(() => ProfileStore.Validate(collection, false));
            });
            Test("Modifier layers round-trip without changing older configs", delegate {
                var c = new Configuration { Enabled = true, Layers = new[] { new ModifierLayer { Name = "Media", ActivationKey = "CapsLock" } } };
                c.Layers[0].Mappings[4] = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" };
                string json = ConfigStore.Serialize(c); Assert(json.Contains("\"version\": 4") && json.Contains("\"layers\""), "version 4");
                var parsed = ConfigStore.Parse(json); Assert(parsed.Layers.Length == 1 && parsed.Layers[0].Name == "Media" && parsed.Layers[0].Mappings[4].Target == "VolumeMute", "layer round trip");
                Assert(ConfigStore.Parse(ConfigStore.Serialize(new Configuration())).Layers.Length == 0, "old configuration behavior");
            });
            Test("Modifier layers reject duplicate keys and invalid mappings", delegate {
                var c = new Configuration { Layers = new[] { new ModifierLayer { Name = "One", ActivationKey = "CapsLock" }, new ModifierLayer { Name = "Two", ActivationKey = "CapsLock" } } }; Reject(() => ConfigStore.Validate(c, false));
                c.Layers[1].ActivationKey = "Apps"; c.Layers[1].Name = "one"; Reject(() => ConfigStore.Validate(c, false));
                c.Layers[1].Name = "Two"; c.Layers[0].Mappings = new Mapping[11]; Reject(() => ConfigStore.Validate(c, false));
            });
            Test("Held layer key selects layer mappings and preserves releases", delegate {
                var c = new Configuration { Enabled = true, Layers = new[] { new ModifierLayer { Name = "Media", ActivationKey = "CapsLock" } } };
                c.Layers[0].Mappings[4] = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" };
                var machine = new KeyStateMachine(); var layerDown = machine.Process((int)Keys.CapsLock, true, false, c); Assert(layerDown.Suppress && layerDown.Action == null, "layer down");
                var fDown = machine.Process(0x74, true, false, c); Assert(fDown.Suppress && fDown.Action != null && fDown.Action.Target == "VolumeMute", "layer mapping");
                Assert(machine.Process(0x74, false, false, c).Suppress && machine.Process((int)Keys.CapsLock, false, false, c).Suppress, "paired releases");
                Assert(!machine.Process(0x74, true, false, c).Suppress && !machine.Process(0x74, false, false, c).Suppress, "base restored");
                Assert(!machine.Process((int)Keys.CapsLock, true, true, c).Suppress, "injected layer ignored");
            });
            Test("Live key diagnostics describe decisions without retaining history", delegate {
                var c = new Configuration { Enabled = true, Layers = new[] { new ModifierLayer { Name = "Media", ActivationKey = "CapsLock" } } };
                c.Layers[0].Mappings[4] = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" };
                var machine = new KeyStateMachine(); var layer = machine.Process((int)Keys.CapsLock, true, false, c); var mapped = machine.Process(0x74, true, false, c);
                Assert(layer.Suppress && layer.LayerName == "Media" && layer.ResolvedAction.Contains("Activate"), "layer diagnostic");
                Assert(mapped.Suppress && mapped.LayerName == "Media" && mapped.ResolvedAction == "Mute / unmute", "mapping diagnostic");
                machine.Process(0x74, false, false, c); machine.Process((int)Keys.CapsLock, false, false, c);
            });
            Test("Modifier layer state remains paired across disable and competing keys", delegate {
                var c = new Configuration { Enabled = true, Layers = new[] {
                    new ModifierLayer { Name = "Media", ActivationKey = "CapsLock" },
                    new ModifierLayer { Name = "Apps", ActivationKey = "Apps" }
                } };
                c.Layers[0].Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" };
                c.Layers[1].Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" };
                var machine = new KeyStateMachine();
                Assert(machine.Process((int)Keys.CapsLock, true, false, c).Suppress, "first layer down");
                Assert(!machine.Process((int)Keys.Apps, true, false, c).Suppress, "competing layer must pass through");
                var mapped = machine.Process(0x70, true, false, c); Assert(mapped.Suppress && mapped.Action.Target == "VolumeMute", "first layer remains active");
                Assert(machine.Process(0x70, false, false, c).Suppress && !machine.Process((int)Keys.Apps, false, false, c).Suppress, "function and competing releases pair");
                c.Enabled = false; machine.CancelHeldActions(); Assert(machine.Process((int)Keys.CapsLock, false, false, c).Suppress, "layer release remains paired after disable");
                Assert(!machine.Process((int)Keys.CapsLock, true, false, c).Suppress && !machine.Process((int)Keys.CapsLock, false, false, c).Suppress, "disabled layer passes through");
            });
            Test("KeyWeave backup format round-trips and rejects unknown fields", delegate {
                var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+B", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" } } } };
                var p = new ProfileCollection { Profiles = new[] { new KeyWeaveProfile { Name = "Games", Applications = new[] { "game.exe" }, Configuration = c.Copy() } } };
                string text = BackupBundle.Serialize(c, p, new UserPreferences { UseTray = false, NetworkAccess = true }, true); var loaded = BackupBundle.Parse(text);
                Assert(loaded.Configuration.Enabled && loaded.Configuration.CustomHotkeys.Length == 1 && loaded.Profiles.Find("Games") != null && !loaded.Preferences.UseTray && loaded.Preferences.NetworkAccess && loaded.StartWithWindows, "backup round trip");
                string path = Path.Combine(scratch, "setup.keyweave"); BackupBundle.Save(path, c, p, new UserPreferences { UseTray = false }, true); Assert(BackupBundle.Load(path).Profiles.Find("Games") != null, "backup file load");
                BackupBundle.Save(path, new Configuration(), new ProfileCollection(), new UserPreferences(), false); Assert(File.Exists(path + ".bak") && BackupBundle.Load(path + ".bak").Configuration.Enabled, "backup atomic replacement");
                Reject(() => BackupBundle.Parse(text.Replace("\"format\":\"KeyWeave Backup\"", "\"unknown\":1,\"format\":\"KeyWeave Backup\"")));
                Reject(() => BackupBundle.Parse(new string('x', BackupBundle.MaxBytes + 1)));
            });
            Test("Configuration history is bounded, local and compares without exposing targets", delegate {
                string folder = Path.Combine(scratch, "history"); var profiles = new ProfileCollection(); var preferences = new UserPreferences();
                var first = new Configuration(); first.Mappings[0] = new Mapping { Kind = ActionKind.Application, Target = @"C:\private\secret.exe" };
                ConfigurationHistory.Capture(folder, "mapping save", first, profiles, preferences, false, 2);
                var second = first.Copy(); second.Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" }; ConfigurationHistory.Capture(folder, "profile save", second, profiles, preferences, false, 2);
                var third = second.Copy(); third.Mappings[1] = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+S" }; ConfigurationHistory.Capture(folder, "backup restore", third, profiles, preferences, true, 2);
                var entries = ConfigurationHistory.List(folder); Assert(entries.Count == 2 && entries.All(x => x.Backup.PowerToys.Length == 0), "bounded/private history");
                string comparison = ConfigurationHistory.Compare(new KeyWeaveBackup { Configuration = first, Profiles = profiles, Preferences = preferences, StartWithWindows = false }, third, profiles, preferences, true);
                Assert(comparison.Contains("F1") && comparison.Contains("Start with Windows") && !comparison.Contains("private") && !comparison.Contains("secret.exe"), "safe comparison");
            });
            Test("Global mapping search covers profiles, layers, hotkeys and nested actions without displaying private targets", delegate {
                var defaults = new Configuration(); defaults.Mappings[0] = new Mapping { Kind = ActionKind.Application, Target = @"C:\private\secret-tool.exe" };
                defaults.Layers = new[] { new ModifierLayer { Name = "Media", ActivationKey = "CapsLock" } }; defaults.Layers[0].Mappings[1] = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" };
                defaults.CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+K", Action = new Mapping { Kind = ActionKind.Conditional, Target = ConditionalCodec.Serialize(new ConditionalRule { Application = "Discord.exe", WhenMatched = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+Shift+M" }, Otherwise = new Mapping { Kind = ActionKind.Unbound } }) } } };
                var gaming = new KeyWeaveProfile { Name = "Gaming", Configuration = defaults.Copy() }; gaming.Configuration.Mappings[2] = new Mapping { Kind = ActionKind.Media, Target = "MediaPlayPause" };
                var index = MappingSearchIndex.Build(defaults, new ProfileCollection { Profiles = new[] { gaming } });
                Assert(MappingSearchIndex.Filter(index, "secret-tool").Count == 2, "private target search");
                Assert(index.All(x => !x.Location.Contains("secret-tool") && !x.Action.Contains("secret-tool")), "private target displayed");
                Assert(MappingSearchIndex.Filter(index, "Media F2 mute").Count == 2, "layer search");
                Assert(MappingSearchIndex.Filter(index, "Discord Ctrl+Shift+M").Count == 2, "nested condition search");
                Assert(MappingSearchIndex.Filter(index, "Gaming Base F3").Count == 1, "profile search");
            });
            Test("Crash recovery draft stays private, preserves profile context and never activates itself", delegate {
                string folder = Path.Combine(scratch, "draft-recovery"), path = Path.Combine(folder, "draft.keyweave"), meta = Path.Combine(folder, "profile.txt");
                var defaults = new Configuration(); var edited = defaults.Copy(); edited.Mappings[0] = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+Shift+S" };
                var profiles = new ProfileCollection { Profiles = new[] { new KeyWeaveProfile { Name = "Work", Configuration = defaults.Copy() } } };
                RecoveryStore.SaveDraft(path, meta, defaults, profiles, new UserPreferences { NetworkAccess = false }, false, "Work", edited);
                KeyWeaveBackup recovered; string profile; Assert(RecoveryStore.TryLoadDraft(path, meta, out recovered, out profile), "draft missing");
                Assert(profile == "Work" && recovered.Profiles.Resolve("Work", recovered.Configuration).Mappings[0].Target == "Ctrl+Shift+S", "draft/profile context");
                Assert(!recovered.Configuration.Enabled && !recovered.Preferences.NetworkAccess && recovered.PowerToys.Length == 0, "draft gained active/network/integration state");
            });
            Test("KiWeave data migration copies legacy state without overwriting newer files", delegate {
                string legacy = Path.Combine(scratch, "legacy-data"), current = Path.Combine(scratch, "kiweave-data"); Directory.CreateDirectory(legacy); Directory.CreateDirectory(current);
                File.WriteAllText(Path.Combine(legacy, "config.json"), "legacy"); File.WriteAllText(Path.Combine(legacy, "preferences.json"), "legacy prefs"); File.WriteAllText(Path.Combine(current, "preferences.json"), "new prefs");
                Directory.CreateDirectory(Path.Combine(legacy, "History")); File.WriteAllText(Path.Combine(legacy, "History", "one.keyweave"), "history");
                AppStorage.MigrateLegacy(legacy, current);
                Assert(File.ReadAllText(Path.Combine(current, "config.json")) == "legacy", "config not migrated");
                Assert(File.ReadAllText(Path.Combine(current, "preferences.json")) == "new prefs", "newer preferences overwritten");
                Assert(File.Exists(Path.Combine(current, "History", "one.keyweave")) && File.Exists(Path.Combine(current, ".migrated-from-function-row-remapper")), "folders/marker missing");
            });
            Test("Private-safe log excludes exception messages and rotates", delegate {
                string path = Path.Combine(scratch, "keyweave.log"), secret = "https://example.invalid/private-token";
                AppLog.Write(path, "Test component", new InvalidOperationException(secret)); string text = File.ReadAllText(path);
                Assert(text.Contains("Test component") && text.Contains("InvalidOperationException") && !text.Contains(secret), "private message leaked");
            });
            Test("Master network policy blocks HTTP before a request", delegate {
                NetworkPolicy.Enabled = false; bool blocked = false; try { SystemActions.HttpRequest(new Mapping { Kind = ActionKind.HttpRequest, Target = "http://127.0.0.1:1/private" }); } catch (InvalidOperationException ex) { blocked = ex.Message.Contains("turned off"); }
                Assert(blocked, "network action was not blocked");
            });
            Test("Import quarantine describes risky actions without executing", delegate {
                var c = new Configuration { CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+W", Action = new Mapping { Kind = ActionKind.HttpRequest, Target = "https://example.invalid/hook", Arguments = "{\"ok\":true}" } } } };
                c.Mappings[0] = new Mapping { Kind = ActionKind.Command, Target = @"C:\Tools\safe.cmd", Arguments = "--preview" };
                string review = ActionPrivacy.Review(c, @"C:\private\import.json");
                Assert(review.Contains("USES NETWORK") && review.Contains("OPENS OR RUNS LOCAL CONTENT") && review.Contains("example.invalid") && review.Contains("import.json") && !review.Contains(@"C:\private\import.json"), "risk review");
            });
            Test("Import quarantine exposes effects hidden behind conditions", delegate {
                var rule = new ConditionalRule { Application = "Discord.exe", WhenMatched = new Mapping { Kind = ActionKind.HttpRequest, Target = "https://example.invalid/conditional" }, Otherwise = new Mapping { Kind = ActionKind.Application, Target = @"C:\Tools\fallback.exe" } };
                var c = new Configuration(); c.Mappings[0] = new Mapping { Kind = ActionKind.Conditional, Target = ConditionalCodec.Serialize(rule) };
                string review = ActionPrivacy.Review(c, "condition.keyweave");
                Assert(ActionPrivacy.Risk(c.Mappings[0]) == "CONDITIONAL: USES NETWORK" && review.Contains("1 network") && review.Contains("1 local launch/open") && review.Contains("When foreground app") && review.Contains("Match:"), "nested risks hidden");
            });
            Test("System integrations and HTTP actions validate", delegate {
                ConfigStore.Validate(new Mapping { Kind = ActionKind.SystemAction, Target = "CenterWindow" }, false);
                ConfigStore.Validate(new Mapping { Kind = ActionKind.SystemAction, Target = SystemActions.ActivateProfilePrefix + "Gaming" }, false);
                ConfigStore.Validate(new Mapping { Kind = ActionKind.HttpRequest, Target = "http://127.0.0.1:9000/hook", Arguments = "{\"ok\":true}" }, false);
                Reject(() => ConfigStore.Validate(new Mapping { Kind = ActionKind.SystemAction, Target = "Unknown" }, false));
                Reject(() => ConfigStore.Validate(new Mapping { Kind = ActionKind.HttpRequest, Target = "file:///secret" }, false));
                Reject(() => ConfigStore.Validate(new Mapping { Kind = ActionKind.HttpRequest, Target = "https://user:pass@example.com/" }, false));
            });
            Test("Profile actions validate, summarize and route without touching disk", delegate {
                var action = new Mapping { Kind = ActionKind.SystemAction, Target = SystemActions.ActivateProfilePrefix + "Gaming" };
                var sink = new FakeSink(); new ActionDispatcher(sink, new FakeDdc()).Execute(action);
                Assert(action.Summary == "Activate profile: Gaming" && sink.Profiles.SequenceEqual(new[] { "Gaming" }), "profile action routing");
                Reject(() => ConfigStore.Validate(new Mapping { Kind = ActionKind.SystemAction, Target = SystemActions.ActivateProfilePrefix }, false));
                Reject(() => ConfigStore.Validate(new Mapping { Kind = ActionKind.SystemAction, Target = SystemActions.ActivateProfilePrefix + "Bad\nName" }, false));
            });
            Test("HTTP action reaches a local endpoint", delegate {
                NetworkPolicy.Enabled = true;
                string requestLine = null; Exception serverError = null;
                var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var thread = new Thread(new ThreadStart(delegate {
                    try {
                        using (var client = listener.AcceptTcpClient()) using (var stream = client.GetStream()) using (var reader = new StreamReader(stream)) {
                            requestLine = reader.ReadLine(); string line; while (!String.IsNullOrEmpty(line = reader.ReadLine())) { }
                            byte[] response = System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"); stream.Write(response, 0, response.Length);
                        }
                    } catch (Exception ex) { serverError = ex; }
                }));
                thread.IsBackground = true; thread.Start();
                try { SystemActions.HttpRequest(new Mapping { Kind = ActionKind.HttpRequest, Target = "http://127.0.0.1:" + port + "/keyweave" }); }
                finally { listener.Stop(); thread.Join(2000); }
                if (serverError != null) throw serverError;
                Assert(requestLine == "GET /keyweave HTTP/1.1", "unexpected request: " + requestLine);
                NetworkPolicy.Enabled = false;
            });
            Test("Media integrations route through the safe input sink", delegate {
                DiscordIntegration.Disconnect();
                var sink = new FakeSink(); var dispatcher = new ActionDispatcher(sink, new FakeDdc());
                dispatcher.Execute(new Mapping { Kind = ActionKind.SystemAction, Target = "SpotifyPlayPause" });
                dispatcher.Execute(new Mapping { Kind = ActionKind.SystemAction, Target = "DiscordMute" });
                Assert(sink.Keys.Count == 2 && sink.Keys[0].Last() == (int)Keys.MediaPlayPause && sink.Keys[1].Last() == (int)Keys.M, "integration shortcut routing");
            });
            Test("Configuration health scan is local and read-only", delegate {
                var clean = ConfigurationHealth.Scan(new Configuration(), new ProfileCollection(), new DdcMonitor[0]);
                Assert(!clean.HasWarnings, "safe defaults should be healthy");
                var broken = new Configuration(); broken.Mappings[0] = new Mapping { Kind = ActionKind.Application, Target = Path.Combine(Path.GetTempPath(), "KiWeave-health-missing-" + Guid.NewGuid().ToString("N") + ".exe") };
                var report = ConfigurationHealth.Scan(broken, new ProfileCollection(), new DdcMonitor[0]);
                Assert(report.HasWarnings && report.Findings.Any(f => f.Title == "Missing target"), "missing target was not reported");
            });
            Test("Release version metadata is 1.0.0 beta 3", delegate {
                Assert(UpdateChecker.CurrentVersion == "1.0.0-beta.3" && typeof(Program).Assembly.GetName().Version.ToString() == "1.0.0.0", "version mismatch");
            });
        }
        static Configuration MonitorConfig() { var c = new Configuration(); c.Mappings[0] = new Mapping { Kind = ActionKind.Monitor, MonitorId = new string('a',64), MonitorControl = "VolumeDown", MonitorStep = 5 }; return c; }
        sealed class FakeDdc : IDdcController { public int Calls; public bool Allowed; public Mapping Last; public void Apply(Mapping m, Func<bool> active) { Calls++; Last = m; Allowed = active(); } }
        static void NativeTests()
        {
            // A lower observation hook swallows the test F5 events so no foreground application sees them.
            using (var observer = new NativeObserver()) {
                Test("Production hook ignores injected F5", delegate { using (var engine = new KeyboardEngine(new WindowsActionSink(), false)) { engine.Apply(Active(ActionKind.Unbound, "")); observer.Clear(); SendTest(false); SendTest(true); Thread.Sleep(150); Assert(observer.F5 == 2, "production injection must pass"); } });
                Test("Native hook suppresses marked test F5 and emits one F6 pair", delegate { using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(Active(ActionKind.SendKey, "F6")); observer.Clear(); SendTest(false); Thread.Sleep(120); for (int i = 0; i < 8; i++) SendTest(false); SendTest(true); Thread.Sleep(150); Assert(observer.F5 == 0 && observer.F6Down == 1 && observer.F6Up == 1, "F5=" + observer.F5 + ", F6=" + observer.F6Down + "/" + observer.F6Up); } });
                Test("Native live tester receives one transient resolved event", delegate { using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) using (var seen = new AutoResetEvent(false)) { engine.Apply(Active(ActionKind.Unbound, "")); KeyDiagnostic latest = null; engine.KeyObserved += item => { latest = item; seen.Set(); }; observer.Clear(); SendTest(false); Assert(seen.WaitOne(1000), "no diagnostic event"); SendTest(true); Assert(latest != null && latest.KeyName == "F5" && latest.Suppressed && latest.ResolvedAction == "Unbound (do nothing)", "incorrect diagnostic"); } });
                Test("Native modifier layer suppresses its hold key and routes F5", delegate { var c = new Configuration { Enabled = true, Layers = new[] { new ModifierLayer { Name = "Test", ActivationKey = "CapsLock" } } }; c.Layers[0].Mappings[4] = new Mapping { Kind = ActionKind.SendKey, Target = "F6" }; using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(c); observer.Clear(); SendTestKey((int)Keys.CapsLock, false); SendTest(false); SendTest(true); SendTestKey((int)Keys.CapsLock, true); Thread.Sleep(150); Assert(observer.CapsLock == 0 && observer.F5 == 0 && observer.F6Down == 1 && observer.F6Up == 1, "Caps=" + observer.CapsLock + ", F5=" + observer.F5 + ", F6=" + observer.F6Down + "/" + observer.F6Up); } });
                Test("Native Unbound swallows both edges; disable restores pass-through", delegate { using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(Active(ActionKind.Unbound, "")); observer.Clear(); SendTest(false); SendTest(true); Thread.Sleep(100); Assert(observer.F5 == 0, "unbound"); engine.SetEnabled(false); SendTest(false); SendTest(true); Thread.Sleep(100); Assert(observer.F5 == 2, "disable"); } });
                Test("Native held key launches exactly one real process", delegate { string marker = Path.Combine(scratch, "launch-marker.txt"); var c = Active(ActionKind.Application, Process.GetCurrentProcess().MainModule.FileName); c.Mappings[4].Arguments = "--probe \"" + marker + "\""; using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(c); observer.Clear(); SendTest(false); for (int i = 0; i < 20; i++) SendTest(false); SendTest(true); WaitFor(() => File.Exists(marker)); Thread.Sleep(200); Assert(File.ReadAllLines(marker).Length == 1 && observer.F5 == 0, "launch count/suppression"); } });
                Test("Native pass-through and disposal restore both edges", delegate { using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(new Configuration { Enabled = true }); observer.Clear(); SendTest(false); SendTest(true); Thread.Sleep(100); Assert(observer.F5 == 2, "pass through"); engine.Apply(Active(ActionKind.Unbound, "")); } observer.Clear(); SendTest(false); SendTest(true); Thread.Sleep(100); Assert(observer.F5 == 2, "after disposal"); });
            }
        }
        static void WaitFor(Func<bool> condition) { var sw = Stopwatch.StartNew(); while (!condition() && sw.ElapsedMilliseconds < 4000) Thread.Sleep(25); Assert(condition(), "Timed out waiting for test action"); }
        static void SendTest(bool up) { SendTestKey(0x74, up); }
        static void SendTestKey(int key, bool up) { var a = new[] { Native.Key(key, up, Native.TestTag) }; Assert(Native.SendInput(1, a, Marshal.SizeOf(typeof(Native.Input))) == 1, "test SendInput failed"); }
        sealed class NativeObserver : IDisposable
        {
            readonly Thread thread; readonly ManualResetEvent ready = new ManualResetEvent(false); readonly Native.HookProc callback; Control control; IntPtr hook;
            public int CapsLock, F5, F6Down, F6Up;
            public void Clear() { CapsLock = F5 = F6Down = F6Up = 0; }
            public NativeObserver()
            {
                callback = Observe; thread = new Thread(delegate() { control = new Control(); var h = control.Handle; hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0); ready.Set(); Application.Run(); Native.UnhookWindowsHookEx(hook); control.Dispose(); }) { IsBackground = true };
                thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert(ready.WaitOne(3000) && hook != IntPtr.Zero, "observer installation");
            }
            IntPtr Observe(int code, IntPtr w, IntPtr l)
            {
                if (code >= 0) { var k = (Native.KeyboardData)Marshal.PtrToStructure(l, typeof(Native.KeyboardData)); if (k.ExtraInfo == Native.TestTag && k.Vk == (int)Keys.CapsLock) { Interlocked.Increment(ref CapsLock); return new IntPtr(1); } if (k.ExtraInfo == Native.TestTag && k.Vk == 0x74) { Interlocked.Increment(ref F5); return new IntPtr(1); } if (k.ExtraInfo == Native.Tag && k.Vk == 0x75) { if ((k.Flags & 0x80) == 0) Interlocked.Increment(ref F6Down); else Interlocked.Increment(ref F6Up); return new IntPtr(1); } }
                return Native.CallNextHookEx(hook, code, w, l);
            }
            public void Dispose() { control.BeginInvoke((Action)delegate { Application.ExitThread(); }); thread.Join(2000); ready.Dispose(); }
        }
    }
}
