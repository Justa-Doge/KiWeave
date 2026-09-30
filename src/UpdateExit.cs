using System;
using System.Threading;

namespace FunctionRowRemapper
{
    // Same-user, same-session IPC. No forced termination and no saved-file writes.
    internal sealed class UpdateExit : IDisposable
    {
        static string Name(string suffix) { return @"Local\FunctionRowRemapper-Update-" + suffix + "-" + Environment.UserName; }
        static readonly int[] Codes = { 0, 10, 11, 12 };
        readonly EventWaitHandle request;
        readonly EventWaitHandle[] responses;
        readonly RegisteredWaitHandle listener;

        internal UpdateExit(MainForm form)
        {
            request = new EventWaitHandle(false, EventResetMode.AutoReset, Name("Request"));
            responses = new EventWaitHandle[Codes.Length];
            for (int i=0;i<Codes.Length;i++) responses[i] = new EventWaitHandle(false,EventResetMode.ManualReset,Name("Result"+Codes[i]));
            listener = ThreadPool.RegisterWaitForSingleObject(request, delegate(object state,bool timeout) {
                form.RequestUpdateExit(delegate(int code) { int index=Array.IndexOf(Codes,code); if(index>=0) responses[index].Set(); });
            }, null, Timeout.Infinite, false);
        }
        internal static int Request()
        {
            Mutex existing;
            if (!Mutex.TryOpenExisting(@"Local\FunctionRowRemapper-" + Environment.UserName,out existing)) return 0;
            existing.Dispose();
            using(var commandLock=new Mutex(false,Name("CommandLock"))) {
                bool held;
                try { held=commandLock.WaitOne(0); } catch(AbandonedMutexException) { held=true; }
                if(!held) return 15;
                try {
                    var handles=new EventWaitHandle[Codes.Length];
                    try {
                        using(var command=EventWaitHandle.OpenExisting(Name("Request"))) {
                            for(int i=0;i<Codes.Length;i++) { handles[i]=EventWaitHandle.OpenExisting(Name("Result"+Codes[i])); handles[i].Reset(); }
                            command.Set(); int result=WaitHandle.WaitAny(handles,5000);
                            return result==WaitHandle.WaitTimeout ? 13 : Codes[result];
                        }
                    } catch(WaitHandleCannotBeOpenedException) { return 14; }
                    finally { foreach(var handle in handles) if(handle!=null) handle.Dispose(); }
                } finally { commandLock.ReleaseMutex(); }
            }
        }
        public void Dispose()
        {
            listener.Unregister(null); request.Dispose(); foreach(var response in responses) response.Dispose();
        }
    }
}
