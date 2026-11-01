using System;
using System.Drawing;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class FirstPartyExtensionsForm : Form
    {
        readonly TextBox search = new DesignTextBox();
        readonly Label stateWarning = new Label();
        readonly FlowLayoutPanel installed = new FlowLayoutPanel(), featured = new FlowLayoutPanel();
        internal FirstPartyExtensionsForm(IWin32Window owner)
        {
            Text = "KiWeave - First-party extensions"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            ClientSize = new Size(900, 720); MinimumSize = new Size(700, 560); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var scroll = new DesignScrollPanel { Dock = DockStyle.Fill, Padding = new Padding(28) }; Controls.Add(scroll);
            var root = UiStyle.Stack(); scroll.Controls.Add(root);
            root.Controls.Add(UiStyle.Text("First-party extensions", 24, true));
            root.Controls.Add(UiStyle.Text("Optional KiWeave integrations live here instead of being mixed into the core shortcut editor. They are built and reviewed with KiWeave; this page never runs downloaded code.", 10, false));
            stateWarning.AutoSize = true; stateWarning.MaximumSize = new Size(820, 0); stateWarning.ForeColor = Color.FromArgb(255, 190, 110); stateWarning.Margin = new Padding(0, 10, 0, 0); root.Controls.Add(stateWarning); RefreshStateWarning();
            var searchRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 18, 0, 18) }; searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            search.Dock = DockStyle.Top; search.Margin = Padding.Empty; search.TextChanged += delegate { BuildRows(); }; new ToolTip().SetToolTip(search, "Search extensions");
            var searchField = UiStyle.Field("Search extensions", search); searchField.Margin = new Padding(0, 0, 10, 0); searchRow.Controls.Add(searchField, 0, 0); searchRow.Controls.Add(UiStyle.Button("Clear", delegate { search.Clear(); }), 1, 0); root.Controls.Add(searchRow);
            AddSection(root, "Installed extensions", installed);
            AddSection(root, "Featured first-party extensions", featured);
            var note = UiStyle.Text("Disabling an extension keeps existing mappings and notes intact. Those mappings simply fail safely until the extension is enabled again.", 9, false); note.Margin = new Padding(0, 14, 0, 10); root.Controls.Add(note);
            var credit = UiStyle.Text("Browsing layout inspired by Windhawk's installed-mod and featured-mod pages. KiWeave's extension model, permissions, and safety rules are original.", 8.5f, false); credit.ForeColor = Color.FromArgb(145, 140, 170); credit.Margin = new Padding(0, 0, 0, 14); root.Controls.Add(credit);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true }; buttons.Controls.Add(UiStyle.Button("Done", delegate { Close(); }, true)); root.Controls.Add(buttons);
            BuildRows();
        }
        void AddSection(TableLayoutPanel root, string title, FlowLayoutPanel cards)
        {
            var label = UiStyle.Text(title, 17, true); label.Margin = new Padding(0, 0, 0, 10); root.Controls.Add(label);
            cards.Dock = DockStyle.Top; cards.AutoSize = true; cards.WrapContents = true; cards.FlowDirection = FlowDirection.LeftToRight; cards.Margin = new Padding(0, 0, 0, 16); root.Controls.Add(cards);
        }
        void RefreshStateWarning() { stateWarning.Text = FirstPartyExtensionCatalog.LoadWarning; stateWarning.Visible = !String.IsNullOrWhiteSpace(stateWarning.Text); }
        void BuildRows()
        {
            installed.SuspendLayout(); featured.SuspendLayout(); ClearAndDispose(installed); ClearAndDispose(featured);
            string query = (search.Text ?? "").Trim();
            foreach (var item in FirstPartyExtensionCatalog.Items) {
                if (query.Length > 0 && (item.Name + " " + item.Description + " " + item.Maturity + " " + item.Permissions).IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var card = new DesignCard { Width = 395, Height = 178, Margin = new Padding(0, 0, 14, 14), Padding = new Padding(14) };
                var stack = UiStyle.Stack(); stack.Dock = DockStyle.Fill; card.Controls.Add(stack);
                var title = UiStyle.Text(item.Name, 13, true); title.Margin = new Padding(0, 0, 0, 4); stack.Controls.Add(title);
                var description = UiStyle.Text(item.Description, 9, false); description.MaximumSize = new Size(360, 0); description.Margin = new Padding(0, 0, 0, 6); stack.Controls.Add(description);
                var details = UiStyle.Text(item.Maturity + "  ·  " + item.Permissions, 8.5f, false); details.MaximumSize = new Size(360, 0); details.ForeColor = UiStyle.Blue; details.Margin = new Padding(0, 0, 0, 8); stack.Controls.Add(details);
                bool initiallyEnabled = FirstPartyExtensionCatalog.IsEnabled(item.Id);
                var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
                var enable = UiStyle.Button(initiallyEnabled ? "Disable" : "Enable", delegate { try { FirstPartyExtensionCatalog.SetEnabled(item.Id, !FirstPartyExtensionCatalog.IsEnabled(item.Id)); RefreshStateWarning(); BuildRows(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Extension", MessageBoxButtons.OK, MessageBoxIcon.Information); } });
                actions.Controls.Add(enable);
                actions.Controls.Add(UiStyle.Button("Details", delegate { using (var dialog = new FirstPartyExtensionDetailsForm(item)) dialog.ShowDialog(this); }));
                stack.Controls.Add(actions);
                (FirstPartyExtensionCatalog.IsEnabled(item.Id) ? installed : featured).Controls.Add(card);
            }
            installed.ResumeLayout(true); featured.ResumeLayout(true);
        }
        static void ClearAndDispose(Control parent)
        {
            while (parent.Controls.Count > 0) {
                Control child = parent.Controls[0]; parent.Controls.RemoveAt(0); child.Dispose();
            }
        }
    }

    internal sealed class FirstPartyExtensionDetailsForm : Form
    {
        internal FirstPartyExtensionDetailsForm(FirstPartyExtension item)
        {
            Text = "KiWeave - " + item.Name; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            ClientSize = new Size(600, 500); MinimumSize = new Size(520, 420); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var scroll = new DesignScrollPanel { Dock = DockStyle.Fill, Padding = new Padding(26) }; Controls.Add(scroll);
            var root = UiStyle.Stack(); scroll.Controls.Add(root);
            root.Controls.Add(UiStyle.Text(item.Name, 24, true));
            var state = UiStyle.Text(FirstPartyExtensionCatalog.IsEnabled(item.Id) ? "Enabled" : "Disabled", 10, true); state.ForeColor = FirstPartyExtensionCatalog.IsEnabled(item.Id) ? Color.FromArgb(127, 214, 169) : UiStyle.Muted; root.Controls.Add(state);
            Add(root, "Description", item.Description); Add(root, "Current status", FirstPartyExtensionCatalog.Status(item)); Add(root, "Version", "Built into KiWeave " + KiWeaveBuildIdentity.CurrentVersion); Add(root, "Compatibility", "KiWeave 1.0 series and newer compatible releases"); Add(root, "Permissions", item.Permissions); Add(root, "Maturity", item.Maturity);
            root.Controls.Add(UiStyle.Text("No extension code is downloaded or executed from this view. Existing mappings remain local and are preserved if the extension is disabled.", 9, false));
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 18, 0, 0) };
            if (item.Id == "discord") buttons.Controls.Add(UiStyle.Button("Reconnect", delegate { DiscordIntegration.Reconnect(); MessageBox.Show(this, DiscordIntegration.Status, "Discord", MessageBoxButtons.OK, MessageBoxIcon.Information); }));
            buttons.Controls.Add(UiStyle.Button("Done", delegate { Close(); }, true)); root.Controls.Add(buttons);
        }
        static void Add(TableLayoutPanel root, string title, string value)
        {
            var label = UiStyle.Text(title, 9, true); label.Margin = new Padding(0, 14, 0, 3); root.Controls.Add(label);
            var text = UiStyle.Text(value ?? "Unavailable", 10, false); text.Margin = new Padding(0, 0, 0, 2); root.Controls.Add(text);
        }
    }
}
