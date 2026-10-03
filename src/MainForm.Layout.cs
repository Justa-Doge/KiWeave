using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal static class UiStyle
    {
        internal static readonly Color Canvas = Color.FromArgb(23, 24, 32), Ink = Color.FromArgb(237, 235, 245),
            Muted = Color.FromArgb(165, 161, 181), Blue = Color.FromArgb(187, 164, 255), AccentFill = Color.FromArgb(112, 82, 202),
            Border = Color.FromArgb(56, 56, 74), Soft = Color.FromArgb(52, 43, 77), Sidebar = Color.FromArgb(16, 17, 25),
            Surface = Color.FromArgb(31, 32, 44), Input = Color.FromArgb(39, 40, 54);
        internal static Label Text(string text, float size, bool bold)
        {
            return new DesignLabel { Text = text, AutoSize = true, Dock = DockStyle.Top, BackColor = Color.Transparent,
                ForeColor = bold ? Ink : Muted, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(0, 0, 0, 8) };
        }
        internal static Button Button(string text, EventHandler action, bool primary = false)
        {
            var b = new DesignButton { Text = text, Primary = primary }; b.Click += action; return b;
        }
        internal static TableLayoutPanel Stack()
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.Transparent };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); return t;
        }
        internal static Control Field(string caption, Control input)
        {
            var text = input as TextBox; if (text != null) input = new InputFrame(text);
            var t = Stack(); t.Margin = new Padding(0, 0, 0, 18); t.RowCount = 2;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label = Text(caption, 9, true); label.Margin = new Padding(0, 0, 0, 8);
            t.Controls.Add(label, 0, 0); input.Dock = DockStyle.Top; input.Margin = new Padding(0, 0, 0, 4);
            // Native combo boxes report a smaller preferred height than their actual chrome.
            // A dedicated host reserves the complete measured height, including the lower border.
            var combo = input as ComboBox;
            if (combo != null) {
                var host = new Panel { Dock = DockStyle.Top, Height = Math.Max(combo.Height, combo.PreferredSize.Height) + 6, Margin = Padding.Empty };
                combo.Dock = DockStyle.Top; combo.Margin = Padding.Empty; host.Controls.Add(combo);
                combo.SizeChanged += delegate { int h = Math.Max(combo.Height, combo.PreferredSize.Height) + 6; if (host.Height != h) host.Height = h; };
                t.Controls.Add(host, 0, 1);
            } else t.Controls.Add(input, 0, 1);
            return t;
        }
        internal static void Combo(ComboBox box)
        {
            box.DropDownStyle = ComboBoxStyle.DropDownList; box.IntegralHeight = true; box.MaxDropDownItems = 9;
            box.Dock = DockStyle.Top; box.Margin = new Padding(0, 0, 0, 6);
        }
        internal static void Wrap(Control container)
        {
            foreach (Control c in container.Controls) {
                var label = c as Label;
                if (label != null && label.AutoSize) label.MaximumSize = new Size(Math.Max(100, container.ClientSize.Width - container.Padding.Horizontal - 12), 0);
                Wrap(c);
            }
        }
    }

    public sealed partial class MainForm
    {
        readonly Panel functionPage = new Panel(), customPage = new Panel(), settingsPage = new Panel(), customHotkeyView = new Panel(), powerToysView = new Panel();
        DesignButton hotkeysTab, powerToysTab;
        readonly Label pageTitle = UiStyle.Text("Function keys", 26, true), pageSubtitle = UiStyle.Text("", 10, false);
        DesignButton functionNav, customNav, settingsNav;
        readonly DesignButton profileBadge = new DesignButton();
        readonly Label customTitle = UiStyle.Text("Edit shortcut", 19, true);
        readonly ComboBox layerView = new DesignComboBox();
        readonly Label customHelp = UiStyle.Text("", 10, false), sequenceSummary = UiStyle.Text("", 10, false);
        readonly Panel customEditorHost = new Panel();
        readonly Label customEmpty = UiStyle.Text("Create your first shortcut\n\nChoose Add hotkey to get started.", 15, true);
        Control functionTargetField, functionArgsField, functionWorkField, customTargetField, customArgsField, customWorkField;
        Control sequenceField;
        Button removeCustomButton, functionRecord, customShortcutRecord, customActionRecord, functionConditionalButton, customConditionalButton;
        Button moreButton, saveChangesButton;
        TableLayoutPanel functionStack, customStack;

        void BuildUi()
        {
            BackColor = UiStyle.Canvas;
            Design.DarkTitlebar(this);
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 206)); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(shell);
            var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = UiStyle.Sidebar, Margin = Padding.Empty, Padding = new Padding(20, 28, 20, 22) }; shell.Controls.Add(sidebar, 0, 0);
            var sideTop = UiStyle.Stack(); sidebar.Controls.Add(sideTop);
            var logo = new Panel { Height = 60, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 14) };
            logo.Paint += delegate(object sender, PaintEventArgs e) {
                Design.Box(e.Graphics, new Rectangle(0, 0, 48, 48), UiStyle.AccentFill, Color.FromArgb(159, 137, 240), 13);
                using (var f = new Font("Segoe UI", 23, FontStyle.Bold)) {
                    var size = e.Graphics.MeasureString("F", f);
                    Design.ShadowedF(e.Graphics, f, (48 - size.Width) / 2, (47 - size.Height) / 2);
                }
            }; sideTop.Controls.Add(logo);
            var brand = UiStyle.Text("KiWeave", 17, true); brand.ForeColor = Color.White; sideTop.Controls.Add(brand);
            var subbrand = UiStyle.Text("A little more control.", 9, false); subbrand.ForeColor = Color.FromArgb(155, 155, 179); subbrand.Margin = new Padding(0, 0, 0, 38); sideTop.Controls.Add(subbrand);
            var workspace = UiStyle.Text("Workspace", 9, true); workspace.ForeColor = Color.FromArgb(132, 132, 157); workspace.Margin = new Padding(8, 0, 0, 12); sideTop.Controls.Add(workspace);
            functionNav = Nav("Function keys", "\uE765", delegate { SelectPage(0); });
            customNav = Nav("Custom hotkeys", "\uE70F", delegate { SelectPage(1); });
            sideTop.Controls.Add(functionNav); sideTop.Controls.Add(customNav);
            var sidebarBottom = new Panel { Dock = DockStyle.Bottom, Height = 178, Margin = Padding.Empty };
            var safety = UiStyle.Stack(); safety.Dock = DockStyle.Fill; safety.Padding = new Padding(8, 0, 8, 4);
            var safeTitle = UiStyle.Text("Always in control", 10, true); safeTitle.ForeColor = Color.FromArgb(214, 210, 237); safety.Controls.Add(safeTitle);
            var safeText = UiStyle.Text("Hold Ctrl + Alt + Shift\nfor 1.5s to pause shortcuts.", 9, false); safeText.ForeColor = Color.FromArgb(156, 155, 180); safety.Controls.Add(safeText); sidebar.Controls.Add(safety);
            settingsNav = Nav("Settings", "\uE713", delegate { SelectPage(2); }); settingsNav.Dock = DockStyle.Bottom;
            sidebarBottom.Controls.Add(safety); sidebarBottom.Controls.Add(settingsNav); sidebar.Controls.Add(sidebarBottom);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26, 28, 26, 20), ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); shell.Controls.Add(root, 1, 0);
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 215));
            var headings = UiStyle.Stack(); headings.Controls.Add(pageTitle); headings.Controls.Add(pageSubtitle); header.Controls.Add(headings, 0, 0);
            var active = UiStyle.Stack(); active.Padding = new Padding(0, 4, 0, 0);
            enabled.Text = "Shortcuts enabled"; enabled.AutoSize = false; enabled.Size = new Size(206, 36); enabled.CheckedChanged += ToggleEnabled;
            profileBadge.AutoSize = false; profileBadge.Size = new Size(206, 34); profileBadge.MinimumSize = new Size(206, 34); profileBadge.Padding = new Padding(10, 4, 10, 4); profileBadge.Margin = new Padding(0, 2, 0, 0);
            profileBadge.Text = "Profile: Default"; profileBadge.Click += delegate { OpenProfileStatus(); };
            active.Controls.Add(enabled); active.Controls.Add(profileBadge); header.Controls.Add(active, 1, 0); root.Controls.Add(header, 0, 0);
            var pages = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            functionPage.Dock = customPage.Dock = settingsPage.Dock = DockStyle.Fill; pages.Controls.Add(functionPage); pages.Controls.Add(customPage); pages.Controls.Add(settingsPage); root.Controls.Add(pages, 0, 1);
            BuildFunctionPage(functionPage); BuildCustomPage(customPage); BuildSettingsPage(settingsPage);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            feedback.Dock = DockStyle.Fill; feedback.ForeColor = muted; feedback.Text = "All set. Edit a key to get started."; feedback.Font = new Font("Segoe UI", 9); feedback.Padding = new Padding(0, 10, 12, 0); footer.Controls.Add(feedback, 0, 0);
            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            moreButton = UiStyle.Button("More", delegate { });
            var menu = Design.DarkMenu(Font);
            menu.Items.Add("Profiles...", null, delegate { OpenProfiles(); }); menu.Items.Add("Diagnostics...", null, delegate { OpenDiagnostics(); }); menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Back up everything...", null, ExportBackup); menu.Items.Add("Restore backup...", null, ImportBackup); menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Import configuration...", null, Import); menu.Items.Add("Export configuration...", null, Export);
            menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Reset function keys", null, delegate { Bulk(false); });
            menu.Items.Add("Disable all function keys", null, delegate { Bulk(true); }); menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("About KiWeave...", null, delegate { using (var about = new AboutForm()) about.ShowDialog(this); });
            menu.Items.Add("Open log folder", null, delegate { try { AppLog.OpenFolder(); } catch (Exception ex) { SetFeedback("Could not open the log folder: " + ex.Message, true); } });
            menu.Items.Add("Exit app", null, delegate { ExitApp(); });
            moreButton.Click += delegate { menu.Show(moreButton, new Point(0, moreButton.Height)); }; moreButton.Disposed += delegate { menu.Dispose(); };
            hideToTray = UiStyle.Button("Hide to tray", delegate { if (preferences.UseTray) Hide(); });
            saveChangesButton = UiStyle.Button("Save changes", delegate { Save(); }, true);
            buttons.Controls.Add(moreButton); buttons.Controls.Add(hideToTray); buttons.Controls.Add(saveChangesButton);
            footer.Controls.Add(buttons, 1, 0); root.Controls.Add(footer, 0, 2); SelectPage(0);
        }
        DesignButton Nav(string text, string glyph, EventHandler click)
        {
            var b = new DesignButton { Text = text, Glyph = glyph, Sidebar = true, AutoSize = false, Height = 48, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            b.Click += click; return b;
        }
        void SelectPage(int index)
        {
            functionPage.Visible = index == 0; customPage.Visible = index == 1; settingsPage.Visible = index == 2;
            functionNav.Active = index == 0; customNav.Active = index == 1; settingsNav.Active = index == 2;
            functionNav.Invalidate(); customNav.Invalidate(); settingsNav.Invalidate();
            moreButton.Visible = index != 2; saveChangesButton.Visible = index != 2;
            pageTitle.Text = index == 0 ? "Function keys" : index == 1 ? "Custom hotkeys" : "Settings";
            pageSubtitle.Text = index == 0 ? "Small keys. Big possibilities." : index == 2 ? "Make KiWeave feel at home." :
                powerToysView.Visible ? "PowerToys shortcuts, right where your hotkeys live." : "Your favorite actions, one shortcut away.";
            if (index == 2 && feedback.Text == "All set. Edit a key to get started.") feedback.Text = "Settings save as you change them.";
            else if (index != 2 && feedback.Text == "Settings save as you change them.") feedback.Text = "All set. Edit a key to get started.";
        }

        void BuildSettingsPage(Control page)
        {
            var columns = PageColumns(page);
            var general = Card(); general.Margin = new Padding(0, 0, 16, 0); columns.Controls.Add(general, 0, 0);
            var left = UiStyle.Stack(); general.Controls.Add(left);
            left.Controls.Add(UiStyle.Text("General", 15, true));
            var intro = UiStyle.Text("Background behavior saves as soon as you change it.", 9, false); intro.Margin = new Padding(0, 0, 0, 22); left.Controls.Add(intro);
            AddSetting(left, startup, "Start with Windows", "Launch KiWeave quietly when you sign in.", ToggleStartup);
            AddSetting(left, useTray, "Keep running in tray", "Closing the window keeps your shortcuts active.", ToggleTray);
            AddSetting(left, automaticProfiles, "Switch profiles automatically", "Use app matches from Profiles while KiWeave is in the background.", ToggleBackgroundPreference);
            AddSetting(left, networkAccess, "Allow network access", "Administrator approval is required. Master switch for GitHub update checks and user-triggered HTTP actions; local remapping stays available when off.", ToggleBackgroundPreference);
            AddSetting(left, checkUpdates, "Check for updates automatically", "Requires Allow network access. Checks GitHub at launch and every 12 hours; only notifies, never downloads.", ToggleBackgroundPreference);

            var tools = Card(); columns.Controls.Add(tools, 1, 0);
            var right = UiStyle.Stack(); tools.Controls.Add(right);
            right.Controls.Add(UiStyle.Text("Tools and data", 15, true));
            var toolsHelp = UiStyle.Text("Back up your setup, troubleshoot problems, or revisit the basics.", 9, false); toolsHelp.Margin = new Padding(0, 0, 0, 18); right.Controls.Add(toolsHelp);
            var primary = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 12) };
            primary.Controls.Add(UiStyle.Button("Check for updates", delegate { CheckForUpdatesNow(); }, true));
            primary.Controls.Add(UiStyle.Button("Manage profiles", delegate { OpenProfiles(); })); right.Controls.Add(primary);
            var backup = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 12) };
            backup.Controls.Add(UiStyle.Button("Back up everything", ExportBackup)); backup.Controls.Add(UiStyle.Button("Restore backup", ImportBackup)); right.Controls.Add(backup);
            var help = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 12) };
            help.Controls.Add(UiStyle.Button("Welcome guide", delegate { using (var welcome = new WelcomeForm()) welcome.ShowDialog(this); }));
            help.Controls.Add(UiStyle.Button("Privacy center", delegate { OpenPrivacyCenter(); }, true));
            help.Controls.Add(UiStyle.Button("Find mappings", delegate { OpenMappingSearch(); }, true));
            help.Controls.Add(UiStyle.Button("Live key tester", delegate { OpenLiveKeyTester(); }));
            help.Controls.Add(UiStyle.Button("Conflict center", delegate { OpenConflictCenter(); }));
            help.Controls.Add(UiStyle.Button("Undo and history", delegate { OpenHistory(); }));
            help.Controls.Add(UiStyle.Button("Diagnostics", delegate { OpenDiagnostics(); })); right.Controls.Add(help);
            var folders = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 12) };
            folders.Controls.Add(UiStyle.Button("Open data folder", delegate { OpenDataFolder(); }));
            folders.Controls.Add(UiStyle.Button("Open log folder", delegate { try { AppLog.OpenFolder(); } catch (Exception ex) { SetFeedback("Could not open the log folder: " + ex.Message, true); } })); right.Controls.Add(folders);
            right.Controls.Add(UiStyle.Button("Discord connection", delegate { using (var dialog = new DiscordConnectionForm(this)) dialog.ShowDialog(this); }));
            right.Controls.Add(UiStyle.Button("About KiWeave", delegate { using (var about = new AboutForm()) about.ShowDialog(this); }));
        }

        void AddSetting(TableLayoutPanel stack, CheckBox box, string title, string description, EventHandler changed)
        {
            var row = UiStyle.Stack(); row.Margin = new Padding(0, 0, 0, 12);
            box.Text = title; box.AutoSize = true; box.ForeColor = UiStyle.Ink; box.Font = new Font("Segoe UI", 10, FontStyle.Bold); box.Margin = new Padding(0, 0, 0, 5); box.CheckedChanged += changed; row.Controls.Add(box);
            var help = UiStyle.Text(description, 9, false); help.Margin = new Padding(24, 0, 0, 0); row.Controls.Add(help); stack.Controls.Add(row);
        }
        TableLayoutPanel PageColumns(Control page)
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63)); page.Controls.Add(t); return t;
        }
        Panel Card()
        {
            return new DesignCard { Dock = DockStyle.Fill, Margin = Padding.Empty };
        }
        TableLayoutPanel EditorStack(Panel card)
        {
            var scroll = new DesignScrollPanel { Dock = DockStyle.Fill }; card.Controls.Add(scroll);
            var stack = UiStyle.Stack(); scroll.Controls.Add(stack);
            scroll.SizeChanged += delegate { UiStyle.Wrap(stack); }; return stack;
        }
        void PrepareList(ListView view, string keyCaption)
        {
            view.Dock = DockStyle.Fill; view.View = View.Details; view.FullRowSelect = true; view.MultiSelect = false; view.HideSelection = false;
            view.HeaderStyle = ColumnHeaderStyle.None; view.OwnerDraw = true; view.BorderStyle = BorderStyle.None; view.BackColor = UiStyle.Surface; view.ForeColor = ink;
            view.Columns.Add(keyCaption, keyCaption == "KEY" ? 65 : 114); view.Columns.Add("Action", 230);
            view.SmallImageList = new ImageList { ImageSize = new Size(1, keyCaption == "KEY" ? 39 : 84) };
            EventHandler fitColumns = delegate {
                if (keyCaption != "KEY") { view.Columns[0].Width = Math.Max(20, view.ClientSize.Width - 10); view.Columns[1].Width = 0; }
                else { int width = Math.Max(80, view.ClientSize.Width - view.Columns[0].Width - 10); if (view.Columns[1].Width != width) view.Columns[1].Width = width; }
            };
            view.DrawItem += delegate(object sender, DrawListViewItemEventArgs e) {
                // Windows can repaint one subitem when the pointer enters a row. Drawing the
                // background in DrawItem but text later in DrawSubItem lets that partial repaint
                // erase the action text. Paint the complete row in one pass instead.
                var r = new Rectangle(0, e.Bounds.Y, view.ClientSize.Width, e.Bounds.Height);
                bool selected = e.Item.Selected;
                using (var brush = new SolidBrush(UiStyle.Surface)) e.Graphics.FillRectangle(brush, r);
                if (selected) Design.Box(e.Graphics, new Rectangle(0, r.Y + 2, r.Width - 4, r.Height - 4), UiStyle.Soft, UiStyle.Soft, 8);
                if (keyCaption != "KEY") {
                    var keycap = new Rectangle(10, r.Y + 10, Math.Min(r.Width - 20, 126), 27);
                    Design.Box(e.Graphics, keycap, selected ? UiStyle.Input : UiStyle.Canvas, UiStyle.Border, 6);
                    using (var font = new Font("Segoe UI", 9, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, e.Item.Text, font, keycap, selected ? UiStyle.Blue : UiStyle.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(e.Graphics, e.Item.SubItems[1].Text, Font, new Rectangle(10, r.Y + 45, r.Width - 20, 25), UiStyle.Ink, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
                    return;
                }
                var cap = new Rectangle(7, r.Y + 7, view.Columns[0].Width - 15, r.Height - 14);
                Design.Box(e.Graphics, cap, selected ? UiStyle.Input : UiStyle.Canvas, selected ? Color.FromArgb(115, 92, 169) : UiStyle.Border, 6);
                using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
                    TextRenderer.DrawText(e.Graphics, e.Item.Text, font, cap, selected ? UiStyle.Blue : UiStyle.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(e.Graphics, e.Item.SubItems[1].Text, Font,
                    new Rectangle(view.Columns[0].Width + 4, r.Y, Math.Max(1, r.Width - view.Columns[0].Width - 8), r.Height),
                    selected ? UiStyle.Blue : UiStyle.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            view.DrawSubItem += delegate { }; // DrawItem owns every pixel of each row.
            view.SelectedIndexChanged += delegate { view.Invalidate(); };
            view.Resize += fitColumns; view.HandleCreated += fitColumns; view.VisibleChanged += fitColumns;
        }
        Control PathRow(TextBox input, params Button[] buttons)
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = buttons.Length + 1, Margin = Padding.Empty };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); var frame = new InputFrame(input) { Margin = new Padding(0, 0, 8, 0) }; t.Controls.Add(frame, 0, 0);
            for (int i = 0; i < buttons.Length; i++) { t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); t.Controls.Add(buttons[i], i + 1, 0); }
            return t;
        }
        void BuildFunctionPage(Control page)
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); page.Controls.Add(layout);
            var layerBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
            layerBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); layerBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layerBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var layerLabel = UiStyle.Text("Editing", 9, true); layerLabel.Margin = new Padding(0, 11, 10, 0); layerBar.Controls.Add(layerLabel, 0, 0);
            UiStyle.Combo(layerView); layerView.Width = 260; layerView.Dock = DockStyle.Left; layerView.SelectedIndexChanged += delegate { SelectLayerView(layerView.SelectedIndex - 1); }; layerBar.Controls.Add(layerView, 1, 0);
            layerBar.Controls.Add(UiStyle.Button("Manage layers", delegate { ManageLayers(); }), 2, 0); layout.Controls.Add(layerBar, 0, 0);
            var editorHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty }; layout.Controls.Add(editorHost, 0, 1);
            var columns = PageColumns(editorHost); var left = Card(); left.Margin = new Padding(0, 0, 16, 0); columns.Controls.Add(left, 0, 0);
            var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); rows.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rows.Controls.Add(UiStyle.Text("Your function row", 9, true), 0, 0); PrepareList(list, "KEY"); rows.Controls.Add(list, 0, 1); left.Controls.Add(rows);
            list.SelectedIndexChanged += delegate { if (!loading && list.SelectedIndices.Count == 1) LoadEditor(list.SelectedIndices[0]); };
            var right = Card(); columns.Controls.Add(right, 1, 0); functionStack = EditorStack(right);
            editorTitle.AutoSize = true; editorTitle.Font = new Font("Segoe UI", 20, FontStyle.Bold); editorTitle.Margin = new Padding(0, 0, 0, 18); functionStack.Controls.Add(editorTitle);
            functionStack.Controls.Add(UiStyle.Text("Choose what happens when you press this key.", 9, false));
            kind.Items.AddRange(Mapping.Labels);
            UiStyle.Combo(simpleKind); simpleKind.Items.AddRange(FunctionGroups); functionStack.Controls.Add(UiStyle.Field("Action category", simpleKind));
            simpleKind.SelectedIndexChanged += delegate { if (!loading) { loading = true; PopulateChoices(specificKind, simpleKind.SelectedIndex, false, null); loading = false; ApplyFunctionChoice(); } };
            UiStyle.Combo(specificKind); functionStack.Controls.Add(UiStyle.Field("Action", specificKind)); specificKind.SelectedIndexChanged += delegate { ApplyFunctionChoice(); };
            var functionTools = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0, 0, 0, 16) };
            functionTools.Controls.Add(UiStyle.Button("Browse action library", delegate { PickAction(false); }));
            functionConditionalButton = UiStyle.Button("Edit condition", delegate { OpenConditionalBuilder(false); }); functionTools.Controls.Add(functionConditionalButton);
            functionTools.Controls.Add(UiStyle.Button("Action info", delegate { ShowActionInfo(CurrentMappings()[selected]); }));
            functionTools.Controls.Add(UiStyle.Button("Test action", delegate { TestAction(CurrentMappings()[selected]); })); functionStack.Controls.Add(functionTools);
            BuildMonitorEditor(); functionStack.Controls.Add(monitorPanel);
            browse.Text = "Browse..."; browse.AutoSize = true; browse.Click += BrowseTarget; folder.Text = "Folder..."; folder.AutoSize = true;
            folder.Click += delegate { using (var d = new FolderBrowserDialog()) if (d.ShowDialog(this) == DialogResult.OK) target.Text = d.SelectedPath; };
            functionRecord = UiStyle.Button("Record", delegate { RecordShortcut(target, false); });
            functionTargetField = UiStyle.Field("Key, shortcut or file", PathRow(target, functionRecord, browse, folder)); functionStack.Controls.Add(functionTargetField);
            UiStyle.Combo(media); media.Items.AddRange(Shortcuts.MediaLabels.Values.ToArray()); // Kept as the internal media-value adapter; the action picker displays it.
            functionArgsField = UiStyle.Field("Arguments (optional)", arguments); functionStack.Controls.Add(functionArgsField);
            workBrowse.Text = "Browse..."; workBrowse.AutoSize = true;
            workBrowse.Click += delegate { using (var d = new FolderBrowserDialog()) if (d.ShowDialog(this) == DialogResult.OK) working.Text = d.SelectedPath; };
            functionWorkField = UiStyle.Field("Working folder (optional)", PathRow(working, workBrowse)); functionStack.Controls.Add(functionWorkField);
            hint.AutoSize = true; hint.Font = new Font("Segoe UI", 9); hint.Dock = DockStyle.Top; hint.ForeColor = muted; hint.Margin = new Padding(0, 20, 0, 0); functionStack.Controls.Add(hint);
            target.TextChanged += delegate { Edited(); }; arguments.TextChanged += delegate { Edited(); }; working.TextChanged += delegate { Edited(); }; media.SelectedIndexChanged += delegate { Edited(); };
        }
        void BuildCustomPage(Control page)
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            page.Controls.Add(layout);
            var tabs = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            hotkeysTab = new DesignButton { Text = "My hotkeys", Width = 146, Height = 42, Margin = new Padding(0, 0, 10, 0) };
            powerToysTab = new DesignButton { Text = "PowerToys", Width = 146, Height = 42, Margin = Padding.Empty };
            hotkeysTab.Click += delegate { SelectCustomSection(false); };
            powerToysTab.Click += delegate { SelectCustomSection(true); };
            tabs.Controls.Add(hotkeysTab); tabs.Controls.Add(powerToysTab); layout.Controls.Add(tabs, 0, 0);
            var viewHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            customHotkeyView.Dock = powerToysView.Dock = DockStyle.Fill;
            viewHost.Controls.Add(customHotkeyView); viewHost.Controls.Add(powerToysView); layout.Controls.Add(viewHost, 0, 1);
            powerToysView.Controls.Add(new PowerToysPanel(PrepareList) { Dock = DockStyle.Fill });
            var columns = PageColumns(customHotkeyView); var left = Card(); left.Margin = new Padding(0, 0, 16, 0); columns.Controls.Add(left, 0, 0);
            var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); rows.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            rows.Controls.Add(UiStyle.Text("Saved hotkeys", 9, true), 0, 0); PrepareList(customList, "HOTKEY"); rows.Controls.Add(customList, 0, 1);
            var commands = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 0), WrapContents = false };
            commands.Controls.Add(UiStyle.Button("Add hotkey", delegate { AddCustomHotkey(); }));
            removeCustomButton = UiStyle.Button("Remove", delegate { RemoveCustomHotkey(); }); commands.Controls.Add(removeCustomButton);
            removeCustomButton.MinimumSize = new Size(70, 42); removeCustomButton.Padding = new Padding(8, 8, 8, 8);
            rows.Controls.Add(commands, 0, 2); left.Controls.Add(rows);
            customList.SelectedIndexChanged += delegate { if (!loading && customList.SelectedIndices.Count == 1) LoadCustomEditor(customList.SelectedIndices[0]); };
            var right = Card(); columns.Controls.Add(right, 1, 0);
            customEditorHost.Dock = DockStyle.Fill; right.Controls.Add(customEditorHost); customEmpty.Dock = DockStyle.Fill; right.Controls.Add(customEmpty);
            var scroll = new DesignScrollPanel { Dock = DockStyle.Fill }; customEditorHost.Controls.Add(scroll); customStack = UiStyle.Stack(); scroll.Controls.Add(customStack); scroll.SizeChanged += delegate { UiStyle.Wrap(customStack); };
            customShortcutRecord = UiStyle.Button("Record", delegate { RecordShortcut(customShortcut, true); });
            customStack.Controls.Add(customTitle); customStack.Controls.Add(UiStyle.Field("Shortcut (e.g. Ctrl+Alt+K)", PathRow(customShortcut, customShortcutRecord)));
            customKind.Items.AddRange(Mapping.Labels); UiStyle.Combo(customSimpleKind); customSimpleKind.Items.AddRange(CustomGroups);
            customStack.Controls.Add(UiStyle.Field("Action category", customSimpleKind));
            customSimpleKind.SelectedIndexChanged += delegate { if (!loading) { loading = true; PopulateChoices(customSpecificKind, customSimpleKind.SelectedIndex, true, null); loading = false; ApplyCustomChoice(); } };
            UiStyle.Combo(customSpecificKind); customStack.Controls.Add(UiStyle.Field("Action", customSpecificKind)); customSpecificKind.SelectedIndexChanged += delegate { ApplyCustomChoice(); };
            var tools = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0, 0, 0, 16) };
            tools.Controls.Add(UiStyle.Button("Browse action library", delegate { PickAction(true); }));
            tools.Controls.Add(UiStyle.Button("Build sequence", delegate { OpenSequenceBuilder(); }));
            customConditionalButton = UiStyle.Button("Edit condition", delegate { OpenConditionalBuilder(true); }); tools.Controls.Add(customConditionalButton);
            tools.Controls.Add(UiStyle.Button("Action info", delegate { if (customSelected >= 0 && customSelected < draft.CustomHotkeys.Length) ShowActionInfo(draft.CustomHotkeys[customSelected].Action); }));
            tools.Controls.Add(UiStyle.Button("Test action", delegate { if (customSelected >= 0 && customSelected < draft.CustomHotkeys.Length) TestAction(draft.CustomHotkeys[customSelected].Action); })); customStack.Controls.Add(tools);
            customBrowse = UiStyle.Button("Browse...", BrowseCustomTarget); customActionRecord = UiStyle.Button("Record", delegate { RecordShortcut(customTarget, false); });
            customTargetField = UiStyle.Field("Key, shortcut or file", PathRow(customTarget, customActionRecord, customBrowse)); customStack.Controls.Add(customTargetField);
            UiStyle.Combo(customMedia); customMedia.Items.AddRange(Shortcuts.MediaLabels.Values.ToArray());
            customArgsField = UiStyle.Field("Arguments (optional)", customArguments); customWorkField = UiStyle.Field("Working folder (optional)", customWorking);
            customStack.Controls.Add(customArgsField); customStack.Controls.Add(customWorkField);
            sequenceField = UiStyle.Field("Sequence", sequenceSummary); customStack.Controls.Add(sequenceField);
            customHelp.Margin = new Padding(0, 12, 0, 0); customStack.Controls.Add(customHelp);
            customShortcut.TextChanged += delegate { CustomEdited(); }; customTarget.TextChanged += delegate { CustomEdited(); }; customArguments.TextChanged += delegate { CustomEdited(); }; customWorking.TextChanged += delegate { CustomEdited(); }; customMedia.SelectedIndexChanged += delegate { CustomEdited(); };
            SelectCustomSection(false);
        }
        void SelectCustomSection(bool powerToys)
        {
            customHotkeyView.Visible = !powerToys; powerToysView.Visible = powerToys;
            hotkeysTab.Primary = !powerToys; powerToysTab.Primary = powerToys;
            hotkeysTab.Invalidate(); powerToysTab.Invalidate();
            pageSubtitle.Text = powerToys ? "PowerToys shortcuts, right where your hotkeys live." : "Your favorite actions, one shortcut away.";
        }
        void SetCustomEditorState(bool hasSelection)
        {
            customEditorHost.Visible = hasSelection; customEmpty.Visible = !hasSelection; removeCustomButton.Enabled = hasSelection;
        }
        void PickAction(bool custom)
        {
            using (var dialog = new ActionPickerForm(custom)) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var chosen = dialog.SelectedAction; if (chosen == null) return;
                if (custom && chosen.Kind == ActionKind.Sequence) { OpenSequenceBuilder(); return; }
                if (chosen.Kind == ActionKind.Conditional) { OpenConditionalBuilder(custom); return; }
                if (custom) {
                    var h = draft.CustomHotkeys[customSelected]; h.Action = chosen.Copy(); LoadCustomEditor(customSelected); CustomEdited();
                } else { CurrentMappings()[selected] = chosen.Copy(); LoadEditor(selected); Edited(); }
            }
        }
    }
}
