using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class TriggerDispatchGate
    {
        readonly object gate = new object();
        bool closed;

        internal bool TryRun(Action callback)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            lock (gate)
            {
                if (closed) return false;
                callback();
                return true;
            }
        }

        internal void Close()
        {
            lock (gate) closed = true;
        }
    }

    public sealed class ControllerTrigger
    {
        public int UserIndex, Button, FunctionKey;
        public ControllerTrigger Copy() { return new ControllerTrigger { UserIndex = UserIndex, Button = Button, FunctionKey = FunctionKey }; }
        public bool Matches(int index, ushort buttons) { return index == UserIndex && (buttons & (1 << Button)) != 0; }
        public override string ToString() { return "Controller " + (UserIndex + 1) + " · " + ButtonName(Button) + " · F" + FunctionKey; }
        public static string ButtonName(int button) { return new[] { "DPadUp", "DPadDown", "DPadLeft", "DPadRight", "Start", "Back", "LeftThumb", "RightThumb", "LeftShoulder", "RightShoulder", "A", "B", "X", "Y" }[Math.Max(0, Math.Min(13, button))]; }
    }
    public static class ControllerTriggerStore
    {
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "controller-triggers.json"); } }
        public const int MaxTriggers = 256, MaxBytes = 65536;
        public static List<ControllerTrigger> Load() { return Load(Path); }
        internal static List<ControllerTrigger> Load(string path)
        {
            if (!File.Exists(path)) return new List<ControllerTrigger>();
            string json = AtomicFile.ReadAllTextBounded(path, MaxBytes, Encoding.UTF8); JsonSyntax.Check(json); var root = new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 8 }.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null || root.Count != 2 || !root.ContainsKey("version") || !(root["version"] is int) || (int)root["version"] != 1 || !root.ContainsKey("triggers")) throw new ArgumentException("Invalid controller trigger file.");
            var rows = root["triggers"] as object[]; if (rows == null || rows.Length > MaxTriggers) throw new ArgumentException("Controller triggers must be an array of up to " + MaxTriggers + " entries.");
            return rows.Select(item => {
                var row = item as Dictionary<string, object>; if (row == null || row.Count != 3) throw new ArgumentException("Invalid controller trigger entry.");
                string user = row.ContainsKey("userIndex") ? "userIndex" : "UserIndex", button = row.ContainsKey("button") ? "button" : "Button", key = row.ContainsKey("functionKey") ? "functionKey" : "FunctionKey";
                if (!row.ContainsKey(user) || !row.ContainsKey(button) || !row.ContainsKey(key) || !(row[user] is int) || !(row[button] is int) || !(row[key] is int)) throw new ArgumentException("Invalid controller trigger entry.");
                var trigger = new ControllerTrigger { UserIndex = (int)row[user], Button = (int)row[button], FunctionKey = (int)row[key] }; Validate(trigger); return trigger;
            }).ToList();
        }
        public static void Save(IEnumerable<ControllerTrigger> triggers) { Save(Path, triggers); }
        internal static void Save(string path, IEnumerable<ControllerTrigger> triggers)
        {
            if (triggers == null) throw new ArgumentNullException("triggers"); var list = triggers.Select(x => { if (x == null) throw new ArgumentException("A controller trigger cannot be empty."); var copy = x.Copy(); Validate(copy); return copy; }).ToArray();
            if (list.Length > MaxTriggers) throw new ArgumentException("Use up to " + MaxTriggers + " controller triggers.");
            string json = new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 8 }.Serialize(new { version = 1, triggers = list.Select(x => new { userIndex = x.UserIndex, button = x.Button, functionKey = x.FunctionKey }).ToArray() });
            if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("Controller trigger file exceeds the 64 KB safety limit.");
            string full = System.IO.Path.GetFullPath(path), directory = System.IO.Path.GetDirectoryName(full); Directory.CreateDirectory(directory); string temporary = System.IO.Path.Combine(directory, ".controller-triggers-" + Guid.NewGuid().ToString("N") + ".tmp");
            try { File.WriteAllText(temporary, json + Environment.NewLine, new UTF8Encoding(false)); if (File.Exists(full)) File.Replace(temporary, full, full + ".bak", true); else File.Move(temporary, full); } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static void Validate(ControllerTrigger t) { if (t == null || t.UserIndex < 0 || t.UserIndex > 3 || t.Button < 0 || t.Button > 13 || t.FunctionKey < 1 || t.FunctionKey > 12) throw new ArgumentException("Controller trigger values are outside the supported range."); }
    }
    internal static class ControllerPollCadence
    {
        const int DefaultRefreshRate = 60;
        const int MinimumRefreshRate = 24;
        const int MaximumRefreshRate = 500;
        const int VerticalRefreshRateCapability = 116;
        const int CurrentDisplaySettings = -1;
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct MonitorInfoEx
        {
            public int Size;
            public Native.Rect Monitor, Work;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct DisplayMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            public ushort SpecVersion, DriverVersion, Size, DriverExtra;
            public uint Fields;
            public int PositionX, PositionY;
            public uint DisplayOrientation, DisplayFixedOutput;
            public short Color, Duplex, YResolution, TTOption, Collate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FormName;
            public ushort LogPixels;
            public uint BitsPerPel, PelsWidth, PelsHeight, DisplayFlags, DisplayFrequency;
            public uint ICMMethod, ICMIntent, MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
        }
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc, int index);
        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfoEx(IntPtr monitor, ref MonitorInfoEx info);
        [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", CharSet = CharSet.Unicode)] static extern bool EnumDisplaySettings(string deviceName, int modeNumber, ref DisplayMode mode);
        internal static int NormalizeRefreshRate(int refreshRate) { return refreshRate < MinimumRefreshRate || refreshRate > MaximumRefreshRate ? DefaultRefreshRate : refreshRate; }
        internal static long Period100Nanoseconds(int refreshRate) { return Math.Max(1, (long)Math.Round(10000000.0 / NormalizeRefreshRate(refreshRate), MidpointRounding.AwayFromZero)); }
        internal static int PeriodMilliseconds(int refreshRate) { return Math.Max(1, (int)Math.Round(1000.0 / NormalizeRefreshRate(refreshRate), MidpointRounding.AwayFromZero)); }
        internal static long AdvancePollDeadline(long nowTicks, ref long nextDeadlineTicks, int refreshRate, long clockFrequency)
        {
            if (clockFrequency < 1) throw new ArgumentOutOfRangeException("clockFrequency");
            long intervalTicks = Math.Max(1, (long)Math.Round((double)clockFrequency / NormalizeRefreshRate(refreshRate), MidpointRounding.AwayFromZero));
            if (nextDeadlineTicks <= 0) nextDeadlineTicks = nowTicks;
            long next = nextDeadlineTicks + intervalTicks;
            if (next <= nowTicks)
            {
                long missedIntervals = (nowTicks - next) / intervalTicks + 1;
                next += missedIntervals * intervalTicks;
            }
            nextDeadlineTicks = next;
            return next - nowTicks;
        }
        internal static bool TryGetForegroundDisplayRefreshRate(out int refreshRate)
        {
            refreshRate = 0;
            IntPtr window = Native.GetForegroundWindow();
            try
            {
                if (window != IntPtr.Zero)
                {
                    IntPtr monitor = Native.MonitorFromWindow(window, 2);
                    var monitorInfo = new MonitorInfoEx { Size = Marshal.SizeOf(typeof(MonitorInfoEx)), Device = String.Empty };
                    if (monitor != IntPtr.Zero && GetMonitorInfoEx(monitor, ref monitorInfo) && !String.IsNullOrWhiteSpace(monitorInfo.Device))
                    {
                        var mode = new DisplayMode { Size = (ushort)Marshal.SizeOf(typeof(DisplayMode)), DeviceName = String.Empty, FormName = String.Empty };
                        if (EnumDisplaySettings(monitorInfo.Device, CurrentDisplaySettings, ref mode))
                        {
                            refreshRate = NormalizeRefreshRate((int)mode.DisplayFrequency);
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }
        internal static int CurrentRefreshRate()
        {
            int monitorRefreshRate;
            if (TryGetForegroundDisplayRefreshRate(out monitorRefreshRate)) return monitorRefreshRate;
            IntPtr window = Native.GetForegroundWindow();
            IntPtr dcWindow = window;
            IntPtr dc = IntPtr.Zero;
            try
            {
                dc = GetDC(window);
                if (dc == IntPtr.Zero && window != IntPtr.Zero) { dcWindow = IntPtr.Zero; dc = GetDC(dcWindow); }
                return NormalizeRefreshRate(dc == IntPtr.Zero ? 0 : GetDeviceCaps(dc, VerticalRefreshRateCapability));
            }
            catch { return DefaultRefreshRate; }
            finally { if (dc != IntPtr.Zero) try { ReleaseDC(dcWindow, dc); } catch { } }
        }
    }

    public sealed class ControllerTriggerService : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] struct State { public uint Packet; public ushort Buttons; public byte LeftTrigger, RightTrigger; public short LX, LY, RX, RY; }
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] static extern uint GetState(uint index, out State state);
        [DllImport("kernel32.dll", EntryPoint = "CreateWaitableTimerExW", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeWaitHandle CreateWaitableTimerEx(IntPtr attributes, string name, uint flags, uint desiredAccess);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetWaitableTimer(SafeWaitHandle timer, ref long dueTime, int period, IntPtr completion, IntPtr argument, bool resume);
        [DllImport("kernel32.dll", SetLastError = true)] static extern uint WaitForSingleObject(SafeWaitHandle handle, uint milliseconds);
        const uint CreateWaitableTimerHighResolution = 0x00000002, TimerModifyState = 0x0002, WaitObject0 = 0;
        readonly List<ControllerTrigger> triggers; readonly Action<int> fired; readonly Thread thread; readonly TriggerDispatchGate dispatchGate = new TriggerDispatchGate(); volatile bool stopping; readonly ushort[] previous = new ushort[4];
        public event Action<Exception> Error;
        public ControllerTriggerService(IEnumerable<ControllerTrigger> values, Action<int> fired, Action<Exception> errorHandler = null)
        {
            if (values == null) throw new ArgumentNullException("values"); if (fired == null) throw new ArgumentNullException("fired");
            triggers = values.Select(x => { if (x == null) throw new ArgumentException("A controller trigger cannot be empty."); var copy = x.Copy(); ControllerTriggerStore.Validate(copy); return copy; }).ToList();
            if (triggers.Count > ControllerTriggerStore.MaxTriggers) throw new ArgumentException("Use up to " + ControllerTriggerStore.MaxTriggers + " controller triggers.");
            this.fired = fired;
            if (errorHandler != null) Error += errorHandler;
            if (triggers.Count == 0) return;
            thread = new Thread(Poll) { IsBackground = true, Name = "Controller triggers" }; thread.Start();
        }
        void Poll()
        {
            SafeWaitHandle timer = CreatePollTimer();
            try
            {
                int refreshRate = ControllerPollCadence.CurrentRefreshRate();
                long nextRefreshProbe = 0;
                long nextPollDeadline = System.Diagnostics.Stopwatch.GetTimestamp();
                while (!stopping)
                {
                    long now = System.Diagnostics.Stopwatch.GetTimestamp();
                    if (now >= nextRefreshProbe) { int detectedRefreshRate = ControllerPollCadence.CurrentRefreshRate(); if (detectedRefreshRate != refreshRate) { refreshRate = detectedRefreshRate; nextPollDeadline = now; } nextRefreshProbe = now + System.Diagnostics.Stopwatch.Frequency; }
                    for (uint i = 0; i < 4; i++)
                    {
                        if (stopping) return;
                        ushort buttons; State state;
                        if (!GameInputSupport.TryReadButtons((int)i, out buttons)) { if (GetState(i, out state) != 0) { previous[i] = 0; continue; } buttons = state.Buttons; }
                        ushort rising = (ushort)(buttons & ~previous[i]); previous[i] = buttons;
                        foreach (var trigger in triggers)
                        {
                            if (stopping) return;
                            if (trigger.Matches((int)i, rising))
                            {
                                bool dispatched = false;
                                try { dispatched = dispatchGate.TryRun(delegate { fired(trigger.FunctionKey); }); } catch { }
                                if (!dispatched) return;
                            }
                        }
                    }
                    WaitForNextPoll(timer, refreshRate, ref nextPollDeadline);
                }
            }
            catch (Exception error)
            {
                stopping = true; var handler = Error;
                if (handler != null) try { handler(error); } catch { }
            }
            finally { if (timer != null) timer.Dispose(); }
        }
        static SafeWaitHandle CreatePollTimer()
        {
            try
            {
                SafeWaitHandle timer = CreateWaitableTimerEx(IntPtr.Zero, null, CreateWaitableTimerHighResolution, TimerModifyState | 0x00100000);
                if (timer != null && !timer.IsInvalid) return timer;
                if (timer != null) timer.Dispose();
                timer = CreateWaitableTimerEx(IntPtr.Zero, null, 0, TimerModifyState | 0x00100000);
                if (timer == null || timer.IsInvalid) { if (timer != null) timer.Dispose(); return null; }
                return timer;
            }
            catch { return null; }
        }
        static void WaitForNextPoll(SafeWaitHandle timer, int refreshRate, ref long nextPollDeadline)
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            long waitTicks = ControllerPollCadence.AdvancePollDeadline(now, ref nextPollDeadline, refreshRate, System.Diagnostics.Stopwatch.Frequency);
            long wait100Nanoseconds = Math.Max(1, (long)Math.Ceiling((double)waitTicks * 10000000.0 / System.Diagnostics.Stopwatch.Frequency));
            long dueTime = -wait100Nanoseconds;
            if (timer != null && !timer.IsInvalid && SetWaitableTimer(timer, ref dueTime, 0, IntPtr.Zero, IntPtr.Zero, false) && WaitForSingleObject(timer, 1000) == WaitObject0) return;
            int waitMilliseconds = Math.Max(1, (int)Math.Ceiling((double)waitTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency));
            Thread.Sleep(waitMilliseconds);
        }
        public void Dispose() { stopping = true; dispatchGate.Close(); if (thread != null) thread.Join(250); }
    }
 #if !WPF_REWRITE
    internal sealed class ControllerTriggersForm : Form
    {
        readonly ListBox list = new DesignListBox(); readonly NumericUpDown user = new DesignNumericUpDown { Minimum = 1, Maximum = 4, Value = 1 }, button = new DesignNumericUpDown { Minimum = 0, Maximum = 13 }, key = new DesignNumericUpDown { Minimum = 1, Maximum = 12, Value = 1 }; readonly List<ControllerTrigger> values;
        internal ControllerTriggersForm() { values = ControllerTriggerStore.Load(); Text = "KiWeave - Controller triggers"; Icon = Program.AppIcon(); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Font = new System.Drawing.Font("Segoe UI", 10); ClientSize = new System.Drawing.Size(700, 500); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this); var root = UiStyle.Stack(); root.Padding = new Padding(24); Controls.Add(root); root.Controls.Add(UiStyle.Text("Controller triggers", 20, true)); root.Controls.Add(UiStyle.Text("Optional local XInput input. Rising button presses invoke an existing saved function-row action; controller state is not recorded.", 9, false)); list.Height = 180; root.Controls.Add(UiStyle.Field("Saved triggers", list)); root.Controls.Add(UiStyle.Field("Controller", user)); root.Controls.Add(UiStyle.Field("Button number (0-13)", button)); root.Controls.Add(UiStyle.Field("Invoke function key", key)); var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft }; buttons.Controls.Add(UiStyle.Button("Save", delegate { try { ControllerTriggerStore.Save(values); DialogResult = DialogResult.OK; Close(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Controller triggers", MessageBoxButtons.OK, MessageBoxIcon.Error); } }, true)); buttons.Controls.Add(UiStyle.Button("Add", delegate { values.Add(new ControllerTrigger { UserIndex = (int)user.Value - 1, Button = (int)button.Value, FunctionKey = (int)key.Value }); RefreshList(); })); buttons.Controls.Add(UiStyle.Button("Delete selected", delegate { if (list.SelectedIndex >= 0) { values.RemoveAt(list.SelectedIndex); RefreshList(); } })); buttons.Controls.Add(UiStyle.Button("Done", delegate { Close(); })); root.Controls.Add(buttons); RefreshList(); }
        void RefreshList() { list.Items.Clear(); list.Items.AddRange(values.Select(x => x.ToString()).ToArray()); }
    }
 #endif
}
