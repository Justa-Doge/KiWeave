using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class UpdateNotification : IDisposable
    {
        readonly NotifyIcon icon = new NotifyIcon();
        readonly Timer lifetime = new Timer { Interval = 45000 };
        readonly string tag;
        bool disposed;

        public UpdateNotification(string tag)
        {
            this.tag = tag;
            icon.Icon = Program.TrayIcon();
            icon.Text = "KiWeave update available";
            icon.BalloonTipClicked += delegate { OpenRelease(); };
            icon.DoubleClick += delegate { OpenRelease(); };
            lifetime.Tick += delegate { Dispose(); };
            icon.Visible = true;
            icon.ShowBalloonTip(8000, "KiWeave update available",
                tag + " is on GitHub. Click to view it. Nothing was downloaded or installed.", ToolTipIcon.Info);
            lifetime.Start();
        }

        void OpenRelease()
        {
            try { Process.Start(new ProcessStartInfo("https://github.com/Justa-Doge/KeyWeave/releases/tag/" + tag) { UseShellExecute = true }); }
            catch { /* The notification is informational; failure to open a browser cannot affect hotkeys. */ }
            Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Stop(); lifetime.Dispose();
            icon.Visible = false; var image = icon.Icon; icon.Dispose(); if (image != null) image.Dispose();
        }
    }
}
