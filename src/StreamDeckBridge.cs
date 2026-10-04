using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Linq;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    internal static class StreamDeckProtocol
    {
        internal static string Parse(string json)
        {
            if (String.IsNullOrWhiteSpace(json) || json.Length > 2048) throw new ArgumentException("Stream Deck message is invalid.");
            JsonSyntax.Check(json); var d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>; if (d == null || d.Count < 2 || d.Count > 3 || !(d["version"] is int) || (int)d["version"] != 1 || !(d["action"] is string)) throw new ArgumentException("Unsupported Stream Deck message.");
            string action = (string)d["action"]; if (action != "status" && action != "show-settings" && action != "activate-profile") throw new ArgumentException("Unsupported Stream Deck action.");
            if (action == "activate-profile" && (!d.ContainsKey("profile") || !(d["profile"] is string) || String.IsNullOrWhiteSpace((string)d["profile"]) || ((string)d["profile"]).Length > 40 || ((string)d["profile"]).Any(Char.IsControl))) throw new ArgumentException("A valid profile is required.");
            if (action != "activate-profile" && d.ContainsKey("profile")) throw new ArgumentException("Profile is only valid for activate-profile.");
            return action;
        }
        internal static string Profile(string json) { var d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>; return d != null && d.ContainsKey("profile") ? d["profile"] as string : ""; }
    }
    internal sealed class StreamDeckBridge : IDisposable
    {
        readonly Action<string> dispatch; readonly Thread thread; volatile bool stopping;
        internal static string PipeName { get { return "KiWeave-StreamDeck-" + Environment.UserName; } }
        internal StreamDeckBridge(Action<string> action) { dispatch = action; thread = new Thread(Loop) { IsBackground = true, Name = "KiWeave Stream Deck IPC" }; thread.Start(); }
        void Loop()
        {
            while (!stopping) try { using (var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous)) { pipe.WaitForConnection(); using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 2048, true)) { string line = reader.ReadLine(); string response = "{\"ok\":false}"; try { string action = StreamDeckProtocol.Parse(line); if (action == "status") response = "{\"ok\":true,\"status\":\"ready\"}"; else { dispatch(action == "activate-profile" ? "activate-profile:" + StreamDeckProtocol.Profile(line) : action); response = "{\"ok\":true}"; } } catch (Exception ex) { response = "{\"ok\":false,\"error\":\"" + ex.Message.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}"; } using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true }) writer.WriteLine(response); } } } catch { if (!stopping) Thread.Sleep(250); }
        }
        public void Dispose() { stopping = true; try { using (var wake = new NamedPipeClientStream(".", PipeName, PipeDirection.Out)) wake.Connect(50); } catch { } if (thread != null && thread != Thread.CurrentThread) thread.Join(500); }
    }
}
