using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal enum ConflictSeverity { Critical, Warning, Notice }
    internal enum ConflictArea { Profiles, CustomHotkeys, PowerToys, FunctionKeys }
    internal sealed class ConflictIssue
    {
        public ConflictSeverity Severity;
        public ConflictArea Area;
        public string Title = "", Detail = "", Winner = "", ProfileName = "Default";
        public string Suggestion = "";
        public int CustomHotkeyIndex = -1;
        public override string ToString() { return Severity.ToString().ToUpperInvariant() + "  ·  " + Title; }
    }

    internal static class ConflictScanner
    {
        static readonly HashSet<string> WindowsOwned = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "Win+L", "Win+U", "Win+G", "Win+R", "Win+X", "Win+D", "Win+E", "Win+I", "Win+V", "Alt+Tab", "Alt+Esc", "Ctrl+Shift+Esc"
        };
        internal static List<ConflictIssue> Scan(Configuration defaultConfiguration, ProfileCollection profiles)
        {
            List<PowerToysShortcut> powerToys;
            try { powerToys = PowerToysIntegration.Load().Where(x => x.ModuleEnabled && !String.Equals(x.Chord, "Unassigned", StringComparison.OrdinalIgnoreCase)).ToList(); }
            catch { powerToys = new List<PowerToysShortcut>(); }
            return Scan(defaultConfiguration, profiles, powerToys);
        }
        internal static List<ConflictIssue> Scan(Configuration defaultConfiguration, ProfileCollection profiles, IEnumerable<PowerToysShortcut> powerToys)
        {
            var issues = new List<ConflictIssue>();
            var appOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var profile in profiles.Profiles) foreach (string app in profile.Applications ?? new string[0]) {
                string normalized = ProfileStore.NormalizeProcess(app), owner;
                if (appOwners.TryGetValue(normalized, out owner)) {
                    string differences = ProfileDifference(owner, profile.Name, defaultConfiguration, profiles);
                    issues.Add(new ConflictIssue { Severity = ConflictSeverity.Critical, Area = ConflictArea.Profiles, ProfileName = profile.Name, Title = normalized + " activates more than one profile", Detail = owner + " and " + profile.Name + " both claim the same foreground application." + differences, Winner = owner + " wins because it appears first in the saved profile order." });
                }
                else appOwners[normalized] = profile.Name;
            }
            try { ProfileStore.Validate(profiles, false); }
            catch (Exception ex) { issues.Add(new ConflictIssue { Severity = ConflictSeverity.Critical, Area = ConflictArea.Profiles, Title = "Profile setup is invalid", Detail = ex.Message, Winner = "KiWeave refuses to activate an invalid profile setup." }); }

            var scopes = new List<KeyValuePair<string, Configuration>> { new KeyValuePair<string, Configuration>("Default", defaultConfiguration) };
            foreach (var profile in profiles.Profiles) try { scopes.Add(new KeyValuePair<string, Configuration>(profile.Name, profiles.Resolve(profile.Name, defaultConfiguration))); } catch { }
            var pt = new Dictionary<string, List<PowerToysShortcut>>(StringComparer.OrdinalIgnoreCase);
            foreach (var shortcut in powerToys ?? new PowerToysShortcut[0]) try {
                string chord = HotkeyChord.Normalize(shortcut.Chord); List<PowerToysShortcut> owners;
                if (!pt.TryGetValue(chord, out owners)) pt[chord] = owners = new List<PowerToysShortcut>(); owners.Add(shortcut);
            } catch { }

            foreach (var scope in scopes) {
                Configuration configuration = scope.Value;
                for (int i = 0; i < configuration.CustomHotkeys.Length; i++) {
                    string chord; try { chord = HotkeyChord.Normalize(configuration.CustomHotkeys[i].Shortcut); } catch { continue; }
                    if (WindowsOwned.Contains(chord)) issues.Add(new ConflictIssue { Severity = ConflictSeverity.Warning, Area = ConflictArea.CustomHotkeys, ProfileName = scope.Key, CustomHotkeyIndex = i, Title = chord + " is normally owned by Windows", Detail = "The " + scope.Key + " profile assigns a global KiWeave action to a Windows shell shortcut.", Winner = "Windows usually wins, so KiWeave may be unable to register this shortcut." });
                    List<PowerToysShortcut> owners;
                    if (pt.TryGetValue(chord, out owners)) foreach (var owner in owners) issues.Add(new ConflictIssue { Severity = ConflictSeverity.Warning, Area = ConflictArea.CustomHotkeys, ProfileName = scope.Key, CustomHotkeyIndex = i, Title = chord + " is also used by PowerToys", Detail = owner.Module + " • " + owner.Action + " uses the same active shortcut as the " + scope.Key + " profile.", Winner = "Only one app can own a registered shortcut. If PowerToys registered first, KiWeave cannot activate it." });
                    HotkeyChord parsed; try { parsed = HotkeyChord.Parse(chord); } catch { continue; }
                    var layer = configuration.Layers.FirstOrDefault(x => LayerKeys.VirtualKey(x.ActivationKey) == parsed.Key);
                    if (layer != null) issues.Add(new ConflictIssue { Severity = ConflictSeverity.Critical, Area = ConflictArea.CustomHotkeys, ProfileName = scope.Key, CustomHotkeyIndex = i, Title = chord + " ends with the " + layer.Name + " layer key", Detail = "The shortcut and modifier layer depend on the same physical key in the " + scope.Key + " profile.", Winner = "The low-level layer rule wins and suppresses the key before the global hotkey can activate." });
                }
            }
            foreach (var issue in issues) issue.Suggestion = SuggestionFor(issue);
            return issues.OrderBy(x => x.Severity).ThenBy(x => x.ProfileName).ThenBy(x => x.Title).ToList();
        }
        internal static string LayerVisualization(Configuration configuration)
        {
            if (configuration == null) return "No layer map."; var parts = new List<string> { "Base: F1–F12" }; var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var layer in configuration.Layers ?? new ModifierLayer[0]) { string key = layer.ActivationKey ?? "?"; parts.Add(layer.Name + " [hold " + LayerKeys.Label(key) + "]: F1–F12"); if (!used.Add(key)) parts.Add("⚠ duplicate hold key " + LayerKeys.Label(key)); }
            return String.Join("\r\n", parts.ToArray());
        }
        static string SuggestionFor(ConflictIssue issue)
        {
            if (issue == null) return "Review the affected rule before changing anything.";
            if (issue.Area == ConflictArea.Profiles) return "Assign each foreground application to one profile, or remove the duplicate app rule.";
            if (issue.Title.IndexOf("PowerToys", StringComparison.OrdinalIgnoreCase) >= 0) return "Choose a different chord in KiWeave or PowerToys so only one global shortcut claims it.";
            if (issue.Title.IndexOf("Windows", StringComparison.OrdinalIgnoreCase) >= 0) return "Choose a non-system chord; Windows may keep ownership of this shortcut.";
            if (issue.Title.IndexOf("layer key", StringComparison.OrdinalIgnoreCase) >= 0) return "Choose a chord that does not end with the active layer key, or change the layer key.";
            return "Open the relevant editor and review the conflicting shortcut before saving.";
        }
        static string ProfileDifference(string first, string second, Configuration defaults, ProfileCollection profiles)
        {
            try {
                Configuration a = profiles.Resolve(first, defaults), b = profiles.Resolve(second, defaults);
                var changed = new List<string>();
                for (int i = 0; i < 12 && changed.Count < 3; i++) if (a.Mappings[i].Summary != b.Mappings[i].Summary) changed.Add("F" + (i + 1));
                var hotkeysA = new HashSet<string>((a.CustomHotkeys ?? new CustomHotkey[0]).Select(x => HotkeyChord.Normalize(x.Shortcut)), StringComparer.OrdinalIgnoreCase);
                var hotkeysB = new HashSet<string>((b.CustomHotkeys ?? new CustomHotkey[0]).Select(x => HotkeyChord.Normalize(x.Shortcut)), StringComparer.OrdinalIgnoreCase);
                int overlap = hotkeysA.Count(hotkeysB.Contains);
                if (changed.Count == 0 && overlap == 0) return " Their resolved mappings are currently identical, so the overlap is purely an activation-order conflict.";
                string detail = " Their resolved configurations differ";
                if (changed.Count > 0) detail += " on " + String.Join(", ", changed.ToArray()) + (changed.Count == 3 ? " or more" : "");
                if (overlap > 0) detail += (changed.Count > 0 ? "; " : " on top of that, ") + overlap + " custom hotkey" + (overlap == 1 ? " also overlaps" : "s also overlap");
                return detail + ".";
            } catch { return " KiWeave could not compare their resolved mappings safely."; }
        }
    }

    internal static class CollisionSimulator
    {
        internal static string Simulate(string shortcut, Configuration defaults, ProfileCollection profiles, IEnumerable<PowerToysShortcut> powerToys)
        {
            string chord = HotkeyChord.Normalize(shortcut); var hits = new List<string>();
            if (new[] { "Win+L", "Win+U", "Win+G", "Win+R", "Win+X", "Win+D", "Win+E", "Win+I", "Win+V", "Alt+Tab", "Alt+Esc", "Ctrl+Shift+Esc" }.Contains(chord, StringComparer.OrdinalIgnoreCase)) hits.Add("Windows normally owns this shortcut.");
            foreach (var pt in powerToys ?? new PowerToysShortcut[0]) try { if (String.Equals(HotkeyChord.Normalize(pt.Chord), chord, StringComparison.OrdinalIgnoreCase)) hits.Add("PowerToys: " + pt.Module + " · " + pt.Action); } catch { }
            var scopes = new List<KeyValuePair<string, Configuration>> { new KeyValuePair<string, Configuration>("Default", defaults) };
            foreach (var p in profiles.Profiles) try { scopes.Add(new KeyValuePair<string, Configuration>(p.Name, profiles.Resolve(p.Name, defaults))); } catch { }
            foreach (var scope in scopes) foreach (var hotkey in scope.Value.CustomHotkeys ?? new CustomHotkey[0]) try { if (String.Equals(HotkeyChord.Normalize(hotkey.Shortcut), chord, StringComparison.OrdinalIgnoreCase)) hits.Add(scope.Key + " profile already assigns this shortcut."); } catch { }
            return hits.Count == 0 ? chord + " has no known collision in the current KiWeave, Windows, or PowerToys snapshot." : chord + " collision simulation:\r\n• " + String.Join("\r\n• ", hits.Distinct().ToArray());
        }
    }

    internal sealed class ConflictCenterForm : Form
    {
        readonly Configuration defaultConfiguration;
        readonly ProfileCollection profiles;
        readonly IEnumerable<PowerToysShortcut> suppliedPowerToys;
        readonly ListBox list = new DesignListBox();
        readonly Label summary = new DesignLabel(), layerMap = new DesignLabel(), title = new DesignLabel(), detail = new DesignLabel(), winner = new DesignLabel(), suggestion = new DesignLabel();
        readonly Button open;
        public ConflictIssue SelectedIssue { get { return list.SelectedItem as ConflictIssue; } }

        internal ConflictCenterForm(Configuration defaults, ProfileCollection profileCollection) : this(defaults, profileCollection, null) { }
        internal ConflictCenterForm(Configuration defaults, ProfileCollection profileCollection, IEnumerable<PowerToysShortcut> powerToys)
        {
            defaultConfiguration = defaults.Copy(); profiles = profileCollection.Copy(); suppliedPowerToys = powerToys;
            Text = "KiWeave conflict center"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Design.DarkTitlebar(this);
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(940, 650); MinimumSize = new Size(820, 570);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); Controls.Add(root);
            var header = UiStyle.Stack(); header.Controls.Add(UiStyle.Text("Conflict center", 22, true)); header.Controls.Add(UiStyle.Text("Find overlapping shortcuts and rules without changing anything automatically.", 9, false));
            var simulator = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 8, 0, 0) }; var proposed = new DesignTextBox { Width = 180, Text = "Ctrl+Shift+P" }; simulator.Controls.Add(proposed); simulator.Controls.Add(UiStyle.Button("Simulate collision", delegate { try { IEnumerable<PowerToysShortcut> pts = suppliedPowerToys ?? PowerToysIntegration.Load(); MessageBox.Show(this, CollisionSimulator.Simulate(proposed.Text, defaultConfiguration, profiles, pts), "Collision simulation", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception ex) { MessageBox.Show(this, "Enter a valid modifier shortcut. " + ex.Message, "Collision simulation", MessageBoxButtons.OK, MessageBoxIcon.Information); } })); header.Controls.Add(simulator);
            summary.AutoSize = true; summary.ForeColor = UiStyle.Muted; summary.Margin = new Padding(0, 8, 0, 0); header.Controls.Add(summary); layerMap.AutoSize = true; layerMap.MaximumSize = new Size(850, 0); layerMap.ForeColor = UiStyle.Blue; layerMap.Margin = new Padding(0, 8, 0, 0); header.Controls.Add(layerMap); root.Controls.Add(header, 0, 0);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 18, 0, 12) };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57)); root.Controls.Add(body, 0, 1);
            var left = new DesignCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 14, 0) }; body.Controls.Add(left, 0, 0);
            list.Dock = DockStyle.Fill; list.BorderStyle = BorderStyle.None; list.BackColor = UiStyle.Surface; list.ForeColor = UiStyle.Ink; list.IntegralHeight = false; list.ItemHeight = 46; list.DrawMode = DrawMode.OwnerDrawFixed; list.DrawItem += DrawIssue; list.SelectedIndexChanged += delegate { ShowSelected(); }; left.Controls.Add(list);
            var right = new DesignCard { Dock = DockStyle.Fill }; body.Controls.Add(right, 1, 0); var details = UiStyle.Stack(); right.Controls.Add(details);
            title.AutoSize = true; title.Font = new Font("Segoe UI", 16, FontStyle.Bold); title.ForeColor = UiStyle.Ink; title.Margin = new Padding(0, 0, 0, 16); details.Controls.Add(title);
            detail.AutoSize = true; detail.MaximumSize = new Size(430, 0); detail.ForeColor = UiStyle.Ink; detail.Margin = new Padding(0, 0, 0, 20); details.Controls.Add(detail);
            var wins = UiStyle.Text("What wins", 9, true); details.Controls.Add(wins); winner.AutoSize = true; winner.MaximumSize = new Size(430, 0); winner.ForeColor = UiStyle.Muted; details.Controls.Add(winner);
            var suggest = UiStyle.Text("Suggested fix", 9, true); suggest.Margin = new Padding(0, 18, 0, 0); details.Controls.Add(suggest); suggestion.AutoSize = true; suggestion.MaximumSize = new Size(430, 0); suggestion.ForeColor = UiStyle.Blue; details.Controls.Add(suggestion);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
            buttons.Controls.Add(UiStyle.Button("Close", delegate { Close(); })); open = UiStyle.Button("Open relevant area", delegate { if (SelectedIssue != null) { DialogResult = DialogResult.OK; Close(); } }, true); buttons.Controls.Add(open);
            buttons.Controls.Add(UiStyle.Button("Refresh", delegate { RefreshScan(); })); root.Controls.Add(buttons, 0, 2); RefreshScan();
        }
        void RefreshScan()
        {
            var issues = suppliedPowerToys == null ? ConflictScanner.Scan(defaultConfiguration, profiles) : ConflictScanner.Scan(defaultConfiguration, profiles, suppliedPowerToys); list.Items.Clear(); list.Items.AddRange(issues.Cast<object>().ToArray());
            int critical = issues.Count(x => x.Severity == ConflictSeverity.Critical), warnings = issues.Count(x => x.Severity == ConflictSeverity.Warning);
            summary.Text = issues.Count == 0 ? "All clear. No known conflicts were found." : issues.Count + " finding" + (issues.Count == 1 ? "" : "s") + "  •  " + critical + " critical  •  " + warnings + " warning" + (warnings == 1 ? "" : "s");
            layerMap.Text = "Layer map\r\n" + ConflictScanner.LayerVisualization(defaultConfiguration);
            if (list.Items.Count > 0) list.SelectedIndex = 0; else { title.Text = "No known conflicts"; detail.Text = "KiWeave did not find duplicate application rules, layer-key collisions, Windows-owned shortcuts, or active PowerToys overlaps."; winner.Text = "Nothing needs attention."; suggestion.Text = ""; open.Enabled = false; }
        }
        void ShowSelected() { var issue = SelectedIssue; open.Enabled = issue != null; if (issue == null) return; title.Text = issue.Title; detail.Text = issue.Detail; winner.Text = issue.Winner; suggestion.Text = issue.Suggestion; }
        void DrawIssue(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return; var issue = (ConflictIssue)list.Items[e.Index]; bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(selected ? UiStyle.Soft : UiStyle.Surface)) e.Graphics.FillRectangle(b, e.Bounds);
            Color severity = issue.Severity == ConflictSeverity.Critical ? Color.FromArgb(255, 151, 153) : issue.Severity == ConflictSeverity.Warning ? Color.FromArgb(242, 190, 92) : UiStyle.Blue;
            using (var font = new Font("Segoe UI", 9, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, issue.Severity.ToString().ToUpperInvariant(), font, new Rectangle(e.Bounds.X + 10, e.Bounds.Y, 72, e.Bounds.Height), severity, TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, issue.Title, Font, new Rectangle(e.Bounds.X + 88, e.Bounds.Y, e.Bounds.Width - 98, e.Bounds.Height), UiStyle.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
