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
    internal static class AutomaticProfilePolicy
    {
        internal static bool CanSwitch(bool enabled, bool pinned, bool windowVisible, bool hasUnsavedChanges, bool runtimeAvailable)
        {
            return enabled && !pinned && !windowVisible && !hasUnsavedChanges && runtimeAvailable;
        }
        internal static bool ShouldPollForegroundForAutomaticProfile(bool enabled, bool foregroundEventsAvailable)
        {
            return enabled && !foregroundEventsAvailable;
        }
    }

    public sealed class ProfileSchedule
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
    public static class ProfileScheduleStore
    {
        static string PathName { get { return Path.Combine(AppStorage.DataFolder, "profile-schedules.json"); } }
        static readonly object loadFailureGate = new object(); static string lastLoadFailure = "";
        public const int MaxSchedules = 64;
        public const int PollIntervalMilliseconds = 30000;
        public static List<ProfileSchedule> Load() { return Load(PathName); }
        internal static List<ProfileSchedule> Load(string path)
        {
            if (!File.Exists(path)) return new List<ProfileSchedule>();
            string json = AtomicFile.ReadAllTextBounded(path, 65536, Encoding.UTF8); JsonSyntax.Check(json);
            var root = new JavaScriptSerializer { MaxJsonLength = 65536, RecursionLimit = 8 }.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null || root.Count != 2 || !root.ContainsKey("version") || !(root["version"] is int) || (int)root["version"] != 1 || !root.ContainsKey("schedules")) throw new ArgumentException("Invalid profile schedule file.");
            var rows = root["schedules"] as object[]; if (rows == null || rows.Length > MaxSchedules) throw new ArgumentException("Profile schedules must be an array of up to " + MaxSchedules + " entries.");
            return rows.Select(item => {
                var row = item as Dictionary<string, object>;
                if (row == null || row.Count != 5 || !row.ContainsKey("profile") || !row.ContainsKey("days") || !row.ContainsKey("start") || !row.ContainsKey("end") || !row.ContainsKey("enabled") || !(row["profile"] is string) || !(row["days"] is string) || !(row["start"] is int) || !(row["end"] is int) || !(row["enabled"] is bool)) throw new ArgumentException("Invalid profile schedule entry.");
                var schedule = new ProfileSchedule { Profile = (string)row["profile"], Days = (string)row["days"], StartMinute = (int)row["start"], EndMinute = (int)row["end"], Enabled = (bool)row["enabled"] };
                Validate(schedule); return schedule;
            }).ToList();
        }
        public static void Save(IEnumerable<ProfileSchedule> schedules) { Save(PathName, schedules); }
        internal static void Save(string path, IEnumerable<ProfileSchedule> schedules)
        {
            if (schedules == null) throw new ArgumentNullException("schedules");
            var list = schedules.Select(x => { if (x == null) throw new ArgumentException("A profile schedule cannot be empty."); var copy = new ProfileSchedule { Profile = x.Profile, Days = x.Days, StartMinute = x.StartMinute, EndMinute = x.EndMinute, Enabled = x.Enabled }; Validate(copy); return copy; }).ToArray();
            if (list.Length > MaxSchedules) throw new ArgumentException("Use up to " + MaxSchedules + " profile schedules.");
            var serializer = new JavaScriptSerializer { MaxJsonLength = 65536, RecursionLimit = 8 };
            string json = serializer.Serialize(new { version = 1, schedules = list.Select(x => new { profile = x.Profile.Trim(), days = x.Days, start = x.StartMinute, end = x.EndMinute, enabled = x.Enabled }).ToArray() });
            if (Encoding.UTF8.GetByteCount(json) > 65536) throw new ArgumentException("Profile schedules exceed the 64 KB safety limit.");
            string full = Path.GetFullPath(path), directory = Path.GetDirectoryName(full); Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, ".profile-schedules-" + Guid.NewGuid().ToString("N") + ".tmp");
            try { File.WriteAllText(temporary, json + Environment.NewLine, new UTF8Encoding(false)); if (File.Exists(full)) File.Replace(temporary, full, full + ".bak", true); else File.Move(temporary, full); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal static void Validate(ProfileSchedule schedule)
        {
            if (schedule == null || String.IsNullOrWhiteSpace(schedule.Profile) || schedule.Profile.Trim().Length > 40 || schedule.Profile.Any(Char.IsControl)) throw new ArgumentException("Choose a valid profile for the schedule.");
            if (String.IsNullOrEmpty(schedule.Days) || schedule.Days.Any(c => c < '1' || c > '7') || schedule.Days.Distinct().Count() != schedule.Days.Length) throw new ArgumentException("Choose each weekday at most once.");
            if (schedule.StartMinute < 0 || schedule.StartMinute >= 1440 || schedule.EndMinute <= schedule.StartMinute || schedule.EndMinute > 1440) throw new ArgumentException("Schedule end time must be after its start time.");
        }
        public static string ActiveProfile(DateTime now)
        {
            List<ProfileSchedule> schedules;
            try { schedules = Load(); lock (loadFailureGate) lastLoadFailure = ""; }
            catch (Exception error) { lock (loadFailureGate) { if (lastLoadFailure != error.Message) { lastLoadFailure = error.Message; AppLog.Record("Profile schedule loading", error); } } return ""; }
            int day = ((int)now.DayOfWeek + 6) % 7 + 1, minute = now.Hour * 60 + now.Minute;
            foreach (var schedule in schedules) if (schedule.Enabled && schedule.Days.Contains(day.ToString()) && minute >= schedule.StartMinute && minute < schedule.EndMinute) return schedule.Profile;
            return "";
        }
    }
    #if !WPF_REWRITE
    internal sealed class ProfileSchedulesForm : Form
    {
        readonly ProfileCollection profiles; readonly ListBox list = new DesignListBox(); readonly ComboBox profile = new DesignComboBox(), start = new DesignComboBox(), end = new DesignComboBox(); readonly CheckBox[] dayChecks = new CheckBox[7]; readonly List<ProfileSchedule> schedules; readonly Label status = new Label(); string loadError;
        internal ProfileSchedulesForm(ProfileCollection values)
        {
            profiles = values.Copy(); try { schedules = ProfileScheduleStore.Load(); } catch (Exception error) { schedules = new List<ProfileSchedule>(); loadError = error.Message; } Text = "KiWeave - Profile schedules"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; ClientSize = new Size(820, 660); MinimumSize = new Size(760, 660); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
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
            form.Controls.Add(ScheduleField("Status", status)); status.Text = loadError == null ? "" : "Saved schedules could not be read; no changes will be saved. " + loadError; status.ForeColor = loadError == null ? UiStyle.Muted : Color.FromArgb(235, 174, 105);
            var add = UiStyle.Button("Add schedule", delegate { string d = ""; for (int i = 0; i < dayChecks.Length; i++) if (dayChecks[i].Checked) d += (i + 1).ToString(); int startMinute = start.SelectedIndex * 15, endMinute = end.SelectedIndex * 15; if (d.Length == 0 || endMinute <= startMinute) { MessageBox.Show(this, "Choose at least one day and an end time after the start time.", "Profile schedule", MessageBoxButtons.OK, MessageBoxIcon.Information); return; } var next = schedules.ToList(); next.Add(new ProfileSchedule { Profile = profile.Text, Days = d, StartMinute = startMinute, EndMinute = endMinute }); Save(next); }); ((DesignButton)add).Primary = true; add.Enabled = loadError == null; add.Dock = DockStyle.Left; rightLayout.Controls.Add(add, 0, 1); body.Controls.Add(right, 1, 0);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft }; buttons.Controls.Add(UiStyle.Button("Done", delegate { Close(); }, true)); var delete = UiStyle.Button("Delete selected", delegate { if (list.SelectedIndex >= 0) { var next = schedules.ToList(); next.RemoveAt(list.SelectedIndex); Save(next); } }); delete.Enabled = loadError == null; buttons.Controls.Add(delete); root.Controls.Add(buttons, 0, 2); RefreshList();
        }
        static void PopulateTimes(ComboBox box)
        {
            for (int minute = 0; minute < 24 * 60; minute += 15) box.Items.Add(DateTime.Today.AddMinutes(minute).ToString("h:mm tt"));
        }
        static Control ScheduleField(string caption, Control input)
        {
            var field = UiStyle.Field(caption, input); field.Margin = new Padding(0, 0, 0, 10); return field;
        }
        void Save(List<ProfileSchedule> next) { try { ProfileScheduleStore.Save(next); schedules.Clear(); schedules.AddRange(next); status.Text = "Schedule changes saved."; status.ForeColor = UiStyle.Muted; RefreshList(); } catch (Exception error) { MessageBox.Show(this, "The schedule was not saved. Your previous schedule file is unchanged.\r\n\r\n" + error.Message, "Profile schedules", MessageBoxButtons.OK, MessageBoxIcon.Error); } }
        void RefreshList() { list.Items.Clear(); foreach (var s in schedules) list.Items.Add(s); }
    }
    #endif
}
