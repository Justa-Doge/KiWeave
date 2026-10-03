using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class CreateUninstallerIcon
{
    static GraphicsPath Round(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath(); float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    static Bitmap Render(int size)
    {
        int high = Math.Max(512, size * 4);
        using (var source = new Bitmap(high, high, PixelFormat.Format32bppArgb)) {
            using (var g = Graphics.FromImage(source)) {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.ScaleTransform(high / 256f, high / 256f); g.Clear(Color.Transparent);
                using (var shadow = Round(23, 29, 210, 220, 51)) using (var brush = new SolidBrush(Color.FromArgb(45, 30, 4, 4))) g.FillPath(brush, shadow);
                using (var face = Round(18, 13, 220, 222, 52)) using (var brush = new LinearGradientBrush(new RectangleF(18, 13, 220, 222), Color.FromArgb(245, 106, 112), Color.FromArgb(150, 24, 35), LinearGradientMode.Vertical)) using (var rim = new Pen(Color.FromArgb(255, 255, 220, 220), 2.2f)) { g.FillPath(brush, face); g.DrawPath(rim, face); }
                using (var mark = new Pen(Color.White, 11f)) using (var shadowMark = new Pen(Color.FromArgb(255, 112, 20, 30), 17f)) {
                    g.DrawLine(shadowMark, 77, 78, 179, 178); g.DrawLine(shadowMark, 179, 78, 77, 178);
                    g.DrawLine(mark, 77, 78, 179, 178); g.DrawLine(mark, 179, 78, 77, 178);
                }
            }
            var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result)) { g.CompositingMode = CompositingMode.SourceCopy; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(source, new Rectangle(0, 0, size, size)); }
            return result;
        }
    }
    static void Main(string[] args)
    {
        string root = args[0]; int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 }; var frames = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++) using (var bmp = Render(sizes[i])) using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); frames[i] = ms.ToArray(); }
        using (var output = new BinaryWriter(File.Create(Path.Combine(root, "uninstall.ico")))) {
            output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)sizes.Length); uint offset = (uint)(6 + 16 * sizes.Length);
            for (int i = 0; i < sizes.Length; i++) { output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); output.Write((byte)0); output.Write((byte)0); output.Write((ushort)1); output.Write((ushort)32); output.Write((uint)frames[i].Length); output.Write(offset); offset += (uint)frames[i].Length; }
            foreach (var frame in frames) output.Write(frame);
        }
    }
}
