using System;
using System.Runtime.InteropServices;

namespace FunctionRowRemapper
{
    /// <summary>Notifies the UI when the foreground window changes, with a caller-owned polling fallback.</summary>
    internal sealed class ForegroundChangeMonitor : IDisposable
    {
        const uint EventSystemForeground = 0x0003;
        const uint WineventOutOfContext = 0x0000;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint eventThread, uint eventTime);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnhookWinEvent(IntPtr hook);

        readonly Action foregroundChanged;
        readonly WinEventProc callback;
        IntPtr hook;
        bool disposed;

        internal ForegroundChangeMonitor(Action foregroundChanged)
        {
            if (foregroundChanged == null) throw new ArgumentNullException("foregroundChanged");
            this.foregroundChanged = foregroundChanged;
            callback = OnWinEvent;
            try { hook = SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, callback, 0, 0, WineventOutOfContext); }
            catch { hook = IntPtr.Zero; }
        }

        internal bool IsInstalled { get { return hook != IntPtr.Zero; } }
        internal static bool IsForegroundEvent(uint eventType) { return eventType == EventSystemForeground; }

        void OnWinEvent(IntPtr hookHandle, uint eventType, IntPtr window, int objectId, int childId, uint eventThread, uint eventTime)
        {
            if (disposed || !IsForegroundEvent(eventType)) return;
            try { foregroundChanged(); } catch { /* The UI's periodic fallback will retry safely. */ }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            IntPtr current = hook;
            hook = IntPtr.Zero;
            if (current != IntPtr.Zero) try { UnhookWinEvent(current); } catch { }
        }
    }
}
