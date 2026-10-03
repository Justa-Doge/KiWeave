using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class DesignListView : ListView
    {
        internal DesignListView() { DoubleBuffered = true; }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Design.DarkNative(this); }
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
        internal static void DarkTitlebar(Form form) { form.HandleCreated += delegate { try { int on = 1; DwmSetWindowAttribute(form.Handle, 20, ref on, 4); } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { } }; }
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
        protected override void OnPaint(PaintEventArgs e)
        {
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
        internal bool Primary, Sidebar, Active;
        internal string Glyph;
        bool hover, pressed;
        internal DesignButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Color.Transparent; Cursor = Cursors.Hand;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; MinimumSize = new Size(88, 42); Padding = new Padding(14, 8, 14, 8); Margin = new Padding(0, 0, 8, 0);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Design.Background(Parent));
            Color fill = Sidebar ? (Active ? UiStyle.Soft : hover ? Color.FromArgb(44, 45, 65) : UiStyle.Sidebar) : Primary ? (hover ? Color.FromArgb(125, 94, 213) : UiStyle.AccentFill) : hover ? UiStyle.Soft : UiStyle.Input;
            if (pressed) fill = Sidebar ? Color.FromArgb(78, 65, 123) : UiStyle.Soft;
            Color ink = !Enabled ? UiStyle.Muted : Sidebar ? (Active ? Color.White : Color.FromArgb(190, 190, 209)) : Primary && !pressed ? Color.White : UiStyle.Ink;
            var r = new Rectangle(1, 1, Width - 3, Height - 3);
            Design.Box(e.Graphics, r, fill, Sidebar ? fill : Primary ? UiStyle.AccentFill : UiStyle.Border, 10);
            if (Glyph != null) Design.Glyph(e.Graphics, Glyph, new Rectangle(12, 0, 28, Height), ink);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(Glyph == null ? 8 : 43, 0, Width - (Glyph == null ? 16 : 47), Height), ink,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | (Sidebar ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter));
            if (Focused && ShowFocusCues) { r.Inflate(-4, -4); using (var path = Design.Round(r, 7)) using (var p = new Pen((Sidebar && Active) || Primary ? Color.White : UiStyle.Blue)) e.Graphics.DrawPath(p, path); }
        }
    }
    internal sealed class DesignCard : Panel
    {
        internal DesignCard() { DoubleBuffered = true; BackColor = UiStyle.Surface; Padding = new Padding(22); }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Design.Background(Parent)); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Design.Box(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), BackColor, UiStyle.Border, 14);
        }
    }
    internal sealed class DesignToggle : CheckBox
    {
        internal DesignToggle() { AutoSize = false; Size = new Size(174, 36); Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Design.Background(Parent)); var track = new Rectangle(0, (Height - 22) / 2, 40, 22);
            Design.Box(e.Graphics, track, Checked ? UiStyle.AccentFill : UiStyle.Border, Checked ? UiStyle.AccentFill : UiStyle.Border, 11);
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
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
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

    // Native text/list controls keep their accessibility and keyboard behavior,
    // while opting into the same dark scrollbar theme as the rest of KiWeave.
    internal sealed class DesignTextBox : TextBox
    {
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Design.DarkNative(this); }
    }
    internal sealed class DesignListBox : ListBox
    {
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Design.DarkNative(this); }
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

        internal DesignScrollPanel()
        {
            AutoScroll = true;
            BackColor = UiStyle.Surface;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { SetWindowTheme(Handle, "DarkMode_Explorer", null); } catch { }
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
