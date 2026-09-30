using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class FakeSink : IActionSink
    {
        public readonly List<int[]> Keys = new List<int[]>();
        public readonly List<ProcessStartInfo> Launches = new List<ProcessStartInfo>();
        public void Send(int[] keys) { Keys.Add(keys); }
        public void Launch(ProcessStartInfo p) { Launches.Add(p); }
    }
    internal static class Tests
    {
        static int passed, failed; static string scratch;
        static void Assert(bool condition, string detail) { if (!condition) throw new Exception(detail); }
        static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } if (!rejected) throw new Exception("Expected rejection"); }
        static void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); } }
        static Configuration Active(ActionKind kind, string target) { var c = new Configuration { Enabled = true }; c.Mappings[4] = new Mapping { Kind = kind, Target = target }; return c; }
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--probe") { File.AppendAllText(args[1], "launched\r\n"); return 0; }
            scratch = Path.Combine(Path.GetTempPath(), "FunctionRowRemapper-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch);
            try {
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
            Test("Malformed and reserved shortcuts rejected", delegate { foreach (string s in new[] { "", "Ctrl++A", "Ctrl+Ctrl+A", "Ctrl", "Ctrl+Alt+Delete", "Ctrl+Banana", "MouseButtons", "123", "Shift+Alt" }) Reject(() => Shortcuts.Parse(s, false)); Reject(() => Shortcuts.Parse("Ctrl+A", true)); });
            Test("Custom hotkeys normalize and require a modifier", delegate { Assert(HotkeyChord.Normalize("shift + win + l") == "Shift+Win+L", "normalize"); Reject(() => HotkeyChord.Parse("K")); });
            Test("Custom hotkeys and Python scripts round-trip", delegate { var c = new Configuration { CustomHotkeys = new[] { new CustomHotkey { Shortcut = "Ctrl+Alt+P", Action = new Mapping { Kind = ActionKind.Python, Target = Path.Combine(scratch, "hello.py"), Arguments = "--fast", WorkingDirectory = scratch } }, new CustomHotkey { Shortcut = "Shift+Win+L", Action = new Mapping { Kind = ActionKind.LockThenSleep } } } }; string json = ConfigStore.Serialize(c); var loaded = ConfigStore.Parse(json); Assert(json.Contains("\"version\": 3") && loaded.CustomHotkeys[0].Action.Kind == ActionKind.Python && loaded.CustomHotkeys[1].Action.Kind == ActionKind.LockThenSleep, "v3"); });
            Test("Action sequences preserve order and waits", delegate { var steps = new[] { new SequenceStep { Action = new Mapping { Kind = ActionKind.SendShortcut, Target = "Win+E" } }, new SequenceStep { WaitMilliseconds = 750 }, new SequenceStep { Action = new Mapping { Kind = ActionKind.Media, Target = "VolumeMute" } } }; string text = SequenceCodec.Serialize(steps); var parsed = SequenceCodec.Parse(text); Assert(parsed.Count == 3 && parsed[1].WaitMilliseconds == 750 && parsed[2].Action.Target == "VolumeMute", "sequence"); });
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
            Test("Tray preference defaults on and persists independently", delegate { string path = Path.Combine(scratch,"preferences.json"); Assert(UserPreferences.Load(path).UseTray,"default"); UserPreferences.Save(path,new UserPreferences {UseTray=false}); Assert(!UserPreferences.Load(path).UseTray,"off"); UserPreferences.Save(path,new UserPreferences {UseTray=true}); Assert(UserPreferences.Load(path).UseTray && !UserPreferences.Load(path+".bak").UseTray,"on and backup"); });
            Test("Tray preferences reject malformed and ambiguous input", delegate { foreach (string json in new[] {"{}", "{\"version\":2,\"useTray\":true}", "{\"version\":1,\"useTray\":\"false\"}", "{\"version\":1,\"useTray\":true,\"useTray\":false}", "{\"version\":1,\"useTray\":true,\"command\":\"x\"}"}) Reject(() => UserPreferences.Parse(json)); });
            Test("Corrupt preferences remain untouched on load failure", delegate { string path=Path.Combine(scratch,"corrupt-preferences.json"); File.WriteAllText(path,"{broken"); Reject(() => UserPreferences.Load(path)); Assert(File.ReadAllText(path)=="{broken","preserved"); });
        }
        static Configuration MonitorConfig() { var c = new Configuration(); c.Mappings[0] = new Mapping { Kind = ActionKind.Monitor, MonitorId = new string('a',64), MonitorControl = "VolumeDown", MonitorStep = 5 }; return c; }
        sealed class FakeDdc : IDdcController { public int Calls; public bool Allowed; public Mapping Last; public void Apply(Mapping m, Func<bool> active) { Calls++; Last = m; Allowed = active(); } }
        static void NativeTests()
        {
            // A lower observation hook swallows the test F5 events so no foreground application sees them.
            using (var observer = new NativeObserver()) {
                Test("Production hook ignores injected F5", delegate { using (var engine = new KeyboardEngine(new WindowsActionSink(), false)) { engine.Apply(Active(ActionKind.Unbound, "")); observer.Clear(); SendTest(false); SendTest(true); Thread.Sleep(150); Assert(observer.F5 == 2, "production injection must pass"); } });
                Test("Native hook suppresses marked test F5 and emits one F6 pair", delegate { using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(Active(ActionKind.SendKey, "F6")); observer.Clear(); SendTest(false); Thread.Sleep(120); for (int i = 0; i < 8; i++) SendTest(false); SendTest(true); Thread.Sleep(150); Assert(observer.F5 == 0 && observer.F6Down == 1 && observer.F6Up == 1, "F5=" + observer.F5 + ", F6=" + observer.F6Down + "/" + observer.F6Up); } });
                Test("Native Unbound swallows both edges; disable restores pass-through", delegate { using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(Active(ActionKind.Unbound, "")); observer.Clear(); SendTest(false); SendTest(true); Thread.Sleep(100); Assert(observer.F5 == 0, "unbound"); engine.SetEnabled(false); SendTest(false); SendTest(true); Thread.Sleep(100); Assert(observer.F5 == 2, "disable"); } });
                Test("Native held key launches exactly one real process", delegate { string marker = Path.Combine(scratch, "launch-marker.txt"); var c = Active(ActionKind.Application, Process.GetCurrentProcess().MainModule.FileName); c.Mappings[4].Arguments = "--probe \"" + marker + "\""; using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(c); observer.Clear(); SendTest(false); for (int i = 0; i < 20; i++) SendTest(false); SendTest(true); WaitFor(() => File.Exists(marker)); Thread.Sleep(200); Assert(File.ReadAllLines(marker).Length == 1 && observer.F5 == 0, "launch count/suppression"); } });
                Test("Native pass-through and disposal restore both edges", delegate { using (var engine = new KeyboardEngine(new WindowsActionSink(), true)) { engine.Apply(new Configuration { Enabled = true }); observer.Clear(); SendTest(false); SendTest(true); Thread.Sleep(100); Assert(observer.F5 == 2, "pass through"); engine.Apply(Active(ActionKind.Unbound, "")); } observer.Clear(); SendTest(false); SendTest(true); Thread.Sleep(100); Assert(observer.F5 == 2, "after disposal"); });
            }
        }
        static void WaitFor(Func<bool> condition) { var sw = Stopwatch.StartNew(); while (!condition() && sw.ElapsedMilliseconds < 4000) Thread.Sleep(25); Assert(condition(), "Timed out waiting for test action"); }
        static void SendTest(bool up) { var a = new[] { Native.Key(0x74, up, Native.TestTag) }; Assert(Native.SendInput(1, a, Marshal.SizeOf(typeof(Native.Input))) == 1, "test SendInput failed"); }
        sealed class NativeObserver : IDisposable
        {
            readonly Thread thread; readonly ManualResetEvent ready = new ManualResetEvent(false); readonly Native.HookProc callback; Control control; IntPtr hook;
            public int F5, F6Down, F6Up;
            public void Clear() { F5 = F6Down = F6Up = 0; }
            public NativeObserver()
            {
                callback = Observe; thread = new Thread(delegate() { control = new Control(); var h = control.Handle; hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0); ready.Set(); Application.Run(); Native.UnhookWindowsHookEx(hook); control.Dispose(); }) { IsBackground = true };
                thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert(ready.WaitOne(3000) && hook != IntPtr.Zero, "observer installation");
            }
            IntPtr Observe(int code, IntPtr w, IntPtr l)
            {
                if (code >= 0) { var k = (Native.KeyboardData)Marshal.PtrToStructure(l, typeof(Native.KeyboardData)); if (k.ExtraInfo == Native.TestTag && k.Vk == 0x74) { Interlocked.Increment(ref F5); return new IntPtr(1); } if (k.ExtraInfo == Native.Tag && k.Vk == 0x75) { if ((k.Flags & 0x80) == 0) Interlocked.Increment(ref F6Down); else Interlocked.Increment(ref F6Up); return new IntPtr(1); } }
                return Native.CallNextHookEx(hook, code, w, l);
            }
            public void Dispose() { control.BeginInvoke((Action)delegate { Application.ExitThread(); }); thread.Join(2000); ready.Dispose(); }
        }
    }
}
