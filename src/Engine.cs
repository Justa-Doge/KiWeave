using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    // No full keyboard history is retained. State consists only of current mapped F-key/media-button
    // presses and the currently held layer key.
    public sealed class KeyDecision
    {
        public bool Suppress;
        public Mapping Action;
        public string LayerName = "Base";
        public string ResolvedAction = "Not handled by KiWeave";
    }
    public sealed class KeyDiagnostic
    {
        public int VirtualKey;
        public string KeyName = "", Modifiers = "None", Source = "Physical", LayerName = "Base", ResolvedAction = "Not handled by KiWeave", ProfileName = "Default";
        public bool Suppressed;
    }
    public sealed class KeyStateMachine
    {
        sealed class Press { public bool Suppress; public Mapping Mapping; public bool Cancelled; }
        readonly Press[] pressed = new Press[12];
        readonly Dictionary<int, Press> mediaPressed = new Dictionary<int, Press>();
        readonly Dictionary<int, Press> standalonePressed = new Dictionary<int, Press>();
        int activeLayerKey;
        bool suppressLayerRelease;
        public KeyDecision Process(int vk, bool down, bool injected, Configuration config, bool customChordWins = false, bool allowNewActions = true, bool ejectHandledByRawInput = false)
        {
            var d = new KeyDecision();
            if (injected) return d;
            ModifierLayer layerForKey = config.Layers == null ? null : config.Layers.FirstOrDefault(l => LayerKeys.VirtualKey(l.ActivationKey) == vk);
            if (layerForKey != null || (activeLayerKey == vk && suppressLayerRelease)) {
                if (down) {
                    if (layerForKey != null) { d.LayerName = layerForKey.Name; d.ResolvedAction = config.Enabled ? "Activate layer " + layerForKey.Name : "Shortcuts are paused"; }
                    if (layerForKey != null && config.Enabled && allowNewActions && (activeLayerKey == 0 || activeLayerKey == vk)) { activeLayerKey = vk; suppressLayerRelease = true; d.Suppress = true; }
                    else if (activeLayerKey == vk && suppressLayerRelease) d.Suppress = true;
                } else if (activeLayerKey == vk) {
                    d.Suppress = suppressLayerRelease; activeLayerKey = 0; suppressLayerRelease = false;
                }
                return d;
            }
            if (HotkeyChord.IsHardwareMediaKey(vk)) {
                Press mediaPress;
                if (!down) {
                    if (mediaPressed.TryGetValue(vk, out mediaPress)) { d.Suppress = mediaPress.Suppress; mediaPressed.Remove(vk); }
                    return d;
                }
                if (!mediaPressed.TryGetValue(vk, out mediaPress)) {
                    if (!allowNewActions) return d;
                    CustomHotkey hotkey = (config.CustomHotkeys ?? new CustomHotkey[0]).FirstOrDefault(h => {
                        HotkeyChord chord;
                        try { chord = HotkeyChord.Parse(h.Shortcut); } catch { return false; }
                        return chord.Modifiers == 0 && chord.Key == vk;
                    });
                    if (hotkey == null || hotkey.Action == null || hotkey.Action.Kind == ActionKind.PassThrough || !config.Enabled) return d;
                    mediaPress = new Press { Suppress = true, Mapping = hotkey.Action.Copy() };
                    mediaPressed[vk] = mediaPress; d.Suppress = true;
                    // Raw HID dispatches the Consumer-page Eject action. The
                    // keyboard hook still swallows both edges but must not queue it again.
                    if (mediaPress.Mapping.Kind != ActionKind.Unbound && !(vk == 0xB8 && ejectHandledByRawInput)) d.Action = mediaPress.Mapping;
                    d.ResolvedAction = mediaPress.Mapping.Summary; return d;
                }
                d.Suppress = mediaPress.Suppress;
                if (allowNewActions && config.Enabled && !mediaPress.Cancelled && mediaPress.Mapping.Kind != ActionKind.Unbound && mediaPress.Mapping.Repeats) d.Action = mediaPress.Mapping;
                d.ResolvedAction = mediaPress.Mapping.Summary; return d;
            }
            if (HotkeyChord.IsStandaloneKeyboardKey(vk)) {
                Press standalonePress;
                if (!down) {
                    if (standalonePressed.TryGetValue(vk, out standalonePress)) { d.Suppress = standalonePress.Suppress; standalonePressed.Remove(vk); }
                    return d;
                }
                if (!standalonePressed.TryGetValue(vk, out standalonePress)) {
                    if (!allowNewActions) return d;
                    CustomHotkey hotkey = (config.CustomHotkeys ?? new CustomHotkey[0]).FirstOrDefault(h => {
                        HotkeyChord chord;
                        try { chord = HotkeyChord.Parse(h.Shortcut); } catch { return false; }
                        return chord.Modifiers == 0 && chord.Key == vk;
                    });
                    if (hotkey == null || hotkey.Action == null || hotkey.Action.Kind == ActionKind.PassThrough || !config.Enabled) return d;
                    standalonePress = new Press { Suppress = true, Mapping = hotkey.Action.Copy() };
                    standalonePressed[vk] = standalonePress; d.Suppress = true;
                    if (standalonePress.Mapping.Kind != ActionKind.Unbound) d.Action = standalonePress.Mapping;
                    d.ResolvedAction = standalonePress.Mapping.Summary; return d;
                }
                d.Suppress = standalonePress.Suppress;
                if (allowNewActions && config.Enabled && !standalonePress.Cancelled && standalonePress.Mapping.Kind != ActionKind.Unbound && standalonePress.Mapping.Repeats) d.Action = standalonePress.Mapping;
                d.ResolvedAction = standalonePress.Mapping.Summary; return d;
            }
            if (vk < 0x70 || vk > 0x7B) return d;
            int i = vk - 0x70; Press p = pressed[i];
            if (!down) {
                if (p != null) { d.Suppress = p.Suppress; pressed[i] = null; }
                return d;
            }
            if (p == null) {
                if (!allowNewActions) return d;
                if (customChordWins) {
                    pressed[i] = new Press { Suppress = false, Mapping = new Mapping { Kind = ActionKind.PassThrough } };
                    d.ResolvedAction = "Custom hotkey chord"; return d;
                }
                ModifierLayer activeLayer = activeLayerKey == 0 || config.Layers == null ? null : config.Layers.FirstOrDefault(l => LayerKeys.VirtualKey(l.ActivationKey) == activeLayerKey);
                Mapping m = activeLayer == null ? config.Mappings[i] : activeLayer.Mappings[i];
                d.LayerName = activeLayer == null ? "Base" : activeLayer.Name; d.ResolvedAction = config.Enabled ? m.Summary : "Shortcuts are paused";
                p = new Press { Suppress = config.Enabled && m.Kind != ActionKind.PassThrough, Mapping = m.Copy() };
                pressed[i] = p;
                if (p.Suppress && m.Kind != ActionKind.Unbound) d.Action = p.Mapping;
            }
            else if (allowNewActions && p.Suppress && config.Enabled && !p.Cancelled && p.Mapping.Repeats) d.Action = p.Mapping;
            d.Suppress = p.Suppress; return d;
        }
        public string ActiveLayerName(Configuration config)
        {
            ModifierLayer active = activeLayerKey == 0 || config.Layers == null ? null : config.Layers.FirstOrDefault(l => LayerKeys.VirtualKey(l.ActivationKey) == activeLayerKey);
            return active == null ? "Base" : active.Name;
        }
        public void CancelHeldActions() { foreach (Press p in pressed) if (p != null) p.Cancelled = true; foreach (Press p in mediaPressed.Values) p.Cancelled = true; foreach (Press p in standalonePressed.Values) p.Cancelled = true; }
    }

    internal sealed class EjectDispatchGate
    {
        readonly object gate = new object();
        long lastDispatch;
        internal bool TryEnter(long timestamp, long frequency)
        {
            if (timestamp <= 0 || frequency <= 0) throw new ArgumentOutOfRangeException();
            lock (gate) {
                long window = Math.Max(1, frequency / 10); // Coalesce the raw-HID, VK and app-command reports of one press.
                if (lastDispatch != 0 && (timestamp <= lastDispatch || timestamp - lastDispatch < window)) return false;
                lastDispatch = timestamp;
                return true;
            }
        }
        internal void Reset(long timestamp) { lock (gate) if (lastDispatch == timestamp) lastDispatch = 0; }
    }

    public sealed class EmergencyHold
    {
        long since = -1; bool fired;
        public bool Tick(bool chordDown, long milliseconds)
        {
            if (!chordDown) { since = -1; fired = false; return false; }
            if (since < 0) since = milliseconds;
            if (!fired && milliseconds - since >= 1500) { fired = true; return true; }
            return false;
        }
    }

    public interface IActionSink { void Send(int[] keys); void Launch(ProcessStartInfo info); }
    internal interface IProfileActionSink { void ActivateProfile(string name); }
    public sealed class WindowsActionSink : IActionSink, IProfileActionSink
    {
        readonly Action<string> profileActivation;
        public WindowsActionSink() { }
        internal WindowsActionSink(Action<string> profileActivation) { this.profileActivation = profileActivation; }
        public void Send(int[] keys) { Native.SendChord(keys); }
        public void Launch(ProcessStartInfo info) { using (Process p = Process.Start(info)) { } }
        void IProfileActionSink.ActivateProfile(string name)
        {
            if (profileActivation == null) throw new InvalidOperationException("Profile switching is unavailable in this KiWeave session.");
            profileActivation(name);
        }
    }
    public sealed class ActionDispatcher
    {
        readonly IActionSink sink;
        readonly IDdcController monitors;
        readonly Func<bool> lockWorkstation;
        readonly Action suspendWorkstation;
        readonly Action<bool> mediaToggleForSelfTest;
        public ActionDispatcher(IActionSink sink) : this(sink, DdcService.Shared, null) { }
        public ActionDispatcher(IActionSink sink, IDdcController monitors) : this(sink, monitors, null) { }
        internal ActionDispatcher(IActionSink sink, IDdcController monitors, Action<bool> mediaToggleForSelfTest)
            : this(sink, monitors, Native.LockWorkStation, delegate { Native.SetSuspendState(false, false, false); }, mediaToggleForSelfTest) { }
        internal ActionDispatcher(IActionSink sink, IDdcController monitors, Func<bool> lockWorkstation, Action suspendWorkstation)
            : this(sink, monitors, lockWorkstation, suspendWorkstation, null) { }
        internal ActionDispatcher(IActionSink sink, IDdcController monitors, Func<bool> lockWorkstation, Action suspendWorkstation, Action<bool> mediaToggleForSelfTest)
        {
            if (sink == null) throw new ArgumentNullException("sink");
            if (monitors == null) throw new ArgumentNullException("monitors");
            if (lockWorkstation == null) throw new ArgumentNullException("lockWorkstation");
            if (suspendWorkstation == null) throw new ArgumentNullException("suspendWorkstation");
            this.sink = sink; this.monitors = monitors; this.lockWorkstation = lockWorkstation; this.suspendWorkstation = suspendWorkstation;
            this.mediaToggleForSelfTest = mediaToggleForSelfTest;
        }
        internal static bool RequiresMonitorQueue(Mapping mapping)
        {
            if (mapping == null) return false;
            if (mapping.Kind == ActionKind.Monitor) return true;
            if (mapping.Kind == ActionKind.Sequence) {
                try { return SequenceCodec.Parse(mapping.Target).Any(step => !step.IsWait && RequiresMonitorQueue(step.Action)); }
                catch { return false; }
            }
            if (mapping.Kind == ActionKind.Conditional) {
                try { var rule = ConditionalCodec.Parse(mapping.Target); return RequiresMonitorQueue(rule.WhenMatched) || RequiresMonitorQueue(rule.Otherwise); }
                catch { return false; }
            }
            return false;
        }
        public void Execute(Mapping m) { Execute(m, () => true); }
        public void Execute(Mapping m, Func<bool> stillActive)
        {
            if (stillActive == null) stillActive = delegate { return true; };
            if (!stillActive() && (m == null || m.Kind != ActionKind.Monitor)) return;
            ActionExplanation.Record(m, "action dispatcher");
            if (!FeatureFlags.Allows(m, delegate { return FeatureFlags.Load().ExperimentalEnabled; })) throw new InvalidOperationException("Experimental actions are disabled in Settings.");
            ConfigStore.Validate(m, true);
            if (m.Kind == ActionKind.Monitor) { monitors.Apply(m, stillActive); return; }
            if (!stillActive()) return;
            if (m.Kind == ActionKind.Conditional) {
                var rule = ConditionalCodec.Parse(m.Target); Execute(ConditionalActions.Matches(rule) ? rule.WhenMatched : rule.Otherwise, stillActive); return;
            }
            if (m.Kind == ActionKind.SystemAction) { SystemActions.Execute(m.Target, sink, mediaToggleForSelfTest); return; }
            if (m.Kind == ActionKind.HttpRequest) { SystemActions.HttpRequest(m, stillActive); return; }
            if (m.Kind == ActionKind.PassThrough || m.Kind == ActionKind.Unbound) return;
            if (m.Kind == ActionKind.LockThenSleep) { RunLockThenSleep(lockWorkstation, suspendWorkstation, stillActive); return; }
            if (m.Kind == ActionKind.Sequence) { foreach (var step in SequenceCodec.Parse(m.Target)) { if (!stillActive()) return; if (step.IsWait) { if (!WaitWhileActive(step.WaitMilliseconds, stillActive)) return; } else Execute(step.Action, stillActive); } return; }
            if (m.Kind == ActionKind.SendKey || m.Kind == ActionKind.SendShortcut || m.Kind == ActionKind.Media) {
                if (m.Kind == ActionKind.Media) { SystemActions.ExecuteMedia(m.Target, sink, mediaToggleForSelfTest); return; }
                sink.Send(m.Kind == ActionKind.SendKey ? Shortcuts.ParseSendKey(m.Target) : Shortcuts.Parse(m.Target, false)); return;
            }
            sink.Launch(BuildLaunch(m));
        }
        internal static bool RunLockThenSleep(Func<bool> lockWorkstation, Action suspend, Func<bool> stillActive)
        {
            return RunLockThenSleep(750, lockWorkstation, suspend, stillActive);
        }
        internal static bool RunLockThenSleep(int delayMilliseconds, Func<bool> lockWorkstation, Action suspend, Func<bool> stillActive)
        {
            if (lockWorkstation == null) throw new ArgumentNullException("lockWorkstation");
            if (suspend == null) throw new ArgumentNullException("suspend");
            if (delayMilliseconds < 0) throw new ArgumentOutOfRangeException("delayMilliseconds");
            if (stillActive == null) stillActive = delegate { return true; };
            if (!lockWorkstation() || !WaitWhileActive(delayMilliseconds, stillActive)) return false;
            suspend();
            return true;
        }
        internal static bool WaitWhileActive(int milliseconds, Func<bool> stillActive)
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            while (elapsed.ElapsedMilliseconds < milliseconds) {
                if (!stillActive()) return false;
                int remaining = milliseconds - (int)elapsed.ElapsedMilliseconds;
                Thread.Sleep(Math.Min(25, Math.Max(1, remaining)));
            }
            return stillActive();
        }
        public static ProcessStartInfo BuildLaunch(Mapping m)
        {
            var p = new ProcessStartInfo { FileName = m.Target, Arguments = m.Arguments, WorkingDirectory = m.WorkingDirectory, UseShellExecute = true };
            if (m.Kind == ActionKind.Command) {
                p.UseShellExecute = false; p.CreateNoWindow = true; p.WindowStyle = ProcessWindowStyle.Hidden;
                string ext = Path.GetExtension(m.Target).ToLowerInvariant();
                if (ext == ".ps1") {
                    p.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
                    p.Arguments = "-NoLogo -NoProfile -NonInteractive -File \"" + m.Target + "\" " + m.Arguments;
                } else if (ext == ".cmd" || ext == ".bat") {
                    p.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                    p.Arguments = "/d /s /c \"\"" + m.Target + "\" " + m.Arguments + "\"";
                }
            } else if (m.Kind == ActionKind.Python) {
                p.UseShellExecute = false; p.CreateNoWindow = true; p.WindowStyle = ProcessWindowStyle.Hidden;
                p.FileName = "py.exe";
                p.Arguments = "-3 \"" + m.Target + "\" " + m.Arguments;
            }
            return p;
        }
    }

    public sealed class KeyboardEngine : IDisposable
    {
        sealed class Job { public Mapping Mapping; public int Generation; }
        readonly BlockingCollection<Job> queue = new BlockingCollection<Job>(24);
        readonly BlockingCollection<Job> monitorQueue = new BlockingCollection<Job>(1);
        readonly Thread hookThread, workerThread, monitorThread;
        readonly ManualResetEvent ready = new ManualResetEvent(false);
        readonly KeyStateMachine machine = new KeyStateMachine();
        readonly ActionDispatcher dispatcher;
        readonly EjectDispatchGate ejectDispatchGate = new EjectDispatchGate();
        readonly bool testInjected;
        readonly Action beforeHookStartupForTest, hookThreadExitedForTest;
        readonly int hookStartupTimeoutMilliseconds;
        readonly Native.HookProc callback;
        Configuration config = new Configuration();
        int[] customChordPrecedence = new int[0];
        Control control; IntPtr hook; Exception startupError;
        volatile bool stopping, installed, ejectHandledByRawInput; int generation, queueSaturationReported, monitorQueueSaturationReported, stopSignaled, readyDisposed;
        public event Action EmergencyDisabled;
        public event Action<string> Error;
        public event Action<KeyDiagnostic> KeyObserved;
        public bool Installed { get { return installed; } }
        public bool Enabled { get { return Volatile.Read(ref config).Enabled; } }
        public void SetEjectHandledByRawInput(bool value) { ejectHandledByRawInput = value; }
        internal void SetCustomChordPrecedence(int[] chordKeys) { Volatile.Write(ref customChordPrecedence, chordKeys == null ? new int[0] : (int[])chordKeys.Clone()); }
        public string ActiveLayerName { get { return machine.ActiveLayerName(Volatile.Read(ref config)); } }
        public KeyboardEngine() : this(new WindowsActionSink(), false) { }
        public KeyboardEngine(Action<string> profileActivation) : this(new WindowsActionSink(profileActivation), false) { }
        // Test-only injection seam. The shipping application never enables it.
        internal KeyboardEngine(IActionSink sink, bool testInjected, IDdcController monitors = null)
            : this(sink, testInjected, monitors, Native.LockWorkStation, delegate { Native.SetSuspendState(false, false, false); }) { }
        // Test-only injection seam for exercising sensitive power actions without locking or suspending the test machine.
        internal KeyboardEngine(IActionSink sink, bool testInjected, IDdcController monitors, Func<bool> lockWorkstation, Action suspendWorkstation)
            : this(sink, testInjected, monitors, lockWorkstation, suspendWorkstation, null, 5000, null) { }
        // Test seam for deterministically exercising the bounded startup-timeout cleanup path.
        internal KeyboardEngine(IActionSink sink, bool testInjected, IDdcController monitors, Func<bool> lockWorkstation, Action suspendWorkstation, Action beforeHookStartupForTest, int hookStartupTimeoutMilliseconds, Action hookThreadExitedForTest)
        {
            if (hookStartupTimeoutMilliseconds <= 0) throw new ArgumentOutOfRangeException("hookStartupTimeoutMilliseconds");
            // Prime filesystem and foreground policy before installing the time-sensitive input hook.
            GameMode.RefreshEnabledState();
            ShortcutSuspension.RefreshForegroundState();
            this.testInjected = testInjected; this.beforeHookStartupForTest = beforeHookStartupForTest; this.hookThreadExitedForTest = hookThreadExitedForTest; this.hookStartupTimeoutMilliseconds = hookStartupTimeoutMilliseconds;
            dispatcher = new ActionDispatcher(sink, monitors ?? DdcService.Shared, lockWorkstation, suspendWorkstation); callback = OnKey;
            workerThread = new Thread(Work) { IsBackground = true, Name = "Action dispatcher" }; workerThread.SetApartmentState(ApartmentState.STA);
            monitorThread = new Thread(delegate() { WorkQueue(monitorQueue); }) { IsBackground = true, Name = "DDC monitor dispatcher" };
            hookThread = new Thread(RunHook) { IsBackground = true, Name = "Function row hook" }; hookThread.SetApartmentState(ApartmentState.STA);
            workerThread.Start(); monitorThread.Start(); hookThread.Start();
            if (!ready.WaitOne(hookStartupTimeoutMilliseconds)) { Dispose(); throw new InvalidOperationException("The keyboard hook did not start within five seconds."); }
            if (startupError != null) { Dispose(); throw new InvalidOperationException("Could not install the keyboard hook.", startupError); }
        }
        public void Apply(Configuration c)
        {
            ConfigStore.Validate(c, false); Configuration snapshot = c.Copy();
            if (control == null || stopping) return;
            control.Invoke((Action)delegate { machine.CancelHeldActions(); Interlocked.Increment(ref generation); Volatile.Write(ref config, snapshot); });
        }
        public void SetEnabled(bool value) { Configuration c = Volatile.Read(ref config).Copy(); c.Enabled = value; Apply(c); }
        public bool TriggerFunctionKey(int functionKey)
        {
            if (functionKey < 1 || functionKey > 12 || stopping || !Enabled || ShortcutSuspension.IsSuspended()) return false;
            var snapshot = Volatile.Read(ref config); var mappings = snapshot.Mappings ?? new Mapping[0]; if (mappings.Length < functionKey || mappings[functionKey - 1] == null) return false;
            Mapping mapping = mappings[functionKey - 1];
            return TryQueue(mapping, true);
        }
        public bool TriggerCustomHotkey(string shortcut)
        {
            if (String.IsNullOrWhiteSpace(shortcut) || stopping || !Enabled || ShortcutSuspension.IsSuspended()) return false;
            var snapshot = Volatile.Read(ref config);
            var hotkey = HotkeyChord.FindCustomHotkey(snapshot, shortcut);
            if (hotkey == null || hotkey.Action == null) return false;
            if (hotkey.Action.Kind == ActionKind.PassThrough) return false;
            if (hotkey.Action.Kind == ActionKind.Unbound) return true;
            var mapping = hotkey.Action.Copy();
            return String.Equals(HotkeyChord.Normalize(hotkey.Shortcut), "MediaEject", StringComparison.OrdinalIgnoreCase) ? TryQueueEject(mapping) : TryQueue(mapping, true);
        }
        bool TryQueueEject(Mapping mapping)
        {
            long timestamp = Stopwatch.GetTimestamp();
            if (!ejectDispatchGate.TryEnter(timestamp, Stopwatch.Frequency)) return true;
            bool queued = TryQueue(mapping, true);
            if (!queued) ejectDispatchGate.Reset(timestamp);
            return queued;
        }
        bool TryQueue(Mapping mapping, bool reportSaturation)
        {
            bool monitorAction = ActionDispatcher.RequiresMonitorQueue(mapping);
            BlockingCollection<Job> destination = monitorAction ? monitorQueue : queue;
            bool queued;
            try { queued = destination.TryAdd(new Job { Mapping = mapping.Copy(), Generation = Volatile.Read(ref generation) }); }
            catch (InvalidOperationException) { return false; } // A producer can race Dispose/CompleteAdding.
            if (queued) {
                if (monitorAction) Interlocked.Exchange(ref monitorQueueSaturationReported, 0);
                else Interlocked.Exchange(ref queueSaturationReported, 0);
            } else if (reportSaturation && (monitorAction
                ? Interlocked.CompareExchange(ref monitorQueueSaturationReported, 1, 0)
                : Interlocked.CompareExchange(ref queueSaturationReported, 1, 0)) == 0) {
                ThreadPool.QueueUserWorkItem(delegate { Report("KiWeave is busy; a triggered action was skipped. Try again when the current action finishes."); });
            }
            return queued;
        }
        void Report(string text) { var e = Error; if (e != null) try { e(text); } catch { } }
        void Work() { WorkQueue(queue); }
        void WorkQueue(BlockingCollection<Job> jobs)
        {
            foreach (Job job in jobs.GetConsumingEnumerable()) {
                if (stopping || job.Generation != Volatile.Read(ref generation) || !Enabled) continue;
                try { dispatcher.Execute(job.Mapping, () => !stopping && job.Generation == Volatile.Read(ref generation) && Enabled); } catch (Exception ex) { Report(ex.Message); }
            }
        }
        void RunHook()
        {
            System.Windows.Forms.Timer timer = null;
            EventHandler messageLoopReady = null;
            try {
                if (beforeHookStartupForTest != null) beforeHookStartupForTest();
                control = new Control(); var handle = control.Handle;
                hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
                if (hook == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                installed = true;
                var emergency = new EmergencyHold(); var clock = Stopwatch.StartNew();
                timer = new System.Windows.Forms.Timer { Interval = 50 };
                timer.Tick += delegate {
                    if (emergency.Tick(Native.IsDown(0x11) && Native.IsDown(0x12) && Native.IsDown(0x10), clock.ElapsedMilliseconds)) {
                        var c = Volatile.Read(ref config).Copy(); c.Enabled = false;
                        machine.CancelHeldActions(); Interlocked.Increment(ref generation); Volatile.Write(ref config, c);
                        NotifyEmergencyDisabled();
                    }
                };
                timer.Start();
                if (!stopping) {
                    messageLoopReady = delegate { Application.Idle -= messageLoopReady; ready.Set(); };
                    Application.Idle += messageLoopReady;
                    Application.Run();
                } else ready.Set();
            }
            catch (Exception ex) { startupError = ex; ready.Set(); if (!stopping) Report(ex.Message); }
            finally {
                if (messageLoopReady != null) Application.Idle -= messageLoopReady;
                installed = false;
                if (timer != null) timer.Dispose();
                if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
                if (control != null) control.Dispose();
                if (hookThreadExitedForTest != null) try { hookThreadExitedForTest(); } catch { }
            }
        }
        IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0 || stopping) return Native.CallNextHookEx(hook, code, wParam, lParam);
            try {
                int msg = wParam.ToInt32();
                if (msg != 0x100 && msg != 0x104 && msg != 0x101 && msg != 0x105) return Native.CallNextHookEx(hook, code, wParam, lParam);
                var k = (Native.KeyboardData)Marshal.PtrToStructure(lParam, typeof(Native.KeyboardData));
                bool injected = (k.Flags & 0x12) != 0;
                bool ignore = k.ExtraInfo == Native.Tag || (injected && (!testInjected || k.ExtraInfo != Native.TestTag));
                bool down = msg == 0x100 || msg == 0x104;

                Configuration snapshot = Volatile.Read(ref config);
                bool allowNewActions = ignore || (!ShortcutSuspension.IsSuspendedCached() && !(GameMode.EnabledCached && GameMode.IsFullscreenForegroundCached && !GameMode.AllowsControl(snapshot, (int)k.Vk)));
                int virtualKey = (int)k.Vk;
                bool customChordWins = virtualKey >= 0x70 && virtualKey <= 0x7B && allowNewActions && !ignore && HotkeyChord.HasMatchingModifierChord(Volatile.Read(ref customChordPrecedence), virtualKey, Native.IsDown);
                KeyDecision d = machine.Process(virtualKey, down, ignore, snapshot, customChordWins, allowNewActions, ejectHandledByRawInput);
                var observed = KeyObserved;
                if (observed != null && down) {
                    string modifiers = String.Join("+", new[] { Native.IsDown(0x11) ? "Ctrl" : "", Native.IsDown(0x12) ? "Alt" : "", Native.IsDown(0x10) ? "Shift" : "", (Native.IsDown(0x5B) || Native.IsDown(0x5C)) ? "Win" : "" }.Where(x => x.Length > 0));
                    var diagnostic = new KeyDiagnostic { VirtualKey = (int)k.Vk, KeyName = KeyLabel((int)k.Vk), Modifiers = modifiers.Length == 0 ? "None" : modifiers, Source = ignore ? "Injected (ignored)" : injected ? "Injected test input" : "Physical", Suppressed = d.Suppress, LayerName = d.LayerName == "Base" ? machine.ActiveLayerName(snapshot) : d.LayerName, ResolvedAction = ignore ? "Ignored injected input" : d.ResolvedAction };
                    ThreadPool.QueueUserWorkItem(delegate { try { observed(diagnostic); } catch { } });
                }
                if (d.Action != null) { if (virtualKey == 0xB8) TryQueueEject(d.Action); else TryQueue(d.Action, true); }
                if (d.Suppress) return new IntPtr(1);
            }
            catch { // Fail open, disable further actions; do not perform I/O or call UI from the hook.
                var c = Volatile.Read(ref config);
                if (c.Enabled) {
                    c = c.Copy(); c.Enabled = false;
                    machine.CancelHeldActions(); Interlocked.Increment(ref generation); Volatile.Write(ref config, c);
                    NotifyEmergencyDisabled();
                }
            }
            return Native.CallNextHookEx(hook, code, wParam, lParam);
        }
        void NotifyEmergencyDisabled()
        {
            var disabled = EmergencyDisabled;
            if (disabled != null) ThreadPool.QueueUserWorkItem(delegate { try { disabled(); } catch { } });
        }
        static string KeyLabel(int vk)
        {
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
            string name = ((Keys)vk).ToString(); return String.IsNullOrWhiteSpace(name) || Char.IsDigit(name[0]) ? "VK 0x" + vk.ToString("X2") : name;
        }
        public void Dispose()
        {
            stopping = true;
            if (Interlocked.Exchange(ref stopSignaled, 1) == 0) { Interlocked.Increment(ref generation); queue.CompleteAdding(); monitorQueue.CompleteAdding(); }
            if (control != null && !control.IsDisposed) try { control.BeginInvoke((Action)delegate { Application.ExitThread(); }); } catch (InvalidOperationException) { }
            if (Thread.CurrentThread != hookThread) hookThread.Join(2000);
            workerThread.Join(1000); monitorThread.Join(500);
            // A hook startup can still be unwinding after the bounded join. Keep
            // its readiness event alive until RunHook has stopped touching it.
            if (!hookThread.IsAlive && Interlocked.Exchange(ref readyDisposed, 1) == 0) ready.Dispose();
        }
    }

    internal static class Native
    {
        internal static readonly UIntPtr Tag = new UIntPtr(0x46525231);
        internal static readonly UIntPtr TestTag = new UIntPtr(0x46525453);
        internal delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
        [StructLayout(LayoutKind.Sequential)] internal struct KeyboardData { public uint Vk, Scan, Flags, Time; public UIntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] internal struct KeyInput { public ushort Vk, Scan; public uint Flags, Time; public UIntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] internal struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Explicit)] internal struct InputUnion { [FieldOffset(0)] public KeyInput Key; [FieldOffset(0)] public MouseInput Mouse; }
        [StructLayout(LayoutKind.Sequential)] internal struct Input { public uint Type; public InputUnion Data; }
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
        [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool LockWorkStation();
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool UnregisterHotKey(IntPtr handle, int id);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] internal static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
        [DllImport("powrprof.dll", SetLastError = true)] internal static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
        [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);
        internal static bool IsDown(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }
        internal static string ForegroundProcessName()
        {
            try {
                uint id; GetWindowThreadProcessId(GetForegroundWindow(), out id); if (id == 0) return "";
                using (var process = Process.GetProcessById((int)id)) return process.ProcessName;
            } catch { return ""; }
        }
        internal static bool TryForegroundProcessName(out string processName)
        {
            processName = "";
            try {
                IntPtr window = GetForegroundWindow();
                if (window == IntPtr.Zero) return true;
                uint id;
                if (GetWindowThreadProcessId(window, out id) == 0 || id == 0) return false;
                using (var process = Process.GetProcessById((int)id)) processName = process.ProcessName;
                return !String.IsNullOrWhiteSpace(processName);
            } catch { processName = ""; return false; }
        }
        internal static string ForegroundProcessPath()
        {
            try { uint id; GetWindowThreadProcessId(GetForegroundWindow(), out id); if (id == 0) return ""; using (var process = Process.GetProcessById((int)id)) { var module = process.MainModule; return module == null ? "" : module.FileName; } } catch { return ""; }
        }
        internal static string WindowTitle(IntPtr window)
        {
            try { if (window == IntPtr.Zero) return ""; var text = new StringBuilder(512); return GetWindowText(window, text, text.Capacity) > 0 ? text.ToString() : ""; } catch { return ""; }
        }
        internal static Input Key(int vk, bool up, UIntPtr tag)
        {
            bool extended = (vk >= 0x21 && vk <= 0x2E && vk != 0x2A && vk != 0x2B) || vk == 0x5B || vk == 0x5C || vk == 0x6F || (vk >= 0xA6 && vk <= 0xB7);
            return new Input { Type = 1, Data = new InputUnion { Key = new KeyInput { Vk = (ushort)vk, Flags = (uint)((up ? 2 : 0) | (extended ? 1 : 0)), ExtraInfo = tag } } };
        }
        internal static void SendChord(int[] keys)
        {
            var added = new List<int>(); var batch = new List<Input>();
            // Never release modifiers that the user is physically holding.
            for (int i = 0; i < keys.Length - 1; i++) if (!IsDown(keys[i])) { added.Add(keys[i]); batch.Add(Key(keys[i], false, Tag)); }
            int target = keys[keys.Length - 1];
            if (IsDown(target)) throw new InvalidOperationException("Release the destination key before triggering this mapping.");
            batch.Add(Key(target, false, Tag)); batch.Add(Key(target, true, Tag));
            for (int i = added.Count - 1; i >= 0; i--) batch.Add(Key(added[i], true, Tag));
            uint sent = SendInput((uint)batch.Count, batch.ToArray(), Marshal.SizeOf(typeof(Input)));
            if (sent != batch.Count) {
                // If only part of the batch entered the stream, release only keys it pressed.
                var down = new HashSet<int>();
                for (int i = 0; i < sent; i++) { var k = batch[i].Data.Key; if ((k.Flags & 2) == 0) down.Add(k.Vk); else down.Remove(k.Vk); }
                var cleanup = down.Select(k => Key(k, true, Tag)).ToArray();
                if (cleanup.Length > 0) SendInput((uint)cleanup.Length, cleanup, Marshal.SizeOf(typeof(Input)));
                throw new InvalidOperationException("Windows blocked keyboard input. Check whether the target app is elevated.");
            }
        }
    }
}
