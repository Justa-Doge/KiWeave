using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal static class UpdateControlTests
    {
        static MainForm form; static string app; static int failures;
        static int Command()
        {
            using(var p=Process.Start(new ProcessStartInfo(app,"--exit-for-update") { UseShellExecute=false,CreateNoWindow=true })) {
                if(!p.WaitForExit(7000)) throw new Exception("Update command timed out"); return p.ExitCode;
            }
        }
        static void Check(string name,int result,int expected)
        {
            if(result!=expected) { failures++; Console.WriteLine("FAIL "+name+": "+result); } else Console.WriteLine("PASS "+name);
        }
        static void Run(int expected,string name,Action next)
        {
            Task.Run(delegate { int result=Command(); form.BeginInvoke((Action)delegate { Check(name,result,expected);next(); }); });
        }
        [STAThread] static int Main(string[] args)
        {
            app=args[0]; Application.EnableVisualStyles();
            using(var icon=Program.AppIcon()) using(var tray=Program.TrayIcon()) {
                if(icon==null||tray==null) throw new Exception("Missing app or tray icon");
                using(var expected=new Bitmap(32,32)) using(var g=Graphics.FromImage(expected)) using(var font=new Font("Segoe UI",17,FontStyle.Bold)) {
                    g.Clear(UiStyle.AccentFill);g.DrawString("F",font,Brushes.White,5,1);
                    int shadowPixels=0,whitePixels=0;
                    using(var actual=tray.ToBitmap()) {
                        if(actual.GetPixel(0,0)!=UiStyle.AccentFill) throw new Exception("Tray base color mismatch");
                        for(int y=0;y<32;y++) for(int x=0;x<32;x++) {
                            var before=expected.GetPixel(x,y); var after=actual.GetPixel(x,y);
                            if(before.ToArgb()==Color.White.ToArgb()) { whitePixels++; if(after!=before) throw new Exception("White F was blurred or moved"); }
                            if(after.R<before.R && after.B<before.B) shadowPixels++;
                        }
                    }
                    if(shadowPixels<10 || whitePixels<10) throw new Exception("Missing shadow or foreground F");
                }
                Console.WriteLine("PASS Purple tray icon has a soft shadow and unchanged sharp white F; app icon loads");
            }
            using(var box=new TextBox()) using(var frame=new InputFrame(box)) {
                frame.Size=new Size(300,44);frame.PerformLayout();
                Check("Input text vertically centered",box.Top,(frame.Height-box.PreferredHeight)/2);
                Check("Input text inset matches dropdown",box.Left,12);
            }
            Check("No running app is a no-op",Command(),0);
            using(var mutex=new Mutex(true,@"Local\FunctionRowRemapper-"+Environment.UserName))
            using(form=new MainForm(false,true)) {
                var handle=form.Handle;
                using(var control=new UpdateExit(form)) {
                    form.Shown+=delegate { Run(10,"Visible app refuses automated exit",delegate {
                        form.Hide(); typeof(MainForm).GetField("dirty",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,true);
                        Run(11,"Hidden unsaved edits refuse automated exit",delegate {
                            typeof(MainForm).GetField("dirty",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,false);form.Enabled=false;
                            Run(12,"Disabled form refuses automated exit",delegate {
                                form.Enabled=true;
                                Task.Run(delegate { int result=Command(); Check("Hidden clean app exits normally",result,0); });
                            });
                        });
                    }); };
                    Application.Run(form);
                    Thread.Sleep(250);
                }
            }
            return failures==0?0:1;
        }
    }
}
