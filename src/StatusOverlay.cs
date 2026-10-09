using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal static class StatusOverlaySettings
    {
        internal static int Width = 230, Height = 72, LeftOffset = 18, BottomOffset = 28;
        internal static int ContentX = 40, ContentY = 16, HoldMilliseconds = 1500;
    }

    internal static class StatusOverlay
    {
        static readonly object Gate = new object();
        static StatusOverlayForm current;
        static Form owner;
        internal static string Style = "KiWeave";
        internal static bool UsesWindowsStyle { get { return String.Equals(Style, "Compact flyout", StringComparison.OrdinalIgnoreCase) || String.Equals(Style, "Windows 11", StringComparison.OrdinalIgnoreCase); } }

        internal static void SetOwner(Form form)
        {
            lock (Gate) owner = form;
        }

        internal static void ClearOwner(Form form)
        {
            lock (Gate) if (ReferenceEquals(owner, form)) owner = null;
        }

        internal static void ShowDebugMenu(IWin32Window owner)
        {
            using (var form = new StatusOverlayDebugForm()) form.ShowDialog(owner);
        }

        internal static void Show(string title, string message) { Show(title, message, "", false); }
        internal static void Show(string title, string message, string icon, bool active) { Show(title, message, icon, active, false); }
        internal static void ShowStyled(string title, string message, string icon, bool active) { Show(title, message, icon, active, UsesWindowsStyle); }
        static void Show(string title, string message, string icon, bool active, bool windowsStyle)
        {
            try {
                Form host;
                lock (Gate) host = owner;
                if (host == null || host.IsDisposed || !host.IsHandleCreated) {
                    host = Application.OpenForms.Cast<Form>().FirstOrDefault(f => !f.IsDisposed && f.IsHandleCreated);
                }
                if (host == null || host.IsDisposed || !host.IsHandleCreated) return;
                Action show = delegate {
                    if (host.IsDisposed) return;
                    lock (Gate) {
                        if (current != null && !current.IsDisposed) { current.Replace(title, message, icon, active, windowsStyle); return; }
                        current = new StatusOverlayForm(title, message, icon, active, windowsStyle);
                        StatusOverlayForm shown = current;
                        shown.FormClosed += delegate { lock (Gate) { if (ReferenceEquals(current, shown)) current = null; } };
                        // Do not make the overlay owned by the hidden tray window. An owned
                        // window can be suppressed behind another app when KiWeave is hidden.
                        // The overlay is already tool-window + TopMost, so it can stand alone.
                        shown.Show();
                    }
                };
                host.BeginInvoke(show);
            } catch { }
        }
    }

    internal sealed class StatusOverlayDebugForm : Form
    {
        readonly NumericUpDown width = Number(180, 420, StatusOverlaySettings.Width);
        readonly NumericUpDown height = Number(56, 140, StatusOverlaySettings.Height);
        readonly NumericUpDown left = Number(0, 300, StatusOverlaySettings.LeftOffset);
        readonly NumericUpDown bottom = Number(0, 300, StatusOverlaySettings.BottomOffset);
        readonly NumericUpDown contentX = Number(0, 220, StatusOverlaySettings.ContentX);
        readonly NumericUpDown contentY = Number(0, 80, StatusOverlaySettings.ContentY);
        readonly NumericUpDown hold = Number(250, 5000, StatusOverlaySettings.HoldMilliseconds);
        readonly ComboBox kind = new ComboBox();
        readonly CheckBox active = new CheckBox();

        internal StatusOverlayDebugForm()
        {
            Text = "KiWeave private UI debug editor"; Icon = Program.AppIcon(); StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(470, 430); MinimumSize = new Size(470, 430); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(20), BackColor = UiStyle.Canvas };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(root);
            var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = UiStyle.Canvas };
            root.Controls.Add(stack, 0, 0);
            stack.Controls.Add(Label("Private live editor", 16, true));
            stack.Controls.Add(Label("Adjust the toast geometry and preview it without rebuilding KiWeave.", 9, false));
            stack.Controls.Add(Row("Toast width", width)); stack.Controls.Add(Row("Toast height", height));
            stack.Controls.Add(Row("Left offset", left)); stack.Controls.Add(Row("Bottom offset", bottom));
            stack.Controls.Add(Row("Content X", contentX)); stack.Controls.Add(Row("Content Y", contentY));
            stack.Controls.Add(Row("Hold milliseconds", hold));
            kind.DropDownStyle = ComboBoxStyle.DropDownList; kind.Width = 180; kind.Items.AddRange(new object[] { "Mute", "Deafen", "KiWeave" }); kind.SelectedIndex = 0;
            stack.Controls.Add(Row("Preview type", kind));
            active.Text = "Show active state (slash)"; active.AutoSize = true; active.Checked = true; active.ForeColor = UiStyle.Ink; active.BackColor = UiStyle.Canvas; stack.Controls.Add(active);
            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 16, 0, 0), BackColor = UiStyle.Canvas };
            buttons.Controls.Add(UiStyle.Button("Apply + preview", delegate { Apply(); Preview(); }, true));
            buttons.Controls.Add(UiStyle.Button("Reset", delegate { ResetValues(); }, false));
            buttons.Controls.Add(UiStyle.Button("Close", delegate { Close(); }, false)); stack.Controls.Add(buttons);
        }
        static NumericUpDown Number(int min, int max, int value) { return new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, value)), Width = 180, Height = 30, BackColor = UiStyle.Input, ForeColor = UiStyle.Ink, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(8, 0, 0, 8) }; }
        static Label Label(string text, float size, bool bold) { return new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = bold ? UiStyle.Ink : UiStyle.Muted, BackColor = UiStyle.Canvas, Margin = new Padding(0, 0, 0, 8) }; }
        static Control Row(string text, Control input) { var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = UiStyle.Canvas, Margin = new Padding(0, 2, 0, 0) }; row.Controls.Add(Label(text, 9, false)); row.Controls.Add(input); return row; }
        void Apply()
        {
            StatusOverlaySettings.Width = (int)width.Value; StatusOverlaySettings.Height = (int)height.Value; StatusOverlaySettings.LeftOffset = (int)left.Value; StatusOverlaySettings.BottomOffset = (int)bottom.Value; StatusOverlaySettings.ContentX = (int)contentX.Value; StatusOverlaySettings.ContentY = (int)contentY.Value; StatusOverlaySettings.HoldMilliseconds = (int)hold.Value;
        }
        void Preview()
        {
            string selected = kind.SelectedItem as string ?? "Mute"; string icon = selected == "Deafen" ? "deaf" : selected == "Mute" ? "mute" : "";
            StatusOverlay.Show("Discord", selected == "Deafen" ? (active.Checked ? "Deafened" : "Undeafened") : selected == "Mute" ? (active.Checked ? "Muted" : "Unmuted") : "Debug preview", icon, active.Checked);
        }
        void ResetValues()
        {
            width.Value = 230; height.Value = 72; left.Value = 18; bottom.Value = 28; contentX.Value = 40; contentY.Value = 16; hold.Value = 1500;
        }
    }

    internal sealed class StatusOverlayForm : Form
    {
        string title;
        string message;
        string icon;
        bool active;
        bool windowsStyle;
        Image customIcon;
        readonly Timer animation = new Timer();
        DateTime animationStart;
        Point targetLocation;

        internal StatusOverlayForm(string title, string message, string icon, bool active, bool windowsStyle = false)
        {
            this.title = title ?? "KiWeave"; this.message = message ?? ""; this.icon = icon ?? ""; this.active = active; this.windowsStyle = windowsStyle;
            customIcon = windowsStyle ? null : OverlayAssetLoader.Load(icon, active);
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual; Size = windowsStyle ? new Size(160, 48) : new Size(StatusOverlaySettings.Width, StatusOverlaySettings.Height); BackColor = windowsStyle ? Color.FromArgb(32, 32, 32) : UiStyle.Surface;
            DoubleBuffered = true; animation.Interval = 16;
            animation.Tick += delegate {
                double elapsed = (DateTime.UtcNow - animationStart).TotalMilliseconds;
                double offset = 0;
                if (elapsed < 190) { double progress = EaseOut(elapsed / 190.0); Opacity = progress; offset = windowsStyle ? (1.0 - progress) * 8.0 : 0; }
                else if (elapsed < 190 + StatusOverlaySettings.HoldMilliseconds) Opacity = 1.0;
                else if (elapsed < 190 + StatusOverlaySettings.HoldMilliseconds + 240) { double progress = EaseIn((elapsed - 190 - StatusOverlaySettings.HoldMilliseconds) / 240.0); Opacity = 1.0 - progress; offset = windowsStyle ? progress * 8.0 : 0; }
                else { animation.Stop(); Close(); return; }
                if (windowsStyle) Location = new Point(targetLocation.X, targetLocation.Y + (int)Math.Round(offset));
                Invalidate();
            };
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x00000080 | 0x08000000; return p; } }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e); ApplyRoundedRegion(); RestartAnimation();
        }
        internal void Replace(string nextTitle, string nextMessage, string nextIcon, bool nextActive, bool nextWindowsStyle)
        {
            title = nextTitle ?? "KiWeave"; message = nextMessage ?? ""; icon = nextIcon ?? ""; active = nextActive; windowsStyle = nextWindowsStyle;
            if (customIcon != null) { customIcon.Dispose(); customIcon = null; }
            if (!windowsStyle) customIcon = OverlayAssetLoader.Load(icon, active);
            Size = windowsStyle ? new Size(160, 48) : new Size(StatusOverlaySettings.Width, StatusOverlaySettings.Height);
            BackColor = windowsStyle ? Color.FromArgb(32, 32, 32) : UiStyle.Surface;
            ApplyRoundedRegion(); RestartAnimation(); Invalidate();
        }
        void RestartAnimation()
        {
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            targetLocation = windowsStyle ? new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - 10) : new Point(area.Left + StatusOverlaySettings.LeftOffset, area.Bottom - Height - StatusOverlaySettings.BottomOffset);
            Location = windowsStyle ? new Point(targetLocation.X, targetLocation.Y + 8) : targetLocation; Opacity = 0; animationStart = DateTime.UtcNow; animation.Start();
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e); if (IsHandleCreated) ApplyRoundedRegion();
        }
        void ApplyRoundedRegion()
        {
            using (var path = Rounded(new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 11)) {
                Region old = Region; Region = new Region(path); if (old != null) old.Dispose();
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality; e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            if (windowsStyle) { PaintWindowsStyle(e.Graphics); return; }
            using (var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 12)) using (var fill = new SolidBrush(UiStyle.Surface)) using (var border = new Pen(UiStyle.Border, 1)) {
                e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(border, path);
            }
            e.Graphics.TranslateTransform(StatusOverlaySettings.ContentX - 72, StatusOverlaySettings.ContentY - 16);
            if (customIcon != null) e.Graphics.DrawImage(customIcon, new Rectangle(72, 16, 38, 38));
            else {
                using (var shadowPath = Rounded(new Rectangle(70, 18, 38, 38), 10)) using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) e.Graphics.FillPath(shadow, shadowPath);
                using (var accentPath = Rounded(new Rectangle(72, 16, 38, 38), 10)) using (var accent = new SolidBrush(UiStyle.AccentFill)) e.Graphics.FillPath(accent, accentPath);
                if (icon == "mute") DrawMic(e.Graphics, active); else if (icon == "deaf") DrawHeadphones(e.Graphics, active); else using (var logo = new Font("Segoe UI", 19, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, "F", logo, new Rectangle(72, 16, 38, 38), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            using (var heading = new Font("Segoe UI", 9, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, title, heading, new Rectangle(124, 14, Width - 138, 22), UiStyle.Ink, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            using (var detail = new Font("Segoe UI", 9)) TextRenderer.DrawText(e.Graphics, message, detail, new Rectangle(124, 37, Width - 138, 22), UiStyle.Muted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            e.Graphics.ResetTransform();
        }
        void PaintWindowsStyle(Graphics graphics)
        {
            using (var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 11))
            using (var fill = new SolidBrush(Color.FromArgb(32, 32, 32)))
            using (var border = new Pen(Color.FromArgb(48, 48, 48), 1)) {
                graphics.FillPath(fill, path); graphics.DrawPath(border, path);
            }
            GraphicsState state = graphics.Save(); graphics.TranslateTransform(-64, -6);
            if (icon == "mute") DrawMic(graphics, active); else if (icon == "deaf") DrawHeadphones(graphics, active);
            graphics.Restore(state);
            string label = message == "Mute shortcut sent" ? "Muted" : message == "Deafen shortcut sent" ? "Deafened" : message;
            using (var font = new Font("Segoe UI", 8.5f)) TextRenderer.DrawText(graphics, label, font, new Rectangle(42, 10, 112, 25), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
        }
        static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var p = new GraphicsPath(); int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        static void DrawMic(Graphics g, bool muted)
        {
            using (var pen = new Pen(Color.White, 2.2f)) {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawArc(pen, new Rectangle(82, 22, 14, 19), 0, 180); g.DrawLine(pen, 82, 31, 82, 33); g.DrawLine(pen, 96, 31, 96, 33); g.DrawLine(pen, 89, 39, 89, 44); g.DrawLine(pen, 84, 44, 94, 44);
            }
            using (var fill = new SolidBrush(Color.White)) FillRounded(g, fill, new Rectangle(85, 20, 8, 16), 4);
            if (muted) using (var slash = new Pen(Color.FromArgb(255, 190, 198), 2.4f)) { slash.StartCap = LineCap.Round; slash.EndCap = LineCap.Round; g.DrawLine(slash, 78, 20, 100, 44); }
        }
        static void DrawHeadphones(Graphics g, bool deafened)
        {
            using (var pen = new Pen(Color.White, 2.2f)) {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawArc(pen, new Rectangle(79, 20, 20, 23), 180, 180); g.DrawLine(pen, 79, 32, 79, 40); g.DrawLine(pen, 99, 32, 99, 40);
            }
            using (var fill = new SolidBrush(Color.White)) { FillRounded(g, fill, new Rectangle(77, 33, 6, 11), 3); FillRounded(g, fill, new Rectangle(95, 33, 6, 11), 3); }
            if (deafened) using (var slash = new Pen(Color.FromArgb(255, 190, 198), 2.4f)) { slash.StartCap = LineCap.Round; slash.EndCap = LineCap.Round; g.DrawLine(slash, 78, 20, 100, 44); }
        }
        static void FillRounded(Graphics g, Brush brush, Rectangle rectangle, int radius) { using (var path = Rounded(rectangle, radius)) g.FillPath(brush, path); }
        static double EaseIn(double value) { value = Math.Max(0, Math.Min(1, value)); return value * value * value; }
        static double EaseOut(double value) { return 1.0 - EaseIn(1.0 - value); }
        protected override void Dispose(bool disposing) { if (disposing) { animation.Dispose(); if (customIcon != null) customIcon.Dispose(); } base.Dispose(disposing); }
    }

    internal static class OverlayAssetLoader
    {
        internal static Image Load(string icon, bool active)
        {
            try {
                if (icon != "mute" && icon != "deaf") return null;
                string file = icon == "mute" ? (active ? "icon background mute.png" : "icon-background.png") : (active ? "icon background deafen.png" : "icon-background undeafen.png");
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "overlay-icons", file);
                if (!System.IO.File.Exists(path)) return null;
                using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite)) using (var source = Image.FromStream(stream)) return new Bitmap(source);
            } catch { return null; }
        }
    }
}
