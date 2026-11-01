using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    public sealed class MidiTrigger
    {
        public int Device, Channel, Number, MinimumValue = 1, FunctionKey = 1;
        public string Message = "NoteOn";
        public MidiTrigger Copy() { return new MidiTrigger { Device = Device, Channel = Channel, Number = Number, MinimumValue = MinimumValue, FunctionKey = FunctionKey, Message = Message }; }
        public bool Matches(int status, int data1, int data2) { int type = status & 0xF0, channel = status & 0x0F; return channel == Channel && data1 == Number && data2 >= MinimumValue && ((Message == "NoteOn" && type == 0x90 && data2 > 0) || (Message == "ControlChange" && type == 0xB0)); }
        public bool Matches(int device, int status, int data1, int data2) { return Device == device && Matches(status, data1, data2); }
        public override string ToString() { return "MIDI " + Device + " · " + Message + " " + Number + " · F" + FunctionKey; }
    }
    public static class MidiTriggerStore
    {
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "midi-triggers.json"); } }
        public const int MaxTriggers = 256, MaxBytes = 65536;
        public static List<MidiTrigger> Load() { return Load(Path); }
        internal static List<MidiTrigger> Load(string path)
        {
            if (!File.Exists(path)) return new List<MidiTrigger>();
            string json = AtomicFile.ReadAllTextBounded(path, MaxBytes, Encoding.UTF8); JsonSyntax.Check(json); var root = new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 8 }.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null || root.Count != 2 || !root.ContainsKey("version") || !(root["version"] is int) || (int)root["version"] != 1 || !root.ContainsKey("triggers")) throw new ArgumentException("Invalid MIDI trigger file.");
            var rows = root["triggers"] as object[]; if (rows == null || rows.Length > MaxTriggers) throw new ArgumentException("MIDI triggers must be an array of up to " + MaxTriggers + " entries.");
            return rows.Select(item => {
                var row = item as Dictionary<string, object>; if (row == null || row.Count != 6) throw new ArgumentException("Invalid MIDI trigger entry.");
                string device = row.ContainsKey("device") ? "device" : "Device", channel = row.ContainsKey("channel") ? "channel" : "Channel", number = row.ContainsKey("number") ? "number" : "Number", minimum = row.ContainsKey("minimumValue") ? "minimumValue" : "MinimumValue", key = row.ContainsKey("functionKey") ? "functionKey" : "FunctionKey", message = row.ContainsKey("message") ? "message" : "Message";
                if (!row.ContainsKey(device) || !row.ContainsKey(channel) || !row.ContainsKey(number) || !row.ContainsKey(minimum) || !row.ContainsKey(key) || !row.ContainsKey(message) || !(row[device] is int) || !(row[channel] is int) || !(row[number] is int) || !(row[minimum] is int) || !(row[key] is int) || !(row[message] is string)) throw new ArgumentException("Invalid MIDI trigger entry.");
                var trigger = new MidiTrigger { Device = (int)row[device], Channel = (int)row[channel], Number = (int)row[number], MinimumValue = (int)row[minimum], FunctionKey = (int)row[key], Message = (string)row[message] }; Validate(trigger); return trigger;
            }).ToList();
        }
        public static void Save(IEnumerable<MidiTrigger> triggers) { Save(Path, triggers); }
        internal static void Save(string path, IEnumerable<MidiTrigger> triggers)
        {
            if (triggers == null) throw new ArgumentNullException("triggers"); var list = triggers.Select(x => { if (x == null) throw new ArgumentException("A MIDI trigger cannot be empty."); var copy = x.Copy(); Validate(copy); return copy; }).ToArray();
            if (list.Length > MaxTriggers) throw new ArgumentException("Use up to " + MaxTriggers + " MIDI triggers.");
            string json = new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 8 }.Serialize(new { version = 1, triggers = list.Select(x => new { device = x.Device, channel = x.Channel, number = x.Number, minimumValue = x.MinimumValue, functionKey = x.FunctionKey, message = x.Message }).ToArray() });
            if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("MIDI trigger file exceeds the 64 KB safety limit."); SaveAtomic(path, json);
        }
        public static void Validate(MidiTrigger t) { if (t == null || t.Device < 0 || t.Device > 31 || t.Channel < 0 || t.Channel > 15 || t.Number < 0 || t.Number > 127 || t.MinimumValue < 1 || t.MinimumValue > 127 || t.FunctionKey < 1 || t.FunctionKey > 12 || (t.Message != "NoteOn" && t.Message != "ControlChange")) throw new ArgumentException("MIDI trigger values are outside the supported range."); }
        static void SaveAtomic(string path, string json) { string full = System.IO.Path.GetFullPath(path), directory = System.IO.Path.GetDirectoryName(full); Directory.CreateDirectory(directory); string temporary = System.IO.Path.Combine(directory, ".midi-triggers-" + Guid.NewGuid().ToString("N") + ".tmp"); try { File.WriteAllText(temporary, json + Environment.NewLine, new UTF8Encoding(false)); if (File.Exists(full)) File.Replace(temporary, full, full + ".bak", true); else File.Move(temporary, full); } finally { if (File.Exists(temporary)) File.Delete(temporary); } }
    }
    public sealed class MidiTriggerService : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void Callback(IntPtr handle, uint message, IntPtr instance, IntPtr parameter1, IntPtr parameter2);
        [DllImport("winmm.dll")] static extern int midiInOpen(out IntPtr handle, uint device, Callback callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] static extern int midiInStart(IntPtr handle);
        [DllImport("winmm.dll")] static extern int midiInStop(IntPtr handle);
        [DllImport("winmm.dll")] static extern int midiInClose(IntPtr handle);
        const uint MIM_DATA = 0x3C3, CALLBACK_FUNCTION = 0x30000;
        readonly List<MidiTrigger> triggers; readonly Action<int> fired; readonly Callback callback; readonly object handleGate = new object(); readonly TriggerDispatchGate dispatchGate = new TriggerDispatchGate(); readonly List<IntPtr> handles = new List<IntPtr>(); readonly Dictionary<IntPtr, int> deviceByHandle = new Dictionary<IntPtr, int>(); bool disposed;
        internal bool Running { get { lock (handleGate) return handles.Count > 0 && !disposed; } }
        public MidiTriggerService(IEnumerable<MidiTrigger> values, Action<int> fired)
        {
            if (values == null) throw new ArgumentNullException("values"); if (fired == null) throw new ArgumentNullException("fired"); this.fired = fired; triggers = values.Select(x => { if (x == null) throw new ArgumentException("A MIDI trigger cannot be empty."); var copy = x.Copy(); MidiTriggerStore.Validate(copy); return copy; }).ToList();
            if (triggers.Count > MidiTriggerStore.MaxTriggers) throw new ArgumentException("Use up to " + MidiTriggerStore.MaxTriggers + " MIDI triggers.");
            callback = OnMessage;
            try
            {
                foreach (int device in triggers.Select(x => x.Device).Distinct())
                {
                    IntPtr handle; int result = midiInOpen(out handle, (uint)device, callback, IntPtr.Zero, CALLBACK_FUNCTION);
                    if (result != 0) throw new InvalidOperationException("Windows could not open MIDI input device " + device + " (error " + result + ").");
                    lock (handleGate) { handles.Add(handle); deviceByHandle[handle] = device; }
                    result = midiInStart(handle); if (result != 0) throw new InvalidOperationException("Windows could not start MIDI input device " + device + " (error " + result + ").");
                }
            }
            catch { Dispose(); throw; }
        }
        void OnMessage(IntPtr h, uint message, IntPtr instance, IntPtr p1, IntPtr p2)
        {
            if (message != MIM_DATA) return; int device; lock (handleGate) if (disposed || !deviceByHandle.TryGetValue(h, out device)) return;
            int packed = unchecked((int)p1.ToInt64()); int status = packed & 0xFF, data1 = (packed >> 8) & 0xFF, data2 = (packed >> 16) & 0xFF;
            foreach (var trigger in triggers) if (trigger.Matches(device, status, data1, data2)) { try { if (!dispatchGate.TryRun(delegate { fired(trigger.FunctionKey); })) return; } catch { } }
        }
        public void Dispose()
        {
            dispatchGate.Close(); IntPtr[] closing; lock (handleGate) { if (disposed) return; disposed = true; closing = handles.ToArray(); handles.Clear(); deviceByHandle.Clear(); }
            foreach (IntPtr handle in closing) { midiInStop(handle); midiInClose(handle); }
        }
    }
 #if !WPF_REWRITE
    internal sealed class MidiTriggersForm : Form
    {
        readonly ListBox list = new DesignListBox(); readonly NumericUpDown device = new DesignNumericUpDown { Minimum = 0, Maximum = 31 }, channel = new DesignNumericUpDown { Minimum = 1, Maximum = 16, Value = 1 }, number = new DesignNumericUpDown { Minimum = 0, Maximum = 127 }, minimum = new DesignNumericUpDown { Minimum = 1, Maximum = 127, Value = 1 }, key = new DesignNumericUpDown { Minimum = 1, Maximum = 12, Value = 1 }; readonly ComboBox message = new DesignComboBox(); readonly List<MidiTrigger> values;
        internal MidiTriggersForm() { values = MidiTriggerStore.Load(); Text = "KiWeave - MIDI triggers"; Icon = Program.AppIcon(); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Font = new System.Drawing.Font("Segoe UI", 10); ClientSize = new System.Drawing.Size(760, 590); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this); var root = UiStyle.Stack(); root.Padding = new Padding(24); Controls.Add(root); root.Controls.Add(UiStyle.Text("MIDI triggers", 20, true)); root.Controls.Add(UiStyle.Text("Optional local MIDI input. Triggers invoke an existing saved function-row action; no MIDI data is recorded.", 9, false)); list.Height = 180; root.Controls.Add(UiStyle.Field("Saved triggers", list)); message.Items.AddRange(new object[] { "NoteOn", "ControlChange" }); message.SelectedIndex = 0; UiStyle.Combo(message); root.Controls.Add(UiStyle.Field("Message", message)); root.Controls.Add(UiStyle.Field("Device index", device)); root.Controls.Add(UiStyle.Field("Channel", channel)); root.Controls.Add(UiStyle.Field("Note/CC number", number)); root.Controls.Add(UiStyle.Field("Minimum value", minimum)); root.Controls.Add(UiStyle.Field("Invoke function key", key)); var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft }; buttons.Controls.Add(UiStyle.Button("Save", delegate { try { MidiTriggerStore.Save(values); DialogResult = DialogResult.OK; Close(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "MIDI triggers", MessageBoxButtons.OK, MessageBoxIcon.Error); } }, true)); buttons.Controls.Add(UiStyle.Button("Add", delegate { values.Add(new MidiTrigger { Device = (int)device.Value, Channel = (int)channel.Value - 1, Number = (int)number.Value, MinimumValue = (int)minimum.Value, FunctionKey = (int)key.Value, Message = message.Text }); RefreshList(); })); buttons.Controls.Add(UiStyle.Button("Delete selected", delegate { if (list.SelectedIndex >= 0) { values.RemoveAt(list.SelectedIndex); RefreshList(); } })); buttons.Controls.Add(UiStyle.Button("Done", delegate { Close(); })); root.Controls.Add(buttons); RefreshList(); }
        void RefreshList() { list.Items.Clear(); list.Items.AddRange(values.Select(x => x.ToString()).ToArray()); }
    }
 #endif
}
