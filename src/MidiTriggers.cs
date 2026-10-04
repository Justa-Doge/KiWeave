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
    internal sealed class MidiTrigger
    {
        public int Device, Channel, Number, MinimumValue = 1, FunctionKey = 1;
        public string Message = "NoteOn";
        internal MidiTrigger Copy() { return new MidiTrigger { Device = Device, Channel = Channel, Number = Number, MinimumValue = MinimumValue, FunctionKey = FunctionKey, Message = Message }; }
        internal bool Matches(int status, int data1, int data2) { int type = status & 0xF0, channel = status & 0x0F; return channel == Channel && data1 == Number && data2 >= MinimumValue && ((Message == "NoteOn" && type == 0x90 && data2 > 0) || (Message == "ControlChange" && type == 0xB0)); }
        public override string ToString() { return "MIDI " + Device + " · " + Message + " " + Number + " · F" + FunctionKey; }
    }
    internal static class MidiTriggerStore
    {
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "midi-triggers.json"); } }
        internal static List<MidiTrigger> Load()
        {
            try { if (!File.Exists(Path)) return new List<MidiTrigger>(); var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path, Encoding.UTF8)) as Dictionary<string, object>; var rows = d == null ? null : d["triggers"] as object[]; return rows == null ? new List<MidiTrigger>() : rows.Select(x => { var r = x as Dictionary<string, object>; return new MidiTrigger { Device = Convert.ToInt32(r["device"]), Channel = Convert.ToInt32(r["channel"]), Number = Convert.ToInt32(r["number"]), MinimumValue = Convert.ToInt32(r["minimumValue"]), FunctionKey = Convert.ToInt32(r["functionKey"]), Message = (string)r["message"] }; }).ToList(); } catch { return new List<MidiTrigger>(); }
        }
        internal static void Save(IEnumerable<MidiTrigger> triggers)
        {
            var list = triggers.Select(x => x.Copy()).ToList(); foreach (var t in list) Validate(t); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)); File.WriteAllText(Path, new JavaScriptSerializer().Serialize(new { version = 1, triggers = list }), new UTF8Encoding(false));
        }
        internal static void Validate(MidiTrigger t) { if (t == null || t.Device < 0 || t.Channel < 0 || t.Channel > 15 || t.Number < 0 || t.Number > 127 || t.MinimumValue < 1 || t.MinimumValue > 127 || t.FunctionKey < 1 || t.FunctionKey > 12 || (t.Message != "NoteOn" && t.Message != "ControlChange")) throw new ArgumentException("MIDI trigger values are outside the supported range."); }
    }
    internal sealed class MidiTriggerService : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void Callback(IntPtr handle, uint message, IntPtr instance, IntPtr parameter1, IntPtr parameter2);
        [DllImport("winmm.dll")] static extern int midiInOpen(out IntPtr handle, uint device, Callback callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] static extern int midiInStart(IntPtr handle);
        [DllImport("winmm.dll")] static extern int midiInStop(IntPtr handle);
        [DllImport("winmm.dll")] static extern int midiInClose(IntPtr handle);
        const uint MIM_DATA = 0x3C3, CALLBACK_FUNCTION = 0x30000;
        readonly List<MidiTrigger> triggers; readonly Action<int> fired; readonly Callback callback; IntPtr handle;
        internal bool Running { get { return handle != IntPtr.Zero; } }
        internal MidiTriggerService(IEnumerable<MidiTrigger> values, Action<int> fired) { triggers = values.Select(x => x.Copy()).ToList(); this.fired = fired; callback = OnMessage; if (triggers.Count == 0) return; int result = midiInOpen(out handle, (uint)triggers[0].Device, callback, IntPtr.Zero, CALLBACK_FUNCTION); if (result != 0) { handle = IntPtr.Zero; throw new InvalidOperationException("Windows could not open the selected MIDI input device (error " + result + ")."); } if (midiInStart(handle) != 0) { midiInClose(handle); handle = IntPtr.Zero; throw new InvalidOperationException("Windows could not start MIDI input."); } }
        void OnMessage(IntPtr h, uint message, IntPtr instance, IntPtr p1, IntPtr p2) { if (message != MIM_DATA) return; int packed = unchecked((int)p1.ToInt64()); int status = packed & 0xFF, data1 = (packed >> 8) & 0xFF, data2 = (packed >> 16) & 0xFF; foreach (var t in triggers) if (t.Matches(status, data1, data2)) { try { fired(t.FunctionKey); } catch { } } }
        public void Dispose() { if (handle == IntPtr.Zero) return; midiInStop(handle); midiInClose(handle); handle = IntPtr.Zero; }
    }
    internal sealed class MidiTriggersForm : Form
    {
        readonly ListBox list = new DesignListBox(); readonly NumericUpDown device = new DesignNumericUpDown { Minimum = 0, Maximum = 31 }, channel = new DesignNumericUpDown { Minimum = 1, Maximum = 16, Value = 1 }, number = new DesignNumericUpDown { Minimum = 0, Maximum = 127 }, minimum = new DesignNumericUpDown { Minimum = 1, Maximum = 127, Value = 1 }, key = new DesignNumericUpDown { Minimum = 1, Maximum = 12, Value = 1 }; readonly ComboBox message = new DesignComboBox(); readonly List<MidiTrigger> values;
        internal MidiTriggersForm() { values = MidiTriggerStore.Load(); Text = "KiWeave - MIDI triggers"; Icon = Program.AppIcon(); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Font = new System.Drawing.Font("Segoe UI", 10); ClientSize = new System.Drawing.Size(760, 590); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this); var root = UiStyle.Stack(); root.Padding = new Padding(24); Controls.Add(root); root.Controls.Add(UiStyle.Text("MIDI triggers", 20, true)); root.Controls.Add(UiStyle.Text("Optional local MIDI input. Triggers invoke an existing saved function-row action; no MIDI data is recorded.", 9, false)); list.Height = 180; root.Controls.Add(UiStyle.Field("Saved triggers", list)); message.Items.AddRange(new object[] { "NoteOn", "ControlChange" }); message.SelectedIndex = 0; UiStyle.Combo(message); root.Controls.Add(UiStyle.Field("Message", message)); root.Controls.Add(UiStyle.Field("Device index", device)); root.Controls.Add(UiStyle.Field("Channel", channel)); root.Controls.Add(UiStyle.Field("Note/CC number", number)); root.Controls.Add(UiStyle.Field("Minimum value", minimum)); root.Controls.Add(UiStyle.Field("Invoke function key", key)); var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft }; buttons.Controls.Add(UiStyle.Button("Save", delegate { try { MidiTriggerStore.Save(values); DialogResult = DialogResult.OK; Close(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "MIDI triggers", MessageBoxButtons.OK, MessageBoxIcon.Error); } }, true)); buttons.Controls.Add(UiStyle.Button("Add", delegate { values.Add(new MidiTrigger { Device = (int)device.Value, Channel = (int)channel.Value - 1, Number = (int)number.Value, MinimumValue = (int)minimum.Value, FunctionKey = (int)key.Value, Message = message.Text }); RefreshList(); })); buttons.Controls.Add(UiStyle.Button("Delete selected", delegate { if (list.SelectedIndex >= 0) { values.RemoveAt(list.SelectedIndex); RefreshList(); } })); buttons.Controls.Add(UiStyle.Button("Done", delegate { Close(); })); root.Controls.Add(buttons); RefreshList(); }
        void RefreshList() { list.Items.Clear(); list.Items.AddRange(values.Select(x => x.ToString()).ToArray()); }
    }
}
