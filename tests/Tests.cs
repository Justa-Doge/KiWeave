using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class StalledReadStream : Stream
    {
        readonly TaskCompletionSource<int> pending = new TaskCompletionSource<int>();
        public bool Disposed { get; private set; }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) { return 0; }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { return pending.Task; }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    internal sealed class FakeSink : IActionSink, IProfileActionSink
    {
        public readonly List<int[]> Keys = new List<int[]>();
        public readonly List<ProcessStartInfo> Launches = new List<ProcessStartInfo>();
        public readonly List<string> Profiles = new List<string>();
        public void Send(int[] keys) { Keys.Add(keys); }
        public void Launch(ProcessStartInfo p) { Launches.Add(p); }
        void IProfileActionSink.ActivateProfile(string name) { Profiles.Add(name); }
    }
    internal sealed class BlockingSink : IActionSink, IDisposable
    {
        public readonly ManualResetEvent Started = new ManualResetEvent(false), Release = new ManualResetEvent(false);
        int calls;
        public void Send(int[] keys) { if (Interlocked.Increment(ref calls) == 1) { Started.Set(); Release.WaitOne(3000); } }
        public void Launch(ProcessStartInfo info) { }
        public void Dispose() { Started.Dispose(); Release.Dispose(); }
    }
    internal sealed class SignalingSink : IActionSink, IProfileActionSink, IDisposable
    {
        readonly object gate = new object(); readonly List<int[]> keys = new List<int[]>(); readonly List<ProcessStartInfo> launches = new List<ProcessStartInfo>(); readonly List<string> profiles = new List<string>();
        public readonly AutoResetEvent Changed = new AutoResetEvent(false);
        public void Send(int[] value) { lock (gate) keys.Add(value); Changed.Set(); }
        public void Launch(ProcessStartInfo value) { lock (gate) launches.Add(value); Changed.Set(); }
        public void ActivateProfile(string value) { lock (gate) profiles.Add(value); Changed.Set(); }
        public int[][] Snapshot() { lock (gate) return keys.ToArray(); }
        public ProcessStartInfo[] LaunchSnapshot() { lock (gate) return launches.ToArray(); }
        public string[] ProfileSnapshot() { lock (gate) return profiles.ToArray(); }
        public void Dispose() { Changed.Dispose(); }
    }
    internal static class Tests
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
        static int passed, failed; static string scratch;
        static void Assert(bool condition, string detail) { if (!condition) throw new Exception(detail); }
        static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } if (!rejected) throw new Exception("Expected rejection"); }
        static void RejectOversizedRead(Action action) { bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; } if (!rejected) throw new Exception("Expected bounded reader to reject oversized file"); }
        static void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); } }
        static IEnumerable<Control> Descendants(Control parent) { foreach (Control child in parent.Controls) { yield return child; foreach (Control nested in Descendants(child)) yield return nested; } }
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
            AppStorage.SetTestDataFolder(Path.Combine(scratch, "KiWeaveData"));
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
                        typeof(MainForm).GetMethod("AddCustomHotkey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(form, null);
                        var customMonitor = (ComboBox)typeof(MainForm).GetField("customMonitor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
                        var fakeMonitor = new DdcMonitor { Id = new string('a', 64), Name = "Display 1", Status = "DDC/CI connected", Codes = new byte[] { 0x6B, 0xD6 } };
                        typeof(MainForm).GetField("detected", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(form, new[] { fakeMonitor });
                        customGroup.SelectedIndex = 4;
                        customChoice.SelectedItem = customChoice.Items.Cast<object>().First(x => ((ActionPickerChoice)x).Mapping.MonitorControl == "BacklightOff"); Application.DoEvents();
                        using (var image = new System.Drawing.Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, form.Size)); string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom-monitor-preview.png"); image.Save(path); Console.WriteLine(path); }
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
                if (args.Contains("--toast-preview")) {
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    using (var muted = new StatusOverlayForm("Discord", "Muted", "mute", true, true)) CapturePreview(muted, "windows11-muted-toast-preview");
                    using (var live = new StatusOverlayForm("Discord", "Undeafened", "deaf", false, true)) CapturePreview(live, "windows11-undeafened-toast-preview");
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
            Test("Regression storage override is temp-only", delegate {
                string scratchRoot = Path.GetFullPath(scratch).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Assert(AppStorage.DataFolder.StartsWith(scratchRoot, StringComparison.OrdinalIgnoreCase), "data folder escaped the test scratch root");
                string outsideTemp = Directory.GetParent(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).FullName;
                Reject(() => AppStorage.SetTestDataFolder(outsideTemp));
                Assert(AppStorage.DataFolder.StartsWith(scratchRoot, StringComparison.OrdinalIgnoreCase), "rejected override changed the active test root");
            });
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
            Test("Elevated network helper wait is bounded and terminates only on timeout", delegate {
                int waitBudget = 0, cleanupBudget = 0; bool terminated = false;
                Assert(!Program.WaitForElevatedHelper(milliseconds => { waitBudget = milliseconds; return false; }, () => terminated = true, milliseconds => { cleanupBudget = milliseconds; return true; }), "timed-out helper reported success");
                Assert(waitBudget == Program.ElevatedNetworkWaitTimeoutMilliseconds && cleanupBudget == 1000 && terminated, "timeout cleanup policy");
                Assert(Program.WaitForElevatedHelper(milliseconds => milliseconds == Program.ElevatedNetworkWaitTimeoutMilliseconds, () => { throw new Exception("completed process was terminated"); }, milliseconds => true), "successful helper did not finish");
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
                var customMonitorChoices = MainForm.ChoicesFor(4, true);
                Assert(customMonitorChoices.Any(x => x.Mapping.Kind == ActionKind.Monitor && x.Mapping.MonitorControl == "BacklightOff"), "custom backlight-off picker choice missing");
                Assert(customMonitorChoices.Any(x => x.Mapping.Kind == ActionKind.Monitor && x.Mapping.MonitorControl == "BacklightOn"), "custom backlight-on picker choice missing");
                Assert(customMonitorChoices.Any(x => x.Mapping.MonitorControl == "MonitorStandby") && customMonitorChoices.Any(x => x.Mapping.MonitorControl == "MonitorWake"), "explicit monitor power-mode choices missing");
                Assert(ActionPickerForm.Catalog(true).Any(x => x.Mapping.Kind == ActionKind.Monitor && x.Mapping.MonitorControl == "BacklightOff"), "custom action library omitted backlight-off");
                using (var form = new MainForm(false, true)) {
                    var add = typeof(MainForm).GetMethod("AddCustomHotkey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic); add.Invoke(form, null);
                    var group = (ComboBox)typeof(MainForm).GetField("customSimpleKind", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
                    var action = (ComboBox)typeof(MainForm).GetField("customSpecificKind", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
                    var fakeMonitor = new DdcMonitor { Id = new string('a', 64), Name = "DDC test monitor", Codes = new byte[] { 0x6B, 0xD6 } };
                    typeof(MainForm).GetField("detected", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(form, new[] { fakeMonitor });
                    group.SelectedIndex = 4;
                    action.SelectedItem = action.Items.Cast<object>().First(x => ((ActionPickerChoice)x).Mapping.MonitorControl == "BacklightOff");
                    var draft = (Configuration)typeof(MainForm).GetField("draft", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
                    var configured = draft.CustomHotkeys.Last().Action;
                    Assert(configured.Kind == ActionKind.Monitor && configured.MonitorId == fakeMonitor.Id && configured.MonitorControl == "BacklightOff", "custom backlight choice did not configure the selected monitor: " + configured.Kind + ", " + configured.MonitorId + ", " + configured.MonitorControl);
                }
            });
            Test("DDC action picker restores the selected monitor control", delegate {
                using (var form = new MainForm(false, true)) {
                    var populate = typeof(MainForm).GetMethod("PopulateChoices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    foreach (bool custom in new[] { false, true }) {
                        int group = custom ? 4 : 5;
                        foreach (string control in new[] { "BacklightOff", "BacklightOn", "BacklightLevelOff", "BacklightLevelOn" }) {
                            var box = new ComboBox();
                            var mapping = new Mapping { Kind = ActionKind.Monitor, MonitorControl = control, MonitorId = new string('a', 64), MonitorStep = 5 };
                            populate.Invoke(form, new object[] { box, group, custom, mapping });
                            var selected = box.SelectedItem as ActionPickerChoice;
                            Assert(selected != null && DdcOperation.SameControl(selected.Mapping.MonitorControl, control),
                                (custom ? "custom" : "function-key") + " picker selected " + (selected == null ? "nothing" : selected.Mapping.MonitorControl) + " for " + control);
                            box.Dispose();
                        }
                    }
                }
            });
            Test("Action-choice matching treats Windows paths case-insensitively", delegate {
                var choice = new ActionPickerChoice("Notepad", new Mapping { Kind = ActionKind.Application, Target = @"C:\Windows\System32\notepad.exe", Arguments = "--safe", WorkingDirectory = @"C:\Windows\System32" });
                Assert(choice.Matches(new Mapping { Kind = ActionKind.Application, Target = @"c:\windows\system32\NOTEPAD.EXE", Arguments = "--safe", WorkingDirectory = @"c:\WINDOWS\system32" }), "case-only path differences should match the same Windows action");
                Assert(!choice.Matches(new Mapping { Kind = ActionKind.Application, Target = @"C:\Windows\System32\notepad.exe", Arguments = "--other", WorkingDirectory = @"C:\Windows\System32" }), "arguments must remain case-sensitive and exact");
            });
            Test("Every executable action kind is selectable for custom hotkeys", delegate {
                var expected = Enum.GetValues(typeof(ActionKind)).Cast<ActionKind>().Where(kind => kind != ActionKind.PassThrough && kind != ActionKind.Unbound).ToArray();
                var choices = Enumerable.Range(0, 5).SelectMany(group => MainForm.ChoicesFor(group, true)).Where(choice => choice.Label != "Choose an action").ToArray();
                var missingFromEditor = expected.Except(choices.Select(choice => choice.Mapping.Kind)).ToArray();
                Assert(missingFromEditor.Length == 0, "custom editor missing: " + String.Join(", ", missingFromEditor));
                var library = ActionPickerForm.Catalog(true);
                var missingSystemActions = SystemActions.Names.Except(choices.Where(choice => choice.Mapping.Kind == ActionKind.SystemAction).Select(choice => choice.Mapping.Target), StringComparer.Ordinal).ToArray();
                Assert(missingSystemActions.Length == 0, "custom editor missing integration actions: " + String.Join(", ", missingSystemActions));
                missingSystemActions = SystemActions.Names.Except(library.Where(choice => choice.Mapping.Kind == ActionKind.SystemAction).Select(choice => choice.Mapping.Target), StringComparer.Ordinal).ToArray();
                Assert(missingSystemActions.Length == 0, "custom action library missing integration actions: " + String.Join(", ", missingSystemActions));
                var unknownSystemActions = library.Where(choice => choice.Mapping.Kind == ActionKind.SystemAction && !SystemActions.IsKnown(choice.Mapping.Target)).Select(choice => choice.Label + " -> " + choice.Mapping.Target).ToArray();
                Assert(unknownSystemActions.Length == 0, "custom action library exposes actions the runtime does not recognize: " + String.Join(", ", unknownSystemActions));
                var libraryKinds = library.Select(choice => choice.Mapping.Kind).Distinct().ToArray();
                var missingFromLibrary = expected.Except(libraryKinds).ToArray();
                Assert(missingFromLibrary.Length == 0, "custom action library missing: " + String.Join(", ", missingFromLibrary));
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
                Assert(UpdateChecker.NewestUpdate("release refs/tags/v1.1.0-beta.2\nrelease refs/tags/v1.0.0\n", "1.0.0-beta.1", "Beta") == "v1.1.0-beta.2", "beta channel");
                Assert(UpdateChecker.NewestUpdate("release refs/tags/v1.1.0-alpha.2\nrelease refs/tags/v1.0.0\n", "1.0.0", "Stable") == null, "stable excludes prerelease");
            });
            Test("PowerToys shortcut scan includes live fields and skips defaults", delegate {
                string root = Path.Combine(scratch, "PowerToysFixture"), module = Path.Combine(root, "ColorPicker"); Directory.CreateDirectory(module);
                File.WriteAllText(Path.Combine(root, "settings.json"), "{\"enabled\":{\"ColorPicker\":true}}");
                File.WriteAllText(Path.Combine(module, "settings.json"), "{\"properties\":{\"DefaultActivationShortcut\":{\"win\":true,\"ctrl\":false,\"alt\":false,\"shift\":true,\"code\":67},\"ActivationShortcut\":{\"win\":true,\"ctrl\":false,\"alt\":false,\"shift\":true,\"code\":67},\"unused_hotkey\":{\"win\":false,\"ctrl\":true,\"alt\":false,\"shift\":false,\"code\":0}}}");
                string external = Path.Combine(scratch, "PowerToysExternalModule"); Directory.CreateDirectory(external);
                File.WriteAllText(Path.Combine(external, "settings.json"), "{\"properties\":{\"ExternalShortcut\":{\"win\":false,\"ctrl\":true,\"alt\":true,\"shift\":false,\"code\":69}}}");
                string junction = Path.Combine(root, "ExternalModule");
                using (var createLink = Process.Start(new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + junction + "\" \"" + external + "\"") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
                    Assert(createLink != null && createLink.WaitForExit(10000) && createLink.ExitCode == 0, "PowerToys fixture junction could not be created");
                try
                {
                var found = PowerToysIntegration.Load(root);
                Assert(found.Count == 2, "shortcut count, including junction target if followed: " + found.Count);
                Assert(found.Any(x => x.Chord == "Shift+Win+C" && x.ModuleEnabled && x.Action == "Activation"), "active shortcut");
                Assert(found.Any(x => x.Chord == "Unassigned"), "unassigned shortcut");
                string expectedHash; using (var sha = SHA256.Create()) using (var stream = File.OpenRead(Path.Combine(module, "settings.json"))) expectedHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                Assert(found.All(x => x.FileHash == expectedHash), "bounded PowerToys hash changed the standard SHA-256 digest");
                string oversizedSettings = Path.Combine(scratch, "PowerToys-oversized-settings.json"); File.WriteAllBytes(oversizedSettings, new byte[1024 * 1024 + 1]);
                bool oversizedHashRejected = false;
                try { PowerToysIntegration.Hash(oversizedSettings); }
                catch (InvalidDataException) { oversizedHashRejected = true; }
                Assert(oversizedHashRejected, "PowerToys change-detection hash accepted an oversized settings file");
                bool linkedBackupRejected = false;
                try { PowerToysIntegration.Backup(Path.Combine(junction, "settings.json")); }
                catch (IOException) { linkedBackupRejected = true; }
                Assert(linkedBackupRejected && File.Exists(Path.Combine(external, "settings.json")), "PowerToys rollback backup followed a linked settings directory");
                string backupFolder = Path.Combine(AppStorage.DataFolder, "PowerToysBackups");
                Directory.CreateDirectory(Path.GetDirectoryName(backupFolder));
                using (var createBackupLink = Process.Start(new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + backupFolder + "\" \"" + external + "\"") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
                    Assert(createBackupLink != null && createBackupLink.WaitForExit(10000) && createBackupLink.ExitCode == 0, "PowerToys backup-folder junction could not be created");
                try
                {
                    bool linkedBackupFolderRejected = false;
                    try { PowerToysIntegration.Backup(Path.Combine(module, "settings.json")); }
                    catch (IOException) { linkedBackupFolderRejected = true; }
                    Assert(linkedBackupFolderRejected && !Directory.GetFiles(external, "ColorPicker-settings.json-*.bak").Any(), "PowerToys rollback backup wrote through a linked backup folder");
                }
                finally { try { Directory.Delete(backupFolder, false); } catch { } }
                }
                finally { try { Directory.Delete(junction, false); } catch { } }
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
            Test("Every legacy library action survives custom-hotkey validation and round-trip", delegate {
                var customChoices = ActionPickerForm.Catalog(true);
                foreach (var item in ExpandedActions.All) {
                    var choice = customChoices.FirstOrDefault(candidate => String.Equals(candidate.Label, item.Label, StringComparison.OrdinalIgnoreCase));
                    Assert(choice != null, "custom-hotkey library is missing " + item.Label);
                    Assert(choice.Mapping.Kind == item.Mapping.Kind && choice.Mapping.Target == item.Mapping.Target && choice.Mapping.Arguments == item.Mapping.Arguments && choice.Mapping.WorkingDirectory == item.Mapping.WorkingDirectory && choice.Mapping.MonitorId == item.Mapping.MonitorId && choice.Mapping.MonitorControl == item.Mapping.MonitorControl && choice.Mapping.MonitorStep == item.Mapping.MonitorStep, "custom-hotkey library changed " + item.Label);
                    var config = new Configuration { CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+F24", Action = choice.Mapping.Copy() } } };
                    try {
                        ConfigStore.Validate(config, false);
                        var loaded = ConfigStore.Parse(ConfigStore.Serialize(config));
                        var actual = loaded.CustomHotkeys[0].Action;
                        Assert(actual.Kind == item.Mapping.Kind && actual.Target == item.Mapping.Target && actual.Arguments == item.Mapping.Arguments && actual.WorkingDirectory == item.Mapping.WorkingDirectory && actual.MonitorId == item.Mapping.MonitorId && actual.MonitorControl == item.Mapping.MonitorControl && actual.MonitorStep == item.Mapping.MonitorStep, "custom-hotkey round-trip changed " + item.Label);
                    } catch (Exception error) { throw new Exception(item.Label + ": " + error.Message); }
                }
            });
            Test("Every concrete action-library preset survives custom-hotkey round-trip", delegate {
                var choices = ActionPickerForm.Catalog(true);
                int verified = 0, editorTemplates = 0;
                foreach (var choice in choices) {
                    Mapping action = choice.Mapping;
                    bool needsTarget = action.Kind == ActionKind.SendKey || action.Kind == ActionKind.SendShortcut ||
                        action.Kind == ActionKind.Application || action.Kind == ActionKind.FileOrFolder ||
                        action.Kind == ActionKind.WindowsShortcut || action.Kind == ActionKind.Command ||
                        action.Kind == ActionKind.Python || action.Kind == ActionKind.HttpRequest ||
                        action.Kind == ActionKind.Sequence || action.Kind == ActionKind.Conditional;
                    bool needsMonitor = action.Kind == ActionKind.Monitor &&
                        (String.IsNullOrWhiteSpace(action.MonitorId) || String.IsNullOrWhiteSpace(action.MonitorControl));
                    if ((needsTarget && String.IsNullOrWhiteSpace(action.Target)) || needsMonitor) {
                        editorTemplates++;
                        continue;
                    }

                    var source = new Configuration { CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+F24", Action = action.Copy() } } };
                    try {
                        ConfigStore.Validate(source, false);
                        string json = ConfigStore.Serialize(source);
                        Configuration loaded = ConfigStore.Parse(json);
                        Mapping actual = loaded.CustomHotkeys.Single().Action;
                        Assert(actual.Kind == action.Kind && actual.Target == action.Target && actual.Arguments == action.Arguments &&
                            actual.WorkingDirectory == action.WorkingDirectory && actual.MonitorId == action.MonitorId &&
                            actual.MonitorControl == action.MonitorControl && actual.MonitorStep == action.MonitorStep,
                            "custom-hotkey catalog round-trip changed " + choice.Label);
                        Assert(ConfigStore.Serialize(loaded) == json, "custom-hotkey catalog JSON was not stable for " + choice.Label);
                        verified++;
                    } catch (Exception error) { throw new Exception(choice.Label + ": " + error.Message); }
                }
                Assert(verified > 300, "too few concrete custom-hotkey library presets were verified: " + verified);
                Assert(verified + editorTemplates == choices.Length, "custom-hotkey library contains an unclassified preset");
                Console.WriteLine("Custom-hotkey library round-trip: " + verified + " concrete actions; " + editorTemplates + " editor templates");
            });
            Test("Settings presets open pages through explorer without executing commands", delegate {
                foreach (var item in ExpandedActions.All.Where(c => c.Category == "Windows settings")) {
                    var p = ActionDispatcher.BuildLaunch(item.Mapping);
                    Assert(Path.GetFileName(p.FileName) == "explorer.exe" && p.Arguments.StartsWith("ms-settings:") && p.UseShellExecute, item.Label);
                }
            });
            Test("Safe defaults: twelve pass-through keys, off", delegate { var c = new Configuration(); Assert(!c.Enabled && c.Mappings.Length == 12 && c.Mappings.All(m => m.Kind == ActionKind.PassThrough), "defaults"); });
            Test("Core launch and input mappings round-trip in readable JSON", delegate {
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
            Test("Every current action kind round-trips through custom-hotkey configuration", delegate {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                var sequence = SequenceCodec.Serialize(new[] { new SequenceStep { Action = new Mapping { Kind = ActionKind.SendKey, Target = "F3" } } });
                var conditional = ConditionalCodec.Serialize(new ConditionalRule {
                    Condition = ConditionKind.ForegroundApplication,
                    Application = "notepad.exe",
                    WhenMatched = new Mapping { Kind = ActionKind.SendKey, Target = "F4" },
                    Otherwise = new Mapping { Kind = ActionKind.Unbound }
                });
                var mappings = new Dictionary<ActionKind, Mapping> {
                    { ActionKind.PassThrough, new Mapping { Kind = ActionKind.PassThrough } },
                    { ActionKind.Unbound, new Mapping { Kind = ActionKind.Unbound } },
                    { ActionKind.SendKey, new Mapping { Kind = ActionKind.SendKey, Target = "F2" } },
                    { ActionKind.SendShortcut, new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+Shift+S" } },
                    { ActionKind.Media, new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } },
                    { ActionKind.Application, new Mapping { Kind = ActionKind.Application, Target = exe, Arguments = "--sample", WorkingDirectory = scratch } },
                    { ActionKind.FileOrFolder, new Mapping { Kind = ActionKind.FileOrFolder, Target = scratch } },
                    { ActionKind.WindowsShortcut, new Mapping { Kind = ActionKind.WindowsShortcut, Target = Path.Combine(scratch, "sample.lnk") } },
                    { ActionKind.Command, new Mapping { Kind = ActionKind.Command, Target = exe, Arguments = "--command-sample", WorkingDirectory = scratch } },
                    { ActionKind.Monitor, new Mapping { Kind = ActionKind.Monitor, MonitorId = new string('a', 64), MonitorControl = "BrightnessUp", MonitorStep = 5 } },
                    { ActionKind.Python, new Mapping { Kind = ActionKind.Python, Target = Path.Combine(scratch, "sample.py"), Arguments = "--fast", WorkingDirectory = scratch } },
                    { ActionKind.LockThenSleep, new Mapping { Kind = ActionKind.LockThenSleep } },
                    { ActionKind.Sequence, new Mapping { Kind = ActionKind.Sequence, Target = sequence } },
                    { ActionKind.SystemAction, new Mapping { Kind = ActionKind.SystemAction, Target = "CenterWindow" } },
                    { ActionKind.HttpRequest, new Mapping { Kind = ActionKind.HttpRequest, Target = "https://example.invalid/action" } },
                    { ActionKind.Conditional, new Mapping { Kind = ActionKind.Conditional, Target = conditional } }
                };
                ActionKind[] kinds = Enum.GetValues(typeof(ActionKind)).Cast<ActionKind>().ToArray();
                Assert(mappings.Count == kinds.Length && kinds.All(mappings.ContainsKey), "round-trip fixture does not cover every declared action kind");
                var configuration = new Configuration {
                    CustomHotkeys = kinds.Select((kind, index) => new CustomHotkey {
                        Shortcut = "Ctrl+Alt+Shift+" + (char)('A' + index),
                        Action = mappings[kind].Copy()
                    }).ToArray()
                };
                string json = ConfigStore.Serialize(configuration);
                Configuration loaded = ConfigStore.Parse(json);
                Assert(loaded.CustomHotkeys.Length == kinds.Length, "custom action count changed during serialization");
                for (int index = 0; index < kinds.Length; index++) {
                    Mapping expected = mappings[kinds[index]], actual = loaded.CustomHotkeys[index].Action;
                    Assert(loaded.CustomHotkeys[index].Shortcut == configuration.CustomHotkeys[index].Shortcut &&
                        actual.Kind == expected.Kind && actual.Target == expected.Target && actual.Arguments == expected.Arguments &&
                        actual.WorkingDirectory == expected.WorkingDirectory && actual.MonitorId == expected.MonitorId &&
                        actual.MonitorControl == expected.MonitorControl && actual.MonitorStep == expected.MonitorStep,
                        "custom action changed during round-trip: " + kinds[index]);
                }
                Assert(ConfigStore.Serialize(loaded) == json, "custom action JSON was not stable after a second round-trip");
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
            Test("Shared shortcut explanations preserve WinForms/WPF safety context", delegate {
                var http = new Mapping { Kind = ActionKind.HttpRequest, Target = "https://example.invalid" };
                string explanation = MainForm.ExplainShortcut("F5", http, "Base layer");
                Assert(explanation == ShortcutExplanation.Build("F5", http, "Base layer") && explanation.Contains("Explain F5") && explanation.Contains("Base layer") && explanation.Contains("Maturity: Experimental") && explanation.Contains("Dependencies:") && explanation.Contains("Permission: USES NETWORK") && explanation.Contains("Network policy:"), "HTTP shortcut explanation parity");
                string encoded = SequenceCodec.Serialize(new[] { new SequenceStep { Action = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+S" } } });
                string sequence = ShortcutExplanation.Build("Ctrl+Alt+K", new Mapping { Kind = ActionKind.Sequence, Target = encoded }, "Custom hotkey");
                Assert(sequence.Contains("Custom hotkey") && sequence.Contains("Safety: 1 / 20 steps") && sequence.Contains("0 hardware"), "sequence shortcut explanation");
            });
            Test("Declarative action packs round-trip and reject scripts everywhere", delegate {
                var pack = new ActionPack { Id = "demo.pack", Name = "Demo pack", Publisher = "KiWeave", Description = "Safe demo", Configuration = new Configuration(), Profiles = new ProfileCollection() };
                string json = ActionPackStore.Serialize(pack); Assert(ActionPackStore.Parse(json).Name == "Demo pack", "pack round trip");
                Reject(() => ActionPackStore.Parse(json.Replace("\"version\":1", "\"version\":1,\"version\":1")));
                var unsafePack = new ActionPack { Id = "unsafe.pack", Name = "Unsafe", Configuration = new Configuration() };
                unsafePack.Configuration.Mappings[0] = new Mapping { Kind = ActionKind.Command, Target = @"C:\\cmd.exe" }; Reject(() => ActionPackStore.Serialize(unsafePack));
                var scriptedProfileConfig = new Configuration { CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+K", Action = new Mapping { Kind = ActionKind.Python, Target = @"C:\\script.py" } } } };
                var scriptedProfiles = new ProfileCollection { Profiles = new[] { new KeyWeaveProfile { Name = "Script profile", Configuration = scriptedProfileConfig } } };
                ProfileStore.Validate(scriptedProfiles, false);
                Reject(() => ActionPackStore.Serialize(new ActionPack { Id = "profile-script.pack", Name = "Profile script", Configuration = new Configuration(), Profiles = scriptedProfiles }));
            });
            Test("Action-pack saves atomically replace existing packs", delegate {
                string path = Path.Combine(scratch, "atomic-action-pack.kiweave-pack");
                var first = new ActionPack { Id = "atomic.pack", Name = "First", Configuration = new Configuration(), Profiles = new ProfileCollection() };
                var second = new ActionPack { Id = "atomic.pack", Name = "Second", Configuration = new Configuration(), Profiles = new ProfileCollection() };
                ActionPackStore.Save(path, first); Assert(ActionPackStore.Load(path).Name == "First", "initial saved pack loads");
                ActionPackStore.Save(path, second); Assert(ActionPackStore.Load(path).Name == "Second", "replacement saved pack loads");
                Assert(Directory.GetFiles(scratch, ".atomic-action-pack.kiweave-pack.*.tmp").Length == 0, "atomic temp file cleaned");
            });
            Test("Malformed and reserved shortcuts rejected", delegate { foreach (string s in new[] { "", "Ctrl++A", "Ctrl+Ctrl+A", "Ctrl", "Ctrl+Alt+Delete", "Ctrl+Banana", "MouseButtons", "123", "Shift+Alt" }) Reject(() => Shortcuts.Parse(s, false)); Reject(() => Shortcuts.Parse("Ctrl+A", true)); });
            Test("Custom hotkeys normalize media and extended keyboard buttons", delegate { Assert(HotkeyChord.Normalize("shift + win + l") == "Shift+Win+L", "normalize"); Assert(HotkeyChord.Normalize("volumeup") == "VolumeUp" && HotkeyChord.IsHardwareMediaShortcut("MediaPlayPause"), "media input"); Assert(HotkeyChord.Normalize("F13") == "F13" && HotkeyChord.Normalize("Apps") == "Apps", "extended keyboard input"); Reject(() => HotkeyChord.Parse("K")); });
            Test("Consumer-control eject accepts only the Consumer-page Eject usage", delegate { Assert(ConsumerControlDecoder.NameFor(0x0C, 0xB8) == "MediaEject", "consumer eject"); Assert(ConsumerControlDecoder.NameFor(0x07, 0x49) == null && ConsumerControlDecoder.NameFor(0x07, 0x4A) == null, "Insert/Home must not be interpreted as eject"); Assert(ConsumerControlDecoder.NameFor(0x0C, 0xB7) == null, "unrelated consumer usage"); });
            Test("Raw consumer usage press state is independent per device and resets on removal", delegate {
                var state = new ConsumerUsageState(); IntPtr keyboard = new IntPtr(101), controller = new IntPtr(202); ushort eject = 0xB8;
                Assert(state.Update(keyboard, new[] { eject }).SequenceEqual(new[] { eject }), "first device press");
                Assert(state.Update(controller, new[] { eject }).SequenceEqual(new[] { eject }), "same usage from a second device was swallowed");
                Assert(state.Update(keyboard, new[] { eject }).Length == 0, "held report repeated the action");
                Assert(state.Update(keyboard, new ushort[0]).Length == 0, "release should not trigger");
                Assert(state.Update(keyboard, new[] { eject }).SequenceEqual(new[] { eject }), "press after release");
                state.ResetDevice(controller);
                Assert(state.Update(controller, new[] { eject }).SequenceEqual(new[] { eject }), "press after device removal/reset");
            });
            Test("Raw HID consumer reports validate bounds and HID status codes", delegate { Assert(RawConsumerInput.HasValidReportLayout(24 + 8 + 3 * 16, 24, 16, 3), "valid batched reports"); Assert(!RawConsumerInput.HasValidReportLayout(24 + 8 + 2 * 16, 24, 16, 3), "truncated report array"); Assert(!RawConsumerInput.HasValidReportLayout(24 + 8, 24, 0, 1), "zero-length report"); Assert(!RawConsumerInput.HasValidReportLayout(24 + 8 + 16, 24, 16, 257), "excessive report count"); Assert(!RawConsumerInput.HasValidReportLayout(1024 * 1024 + 1, 24, 16, 1), "oversized raw input"); Assert(!RawConsumerInput.HasValidReportLayout(16, 24, 1, 1), "header underflow"); Assert(RawConsumerInput.IsSuccessfulUsageQuery(0x00110000) && !RawConsumerInput.IsSuccessfulUsageQuery(0), "HID status success constant"); });
            Test("Raw consumer input uses the required remove registration on disposal", delegate {
                RawConsumerInput.DeviceRegistration removal = RawConsumerInput.CreateRemovalRegistration();
                Assert(removal.Page == 0x0C && removal.Usage == 1, "removal targets the registered Consumer Control collection");
                Assert(removal.Flags == 0x00000001 && removal.Target == IntPtr.Zero, "RIDEV_REMOVE requires a null target");
            });
            Test("Shortcut capture normalizes reviewed keys without retaining input", delegate {
                Assert(ShortcutCapture.Normalize(true, true, false, false, (int)Keys.K, true) == "Ctrl+Alt+K", "custom chord");
                Assert(ShortcutCapture.Normalize(false, false, false, false, (int)Keys.F5, false) == "F5", "plain action key");
                Assert(ShortcutCapture.Normalize(false, false, true, true, (int)Keys.D1, true) == "Shift+Win+1", "digit chord");
                Assert(ShortcutCapture.Normalize(false, false, false, false, (int)Keys.MediaPlayPause, true) == "MediaPlayPause", "media button");
                Reject(() => ShortcutCapture.Normalize(false, false, false, false, (int)Keys.A, true));
                Reject(() => ShortcutCapture.Normalize(true, true, false, false, (int)Keys.Delete, true));
            });
            Test("Automatic update cadence is twelve hours", delegate { Assert(UpdateChecker.CheckIntervalMilliseconds == 43200000, "interval"); });
            Test("Custom hotkeys and Python scripts round-trip", delegate { var c = new Configuration { CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+P", Action = new Mapping { Kind = ActionKind.Python, Target = Path.Combine(scratch, "hello.py"), Arguments = "--fast", WorkingDirectory = scratch } }, new CustomHotkey { Shortcut = "Shift+Win+L", Action = new Mapping { Kind = ActionKind.LockThenSleep } } } }; string json = ConfigStore.Serialize(c); var loaded = ConfigStore.Parse(json); Assert(json.Contains("\"version\": 3") && loaded.CustomHotkeys[0].Action.Kind == ActionKind.Python && loaded.CustomHotkeys[1].Action.Kind == ActionKind.LockThenSleep, "v3"); });
            Test("Custom PassThrough and Unbound actions validate and persist", delegate {
                var configuration = new Configuration { Enabled = true, CustomHotkeys = new[] {
                    new CustomHotkey { Shortcut = "Ctrl+Alt+P", Action = new Mapping { Kind = ActionKind.PassThrough } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+U", Action = new Mapping { Kind = ActionKind.Unbound } },
                    new CustomHotkey { Shortcut = "F13", Action = new Mapping { Kind = ActionKind.Unbound } }
                } };
                string json = ConfigStore.Serialize(configuration); var loaded = ConfigStore.Parse(json);
                Assert(loaded.CustomHotkeys.Length == 3 && loaded.CustomHotkeys[0].Action.Kind == ActionKind.PassThrough && loaded.CustomHotkeys[1].Action.Kind == ActionKind.Unbound && loaded.CustomHotkeys[2].Action.Kind == ActionKind.Unbound, "no-op binding kinds changed in the config round-trip");
                loaded.CustomHotkeys[2].Action.Kind = ActionKind.PassThrough;
                var machine = new KeyStateMachine();
                Assert(!machine.Process((int)Keys.F13, true, false, loaded).Suppress && !machine.Process((int)Keys.F13, false, false, loaded).Suppress, "Pass Through standalone key did not retain native behavior");
                loaded.CustomHotkeys[2].Action.Kind = ActionKind.Unbound; machine = new KeyStateMachine();
                Assert(machine.Process((int)Keys.F13, true, false, loaded).Suppress && machine.Process((int)Keys.F13, false, false, loaded).Suppress, "Unbound standalone key did not consume both edges");
                var sink = new FakeSink();
                using (var engine = new KeyboardEngine(sink, true)) {
                    engine.Apply(loaded);
                    Assert(engine.TriggerCustomHotkey("Ctrl+Alt+U"), "Unbound custom chord was not accepted as a deliberate no-op");
                    Assert(!engine.TriggerCustomHotkey("Ctrl+Alt+P"), "Pass Through custom chord was incorrectly dispatched");
                    Thread.Sleep(50);
                    Assert(sink.Keys.Count == 0 && sink.Launches.Count == 0, "no-op custom binding unexpectedly executed an action");
                }
            });
            Test("Custom hotkey rebinding normalizes shortcuts and prevents collisions", delegate {
                var configuration = new Configuration { CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+A", Action = new Mapping { Kind = ActionKind.Unbound } }, new CustomHotkey { Shortcut = "Ctrl+Alt+B", Action = new Mapping { Kind = ActionKind.Unbound } } } };
                Assert(HotkeyChord.NormalizeCustomHotkeyReplacement(configuration, 0, "ctrl+alt+a") == "Ctrl+Alt+A", "an unchanged binding should be allowed and normalized");
                Assert(HotkeyChord.NormalizeCustomHotkeyReplacement(configuration, 0, "ctrl+alt+c") == "Ctrl+Alt+C", "a free replacement should be allowed and normalized");
                Reject(() => HotkeyChord.NormalizeCustomHotkeyReplacement(configuration, 0, "ctrl+alt+b"));
                Reject(() => HotkeyChord.NormalizeCustomHotkeyReplacement(configuration, 2, "ctrl+alt+a"));
                Reject(() => HotkeyChord.NormalizeCustomHotkeyReplacement(configuration, 3, "ctrl+alt+c"));
            });
            Test("Backlight off/on send VCP 0x6B and power requests send VCP 0xD6", delegate {
                var powerOff = DdcOperation.Find("MonitorStandby"); var powerOn = DdcOperation.Find("MonitorWake");
                var levelOff = DdcOperation.Find("BacklightOff"); var levelOn = DdcOperation.Find("BacklightOn");
                Assert(powerOff != null && powerOn != null && powerOff.Code == 0xD6 && powerOn.Code == 0xD6 && powerOff.FixedValue == 4 && powerOn.FixedValue == 1, "monitor power-mode requests must use VCP D6 values 4 and 1");
                Assert(powerOff.ValueFor(0) == 4 && powerOn.ValueFor(0) == 1, "monitor power request values");
                Assert(powerOff.Label.Contains("Monitor power") && powerOff.Label.Contains("standby") && powerOn.Label.Contains("Monitor power") && powerOn.Label.Contains("normal operation"), "power commands need descriptive names");
                Assert(levelOff != null && levelOn != null && levelOff.Code == 0xD6 && levelOn.Code == 0xD6 && levelOff.FixedValue == 5 && levelOn.FixedValue == 1, "Backlight off/on must directly write VCP 0xD6 values 5 and 1");
                Assert(levelOff.ValueFor(0) == 5 && levelOn.ValueFor(0) == 1, "backlight commands must use their fixed DDC/CI values");
                Assert(levelOff.Label.Contains("Backlight off") && levelOn.Label.Contains("Backlight on"), "backlight actions must be explicitly labeled");
                Assert(Object.ReferenceEquals(DdcOperation.Find("BacklightLevelOff"), levelOff) && Object.ReferenceEquals(DdcOperation.Find("BacklightLevelOn"), levelOn), "older backlight-level IDs must remain readable as aliases");
                var monitorChoices = MainForm.ChoicesFor(4, true);
                Assert(monitorChoices.All(choice => !choice.Label.StartsWith("Monitor: Monitor", StringComparison.Ordinal)) && monitorChoices.Any(choice => choice.Label == levelOff.Label && choice.Mapping.MonitorControl == "BacklightOff") && monitorChoices.Any(choice => choice.Label == levelOn.Label && choice.Mapping.MonitorControl == "BacklightOn") && monitorChoices.Any(choice => choice.Mapping.MonitorControl == "MonitorStandby") && monitorChoices.Any(choice => choice.Mapping.MonitorControl == "MonitorWake"), "picker must expose direct backlight and clearly separate monitor-power actions");
                string monitor = new string('a', 64); var c = new Configuration { CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+B", Action = new Mapping { Kind = ActionKind.Monitor, MonitorId = monitor, MonitorControl = "BacklightLevelOff", MonitorStep = 5 } } } };
                string json = ConfigStore.Serialize(c); var loaded = ConfigStore.Parse(json);
                Assert(json.Contains("\"action\":\"Monitor\"") && loaded.CustomHotkeys[0].Action.MonitorControl == "BacklightLevelOff" && loaded.CustomHotkeys[0].Action.MonitorId == monitor && Object.ReferenceEquals(DdcOperation.Find(loaded.CustomHotkeys[0].Action.MonitorControl), levelOff), "legacy custom backlight-level ID remains compatible");
                var mapping = new Mapping { Kind = ActionKind.Monitor, MonitorControl = "BacklightLevelOn" };
                Assert(!mapping.Repeats && mapping.Summary.Contains("Backlight on") && !mapping.Summary.Contains("(5%)"), "one-shot backlight mapping summary/repeat");
            });
            Test("DDC discovery treats power mode as a discrete VCP code", delegate {
                Assert(DdcService.IsSupportedProbe(0xD6, true, 1, 4, 0), "supported power-off mode was rejected because it has no continuous maximum");
                Assert(!DdcService.IsSupportedProbe(0xD6, true, 0, 4, 0), "momentary response was accepted as a persistent power setting");
                Assert(!DdcService.IsSupportedProbe(0xD6, false, 1, 1, 5), "unresponsive power-mode code was accepted");
                Assert(DdcService.IsSupportedProbe(0x10, true, 1, 50, 100), "continuous brightness range was rejected");
                Assert(!DdcService.IsSupportedProbe(0x10, true, 1, 50, 0), "continuous brightness without a valid range was accepted");
                Assert(DdcService.IsSupportedProbe(0x6B, true, 1, 50, 100), "continuous backlight level range was rejected");
                Assert(!DdcService.IsSupportedProbe(0x6B, true, 1, 50, 0), "backlight level without a valid range was accepted");
            });
            Test("Action sequences preserve order and waits", delegate { var steps = new[] { new SequenceStep { Action = new Mapping { Kind = ActionKind.SendShortcut, Target = "Win+E" } }, new SequenceStep { WaitMilliseconds = 750 }, new SequenceStep { Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" } } }; string text = SequenceCodec.Serialize(steps); var parsed = SequenceCodec.Parse(text); Assert(parsed.Count == 3 && parsed[1].WaitMilliseconds == 750 && parsed[2].Action.Target == "VolumeMute", "sequence"); });
            Test("Oversized action sequences are rejected before parsing or saving", delegate {
                Reject(() => SequenceCodec.Parse(new string('x', SequenceCodec.MaxEncodedLength + 1)));
                Reject(() => SequenceCodec.Serialize(new SequenceStep[] { null }));
                string url = "https://example.invalid/?q=" + new string('a', 1800);
                Reject(() => SequenceCodec.Serialize(new[] {
                    new SequenceStep { Action = new Mapping { Kind = ActionKind.HttpRequest, Target = url } },
                    new SequenceStep { Action = new Mapping { Kind = ActionKind.HttpRequest, Target = url } }
                }));
            });
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
            Test("Sequences reject DDC writes instead of bypassing the dedicated monitor queue", delegate {
                var monitor = new Mapping { Kind = ActionKind.Monitor, MonitorId = new string('a', 64), MonitorControl = "BacklightLevelOff", MonitorStep = 5 };
                Reject(() => SequenceCodec.Serialize(new[] { new SequenceStep { Action = monitor } }));
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
            Test("Persisted JSON readers enforce byte limits while reading", delegate {
                string path = Path.Combine(scratch, "oversized-read.json");
                File.WriteAllText(path, new string(' ', ConfigStore.MaxBytes + 1), new UTF8Encoding(false)); RejectOversizedRead(() => ConfigStore.Load(path)); RejectOversizedRead(() => ConfigStore.MigrationPreview(path));
                File.WriteAllText(path, new string(' ', 1025), new UTF8Encoding(false)); RejectOversizedRead(() => UserPreferences.Load(path));
                File.WriteAllText(path, new string(' ', ProfileStore.MaxBytes + 1), new UTF8Encoding(false)); RejectOversizedRead(() => ProfileStore.Load(path));
                File.WriteAllText(path, new string(' ', 65537), new UTF8Encoding(false)); RejectOversizedRead(() => ProfileScheduleStore.Load(path)); RejectOversizedRead(() => MidiTriggerStore.Load(path)); RejectOversizedRead(() => ControllerTriggerStore.Load(path));
                File.WriteAllText(path, new string(' ', MappingNoteStore.MaxBytes + 1), new UTF8Encoding(false)); RejectOversizedRead(() => MappingNoteStore.Load(path));
                File.WriteAllText(path, new string(' ', 256 * 1024 + 1), new UTF8Encoding(false)); RejectOversizedRead(() => ActionPackRepository.Load(path));
            });
            Test("Atomic backup replacement remains writable across repeated saves", delegate {
                string path = Path.Combine(scratch, "rotating-settings.json");
                AtomicFile.WriteAllText(path, "one", Encoding.ASCII, ".bak");
                AtomicFile.WriteAllText(path, "two", Encoding.ASCII, ".bak");
                Assert(File.ReadAllText(path) == "two" && File.ReadAllText(path + ".bak") == "one", "first backup rotation");
                AtomicFile.WriteAllText(path, "three", Encoding.ASCII, ".bak");
                Assert(File.ReadAllText(path) == "three" && File.ReadAllText(path + ".bak") == "two", "existing backup prevented replacement or wrong version retained");
            });
            Test("Rejected save does not replace valid configuration", delegate { string p = Path.Combine(scratch, "safe.json"); var c = new Configuration(); ConfigStore.Save(p, c); string before = File.ReadAllText(p); c.Mappings[0].Kind = ActionKind.Media; c.Mappings[0].Target = "bad"; Reject(() => ConfigStore.Save(p, c)); Assert(File.ReadAllText(p) == before, "unchanged"); });
            Test("F5 volume suppresses both edges and repeats", delegate { var m = new KeyStateMachine(); var c = Active(ActionKind.Media, "VolumeUp"); Assert(m.Process(0x74, true, false, c).Action != null, "down action"); var r = m.Process(0x74, true, false, c); Assert(r.Suppress && r.Action != null, "repeat"); r = m.Process(0x74, false, false, c); Assert(r.Suppress && r.Action == null, "up swallowed"); });
            Test("All twelve keys can be swallowed", delegate { var c = new Configuration { Enabled = true }; foreach (var a in c.Mappings) a.Kind = ActionKind.Unbound; var m = new KeyStateMachine(); for (int k = 0x70; k <= 0x7B; k++) { var d = m.Process(k, true, false, c); Assert(d.Suppress && d.Action == null, "unbound down"); Assert(m.Process(k, false, false, c).Suppress, "unbound up"); } });
            Test("Pass through and unrelated keys remain untouched", delegate { var c = new Configuration { Enabled = true }; var m = new KeyStateMachine(); for (int k = 0; k < 256; k++) { Assert(!m.Process(k, true, false, c).Suppress, "down"); Assert(!m.Process(k, false, false, c).Suppress, "up"); } });
            Test("Dedicated media buttons can trigger custom actions and suppress both edges", delegate { var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "MediaPlayPause", Action = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+Shift+S" } } } }; var m = new KeyStateMachine(); var down = m.Process((int)Keys.MediaPlayPause, true, false, c); Assert(down.Suppress && down.Action != null && down.Action.Target == "Ctrl+Shift+S", "mapped down"); Assert(m.Process((int)Keys.MediaPlayPause, true, false, c).Action == null, "repeat blocked"); Assert(m.Process((int)Keys.MediaPlayPause, false, false, c).Suppress, "mapped up"); Assert(!m.Process((int)Keys.VolumeUp, true, false, c).Suppress, "unmapped media passes"); });
            Test("Consumer Eject uses one input route when raw HID is active", delegate {
                var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "MediaEject", Action = new Mapping { Kind = ActionKind.SendKey, Target = "F6" } } } };
                var machine = new KeyStateMachine();
                KeyDecision down = machine.Process(0xB8, true, false, c, false, true, true);
                KeyDecision up = machine.Process(0xB8, false, false, c, false, true, true);
                Assert(down.Suppress && down.Action == null && up.Suppress, "keyboard hook failed to suppress the key or duplicated Eject handled by raw HID");
                down = machine.Process(0xB8, true, false, c);
                up = machine.Process(0xB8, false, false, c);
                Assert(down.Suppress && down.Action != null && down.Action.Target == "F6" && up.Suppress, "keyboard fallback stopped working without raw HID");
            });
            Test("Consumer Eject duplicate reports are coalesced but later presses remain valid", delegate {
                var gate = new EjectDispatchGate(); long frequency = 1000;
                Assert(gate.TryEnter(1000, frequency), "first source report");
                Assert(!gate.TryEnter(1050, frequency), "paired raw/VK/app-command report duplicated the action");
                Assert(!gate.TryEnter(950, frequency), "delayed source report bypassed coalescing");
                Assert(gate.TryEnter(1100, frequency), "later physical press was incorrectly suppressed");
                gate.Reset(1100);
                Assert(gate.TryEnter(1101, frequency), "failed queue could not release its dispatch claim");
            });
            Test("Custom PassThrough media and standalone keys retain native behavior", delegate {
                foreach (string shortcut in new[] { "MediaPlayPause", "F13" }) {
                    var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = shortcut, Action = new Mapping { Kind = ActionKind.PassThrough } } } };
                    int key = HotkeyChord.Parse(shortcut).Key; var machine = new KeyStateMachine();
                    Assert(!machine.Process(key, true, false, c).Suppress && !machine.Process(key, false, false, c).Suppress, shortcut + " pass-through was swallowed");
                    c.CustomHotkeys[0].Action.Kind = ActionKind.Unbound; machine = new KeyStateMachine();
                    KeyDecision unboundDown = machine.Process(key, true, false, c);
                    KeyDecision unboundRepeat = machine.Process(key, true, false, c);
                    KeyDecision unboundUp = machine.Process(key, false, false, c);
                    Assert(unboundDown.Suppress && unboundUp.Suppress, shortcut + " unbound action did not consume both edges");
                    Assert(unboundDown.Action == null && unboundRepeat.Action == null, shortcut + " unbound action was needlessly dispatched");
                }
            });
            Test("Dedicated media mapping ignores injected input and cancels held repeats", delegate { var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "VolumeUp", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } } } }; var m = new KeyStateMachine(); Assert(!m.Process((int)Keys.VolumeUp, true, true, c).Suppress, "injected pass"); Assert(m.Process((int)Keys.VolumeUp, true, false, c).Action != null, "physical down"); Assert(m.Process((int)Keys.VolumeUp, true, false, c).Action != null, "repeat"); c.Enabled = false; m.CancelHeldActions(); Assert(m.Process((int)Keys.VolumeUp, true, false, c).Action == null && m.Process((int)Keys.VolumeUp, false, false, c).Suppress, "cancelled pair"); });
            Test("Standalone extended keyboard hotkeys dispatch and pair releases", delegate { var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "F13", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } } } }; var m = new KeyStateMachine(); var down = m.Process((int)Keys.F13, true, false, c); Assert(down.Suppress && down.Action != null && down.Action.Target == "VolumeUp", "F13 down action"); var repeat = m.Process((int)Keys.F13, true, false, c); Assert(repeat.Suppress && repeat.Action != null, "F13 repeat"); Assert(m.Process((int)Keys.F13, false, false, c).Suppress, "F13 release paired"); Assert(!m.Process((int)Keys.F14, true, false, c).Suppress && !m.Process((int)Keys.F14, false, false, c).Suppress, "unmapped extended key passes"); });
            Test("Standalone extended hotkeys ignore injected keys and cancel repeats", delegate { var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "F13", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } } } }; var m = new KeyStateMachine(); Assert(!m.Process((int)Keys.F13, true, true, c).Suppress, "injected pass"); Assert(m.Process((int)Keys.F13, true, false, c).Action != null, "physical press"); c.Enabled = false; m.CancelHeldActions(); Assert(m.Process((int)Keys.F13, true, false, c).Suppress && m.Process((int)Keys.F13, true, false, c).Action == null, "cancelled repeat remains swallowed"); Assert(m.Process((int)Keys.F13, false, false, c).Suppress, "cancelled release paired"); });
            Test("Suspension blocks new/repeat actions while pairing already handled key releases", delegate {
                var c = new Configuration { Enabled = true, CustomHotkeys = new[] {
                    new CustomHotkey { Shortcut = "F13", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } },
                    new CustomHotkey { Shortcut = "VolumeUp", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } }
                } };
                c.Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" };
                var machine = new KeyStateMachine();
                foreach (int vk in new[] { (int)Keys.F1, (int)Keys.F13, (int)Keys.VolumeUp })
                {
                    Assert(machine.Process(vk, true, false, c, false, true).Suppress, "initial handled down " + vk);
                    var repeat = machine.Process(vk, true, false, c, false, false);
                    Assert(repeat.Suppress && repeat.Action == null, "suspended repeat remains paired but does not fire " + vk);
                    Assert(machine.Process(vk, false, false, c, false, false).Suppress, "suspended release remains swallowed " + vk);
                    Assert(machine.Process(vk, true, false, c, false, true).Action != null, "next press works after unsuspend " + vk);
                    Assert(machine.Process(vk, false, false, c).Suppress, "next release paired " + vk);
                }
                var layers = new Configuration { Enabled = true, Layers = new[] { new ModifierLayer { Name = "Media", ActivationKey = "CapsLock" } } };
                Assert(machine.Process((int)Keys.CapsLock, true, false, layers, false, true).Suppress, "layer starts while active");
                Assert(machine.Process((int)Keys.CapsLock, true, false, layers, false, false).Suppress, "held layer repeat stays paired while suspended");
                Assert(machine.Process((int)Keys.CapsLock, false, false, layers, false, false).Suppress, "layer release stays paired while suspended");
                Assert(!machine.Process((int)Keys.CapsLock, true, false, layers, false, false).Suppress && !machine.Process((int)Keys.CapsLock, false, false, layers, false, false).Suppress, "new layer press passes through while suspended");
            });
            Test("Custom hotkeys cannot collide with layer activation keys", delegate { var c = new Configuration { Layers = new[] { new ModifierLayer { Name = "Function", ActivationKey = "F13" } }, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "F13", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } } } }; Reject(() => ConfigStore.Validate(c, false)); c.CustomHotkeys[0].Shortcut = "Ctrl+F13"; Reject(() => ConfigStore.Validate(c, false)); });
            Test("Registered F-key custom chord takes precedence over the base F-key mapping", delegate {
                var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+F1", Action = new Mapping { Kind = ActionKind.SendKey, Target = "F7" } } } };
                c.Mappings[0] = new Mapping { Kind = ActionKind.SendKey, Target = "F6" };
                var registered = new Dictionary<int, CustomHotkey>();
                string[] failures = HotkeyRegistration.Apply(c, IntPtr.Zero, registered, true, false, (window, id, modifiers, key) => true, (window, id) => true, () => 0);
                int[] precedence = HotkeyRegistration.ActiveModifierChordKeys(c, registered); Func<int, bool> ctrlOnly = key => key == 0x11;
                Assert(failures.Length == 0 && HotkeyChord.HasMatchingModifierChord(precedence, 0x70, ctrlOnly), "successfully registered Ctrl+F1 chord was not detected");
                Assert(!HotkeyChord.HasMatchingModifierChord(precedence, 0x70, key => key == 0x11 || key == 0x10), "non-matching modifier set claimed F1");
                var machine = new KeyStateMachine(); KeyDecision down = machine.Process(0x70, true, false, c, true);
                Assert(!down.Suppress && down.Action == null && down.ResolvedAction == "Custom hotkey chord", "base F1 action competed with Ctrl+F1");
                Assert(!machine.Process(0x70, false, false, c, false).Suppress, "F1 release was not paired as pass-through");
                down = machine.Process(0x70, true, false, c, false); Assert(down.Suppress && down.Action != null && down.Action.Target == "F6", "ordinary F1 mapping stopped working");
            });
            Test("Failed F-key custom registration leaves the base mapping active while Pass Through still wins", delegate {
                var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+F1", Action = new Mapping { Kind = ActionKind.SendKey, Target = "F7" } } } };
                c.Mappings[0] = new Mapping { Kind = ActionKind.SendKey, Target = "F6" };
                var registered = new Dictionary<int, CustomHotkey>();
                string[] failures = HotkeyRegistration.Apply(c, IntPtr.Zero, registered, true, false, (window, id, modifiers, key) => false, (window, id) => true, () => 1409);
                int[] precedence = HotkeyRegistration.ActiveModifierChordKeys(c, registered);
                Assert(failures.Length == 1 && !HotkeyChord.HasMatchingModifierChord(precedence, 0x70, key => key == 0x11), "failed OS registration still claimed F1");
                KeyDecision down = new KeyStateMachine().Process(0x70, true, false, c, false);
                Assert(down.Suppress && down.Action != null && down.Action.Target == "F6", "base F1 mapping was lost with a failed custom registration");
                c.CustomHotkeys[0].Action.Kind = ActionKind.PassThrough; registered.Clear(); precedence = HotkeyRegistration.ActiveModifierChordKeys(c, registered);
                Assert(precedence.Length == 1 && HotkeyChord.HasMatchingModifierChord(precedence, 0x70, key => key == 0x11), "intentional Pass Through chord stopped taking precedence without OS registration");
            });
            Test("Global disable restores new presses", delegate { var c = Active(ActionKind.Unbound, ""); c.Enabled = false; Assert(!new KeyStateMachine().Process(0x74, true, false, c).Suppress, "disabled"); });
            Test("Injected keys cannot recurse or consume physical state", delegate { var c = Active(ActionKind.SendKey, "F5"); var m = new KeyStateMachine(); m.Process(0x74, true, false, c); var d = m.Process(0x74, true, true, c); Assert(!d.Suppress && d.Action == null, "injected down"); Assert(!m.Process(0x74, false, true, c).Suppress, "injected up"); Assert(m.Process(0x74, false, false, c).Suppress, "physical up remains paired"); });
            Test("Launches, shortcuts and toggles run once per press", delegate { foreach (var kind in new[] { ActionKind.Application, ActionKind.Command, ActionKind.FileOrFolder, ActionKind.WindowsShortcut, ActionKind.SendKey, ActionKind.SendShortcut, ActionKind.Media }) { var c = Active(kind, "VolumeMute"); var m = new KeyStateMachine(); Assert(m.Process(0x74, true, false, c).Action != null, "initial"); for (int n = 0; n < 30; n++) Assert(m.Process(0x74, true, false, c).Action == null, "repeat blocked"); m.Process(0x74, false, false, c); Assert(m.Process(0x74, true, false, c).Action != null, "next press"); } });
            Test("Disable mid-press keeps release paired", delegate { var c = Active(ActionKind.Media, "VolumeUp"); var m = new KeyStateMachine(); m.Process(0x74, true, false, c); c.Enabled = false; m.CancelHeldActions(); var d = m.Process(0x74, true, false, c); Assert(d.Suppress && d.Action == null, "repeat off"); Assert(m.Process(0x74, false, false, c).Suppress, "matching up"); Assert(!m.Process(0x74, true, false, c).Suppress, "new pass"); });
            Test("Enable mid-press preserves original pass-through release", delegate { var c = Active(ActionKind.Unbound, ""); c.Enabled = false; var m = new KeyStateMachine(); m.Process(0x74, true, false, c); c.Enabled = true; Assert(!m.Process(0x74, true, false, c).Suppress && !m.Process(0x74, false, false, c).Suppress, "paired pass"); Assert(m.Process(0x74, true, false, c).Suppress, "next remap"); });
            Test("Emergency chord requires 1.5 seconds, fires once, rearms", delegate { var e = new EmergencyHold(); Assert(!e.Tick(true, 0) && !e.Tick(true, 1499) && e.Tick(true, 1500), "hold"); Assert(!e.Tick(true, 3000), "once"); e.Tick(false, 3100); Assert(!e.Tick(true, 3200) && e.Tick(true, 4700), "rearm"); });
            Test("Action dispatcher routes keyboard and all media actions", delegate { var sink = new FakeSink(); var d = new ActionDispatcher(sink); d.Execute(new Mapping { Kind = ActionKind.SendShortcut, Target = "Alt+Tab" }); foreach (string s in Shortcuts.MediaLabels.Keys) d.Execute(new Mapping { Kind = ActionKind.Media, Target = s }); d.Execute(new Mapping { Kind = ActionKind.Unbound }); Assert(sink.Keys.Count == 8 && sink.Keys[0].SequenceEqual(new[] { 18, 9 }) && sink.Launches.Count == 0, "routes"); });
            Test("Cancelled actions stop before side effects and after validation", delegate {
                var sink = new FakeSink(); var dispatcher = new ActionDispatcher(sink); var mapping = new Mapping { Kind = ActionKind.SendKey, Target = "A" };
                dispatcher.Execute(mapping, delegate { return false; });
                int checks = 0; dispatcher.Execute(mapping, delegate { return ++checks < 2; });
                Assert(sink.Keys.Count == 0, "cancelled action reached the key sink");
            });
            Test("Lock-then-sleep cancels its delayed suspend when shortcuts are disabled", delegate {
                int checks = 0, suspends = 0; bool locked = false;
                bool completed = ActionDispatcher.RunLockThenSleep(delegate { locked = true; return true; }, delegate { suspends++; }, delegate { return ++checks < 3; });
                Assert(locked && !completed && suspends == 0 && checks == 3, "cancelled delayed sleep should keep the workstation locked without suspending");
                suspends = 0;
                completed = ActionDispatcher.RunLockThenSleep(delegate { return false; }, delegate { suspends++; }, delegate { return true; });
                Assert(!completed && suspends == 0, "failed workstation lock must not suspend");
                var elapsed = System.Diagnostics.Stopwatch.StartNew(); int activeChecks = 0;
                completed = ActionDispatcher.RunLockThenSleep(30, delegate { return true; }, delegate { suspends++; }, delegate { activeChecks++; return true; });
                Assert(completed && suspends == 1 && activeChecks >= 2 && elapsed.ElapsedMilliseconds >= 30, "active lock-then-sleep did not preserve its delay and suspend action");
            });
            Test("Custom Lock-then-sleep reaches the shared dispatcher and cancels safely on disable", delegate {
                KeyboardEngine engine = null; bool locked = false; int suspends = 0; var sink = new FakeSink();
                using (var lockStarted = new AutoResetEvent(false)) using (var suspendCalled = new AutoResetEvent(false))
                using (engine = new KeyboardEngine(sink, false, new FakeDdc(), delegate { locked = true; engine.SetEnabled(false); lockStarted.Set(); return true; }, delegate { Interlocked.Increment(ref suspends); suspendCalled.Set(); }))
                {
                    engine.Apply(new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+L", Action = new Mapping { Kind = ActionKind.LockThenSleep } } } });
                    Assert(engine.TriggerCustomHotkey("Ctrl+Alt+L"), "custom Lock-then-sleep action was not queued");
                    Assert(lockStarted.WaitOne(1000), "custom Lock-then-sleep action did not reach the injected lock operation");
                    Assert(!engine.Enabled && locked && !suspendCalled.WaitOne(900) && suspends == 0, "disabling shortcuts during a custom Lock-then-sleep action still suspended the machine");
                }
            });
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
#if PRIVATE_DDC_BACKLIGHT
            Test("DDC monitor power and backlight actions do not repeat", delegate { foreach (string id in new[] { "MonitorWake", "MonitorStandby", "BacklightOn", "BacklightOff", "BacklightLevelOn", "BacklightLevelOff" }) { var operation = DdcOperation.Find(id); Assert(operation != null && operation.IsOneShotCommand, id + " command"); var m = MonitorConfig().Mappings[0]; m.MonitorControl = id; Assert(!m.Repeats && !m.Summary.Contains("(5%)"), id + " mapping summary/repeat"); } });
#endif
            Test("DDC strict schema rejects extra launch fields and fractional step", delegate { string json = ConfigStore.Serialize(MonitorConfig()); Reject(() => ConfigStore.Parse(json.Replace("\"step\":5", "\"step\":5,\"target\":\"x\""))); Reject(() => ConfigStore.Parse(json.Replace("\"step\":5", "\"step\":1.5"))); });
            Test("DDC capability parser ignores nested values", delegate { var codes = DdcService.ParseVcpCodes("(prot(monitor)vcp(10 12 60(01 10 12 62) 62)mccs_ver(2.1))"); Assert(codes.SetEquals(new byte[] {16,18,96,98}), "codes"); codes = DdcService.ParseVcpCodes("(vcp(60(10 12 62)))"); Assert(codes.SetEquals(new byte[] {96}), "nested excluded"); Assert(DdcService.ParseVcpCodes("vcp(10") == null && DdcService.ParseVcpCodes(null) == null, "fallback"); });
            Test("DDC steps scale and clamp without overflow", delegate { Assert(DdcOperation.Next(98,100,5,1)==100 && DdcOperation.Next(2,100,5,-1)==0, "bounds"); Assert(DdcOperation.Next(50,255,5,1)==63, "scaled"); Assert(DdcOperation.Next(0,10,1,1)==1, "minimum step"); Assert(DdcOperation.Next(65530,65535,20,1)==65535, "overflow"); Reject(() => DdcOperation.Next(1,0,5,1)); Reject(() => DdcOperation.Next(101,100,5,1)); });
            Test("DDC dispatch is separate from keys and launches and propagates cancellation", delegate { var sink = new FakeSink(); var monitor = new FakeDdc(); var d = new ActionDispatcher(sink, monitor); var m = MonitorConfig().Mappings[0]; d.Execute(m, () => false); Assert(monitor.Calls == 1 && !monitor.Allowed && monitor.Last.MonitorControl == "VolumeDown" && sink.Keys.Count == 0 && sink.Launches.Count == 0, "routing"); });
            Test("DDC held key repeats and disable cancels repeat", delegate { var c = MonitorConfig(); c.Enabled = true; var m = new KeyStateMachine(); Assert(m.Process(0x70,true,false,c).Action != null && m.Process(0x70,true,false,c).Action != null, "repeat"); c.Enabled = false; m.CancelHeldActions(); Assert(m.Process(0x70,true,false,c).Action == null && m.Process(0x70,false,false,c).Suppress, "disabled pair"); });
            Test("Undetected monitor cannot dispatch native writes", delegate { var service = new DdcService(); bool rejected = false; try { service.Apply(MonitorConfig().Mappings[0], () => true); } catch (InvalidOperationException) { rejected = true; } Assert(rejected,"unsupported"); service.Apply(MonitorConfig().Mappings[0], () => false); });
            Test("Preferences default, migrate, persist and back up", delegate { string path = Path.Combine(scratch,"preferences.json"); var defaults = UserPreferences.Load(path); Assert(defaults.UseTray && defaults.CheckUpdates && defaults.AutomaticProfiles && !defaults.NetworkAccess,"privacy-first defaults"); var migrated = UserPreferences.Parse("{\"version\":1,\"useTray\":false}"); Assert(!migrated.UseTray && migrated.CheckUpdates && migrated.AutomaticProfiles && !migrated.NetworkAccess,"v1 migration"); var v2 = UserPreferences.Parse("{\"version\":2,\"useTray\":true,\"checkUpdates\":true,\"automaticProfiles\":true}"); Assert(v2.NetworkAccess,"v2 preserves enabled updates"); UserPreferences.Save(path,new UserPreferences {UseTray=false,CheckUpdates=false,AutomaticProfiles=true,NetworkAccess=true}); var loaded=UserPreferences.Load(path); Assert(!loaded.UseTray && !loaded.CheckUpdates && loaded.AutomaticProfiles && loaded.NetworkAccess,"saved"); UserPreferences.Save(path,new UserPreferences {UseTray=true,CheckUpdates=true,AutomaticProfiles=false,NetworkAccess=false}); Assert(UserPreferences.Load(path).UseTray && !UserPreferences.Load(path+".bak").UseTray,"backup"); });
            Test("Profile schedules validate strictly and replace atomically", delegate {
                Assert(ProfileScheduleStore.PollIntervalMilliseconds == 30000, "profile schedule polling drifted from the shared WinForms/WPF cadence");
                string path = Path.Combine(scratch, "profile-schedules.json");
                var morning = new ProfileSchedule { Profile = "Work", Days = "12345", StartMinute = 540, EndMinute = 1020, Enabled = true };
                ProfileScheduleStore.Save(path, new[] { morning }); Assert(ProfileScheduleStore.Load(path).Single().Profile == "Work", "round-trip");
                var evening = new ProfileSchedule { Profile = "Game", Days = "67", StartMinute = 600, EndMinute = 900, Enabled = false };
                ProfileScheduleStore.Save(path, new[] { evening }); Assert(ProfileScheduleStore.Load(path).Single().Profile == "Game" && ProfileScheduleStore.Load(path + ".bak").Single().Profile == "Work", "atomic replacement backup");
                bool invalidRejected = false; try { ProfileScheduleStore.Save(path, new[] { new ProfileSchedule { Profile = "Bad", Days = "11", StartMinute = 600, EndMinute = 540 } }); } catch (ArgumentException) { invalidRejected = true; }
                Assert(invalidRejected && ProfileScheduleStore.Load(path).Single().Profile == "Game" && Directory.GetFiles(scratch, ".profile-schedules-*.tmp").Length == 0, "invalid save damaged valid state");
                File.WriteAllText(path, "{\"version\":1,\"schedules\":[{\"profile\":\"Work\"}]}"); bool malformedRejected = false; try { ProfileScheduleStore.Load(path); } catch (ArgumentException) { malformedRejected = true; }
                Assert(malformedRejected, "malformed schedules silently accepted");
            });
            Test("Automatic profiles only switch in the clean, hidden, unpinned runtime state", delegate {
                Assert(AutomaticProfilePolicy.CanSwitch(true, false, false, false, true), "eligible background session was blocked");
                Assert(!AutomaticProfilePolicy.CanSwitch(false, false, false, false, true), "disabled automatic switching was allowed");
                Assert(!AutomaticProfilePolicy.CanSwitch(true, true, false, false, true), "pinned profile was overridden");
                Assert(!AutomaticProfilePolicy.CanSwitch(true, false, true, false, true), "visible editor profile was overridden");
                Assert(!AutomaticProfilePolicy.CanSwitch(true, false, false, true, true), "unsaved edits were exposed to profile switching");
                Assert(!AutomaticProfilePolicy.CanSwitch(true, false, false, false, false), "switch was allowed without a running runtime");
                Assert(AutomaticProfilePolicy.ShouldPollForegroundForAutomaticProfile(true, false), "poll fallback was disabled without foreground notifications");
                Assert(!AutomaticProfilePolicy.ShouldPollForegroundForAutomaticProfile(true, true), "foreground polling remained enabled despite notifications");
                Assert(!AutomaticProfilePolicy.ShouldPollForegroundForAutomaticProfile(false, false), "polling ran when automatic profiles were disabled");
            });
            Test("Controller polling cadence follows display refresh with a safe fallback", delegate {
                Assert(ControllerPollCadence.NormalizeRefreshRate(60) == 60, "60 Hz refresh changed");
                Assert(ControllerPollCadence.NormalizeRefreshRate(144) == 144, "high refresh rate changed");
                Assert(ControllerPollCadence.NormalizeRefreshRate(0) == 60 && ControllerPollCadence.NormalizeRefreshRate(1) == 60, "unknown display refresh did not use the safe fallback");
                Assert(ControllerPollCadence.NormalizeRefreshRate(1000) == 60, "invalid display refresh was not bounded");
                Assert(ControllerPollCadence.Period100Nanoseconds(60) == 166667, "60 Hz timer period is not frame-matched");
                Assert(ControllerPollCadence.Period100Nanoseconds(144) == 69444, "144 Hz timer period is not frame-matched");
                Assert(ControllerPollCadence.PeriodMilliseconds(60) == 17 && ControllerPollCadence.PeriodMilliseconds(144) == 7, "poll fallback interval does not follow refresh");
                long deadline = 1000;
                long waitAfterScan = ControllerPollCadence.AdvancePollDeadline(3000, ref deadline, 60, 10000000);
                Assert(deadline == 167667 && waitAfterScan == 164667, "scan duration was added to the refresh interval");
                long nextWaitAfterScan = ControllerPollCadence.AdvancePollDeadline(deadline + 2000, ref deadline, 60, 10000000);
                Assert(deadline == 334334 && nextWaitAfterScan == 164667, "poll cadence accumulated scan-time drift");
                long waitAfterOverrun = ControllerPollCadence.AdvancePollDeadline(600000, ref deadline, 60, 10000000);
                Assert(deadline == 667668 && waitAfterOverrun == 67668, "late polling attempted a catch-up spin instead of skipping missed frames");
                int foregroundDisplayRefreshRate;
                bool foregroundMonitorWasResolved = ControllerPollCadence.TryGetForegroundDisplayRefreshRate(out foregroundDisplayRefreshRate);
                int activeDisplayRefreshRate = ControllerPollCadence.CurrentRefreshRate();
                Assert(activeDisplayRefreshRate >= 24 && activeDisplayRefreshRate <= 500, "active display refresh probe returned an unsafe rate");
                if (foregroundMonitorWasResolved) Assert(activeDisplayRefreshRate == foregroundDisplayRefreshRate, "controller polling ignored the foreground window's monitor refresh");
            });
            Test("Hardware trigger services require the extension and at least one configured trigger", delegate {
                Assert(!FirstPartyExtensionCatalog.ShouldRunHardwareTriggerService(false, 1), "disabled hardware extension started a polling service");
                Assert(!FirstPartyExtensionCatalog.ShouldRunHardwareTriggerService(true, 0), "empty trigger configuration started a service");
                Assert(FirstPartyExtensionCatalog.ShouldRunHardwareTriggerService(true, 1), "enabled configured hardware trigger was blocked");
            });
            Test("Smooth scrolling and toast style preferences persist and migrate safely", delegate { string json = UserPreferences.Serialize(new UserPreferences { SmoothScrolling = true, ToastStyle = "Compact flyout" }); Assert(json.Contains("\"version\": 8") && json.Contains("\"smoothScrolling\": true") && json.Contains("\"toastStyle\": \"Compact flyout\""), "v8 serialization"); var parsed = UserPreferences.Parse(json); Assert(parsed.SmoothScrolling && parsed.ToastStyle == "Compact flyout", "toast style round trip"); string legacy = json.Replace("Compact flyout", "Windows 11"); Assert(UserPreferences.Parse(legacy).ToastStyle == "Compact flyout", "legacy toast style migration"); string v6 = "{\"version\":6,\"useTray\":true,\"checkUpdates\":true,\"automaticProfiles\":true,\"networkAccess\":false,\"theme\":\"KiWeave Dark\",\"customAccent\":\"\",\"historyRetention\":20}"; Assert(!UserPreferences.Parse(v6).SmoothScrolling && UserPreferences.Parse(v6).ToastStyle == "Compact flyout", "v6 defaults"); });
            Test("Compact flyout style uses the compact OSD surface", delegate { StatusOverlay.Style = "Compact flyout"; Assert(StatusOverlay.UsesWindowsStyle, "compact flyout style was not selected"); using (var form = new StatusOverlayForm("Discord", "Muted", "mute", true, true)) Assert(form.Size == new System.Drawing.Size(160, 48), "compact flyout size changed"); StatusOverlay.Style = "KiWeave"; });
            Test("Preferences reject malformed and ambiguous input", delegate { foreach (string json in new[] {"{}", "{\"version\":2,\"useTray\":true}", "{\"version\":2,\"useTray\":true,\"checkUpdates\":true,\"automaticProfiles\":true,\"extra\":false}", "{\"version\":3,\"useTray\":true,\"checkUpdates\":true,\"automaticProfiles\":true}", "{\"version\":1,\"useTray\":\"false\"}", "{\"version\":1,\"useTray\":true,\"useTray\":false}", "{\"version\":1,\"useTray\":true,\"command\":\"x\"}"}) Reject(() => UserPreferences.Parse(json)); });
            Test("Private mapping notes persist outside exported configuration", delegate { string path = Path.Combine(scratch, "mapping-notes.json"); var notes = new Dictionary<string, string>(); string key = MappingNoteStore.FunctionKey("Default", "Base", 4); MappingNoteStore.Set(notes, key, "Use this for streaming"); MappingNoteStore.Save(path, notes); var loaded = MappingNoteStore.Load(path); Assert(loaded[key] == "Use this for streaming", "note round trip"); MappingNoteStore.Set(loaded, key, ""); Assert(!loaded.ContainsKey(key), "note clear"); Assert(!ConfigStore.Serialize(new Configuration()).Contains("streaming"), "note leaked into config"); Reject(() => MappingNoteStore.Set(notes, key, new string('x', MappingNoteStore.MaxNoteLength + 1))); });
            Test("Custom hotkey copies preserve action templates", delegate { var source = new CustomHotkey { Shortcut = "Ctrl+Alt+R", Action = new Mapping { Kind = ActionKind.SendKey, Target = "R" } }; var copy = source.Copy(); copy.Shortcut = "Ctrl+Alt+K"; Assert(copy.Action.Target == "R" && copy.Shortcut != source.Shortcut && !Object.ReferenceEquals(copy.Action, source.Action), "template copy"); });
            Test("Discord authorization status explains refresh failure", delegate { Assert(DiscordIntegration.StatusText(true, true, true).Contains("could not be saved"), "connected token-save warning"); Assert(DiscordIntegration.StatusText(true, true, false) == "Connected", "connected status"); Assert(DiscordIntegration.StatusText(false, true, false).Contains("Authorized"), "authorized status"); Assert(DiscordIntegration.StatusText(false, true, true).Contains("reconnect"), "refresh warning"); Assert(DiscordIntegration.StatusText(false, false, false) == "Not connected", "empty status"); });
            Test("Discord reconnect polling only runs when networking and authorization are available", delegate { Assert(!DiscordIntegration.ShouldMonitorReconnect(false, true, true), "network-off reconnect monitor"); Assert(!DiscordIntegration.ShouldMonitorReconnect(true, false, false), "unauthorized reconnect monitor"); Assert(DiscordIntegration.ShouldMonitorReconnect(true, true, false), "access-token reconnect monitor"); Assert(DiscordIntegration.ShouldMonitorReconnect(true, false, true), "refresh-token reconnect monitor"); });
            Test("Discord reconnect results reject stale authorization and shutdown races", delegate { Assert(DiscordIntegration.ShouldCommitReconnect(4, 4, true, true, false), "current reconnect result was rejected"); Assert(!DiscordIntegration.ShouldCommitReconnect(3, 4, true, true, false), "a stale reconnect replaced newer Discord state"); Assert(!DiscordIntegration.ShouldCommitReconnect(4, 4, false, true, false), "a cancelled token refresh committed after network access was disabled"); Assert(!DiscordIntegration.ShouldCommitReconnect(4, 4, true, false, false), "a reconnect committed after authorization changed"); Assert(!DiscordIntegration.ShouldCommitReconnect(4, 4, true, true, true), "a background reconnect replaced a session connected by another path"); Assert(DiscordIntegration.ShouldPersistRefreshedToken(true, true), "a successful token rotation was discarded during close"); Assert(!DiscordIntegration.ShouldPersistRefreshedToken(true, false) && !DiscordIntegration.ShouldPersistRefreshedToken(false, true), "a stale or failed token refresh was persisted"); });
            Test("Discord voice hotkeys avoid refresh waits and overlapping reconnects", delegate { Assert(DiscordIntegration.ShouldAttemptImmediateVoiceReconnect(true, false, false), "an authorized startup action should attempt its short local reconnect"); Assert(!DiscordIntegration.ShouldAttemptImmediateVoiceReconnect(false, false, false), "a missing access token must not refresh synchronously from the hotkey"); Assert(!DiscordIntegration.ShouldAttemptImmediateVoiceReconnect(true, true, false), "the hotkey duplicated an in-flight background reconnect"); Assert(!DiscordIntegration.ShouldAttemptImmediateVoiceReconnect(true, false, true), "the hotkey overlapped authorization restoration"); });
            Test("Discord IPC fallback shortcuts only run when Discord is focused", delegate { Assert(SystemActions.ShouldSendDiscordShortcutFallback(true), "focused Discord should receive its native shortcut fallback"); Assert(!SystemActions.ShouldSendDiscordShortcutFallback(false), "failed IPC must not send a Discord shortcut to another foreground app"); });
            Test("Discord voice IPC handshakes and serializes concurrent requests", delegate {
                string pipeName = "kiweave-discord-voice-" + Guid.NewGuid().ToString("N");
                using (var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                using (var client = new DiscordIpcClient("test-access-token", delegate(int index) { return pipeName; })) {
                    bool overlapped = false;
                    Task<string[]> serverTask = Task.Factory.StartNew(delegate {
                        server.WaitForConnection();
                        string hello = ReadDiscordTestFrame(server);
                        if (!hello.Contains("\"client_id\":")) throw new InvalidOperationException("Discord IPC HELLO payload");
                        WriteDiscordTestFrame(server, "{\"evt\":\"READY\",\"data\":{}}");
                        string authenticate = ReadDiscordTestFrame(server);
                        if (!authenticate.Contains("\"cmd\":\"AUTHENTICATE\"") || !authenticate.Contains("test-access-token")) throw new InvalidOperationException("Discord authentication payload");
                        WriteDiscordTestFrame(server, "{\"evt\":\"READY\",\"data\":{}}");
                        string initialState = ReadDiscordTestFrame(server);
                        if (!initialState.Contains("\"cmd\":\"GET_VOICE_SETTINGS\"")) throw new InvalidOperationException("Initial voice-state request");
                        WriteDiscordTestFrame(server, "{\"evt\":\"VOICE_SETTINGS_UPDATE\",\"data\":{\"mute\":false,\"deaf\":false}}");

                        string firstToggle = ReadDiscordTestFrame(server);
                        Task<string> secondRequest = Task.Factory.StartNew(() => ReadDiscordTestFrame(server), TaskCreationOptions.LongRunning);
                        if (secondRequest.Wait(300)) { overlapped = true; }
                        WriteDiscordTestFrame(server, "{\"evt\":\"VOICE_SETTINGS_UPDATE\",\"data\":{}}");
                        Assert(secondRequest.Wait(3000), "second serialized voice request was not sent after the first response");
                        string secondToggle = secondRequest.GetAwaiter().GetResult();
                        WriteDiscordTestFrame(server, "{\"evt\":\"VOICE_SETTINGS_UPDATE\",\"data\":{}}");
                        return new[] { firstToggle, secondToggle };
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                    client.Connect(1000);
                    using (var ready = new CountdownEvent(2)) using (var start = new ManualResetEvent(false)) {
                        Task<bool> mute = Task.Factory.StartNew(delegate { ready.Signal(); start.WaitOne(); return client.ToggleVoice("mute"); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                        Task<bool> deafen = Task.Factory.StartNew(delegate { ready.Signal(); start.WaitOne(); return client.ToggleVoice("deaf"); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                        try {
                            Assert(ready.Wait(2000), "concurrent voice callers did not reach the start gate"); start.Set();
                            Assert(Task.WaitAll(new Task[] { mute, deafen }, 5000), "serialized Discord voice requests timed out");
                            string[] requests = serverTask.GetAwaiter().GetResult();
                            Assert(mute.Result && deafen.Result && !overlapped, "concurrent pipe operations overlapped or returned a stale state");
                            Assert(requests[0].Contains("\"cmd\":\"SET_VOICE_SETTINGS\"") && requests[1].Contains("\"cmd\":\"SET_VOICE_SETTINGS\""), "voice SET requests missing");
                            Assert(requests[0].Contains("\"args\":{\"mute\":true}") || requests[0].Contains("\"args\":{\"deaf\":true}"), "first voice field or value changed");
                            Assert(requests[1].Contains("\"args\":{\"mute\":true}") || requests[1].Contains("\"args\":{\"deaf\":true}"), "second voice field or value changed");
                            Assert(requests[0].Contains("\"args\":{\"mute\":true}") != requests[1].Contains("\"args\":{\"mute\":true}"), "concurrent mute/deafen fields were confused");
                        } finally { start.Set(); }
                    }
                }
                string unavailablePipe = "kiweave-discord-absent-" + Guid.NewGuid().ToString("N");
                using (var unavailable = new DiscordIpcClient("unused", delegate(int index) { return unavailablePipe; })) {
                    bool failed = false; try { unavailable.Connect(10); } catch (InvalidOperationException) { failed = true; }
                    var pipeField = typeof(DiscordIpcClient).GetField("pipe", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    Assert(failed && pipeField != null && pipeField.GetValue(unavailable) == null, "failed Discord pipe candidates were retained after all ten endpoints were unavailable");
                }
                string rejectedPipe = "kiweave-discord-rejected-" + Guid.NewGuid().ToString("N");
                using (var server = new NamedPipeServerStream(rejectedPipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                using (var rejected = new DiscordIpcClient("unused", delegate(int index) { return rejectedPipe; })) {
                    Task serverTask = Task.Factory.StartNew(delegate { server.WaitForConnection(); ReadDiscordTestFrame(server); WriteDiscordTestFrame(server, "{\"evt\":\"ERROR\",\"data\":{\"message\":\"test rejection\"}}"); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                    bool handshakeRejected = false; try { rejected.Connect(1000); } catch (InvalidOperationException) { handshakeRejected = true; }
                    Assert(serverTask.Wait(2000), "fake Discord server did not finish its rejected handshake");
                    var pipeField = typeof(DiscordIpcClient).GetField("pipe", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    Assert(handshakeRejected && pipeField != null && pipeField.GetValue(rejected) == null, "a rejected Discord handshake retained its connected pipe");
                }
            });
            Test("Discord IPC response reads time out and dispose a stalled pipe", delegate {
                var stalled = new StalledReadStream(); var timer = Stopwatch.StartNew(); bool timedOut = false;
                try { DiscordIpcClient.ReadWithTimeout(stalled, new byte[8], 0, 8, 100); }
                catch (TimeoutException) { timedOut = true; }
                Assert(timedOut && stalled.Disposed && timer.ElapsedMilliseconds < 1500, "stalled Discord pipe was not closed at its response deadline");
                Reject(() => DiscordIpcClient.ReadWithTimeout(stalled, new byte[1], 0, 1, 0));
                string pipeName = "kiweave-discord-timeout-" + Guid.NewGuid().ToString("N");
                using (var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous)) {
                    var accepting = Task.Factory.StartNew(() => server.WaitForConnection()); client.Connect(2000);
                    Assert(accepting.Wait(2000), "local stalled-pipe server did not accept the test client");
                    timer.Restart(); timedOut = false;
                    try { DiscordIpcClient.ReadWithTimeout(client, new byte[8], 0, 8, 150); }
                    catch (TimeoutException) { timedOut = true; }
                    Assert(timedOut && timer.ElapsedMilliseconds < 1500, "real async named pipe did not time out promptly (timedOut=" + timedOut + ", elapsed=" + timer.ElapsedMilliseconds + "ms)");
                }
            });
            Test("Discord refresh-token writes are atomic and unreadable tokens are preserved", delegate {
                string path = Path.Combine(scratch, "discord.refresh.dpapi");
                DiscordIntegration.SaveProtectedToken(path, "refresh-old");
                DiscordIntegration.SaveProtectedToken(path, "refresh-current");
                Assert(DiscordIntegration.LoadProtectedToken(path) == "refresh-current", "current token round-trip");
                Assert(DiscordIntegration.LoadProtectedToken(path + ".bak") == "refresh-old", "previous token backup");
                Assert(Directory.GetFiles(scratch, ".discord.refresh.dpapi.*.tmp").Length == 0, "temporary token file remains");
                byte[] damaged = new byte[] { 1, 2, 3, 4 }; File.WriteAllBytes(path, damaged);
                bool rejected = false; try { DiscordIntegration.LoadProtectedToken(path); } catch (InvalidDataException) { rejected = true; }
                Assert(rejected && File.ReadAllBytes(path).SequenceEqual(damaged), "unreadable token was silently discarded or modified");
            });
            Test("PowerToys drift fingerprint is stable and descriptive", delegate { var a = new[] { new PowerToysShortcut { Module = "Keyboard Manager", Action = "Color", Chord = "Ctrl+Alt+C", ModuleEnabled = true }, new PowerToysShortcut { Module = "FancyZones", Action = "Layout", Chord = "Win+`", ModuleEnabled = false } }; var b = a.Reverse().ToArray(); Assert(PowerToysDrift.Fingerprint(a) == PowerToysDrift.Fingerprint(b), "order-sensitive fingerprint"); string first = PowerToysDrift.Fingerprint(a), changed = PowerToysDrift.Fingerprint(new[] { a[0] }); Assert(PowerToysDrift.Compare("", first) == "Baseline recorded" && PowerToysDrift.Compare(first, first).Contains("No changes") && PowerToysDrift.Compare(first, changed).Contains("Changed"), "drift comparison"); });
            Test("Monitor capability drift fingerprint is stable and descriptive", delegate { var a = new[] { new DdcMonitor { Id = "a", Name = "Desk", Codes = new byte[] { 0x12, 0x10 } } }; var b = new[] { new DdcMonitor { Id = "a", Name = "Desk", Codes = new byte[] { 0x10, 0x12 } } }; Assert(MonitorDrift.Fingerprint(a) == MonitorDrift.Fingerprint(b), "monitor order-sensitive fingerprint"); string first = MonitorDrift.Fingerprint(a), changed = MonitorDrift.Fingerprint(new DdcMonitor[0]); Assert(MonitorDrift.Compare("", first) == "Baseline recorded" && MonitorDrift.Compare(first, first).Contains("No changes") && MonitorDrift.Compare(first, changed).Contains("Changed"), "monitor drift comparison"); });
            Test("Audio availability drift fingerprint is descriptive", delegate { string first = AudioDrift.Fingerprint("speaker-a\nspeaker-b"), same = AudioDrift.Fingerprint("speaker-a\nspeaker-b"), changed = AudioDrift.Fingerprint("speaker-a"); Assert(first == same && first != changed && AudioDrift.Compare("", first) == "Baseline recorded" && AudioDrift.Compare(first, same).Contains("No changes") && AudioDrift.Compare(first, changed).Contains("Changed"), "audio drift comparison"); });
            Test("Keyboard layout drift comparison is descriptive", delegate { Assert(KeyboardLayoutDrift.Compare("", "00000409|0x409") == "Baseline recorded" && KeyboardLayoutDrift.Compare("00000409|0x409", "00000409|0x409").Contains("No changes") && KeyboardLayoutDrift.Compare("00000409|0x409", "00000407|0x407").Contains("Changed"), "keyboard layout drift comparison"); });
            Test("Integration drift baselines are bounded and atomically persisted", delegate {
                string folder = Path.Combine(scratch, "drift-state");
                string power = Path.Combine(folder, "power.sha256"); var shortcuts = new[] { new PowerToysShortcut { Module = "Keyboard Manager", Action = "Color", Chord = "Ctrl+Alt+C", ModuleEnabled = true } };
                Assert(PowerToysDrift.Observe(power, shortcuts) == "Baseline recorded" && PowerToysDrift.Observe(power, shortcuts) == "No changes since last check", "PowerToys observation round-trip");
                Assert(PowerToysDrift.Observe(power, new PowerToysShortcut[0]) == "Changed since last check", "PowerToys observation change");
                string monitor = Path.Combine(folder, "monitor.sha256"); var monitors = new[] { new DdcMonitor { Id = "display", Name = "Monitor", Codes = new byte[] { 0x10, 0x12 } } };
                Assert(MonitorDrift.Observe(monitor, monitors) == "Baseline recorded" && MonitorDrift.Observe(monitor, monitors) == "No changes since last check", "monitor observation persistence");
                string audio = Path.Combine(folder, "audio.sha256");
                Assert(AudioDrift.Observe(audio, "device-a") == "Baseline recorded" && AudioDrift.Observe(audio, "device-a") == "No changes since last check" && AudioDrift.Observe(audio, "device-b") == "Changed since last check", "audio observation persistence");
                string keyboard = Path.Combine(folder, "keyboard.txt");
                Assert(KeyboardLayoutDrift.Observe(keyboard, "00000409|0x409") == "Baseline recorded" && KeyboardLayoutDrift.Observe(keyboard, "00000409|0x409") == "No changes since last check", "keyboard-layout observation persistence");
                string oversized = Path.Combine(folder, "oversized.txt"); byte[] large = Encoding.ASCII.GetBytes(new string('x', 1025)); File.WriteAllBytes(oversized, large);
                Assert(AudioDrift.Observe(oversized, "device-c").Contains("Could not read") && File.ReadAllBytes(oversized).SequenceEqual(large), "invalid observation was overwritten");
                Assert(Directory.GetFiles(folder, "*.tmp").Length == 0, "atomic observation temporary file remains");
            });
            Test("Script workspace only accepts supported script types", delegate { Assert(ScriptWorkspace.IsSupported("demo.py") && ScriptWorkspace.IsSupported("demo.ps1") && !ScriptWorkspace.IsSupported("demo.exe"), "script workspace extensions"); });
            Test("Feature flags classify and gate experimental actions", delegate {
                var stable = new Mapping { Kind = ActionKind.SendKey, Target = "A" }; var experimental = new Mapping { Kind = ActionKind.Command, Target = Path.Combine(Environment.SystemDirectory, "cmd.exe") };
                Assert(FeatureFlags.IsExperimental(new Mapping { Kind = ActionKind.HttpRequest }) && FeatureFlags.IsExperimental(experimental) && !FeatureFlags.IsExperimental(stable), "feature flag classification");
                Assert(FeatureFlags.Allows(stable, false) && !FeatureFlags.Allows(experimental, false) && FeatureFlags.Allows(experimental, true), "kill-switch gate");
                var sequence = new Mapping { Kind = ActionKind.Sequence, Target = SequenceCodec.Serialize(new[] { new SequenceStep { Action = experimental } }) };
                Assert(FeatureFlags.IsExperimental(sequence) && !FeatureFlags.Allows(sequence, false), "nested experimental action bypassed the gate");
            });
            Test("Stable action dispatch avoids loading experimental feature settings", delegate {
                int reads = 0;
                var stable = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" };
                var experimental = new Mapping { Kind = ActionKind.Sequence, Target = SequenceCodec.Serialize(new[] { new SequenceStep { Action = new Mapping { Kind = ActionKind.Command, Target = Path.Combine(Environment.SystemDirectory, "cmd.exe") } } }) };
                Assert(FeatureFlags.Allows(stable, delegate { reads++; return false; }) && reads == 0, "stable action queried experimental settings");
                Assert(!FeatureFlags.Allows(experimental, delegate { reads++; return false; }) && reads == 1, "nested experimental action did not query settings exactly once");
                Assert(FeatureFlags.Allows(experimental, delegate { reads++; return true; }) && reads == 2, "enabled experimental action was blocked");
            });
            Test("Unreadable feature flags fail closed and atomic writes retain the prior state", delegate {
                string path = Path.Combine(scratch, "feature-flags", "feature-flags.json");
                var defaults = FeatureFlags.Load(path); Assert(defaults.ExperimentalEnabled && !defaults.LoadWarning, "missing file default changed");
                defaults.DeveloperMode = true; defaults.Save(path);
                var next = FeatureFlags.Load(path); Assert(next.ExperimentalEnabled && next.DeveloperMode && !next.LoadWarning, "feature flags round-trip");
                next.ExperimentalEnabled = false; next.DeveloperMode = false; next.Save(path);
                var saved = FeatureFlags.Load(path);
                Assert(!saved.ExperimentalEnabled && !saved.DeveloperMode && FeatureFlags.Load(path + ".bak").ExperimentalEnabled && FeatureFlags.Load(path + ".bak").DeveloperMode, "atomic replacement backup");
                FeatureFlags locked;
                using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) locked = FeatureFlags.Load(path);
                Assert(!locked.ExperimentalEnabled && !locked.DeveloperMode && locked.LoadWarning, "inaccessible feature settings were treated as missing");
                byte[] corrupt = Encoding.UTF8.GetBytes("{\"version\":1,\"experimentalEnabled\":true,\"experimentalEnabled\":false,\"developerMode\":true}"); File.WriteAllBytes(path, corrupt);
                var recovered = FeatureFlags.Load(path);
                Assert(!recovered.ExperimentalEnabled && !recovered.DeveloperMode && recovered.LoadWarning && File.ReadAllBytes(path).SequenceEqual(corrupt), "corrupt safety settings were enabled, hidden, or rewritten");
                File.WriteAllText(path, new string(' ', 4097), new UTF8Encoding(false));
                recovered = FeatureFlags.Load(path);
                Assert(!recovered.ExperimentalEnabled && recovered.LoadWarning, "oversized safety settings did not fail closed");
            });
            Test("Session awareness describes remote and virtual contexts", delegate { Assert(SessionAwareness.Describe(false, false) == "Local Windows session" && SessionAwareness.Describe(true, false).Contains("Remote") && SessionAwareness.Describe(false, true).ToLowerInvariant().Contains("virtual"), "session context descriptions"); });
            Test("History private notes stay in sidecars", delegate { string path = Path.Combine(scratch, "restore-point.keyweave"); File.WriteAllText(path, "placeholder"); ConfigurationHistory.SaveNote(path, "Review before restoring"); Assert(ConfigurationHistory.LoadNote(path) == "Review before restoring" && File.Exists(ConfigurationHistory.NotePath(path)), "history note sidecar"); ConfigurationHistory.SaveNote(path, ""); Assert(!File.Exists(ConfigurationHistory.NotePath(path)), "history note clear"); });
            Test("Path migration updates nested mapping paths", delegate { var c = new Configuration(); c.Mappings[0] = new Mapping { Kind = ActionKind.Application, Target = Path.Combine(scratch, "old", "tool.exe"), WorkingDirectory = Path.Combine(scratch, "old") }; int changed = PathMigration.Replace(c, Path.Combine(scratch, "old"), Path.Combine(scratch, "new")); Assert(changed == 2 && c.Mappings[0].Target.EndsWith("new" + Path.DirectorySeparatorChar + "tool.exe"), "path migration"); });
            Test("Shortcut suspension normalizes and bounds process names", delegate {
                string path = Path.Combine(scratch, "shortcut-suspension-roundtrip.txt");
                Assert(ShortcutSuspension.NormalizeNames(new[] { "Discord.exe", "notepad" }).SequenceEqual(new[] { "Discord", "notepad" }), "suspension names");
                ShortcutSuspension.SaveToPath(path, new[] { "Discord.exe", "notepad" }); bool recovered, unavailable;
                Assert(ShortcutSuspension.LoadFromPath(path, out recovered, out unavailable).SequenceEqual(new[] { "Discord", "notepad" }) && !recovered && !unavailable, "isolated suspension round-trip");
                Reject(() => ShortcutSuspension.NormalizeNames(Enumerable.Range(0, 33).Select(i => "app" + i)));
            });
            Test("Shortcut-suspension probe failures fail closed", delegate {
                Func<bool> available = () => false;
                Assert(ShortcutSuspension.IsSuspendedSafely(() => new[] { "chrome" }, () => "chrome.exe", available), "matching foreground process was not suspended");
                Assert(!ShortcutSuspension.IsSuspendedSafely(() => new[] { "chrome" }, () => "code.exe", available), "unlisted foreground process was suspended");
                Assert(!ShortcutSuspension.IsSuspendedSafely(() => new[] { "chrome" }, () => "", available), "empty desktop foreground was mistaken for a failed lookup");
                Assert(ShortcutSuspension.IsSuspendedSafely(() => new[] { "chrome" }, () => "code.exe", () => true), "unreadable suspension preferences did not fail closed");
                Assert(ShortcutSuspension.IsSuspendedSafely(() => { throw new IOException("probe"); }, () => "code.exe", available), "settings load failure escaped fail-closed policy");
                Assert(ShortcutSuspension.IsSuspendedSafely(() => new string[0], () => { throw new InvalidOperationException("probe"); }, available), "foreground probe failure escaped fail-closed policy");
                Assert(ShortcutSuspension.IsSuspendedSafely(() => new string[0], () => null, available), "unavailable foreground process was treated as safe");
            });
            Test("Unreadable shortcut-suspension state recovers or fails closed without rewriting", delegate {
                string path = Path.Combine(scratch, "shortcut-suspension.txt"); bool recovered, unavailable;
                Assert(ShortcutSuspension.LoadFromPath(path, out recovered, out unavailable).Length == 0 && !recovered && !unavailable, "missing file defaults safely");
                byte[] saved = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("Discord.exe\r\nnotepad")).ToArray();
                File.WriteAllBytes(path, new byte[] { (byte)'{', 0xFF, (byte)'}' }); File.WriteAllBytes(path + ".bak", saved); byte[] badMain = File.ReadAllBytes(path);
                string[] names = ShortcutSuspension.LoadFromPath(path, out recovered, out unavailable);
                Assert(recovered && !unavailable && names.SequenceEqual(new[] { "Discord", "notepad" }) && File.ReadAllBytes(path).SequenceEqual(badMain), "valid backup recovery preserves damaged main file");
                File.WriteAllBytes(path + ".bak", new byte[] { (byte)'{', 0xFF, (byte)'}' }); byte[] badBackup = File.ReadAllBytes(path + ".bak");
                names = ShortcutSuspension.LoadFromPath(path, out recovered, out unavailable);
                Assert(unavailable && !recovered && names.Length == 0 && ShortcutSuspension.IsProcessSuspended("notepad", names, unavailable) && File.ReadAllBytes(path).SequenceEqual(badMain) && File.ReadAllBytes(path + ".bak").SequenceEqual(badBackup), "dual corruption pauses remapping and preserves both files");
                File.WriteAllBytes(path, Enumerable.Repeat((byte)'x', 4097).ToArray()); File.Delete(path + ".bak"); names = ShortcutSuspension.LoadFromPath(path, out recovered, out unavailable);
                Assert(unavailable && ShortcutSuspension.IsProcessSuspended("anything", names, unavailable), "oversized suspension policy fails closed");
            });
            Test("Registered custom hotkeys respect per-application suspension", delegate { var suspended = new[] { "Discord.exe", "GameClient" }; Assert(ShortcutSuspension.IsProcessSuspended("discord", suspended), "case-insensitive executable normalization"); Assert(ShortcutSuspension.IsProcessSuspended("GameClient.exe", suspended), "executable suffix normalization"); Assert(!ShortcutSuspension.IsProcessSuspended("notepad.exe", suspended), "unlisted app should remain active"); Assert(!ShortcutSuspension.IsProcessSuspended("", suspended), "empty foreground process"); });
            Test("Modifierless custom keys bypass RegisterHotKey without false failures", delegate {
                var config = new Configuration { Enabled = true, CustomHotkeys = new[] {
                    new CustomHotkey { Shortcut = "F13", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+A", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } },
                    new CustomHotkey { Shortcut = "Insert", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } },
                    new CustomHotkey { Shortcut = "MediaPlayPause", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } }
                } };
                var registered = new Dictionary<int, CustomHotkey>(); var attempts = new List<uint>();
                Func<IntPtr, int, uint, uint, bool> register = (window, id, modifiers, key) => { attempts.Add(key); return true; };
                Func<IntPtr, int, bool> unregister = (window, id) => true;
                string[] failures = HotkeyRegistration.Apply(config, IntPtr.Zero, registered, true, false, register, unregister, () => 0);
                Assert(failures.Length == 0 && attempts.SequenceEqual(new[] { (uint)Keys.A }) && registered.Count == 1, "standalone keys were incorrectly registered or reported as failures");
            });
            Test("F12 custom chords explain the Windows debugger reservation without blocking siblings", delegate {
                var config = new Configuration { Enabled = true, CustomHotkeys = new[] {
                    new CustomHotkey { Shortcut = "Ctrl+F12", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+A", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeDown" } }
                } };
                var registered = new Dictionary<int, CustomHotkey>(); var attempts = new List<uint>();
                Func<IntPtr, int, uint, uint, bool> register = (window, id, modifiers, key) => { attempts.Add(key); return true; };
                Func<IntPtr, int, bool> unregister = (window, id) => true;
                string[] failures = HotkeyRegistration.Apply(config, IntPtr.Zero, registered, true, false, register, unregister, () => 0);
                Assert(failures.Length == 1 && failures[0].Contains("Ctrl+F12") && failures[0].Contains("reserved for debuggers"), "reserved chord did not explain the Windows limitation");
                Assert(attempts.SequenceEqual(new[] { (uint)Keys.A }) && registered.Count == 1 && registered.ContainsKey(HotkeyRegistration.FirstId + 1), "reserved F12 registration was attempted or blocked its valid sibling");
            });
            Test("Pass-through custom chords are left for Windows and removed when edited", delegate {
                var config = new Configuration { Enabled = true, CustomHotkeys = new[] {
                    new CustomHotkey { Shortcut = "Ctrl+Alt+P", Action = new Mapping { Kind = ActionKind.PassThrough } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+U", Action = new Mapping { Kind = ActionKind.Unbound } }
                } };
                var registered = new Dictionary<int, CustomHotkey>(); var attempts = new List<int>(); var released = new List<int>();
                Func<IntPtr, int, uint, uint, bool> register = (window, id, modifiers, key) => { attempts.Add(id); return true; };
                Func<IntPtr, int, bool> unregister = (window, id) => { released.Add(id); return true; };
                Assert(HotkeyRegistration.Apply(config, IntPtr.Zero, registered, true, false, register, unregister, () => 0).Length == 0, "register pass-through test");
                Assert(attempts.SequenceEqual(new[] { HotkeyRegistration.FirstId + 1 }) && registered.Keys.Single() == HotkeyRegistration.FirstId + 1, "pass-through chord was registered");
                config.CustomHotkeys[0].Action.Kind = ActionKind.Unbound;
                HotkeyRegistration.Apply(config, IntPtr.Zero, registered, true, false, register, unregister, () => 0);
                Assert(attempts.SequenceEqual(new[] { HotkeyRegistration.FirstId + 1, HotkeyRegistration.FirstId }) && registered.Count == 2, "edited action did not register");
                config.CustomHotkeys[0].Action.Kind = ActionKind.PassThrough;
                HotkeyRegistration.Apply(config, IntPtr.Zero, registered, true, false, register, unregister, () => 0);
                Assert(!registered.ContainsKey(HotkeyRegistration.FirstId) && released.Contains(HotkeyRegistration.FirstId), "pass-through edit left the chord grabbed");
            });
            Test("A failed custom hotkey registration does not disable its valid siblings", delegate {
                var config = new Configuration { Enabled = true, CustomHotkeys = new[] {
                    new CustomHotkey { Shortcut = "Ctrl+Alt+A", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+B", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeDown" } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+C", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" } }
                } };
                var registered = new Dictionary<int, CustomHotkey>(); var attempts = new List<int>(); var released = new List<int>(); bool allowB = false;
                Func<IntPtr, int, uint, uint, bool> register = (window, id, modifiers, key) => { attempts.Add(id); return key != (uint)Keys.B || allowB; };
                Func<IntPtr, int, bool> unregister = (window, id) => { released.Add(id); return true; };
                string[] failures = HotkeyRegistration.Apply(config, IntPtr.Zero, registered, true, false, register, unregister, () => 1409);
                Assert(failures.Length == 1 && failures[0].Contains("Ctrl+Alt+B") && attempts.SequenceEqual(new[] { 3000, 3001, 3002 }) && registered.Keys.OrderBy(x => x).SequenceEqual(new[] { 3000, 3002 }), "one conflict prevented later chords from registering");
                allowB = true; failures = HotkeyRegistration.Apply(config, IntPtr.Zero, registered, true, false, register, unregister, () => 1409);
                Assert(failures.Length == 0 && attempts.SequenceEqual(new[] { 3000, 3001, 3002, 3001 }) && registered.Count == 3, "failed chord was not retried without replacing healthy registrations");
                HotkeyRegistration.Apply(config, IntPtr.Zero, registered, false, false, register, unregister, () => 0);
                Assert(registered.Count == 0 && released.OrderBy(x => x).SequenceEqual(new[] { 3000, 3001, 3002 }), "disable did not unregister all owned hotkeys");
            });
            Test("Editing a custom hotkey action preserves its healthy Windows registration", delegate {
                var registered = new Dictionary<int, CustomHotkey>(); int registerCount = 0, unregisterCount = 0;
                Func<IntPtr, int, uint, uint, bool> register = (window, id, modifiers, key) => { registerCount++; return true; };
                Func<IntPtr, int, bool> unregister = (window, id) => { unregisterCount++; return true; };
                var original = new Configuration { Enabled = true, CustomHotkeys = new[] {
                    new CustomHotkey { Shortcut = "Ctrl+Alt+K", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } }
                } };
                var edited = new Configuration { Enabled = true, CustomHotkeys = new[] {
                    new CustomHotkey { Shortcut = "Ctrl+Alt+K", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" } }
                } };
                Assert(HotkeyRegistration.Apply(original, IntPtr.Zero, registered, true, false, register, unregister, () => 0).Length == 0, "initial custom hotkey did not register");
                Assert(HotkeyRegistration.Apply(edited, IntPtr.Zero, registered, true, false, register, unregister, () => 0).Length == 0, "action edit reported a healthy chord as failed");
                Assert(registerCount == 1 && unregisterCount == 0 && registered.Count == 1 && registered[HotkeyRegistration.FirstId].Action.Target == "VolumeMute", "action edit needlessly changed the OS registration or left stale dispatch data");
            });
            Test("Failed custom hotkey registration retries on a bounded timer", delegate {
                Assert(HotkeyRegistration.ShouldPollForeground(false, false), "foreground polling fallback was disabled when notifications were unavailable");
                Assert(!HotkeyRegistration.ShouldPollForeground(true, false), "foreground was polled despite a working notification hook");
                Assert(HotkeyRegistration.ShouldPollForeground(true, true), "registration retry polling was disabled while a registration was failing");
                Assert(!HotkeyRegistration.ShouldRefresh(true, true, false, 1000, 0), "healthy unchanged bindings should not re-register");
                Assert(!HotkeyRegistration.ShouldRefresh(false, false, true, 1999, 2000), "failed registration retried before its deadline");
                Assert(HotkeyRegistration.ShouldRefresh(false, false, true, 2000, 2000), "failed registration was not retried at its deadline");
                Assert(HotkeyRegistration.ShouldRefresh(false, true, false, 1000, 0), "foreground suspension changes must reconcile immediately");
            });
            Test("Foreground-change monitor filters notifications and keeps timer fallback available", delegate { Assert(ForegroundChangeMonitor.IsForegroundEvent(3), "foreground change event rejected"); Assert(!ForegroundChangeMonitor.IsForegroundEvent(0) && !ForegroundChangeMonitor.IsForegroundEvent(4), "unrelated WinEvent accepted"); });
            Test("Foreground process snapshot avoids per-key process opens and fails closed", delegate { string[] blocked = { "Discord.exe", "GameClient" }; Assert(ShortcutSuspension.IsSuspendedForForeground("discord", blocked, false), "normalized suspended process was allowed"); Assert(!ShortcutSuspension.IsSuspendedForForeground("notepad", blocked, false), "ordinary process was suspended"); Assert(ShortcutSuspension.IsSuspendedForForeground(null, blocked, false), "unavailable foreground process was allowed"); Assert(ShortcutSuspension.IsSuspendedForForeground("notepad", blocked, true), "unreadable settings did not fail closed"); });
            Test("Failed hotkey release is reported and its registration is not overwritten", delegate {
                var registered = new Dictionary<int, CustomHotkey>(); var attempts = new List<uint>(); bool allowRelease = false;
                Func<IntPtr, int, uint, uint, bool> register = (window, id, modifiers, key) => { attempts.Add(key); return true; };
                Func<IntPtr, int, bool> unregister = (window, id) => allowRelease;
                var first = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+A", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } } } };
                var replacement = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+B", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeDown" } } } };
                Assert(HotkeyRegistration.Apply(first, IntPtr.Zero, registered, true, false, register, unregister, () => 0).Length == 0, "initial hotkey registration");
                string[] failures = HotkeyRegistration.Apply(replacement, IntPtr.Zero, registered, true, false, register, unregister, () => 0);
                Assert(failures.Length == 1 && failures[0].Contains("could not be released") && registered.Count == 1 && registered[3000].Shortcut == "Ctrl+Alt+A" && attempts.SequenceEqual(new[] { (uint)Keys.A }), "failed unregister was forgotten or replacement falsely treated as active");
                allowRelease = true;
                failures = HotkeyRegistration.Apply(replacement, IntPtr.Zero, registered, true, false, register, unregister, () => 0);
                Assert(failures.Length == 0 && registered.Count == 1 && registered[3000].Shortcut == "Ctrl+Alt+B" && attempts.SequenceEqual(new[] { (uint)Keys.A, (uint)Keys.B }), "released stale hotkey was not replaced on retry");
                Func<IntPtr, int, bool> alreadyGone = (window, id) => false;
                failures = HotkeyRegistration.Apply(first, IntPtr.Zero, registered, true, false, register, alreadyGone, () => 1419);
                Assert(failures.Length == 0 && registered.Count == 1 && registered[3000].Shortcut == "Ctrl+Alt+A" && attempts.SequenceEqual(new[] { (uint)Keys.A, (uint)Keys.B, (uint)Keys.A }), "Windows-confirmed missing hotkey was not safely replaced");
            });
            Test("Release readiness validates schemas, gates and collision-safe storage", delegate {
                var checks = ReleaseReadiness.Run(new Configuration(), new ProfileCollection());
                Assert(checks.Any(x => x.Name == "Configuration schema" && x.Passed) && checks.Any(x => x.Name == "Profile schema" && x.Passed) && checks.Any(x => x.Name == "Experimental kill switch" && x.Passed) && checks.Any(x => x.Name == "Safe startup" && x.Passed), "release readiness checks");
                var failed = ReleaseReadiness.Check("Local storage", delegate { throw new UnauthorizedAccessException(@"D:\private\KiWeaveData"); }, "not displayed on failure");
                Assert(!failed.Passed && failed.Status == "Review" && !failed.Detail.Contains("D:\\private") && failed.Detail.Contains("retry"), "release readiness exposed exception path or lost safe guidance");
                string folder = Path.Combine(scratch, "readiness-probe"), sentinel = Path.Combine(folder, ".readiness-check"); Directory.CreateDirectory(folder); File.WriteAllText(sentinel, "preserve me");
                ReleaseReadiness.CheckLocalStorage(folder);
                Assert(File.ReadAllText(sentinel) == "preserve me" && Directory.GetFiles(folder).SequenceEqual(new[] { sentinel }), "storage probe modified or left files beside the pre-existing sentinel");
            });
            Test("Read-only process probes detect local processes and dispose every returned handle", delegate {
                string currentName; using (Process current = Process.GetCurrentProcess()) currentName = current.ProcessName;
                Assert(ProcessProbe.AnyRunning(currentName), "current process was not detected");
                Assert(!ProcessProbe.AnyRunning("KiWeave-missing-" + Guid.NewGuid().ToString("N")), "a nonexistent process was reported as running");
            });
            Test("Game mode persists and refreshes its input-path cache without touching live user data", delegate { string path = Path.Combine(scratch, "game-mode.flag"); try { GameMode.Set(path, true); Assert(File.ReadAllText(path) == "enabled" && GameMode.Enabled, "game mode enable was not persisted or cached"); GameMode.Set(path, false); Assert(!File.Exists(path) && !GameMode.Enabled, "game mode disable was not persisted or cached"); GameMode.Write(path, true); Assert(GameMode.RefreshEnabledState(path) && GameMode.Enabled, "external flag refresh did not update the cache"); } finally { try { File.Delete(path); } catch { } GameMode.RefreshEnabledState(); } });
            Test("Game-mode marker writes atomically and reads fail closed when oversized or malformed", delegate {
                string path = Path.Combine(scratch, "game-mode.flag");
                GameMode.Write(path, true); Assert(GameMode.ReadEnabled(path), "atomic enable write");
                File.WriteAllText(path, new string('x', 33), Encoding.ASCII); Assert(!GameMode.ReadEnabled(path), "oversized marker enabled game mode");
                File.WriteAllText(path, "enabled-ish", Encoding.ASCII); Assert(!GameMode.ReadEnabled(path), "malformed marker enabled game mode");
                GameMode.Write(path, false); Assert(!File.Exists(path), "disable removes marker");
            });
            Test("Fullscreen keeps configured function keys and standalone custom keys under KiWeave control", delegate {
                var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "F13", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" } } } };
                c.Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" }; c.Mappings[1] = new Mapping { Kind = ActionKind.PassThrough }; c.Mappings[2] = new Mapping { Kind = ActionKind.Unbound };
                Assert(GameMode.AllowsControl(c, 0x70), "mapped F1 allowed"); Assert(GameMode.AllowsControl(c, 0x71), "pass-through F2 must reach the engine without being swallowed"); Assert(GameMode.AllowsControl(c, 0x72), "explicit Unbound F3 must reach the engine"); Assert(GameMode.AllowsControl(c, 0x7B), "mapped F12 allowed");
                Assert(GameMode.AllowsControl(c, (int)Keys.VolumeUp), "hardware media allowed"); Assert(GameMode.AllowsControl(c, (int)Keys.F13), "mapped F13 allowed"); Assert(!GameMode.AllowsControl(c, (int)Keys.F14), "unmapped F14 bypassed fullscreen");
                var machine = new KeyStateMachine(); var f1Decision = machine.Process(0x70, true, false, c, false, GameMode.AllowsControl(c, 0x70)); Assert(f1Decision.Action != null && f1Decision.Action.Target == "VolumeUp", "configured action did not dispatch in fullscreen"); Assert(machine.Process(0x70, false, false, c).Suppress, "fullscreen action release was not paired");
                Assert(!machine.Process(0x71, true, false, c, false, GameMode.AllowsControl(c, 0x71)).Suppress && !machine.Process(0x71, false, false, c).Suppress, "fullscreen pass-through mapping was swallowed");
                Assert(machine.Process(0x72, true, false, c, false, GameMode.AllowsControl(c, 0x72)).Suppress && machine.Process(0x72, false, false, c).Suppress, "fullscreen explicit Unbound mapping was not paired");
            });
            Test("Backup privacy presets keep values hidden", delegate { Assert(BackupPrivacy.Review("https://private.invalid", BackupPrivacyPreset.MetadataOnly).Contains("Metadata-only") && BackupPrivacy.Review("https://private.invalid", BackupPrivacyPreset.Strict).Contains("network URLs"), "privacy presets"); });
            Test("Profile inheritance tree is readable and cycle-safe", delegate { var collection = new ProfileCollection { Profiles = new[] { new KeyWeaveProfile { Name = "Game", InheritFrom = "Default" }, new KeyWeaveProfile { Name = "Stream", InheritFrom = "Game" } } }; Assert(collection.InheritanceTree("Stream") == "Stream  →  Game  →  Default", "inheritance tree"); collection.Find("Game").InheritFrom = "Stream"; Assert(collection.InheritanceTree("Stream").Contains("inheritance loop"), "inheritance cycle"); });
            Test("Conflict center layer map visualizes hold keys", delegate { var c = new Configuration { Layers = new[] { new ModifierLayer { Name = "Fn", ActivationKey = "CapsLock" } } }; Assert(ConflictScanner.LayerVisualization(c).Contains("Fn [hold CapsLock]"), "layer visualization"); });
            Test("Keyboard layout profile mappings persist locally", delegate { string before = KeyboardLayoutProfiles.ForProfile("Test"); try { KeyboardLayoutProfiles.Set("Test", "00000409"); Assert(KeyboardLayoutProfiles.ForProfile("Test") == "00000409", "layout profile"); } finally { KeyboardLayoutProfiles.Set("Test", before); } });
            Test("Keyboard layout profile rollback reloads the restored file instead of keeping a rejected cached draft", delegate {
                string previousRoot = AppStorage.DataFolder, isolatedRoot = Path.Combine(scratch, "layout-cache-reload");
                Directory.CreateDirectory(isolatedRoot); AppStorage.SetTestDataFolder(isolatedRoot);
                try
                {
                    string path = KeyboardLayoutProfiles.DataPath;
                    KeyboardLayoutProfiles.Save(path, new Dictionary<string, string> { { "RollbackProbe", "00000409" } });
                    KeyboardLayoutProfiles.ReloadSavedAssignments();
                    Assert(KeyboardLayoutProfiles.ForProfile("RollbackProbe") == "00000409", "initial saved mapping was not loaded");
                    KeyboardLayoutProfiles.Save(path, new Dictionary<string, string> { { "RollbackProbe", "00000809" } });
                    Assert(KeyboardLayoutProfiles.ForProfile("RollbackProbe") == "00000409", "test did not establish a stale cached draft");
                    KeyboardLayoutProfiles.ReloadSavedAssignments();
                    Assert(KeyboardLayoutProfiles.ForProfile("RollbackProbe") == "00000809", "restored mapping was hidden by the rejected in-memory draft");
                }
                finally { AppStorage.SetTestDataFolder(previousRoot); KeyboardLayoutProfiles.ReloadSavedAssignments(); }
            });
            Test("Keyboard layout profile rename removes stale old assignment", delegate { var source = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Old Profile", "00000409" }, { "Other", "00000809" } }; var renamed = KeyboardLayoutProfiles.WithProfile(source, "Old Profile", "New Profile", "0000040c"); Assert(!renamed.ContainsKey("Old Profile") && renamed["New Profile"] == "0000040c" && renamed["Other"] == "00000809" && source.ContainsKey("Old Profile"), "rename must be copy-on-write and remove stale assignment"); });
            Test("Keyboard layout profile store strictly loads bounded legacy files", delegate {
                string folder = Path.Combine(scratch, "layout-profile-store"); Directory.CreateDirectory(folder); string path = Path.Combine(folder, "keyboard-layout-profiles.json");
                File.WriteAllText(path, "{\"Default\":\"00000409|0x409\",\"Work\":\"00000809\"}", new UTF8Encoding(true));
                bool recovered; string warning; var loaded = KeyboardLayoutProfiles.Load(path, out recovered, out warning);
                Assert(!recovered && warning == "" && loaded.Count == 2 && loaded["Default"] == "00000409|0x409", "legacy keyboard layout map or UTF-8 BOM was not retained");
            });
            Test("Keyboard layout profile backup recovery preserves damaged primary and backup", delegate {
                string folder = Path.Combine(scratch, "layout-profile-recovery"); Directory.CreateDirectory(folder); string path = Path.Combine(folder, "keyboard-layout-profiles.json"), backup = path + ".bak";
                string damaged = "{\"Default\":\"bad\",\"Default\":\"duplicate\"}"; string good = "{\"Default\":\"00000409\"}";
                File.WriteAllText(path, damaged, new UTF8Encoding(false)); File.WriteAllText(backup, good, new UTF8Encoding(false));
                bool recovered; string warning; var loaded = KeyboardLayoutProfiles.Load(path, out recovered, out warning);
                Assert(recovered && warning.Length > 0 && loaded["Default"] == "00000409", "valid backup was not used");
                KeyboardLayoutProfiles.Save(path, loaded, recovered);
                Assert(File.ReadAllText(backup) == good, "backup was overwritten during recovery");
                Assert(File.ReadAllText(path).Contains("00000409"), "recovered mapping was not saved");
                Assert(Directory.GetFiles(folder, "keyboard-layout-profiles.json.corrupt-*").Length == 1, "damaged primary was not preserved as a recovery artifact");
            });
            Test("Keyboard layout profile store refuses invalid data without modifying files", delegate {
                string folder = Path.Combine(scratch, "layout-profile-invalid"); Directory.CreateDirectory(folder); string path = Path.Combine(folder, "keyboard-layout-profiles.json"), backup = path + ".bak";
                string damaged = "{\"Default\":\"00000409\",}"; string damagedBackup = "not-json";
                File.WriteAllText(path, damaged, new UTF8Encoding(false)); File.WriteAllText(backup, damagedBackup, new UTF8Encoding(false));
                bool rejected = false; try { KeyboardLayoutProfiles.Load(path); } catch (InvalidDataException) { rejected = true; } Assert(rejected, "invalid primary and backup were accepted");
                Assert(File.ReadAllText(path) == damaged && File.ReadAllText(backup) == damagedBackup, "invalid files were modified");
                string ambiguous = Path.Combine(folder, "case-duplicate.json"); File.WriteAllText(ambiguous, "{\"Default\":\"00000409\",\"default\":\"00000809\"}", new UTF8Encoding(false));
                rejected = false; try { KeyboardLayoutProfiles.Load(ambiguous); } catch (InvalidDataException) { rejected = true; } Assert(rejected, "case-insensitive duplicate profile names were accepted");
                string oversized = Path.Combine(folder, "oversized.json"); File.WriteAllText(oversized, new string(' ', KeyboardLayoutProfiles.MaxFileBytes + 1), new UTF8Encoding(false));
                rejected = false; try { KeyboardLayoutProfiles.Load(oversized); } catch (InvalidDataException) { rejected = true; } Assert(rejected, "oversized layout map was accepted");
            });
            Test("Keyboard layout profile atomic saves keep a readable backup", delegate {
                string folder = Path.Combine(scratch, "layout-profile-atomic"); Directory.CreateDirectory(folder); string path = Path.Combine(folder, "keyboard-layout-profiles.json");
                var first = new Dictionary<string, string> { { "Default", "00000409" } }; KeyboardLayoutProfiles.Save(path, first);
                var second = new Dictionary<string, string> { { "Default", "00000809" } }; KeyboardLayoutProfiles.Save(path, second);
                bool recovered; string warning; var current = KeyboardLayoutProfiles.Load(path, out recovered, out warning); var backup = KeyboardLayoutProfiles.Load(path + ".bak");
                Assert(!recovered && current["Default"] == "00000809" && backup["Default"] == "00000409", "atomic save did not retain its last good backup");
                Assert(Directory.GetFiles(folder, ".keyboard-layout-profiles-*.tmp").Length == 0, "temporary save file leaked");
            });
            Test("Monitor topology describes a safe local display snapshot", delegate { Assert(MonitorTopology.Describe().Length > 0, "monitor topology"); });
            Test("Monitor mapping scope summarizes redacted targets", delegate { var c = new Configuration(); c.Mappings[0] = new Mapping { Kind = ActionKind.Monitor, MonitorId = "monitor-identifier-12345" }; Assert(MonitorMappingScope.Describe(c).Contains("saved monitor target") && !MonitorMappingScope.Describe(c).Contains("monitor-identifier-12345"), "monitor mapping scope"); });
            Test("Hardware input inventory stays read-only and descriptive", delegate { Assert(HardwareInputInventory.Describe().Length > 0, "hardware inventory"); });
            Test("Remote and VM profile policy requires explicit layout matches", delegate { Assert(SessionAwareness.AllowsAutomaticAppProfile(false, false, false) && !SessionAwareness.AllowsAutomaticAppProfile(true, false, false) && !SessionAwareness.AllowsAutomaticAppProfile(false, true, false) && SessionAwareness.AllowsAutomaticAppProfile(true, true, true), "session profile policy"); });
            Test("Action-pack loading enforces detached signature status", delegate {
                string path = Path.Combine(scratch, "signed.kiweavepack");
                string json = ActionPackStore.Serialize(new ActionPack { Id = "signed.pack", Name = "Signed pack", Configuration = new Configuration(), Profiles = new ProfileCollection() });
                File.WriteAllText(path, json); Assert(ActionPackStore.Load(path).SignatureStatus == "Unsigned detached pack", "unsigned pack status");
                using (var rsa = new RSACryptoServiceProvider(1024)) {
                    string publicKey = rsa.ToXmlString(false);
                    File.WriteAllBytes(path + ".pub", new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(publicKey)).ToArray());
                    string signature = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(json), CryptoConfig.MapNameToOID("SHA256")));
                    File.WriteAllBytes(path + ".sig", new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(signature)).ToArray());
                    Assert(ActionPackStore.Load(path).SignatureStatus.Contains("verified"), "signature verified through shared loader");
                    File.WriteAllBytes(path + ".sig", new byte[] { 0xFF }); Reject(() => ActionPackStore.Load(path));
                    File.WriteAllText(path + ".sig", Convert.ToBase64String(new byte[] { 1, 2, 3 })); Reject(() => ActionPackStore.Load(path));
                    File.Delete(path + ".sig"); Reject(() => ActionPackStore.Load(path));
                }
            });
            Test("Signed action-pack repository index stays metadata-only", delegate { string json = "{\"format\":\"kiweave-action-pack-repository\",\"version\":1,\"publisher\":\"KiWeave\",\"trust\":\"Detached signatures required\",\"entries\":[{\"id\":\"demo.pack\",\"name\":\"Demo\",\"version\":\"1.0.0\",\"publisher\":\"KiWeave\",\"description\":\"Safe demo\",\"source\":\"packs/demo.kiweavepack\",\"signature\":\"detached-rsa\"}]}"; var index = ActionPackRepository.Parse(json); Assert(index.Entries.Count == 1 && index.Entries[0].Source == "packs/demo.kiweavepack", "repository metadata"); Reject(() => ActionPackRepository.Parse(json.Replace("packs/demo.kiweavepack", "http://unsafe.invalid/demo.kiweavepack"))); });
            Test("Action-pack file reads are bounded and reject invalid UTF-8", delegate {
                string path = Path.Combine(scratch, "bounded-action-pack.kiweavepack");
                string validPack = ActionPackStore.Serialize(new ActionPack { Id = "bom.pack", Name = "BOM pack", Configuration = new Configuration(), Profiles = new ProfileCollection() });
                File.WriteAllBytes(path, new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(validPack)).ToArray());
                Assert(ActionPackStore.Load(path).Name == "BOM pack", "UTF-8 BOM compatibility");
                File.WriteAllBytes(path, Enumerable.Repeat((byte)' ', 1024 * 1024 + 1).ToArray()); Reject(() => ActionPackStore.Load(path));
                File.WriteAllBytes(path, new byte[] { (byte)'{', 0xFF, (byte)'}' }); Reject(() => ActionPackStore.Load(path));
            });
            Test("Action-pack signature sidecars are bounded before decoding", delegate {
                string path = Path.Combine(scratch, "bounded-signature.kiweavepack");
                File.WriteAllText(path, ActionPackStore.Serialize(new ActionPack { Id = "bounded.sig", Name = "Bounded signature", Configuration = new Configuration(), Profiles = new ProfileCollection() }));
                File.WriteAllText(path + ".sig", new string('A', 4097), Encoding.ASCII); File.WriteAllText(path + ".pub", "<RSAKeyValue />", new UTF8Encoding(false)); Reject(() => ActionPackStore.Load(path));
                File.WriteAllText(path + ".sig", "AA==", Encoding.ASCII); File.WriteAllText(path + ".pub", new string('x', 8193), new UTF8Encoding(false)); Reject(() => ActionPackStore.Load(path));
            });
            Test("Publishable repository template is safe and parseable", delegate { string path = Path.Combine(Environment.CurrentDirectory, "repository", "action-packs", "index.json"); Assert(File.Exists(path), "repository template"); var index = ActionPackRepository.Parse(File.ReadAllText(path, Encoding.UTF8)); Assert(index.Entries.Count == 0 && index.Trust.Contains("signatures"), "repository template policy"); });
            Test("Public release gate verifies a private-source WPF bundle and keeps publishing explicit", delegate {
                string workflowPath = Path.Combine(Environment.CurrentDirectory, ".github", "workflows", "release.yml");
                Assert(File.Exists(workflowPath), "release workflow missing");
                string workflow = File.ReadAllText(workflowPath, Encoding.UTF8).Replace("\r\n", "\n");
                Assert(workflow.Contains("workflow_dispatch:") && workflow.Contains("default: false") && workflow.Contains("type: boolean") && !workflow.Contains("  push:"), "publishing must be an explicit manual choice, never a tag-push side effect");
                Assert(workflow.Contains("permissions:\n  contents: read") && workflow.Contains("  verify-prebuilt-beta5:") && workflow.Contains("  publish-beta5:") && workflow.Contains("    permissions:\n      contents: write\n      actions: read") && workflow.Contains("contents: write") && workflow.Contains("Invoke-WebRequest") && workflow.Contains("VerifyReleaseArtifact.ps1"), "the verification job must validate exact prebuilt draft assets and the publish job must retain explicit write access");
                Assert(File.Exists(Path.Combine(Environment.CurrentDirectory, "tools", "VerifyReleaseArtifact.ps1")) && File.Exists(Path.Combine(Environment.CurrentDirectory, "tests", "ReleaseArtifactPolicy.Tests.ps1")), "release artifact verifier or tamper tests are missing");
                Assert(workflow.Contains("locally verified WPF package") && workflow.Contains("inputs.publish") && workflow.Contains("SHA256SUMS.txt") && !workflow.Contains("build.ps1") && !workflow.Contains("wpf-rewrite") && !workflow.Contains("actions/upload-artifact@v4") && !workflow.Contains("actions/download-artifact@v4"), "public workflow must verify locally built binaries without depending on private WPF source or rebuilding it");
                string readme = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "README.md"), Encoding.UTF8);
                Assert(readme.Contains("Run **Verify and publish KiWeave Beta 5** manually") && readme.Contains("upload those three files to a draft release") && readme.Contains("without rebuilding or exposing the private WPF source") && readme.Contains("Pushing a tag alone does not publish Beta 5"), "README release instructions must match the manual private-source-safe workflow");
                string ignore = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, ".gitignore"), Encoding.UTF8);
                Assert(ignore.Contains("/wpf-rewrite/") && ignore.Contains("/src/KiWeaveBuildIdentity.cs") && !ignore.Contains("!/wpf-rewrite/"), "private WPF source and build identity must not be staged accidentally");
                Assert(File.Exists(Path.Combine(Environment.CurrentDirectory, "wpf-rewrite", "KiWeave.Wpf.csproj")) && File.Exists(Path.Combine(Environment.CurrentDirectory, "wpf-rewrite", "KiWeave.Runtime.csproj")), "public WPF app or shared runtime project missing");
                string packageScript = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "tools", "PackageBeta5.ps1"), Encoding.UTF8);
                Assert(packageScript.Contains("PortableArchivePolicy.psm1") && packageScript.Contains("$allEntries = @($archive.Entries)") && packageScript.Contains("Assert-KiWeavePortableArchivePaths -Paths @($allEntries | ForEach-Object FullName)"), "portable release packager must validate every ZIP path, including directory entries");
            });
            Test("Public README reflects current Beta 5 features and safety behavior", delegate {
                string readme = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "README.md"), Encoding.UTF8);
                foreach (string required in new[] { "## What KiWeave can do", "## Extensions and hardware triggers", "the display's refresh cadence", "Media Play/Pause is a no-op", "Backlight minimum writes 0% brightness", "monitor standby/wake actions", "`.kiweave`", "458 entries across 18 categories", "Expanded action library (2026-10-23)" })
                    Assert(readme.Contains(required), "README is missing current Beta 5 documentation: " + required);
                Assert(!readme.Contains("History never syncs or restores automatically, but its `.keyweave` files"), "README must use the current .kiweave history extension");
            });
            Test("Stream Deck protocol accepts only the exact fields for each allow-listed action", delegate { Assert(StreamDeckProtocol.Parse("{\"version\":1,\"action\":\"status\"}") == "status" && StreamDeckProtocol.Parse("{\"version\":1,\"action\":\"activate-profile\",\"profile\":\"Gaming\"}") == "activate-profile", "stream deck protocol"); Reject(() => StreamDeckProtocol.Parse("{\"version\":1,\"action\":\"run-command\"}")); Reject(() => StreamDeckProtocol.Parse("{\"version\":1,\"action\":\"status\",\"extra\":true}")); Reject(() => StreamDeckProtocol.Parse("{\"version\":1,\"action\":\"show-settings\",\"extra\":true}")); Reject(() => StreamDeckProtocol.Parse("{\"version\":1,\"action\":\"activate-profile\",\"profile\":\"Gaming\",\"extra\":true}")); });
            Test("Network and named-pipe readers enforce limits during streaming", delegate {
                using (var accepted = new MemoryStream(Encoding.UTF8.GetBytes("ok"))) Assert(AtomicFile.ReadStreamTextBounded(accepted, 2, Encoding.UTF8) == "ok", "bounded stream content");
                using (var oversized = new MemoryStream(Encoding.UTF8.GetBytes("toolong"))) RejectOversizedRead(() => AtomicFile.ReadStreamTextBounded(oversized, 3, Encoding.UTF8));
                Assert(StreamDeckBridge.ReadBoundedLine(new StringReader(new string('x', 2048) + "\n"), 2048).Length == 2048, "exact pipe limit");
                bool lineRejected = false; try { StreamDeckBridge.ReadBoundedLine(new StringReader(new string('x', 2049) + "\n"), 2048); } catch (InvalidDataException) { lineRejected = true; }
                Assert(lineRejected, "oversized pipe line was buffered/accepted");
            });
            Test("PowerToys process output is capped while both pipes continue draining", delegate {
                string output = PowerToysIntegration.ReadProcessOutputBoundedAsync(new StringReader(new string('o', 10000)), 32).GetAwaiter().GetResult();
                Assert(output.StartsWith(new string('o', 32), StringComparison.Ordinal) && output.EndsWith("[PowerToys output truncated]", StringComparison.Ordinal) && output.Length < 80, "PowerToys output was retained without a cap or wasn't marked truncated");
                Assert(PowerToysIntegration.ReadProcessOutputBoundedAsync(new StringReader("normal"), 32).GetAwaiter().GetResult() == "normal", "ordinary PowerToys output changed");
                var pendingOutput = new TaskCompletionSource<string>(); var completeError = Task.FromResult(""); bool closed = false;
                bool timedOut = false; try { PowerToysIntegration.WaitForProcessOutput(pendingOutput.Task, completeError, 10, delegate { closed = true; pendingOutput.TrySetResult(""); }); } catch (TimeoutException) { timedOut = true; }
                Assert(timedOut && closed, "inherited output handle could leave the caller waiting forever");
                var completeOutput = Task.FromResult("output"); var completeStderr = Task.FromResult("error"); var captured = PowerToysIntegration.WaitForProcessOutput(completeOutput, completeStderr, 100, null);
                Assert(captured.Key == "output" && captured.Value == "error", "completed process output was not returned");
            });
            Test("Repeated crash guard offers Safe Mode without forcing it early", delegate { Assert(!StartupGuard.ShouldOfferRecovery(0) && !StartupGuard.ShouldOfferRecovery(1) && StartupGuard.ShouldOfferRecovery(2), "threshold"); Assert(StartupGuard.NextAttempts(0) == 1 && StartupGuard.NextAttempts(1) == 2 && StartupGuard.NextAttempts(99) == 4, "bounded attempts"); });
            Test("Startup guard persists crash attempts and clears them after a clean exit", delegate {
                string path = StartupGuard.Path;
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                    Assert(!StartupGuard.RecordStart() && File.ReadAllText(path, Encoding.ASCII) == "1", "first launch was counted incorrectly");
                    Assert(StartupGuard.RecordStart() && File.ReadAllText(path, Encoding.ASCII) == "2", "two repeated unclean launches did not offer Safe Mode");
                    Assert(StartupGuard.RecordStart() && File.ReadAllText(path, Encoding.ASCII) == "3", "recovery prompt was lost during another unclean launch");
                    File.WriteAllText(path, new string('9', 4096), Encoding.ASCII);
                    Assert(!StartupGuard.RecordStart() && File.ReadAllText(path, Encoding.ASCII) == "1", "oversized startup marker was not safely ignored");
                    StartupGuard.MarkClean();
                    Assert(!File.Exists(path), "clean exit did not clear crash-attempt state");
                }
                finally { StartupGuard.MarkClean(); }
            });
            Test("Audit trail stays redacted and bounded", delegate { string path = Path.Combine(scratch, "audit.log"); AuditTrail.Record(path, "configuration-save"); AuditTrail.Record(path, "configuration-import-staged"); AuditTrail.Record(path, "C:\\private\\secret.exe"); for (int i = 0; i < AuditTrail.MaxEntries + 8; i++) AuditTrail.Record(path, "event-" + (i % 10)); var lines = AuditTrail.Read(path); Assert(lines.Count == AuditTrail.MaxEntries && lines.All(x => !x.Contains("private") && !x.Contains("secret.exe")), "audit redaction/rotation"); });
            Test("Notification preferences migrate, save atomically, and preserve corrupt files", delegate {
                string path = Path.Combine(scratch, "notification-preferences.json");
                File.WriteAllText(path, "{\"version\":1,\"updates\":false,\"health\":true,\"safety\":false}");
                var legacy = NotificationPreferences.Load(path); Assert(!legacy.Updates && legacy.Health && !legacy.Safety && legacy.Severity == "All" && !legacy.LoadWarning, "v1 migration");
                var strict = new NotificationPreferences { Updates = true, Health = false, Safety = true, Severity = "Critical only" }; strict.Save(path);
                Assert(!NotificationPreferences.Load(path).AllowsWarning && NotificationPreferences.Load(path).Severity == "Critical only", "critical severity");
                var replacement = new NotificationPreferences { Updates = false, Health = false, Safety = false, Severity = "Warnings and above" }; replacement.Save(path);
                Assert(!NotificationPreferences.Load(path).Health && NotificationPreferences.Load(path + ".bak").Severity == "Critical only" && Directory.GetFiles(scratch, ".notification-preferences.json.*.tmp").Length == 0, "atomic replacement backup/cleanup");
                byte[] corrupt = Encoding.UTF8.GetBytes("{\"version\":2,\"updates\":true,\"updates\":false,\"health\":true,\"safety\":true,\"severity\":\"All\"}"); File.WriteAllBytes(path, corrupt);
                var safe = NotificationPreferences.Load(path);
                Assert(safe.LoadWarning && safe.Severity == "Critical only" && !safe.AllowsWarning && File.ReadAllBytes(path).SequenceEqual(corrupt), "corrupt notification settings did not fail closed/preserve source");
                File.WriteAllText(path, new string('x', 4097)); Assert(NotificationPreferences.Load(path).LoadWarning, "oversized notification settings accepted");
            });
            Test("Update channel persistence is strict, atomic, and backward compatible", delegate {
                string path = Path.Combine(scratch, "update-channel.json");
                Assert(UpdateChannels.Load(path) == "Stable", "missing channel default");
                File.WriteAllText(path, "{\"channel\":\"Beta\"}"); Assert(UpdateChannels.Load(path) == "Beta", "legacy channel migration");
                UpdateChannels.Save(path, "Alpha"); Assert(UpdateChannels.Load(path) == "Alpha", "versioned channel save");
                UpdateChannels.Save(path, "Stable"); Assert(UpdateChannels.Load(path + ".bak") == "Alpha" && Directory.GetFiles(scratch, ".update-channel.json.*.tmp").Length == 0, "channel backup/cleanup");
                byte[] corrupt = Encoding.UTF8.GetBytes("{\"version\":1,\"channel\":\"Alpha\",\"channel\":\"Stable\"}"); File.WriteAllBytes(path, corrupt);
                Assert(UpdateChannels.Load(path) == "Stable" && File.ReadAllBytes(path).SequenceEqual(corrupt), "invalid channel did not fall back while preserving source");
            });
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
                var reservedMerge = ProfileCollection.Merge(collection, new ProfileCollection { Profiles = new[] { new KeyWeaveProfile { Name = "Default", Configuration = new Configuration() } } }); Assert(reservedMerge.Find("Default") == null && reservedMerge.Find("Default (2)") != null, "reserved base profile is renamed on merge");
                Reject(() => ProfileStore.Validate(new ProfileCollection { Profiles = new[] { new KeyWeaveProfile { Name = "Default" } } }, false));
                Reject(() => ProfileStore.Validate(new ProfileCollection { Profiles = new[] { new KeyWeaveProfile { Name = " default " } } }, false));
            });
            Test("Profile inheritance resolves overrides and rejects loops", delegate {
                string monitorId = new string('a', 64);
                var defaults = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+B", Action = new Mapping { Kind = ActionKind.Monitor, MonitorId = monitorId, MonitorControl = "BacklightOn", MonitorStep = 5 } } } }; defaults.Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" };
                var workEffective = defaults.Copy(); workEffective.Mappings[1] = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" };
                workEffective.CustomHotkeys[0].Action.MonitorControl = "BacklightOff";
                var work = new KeyWeaveProfile { Name = "Work", InheritFrom = "Default", Configuration = workEffective.Copy() };
                var childEffective = workEffective.Copy(); childEffective.Mappings[2] = new Mapping { Kind = ActionKind.Media, Target = "MediaPlayPause" };
                var child = new KeyWeaveProfile { Name = "Editing", InheritFrom = "Work", Configuration = childEffective.Copy() };
                var collection = new ProfileCollection { Profiles = new[] { work, child } };
                ProfileStore.SetEffectiveConfiguration(collection, work, workEffective, defaults); ProfileStore.SetEffectiveConfiguration(collection, child, childEffective, defaults);
                Assert(work.OverrideKeys.SequenceEqual(new[] { "F2", "CustomHotkeys" }) && child.OverrideKeys.SequenceEqual(new[] { "F3" }), "intentional overrides including custom hotkeys");
                defaults.Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeDown" };
                var resolved = collection.Resolve("Editing", defaults); Assert(resolved.Mappings[0].Target == "VolumeDown" && resolved.Mappings[1].Target == "VolumeMute" && resolved.Mappings[2].Target == "MediaPlayPause" && resolved.CustomHotkeys[0].Action.MonitorControl == "BacklightOff", "inheritance resolution");
                string json = ProfileStore.Serialize(collection); var loaded = ProfileStore.Parse(json); Assert(json.Contains("\"version\":3") && loaded.Resolve("Editing", defaults).Mappings[0].Target == "VolumeDown" && loaded.Resolve("Editing", defaults).CustomHotkeys[0].Action.MonitorControl == "BacklightOff", "inheritance round trip");
                loaded.Find("Work").InheritFrom = "Editing"; Reject(() => ProfileStore.Validate(loaded, false));
            });
            Test("Switching profiles updates custom-hotkey runtime actions", delegate {
                var defaults = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+K", Action = new Mapping { Kind = ActionKind.SendKey, Target = "F6" } } } };
                var effective = defaults.Copy(); effective.CustomHotkeys[0].Shortcut = HotkeyChord.NormalizeCustomHotkeyReplacement(effective, 0, "Ctrl+Alt+G"); effective.CustomHotkeys[0].Action.Target = "F9";
                var profile = new KeyWeaveProfile { Name = "Gaming", InheritFrom = "Default", Configuration = effective.Copy() };
                var profiles = new ProfileCollection { Profiles = new[] { profile } };
                ProfileStore.SetEffectiveConfiguration(profiles, profile, effective, defaults);
                var persisted = ProfileStore.Parse(ProfileStore.Serialize(profiles));
                var resolved = persisted.Resolve("Gaming", defaults);
                Assert(profile.OverrideKeys.Contains("CustomHotkeys") && resolved.CustomHotkeys[0].Shortcut == "Ctrl+Alt+G", "profile chord override did not survive serialization and resolution");
                using (var sink = new SignalingSink()) using (var engine = new KeyboardEngine(sink, false)) {
                    engine.Apply(defaults); Assert(engine.TriggerCustomHotkey("Ctrl+Alt+K") && sink.Changed.WaitOne(1000), "default custom action did not dispatch");
                    engine.Apply(resolved); Assert(!engine.TriggerCustomHotkey("Ctrl+Alt+K"), "old default chord remained active in the profile"); Assert(engine.TriggerCustomHotkey("Ctrl+Alt+G") && sink.Changed.WaitOne(1000), "rebound profile custom action did not dispatch");
                    var actions = sink.Snapshot();
                    Assert(actions.Length == 2 && actions[0].SequenceEqual(new[] { (int)Keys.F6 }) && actions[1].SequenceEqual(new[] { (int)Keys.F9 }), "profile switch kept the old custom action instead of applying the profile override");
                    engine.Apply(defaults); Assert(engine.TriggerCustomHotkey("Ctrl+Alt+K") && sink.Changed.WaitOne(1000) && !engine.TriggerCustomHotkey("Ctrl+Alt+G"), "switching back did not restore only the default chord");
                }
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
                var c = new Configuration { Enabled = true, CustomHotkeys = new[] {
                    new CustomHotkey { Shortcut = "Ctrl+Alt+B", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+P", Action = new Mapping { Kind = ActionKind.PassThrough } },
                    new CustomHotkey { Shortcut = "F13", Action = new Mapping { Kind = ActionKind.Unbound } }
                } };
                var p = new ProfileCollection { Profiles = new[] { new KeyWeaveProfile { Name = "Games", Applications = new[] { "game.exe" }, Configuration = c.Copy() } } };
                string text = BackupBundle.Serialize(c, p, new UserPreferences { UseTray = false, NetworkAccess = true }, true); var loaded = BackupBundle.Parse(text);
                ActionKind[] noOpKinds = { ActionKind.PassThrough, ActionKind.Unbound };
                Assert(loaded.Configuration.Enabled && loaded.Configuration.CustomHotkeys.Length == 3 && loaded.Configuration.CustomHotkeys.Skip(1).Select(h => h.Action.Kind).SequenceEqual(noOpKinds) && loaded.Profiles.Find("Games") != null && loaded.Profiles.Find("Games").Configuration.CustomHotkeys.Skip(1).Select(h => h.Action.Kind).SequenceEqual(noOpKinds) && !loaded.Preferences.UseTray && loaded.Preferences.NetworkAccess && loaded.StartWithWindows, "backup/profile round trip lost custom no-op bindings");
                string path = Path.Combine(scratch, "setup.keyweave"); BackupBundle.Save(path, c, p, new UserPreferences { UseTray = false }, true); Assert(BackupBundle.Load(path).Profiles.Find("Games") != null, "backup file load");
                BackupBundle.Save(path, new Configuration(), new ProfileCollection(), new UserPreferences(), false); Assert(File.Exists(path + ".bak") && BackupBundle.Load(path + ".bak").Configuration.Enabled, "backup atomic replacement");
                Reject(() => BackupBundle.Parse(text.Replace("\"format\":\"KeyWeave Backup\"", "\"unknown\":1,\"format\":\"KeyWeave Backup\"")));
                Reject(() => BackupBundle.Parse(text.Replace("\"version\":1", "\"version\":1,\"version\":1")));
                Reject(() => BackupBundle.Parse(text.Replace(loaded.CreatedUtc.ToString("o"), "not-a-date")));
                Reject(() => BackupBundle.Parse(new string('x', BackupBundle.MaxBytes + 1)));
            });
            Test("Backup restore rollback returns each snapshot to its own file", delegate {
                string folder = Path.Combine(scratch, "restore-rollback"); Directory.CreateDirectory(folder);
                string config = Path.Combine(folder, "active.kiweave"), profiles = Path.Combine(folder, "active-profiles.json"), preferences = Path.Combine(folder, "active-preferences.json");
                File.WriteAllText(Path.Combine(folder, "config.kiweave"), "original config"); File.WriteAllText(Path.Combine(folder, "profiles.json"), "original profiles"); File.WriteAllText(Path.Combine(folder, "preferences.json"), "original preferences");
                File.WriteAllText(config, "partial config"); File.WriteAllText(profiles, "partial profiles"); File.WriteAllText(preferences, "partial preferences");
                BackupBundle.RestoreSnapshots(folder, config, profiles, preferences);
                Assert(File.ReadAllText(config) == "original config" && File.ReadAllText(profiles) == "original profiles" && File.ReadAllText(preferences) == "original preferences", "three independent rollback destinations");
                File.Delete(Path.Combine(folder, "profiles.json")); File.WriteAllBytes(Path.Combine(folder, "profiles.json.missing"), new byte[0]);
                BackupBundle.RestoreSnapshots(folder, config, profiles, preferences);
                Assert(!File.Exists(profiles) && File.ReadAllText(config) == "original config" && File.ReadAllText(preferences) == "original preferences", "previously absent profile file is removed without clobbering config");
                File.WriteAllText(Path.Combine(folder, "profiles.json"), "original profiles"); File.Delete(Path.Combine(folder, "profiles.json.missing"));
                File.WriteAllText(profiles, "partial profiles"); File.Delete(config); Directory.CreateDirectory(config);
                var failures = BackupBundle.RestoreSnapshots(folder, config, profiles, preferences);
                Assert(failures.Count == 1 && File.ReadAllText(profiles) == "original profiles" && File.ReadAllText(preferences) == "original preferences", "rollback continues after one target fails");
                Directory.Delete(config);
            });
            Test("Configuration history is bounded, local and compares without exposing targets", delegate {
                string folder = Path.Combine(scratch, "history"); var profiles = new ProfileCollection(); var preferences = new UserPreferences();
                var first = new Configuration(); first.Mappings[0] = new Mapping { Kind = ActionKind.Application, Target = @"C:\private\secret.exe" };
                ConfigurationHistory.Capture(folder, "mapping save", first, profiles, preferences, false, 2);
                Assert(Directory.GetFiles(folder, "*.kiweave").Length == 1, "new history snapshots use the KiWeave extension");
                var second = first.Copy(); second.Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" }; ConfigurationHistory.Capture(folder, "profile save", second, profiles, preferences, false, 2);
                var third = second.Copy(); third.Mappings[1] = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+S" }; ConfigurationHistory.Capture(folder, "backup restore", third, profiles, preferences, true, 2);
                var entries = ConfigurationHistory.List(folder); Assert(entries.Count == 2 && entries.All(x => x.Backup.PowerToys.Length == 0), "bounded/private history");
                string comparison = ConfigurationHistory.Compare(new KeyWeaveBackup { Configuration = first, Profiles = profiles, Preferences = preferences, StartWithWindows = false }, third, profiles, preferences, true);
                Assert(comparison.Contains("F1") && comparison.Contains("Start with Windows") && !comparison.Contains("private") && !comparison.Contains("secret.exe"), "safe comparison");
                var before = new Configuration { CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+K", Action = new Mapping { Kind = ActionKind.Application, Target = @"C:\private\custom-secret.exe" } }, }, Layers = new[] { new ModifierLayer { Name = "Media", ActivationKey = "CapsLock" } } };
                before.Layers[0].Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeUp" };
                var after = before.Copy(); after.CustomHotkeys[0].Action.Target = @"C:\private\other-secret.exe"; after.Layers[0].Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" };
                var beforeProfiles = new ProfileCollection { Profiles = new[] { new KeyWeaveProfile { Name = "Gaming", Configuration = new Configuration() } } };
                var afterProfiles = beforeProfiles.Copy(); afterProfiles.Profiles[0].Configuration.Mappings[0] = new Mapping { Kind = ActionKind.Media, Target = "VolumeDown" };
                var afterPreferences = new UserPreferences { CheckUpdates = false, Theme = "Plum", CustomAccent = "#123456", SmoothScrolling = true, ToastStyle = "KiWeave" };
                string recoveryComparison = ConfigurationHistory.Compare(new KeyWeaveBackup { Configuration = before, Profiles = beforeProfiles, Preferences = preferences }, after, afterProfiles, afterPreferences, false);
                Assert(recoveryComparison.Contains("Custom hotkeys: shortcuts or actions changed") && recoveryComparison.Contains("Modifier layers: activation keys or mappings changed") && recoveryComparison.Contains("Profiles: rules or mappings changed") && recoveryComparison.Contains("Update checks") && recoveryComparison.Contains("Theme or accent") && recoveryComparison.Contains("Smooth scrolling") && recoveryComparison.Contains("Toast style") && !recoveryComparison.Contains("custom-secret.exe") && !recoveryComparison.Contains("other-secret.exe"), "recovery comparison must surface same-count edits across settings without exposing private action targets");
                string legacyFolder = Path.Combine(scratch, "legacy-history"); Directory.CreateDirectory(legacyFolder); BackupBundle.Save(Path.Combine(legacyFolder, "old.keyweave"), first, profiles, preferences, false, false);
                Assert(ConfigurationHistory.List(legacyFolder).Count == 1, "legacy history remains readable after extension change");
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
                byte[] oversizedProfile = Encoding.ASCII.GetBytes(new string('x', 257)); File.WriteAllBytes(meta, oversizedProfile);
                bool rejectedOversizedProfile = false; try { RecoveryStore.TryLoadDraft(path, meta, out recovered, out profile); } catch (InvalidDataException) { rejectedOversizedProfile = true; }
                Assert(rejectedOversizedProfile && File.ReadAllBytes(meta).SequenceEqual(oversizedProfile), "oversized recovery metadata was accepted or modified");
                var same = new KeyWeaveBackup { Configuration = defaults.Copy(), Profiles = profiles.Copy(), Preferences = new UserPreferences { NetworkAccess = false }, StartWithWindows = false };
                Assert(RecoveryStore.MatchesCurrent(same, defaults, profiles, same.Preferences, false), "identical recovery draft recognized as stale");
                same.Configuration.Mappings[0] = new Mapping { Kind = ActionKind.SendKey, Target = "A" };
                Assert(!RecoveryStore.MatchesCurrent(same, defaults, profiles, same.Preferences, false), "changed recovery draft retained");
            });
            Test("History notes are bounded and oversized notes remain untouched", delegate {
                string history = Path.Combine(scratch, "note-history.kiweave"), path = ConfigurationHistory.NotePath(history); byte[] oversized = Encoding.UTF8.GetBytes(new string('x', 4097)); File.WriteAllBytes(path, oversized);
                Assert(ConfigurationHistory.LoadNote(history) == "" && File.ReadAllBytes(path).SequenceEqual(oversized), "oversized history note was loaded or rewritten");
            });
            Test("KiWeave data migration copies legacy state without overwriting newer files", delegate {
                string legacy = Path.Combine(scratch, "legacy-data"), current = Path.Combine(scratch, "kiweave-data"); Directory.CreateDirectory(legacy); Directory.CreateDirectory(current);
                File.WriteAllText(Path.Combine(legacy, "config.json"), "legacy"); File.WriteAllText(Path.Combine(legacy, "preferences.json"), "legacy prefs"); File.WriteAllText(Path.Combine(current, "preferences.json"), "new prefs");
                File.WriteAllText(Path.Combine(legacy, "language.json"), "legacy language"); File.WriteAllText(Path.Combine(legacy, "language.json.bak"), "legacy language backup"); File.WriteAllText(Path.Combine(current, "language.json"), "new language");
                Directory.CreateDirectory(Path.Combine(legacy, "History")); File.WriteAllText(Path.Combine(legacy, "History", "one.keyweave"), "history");
                AppStorage.MigrateLegacy(legacy, current);
                Assert(File.ReadAllText(Path.Combine(current, "config.kiweave")) == "legacy", "configuration not migrated to .kiweave");
                Assert(File.ReadAllText(Path.Combine(current, "config.json")) == "legacy", "legacy configuration was not preserved");
                Assert(File.ReadAllText(Path.Combine(current, "preferences.json")) == "new prefs", "newer preferences overwritten");
                Assert(File.ReadAllText(Path.Combine(current, "language.json")) == "new language" && File.ReadAllText(Path.Combine(current, "language.json.bak")) == "legacy language backup", "newer language preference overwritten or its recovery backup was not migrated");
                Assert(File.Exists(Path.Combine(current, "History", "one.keyweave")) && File.Exists(Path.Combine(current, ".migrated-from-function-row-remapper")), "folders/marker missing");
            });
            Test("KiWeave data migration skips reparse points and destination-inside-source copies", delegate {
                string source = Path.Combine(scratch, "migration-reparse-source"), destination = Path.Combine(scratch, "migration-reparse-destination"), outside = Path.Combine(scratch, "migration-reparse-outside");
                Directory.CreateDirectory(Path.Combine(source, "History", "ordinary")); Directory.CreateDirectory(outside);
                File.WriteAllText(Path.Combine(source, "History", "ordinary", "keep.txt"), "safe"); File.WriteAllText(Path.Combine(outside, "private.txt"), "outside"); File.WriteAllText(Path.Combine(outside, "external.kiweave"), "outside backup");
                string junction = Path.Combine(source, "History", "outside-link");
                using (var createLink = Process.Start(new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + junction + "\" \"" + outside + "\"") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
                {
                    Assert(createLink != null && createLink.WaitForExit(10000) && createLink.ExitCode == 0, "temporary junction could not be created");
                }
                try
                {
                    AppStorage.MigrateLegacy(source, destination);
                    Assert(File.ReadAllText(Path.Combine(destination, "History", "ordinary", "keep.txt")) == "safe", "ordinary nested files did not migrate");
                    Assert(!File.Exists(Path.Combine(destination, "History", "outside-link", "private.txt")), "migration followed a directory junction outside the data root");
                    var scanned = AppStorage.EnumerateFilesWithoutFollowingReparsePoints(Path.Combine(source, "History"), "*.kiweave").ToArray();
                    Assert(scanned.Length == 0 && !BackupReminder.Inspect(Path.Combine(source, "History"), DateTime.UtcNow, null, delegate(string path) { BackupBundle.Load(path); return true; }).HasBackup, "backup or privacy scan followed a directory junction to count outside files");
                    Assert(AppStorage.EnumerateDirectFilesWithoutFollowingReparsePoints(junction, "*.kiweave").Count() == 0 && ConfigurationHistory.List(junction).Count == 0, "history listing followed a junction at its root");
                    bool unsafeHistoryRejected = false;
                    try { ConfigurationHistory.Capture(junction, "unsafe path", new Configuration(), new ProfileCollection(), new UserPreferences(), false, 20); }
                    catch (IOException) { unsafeHistoryRejected = true; }
                    Assert(unsafeHistoryRejected && File.ReadAllText(Path.Combine(outside, "external.kiweave")) == "outside backup", "history capture wrote through a junction");
                    string profileSnapshots = Path.Combine(ProfileSnapshots.Root, "junction-profile");
                    Directory.CreateDirectory(Path.GetDirectoryName(profileSnapshots));
                    using (var createProfileLink = Process.Start(new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + profileSnapshots + "\" \"" + outside + "\"") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
                    {
                        Assert(createProfileLink != null && createProfileLink.WaitForExit(10000) && createProfileLink.ExitCode == 0, "profile snapshot junction could not be created");
                    }
                    try
                    {
                        bool unsafeProfileSnapshotRejected = false;
                        try { ProfileSnapshots.Save(new KeyWeaveProfile { Name = "junction-profile", Configuration = new Configuration() }, new ProfileCollection()); }
                        catch (IOException) { unsafeProfileSnapshotRejected = true; }
                        Assert(unsafeProfileSnapshotRejected && File.ReadAllText(Path.Combine(outside, "external.kiweave")) == "outside backup", "profile snapshot wrote through a junction");
                    }
                    finally { try { Directory.Delete(profileSnapshots, false); } catch { } }
                    bool unsafeAncestorRejected = false;
                    try { AppStorage.EnsureNoReparsePointInPath(Path.Combine(junction, "new-history-folder")); }
                    catch (IOException) { unsafeAncestorRejected = true; }
                    Assert(unsafeAncestorRejected, "history path validation ignored a reparse-point ancestor");
                    string nestedDestination = Path.Combine(source, "History", "nested-destination");
                    AppStorage.MigrateLegacy(source, nestedDestination);
                    Assert(!File.Exists(Path.Combine(nestedDestination, "History", "ordinary", "keep.txt")), "migration recursed into a destination inside its own source");
                }
                finally { try { Directory.Delete(junction, false); } catch { } }
            });
            Test("Existing config.json migrates to .kiweave and remains recoverable", delegate {
                string folder = Path.Combine(scratch, "extension-migration"); Directory.CreateDirectory(folder);
                string oldPath = Path.Combine(folder, "config.json"), newPath = Path.Combine(folder, "config.kiweave");
                File.WriteAllText(oldPath, "old configuration"); AppStorage.MigrateLegacy(folder, folder);
                Assert(File.ReadAllText(newPath) == "old configuration" && File.Exists(oldPath), "migration did not preserve both files");
                File.WriteAllText(newPath, "new configuration"); AppStorage.MigrateLegacy(folder, folder);
                Assert(File.ReadAllText(newPath) == "new configuration", "migration overwrote the newer native config");
            });
            Test("Private-safe log excludes exception messages and rotates", delegate {
                string path = Path.Combine(scratch, "keyweave.log"), secret = "https://example.invalid/private-token";
                AppLog.Write(path, "Test component", new InvalidOperationException(secret)); string text = File.ReadAllText(path);
                Assert(text.Contains("Test component") && text.Contains("InvalidOperationException") && !text.Contains(secret), "private message leaked");
            });
            Test("Master network policy blocks HTTP before a request", delegate {
                bool previous = NetworkPolicy.Enabled, blocked = false;
                try { NetworkPolicy.Enabled = false; try { SystemActions.HttpRequest(new Mapping { Kind = ActionKind.HttpRequest, Target = "http://127.0.0.1:1/private" }); } catch (InvalidOperationException ex) { blocked = ex.Message.Contains("turned off"); }
                    Assert(blocked, "network action was not blocked"); }
                finally { NetworkPolicy.Enabled = previous; }
            });
            Test("Turning off the master network switch cancels active request tokens", delegate {
                bool previous = NetworkPolicy.Enabled;
                try {
                    NetworkPolicy.Enabled = true; var token = NetworkPolicy.BeginRequest(); NetworkPolicy.Enabled = false;
                    Assert(token.IsCancellationRequested, "active request was not cancelled");
                    bool blocked = false; try { NetworkPolicy.BeginRequest(); } catch (InvalidOperationException) { blocked = true; }
                    Assert(blocked, "new request started while network access was disabled");
                }
                finally { NetworkPolicy.Enabled = previous; }
            });
            Test("Turning off the master network switch aborts an active HTTP action", delegate {
                bool previous = NetworkPolicy.Enabled; Exception requestError = null, serverError = null;
                using (var requestReceived = new ManualResetEvent(false)) using (var finishServer = new ManualResetEvent(false)) {
                    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    var server = new Thread(new ThreadStart(delegate {
                        try { using (var client = listener.AcceptTcpClient()) using (var reader = new StreamReader(client.GetStream())) { string line; while (!String.IsNullOrEmpty(line = reader.ReadLine())) { } requestReceived.Set(); finishServer.WaitOne(3000); } }
                        catch (Exception ex) { serverError = ex; }
                    })); server.IsBackground = true; server.Start();
                    try {
                        NetworkPolicy.Enabled = true;
                        var action = Task.Run(delegate { try { SystemActions.HttpRequest(new Mapping { Kind = ActionKind.HttpRequest, Target = "http://127.0.0.1:" + port + "/cancel" }); } catch (Exception ex) { requestError = ex; } });
                        Assert(requestReceived.WaitOne(3000), "local HTTP action did not reach the test endpoint");
                        NetworkPolicy.Enabled = false;
                        Assert(action.Wait(2000), "HTTP action remained blocked after network access was disabled");
                        Assert(requestError is WebException || requestError is OperationCanceledException, "aborted HTTP action reported an unexpected result");
                    }
                    finally { finishServer.Set(); listener.Stop(); server.Join(3000); NetworkPolicy.Enabled = previous; }
                    if (serverError != null) throw serverError;
                }
            });
            Test("Disabling shortcuts aborts an active custom HTTP action", delegate {
                bool previous = NetworkPolicy.Enabled; Exception serverError = null; string actionError = "";
                using (var requestReceived = new ManualResetEvent(false)) using (var finishServer = new ManualResetEvent(false)) using (var actionAborted = new ManualResetEvent(false)) {
                    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    var server = new Thread(new ThreadStart(delegate {
                        try { using (var client = listener.AcceptTcpClient()) using (var reader = new StreamReader(client.GetStream())) { string line; while (!String.IsNullOrEmpty(line = reader.ReadLine())) { } requestReceived.Set(); finishServer.WaitOne(3000); } }
                        catch (Exception ex) { serverError = ex; }
                    })); server.IsBackground = true; server.Start();
                    try {
                        NetworkPolicy.Enabled = true;
                        var mapping = new Mapping { Kind = ActionKind.HttpRequest, Target = "http://127.0.0.1:" + port + "/shortcut-cancel" };
                        using (var engine = new KeyboardEngine(new FakeSink(), true)) {
                            engine.Error += message => { actionError = message; actionAborted.Set(); };
                            engine.Apply(new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+H", Action = mapping } } });
                            Assert(engine.TriggerCustomHotkey("Ctrl+Alt+H"), "custom HTTP trigger was not queued");
                            Assert(requestReceived.WaitOne(3000), "custom HTTP action did not reach the local endpoint");
                            engine.SetEnabled(false);
                            Assert(actionAborted.WaitOne(2000), "custom HTTP request continued after shortcuts were disabled");
                            Assert(actionError.Length > 0, "cancelled custom HTTP action did not report its abort");
                        }
                    }
                    finally { finishServer.Set(); listener.Stop(); server.Join(3000); NetworkPolicy.Enabled = previous; }
                    if (serverError != null) throw serverError;
                }
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
            Test("Integration actions share extension-gating routes with custom bindings", delegate {
                var extensionIds = FirstPartyExtensionCatalog.Items.Select(item => item.Id).ToArray();
                Assert(extensionIds.Length == 6 && extensionIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() == extensionIds.Length,
                    "first-party extension IDs are missing or duplicated");
                var extensionView = FirstPartyExtensionCatalog.Items as IList<FirstPartyExtension>;
                Assert(extensionView != null && extensionView.IsReadOnly, "extension catalog exposes a mutable item array");
                Assert(FirstPartyExtensionCatalog.Items.All(item => !String.IsNullOrWhiteSpace(item.Name) && !String.IsNullOrWhiteSpace(item.Description) && !String.IsNullOrWhiteSpace(item.Permissions) && !String.IsNullOrWhiteSpace(item.Maturity)),
                    "an extension is missing shared metadata used by the UI");
                foreach (string action in SystemActions.Names) {
                    string expected = action.StartsWith("Discord", StringComparison.OrdinalIgnoreCase) ? "discord"
                        : action.StartsWith("Spotify", StringComparison.OrdinalIgnoreCase) ? "spotify"
                        : action.StartsWith("Obs", StringComparison.OrdinalIgnoreCase) ? "obs"
                        : String.Equals(action, "OpenPowerToys", StringComparison.OrdinalIgnoreCase) ? "powertoys" : null;
                    Assert(String.Equals(FirstPartyExtensionCatalog.ActionExtension(action), expected, StringComparison.OrdinalIgnoreCase), "extension route mismatch for " + action);
                }
                bool wasEnabled = FirstPartyExtensionCatalog.IsEnabled("spotify");
                try {
                    FirstPartyExtensionCatalog.SetEnabled("spotify", false);
                    var sink = new FakeSink();
                    using (var errors = new CountdownEvent(2)) using (var engine = new KeyboardEngine(sink, true)) {
                        engine.Error += message => { if (message.IndexOf("spotify first-party extension is disabled", StringComparison.OrdinalIgnoreCase) >= 0) errors.Signal(); };
                        var config = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+P", Action = new Mapping { Kind = ActionKind.SystemAction, Target = "SpotifyPlayPause" } } } };
                        config.Mappings[0] = new Mapping { Kind = ActionKind.SystemAction, Target = "SpotifyPlayPause" };
                        engine.Apply(config);
                        Assert(engine.TriggerFunctionKey(1), "disabled integration function binding was not queued");
                        Assert(engine.TriggerCustomHotkey("Ctrl+Alt+P"), "disabled integration custom binding was not queued");
                        Assert(errors.Wait(2000), "disabled extension actions did not report their failure");
                        Assert(sink.Keys.Count == 0 && sink.Launches.Count == 0, "disabled extension mapping still performed an action");
                    }
                } finally { FirstPartyExtensionCatalog.SetEnabled("spotify", wasEnabled); }
            });
            Test("Every integration custom binding honors its extension toggle", delegate {
                string[] extensionIds = { "discord", "spotify", "obs", "powertoys" };
                var prior = extensionIds.ToDictionary(id => id, id => FirstPartyExtensionCatalog.IsEnabled(id), StringComparer.OrdinalIgnoreCase);
                var actions = SystemActions.Names.Where(name => FirstPartyExtensionCatalog.ActionExtension(name) != null).ToArray();
                try {
                    foreach (string id in extensionIds) FirstPartyExtensionCatalog.SetEnabled(id, false);
                    var hotkeys = actions.Select((name, index) => new CustomHotkey {
                        Shortcut = "Ctrl+Alt+F" + (index + 1),
                        Action = new Mapping { Kind = ActionKind.SystemAction, Target = name }
                    }).ToArray();
                    var configuration = new Configuration { Enabled = true, CustomHotkeys = hotkeys };
                    var sink = new FakeSink();
                    using (var blocked = new CountdownEvent(actions.Length)) using (var engine = new KeyboardEngine(sink, true)) {
                        engine.Error += message => { if (message.IndexOf("first-party extension is disabled", StringComparison.OrdinalIgnoreCase) >= 0) blocked.Signal(); };
                        engine.Apply(configuration);
                        foreach (var hotkey in hotkeys) Assert(engine.TriggerCustomHotkey(hotkey.Shortcut), "integration custom binding did not queue: " + hotkey.Action.Target);
                        Assert(blocked.Wait(3000), "not every disabled integration custom binding was safely blocked");
                        Assert(sink.Keys.Count == 0 && sink.Launches.Count == 0, "disabled integration custom binding reached an external action sink");
                    }
                } finally {
                    foreach (string id in extensionIds) FirstPartyExtensionCatalog.SetEnabled(id, prior[id]);
                }
            });
            Test("Custom Spotify playback bindings route their media keys through the shared engine", delegate {
                bool wasEnabled = FirstPartyExtensionCatalog.IsEnabled("spotify");
                try {
                    FirstPartyExtensionCatalog.SetEnabled("spotify", true);
                    var hotkeys = new[] {
                        new CustomHotkey { Shortcut = "Ctrl+Alt+N", Action = new Mapping { Kind = ActionKind.SystemAction, Target = "SpotifyNext" } },
                        new CustomHotkey { Shortcut = "Ctrl+Alt+P", Action = new Mapping { Kind = ActionKind.SystemAction, Target = "SpotifyPrevious" } },
                        new CustomHotkey { Shortcut = "Ctrl+Alt+Space", Action = new Mapping { Kind = ActionKind.SystemAction, Target = "SpotifyPlayPause" } }
                    };
                    using (var sink = new SignalingSink()) using (var engine = new KeyboardEngine(sink, true)) {
                        engine.Apply(new Configuration { Enabled = true, CustomHotkeys = hotkeys });
                        foreach (var hotkey in hotkeys) Assert(engine.TriggerCustomHotkey(hotkey.Shortcut), "custom Spotify playback action was not queued: " + hotkey.Action.Target);
                        WaitFor(() => sink.Snapshot().Length == hotkeys.Length);
                        int[][] keys = sink.Snapshot();
                        Assert(keys[0].SequenceEqual(Shortcuts.Parse("MediaNextTrack", true)) &&
                            keys[1].SequenceEqual(Shortcuts.Parse("MediaPreviousTrack", true)) &&
                            keys[2].SequenceEqual(Shortcuts.Parse("MediaPlayPause", true)),
                            "custom Spotify playback bindings did not route the expected Windows media keys");
                    }
                } finally { FirstPartyExtensionCatalog.SetEnabled("spotify", wasEnabled); }
            });
            Test("Extension toggles move cards between installed and featured immediately", delegate {
                bool wasEnabled = FirstPartyExtensionCatalog.IsEnabled("streamdeck");
                try {
                    using (var form = new FirstPartyExtensionsForm(null)) {
                        form.StartPosition = FormStartPosition.Manual; form.Location = new System.Drawing.Point(-20000, -20000); form.Show(); Application.DoEvents();
                        var installed = (FlowLayoutPanel)typeof(FirstPartyExtensionsForm).GetField("installed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
                        var featured = (FlowLayoutPanel)typeof(FirstPartyExtensionsForm).GetField("featured", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
                        var originalSection = wasEnabled ? installed : featured; var nextSection = wasEnabled ? featured : installed;
                        Control card = originalSection.Controls.Cast<Control>().Single(candidate => Descendants(candidate).OfType<Label>().Any(label => label.Text == "Stream Deck"));
                        Button toggle = Descendants(card).OfType<Button>().First(button => button.Text == (wasEnabled ? "Disable" : "Enable"));
                        toggle.PerformClick();
                        Assert(card.IsDisposed, "replaced extension card was not disposed");
                        Assert(!originalSection.Controls.Cast<Control>().Any(candidate => Descendants(candidate).OfType<Label>().Any(label => label.Text == "Stream Deck")) && nextSection.Controls.Cast<Control>().Any(candidate => Descendants(candidate).OfType<Label>().Any(label => label.Text == "Stream Deck")), "extension card stayed in its old section after toggling");
                    }
                } finally { FirstPartyExtensionCatalog.SetEnabled("streamdeck", wasEnabled); }
            });
            Test("Custom hotkeys execute sequences and conditionals through the shared dispatcher", delegate {
                string process = Process.GetCurrentProcess().ProcessName + ".exe";
                var rule = new ConditionalRule { Condition = ConditionKind.ApplicationRunning, Application = process, WhenMatched = new Mapping { Kind = ActionKind.SendKey, Target = "F8" }, Otherwise = new Mapping { Kind = ActionKind.SendKey, Target = "F9" } };
                var sequence = new Mapping { Kind = ActionKind.Sequence, Target = SequenceCodec.Serialize(new[] {
                    new SequenceStep { Action = new Mapping { Kind = ActionKind.SendKey, Target = "F6" } },
                    new SequenceStep { Action = new Mapping { Kind = ActionKind.Conditional, Target = ConditionalCodec.Serialize(rule) } },
                    new SequenceStep { Action = new Mapping { Kind = ActionKind.SendKey, Target = "F7" } }
                }) };
                using (var sink = new SignalingSink()) using (var engine = new KeyboardEngine(sink, true)) {
                    engine.Apply(new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+Q", Action = sequence } } });
                    Assert(engine.TriggerCustomHotkey("Ctrl+Alt+Q"), "custom sequence was not queued");
                    WaitFor(() => sink.Snapshot().Length == 3);
                    int[][] keys = sink.Snapshot();
                    Assert(keys[0].SequenceEqual(new[] { (int)Keys.F6 }) && keys[1].SequenceEqual(new[] { (int)Keys.F8 }) && keys[2].SequenceEqual(new[] { (int)Keys.F7 }), "custom sequence/conditional did not preserve ordered dispatch");
                }
            });
            Test("Custom hotkeys dispatch every legacy launch action through the shared runtime queue", delegate {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                string file = Path.Combine(scratch, "custom-launch-file.txt"), shortcut = Path.Combine(scratch, "custom-launch-shortcut.lnk"), script = Path.Combine(scratch, "custom-launch-script.py");
                File.WriteAllText(file, "file target"); File.WriteAllText(shortcut, "placeholder; launch is captured by the fake sink"); File.WriteAllText(script, "print('captured; not executed')");
                var hotkeys = new[] {
                    new CustomHotkey { Shortcut = "Ctrl+Alt+A", Action = new Mapping { Kind = ActionKind.Application, Target = exe, Arguments = "--from-custom", WorkingDirectory = scratch } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+F", Action = new Mapping { Kind = ActionKind.FileOrFolder, Target = file } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+D", Action = new Mapping { Kind = ActionKind.FileOrFolder, Target = scratch } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+L", Action = new Mapping { Kind = ActionKind.WindowsShortcut, Target = shortcut } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+C", Action = new Mapping { Kind = ActionKind.Command, Target = exe, Arguments = "--command-from-custom", WorkingDirectory = scratch } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+P", Action = new Mapping { Kind = ActionKind.Python, Target = script, Arguments = "--python-from-custom" } }
                };
                using (var sink = new SignalingSink()) using (var engine = new KeyboardEngine(sink, true)) {
                    engine.Apply(new Configuration { Enabled = true, CustomHotkeys = hotkeys });
                    foreach (var hotkey in hotkeys) Assert(engine.TriggerCustomHotkey(hotkey.Shortcut), "custom launch was not queued: " + hotkey.Shortcut);
                    WaitFor(() => sink.LaunchSnapshot().Length == hotkeys.Length);
                    ProcessStartInfo[] launches = sink.LaunchSnapshot();
                    Assert(launches[0].FileName == exe && launches[0].Arguments == "--from-custom" && launches[0].WorkingDirectory == scratch && launches[0].UseShellExecute, "application custom action lost its launch details");
                    Assert(launches[1].FileName == file && launches[1].UseShellExecute, "file custom action was not shell-opened");
                    Assert(launches[2].FileName == scratch && launches[2].UseShellExecute, "folder custom action was not shell-opened");
                    Assert(launches[3].FileName == shortcut && launches[3].UseShellExecute, "shortcut custom action was not shell-opened");
                    Assert(launches[4].FileName == exe && launches[4].Arguments == "--command-from-custom" && launches[4].WorkingDirectory == scratch && !launches[4].UseShellExecute, "command custom action lost its execution mode or details");
                    Assert(launches[5].FileName == "py.exe" && launches[5].Arguments.Contains("\"" + script + "\"") && launches[5].Arguments.EndsWith("--python-from-custom", StringComparison.Ordinal), "Python custom action lost script quoting or arguments");
                }
            });
            Test("Custom hotkeys dispatch send-key, shortcut, and media actions through the shared input sink", delegate {
                var hotkeys = new[] {
                    new CustomHotkey { Shortcut = "Ctrl+Alt+K", Action = new Mapping { Kind = ActionKind.SendKey, Target = "A" } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+S", Action = new Mapping { Kind = ActionKind.SendShortcut, Target = "Ctrl+Shift+F6" } },
                    new CustomHotkey { Shortcut = "Ctrl+Alt+M", Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" } }
                };
                using (var sink = new SignalingSink()) using (var engine = new KeyboardEngine(sink, true)) {
                    engine.Apply(new Configuration { Enabled = true, CustomHotkeys = hotkeys });
                    foreach (var hotkey in hotkeys) Assert(engine.TriggerCustomHotkey(hotkey.Shortcut), "custom input action was not queued: " + hotkey.Shortcut);
                    WaitFor(() => sink.Snapshot().Length == hotkeys.Length);
                    int[][] keys = sink.Snapshot();
                    Assert(keys[0].SequenceEqual(new[] { (int)Keys.Shift, (int)Keys.A }), "single-key character semantics were lost for a custom binding");
                    Assert(keys[1].SequenceEqual(new[] { (int)Keys.ControlKey, (int)Keys.ShiftKey, (int)Keys.F6 }), "custom shortcut modifiers/order were lost");
                    Assert(keys[2].SequenceEqual(new[] { 0xAD }), "custom media binding did not route volume mute");
                }
            });
            Test("All public DDC power and backlight actions dispatch from custom hotkeys", delegate {
                string[] controls = { "MonitorStandby", "MonitorWake", "BacklightOff", "BacklightOn", "BacklightLevelOff", "BacklightLevelOn" };
                var hotkeys = controls.Select((control, index) => new CustomHotkey {
                    Shortcut = "Ctrl+Alt+F" + (index + 1),
                    Action = new Mapping { Kind = ActionKind.Monitor, MonitorId = new string('a', 64), MonitorControl = control, MonitorStep = 5 }
                }).ToArray();
                var ddc = new FakeDdc();
                using (var engine = new KeyboardEngine(new FakeSink(), false, ddc)) {
                    engine.Apply(new Configuration { Enabled = true, CustomHotkeys = hotkeys });
                    int expectedCalls = 0;
                    foreach (var hotkey in hotkeys) { Assert(engine.TriggerCustomHotkey(hotkey.Shortcut), "custom DDC action did not queue: " + hotkey.Action.MonitorControl); expectedCalls++; WaitFor(() => ddc.Calls == expectedCalls); }
                    WaitFor(() => ddc.Calls == controls.Length);
                    Assert(ddc.ControlSnapshot().SequenceEqual(controls), "custom DDC bindings changed or merged a power/backlight operation");
                }
            });
            Test("Profile actions validate, summarize and route without touching disk", delegate {
                var action = new Mapping { Kind = ActionKind.SystemAction, Target = SystemActions.ActivateProfilePrefix + "Gaming" };
                var sink = new FakeSink(); new ActionDispatcher(sink, new FakeDdc()).Execute(action);
                Assert(action.Summary == "Activate profile: Gaming" && sink.Profiles.SequenceEqual(new[] { "Gaming" }), "profile action routing");
                Reject(() => ConfigStore.Validate(new Mapping { Kind = ActionKind.SystemAction, Target = SystemActions.ActivateProfilePrefix }, false));
                Reject(() => ConfigStore.Validate(new Mapping { Kind = ActionKind.SystemAction, Target = SystemActions.ActivateProfilePrefix + "Bad\nName" }, false));
            });
            Test("Custom profile-activation hotkey reaches the profile action sink", delegate {
                const string shortcut = "Ctrl+Alt+G";
                var hotkey = new CustomHotkey { Shortcut = shortcut, Action = new Mapping { Kind = ActionKind.SystemAction, Target = SystemActions.ActivateProfilePrefix + "Gaming" } };
                using (var sink = new SignalingSink()) using (var engine = new KeyboardEngine(sink, true)) {
                    engine.Apply(new Configuration { Enabled = true, CustomHotkeys = new[] { hotkey } });
                    Assert(engine.TriggerCustomHotkey(shortcut), "custom profile action did not queue");
                    WaitFor(() => sink.ProfileSnapshot().Length == 1);
                    Assert(sink.ProfileSnapshot().SequenceEqual(new[] { "Gaming" }) && sink.Snapshot().Length == 0 && sink.LaunchSnapshot().Length == 0,
                        "custom profile binding did not activate exactly the selected profile without unrelated actions");
                }
            });
            Test("Profile removal remaps saved references across default, layer, and nested custom actions", delegate {
                Mapping activate = new Mapping { Kind = ActionKind.SystemAction, Target = SystemActions.ActivateProfilePrefix + "Gaming" };
                Mapping conditional = new Mapping { Kind = ActionKind.Conditional, Target = ConditionalCodec.Serialize(new ConditionalRule { Application = "Discord.exe", WhenMatched = activate.Copy(), Otherwise = new Mapping { Kind = ActionKind.Unbound } }) };
                var otherConfiguration = new Configuration(); otherConfiguration.Mappings[0] = activate.Copy();
                otherConfiguration.Layers = new[] { new ModifierLayer { Name = "Work", ActivationKey = "CapsLock" } }; otherConfiguration.Layers[0].Mappings[1] = activate.Copy();
                otherConfiguration.CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+G", Action = new Mapping { Kind = ActionKind.Sequence, Target = SequenceCodec.Serialize(new[] { new SequenceStep { Action = activate.Copy() }, new SequenceStep { Action = conditional } }) } } };
                var savedProfiles = new ProfileCollection { Profiles = new[] { new KeyWeaveProfile { Name = "Other", Configuration = otherConfiguration } } };
                var savedDefault = new Configuration(); savedDefault.Mappings[0] = activate.Copy(); int defaultReferences;
                int total = ProfileActionReferences.Rewrite(savedProfiles, savedDefault, "Gaming", "Default", out defaultReferences);
                Assert(total == 5 && defaultReferences == 1, "reference count omitted a mapping, layer, sequence, conditional or default action: " + total + "/" + defaultReferences);
                Assert(savedDefault.Mappings[0].Target == SystemActions.ActivateProfilePrefix + "Default" && savedProfiles.Profiles[0].Configuration.Mappings[0].Target == SystemActions.ActivateProfilePrefix + "Default" && savedProfiles.Profiles[0].Configuration.Layers[0].Mappings[1].Target == SystemActions.ActivateProfilePrefix + "Default", "direct profile references were not remapped");
                var sequence = SequenceCodec.Parse(savedProfiles.Profiles[0].Configuration.CustomHotkeys[0].Action.Target);
                var rewrittenConditional = ConditionalCodec.Parse(sequence[1].Action.Target);
                Assert(sequence[0].Action.Target == SystemActions.ActivateProfilePrefix + "Default" && rewrittenConditional.WhenMatched.Target == SystemActions.ActivateProfilePrefix + "Default", "nested custom-hotkey references were not remapped");
            });
            Test("Profile removal saves references across files and restores the old profile file on a failed second save", delegate {
                string folder = Path.Combine(scratch, "profile-reference-transaction"); Directory.CreateDirectory(folder);
                string profilePath = Path.Combine(folder, "profiles.kiweave"), configurationPath = Path.Combine(folder, "config.kiweave");
                var originalProfile = new KeyWeaveProfile { Name = "Gaming", Configuration = new Configuration() };
                var otherProfile = new KeyWeaveProfile { Name = "Other", Configuration = new Configuration() };
                otherProfile.Configuration.CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+G", Action = new Mapping { Kind = ActionKind.SystemAction, Target = SystemActions.ActivateProfilePrefix + "Gaming" } } };
                var originalProfiles = new ProfileCollection { Profiles = new[] { originalProfile, otherProfile } };
                var originalDefault = new Configuration(); originalDefault.Mappings[0] = new Mapping { Kind = ActionKind.SystemAction, Target = SystemActions.ActivateProfilePrefix + "Gaming" };
                ProfileStore.Save(profilePath, originalProfiles); ConfigStore.Save(configurationPath, originalDefault);
                var nextProfiles = originalProfiles.Copy(); nextProfiles.Profiles = nextProfiles.Profiles.Where(x => x.Name != "Gaming").ToArray(); var nextDefault = originalDefault.Copy(); int defaultReferences;
                int references = ProfileActionReferences.Rewrite(nextProfiles, nextDefault, "Gaming", "Default", out defaultReferences);
                ProfileActionReferences.SaveChanges(profilePath, configurationPath, nextProfiles, nextDefault, defaultReferences > 0);
                Assert(references == 2 && ConfigStore.Load(configurationPath).Mappings[0].Target == SystemActions.ActivateProfilePrefix + "Default" && ProfileStore.Load(profilePath).Profiles[0].Configuration.CustomHotkeys[0].Action.Target == SystemActions.ActivateProfilePrefix + "Default", "successful profile removal did not persist both remapped files");

                string failingFolder = Path.Combine(scratch, "profile-reference-rollback"); Directory.CreateDirectory(failingFolder); string rollbackProfilePath = Path.Combine(failingFolder, "profiles.kiweave"), blockedConfigurationPath = Path.Combine(failingFolder, "config.kiweave");
                ProfileStore.Save(rollbackProfilePath, originalProfiles); byte[] before = File.ReadAllBytes(rollbackProfilePath); Directory.CreateDirectory(blockedConfigurationPath);
                var rollbackProfiles = originalProfiles.Copy(); rollbackProfiles.Profiles = rollbackProfiles.Profiles.Where(x => x.Name != "Gaming").ToArray(); var rollbackDefault = originalDefault.Copy();
                ProfileActionReferences.Rewrite(rollbackProfiles, rollbackDefault, "Gaming", "Default", out defaultReferences);
                bool failedSecondSave = false; try { ProfileActionReferences.SaveChanges(rollbackProfilePath, blockedConfigurationPath, rollbackProfiles, rollbackDefault, defaultReferences > 0); } catch (Exception) { failedSecondSave = true; }
                Assert(failedSecondSave && File.ReadAllBytes(rollbackProfilePath).SequenceEqual(before), "failed config save did not restore the prior profile file exactly");
                string oversizedProfilePath = Path.Combine(failingFolder, "oversized-profiles.kiweave"); byte[] oversized = new byte[ProfileStore.MaxBytes + 1]; new Random(17).NextBytes(oversized); File.WriteAllBytes(oversizedProfilePath, oversized);
                bool refusedOversized = false; try { ProfileActionReferences.SaveChanges(oversizedProfilePath, Path.Combine(failingFolder, "unused-config.kiweave"), nextProfiles, nextDefault, true); } catch (IOException) { refusedOversized = true; }
                Assert(refusedOversized && File.ReadAllBytes(oversizedProfilePath).SequenceEqual(oversized), "oversized saved profiles were not refused and preserved");
            });
            Test("HTTP action reaches a local endpoint", delegate {
                bool previousNetwork = NetworkPolicy.Enabled; NetworkPolicy.Enabled = true;
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
                finally { listener.Stop(); thread.Join(2000); NetworkPolicy.Enabled = previousNetwork; }
                if (serverError != null) throw serverError;
                Assert(requestLine == "GET /keyweave HTTP/1.1", "unexpected request: " + requestLine);
            });
            Test("Custom HTTP binding reaches a local endpoint through the runtime queue", delegate {
                bool previousNetwork = NetworkPolicy.Enabled; NetworkPolicy.Enabled = true;
                string requestLine = null; Exception serverError = null;
                using (var requestDone = new ManualResetEvent(false)) {
                    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
                    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    var thread = new Thread(new ThreadStart(delegate {
                        try {
                            using (var client = listener.AcceptTcpClient()) using (var stream = client.GetStream()) using (var reader = new StreamReader(stream)) {
                                requestLine = reader.ReadLine(); string line; while (!String.IsNullOrEmpty(line = reader.ReadLine())) { }
                                byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"); stream.Write(response, 0, response.Length);
                            }
                        } catch (Exception ex) { serverError = ex; }
                        finally { requestDone.Set(); }
                    }));
                    thread.IsBackground = true; thread.Start();
                    try {
                        using (var engine = new KeyboardEngine(new FakeSink(), true)) {
                            engine.Apply(new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+H", Action = new Mapping { Kind = ActionKind.HttpRequest, Target = "http://127.0.0.1:" + port + "/custom-key" } } } });
                            Assert(engine.TriggerCustomHotkey("Ctrl+Alt+H"), "custom HTTP action was not queued");
                            Assert(requestDone.WaitOne(3000), "custom HTTP action did not reach the local endpoint");
                        }
                    } finally { listener.Stop(); thread.Join(2000); NetworkPolicy.Enabled = previousNetwork; }
                    if (serverError != null) throw serverError;
                    Assert(requestLine == "GET /custom-key HTTP/1.1", "unexpected custom request: " + requestLine);
                }
            });
            Test("Media integrations route safely and Discord fallback avoids unrelated foreground apps", delegate {
                DiscordIntegration.Disconnect();
                var sink = new FakeSink(); var dispatcher = new ActionDispatcher(sink, new FakeDdc());
                dispatcher.Execute(new Mapping { Kind = ActionKind.SystemAction, Target = "SpotifyPlayPause" });
                dispatcher.Execute(new Mapping { Kind = ActionKind.SystemAction, Target = "DiscordMute" });
                Assert(sink.Keys.Count == 1 && sink.Keys[0].Last() == (int)Keys.MediaPlayPause, "Discord fallback leaked into the unrelated foreground app");
            });
            Test("Configuration health scan is local and read-only", delegate {
                var clean = ConfigurationHealth.Scan(new Configuration(), new ProfileCollection(), new DdcMonitor[0]);
                Assert(!clean.HasWarnings, "safe defaults should be healthy");
                var broken = new Configuration(); broken.Mappings[0] = new Mapping { Kind = ActionKind.Application, Target = Path.Combine(Path.GetTempPath(), "KiWeave-health-missing-" + Guid.NewGuid().ToString("N") + ".exe") };
                var report = ConfigurationHealth.Scan(broken, new ProfileCollection(), new DdcMonitor[0]);
                Assert(report.HasWarnings && report.Findings.Any(f => f.Title == "Missing target"), "missing target was not reported");
            });
            Test("Configuration health scan failures are contained and redacted", delegate {
                ConfigurationHealthReport report;
                Assert(!ConfigurationHealth.TryScan(() => { throw new InvalidOperationException(@"D:\private\mapping.kiweave"); }, out report), "unexpected scanner exception reported success");
                Assert(Object.ReferenceEquals(report, ConfigurationHealthReport.Unavailable) && report.HasWarnings && !report.Findings[0].Detail.Contains("private"), "scan failure leaked private exception text or looked healthy");
                Assert(ConfigurationHealth.TryScan(() => ConfigurationHealth.Scan(new Configuration(), new ProfileCollection(), new DdcMonitor[0]), out report) && !report.HasWarnings, "healthy result changed through safe scan wrapper");
            });
            Test("Release version metadata follows shared product identity", delegate {
                var informationalVersion = (System.Reflection.AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(typeof(Program).Assembly, typeof(System.Reflection.AssemblyInformationalVersionAttribute));
                Assert(UpdateChecker.CurrentVersion == KiWeaveBuildIdentity.CurrentVersion && typeof(Program).Assembly.GetName().Version.ToString() == "1.0.0.0" && informationalVersion != null && informationalVersion.InformationalVersion == KiWeaveBuildIdentity.CurrentVersion, "version mismatch");
                Assert(KiWeaveBuildIdentity.ToastStyleLabelFor("1.1.0-alpha.1") == "KiWeave (Alpha)" && KiWeaveBuildIdentity.ToastStyleLabelFor("1.1.0-beta.5") == "KiWeave (Beta)" && KiWeaveBuildIdentity.ToastStyleLabelFor("1.1.0") == "KiWeave", "toast release label mismatch");
            });
            Test("Portable data mode has an explicit marker and separate root", delegate {
                Assert(AppStorage.PortableMarkerPath.EndsWith("portable.mode") && !String.Equals(AppStorage.PortableRoot, AppStorage.DefaultDataFolder, StringComparison.OrdinalIgnoreCase), "portable paths");
            });
            Test("Backup reminders are bounded and repeat no more than weekly", delegate {
                string folder = Path.Combine(scratch, "backup-reminder"); Directory.CreateDirectory(folder); string file = Path.Combine(folder, "old.kiweave"); BackupBundle.Save(file, new Configuration(), new ProfileCollection(), new UserPreferences(), false, false); File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-31));
                string legacyFile = Path.Combine(folder, "legacy.keyweave"); BackupBundle.Save(legacyFile, new Configuration(), new ProfileCollection(), new UserPreferences(), false, false); File.SetLastWriteTimeUtc(legacyFile, DateTime.UtcNow.AddDays(-20));
                string invalidFile = Path.Combine(folder, "not-a-backup.kiweave"); File.WriteAllText(invalidFile, "x"); File.SetLastWriteTimeUtc(invalidFile, DateTime.UtcNow.AddDays(-45));
                Func<string, bool> validateBackup = delegate(string path) { BackupBundle.LoadAny(path); return true; };
                var first = BackupReminder.Inspect(folder, DateTime.UtcNow, null, validateBackup); var recent = BackupReminder.Inspect(folder, DateTime.UtcNow, DateTime.UtcNow.AddDays(-2), validateBackup);
                Assert(first.ShouldNotify && first.OldestAgeDays >= 30 && first.OldestAgeDays < 32 && !recent.ShouldNotify && recent.Message.Contains("days old"), "backup reminder cadence or invalid-file filtering");
                string encryptedFolder = Path.Combine(scratch, "encrypted-backup-reminder"); Directory.CreateDirectory(encryptedFolder); string encryptedFile = Path.Combine(encryptedFolder, "only-backup.kiweave.enc"); BackupBundle.SaveEncrypted(encryptedFile, new Configuration(), new ProfileCollection(), new UserPreferences(), false); File.SetLastWriteTimeUtc(encryptedFile, DateTime.UtcNow.AddDays(-31));
                var encryptedOnly = BackupReminder.Inspect(encryptedFolder, DateTime.UtcNow, null, validateBackup);
                Assert(encryptedOnly.HasBackup && encryptedOnly.ShouldNotify && encryptedOnly.OldestAgeDays >= 30 && encryptedOnly.OldestAgeDays < 32, "encrypted-only backup folder was not recognized by the reminder");
                string invalidFolder = Path.Combine(scratch, "invalid-backup-reminder"); Directory.CreateDirectory(invalidFolder); string onlyInvalid = Path.Combine(invalidFolder, "not-a-backup.kiweave"); File.WriteAllText(onlyInvalid, "x"); File.SetLastWriteTimeUtc(onlyInvalid, DateTime.UtcNow.AddDays(-45));
                var noBackup = BackupReminder.Inspect(invalidFolder, DateTime.UtcNow, null, validateBackup);
                Assert(!noBackup.HasBackup && !noBackup.ShouldNotify && noBackup.Message.Contains("No KiWeave backup"), "malformed export was counted as a backup");
                string marker = Path.Combine(scratch, "backup-reminder", "last-seen.txt"); DateTime notified = DateTime.UtcNow; BackupReminder.MarkNotified(marker, notified); DateTime loaded;
                Assert(BackupReminder.TryReadLastNotified(marker, out loaded) && Math.Abs((loaded - notified).TotalSeconds) < 1, "backup reminder timestamp round-trip");
                byte[] malformed = Encoding.ASCII.GetBytes(new string('x', 129)); File.WriteAllBytes(marker, malformed);
                Assert(!BackupReminder.TryReadLastNotified(marker, out loaded) && File.ReadAllBytes(marker).SequenceEqual(malformed), "oversized reminder marker was accepted or rewritten");
            });
            Test("First-party extension preferences default only when missing and fail closed without rewriting invalid files", delegate {
                string path = Path.Combine(scratch, "first-party-extensions.json");
                string[] ids = { "discord", "spotify", "obs", "powertoys", "streamdeck", "controller-midi" };
                var defaults = FirstPartyExtensionStateStore.Load(path, ids);
                Assert(defaults.Warning.Length == 0 && defaults.Values.Values.All(value => value), "missing file should retain first-run defaults");
                byte[] legacy = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("{\"discord\":false}")).ToArray(); File.WriteAllBytes(path, legacy);
                var migrated = FirstPartyExtensionStateStore.Load(path, ids);
                Assert(migrated.Warning.Length == 0 && !migrated.Values["discord"] && migrated.Values["spotify"], "valid partial legacy settings/BOM");
                byte[] duplicate = Encoding.UTF8.GetBytes("{\"discord\":false,\"discord\":true}"); File.WriteAllBytes(path, duplicate);
                var corrupt = FirstPartyExtensionStateStore.Load(path, ids);
                Assert(corrupt.Warning.Length > 0 && corrupt.Values.Values.All(value => !value) && File.ReadAllBytes(path).SequenceEqual(duplicate), "duplicate fields should disable and preserve settings");
                File.WriteAllText(path, new string(' ', 4097), new UTF8Encoding(false));
                corrupt = FirstPartyExtensionStateStore.Load(path, ids);
                Assert(corrupt.Warning.Length > 0 && corrupt.Values.Values.All(value => !value), "oversized file should disable extensions");
                File.WriteAllText(path, "{}", new UTF8Encoding(false));
                using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) { corrupt = FirstPartyExtensionStateStore.Load(path, ids); Assert(corrupt.Warning.Length > 0 && corrupt.Values.Values.All(value => !value), "locked file should disable extensions"); }
                Assert(File.ReadAllText(path) == "{}", "failed load rewrote settings");
                var recovered = ids.ToDictionary(id => id, id => true, StringComparer.OrdinalIgnoreCase); recovered["discord"] = false;
                FirstPartyExtensionStateStore.Save(path, recovered, ids);
                Assert(!FirstPartyExtensionStateStore.Load(path, ids).Values["discord"] && File.ReadAllText(path + ".bak") == "{}", "recovery save or backup");
            });
            Test("Action explanation retains only the latest redacted decision", delegate {
                ActionExplanation.Record(new Mapping { Kind = ActionKind.SendKey, Target = "R" }, "test input");
                string text = ActionExplanation.Latest;
                Assert(text.Contains("test input") && text.Contains("Action: R") && text.Contains("No key history"), "latest action explanation");
            });
            Test("Secret vault expands protected references without exporting values", delegate {
                string name = "test-secret-" + Guid.NewGuid().ToString("N"); SecretVault.Save(name, "sensitive-value");
                Assert(SecretVault.Expand("prefix-{vault:" + name + "}") == "prefix-sensitive-value" && !ActionPackStore.Serialize(new ActionPack { Id = "vault-test", Name = "Vault test", Configuration = new Configuration(), Profiles = new ProfileCollection() }).Contains("sensitive-value"), "vault expansion/export boundary");
                SecretVault.Delete(name);
            });
            Test("Unreadable secret vault is preserved and valid writes are atomic", delegate {
                string path = Path.Combine(scratch, "vault", "secrets.dat");
                SecretVault.Save(path, "api-token", "old-secret"); SecretVault.Save(path, "api-token", "new-secret");
                Assert(SecretVault.Resolve(path, "api-token") == "new-secret" && SecretVault.Resolve(path + ".bak", "api-token") == "old-secret" && Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp").Length == 0, "vault atomic replacement/backup");
                byte[] corrupt = new byte[] { 0x01, 0x02, 0x03 }; File.WriteAllBytes(path, corrupt); byte[] beforeSave = File.ReadAllBytes(path);
                bool saveRejected = false; try { SecretVault.Save(path, "second-token", "must-not-overwrite"); } catch (InvalidDataException) { saveRejected = true; }
                bool deleteRejected = false; try { SecretVault.Delete(path, "api-token"); } catch (InvalidDataException) { deleteRejected = true; }
                Assert(saveRejected && deleteRejected && beforeSave.SequenceEqual(File.ReadAllBytes(path)) && Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp").Length == 0, "corrupt vault was treated as empty or overwritten");
            });
            Test("Secret vault rejects oversized and excessive-entry payloads without rewriting them", delegate {
                string folder = Path.Combine(scratch, "vault-limits"); Directory.CreateDirectory(folder);
                byte[] entropy = Encoding.UTF8.GetBytes("KiWeave local secret vault v1");
                string crowded = Path.Combine(folder, "crowded.dat");
                var entries = Enumerable.Range(0, SecretVault.MaxSecretCount + 1).ToDictionary(i => "secret-" + i, i => "value");
                byte[] crowdedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(entries)), entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(crowded, crowdedBytes); bool tooManyRejected = false;
                try { SecretVault.LoadAll(crowded); } catch (InvalidDataException) { tooManyRejected = true; }
                bool saveRejected = false; try { SecretVault.Save(crowded, "extra", "value"); } catch (InvalidDataException) { saveRejected = true; }
                Assert(tooManyRejected && saveRejected && File.ReadAllBytes(crowded).SequenceEqual(crowdedBytes), "excess-entry vault was accepted or rewritten");

                string oversized = Path.Combine(folder, "oversized.dat");
                byte[] oversizedPlain = new byte[SecretVault.MaxVaultBytes + 1];
                byte[] oversizedBytes = ProtectedData.Protect(oversizedPlain, entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(oversized, oversizedBytes); bool sizeRejected = false;
                try { SecretVault.LoadAll(oversized); } catch (InvalidDataException) { sizeRejected = true; }
                Assert(sizeRejected && File.ReadAllBytes(oversized).SequenceEqual(oversizedBytes), "oversized vault was accepted or rewritten");
            });
            Test("MIDI trigger matching is bounded and channel-aware", delegate {
                var note = new MidiTrigger { Message = "NoteOn", Channel = 1, Number = 60, MinimumValue = 10, FunctionKey = 3 };
                Assert(note.Matches(0x91, 60, 10) && !note.Matches(0x90, 60, 10) && !note.Matches(0x91, 61, 100), "note trigger match");
                var cc = new MidiTrigger { Message = "ControlChange", Channel = 0, Number = 7, MinimumValue = 64 };
                Assert(cc.Matches(0xB0, 7, 64) && !cc.Matches(0x90, 7, 127), "cc trigger match");
                note.Device = 2; Assert(note.Matches(2, 0x91, 60, 10) && !note.Matches(1, 0x91, 60, 10), "MIDI device isolation");
            });
            Test("MIDI trigger persistence is bounded, atomic, and accepts legacy field casing", delegate {
                string path = Path.Combine(scratch, "midi-trigger-state.json"); var trigger = new MidiTrigger { Device = 2, Channel = 1, Number = 60, MinimumValue = 20, FunctionKey = 5, Message = "NoteOn" };
                MidiTriggerStore.Save(path, new[] { trigger }); Assert(MidiTriggerStore.Load(path).Single().Device == 2, "lower-camel round-trip");
                MidiTriggerStore.Save(path, new[] { new MidiTrigger { Device = 3, Channel = 2, Number = 7, MinimumValue = 64, FunctionKey = 6, Message = "ControlChange" } }); Assert(MidiTriggerStore.Load(path + ".bak").Single().Device == 2, "backup");
                File.WriteAllText(path, "{\"version\":1,\"triggers\":[{\"Device\":1,\"Channel\":0,\"Number\":60,\"MinimumValue\":1,\"FunctionKey\":2,\"Message\":\"NoteOn\"}]}"); Assert(MidiTriggerStore.Load(path).Single().FunctionKey == 2, "legacy PascalCase migration");
                File.WriteAllText(path, "{\"version\":1,\"triggers\":[{\"device\":1,\"extra\":true}]}"); bool malformed = false; try { MidiTriggerStore.Load(path); } catch (ArgumentException) { malformed = true; } Assert(malformed, "malformed file silently accepted");
                bool limit = false; try { MidiTriggerStore.Save(path, Enumerable.Range(0, MidiTriggerStore.MaxTriggers + 1).Select(i => trigger)); } catch (ArgumentException) { limit = true; } Assert(limit, "trigger cap");
            });
            Test("Controller trigger matching is rising-edge and bounded", delegate {
                var trigger = new ControllerTrigger { UserIndex = 1, Button = 10, FunctionKey = 4 };
                Assert(trigger.Matches(1, (ushort)(1 << 10)) && !trigger.Matches(0, (ushort)(1 << 10)) && !trigger.Matches(1, (ushort)(1 << 9)), "controller trigger match");
                ControllerTriggerStore.Validate(trigger);
            });
            Test("Controller and MIDI trigger dispatch stops cleanly during shutdown", delegate {
                var dispatch = new TriggerDispatchGate(); int fired = 0;
                Assert(dispatch.TryRun(delegate { fired++; }) && fired == 1, "open dispatch gate should run callbacks");
                dispatch.Close();
                Assert(!dispatch.TryRun(delegate { fired++; }) && fired == 1, "closed dispatch gate must reject later callbacks");

                var synchronized = new TriggerDispatchGate(); using (var entered = new ManualResetEvent(false)) using (var release = new ManualResetEvent(false)) using (var closeAttempted = new ManualResetEvent(false)) using (var closed = new ManualResetEvent(false))
                {
                    int callbackCount = 0;
                    var callbackThread = new Thread(new ThreadStart(delegate { synchronized.TryRun(delegate { Interlocked.Increment(ref callbackCount); entered.Set(); release.WaitOne(2000); }); })); callbackThread.Start();
                    Assert(entered.WaitOne(1000), "dispatch callback did not start");
                    var closeThread = new Thread(new ThreadStart(delegate { closeAttempted.Set(); synchronized.Close(); closed.Set(); })); closeThread.Start();
                    Assert(closeAttempted.WaitOne(1000), "close thread did not start");
                    Assert(!closed.WaitOne(50), "close must wait for an in-flight callback");
                    release.Set();
                    Assert(closed.WaitOne(1000), "close did not finish after callback completed");
                    Assert(!synchronized.TryRun(delegate { Interlocked.Increment(ref callbackCount); }) && callbackCount == 1, "no callback may start after close returns");
                    callbackThread.Join(1000); closeThread.Join(1000);
                }
            });
            Test("Controller trigger persistence is bounded, atomic, and backward compatible", delegate {
                string path = Path.Combine(scratch, "controller-trigger-state.json"); var trigger = new ControllerTrigger { UserIndex = 1, Button = 10, FunctionKey = 4 };
                ControllerTriggerStore.Save(path, new[] { trigger }); Assert(ControllerTriggerStore.Load(path).Single().UserIndex == 1, "lower-camel round-trip");
                ControllerTriggerStore.Save(path, new[] { new ControllerTrigger { UserIndex = 2, Button = 13, FunctionKey = 8 } }); Assert(ControllerTriggerStore.Load(path + ".bak").Single().UserIndex == 1, "backup");
                File.WriteAllText(path, "{\"version\":1,\"triggers\":[{\"UserIndex\":0,\"Button\":10,\"FunctionKey\":1}]}"); Assert(ControllerTriggerStore.Load(path).Single().FunctionKey == 1, "legacy PascalCase migration");
                File.WriteAllText(path, "{\"version\":1,\"triggers\":[{\"userIndex\":0,\"button\":10,\"functionKey\":1,\"extra\":2}]}"); bool malformed = false; try { ControllerTriggerStore.Load(path); } catch (ArgumentException) { malformed = true; } Assert(malformed, "malformed file silently accepted");
                bool limit = false; try { ControllerTriggerStore.Save(path, Enumerable.Range(0, ControllerTriggerStore.MaxTriggers + 1).Select(i => trigger)); } catch (ArgumentException) { limit = true; } Assert(limit, "trigger cap");
            });
            Test("Stream Deck plugin bundle stays declarative", delegate {
                string manifest = Path.Combine(Environment.CurrentDirectory, "extensions", "streamdeck", "com.kiweave.streamdeck.sdPlugin", "manifest.json");
                string plugin = Path.Combine(Environment.CurrentDirectory, "extensions", "streamdeck", "com.kiweave.streamdeck.sdPlugin", "plugin.js");
                Assert(File.Exists(manifest) && File.Exists(plugin), "plugin bundle files");
                string source = File.ReadAllText(plugin); Assert(source.Contains("status") && source.Contains("show-settings") && source.Contains("activate-profile") && !source.Contains("child_process"), "plugin action boundary");
            });
            Test("Localization uses a safe text-only fallback", delegate {
                string path = Path.Combine(scratch, "language-test.json");
                try {
                    Localization.Save(path, "English");
                    Assert(Localization.Translate("Save") == "Save" && Localization.Translate("Unknown label") == "Unknown label", "English text fallback");
                    Localization.Load(path); Assert(Localization.Current == "English", "saved language reload");
                    File.WriteAllText(path, "{\"version\":1,\"language\":\"Español\",\"language\":\"English\"}");
                    Localization.Load(path); Assert(Localization.Current == "English" && File.ReadAllText(path).Contains("Español"), "legacy language data falls back without rewriting file");
                } finally { Localization.Load(); }
            });
            Test("Localization applies recursively without rewriting unknown text", delegate {
                string path = Path.Combine(scratch, "language-ui-test.json"); try {
                    Localization.Save(path, "English");
                    using (var form = new Form()) using (var panel = new Panel()) {
                        var button = new Button { Text = "Save" }; var unknown = new Label { Text = "Private dynamic value" };
                        var combo = new ComboBox(); combo.Items.Add("Cancel"); combo.Items.Add("Private option"); panel.Controls.Add(button); panel.Controls.Add(unknown); panel.Controls.Add(combo); form.Controls.Add(panel);
                        Localization.Apply(form);
                        Assert(button.Text == "Save" && unknown.Text == "Private dynamic value" && Convert.ToString(combo.Items[0]) == "Cancel" && Convert.ToString(combo.Items[1]) == "Private option", "English recursive fallback");
                    }
                } finally { Localization.Load(); }
            });
            Test("Encrypted backups round-trip without plaintext on disk", delegate {
                string path = Path.Combine(scratch, "encrypted.keyweave.enc"); var c = new Configuration(); var p = new ProfileCollection(); var pref = new UserPreferences(); BackupBundle.SaveEncrypted(path, c, p, pref, false); string raw = Encoding.UTF8.GetString(File.ReadAllBytes(path)); Assert(!raw.Contains("KeyWeave Backup"), "encrypted plaintext boundary"); var loaded = BackupBundle.LoadEncrypted(path); Assert(loaded.Configuration.Mappings.Length == 12, "encrypted round trip");
                string oversized = Path.Combine(scratch, "oversized-encrypted.keyweave.enc"); File.WriteAllBytes(oversized, new byte[BackupBundle.MaxBytes * 2 + 1]); Reject(() => BackupBundle.LoadEncrypted(oversized));
            });
            Test("Game Input capability probe is safe on unsupported runtimes", delegate {
                string description = GameInputSupport.Describe(); Assert(!String.IsNullOrWhiteSpace(description) && (description.Contains("available") || description.Contains("unavailable")), "game input capability description");
                ushort buttons; Assert(!GameInputSupport.TryReadButtons(-1, out buttons), "invalid game input index");
            });
            Test("Virtual gamepad output requires explicit opt-in", delegate { VirtualGamepadBackend backend; string error; Assert(!VirtualGamepadBackend.TryCreate(false, out backend, out error) && backend == null && error.Contains("explicit opt-in"), "virtual output gate"); Assert(VirtualGamepadBackend.Describe().Contains("disabled by default"), "virtual output default"); });
        }
        static string ReadDiscordTestFrame(Stream stream)
        {
            byte[] header = ReadDiscordTestBytes(stream, 8); int length = BitConverter.ToInt32(header, 4);
            if (length < 0 || length > 1024 * 1024) throw new InvalidDataException("Test Discord frame length is invalid.");
            return Encoding.UTF8.GetString(ReadDiscordTestBytes(stream, length));
        }
        static byte[] ReadDiscordTestBytes(Stream stream, int length)
        {
            byte[] result = new byte[length]; int offset = 0;
            while (offset < length) { int count = stream.Read(result, offset, length - offset); if (count <= 0) throw new EndOfStreamException("Test Discord pipe closed before a full frame arrived."); offset += count; }
            return result;
        }
        static void WriteDiscordTestFrame(Stream stream, string payload)
        {
            byte[] body = Encoding.UTF8.GetBytes(payload); byte[] header = new byte[8];
            Buffer.BlockCopy(BitConverter.GetBytes(1), 0, header, 0, 4); Buffer.BlockCopy(BitConverter.GetBytes(body.Length), 0, header, 4, 4);
            stream.Write(header, 0, header.Length); stream.Write(body, 0, body.Length); stream.Flush();
        }
        static Configuration MonitorConfig() { var c = new Configuration(); c.Mappings[0] = new Mapping { Kind = ActionKind.Monitor, MonitorId = new string('a',64), MonitorControl = "VolumeDown", MonitorStep = 5 }; return c; }
        sealed class FakeDdc : IDdcController { readonly object gate = new object(); int calls; public int Calls { get { lock (gate) return calls; } } public bool Allowed; public Mapping Last; readonly List<string> controls = new List<string>(); public string[] ControlSnapshot() { lock (gate) return controls.ToArray(); } public void Apply(Mapping m, Func<bool> active) { lock (gate) { calls++; Last = m; controls.Add(m.MonitorControl); Allowed = active(); } } }
        sealed class BlockingDdc : IDdcController, IDisposable { public readonly ManualResetEvent Started = new ManualResetEvent(false), Release = new ManualResetEvent(false), Finished = new ManualResetEvent(false); public volatile bool AllowedAfterRelease; public void Apply(Mapping m, Func<bool> active) { Started.Set(); Release.WaitOne(3000); AllowedAfterRelease = active(); Finished.Set(); } public void Dispose() { Started.Dispose(); Release.Dispose(); Finished.Dispose(); } }
        static void NativeTests()
        {
            // A lower observation hook swallows the test F5 events so no foreground application sees them.
            using (var observer = new NativeObserver()) {
                Test("Production hook ignores injected F5", delegate { using (var engine = new KeyboardEngine(new WindowsActionSink(), false)) { engine.Apply(Active(ActionKind.Unbound, "")); observer.Clear(); SendTest(false); SendTest(true); Thread.Sleep(150); Assert(observer.F5Count == 2, "production injection must pass; observer callbacks=" + observer.CallbackCountValue + ", tagged=" + observer.TaggedCountValue); } });
                Test("Native hook suppresses marked test F5 and emits one F6 pair", delegate { using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(Active(ActionKind.SendKey, "F6")); observer.Clear(); SendTest(false); Thread.Sleep(120); for (int i = 0; i < 8; i++) SendTest(false); SendTest(true); Thread.Sleep(150); Assert(observer.F5Count == 0 && observer.F6DownCount == 1 && observer.F6UpCount == 1, "F5=" + observer.F5Count + ", F6=" + observer.F6DownCount + "/" + observer.F6UpCount); } });
                Test("Native live tester receives one transient resolved event", delegate { using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) using (var seen = new AutoResetEvent(false)) { engine.Apply(Active(ActionKind.Unbound, "")); KeyDiagnostic latest = null; engine.KeyObserved += item => { latest = item; seen.Set(); }; observer.Clear(); SendTest(false); Assert(seen.WaitOne(1000), "no diagnostic event"); SendTest(true); Assert(latest != null && latest.KeyName == "F5" && latest.Suppressed && latest.ResolvedAction == "Unbound (do nothing)", latest == null ? "diagnostic was null" : "key=" + latest.KeyName + ", suppressed=" + latest.Suppressed + ", resolved=" + latest.ResolvedAction + ", source=" + latest.Source); } });
                Test("Native modifier layer suppresses its hold key and routes F5", delegate { string path = GameMode.Path; string previous = File.Exists(path) ? File.ReadAllText(path) : null; try { GameMode.Set(false); var c = new Configuration { Enabled = true, Layers = new[] { new ModifierLayer { Name = "Test", ActivationKey = "CapsLock" } } }; c.Layers[0].Mappings[4] = new Mapping { Kind = ActionKind.SendKey, Target = "F6" }; using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(c); observer.Clear(); SendTestKey((int)Keys.CapsLock, false); SendTest(false); SendTest(true); SendTestKey((int)Keys.CapsLock, true); Thread.Sleep(150); Assert(observer.CapsLockCount == 0 && observer.F5Count == 0 && observer.F6DownCount == 1 && observer.F6UpCount == 1, "Caps=" + observer.CapsLockCount + ", F5=" + observer.F5Count + ", F6=" + observer.F6DownCount + "/" + observer.F6UpCount); } } finally { if (previous == null) { try { File.Delete(path); } catch { } } else File.WriteAllText(path, previous, Encoding.ASCII); } });
                Test("Native hook suppresses an assigned media button and routes its action", delegate { var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "MediaPlayPause", Action = new Mapping { Kind = ActionKind.SendKey, Target = "F6" } } } }; using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(c); observer.Clear(); SendTestKey((int)Keys.MediaPlayPause, false); SendTestKey((int)Keys.MediaPlayPause, true); Thread.Sleep(150); Assert(observer.MediaPlayPauseCount == 0 && observer.F6DownCount == 1 && observer.F6UpCount == 1, "Media=" + observer.MediaPlayPauseCount + ", F6=" + observer.F6DownCount + "/" + observer.F6UpCount); } });
                Test("Native hook suppresses an assigned standalone F13 key and routes its action", delegate { var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "F13", Action = new Mapping { Kind = ActionKind.SendKey, Target = "F6" } } } }; using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(c); observer.Clear(); SendTestKey((int)Keys.F13, false); SendTestKey((int)Keys.F13, true); Thread.Sleep(150); Assert(observer.F13Count == 0 && observer.F6DownCount == 1 && observer.F6UpCount == 1, "F13=" + observer.F13Count + ", F6=" + observer.F6DownCount + "/" + observer.F6UpCount); } });
                Test("Native failed F1 chord registration does not swallow the base function mapping", delegate { var c = Active(ActionKind.SendKey, "F6"); c.Mappings[0] = new Mapping { Kind = ActionKind.SendKey, Target = "F6" }; c.CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+F1", Action = new Mapping { Kind = ActionKind.SendKey, Target = "F7" } } }; using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(c); engine.SetCustomChordPrecedence(new int[0]); observer.Clear(); SendTestKey((int)Keys.ControlKey, false); SendTestKey((int)Keys.F1, false); SendTestKey((int)Keys.F1, true); SendTestKey((int)Keys.ControlKey, true); Thread.Sleep(150); Assert(observer.F6DownCount == 1 && observer.F6UpCount == 1, "failed Ctrl+F1 custom registration also disabled the F1 mapping"); } });
                Test("Native standalone Unbound key does not consume action-queue capacity", delegate { using (var sink = new BlockingSink()) using (var engine = new KeyboardEngine(sink, true)) { try { var c = Active(ActionKind.SendKey, "F6"); c.CustomHotkeys = new[] { new CustomHotkey { Shortcut = "F13", Action = new Mapping { Kind = ActionKind.Unbound } }, new CustomHotkey { Shortcut = "Ctrl+Alt+U", Action = new Mapping { Kind = ActionKind.SendKey, Target = "F7" } } }; engine.Apply(c); Assert(engine.TriggerFunctionKey(5), "could not start queue blocker"); Assert(sink.Started.WaitOne(1000), "queue blocker did not start"); for (int i = 0; i < 23; i++) Assert(engine.TriggerFunctionKey(5), "could not reserve pending action " + i); SendTestKey((int)Keys.F13, false); SendTestKey((int)Keys.F13, false); Thread.Sleep(100); Assert(engine.TriggerCustomHotkey("Ctrl+Alt+U"), "Unbound F13 press occupied the final queue slot"); SendTestKey((int)Keys.F13, true); } finally { sink.Release.Set(); } } });
                Test("Function and custom triggers report a saturated action queue without flooding errors", delegate { using (var sink = new BlockingSink()) using (var engine = new KeyboardEngine(sink, true)) using (var errorSeen = new AutoResetEvent(false)) { int errors = 0; engine.Error += message => { if (message.Contains("busy") && Interlocked.Increment(ref errors) == 1) errorSeen.Set(); }; try { var c = Active(ActionKind.SendKey, "F6"); c.CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+B", Action = new Mapping { Kind = ActionKind.SendKey, Target = "F6" } } }; engine.Apply(c); Assert(engine.TriggerCustomHotkey("Ctrl+Alt+B"), "initial custom action was not queued"); Assert(sink.Started.WaitOne(1000), "blocking action did not start"); for (int i = 0; i < 24; i++) Assert(engine.TriggerFunctionKey(5), "queue did not accept pending action " + i); Assert(!engine.TriggerCustomHotkey("Ctrl+Alt+B"), "full queue accepted an extra custom action"); Assert(errorSeen.WaitOne(1000), "saturated custom action was silently dropped"); SendTest(false); SendTest(true); Thread.Sleep(100); Assert(errors == 1, "queue saturation emitted duplicate errors"); } finally { sink.Release.Set(); } } });
                Test("Custom-hotkey profile action reaches the WPF activation callback", delegate { using (var activated = new AutoResetEvent(false)) using (var engine = new KeyboardEngine(name => { if (name == "Gaming") activated.Set(); })) { var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+G", Action = new Mapping { Kind = ActionKind.SystemAction, Target = SystemActions.ActivateProfilePrefix + "Gaming" } } } }; engine.Apply(c); Assert(engine.TriggerCustomHotkey("Ctrl+Alt+G"), "profile binding was not queued"); Assert(activated.WaitOne(1000), "profile action did not reach WPF activation callback"); } });
                Test("Hardware-triggered monitor actions share the bounded DDC queue", delegate { using (var ddc = new BlockingDdc()) using (var engine = new KeyboardEngine(new WindowsActionSink(), false, ddc)) { try { var c = new Configuration { Enabled = true }; c.Mappings[0] = MonitorConfig().Mappings[0]; engine.Apply(c); Assert(engine.TriggerFunctionKey(1), "first monitor trigger"); Assert(ddc.Started.WaitOne(1000), "monitor worker did not start"); Assert(engine.TriggerFunctionKey(1), "second monitor trigger should occupy the single pending slot"); Assert(!engine.TriggerFunctionKey(1), "third monitor trigger should be rejected by DDC backpressure"); } finally { ddc.Release.Set(); } } });
                Test("Custom-hotkey DDC backlight actions share the bounded monitor queue", delegate { using (var ddc = new BlockingDdc()) using (var engine = new KeyboardEngine(new WindowsActionSink(), false, ddc)) { try { var action = new Mapping { Kind = ActionKind.Monitor, MonitorId = new string('a', 64), MonitorControl = "BacklightOff", MonitorStep = 5 }; var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+B", Action = action } } }; engine.Apply(c); Assert(engine.TriggerCustomHotkey("Ctrl+Alt+B"), "first custom DDC trigger"); Assert(ddc.Started.WaitOne(1000), "custom DDC action did not reach monitor worker"); Assert(engine.TriggerCustomHotkey("Ctrl+Alt+B"), "second custom DDC trigger should occupy the pending slot"); Assert(!engine.TriggerCustomHotkey("Ctrl+Alt+B"), "third custom DDC trigger should be rejected by DDC backpressure"); } finally { ddc.Release.Set(); } } });
                Test("Conditional custom actions reserve DDC backpressure when either branch uses a monitor", delegate { using (var ddc = new BlockingDdc()) using (var engine = new KeyboardEngine(new WindowsActionSink(), false, ddc)) { try { var monitor = new Mapping { Kind = ActionKind.Monitor, MonitorId = new string('a', 64), MonitorControl = "BacklightOff", MonitorStep = 5 }; var rule = new ConditionalRule { Condition = ConditionKind.ApplicationRunning, Application = Process.GetCurrentProcess().ProcessName + ".exe", WhenMatched = monitor, Otherwise = new Mapping { Kind = ActionKind.Unbound } }; var conditional = new Mapping { Kind = ActionKind.Conditional, Target = ConditionalCodec.Serialize(rule) }; var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+C", Action = conditional } } }; engine.Apply(c); Assert(engine.TriggerCustomHotkey("Ctrl+Alt+C"), "first conditional trigger"); Assert(ddc.Started.WaitOne(1000), "conditional DDC branch did not reach monitor worker"); Assert(engine.TriggerCustomHotkey("Ctrl+Alt+C"), "second conditional should occupy the pending DDC slot"); Assert(!engine.TriggerCustomHotkey("Ctrl+Alt+C"), "third conditional was not backpressured"); } finally { ddc.Release.Set(); } } });
                Test("Custom-hotkey DDC work is cancelled when shortcuts are disabled", delegate { using (var ddc = new BlockingDdc()) using (var engine = new KeyboardEngine(new WindowsActionSink(), false, ddc)) { try { var c = new Configuration { Enabled = true, CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+B", Action = MonitorConfig().Mappings[0] } } }; engine.Apply(c); Assert(engine.TriggerCustomHotkey("Ctrl+Alt+B"), "custom DDC trigger"); Assert(ddc.Started.WaitOne(1000), "custom DDC action did not reach monitor worker"); engine.SetEnabled(false); ddc.Release.Set(); Assert(ddc.Finished.WaitOne(1000) && !ddc.AllowedAfterRelease, "disabled custom DDC job was allowed to continue"); } finally { ddc.Release.Set(); } } });
                Test("Native Unbound swallows both edges; disable restores pass-through", delegate { using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(Active(ActionKind.Unbound, "")); observer.Clear(); SendTest(false); SendTest(true); Thread.Sleep(100); Assert(observer.F5Count == 0, "unbound"); engine.SetEnabled(false); SendTest(false); SendTest(true); Thread.Sleep(100); Assert(observer.F5Count == 2, "disable"); } });
                Test("Native held key launches exactly one real process", delegate { string marker = Path.Combine(scratch, "launch-marker.txt"); var c = Active(ActionKind.Application, Process.GetCurrentProcess().MainModule.FileName); c.Mappings[4].Arguments = "--probe \"" + marker + "\""; using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(c); observer.Clear(); SendTest(false); for (int i = 0; i < 20; i++) SendTest(false); SendTest(true); WaitFor(() => File.Exists(marker)); Thread.Sleep(200); Assert(File.ReadAllLines(marker).Length == 1 && observer.F5Count == 0, "launch count/suppression"); } });
                Test("Hook startup timeout waits for full thread cleanup", delegate { using (var hookExited = new ManualResetEvent(false)) { bool timedOut = false; try { new KeyboardEngine(new WindowsActionSink(), true, null, () => true, delegate { }, delegate { Thread.Sleep(100); }, 10, delegate { hookExited.Set(); }); } catch (InvalidOperationException error) { timedOut = error.Message.Contains("did not start within five seconds"); } Assert(timedOut, "startup should fail after the injected deadline"); Assert(hookExited.WaitOne(100), "constructor returned before the hook thread finished cleanup"); } });
                Test("Startup readiness handle survives a hook thread outliving the dispose join", delegate { using (var hookExited = new ManualResetEvent(false)) { bool timedOut = false; try { new KeyboardEngine(new WindowsActionSink(), true, null, () => true, delegate { }, delegate { Thread.Sleep(2150); }, 10, delegate { hookExited.Set(); }); } catch (InvalidOperationException error) { timedOut = error.Message.Contains("did not start within five seconds"); } Assert(timedOut, "startup should time out while the injected hook thread is delayed"); Assert(hookExited.WaitOne(1000), "delayed hook thread did not finish after the bounded dispose join"); } });
                Test("Native pass-through and repeated disposal restore both edges", delegate { using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(new Configuration { Enabled = true }); observer.Clear(); SendTest(false); SendTest(true); Thread.Sleep(100); Assert(observer.F5Count == 2, "pass through"); engine.Apply(Active(ActionKind.Unbound, "")); engine.Dispose(); engine.Dispose(); } observer.Clear(); SendTest(false); SendTest(true); Thread.Sleep(100); Assert(observer.F5Count == 2, "after repeated disposal"); });
            }
        }
        static void WaitFor(Func<bool> condition) { var sw = Stopwatch.StartNew(); while (!condition() && sw.ElapsedMilliseconds < 4000) Thread.Sleep(25); Assert(condition(), "Timed out waiting for test action"); }
        static void SendTest(bool up) { SendTestKey(0x74, up); }
        static void SendTestKey(int key, bool up) { var a = new[] { Native.Key(key, up, Native.TestTag) }; Assert(Native.SendInput(1, a, Marshal.SizeOf(typeof(Native.Input))) == 1, "test SendInput failed"); }
        sealed class NativeObserver : IDisposable
        {
            readonly Thread thread; readonly ManualResetEvent ready = new ManualResetEvent(false); readonly Native.HookProc callback; Control control; IntPtr hook;
            public int CapsLock, MediaPlayPause, F5, F13, F6Down, F6Up, CallbackCount, TaggedCount;
            public int CallbackCountValue { get { return Volatile.Read(ref CallbackCount); } }
            public int TaggedCountValue { get { return Volatile.Read(ref TaggedCount); } }
            public int CapsLockCount { get { return Volatile.Read(ref CapsLock); } }
            public int MediaPlayPauseCount { get { return Volatile.Read(ref MediaPlayPause); } }
            public int F5Count { get { return Volatile.Read(ref F5); } }
            public int F13Count { get { return Volatile.Read(ref F13); } }
            public int F6DownCount { get { return Volatile.Read(ref F6Down); } }
            public int F6UpCount { get { return Volatile.Read(ref F6Up); } }
            public void Clear() { Interlocked.Exchange(ref CapsLock, 0); Interlocked.Exchange(ref MediaPlayPause, 0); Interlocked.Exchange(ref F5, 0); Interlocked.Exchange(ref F13, 0); Interlocked.Exchange(ref F6Down, 0); Interlocked.Exchange(ref F6Up, 0); Interlocked.Exchange(ref CallbackCount, 0); Interlocked.Exchange(ref TaggedCount, 0); }
            public NativeObserver()
            {
                callback = Observe; thread = new Thread(delegate() {
                    control = new Control(); var h = control.Handle; hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
                    if (hook == IntPtr.Zero) { ready.Set(); control.Dispose(); return; }
                    EventHandler messageLoopReady = null;
                    messageLoopReady = delegate { Application.Idle -= messageLoopReady; ready.Set(); };
                    Application.Idle += messageLoopReady;
                    try { Application.Run(); }
                    finally { Application.Idle -= messageLoopReady; Native.UnhookWindowsHookEx(hook); control.Dispose(); }
                }) { IsBackground = true };
                thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert(ready.WaitOne(3000) && hook != IntPtr.Zero, "observer installation");
            }
            IntPtr Observe(int code, IntPtr w, IntPtr l)
            {
                if (code >= 0) { Interlocked.Increment(ref CallbackCount); var k = (Native.KeyboardData)Marshal.PtrToStructure(l, typeof(Native.KeyboardData)); if (k.ExtraInfo == Native.TestTag) Interlocked.Increment(ref TaggedCount); if (k.ExtraInfo == Native.TestTag && k.Vk == (int)Keys.CapsLock) { Interlocked.Increment(ref CapsLock); return new IntPtr(1); } if (k.ExtraInfo == Native.TestTag && k.Vk == (int)Keys.MediaPlayPause) { Interlocked.Increment(ref MediaPlayPause); return new IntPtr(1); } if (k.ExtraInfo == Native.TestTag && k.Vk == 0x74) { Interlocked.Increment(ref F5); return new IntPtr(1); } if (k.ExtraInfo == Native.TestTag && k.Vk == (int)Keys.F13) { Interlocked.Increment(ref F13); return new IntPtr(1); } if (k.ExtraInfo == Native.Tag && k.Vk == 0x75) { if ((k.Flags & 0x80) == 0) Interlocked.Increment(ref F6Down); else Interlocked.Increment(ref F6Up); return new IntPtr(1); } }
                return Native.CallNextHookEx(hook, code, w, l);
            }
            public void Dispose() { control.BeginInvoke((Action)delegate { Application.ExitThread(); }); thread.Join(2000); ready.Dispose(); }
        }
    }
}
