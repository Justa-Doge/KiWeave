using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Drawing;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class ProfileSchedule
    {
        public string Profile = "Default"; public string Days = "12345"; public int StartMinute = 540; public int EndMinute = 1020; public bool Enabled = true;
        public override string ToString() { return Profile + "  ·  " + Time(StartMinute) + "–" + Time(EndMinute) + "  ·  " + DayNames(Days) + (Enabled ? "" : "  ·  Disabled"); }
        static string Time(int minute) { return DateTime.Today.AddMinutes(minute).ToString("h:mm tt"); }
        static string DayNames(string value)
        {
            if (value == "12345") return "Mon–Fri"; if (value == "67") return "Weekend"; if (value == "1234567") return "Every day";
            string[] names = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }; return String.Join(", ", Enumerable.Range(1, 7).Where(x => (value ?? "").Contains(x.ToString())).Select(x => names[x - 1]));
        }
    }
    internal static class ProfileScheduleStore
    {
        static string PathName { get { return Path.Combine(AppStorage.DataFolder, "profile-schedules.json"); } }
        internal static List<ProfileSchedule> Load()
        {
            try { if (!File.Exists(PathName)) return new List<ProfileSchedule>(); var root = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(PathName, Encoding.UTF8)) as Dictionary<string, object>; var rows = root == null ? null : root["schedules"] as object[]; return rows == null ? new List<ProfileSchedule>() : rows.Select(x => { var d = x as Dictionary<string, object>; return new ProfileSchedule { Profile = (string)d["profile"], Days = (string)d["days"], StartMinute = Convert.ToInt32(d["start"]), EndMinute = Convert.ToInt32(d["end"]), Enabled = Convert.ToBoolean(d["enabled"]) }; }).ToList(); } catch { return new List<ProfileSchedule>(); }
        }
        internal static void Save(IEnumerable<ProfileSchedule> schedules)
        { Directory.CreateDirectory(AppStorage.DataFolder); var payload = new { version = 1, schedules = schedules.Select(x => new { profile = x.Profile, days = x.Days, start = x.StartMinute, end = x.EndMinute, enabled = x.Enabled }).ToArray() }; File.WriteAllText(PathName, new JavaScriptSerializer().Serialize(payload) + Environment.NewLine, Encoding.UTF8); }
        internal static string ActiveProfile(DateTime now)
        { int day = ((int)now.DayOfWeek + 6) % 7 + 1, minute = now.Hour * 60 + now.Minute; foreach (var s in Load()) if (s.Enabled && s.Days.Contains(day.ToString()) && minute >= s.StartMinute && minute < s.EndMinute) return s.Profile; return ""; }
    }
    internal sealed class ProfileSchedulesForm : Form
    {
        readonly ProfileCollection profiles; readonly ListBox list = new DesignListBox(); readonly ComboBox profile = new DesignComboBox(), start = new DesignComboBox(), end = new DesignComboBox(); readonly CheckBox[] dayChecks = new CheckBox[7]; readonly List<ProfileSchedule> schedules;
        internal ProfileSchedulesForm(ProfileCollection values)
        {
            profiles = values.Copy(); schedules = ProfileScheduleStore.Load(); Text = "KiWeave - Profile schedules"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; ClientSize = new Size(820, 660); MinimumSize = new Size(760, 660); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 3 }; root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(root);
            var head = UiStyle.Stack(); head.Controls.Add(UiStyle.Text("Local profile schedules", 22, true)); head.Controls.Add(UiStyle.Text("Switch profiles by time and weekday. Schedules stay local and never send data online.", 9, false)); root.Controls.Add(head, 0, 0);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 18, 0, 12) }; body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); root.Controls.Add(body, 0, 1);
            var left = new DesignCard { Dock = DockStyle.Fill, Padding = new Padding(16), Margin = new Padding(0, 0, 14, 0) };
            var leftStack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 }; leftStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); leftStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            leftStack.Controls.Add(UiStyle.Text("Saved schedules", 11, true), 0, 0); list.Dock = DockStyle.Fill; list.BorderStyle = BorderStyle.None; list.BackColor = UiStyle.Surface; list.ForeColor = UiStyle.Ink; leftStack.Controls.Add(list, 0, 1); left.Controls.Add(leftStack); body.Controls.Add(left, 0, 0);
            var right = new DesignCard { Dock = DockStyle.Fill, Padding = new Padding(18) };
            var rightLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); right.Controls.Add(rightLayout);
            var formScroll = new DesignScrollPanel { Dock = DockStyle.Fill }; rightLayout.Controls.Add(formScroll, 0, 0); var form = UiStyle.Stack(); formScroll.Controls.Add(form); form.Controls.Add(UiStyle.Text("New schedule", 11, true));
            profile.Items.Add("Default"); foreach (var p in profiles.Profiles) profile.Items.Add(p.Name); profile.SelectedIndex = 0; UiStyle.Combo(profile); form.Controls.Add(ScheduleField("Profile", profile));
            PopulateTimes(start); PopulateTimes(end); start.SelectedIndex = 36; end.SelectedIndex = 68; UiStyle.Combo(start); UiStyle.Combo(end); form.Controls.Add(ScheduleField("Start", start)); form.Controls.Add(ScheduleField("End", end));
            var dayPicker = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
            string[] dayNames = { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };
            for (int i = 0; i < dayChecks.Length; i++) { dayChecks[i] = new DesignCheckBox { Text = dayNames[i], Checked = i < 5, AutoSize = false, Size = new Size(46, 28), Font = new Font("Segoe UI", 8.5f), Margin = new Padding(0, 0, 3, 2) }; dayPicker.Controls.Add(dayChecks[i]); }
            form.Controls.Add(ScheduleField("Days", dayPicker));
            var add = UiStyle.Button("Add schedule", delegate { string d = ""; for (int i = 0; i < dayChecks.Length; i++) if (dayChecks[i].Checked) d += (i + 1).ToString(); int startMinute = start.SelectedIndex * 15, endMinute = end.SelectedIndex * 15; if (d.Length == 0 || endMinute <= startMinute) { MessageBox.Show(this, "Choose at least one day and an end time after the start time.", "Profile schedule", MessageBoxButtons.OK, MessageBoxIcon.Information); return; } schedules.Add(new ProfileSchedule { Profile = profile.Text, Days = d, StartMinute = startMinute, EndMinute = endMinute }); ProfileScheduleStore.Save(schedules); RefreshList(); }); ((DesignButton)add).Primary = true; add.Dock = DockStyle.Left; rightLayout.Controls.Add(add, 0, 1); body.Controls.Add(right, 1, 0);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft }; buttons.Controls.Add(UiStyle.Button("Done", delegate { Close(); }, true)); buttons.Controls.Add(UiStyle.Button("Delete selected", delegate { if (list.SelectedIndex >= 0) { schedules.RemoveAt(list.SelectedIndex); ProfileScheduleStore.Save(schedules); RefreshList(); } })); root.Controls.Add(buttons, 0, 2); RefreshList();
        }
        static void PopulateTimes(ComboBox box)
        {
            for (int minute = 0; minute < 24 * 60; minute += 15) box.Items.Add(DateTime.Today.AddMinutes(minute).ToString("h:mm tt"));
        }
        static Control ScheduleField(string caption, Control input)
        {
            var field = UiStyle.Field(caption, input); field.Margin = new Padding(0, 0, 0, 10); return field;
        }
        void RefreshList() { list.Items.Clear(); foreach (var s in schedules) list.Items.Add(s); }
    }
}
