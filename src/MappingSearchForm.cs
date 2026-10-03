using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class MappingSearchResult
    {
        internal string Profile = "Default", Layer = "Base", Location = "", Action = "", Maturity = "Stable", SearchText = "";
        internal int LayerIndex = -1, FunctionIndex = -1, CustomIndex = -1;
        public override string ToString() { return Location; }
    }

    internal static class MappingSearchIndex
    {
        internal static List<MappingSearchResult> Build(Configuration defaults, ProfileCollection profiles)
        {
            var results = new List<MappingSearchResult>();
            AddConfiguration(results, "Default", defaults);
            foreach (var profile in profiles.Profiles) {
                Configuration configuration;
                try { configuration = profiles.Resolve(profile.Name, defaults); } catch { continue; }
                AddConfiguration(results, profile.Name, configuration);
            }
            return results;
        }

        static void AddConfiguration(List<MappingSearchResult> results, string profile, Configuration configuration)
        {
            for (int i = 0; i < 12; i++) Add(results, profile, "Base", -1, i, -1, "F" + (i + 1), configuration.Mappings[i]);
            for (int layer = 0; layer < configuration.Layers.Length; layer++)
                for (int i = 0; i < 12; i++) Add(results, profile, configuration.Layers[layer].Name, layer, i, -1, "F" + (i + 1), configuration.Layers[layer].Mappings[i]);
            for (int i = 0; i < configuration.CustomHotkeys.Length; i++)
                Add(results, profile, "Custom hotkeys", -1, -1, i, configuration.CustomHotkeys[i].Shortcut, configuration.CustomHotkeys[i].Action);
        }

        static void Add(List<MappingSearchResult> results, string profile, string layer, int layerIndex, int functionIndex, int customIndex, string location, Mapping mapping)
        {
            if (mapping == null) return;
            string action;
            try { action = mapping.Summary; } catch { action = Mapping.Labels[(int)mapping.Kind]; }
            string nested = Searchable(mapping);
            results.Add(new MappingSearchResult {
                Profile = profile, Layer = layer, LayerIndex = layerIndex, FunctionIndex = functionIndex, CustomIndex = customIndex,
                Location = profile + "  •  " + layer + "  •  " + location,
                Action = SafeAction(mapping), Maturity = ActionInsights.Maturity(mapping),
                SearchText = profile + " " + layer + " " + location + " " + action + " " + nested
            });
        }

        static string Searchable(Mapping mapping)
        {
            string text = mapping.Kind + " " + mapping.Target + " " + mapping.Arguments + " " + mapping.WorkingDirectory + " " + mapping.MonitorControl;
            if (mapping.Kind == ActionKind.Sequence) try { text += " " + String.Join(" ", SequenceCodec.Parse(mapping.Target).Select(x => x.IsWait ? x.Summary : Searchable(x.Action))); } catch { }
            if (mapping.Kind == ActionKind.Conditional) try { var rule = ConditionalCodec.Parse(mapping.Target); text += " " + rule.Summary + " " + Searchable(rule.WhenMatched) + " " + Searchable(rule.Otherwise); } catch { }
            return text;
        }

        static string SafeAction(Mapping mapping)
        {
            if (mapping.Kind == ActionKind.Media && Shortcuts.MediaLabels.ContainsKey(mapping.Target)) return Shortcuts.MediaLabels[mapping.Target];
            if (mapping.Kind == ActionKind.SystemAction && Mapping.SystemActionLabels.ContainsKey(mapping.Target)) return Mapping.SystemActionLabels[mapping.Target];
            if (mapping.Kind == ActionKind.Sequence) { try { return "Action sequence  •  " + SequenceCodec.Parse(mapping.Target).Count + " steps"; } catch { return "Action sequence"; } }
            if (mapping.Kind == ActionKind.Conditional) { try { return ConditionalCodec.Parse(mapping.Target).Summary; } catch { return "Conditional action"; } }
            return Mapping.Labels[(int)mapping.Kind];
        }

        internal static List<MappingSearchResult> Filter(IEnumerable<MappingSearchResult> source, string query)
        {
            string[] terms = (query ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return source.Where(x => terms.All(t => x.SearchText.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        }
    }

    internal sealed class MappingSearchForm : Form
    {
        readonly List<MappingSearchResult> all;
        readonly TextBox search = new TextBox();
        readonly ListBox results = new ListBox();
        readonly Label action = UiStyle.Text("", 10, false), count = UiStyle.Text("", 9, false);
        internal MappingSearchResult SelectedResult { get { return results.SelectedItem as MappingSearchResult; } }

        internal MappingSearchForm(Configuration defaults, ProfileCollection profiles)
        {
            all = MappingSearchIndex.Build(defaults, profiles);
            Text = "KiWeave · Find mappings"; Icon = Program.AppIcon(); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(820, 610); MinimumSize = new Size(650, 470); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 5 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(root);
            var header = UiStyle.Stack(); header.Controls.Add(UiStyle.Text("Find mappings", 21, true)); header.Controls.Add(UiStyle.Text("Search profiles, layers, keys, shortcuts, conditions, sequences, apps, paths, and actions. Private targets help matching but are not shown in results.", 9, false)); root.Controls.Add(header);
            search.AccessibleName = "Search all mappings"; search.Margin = new Padding(0, 16, 0, 12); root.Controls.Add(UiStyle.Field("Search", search));
            results.BackColor = UiStyle.Surface; results.ForeColor = UiStyle.Ink; results.BorderStyle = BorderStyle.FixedSingle; results.IntegralHeight = false; results.Dock = DockStyle.Fill; results.Font = new Font("Segoe UI", 10); root.Controls.Add(results);
            action.Margin = new Padding(0, 12, 0, 2); root.Controls.Add(action);
            var footer = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 10, 0, 0) }; footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); count.Margin = new Padding(0, 9, 0, 0); footer.Controls.Add(count, 0, 0);
            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty }; buttons.Controls.Add(UiStyle.Button("Close", delegate { Close(); })); buttons.Controls.Add(UiStyle.Button("Open selected", OpenSelected, true)); footer.Controls.Add(buttons, 1, 0); root.Controls.Add(footer);
            search.TextChanged += delegate { RefreshResults(); }; results.SelectedIndexChanged += delegate { action.Text = SelectedResult == null ? "Choose a result." : SelectedResult.Action + "  •  " + SelectedResult.Maturity; }; results.DoubleClick += OpenSelected;
            Shown += delegate { search.Focus(); }; RefreshResults();
        }
        void RefreshResults()
        {
            var filtered = MappingSearchIndex.Filter(all, search.Text); results.BeginUpdate(); results.Items.Clear(); results.Items.AddRange(filtered.Cast<object>().ToArray()); results.EndUpdate(); count.Text = filtered.Count + (filtered.Count == 1 ? " mapping" : " mappings"); if (results.Items.Count > 0) results.SelectedIndex = 0; else action.Text = "No mappings match that search.";
        }
        void OpenSelected(object sender, EventArgs e) { if (SelectedResult == null) return; DialogResult = DialogResult.OK; Close(); }
    }
}
