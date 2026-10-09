using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class DesignListView : ListView
    {
        [DllImport("user32.dll")]
        static extern bool ShowScrollBar(IntPtr hwnd, int bar, bool show);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);
        const int GwlStyle = -16, WsHscroll = 0x00100000, WsVscroll = 0x00200000;
        internal DesignListView() { DoubleBuffered = true; }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e); Design.DarkNative(this);
            try {
                ShowScrollBar(Handle, 3, false);
                long style = GetWindowLongPtr64(Handle, GwlStyle).ToInt64();
                style &= ~(WsHscroll | WsVscroll);
                SetWindowLongPtr64(Handle, GwlStyle, new IntPtr(style));
            } catch { }
        }
    }

    internal static class Design
    {
        enum PreferredAppMode { Default, AllowDark, ForceDark, ForceLight, Max }
        [DllImport("uxtheme.dll", EntryPoint = "#135")]
        static extern PreferredAppMode SetPreferredAppMode(PreferredAppMode mode);
        [DllImport("uxtheme.dll", EntryPoint = "#136")]
        static extern void FlushMenuThemes();
        internal static void EnableDarkAppMode()
        {
            try { SetPreferredAppMode(PreferredAppMode.ForceDark); FlushMenuThemes(); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);
        internal static void DarkNative(Control control)
        {
            EventHandler apply = delegate { try { SetWindowTheme(control.Handle, "DarkMode_Explorer", null); } catch { } };
            control.HandleCreated += apply;
            if (control.IsHandleCreated) apply(control, EventArgs.Empty);
        }
        internal static void ShadowedF(Graphics g, Font font, float x, float y)
        {
            // A soft, offset shadow gives the glyph depth without blurring its white face.
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                using (var shadow = new SolidBrush(Color.FromArgb(dx == 0 && dy == 0 ? 100 : 22, 30, 18, 65)))
                    g.DrawString("F", font, shadow, x + 1 + dx * .65f, y + 1.5f + dy * .65f);
            g.DrawString("F", font, Brushes.White, x, y);
        }
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        internal static void DarkTitlebar(Form form)
        {
            // Apply the selected text-only language after each dialog has built
            // its controls, while leaving unknown/dynamic text untouched.
            Localization.Apply(form);
            Action apply = delegate {
                if (!form.IsHandleCreated) return;
                try {
                    // Windows 10/11 use attribute 20 on current builds and 19 on
                    // older DWM revisions. Applying both keeps every KiWeave dialog
                    // out of the light caption fallback.
                    int on = 1; DwmSetWindowAttribute(form.Handle, 20, ref on, 4); DwmSetWindowAttribute(form.Handle, 19, ref on, 4);
                    int caption = ColorTranslator.ToWin32(UiStyle.Canvas), text = ColorTranslator.ToWin32(UiStyle.Ink);
                    DwmSetWindowAttribute(form.Handle, 35, ref caption, 4); DwmSetWindowAttribute(form.Handle, 36, ref text, 4);
                } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
            };
            form.HandleCreated += delegate { apply(); };
            form.Shown += delegate { Localization.Apply(form); apply(); };
            if (form.IsHandleCreated) apply();
        }
        internal static void GlassBackdrop(Form form, bool enabled)
        {
            form.HandleCreated += delegate { try { int value = enabled ? 2 : 0; DwmSetWindowAttribute(form.Handle, 38, ref value, 4); } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { } };
        }
        internal static ContextMenuStrip DarkMenu(Font font)
        {
            var menu = new ContextMenuStrip {
                Font = font, BackColor = UiStyle.Surface, ForeColor = UiStyle.Ink,
                Renderer = new ToolStripProfessionalRenderer(new DesignMenuColors())
            };
            menu.Opening += delegate { RefreshDarkMenu(menu); };
            return menu;
        }
        internal static void RefreshDarkMenu(ContextMenuStrip menu) { DarkMenuItems(menu.Items, menu.Renderer); }
        static void DarkMenuItems(ToolStripItemCollection items, ToolStripRenderer renderer)
        {
            foreach (ToolStripItem item in items) {
                item.BackColor = UiStyle.Surface; item.ForeColor = UiStyle.Ink;
                var submenu = item as ToolStripMenuItem;
                if (submenu == null || submenu.DropDownItems.Count == 0) continue;
                submenu.DropDown.BackColor = UiStyle.Surface; submenu.DropDown.ForeColor = UiStyle.Ink; submenu.DropDown.Renderer = renderer;
                DarkMenuItems(submenu.DropDownItems, renderer);
            }
        }
        internal static Color Background(Control control)
        {
            while (control != null) { if (control.BackColor.A == 255) return control.BackColor; control = control.Parent; }
            return UiStyle.Canvas;
        }
        internal static GraphicsPath Round(Rectangle r, int radius)
        {
            var p = new GraphicsPath(); int d = Math.Max(2, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            p.AddArc(r.Left, r.Top, d, d, 180, 90); p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        internal static void Box(Graphics g, Rectangle r, Color fill, Color border, int radius)
        {
            if (r.Width < 2 || r.Height < 2) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Round(r, radius)) using (var b = new SolidBrush(fill)) using (var pen = new Pen(border)) { g.FillPath(b, path); g.DrawPath(pen, path); }
        }
        internal static void Glyph(Graphics g, string glyph, Rectangle r, Color color)
        {
            using (var f = new Font("Segoe MDL2 Assets", 14)) TextRenderer.DrawText(g, glyph, f, r, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
    // Native labels add font-dependent glyph padding, which shifts large headings.
    // Keep native sizing/accessibility but use a consistent content origin for painting.
    internal sealed class DesignLabel : Label
    {
        internal DesignLabel()
        {
            // Labels are child windows inside auto-scrolling cards. Explicitly
            // repaint their background before drawing text so a rapid scroll cannot
            // expose the previous card contents or leave a blank text band behind.
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Design.Background(Parent));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            OnPaintBackground(e);
            var area = ClientRectangle;
            area.X += Padding.Left; area.Y += Padding.Top;
            area.Width -= Padding.Horizontal; area.Height -= Padding.Vertical;
            TextRenderer.DrawText(e.Graphics, Text, Font, area, Enabled ? ForeColor : UiStyle.Muted,
                TextFormatFlags.NoPadding | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl |
                (UseMnemonic ? 0 : TextFormatFlags.NoPrefix));
        }
    }
    internal class DesignButton : Button
    {
        internal bool Primary, Sidebar, Active, Danger;
        internal string Glyph;
        bool hover, pressed;
        internal DesignButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Color.Transparent; Cursor = Cursors.Hand;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; MinimumSize = new Size(88, 42); Padding = new Padding(14, 8, 14, 8); Margin = new Padding(0, 0, 8, 0);
            AccessibleRole = AccessibleRole.PushButton;
        }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); if (!String.IsNullOrWhiteSpace(Text)) AccessibleName = Text; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Design.Background(Parent));
            Color dangerFill = hover ? Color.FromArgb(91, 40, 49) : Color.FromArgb(55, 34, 42);
            Color fill = Sidebar ? (Active ? UiStyle.Soft : hover ? Color.FromArgb(44, 45, 65) : UiStyle.Sidebar) : Danger ? dangerFill : Primary ? (hover ? Color.FromArgb(125, 94, 213) : UiStyle.AccentFill) : hover ? UiStyle.Soft : UiStyle.Input;
            if (pressed) fill = Sidebar ? Color.FromArgb(78, 65, 123) : UiStyle.Soft;
            Color ink = !Enabled ? UiStyle.Muted : Sidebar ? (Active ? Color.White : Color.FromArgb(190, 190, 209)) : Danger ? Color.FromArgb(255, 184, 193) : Primary && !pressed ? Color.White : UiStyle.Ink;
            var r = new Rectangle(1, 1, Width - 3, Height - 3);
            Design.Box(e.Graphics, r, fill, Sidebar ? fill : Danger ? Color.FromArgb(126, 58, 70) : Primary ? UiStyle.AccentFill : UiStyle.Border, 10);
            if (Glyph != null) Design.Glyph(e.Graphics, Glyph, new Rectangle(12, 0, 28, Height), ink);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(Glyph == null ? 8 : 43, 0, Width - (Glyph == null ? 16 : 47), Height), ink,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | (Sidebar ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter));
            if (Focused && ShowFocusCues) { r.Inflate(-4, -4); using (var path = Design.Round(r, 7)) using (var p = new Pen((Sidebar && Active) || Primary ? Color.White : UiStyle.Blue)) e.Graphics.DrawPath(p, path); }
        }
    }
    internal sealed class DesignCard : Panel
    {
        internal DesignCard()
        {
            // Cards are children of the auto-scrolling surface. A second buffer
            // here can preserve the pre-scroll child composition and then blit
            // it back over freshly moved labels during rapid wheel input.
            DoubleBuffered = false;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = UiStyle.Surface; Padding = new Padding(22);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Design.Background(Parent)); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Design.Box(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), BackColor, UiStyle.Border, 14);
        }
    }
    internal sealed class DesignToggle : CheckBox
    {
        internal DesignToggle() { AutoSize = false; Size = new Size(174, 36); Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.CheckButton; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); if (!String.IsNullOrWhiteSpace(Text)) AccessibleName = Text; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Design.Background(Parent)); var track = new Rectangle(0, (Height - 22) / 2, 40, 22);
            // Keep the disabled face neutral even when a custom accent makes the theme border purple.
            Design.Box(e.Graphics, track, Checked ? UiStyle.AccentFill : UiStyle.Input, Checked ? UiStyle.AccentFill : UiStyle.Border, 11);
            e.Graphics.FillEllipse(Brushes.White, Checked ? 22 : 3, track.Y + 3, 16, 16);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(50, 0, Width - 50, Height), UiStyle.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
        }
    }
    // Keep the native keyboard/popup/accessibility behavior. Only the closed face and items are drawn here.
    internal sealed class DesignComboBox : ComboBox
    {
        internal DesignComboBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed; DropDownStyle = ComboBoxStyle.DropDownList; FlatStyle = FlatStyle.Flat;
            ItemHeight = 32; IntegralHeight = true; MaxDropDownItems = 9; BackColor = UiStyle.Input; ForeColor = UiStyle.Ink;
            AccessibleRole = AccessibleRole.ComboBox;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); if (!String.IsNullOrWhiteSpace(Text)) AccessibleName = Text; }
        public override Size GetPreferredSize(Size proposedSize) { return new Size(base.GetPreferredSize(proposedSize).Width, Math.Max(40, ItemHeight + 8)); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); ItemHeight = Math.Max(32, Font.Height + 14); }
        protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnDropDownClosed(EventArgs e) { base.OnDropDownClosed(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Design.Background(Parent));
            Design.Box(e.Graphics, new Rectangle(1, 1, Width - 3, Height - 3), UiStyle.Input, Focused ? UiStyle.Blue : UiStyle.Border, 8);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(12, 0, Math.Max(1, Width - 45), Height), Enabled ? ForeColor : UiStyle.Muted, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var p = new Pen(UiStyle.Muted, 1.6f)) e.Graphics.DrawLines(p, new[] { new Point(Width - 25, Height / 2 - 2), new Point(Width - 20, Height / 2 + 3), new Point(Width - 15, Height / 2 - 2) });
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Design.DarkNative(this); }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var brush = new SolidBrush(selected ? UiStyle.Soft : UiStyle.Input)) e.Graphics.FillRectangle(brush, e.Bounds);
            if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height), selected ? UiStyle.Blue : UiStyle.Ink, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
    internal sealed class DesignCheckBox : CheckBox
    {
        internal DesignCheckBox()
        {
            AutoSize = true; Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.CheckButton;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); if (!String.IsNullOrWhiteSpace(Text)) AccessibleName = Text; }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Design.Background(Parent));
            var box = new Rectangle(1, Math.Max(1, (Height - 18) / 2), 18, 18);
            Design.Box(e.Graphics, box, Checked ? UiStyle.AccentFill : UiStyle.Input, Checked ? UiStyle.AccentFill : UiStyle.Border, 5);
            if (Checked) using (var pen = new Pen(Color.White, 2f)) {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                e.Graphics.DrawLines(pen, new[] { new Point(5, box.Y + 9), new Point(9, box.Y + 13), new Point(16, box.Y + 5) });
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(28, 0, Math.Max(1, Width - 28), Height), Enabled ? UiStyle.Ink : UiStyle.Muted,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(26, 1, Math.Max(1, Width - 27), Math.Max(1, Height - 2)));
        }
    }

    // Native text/list controls keep their accessibility and keyboard behavior,
    // while opting into the same dark scrollbar theme as the rest of KiWeave.
    internal sealed class DesignTextBox : TextBox
    {
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Design.DarkNative(this); }
    }
    internal sealed class DesignListBox : ListBox
    {
        internal DesignListBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = 30; BorderStyle = BorderStyle.None;
            BackColor = UiStyle.Surface; ForeColor = UiStyle.Ink; IntegralHeight = false;
            AccessibleRole = AccessibleRole.List;
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Design.DarkNative(this); }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index >= 0) {
                bool selected = (e.State & DrawItemState.Selected) != 0;
                using (var brush = new SolidBrush(selected ? UiStyle.Soft : BackColor)) e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font,
                    new Rectangle(e.Bounds.X + 10, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 20), e.Bounds.Height),
                    selected ? UiStyle.Blue : ForeColor, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                if ((e.State & DrawItemState.Focus) != 0) ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, selected ? UiStyle.Blue : ForeColor, selected ? UiStyle.Soft : BackColor);
            }
            // Forms with richer list rows can subscribe to DrawItem and paint over
            // this consistent fallback (for example, the sequence step cards).
            base.OnDrawItem(e);
        }
    }
    internal sealed class InputFrame : Panel
    {
        readonly TextBox input;
        internal InputFrame(TextBox box)
        {
            input = box; DoubleBuffered = true; Height = 44; MinimumSize = new Size(0, 44); Dock = DockStyle.Top;
            BackColor = UiStyle.Input; Padding = new Padding(12, 11, 12, 10); Margin = Padding.Empty;
            input.BorderStyle = BorderStyle.None; input.BackColor = BackColor; input.ForeColor = UiStyle.Ink; input.Dock = DockStyle.None;
            Controls.Add(input); input.Enter += delegate { Invalidate(); }; input.Leave += delegate { Invalidate(); };
            input.FontChanged += delegate { Height = Math.Max(44, input.PreferredHeight + Padding.Vertical + 4); };
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (input != null) input.SetBounds(Padding.Left, Math.Max(0, (ClientSize.Height - input.PreferredHeight) / 2),
                Math.Max(1, ClientSize.Width - Padding.Horizontal), input.PreferredHeight);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Design.Background(Parent)); }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); Design.Box(e.Graphics, new Rectangle(1, 1, Width - 3, Height - 3), BackColor, input.Focused ? UiStyle.Blue : UiStyle.Border, 8); }
    }

    internal sealed class DesignScrollPanel : Panel
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);
        [DllImport("user32.dll")]
        static extern bool ShowScrollBar(IntPtr hwnd, int bar, bool show);
        Control content;
        int offset;
        bool layingOut;
        int targetOffset;
        System.Windows.Forms.Timer smoothTimer;

        internal static bool SmoothScrollingEnabled { get; set; }

        internal int ScrollOffset { get { return offset; } }
        int MaxScrollOffset { get { return Math.Max(0, (content == null ? 0 : content.Height) + Padding.Vertical - ClientSize.Height); } }

        internal DesignScrollPanel()
        {
            AutoScroll = false;
            BackColor = UiStyle.Surface;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            ControlAdded += delegate(object sender, ControlEventArgs e) { AttachWheel(e.Control); LayoutContent(); };
            ControlRemoved += delegate { LayoutContent(); };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && smoothTimer != null) { smoothTimer.Stop(); smoothTimer.Dispose(); smoothTimer = null; }
            base.Dispose(disposing);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (content == null) content = e.Control;
            AttachWheel(e.Control);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            if (!layingOut) LayoutContent();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { SetWindowTheme(Handle, "DarkMode_Explorer", null); } catch { }
            try { ShowScrollBar(Handle, 3, false); } catch { }
            LayoutContent();
        }

        void LayoutContent()
        {
            if (layingOut) return;
            layingOut = true;
            try
            {
                if (content == null || content.IsDisposed) return;
                content.Dock = DockStyle.None;
                int width = Math.Max(1, ClientSize.Width - Padding.Horizontal);
                int height = Math.Max(ClientSize.Height, content.GetPreferredSize(new Size(width, 0)).Height);
                offset = Math.Max(0, Math.Min(offset, Math.Max(0, height + Padding.Vertical - ClientSize.Height)));
                content.SetBounds(Padding.Left, Padding.Top - offset, width, height);
            }
            finally { layingOut = false; }
        }

        void AttachWheel(Control control)
        {
            if (control == null) return;
            control.MouseWheel -= ChildMouseWheel;
            control.MouseWheel += ChildMouseWheel;
            foreach (Control child in control.Controls) AttachWheel(child);
            control.ControlAdded -= ChildControlAdded;
            control.ControlAdded += ChildControlAdded;
        }

        void ChildControlAdded(object sender, ControlEventArgs e)
        {
            AttachWheel(e.Control);
            LayoutContent();
        }

        void ChildMouseWheel(object sender, MouseEventArgs e)
        {
            if (SmoothScrollingEnabled) AnimateTo(offset - Math.Sign(e.Delta) * Math.Max(48, ClientSize.Height / 3));
            else ScrollBy(-(e.Delta / 3));
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.PageUp || key == Keys.PageDown || key == Keys.Home || key == Keys.End || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.PageUp) SetScrollOffset(ScrollOffset - ClientSize.Height);
            else if (e.KeyCode == Keys.PageDown) SetScrollOffset(ScrollOffset + ClientSize.Height);
            else if (e.KeyCode == Keys.Home) SetScrollOffset(0);
            else if (e.KeyCode == Keys.End) ScrollToBottom();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ChildMouseWheel(this, e);
        }

        internal void ScrollBy(int amount)
        {
            SetScrollOffset(ScrollOffset + amount);
        }

        internal void ScrollToBottom()
        {
            LayoutContent();
            SetScrollOffset(Int32.MaxValue);
        }

        internal void SetScrollOffset(int value)
        {
            offset = Math.Max(0, Math.Min(MaxScrollOffset, value));
            targetOffset = offset;
            LayoutContent();
            Invalidate(true);
            if (content != null) { content.Invalidate(true); content.Update(); }
            Update();
        }

        void AnimateTo(int value)
        {
            targetOffset = Math.Max(0, Math.Min(MaxScrollOffset, value));
            if (smoothTimer == null) {
                smoothTimer = new System.Windows.Forms.Timer { Interval = 15 };
                smoothTimer.Tick += delegate {
                    int current = ScrollOffset, distance = targetOffset - current;
                    if (Math.Abs(distance) <= 2) { SetScrollOffset(targetOffset); smoothTimer.Stop(); return; }
                    SetScrollOffset(current + distance / 3);
                };
            }
            smoothTimer.Start();
        }

        internal void EnableKeyboardFocus()
        {
            SetStyle(ControlStyles.Selectable, true); TabStop = true;
        }
    }

    internal sealed class DesignNumericUpDown : NumericUpDown
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

        sealed class DarkSpinnerButtons : Control
        {
            readonly DesignNumericUpDown owner;
            int hover = -1;

            internal DarkSpinnerButtons(DesignNumericUpDown owner)
            {
                this.owner = owner;
                Cursor = Cursors.Hand;
                TabStop = false;
                AccessibleName = "Increase or decrease value";
                AccessibleRole = AccessibleRole.SpinButton;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int next = e.Y < Height / 2 ? 0 : 1;
                if (next != hover) { hover = next; Invalidate(); }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e); hover = -1; Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left) return;
                decimal next = owner.Value + (e.Y < Height / 2 ? owner.Increment : -owner.Increment);
                owner.Value = Math.Max(owner.Minimum, Math.Min(owner.Maximum, next));
                owner.Focus();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(UiStyle.Input);
                int half = Height / 2;
                if (hover >= 0)
                    using (var brush = new SolidBrush(UiStyle.Soft))
                        e.Graphics.FillRectangle(brush, 0, hover == 0 ? 0 : half, Width, hover == 0 ? half : Height - half);
                using (var pen = new Pen(UiStyle.Border))
                {
                    e.Graphics.DrawLine(pen, 0, 0, 0, Height - 1);
                    e.Graphics.DrawLine(pen, 0, half, Width - 1, half);
                }
                DrawArrow(e.Graphics, new Rectangle(0, 0, Width, half), true);
                DrawArrow(e.Graphics, new Rectangle(0, half, Width, Height - half), false);
            }

            static void DrawArrow(Graphics graphics, Rectangle bounds, bool up)
            {
                int centerX = bounds.Left + bounds.Width / 2;
                int centerY = bounds.Top + bounds.Height / 2;
                Point[] points = up
                    ? new[] { new Point(centerX, centerY - 2), new Point(centerX - 4, centerY + 2), new Point(centerX + 4, centerY + 2) }
                    : new[] { new Point(centerX - 4, centerY - 2), new Point(centerX + 4, centerY - 2), new Point(centerX, centerY + 2) };
                using (var brush = new SolidBrush(UiStyle.Ink)) graphics.FillPolygon(brush, points);
            }
        }

        readonly DarkSpinnerButtons spinner;

        internal DesignNumericUpDown()
        {
            BackColor = UiStyle.Input;
            ForeColor = UiStyle.Ink;
            BorderStyle = BorderStyle.FixedSingle;
            TextAlign = HorizontalAlignment.Center;
            Width = 58;
            spinner = new DarkSpinnerButtons(this);
            Controls.Add(spinner);
            LayoutSpinner();
        }

        void LayoutSpinner()
        {
            if (spinner != null) spinner.SetBounds(Math.Max(1, ClientSize.Width - 18), 1, 17, Math.Max(1, ClientSize.Height - 2));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e); LayoutSpinner();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { SetWindowTheme(Handle, "DarkMode_Explorer", null); } catch { }
            foreach (Control child in Controls)
            {
                child.BackColor = UiStyle.Input;
                child.ForeColor = UiStyle.Ink;
                if (child != spinner && child.GetType().Name == "UpDownButtons") child.Visible = false;
            }
            spinner.BringToFront();
            LayoutSpinner();
        }
    }

    internal sealed class DesignMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return UiStyle.Surface; } }
        public override Color ImageMarginGradientBegin { get { return UiStyle.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return UiStyle.Surface; } }
        public override Color ImageMarginGradientEnd { get { return UiStyle.Surface; } }
        public override Color MenuItemSelected { get { return UiStyle.Soft; } }
        public override Color MenuItemSelectedGradientBegin { get { return UiStyle.Soft; } }
        public override Color MenuItemSelectedGradientEnd { get { return UiStyle.Soft; } }
        public override Color MenuItemPressedGradientBegin { get { return UiStyle.Soft; } }
        public override Color MenuItemPressedGradientMiddle { get { return UiStyle.Soft; } }
        public override Color MenuItemPressedGradientEnd { get { return UiStyle.Soft; } }
        public override Color MenuItemBorder { get { return UiStyle.Border; } }
        public override Color MenuBorder { get { return UiStyle.Border; } }
        public override Color CheckBackground { get { return UiStyle.Input; } }
        public override Color CheckSelectedBackground { get { return UiStyle.Soft; } }
        public override Color CheckPressedBackground { get { return UiStyle.Soft; } }
        public override Color SeparatorDark { get { return UiStyle.Border; } }
        public override Color SeparatorLight { get { return UiStyle.Border; } }
    }
}
