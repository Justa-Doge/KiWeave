using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class ControllerTrigger
    {
        public int UserIndex, Button, FunctionKey;
        internal ControllerTrigger Copy() { return new ControllerTrigger { UserIndex = UserIndex, Button = Button, FunctionKey = FunctionKey }; }
        internal bool Matches(int index, ushort buttons) { return index == UserIndex && (buttons & (1 << Button)) != 0; }
        public override string ToString() { return "Controller " + (UserIndex + 1) + " · " + ButtonName(Button) + " · F" + FunctionKey; }
        internal static string ButtonName(int button) { return new[] { "DPadUp", "DPadDown", "DPadLeft", "DPadRight", "Start", "Back", "LeftThumb", "RightThumb", "LeftShoulder", "RightShoulder", "A", "B", "X", "Y" }[Math.Max(0, Math.Min(13, button))]; }
    }
    internal static class ControllerTriggerStore
    {
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "controller-triggers.json"); } }
        internal static List<ControllerTrigger> Load() { try { if (!File.Exists(Path)) return new List<ControllerTrigger>(); var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path, Encoding.UTF8)) as Dictionary<string, object>; var rows = d == null ? null : d["triggers"] as object[]; return rows == null ? new List<ControllerTrigger>() : rows.Select(x => { var r = x as Dictionary<string, object>; return new ControllerTrigger { UserIndex = Convert.ToInt32(r["userIndex"]), Button = Convert.ToInt32(r["button"]), FunctionKey = Convert.ToInt32(r["functionKey"]) }; }).ToList(); } catch { return new List<ControllerTrigger>(); } }
        internal static void Save(IEnumerable<ControllerTrigger> triggers) { var list = triggers.Select(x => x.Copy()).ToList(); foreach (var t in list) Validate(t); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)); File.WriteAllText(Path, new JavaScriptSerializer().Serialize(new { version = 1, triggers = list }), new UTF8Encoding(false)); }
        internal static void Validate(ControllerTrigger t) { if (t == null || t.UserIndex < 0 || t.UserIndex > 3 || t.Button < 0 || t.Button > 13 || t.FunctionKey < 1 || t.FunctionKey > 12) throw new ArgumentException("Controller trigger values are outside the supported range."); }
    }
    internal sealed class ControllerTriggerService : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] struct State { public uint Packet; public ushort Buttons; public byte LeftTrigger, RightTrigger; public short LX, LY, RX, RY; }
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] static extern uint GetState(uint index, out State state);
        readonly List<ControllerTrigger> triggers; readonly Action<int> fired; readonly Thread thread; volatile bool stopping; readonly ushort[] previous = new ushort[4];
        internal ControllerTriggerService(IEnumerable<ControllerTrigger> values, Action<int> fired) { triggers = values.Select(x => x.Copy()).ToList(); this.fired = fired; if (triggers.Count == 0) return; thread = new Thread(Poll) { IsBackground = true, Name = "Controller triggers" }; thread.Start(); }
        void Poll() { while (!stopping) { for (uint i = 0; i < 4; i++) { ushort buttons; State state; if (!GameInputSupport.TryReadButtons((int)i, out buttons)) { if (GetState(i, out state) != 0) { previous[i] = 0; continue; } buttons = state.Buttons; } ushort rising = (ushort)(buttons & ~previous[i]); previous[i] = buttons; foreach (var t in triggers) if (t.Matches((int)i, rising)) try { fired(t.FunctionKey); } catch { } } Thread.Sleep(33); } }
        public void Dispose() { stopping = true; if (thread != null) thread.Join(250); }
    }
    internal sealed class ControllerTriggersForm : Form
    {
        readonly ListBox list = new DesignListBox(); readonly NumericUpDown user = new DesignNumericUpDown { Minimum = 1, Maximum = 4, Value = 1 }, button = new DesignNumericUpDown { Minimum = 0, Maximum = 13 }, key = new DesignNumericUpDown { Minimum = 1, Maximum = 12, Value = 1 }; readonly List<ControllerTrigger> values;
        internal ControllerTriggersForm() { values = ControllerTriggerStore.Load(); Text = "KiWeave - Controller triggers"; Icon = Program.AppIcon(); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Font = new System.Drawing.Font("Segoe UI", 10); ClientSize = new System.Drawing.Size(700, 500); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this); var root = UiStyle.Stack(); root.Padding = new Padding(24); Controls.Add(root); root.Controls.Add(UiStyle.Text("Controller triggers", 20, true)); root.Controls.Add(UiStyle.Text("Optional local XInput input. Rising button presses invoke an existing saved function-row action; controller state is not recorded.", 9, false)); list.Height = 180; root.Controls.Add(UiStyle.Field("Saved triggers", list)); root.Controls.Add(UiStyle.Field("Controller", user)); root.Controls.Add(UiStyle.Field("Button number (0-13)", button)); root.Controls.Add(UiStyle.Field("Invoke function key", key)); var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft }; buttons.Controls.Add(UiStyle.Button("Save", delegate { try { ControllerTriggerStore.Save(values); DialogResult = DialogResult.OK; Close(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Controller triggers", MessageBoxButtons.OK, MessageBoxIcon.Error); } }, true)); buttons.Controls.Add(UiStyle.Button("Add", delegate { values.Add(new ControllerTrigger { UserIndex = (int)user.Value - 1, Button = (int)button.Value, FunctionKey = (int)key.Value }); RefreshList(); })); buttons.Controls.Add(UiStyle.Button("Delete selected", delegate { if (list.SelectedIndex >= 0) { values.RemoveAt(list.SelectedIndex); RefreshList(); } })); buttons.Controls.Add(UiStyle.Button("Done", delegate { Close(); })); root.Controls.Add(buttons); RefreshList(); }
        void RefreshList() { list.Items.Clear(); list.Items.AddRange(values.Select(x => x.ToString()).ToArray()); }
    }
}
