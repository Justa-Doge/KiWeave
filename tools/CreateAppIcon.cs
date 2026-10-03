using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// A keycap with two opposed routing arrows. Render each ICO size independently
// with supersampling, rather than enlarging a tiny bitmap.
internal static class CreateAppIcon
{
    static GraphicsPath Round(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath(); float d = r * 2;
        p.AddArc(x,y,d,d,180,90); p.AddArc(x+w-d,y,d,d,270,90);
        p.AddArc(x+w-d,y+h-d,d,d,0,90); p.AddArc(x,y+h-d,d,d,90,90);
        p.CloseFigure(); return p;
    }
    static void Draw(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using (var shadow = Round(23,29,210,220,51))
        using (var brush = new SolidBrush(Color.FromArgb(38,13,9,34))) g.FillPath(brush,shadow);
        using (var baseShape = Round(18,17,220,222,52))
        using (var brush = new LinearGradientBrush(new Rectangle(18,17,220,222),Color.FromArgb(92,54,169),Color.FromArgb(46,27,94),LinearGradientMode.Vertical)) g.FillPath(brush,baseShape);
        using (var face = Round(18,13,220,210,52))
        using (var brush = new LinearGradientBrush(new Point(36,20),new Point(220,224),Color.FromArgb(167,129,245),Color.FromArgb(107,67,196)))
        using (var rim = new Pen(Color.FromArgb(90,233,214,255),2.2f)) { g.FillPath(brush,face); g.DrawPath(rim,face); }
        // Recessed face makes the mark read as a keyboard key, not a generic tile.
        using (var inset = Round(35,31,186,170,36))
        using (var fill = new LinearGradientBrush(new Point(40,31),new Point(210,205),Color.FromArgb(105,68,190),Color.FromArgb(77,42,149)))
        using (var stroke = new Pen(Color.FromArgb(80,211,181,255),1.8f)) { g.FillPath(fill,inset); g.DrawPath(stroke,inset); }
        using (var mark = new SolidBrush(Color.FromArgb(247,243,255)))
        {
            // Two balanced, generous arrows form the remapping glyph.
            using (var p = new GraphicsPath()) {
                p.AddLines(new[]{new PointF(66,72),new PointF(142,72),new PointF(142,53),new PointF(190,92),new PointF(142,131),new PointF(142,112),new PointF(66,112)});
                p.CloseFigure(); g.FillPath(mark,p);
            }
            using (var p = new GraphicsPath()) {
                p.AddLines(new[]{new PointF(190,133),new PointF(114,133),new PointF(114,114),new PointF(66,153),new PointF(114,192),new PointF(114,173),new PointF(190,173)});
                p.CloseFigure(); g.FillPath(mark,p);
            }
        }
    }
    static Bitmap Render(int size)
    {
        int high = Math.Max(512,size*4);
        using (var source = new Bitmap(high,high,PixelFormat.Format32bppArgb)) {
            using (var g = Graphics.FromImage(source)) { g.Clear(Color.Transparent); g.ScaleTransform(high/256f,high/256f); Draw(g); }
            var result = new Bitmap(size,size,PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result)) { g.CompositingMode=CompositingMode.SourceCopy; g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.PixelOffsetMode=PixelOffsetMode.HighQuality; g.DrawImage(source,new Rectangle(0,0,size,size)); }
            return result;
        }
    }
    static void Main(string[] args)
    {
        string root=args[0]; Directory.CreateDirectory(Path.Combine(root,"assets"));
        int[] sizes={16,20,24,32,40,48,64,96,128,256}; var frames=new byte[sizes.Length][];
        for(int i=0;i<sizes.Length;i++) using(var bmp=Render(sizes[i])) using(var ms=new MemoryStream()) { bmp.Save(ms,ImageFormat.Png); frames[i]=ms.ToArray(); }
        using(var output=new BinaryWriter(File.Create(Path.Combine(root,"app.ico")))) {
            output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)sizes.Length);
            uint offset=(uint)(6+16*sizes.Length);
            for(int i=0;i<sizes.Length;i++) { output.Write((byte)(sizes[i]==256?0:sizes[i])); output.Write((byte)(sizes[i]==256?0:sizes[i])); output.Write((byte)0); output.Write((byte)0); output.Write((ushort)1); output.Write((ushort)32); output.Write((uint)frames[i].Length); output.Write(offset); offset+=(uint)frames[i].Length; }
            foreach(var frame in frames) output.Write(frame);
        }
        using(var bmp=Render(512)) bmp.Save(Path.Combine(root,"assets","app-icon.png"),ImageFormat.Png);
        using(var preview=new Bitmap(760,360)) using(var g=Graphics.FromImage(preview)) {
            g.Clear(Color.FromArgb(24,24,32)); using(var bmp=Render(288)) g.DrawImageUnscaled(bmp,24,32);
            using(var font=new Font("Segoe UI",16,FontStyle.Regular)) using(var b=new SolidBrush(Color.FromArgb(238,232,250))) g.DrawString("KiWeave",font,b,343,54);
            int x=349; foreach(int size in new[]{16,24,32,48,64}) { using(var bmp=Render(size)) g.DrawImageUnscaled(bmp,x,146+(64-size)/2); x+=size+25; }
            using(var font=new Font("Segoe UI",10)) using(var b=new SolidBrush(Color.FromArgb(155,146,179))) g.DrawString("16 / 24 / 32 / 48 / 64 px",font,b,349,240);
            preview.Save(Path.Combine(root,"assets","app-icon-preview.png"),ImageFormat.Png);
        }
    }
}
