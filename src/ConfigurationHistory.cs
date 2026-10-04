using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class ConfigurationHistoryEntry
    {
        internal string Path, Reason;
        internal DateTime CreatedUtc;
        internal long Bytes;
        internal KeyWeaveBackup Backup;
        public override string ToString() { return CreatedUtc.ToLocalTime().ToString("g") + "  •  " + Reason; }
    }

    internal static class ConfigurationHistory
    {
        internal const int Limit = 20;
        internal static string DefaultFolder { get { return Path.Combine(Path.GetDirectoryName(ConfigStore.DefaultPath), "History"); } }

        internal static string CaptureCurrent(string reason)
        {
            Configuration configuration = File.Exists(ConfigStore.DefaultPath) ? ConfigStore.Load(ConfigStore.DefaultPath) : new Configuration();
            ProfileCollection profiles = File.Exists(ProfileStore.DefaultPath) ? ProfileStore.Load(ProfileStore.DefaultPath) : new ProfileCollection();
            UserPreferences preferences = File.Exists(UserPreferences.DefaultPath) ? UserPreferences.Load(UserPreferences.DefaultPath) : new UserPreferences();
            return Capture(DefaultFolder, reason, configuration, profiles, preferences, Startup.Enabled, preferences.HistoryRetention);
        }
        internal static string Capture(string folder, string reason, Configuration configuration, ProfileCollection profiles, UserPreferences preferences, bool startup, int limit)
        {
            if (limit < 1 || limit > 100) throw new ArgumentOutOfRangeException("limit");
            Directory.CreateDirectory(folder);
            string slug = Slug(reason), path = Path.Combine(folder, DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "--" + slug + "--" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".keyweave");
            BackupBundle.Save(path, configuration, profiles, preferences, startup, false);
            Trim(folder, limit); return path;
        }
        internal static List<ConfigurationHistoryEntry> List(string folder)
        {
            var entries = new List<ConfigurationHistoryEntry>(); if (!Directory.Exists(folder)) return entries;
            foreach (string path in Directory.GetFiles(folder, "*.keyweave", SearchOption.TopDirectoryOnly)) try {
                var backup = BackupBundle.Load(path); entries.Add(new ConfigurationHistoryEntry { Path = path, Backup = backup, CreatedUtc = backup.CreatedUtc, Bytes = new FileInfo(path).Length, Reason = ReasonFromFile(path) });
            } catch { }
            return entries.OrderByDescending(x => x.CreatedUtc).ToList();
        }
        internal static string Compare(KeyWeaveBackup older, Configuration current, ProfileCollection profiles, UserPreferences preferences, bool startup)
        {
            if (older == null) return "Select a history entry.";
            var lines = new List<string>(); int changed = 0;
            for (int i = 0; i < 12; i++) if (!Same(older.Configuration.Mappings[i], current.Mappings[i])) { changed++; lines.Add("F" + (i + 1) + ": " + Safe(older.Configuration.Mappings[i]) + " → " + Safe(current.Mappings[i])); }
            if (older.Configuration.CustomHotkeys.Length != current.CustomHotkeys.Length) lines.Add("Custom hotkeys: " + older.Configuration.CustomHotkeys.Length + " → " + current.CustomHotkeys.Length);
            if (older.Configuration.Layers.Length != current.Layers.Length) lines.Add("Modifier layers: " + older.Configuration.Layers.Length + " → " + current.Layers.Length);
            if (older.Profiles.Profiles.Length != profiles.Profiles.Length) lines.Add("Profiles: " + older.Profiles.Profiles.Length + " → " + profiles.Profiles.Length);
            if (older.Configuration.Enabled != current.Enabled) lines.Add("Shortcuts enabled: " + Yes(older.Configuration.Enabled) + " → " + Yes(current.Enabled));
            if (older.Preferences.UseTray != preferences.UseTray) lines.Add("Tray mode: " + Yes(older.Preferences.UseTray) + " → " + Yes(preferences.UseTray));
            if (older.Preferences.AutomaticProfiles != preferences.AutomaticProfiles) lines.Add("Automatic profiles: " + Yes(older.Preferences.AutomaticProfiles) + " → " + Yes(preferences.AutomaticProfiles));
            if (older.Preferences.NetworkAccess != preferences.NetworkAccess) lines.Add("Network access: " + Yes(older.Preferences.NetworkAccess) + " → " + Yes(preferences.NetworkAccess));
            if (older.StartWithWindows != startup) lines.Add("Start with Windows: " + Yes(older.StartWithWindows) + " → " + Yes(startup));
            if (lines.Count == 0) return "No visible differences from the current saved setup.";
            if (changed > 8) { lines = lines.Take(8).ToList(); lines.Add("…and " + (changed - 8) + " more function-key changes."); }
            return String.Join("\r\n", lines);
        }
        static bool Same(Mapping a, Mapping b) { return a.Kind == b.Kind && a.Target == b.Target && a.Arguments == b.Arguments && a.WorkingDirectory == b.WorkingDirectory && a.MonitorId == b.MonitorId && a.MonitorControl == b.MonitorControl && a.MonitorStep == b.MonitorStep; }
        static string Safe(Mapping m)
        {
            if (m.Kind == ActionKind.Media && Shortcuts.MediaLabels.ContainsKey(m.Target)) return Shortcuts.MediaLabels[m.Target];
            if (m.Kind == ActionKind.SystemAction && Mapping.SystemActionLabels.ContainsKey(m.Target)) return Mapping.SystemActionLabels[m.Target];
            return Mapping.Labels[(int)m.Kind];
        }
        static string Yes(bool value) { return value ? "on" : "off"; }
        static string Slug(string reason)
        {
            string value = new string((reason ?? "change").ToLowerInvariant().Select(c => Char.IsLetterOrDigit(c) ? c : '-').ToArray());
            while (value.Contains("--")) value = value.Replace("--", "-"); value = value.Trim('-'); return value.Length == 0 ? "change" : value.Substring(0, Math.Min(40, value.Length));
        }
        static string ReasonFromFile(string path)
        {
            string[] parts = Path.GetFileNameWithoutExtension(path).Split(new[] { "--" }, StringSplitOptions.None); string slug = parts.Length >= 2 ? parts[1] : "change";
            return String.Join(" ", slug.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries).Select(x => Char.ToUpperInvariant(x[0]) + x.Substring(1)));
        }
        static void Trim(string folder, int limit)
        {
            foreach (string path in Directory.GetFiles(folder, "*.keyweave").OrderByDescending(Path.GetFileName).Skip(limit)) try { File.Delete(path); } catch { }
        }
    }

    internal sealed class ConfigurationHistoryForm : Form
    {
        readonly ListBox list = new DesignListBox(); readonly Label title = new DesignLabel(), meta = new DesignLabel(), comparison = new DesignLabel(); readonly Button restore;
        readonly Configuration current; readonly ProfileCollection profiles; readonly UserPreferences preferences; readonly bool startup;
        internal ConfigurationHistoryEntry SelectedEntry { get { return list.SelectedItem as ConfigurationHistoryEntry; } }
        internal ConfigurationHistoryForm(Configuration configuration, ProfileCollection profileCollection, UserPreferences userPreferences, bool startWithWindows) : this(configuration, profileCollection, userPreferences, startWithWindows, null) { }
        internal ConfigurationHistoryForm(Configuration configuration, ProfileCollection profileCollection, UserPreferences userPreferences, bool startWithWindows, IEnumerable<ConfigurationHistoryEntry> suppliedEntries)
        {
            current = configuration.Copy(); profiles = profileCollection.Copy(); preferences = new UserPreferences { UseTray = userPreferences.UseTray, CheckUpdates = userPreferences.CheckUpdates, AutomaticProfiles = userPreferences.AutomaticProfiles, NetworkAccess = userPreferences.NetworkAccess, Theme = userPreferences.Theme, CustomAccent = userPreferences.CustomAccent, HistoryRetention = userPreferences.HistoryRetention }; startup = startWithWindows;
            Text = "KiWeave history"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Design.DarkTitlebar(this);
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(940, 650); MinimumSize = new Size(820, 570);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 3 }; root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); Controls.Add(root);
            var header = UiStyle.Stack(); header.Controls.Add(UiStyle.Text("Undo and history", 22, true)); header.Controls.Add(UiStyle.Text("Review local snapshots before restoring an earlier saved setup.", 9, false)); root.Controls.Add(header, 0, 0);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 18, 0, 12) }; body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57)); root.Controls.Add(body, 0, 1);
            var left = new DesignCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 14, 0) }; body.Controls.Add(left, 0, 0);
            list.Dock = DockStyle.Fill; list.BorderStyle = BorderStyle.None; list.BackColor = UiStyle.Surface; list.ForeColor = UiStyle.Ink; list.IntegralHeight = false; list.DrawMode = DrawMode.OwnerDrawFixed; list.ItemHeight = 44; list.DrawItem += DrawEntry; list.SelectedIndexChanged += delegate { ShowSelected(); }; left.Controls.Add(list);
            var right = new DesignCard { Dock = DockStyle.Fill }; body.Controls.Add(right, 1, 0); var stack = UiStyle.Stack(); right.Controls.Add(stack);
            title.AutoSize = true; title.Font = new Font("Segoe UI", 15, FontStyle.Bold); title.ForeColor = UiStyle.Ink; title.Margin = new Padding(0, 0, 0, 4); stack.Controls.Add(title);
            meta.AutoSize = true; meta.Font = new Font("Segoe UI", 10, FontStyle.Bold); meta.ForeColor = UiStyle.Ink; meta.Margin = new Padding(0, 0, 0, 16); stack.Controls.Add(meta);
            comparison.AutoSize = true; comparison.MaximumSize = new Size(430, 0); comparison.ForeColor = UiStyle.Muted; stack.Controls.Add(comparison);
            var privacy = UiStyle.Text("History stays on this computer and can contain private mappings, targets, URLs, and profile data. Restoring never happens automatically.", 9, false); privacy.Margin = new Padding(0, 20, 0, 0); stack.Controls.Add(privacy);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) }; buttons.Controls.Add(UiStyle.Button("Close", delegate { Close(); }));
            restore = UiStyle.Button("Restore selected", delegate { if (SelectedEntry != null) { DialogResult = DialogResult.OK; Close(); } }, true); buttons.Controls.Add(restore); buttons.Controls.Add(UiStyle.Button("Open history folder", delegate { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + ConfigurationHistory.DefaultFolder + "\"") { UseShellExecute = true }); })); root.Controls.Add(buttons, 0, 2);
            foreach (var entry in suppliedEntries ?? ConfigurationHistory.List(ConfigurationHistory.DefaultFolder)) list.Items.Add(entry); if (list.Items.Count > 0) list.SelectedIndex = 0; else { title.Text = "No history yet"; meta.Text = ""; comparison.Text = "KiWeave creates a snapshot before the next successful mapping save, profile edit, or restore."; restore.Enabled = false; }
        }
        void ShowSelected()
        {
            var entry = SelectedEntry; restore.Enabled = entry != null; if (entry == null) return;
            title.Text = entry.Reason; meta.Text = entry.CreatedUtc.ToLocalTime().ToString("f") + "  •  " + Math.Max(1, entry.Bytes / 1024) + " KB";
            comparison.Text = ConfigurationHistory.Compare(entry.Backup, current, profiles, preferences, startup);
        }
        void DrawEntry(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return; var entry = (ConfigurationHistoryEntry)list.Items[e.Index]; bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var brush = new SolidBrush(selected ? UiStyle.Soft : UiStyle.Surface)) e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, entry.CreatedUtc.ToLocalTime().ToString("g"), Font, new Rectangle(e.Bounds.X + 10, e.Bounds.Y, 132, e.Bounds.Height), selected ? UiStyle.Blue : UiStyle.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var font = new Font("Segoe UI", 9, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, entry.Reason, font, new Rectangle(e.Bounds.X + 146, e.Bounds.Y, e.Bounds.Width - 156, e.Bounds.Height), UiStyle.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
