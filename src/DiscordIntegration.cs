using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal static class DiscordIntegration
    {
        // Public application id only. The refresh token is encrypted with the
        // current Windows account and kept out of configs, backups, logs, and Git.
        internal const string ClientId = "1555947532616597595";
        internal const string RedirectUri = "http://127.0.0.1:46721/callback/";
        static readonly object Gate = new object();
        static DiscordIpcClient client;
        static string accessToken = "";
        static string refreshToken = LoadRefreshToken();
        static bool restoring;
        static System.Threading.Timer reconnectTimer;

        internal static bool Connected { get { lock (Gate) return client != null && client.IsConnected; } }
        internal static bool HasAuthorization { get { lock (Gate) return !String.IsNullOrEmpty(refreshToken) || Connected; } }
        internal static string Status { get { lock (Gate) return Connected ? "Connected" : (String.IsNullOrEmpty(refreshToken) ? "Not connected" : "Authorized; waiting for Discord"); } }

        internal static void Start(bool networkAllowed)
        {
            lock (Gate) { if (networkAllowed) StartReconnectMonitorLocked(); }
        }

        internal static void SetNetworkAccess(bool enabled)
        {
            lock (Gate) { if (enabled) StartReconnectMonitorLocked(); }
        }

        internal static string CreateAuthorizationUrl(out string state, out string verifier)
        {
            state = RandomText(32); verifier = RandomText(48);
            string challenge;
            using (var sha = SHA256.Create()) challenge = Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            return "https://discord.com/oauth2/authorize?response_type=code&client_id=" + Uri.EscapeDataString(ClientId) +
                "&scope=" + Uri.EscapeDataString("identify rpc rpc.voice.read rpc.voice.write") +
                "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) + "&state=" + Uri.EscapeDataString(state) +
                "&code_challenge=" + Uri.EscapeDataString(challenge) + "&code_challenge_method=S256";
        }

        internal static async Task<bool> ConnectInteractive(IWin32Window owner, Action<string> status)
        {
            NetworkPolicy.Require();
            lock (Gate) if (Connected) return true;
            string state, verifier; string url = CreateAuthorizationUrl(out state, out verifier);
            using (var listener = new HttpListener()) {
                listener.Prefixes.Add(RedirectUri);
                try { listener.Start(); } catch (Exception ex) { throw new InvalidOperationException("KiWeave could not open its private Discord callback: " + ex.Message); }
                try {
                    using (var browser = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })) { }
                    status("Waiting for Discord authorization in your browser...");
                    HttpListenerContext context = await Task.Run(() => listener.GetContext()).ConfigureAwait(true);
                    string returnedState = context.Request.QueryString["state"] ?? "";
                    string code = context.Request.QueryString["code"] ?? "";
                    string error = context.Request.QueryString["error"] ?? "";
                    string html = String.IsNullOrEmpty(error) ? "<html><body>You can return to KiWeave.</body></html>" : "<html><body>Discord authorization was not completed.</body></html>";
                    byte[] htmlBytes = Encoding.UTF8.GetBytes(html); context.Response.ContentType = "text/html"; context.Response.ContentLength64 = htmlBytes.Length; using (Stream stream = context.Response.OutputStream) stream.Write(htmlBytes, 0, htmlBytes.Length);
                    if (!String.Equals(returnedState, state, StringComparison.Ordinal) || String.IsNullOrEmpty(code)) throw new InvalidOperationException(String.IsNullOrEmpty(error) ? "Discord returned an invalid authorization response." : "Discord authorization was declined: " + error);
                    status("Exchanging the short-lived authorization code...");
                    TokenSet tokens = await Task.Run(() => ExchangeCode(code, verifier)).ConfigureAwait(true);
                    var next = new DiscordIpcClient(tokens.AccessToken); next.Connect();
                    lock (Gate) { if (client != null) client.Dispose(); client = next; accessToken = tokens.AccessToken; if (!String.IsNullOrEmpty(tokens.RefreshToken)) refreshToken = tokens.RefreshToken; SaveRefreshTokenLocked(); StartReconnectMonitorLocked(); }
                    status("Connected to Discord. Authorization will be reused on this Windows account."); return true;
                } finally { listener.Stop(); }
            }
        }

        sealed class TokenSet
        {
            internal string AccessToken;
            internal string RefreshToken;
        }

        static TokenSet ExchangeCode(string code, string verifier)
        {
            // KiWeave targets .NET Framework 4.8, but some Windows installations
            // still inherit an older ServicePointManager default. Discord requires
            // modern TLS, so make the protocol choice explicit for this HTTPS call.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string body = "client_id=" + Uri.EscapeDataString(ClientId) + "&grant_type=authorization_code&code=" + Uri.EscapeDataString(code) +
                "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) + "&code_verifier=" + Uri.EscapeDataString(verifier);
            return PostToken(body);
        }

        static TokenSet RefreshAccessToken(string token)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string body = "client_id=" + Uri.EscapeDataString(ClientId) + "&grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(token);
            return PostToken(body);
        }

        static TokenSet PostToken(string body)
        {
            var request = (HttpWebRequest)WebRequest.Create("https://discord.com/api/oauth2/token"); request.Method = "POST"; request.ContentType = "application/x-www-form-urlencoded"; request.UserAgent = "KiWeave/1.0"; request.Timeout = 8000; request.ReadWriteTimeout = 8000;
            byte[] bytes = Encoding.UTF8.GetBytes(body); request.ContentLength = bytes.Length;
            using (Stream stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
            using (var response = (HttpWebResponse)request.GetResponse()) using (var reader = new StreamReader(response.GetResponseStream())) {
                var data = new JavaScriptSerializer().DeserializeObject(reader.ReadToEnd()) as Dictionary<string, object>;
                string access = data == null || !data.ContainsKey("access_token") ? "" : data["access_token"] as string;
                if (String.IsNullOrEmpty(access)) throw new InvalidOperationException("Discord did not return an access token. Public Client/PKCE may not be enabled yet.");
                return new TokenSet { AccessToken = access, RefreshToken = data.ContainsKey("refresh_token") ? data["refresh_token"] as string : "" };
            }
        }

        internal static bool TryToggleVoice(string field)
        {
            lock (Gate) {
                if (!Connected && !String.IsNullOrEmpty(accessToken)) ReconnectLocked(250);
                if (!Connected) return false;
                try { client.ToggleVoice(field); return true; } catch { return false; }
            }
        }

        static void StartReconnectMonitorLocked()
        {
            if (reconnectTimer != null) return;
            reconnectTimer = new System.Threading.Timer(delegate { ReconnectIfNeeded(); }, null, 1500, 3000);
        }

        static void ReconnectIfNeeded()
        {
            lock (Gate)
            {
                if (!NetworkPolicy.Enabled || restoring || Connected || !DiscordDesktopIsRunning() || (String.IsNullOrEmpty(accessToken) && String.IsNullOrEmpty(refreshToken))) return;
                if (String.IsNullOrEmpty(accessToken) && !String.IsNullOrEmpty(refreshToken)) {
                    restoring = true;
                    try { TokenSet tokens = RefreshAccessToken(refreshToken); accessToken = tokens.AccessToken; if (!String.IsNullOrEmpty(tokens.RefreshToken)) refreshToken = tokens.RefreshToken; SaveRefreshTokenLocked(); }
                    catch { accessToken = ""; return; }
                    finally { restoring = false; }
                }
                ReconnectLocked(1200);
            }
        }

        static void ReconnectLocked(int timeoutMilliseconds)
        {
            var next = new DiscordIpcClient(accessToken);
            try { next.Connect(timeoutMilliseconds); if (client != null) client.Dispose(); client = next; }
            catch { next.Dispose(); }
        }

        static bool DiscordDesktopIsRunning()
        {
            return Process.GetProcessesByName("Discord").Length > 0 ||
                Process.GetProcessesByName("DiscordPTB").Length > 0 ||
                Process.GetProcessesByName("DiscordCanary").Length > 0;
        }

        internal static void Disconnect()
        {
            lock (Gate) { if (reconnectTimer != null) { reconnectTimer.Dispose(); reconnectTimer = null; } if (client != null) client.Dispose(); client = null; accessToken = ""; }
        }
        internal static void Reconnect()
        {
            lock (Gate) { Disconnect(); if (NetworkPolicy.Enabled) StartReconnectMonitorLocked(); }
        }

        internal static void ForgetAuthorization()
        {
            lock (Gate) { Disconnect(); refreshToken = ""; DeleteRefreshTokenLocked(); }
        }

        static string TokenPath { get { return Path.Combine(AppStorage.DataFolder, "discord.refresh.dpapi"); } }
        static string LoadRefreshToken()
        {
            try { if (!File.Exists(TokenPath)) return ""; byte[] protectedBytes = File.ReadAllBytes(TokenPath); return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser)); }
            catch { return ""; }
        }
        static void SaveRefreshTokenLocked()
        {
            if (String.IsNullOrEmpty(refreshToken)) return;
            try { Directory.CreateDirectory(AppStorage.DataFolder); string temp = TokenPath + ".tmp"; File.WriteAllBytes(temp, ProtectedData.Protect(Encoding.UTF8.GetBytes(refreshToken), null, DataProtectionScope.CurrentUser)); File.Copy(temp, TokenPath, true); File.Delete(temp); }
            catch { }
        }
        static void DeleteRefreshTokenLocked()
        {
            try { if (File.Exists(TokenPath)) File.Delete(TokenPath); } catch { }
        }

        static string RandomText(int bytes)
        {
            byte[] data = new byte[bytes]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(data); return Base64Url(data);
        }
        static string Base64Url(byte[] data) { return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
    }

    internal sealed class DiscordIpcClient : IDisposable
    {
        readonly string token; NamedPipeClientStream pipe; readonly JavaScriptSerializer json = new JavaScriptSerializer(); int nonce; bool? muteState; bool? deafState;
        internal bool IsConnected { get { return pipe != null && pipe.IsConnected; } }
        internal DiscordIpcClient(string token) { this.token = token; }
        internal void Connect() { Connect(1200); }
        internal void Connect(int timeoutMilliseconds)
        {
            Exception last = null;
            for (int i = 0; i < 10; i++) try { pipe = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut, PipeOptions.Asynchronous); pipe.Connect(timeoutMilliseconds); break; } catch (Exception ex) { last = ex; pipe = null; }
            if (!IsConnected) throw new InvalidOperationException("Discord's local RPC pipe was not available." + (last == null ? "" : " " + last.Message));
            Send(0, json.Serialize(new Dictionary<string, object> { { "v", 1 }, { "client_id", DiscordIntegration.ClientId } })); Read();
            Send(1, json.Serialize(new Dictionary<string, object> { { "cmd", "AUTHENTICATE" }, { "nonce", NextNonce() }, { "args", new Dictionary<string, object> { { "access_token", token } } } })); Read(); RefreshVoiceState();
        }
        internal void SetVoice(string field, bool value)
        {
            // Discord's voice mute/deafen fields are top-level SET_VOICE_SETTINGS
            // arguments, not members of the input-device object.
            var args = new Dictionary<string, object>(); args[field] = value;
            Send(1, json.Serialize(new Dictionary<string, object> { { "cmd", "SET_VOICE_SETTINGS" }, { "nonce", NextNonce() }, { "args", args } })); Read();
        }
        internal void ToggleVoice(string field)
        {
            bool current;
            if (field == "mute" && muteState.HasValue) current = muteState.Value;
            else if (field == "deaf" && deafState.HasValue) current = deafState.Value;
            else current = GetVoice(field);
            SetVoice(field, !current);
            if (field == "mute") muteState = !current;
            if (field == "deaf") deafState = !current;
        }
        void RefreshVoiceState()
        {
            Send(1, json.Serialize(new Dictionary<string, object> { { "cmd", "GET_VOICE_SETTINGS" }, { "nonce", NextNonce() }, { "args", new Dictionary<string, object>() } }));
            var response = Read(); var data = response == null ? null : response["data"] as Dictionary<string, object>; object value;
            if (data != null && data.TryGetValue("mute", out value) && value is bool) muteState = (bool)value;
            if (data != null && data.TryGetValue("deaf", out value) && value is bool) deafState = (bool)value;
        }
        bool GetVoice(string field)
        {
            Send(1, json.Serialize(new Dictionary<string, object> { { "cmd", "GET_VOICE_SETTINGS" }, { "nonce", NextNonce() }, { "args", new Dictionary<string, object>() } }));
            var response = Read(); var data = response == null ? null : response["data"] as Dictionary<string, object>;
            object value; if (data == null || !data.TryGetValue(field, out value) || !(value is bool)) throw new InvalidOperationException("Discord did not return the current voice state.");
            if (field == "mute") muteState = (bool)value; if (field == "deaf") deafState = (bool)value; return (bool)value;
        }
        void Send(int opcode, string payload)
        {
            byte[] body = Encoding.UTF8.GetBytes(payload); byte[] header = new byte[8]; Buffer.BlockCopy(BitConverter.GetBytes(opcode), 0, header, 0, 4); Buffer.BlockCopy(BitConverter.GetBytes(body.Length), 0, header, 4, 4); pipe.Write(header, 0, header.Length); pipe.Write(body, 0, body.Length); pipe.Flush();
        }
        Dictionary<string, object> Read()
        {
            byte[] header = ReadExact(8); int opcode = BitConverter.ToInt32(header, 0); int length = BitConverter.ToInt32(header, 4); if (length < 0 || length > 1024 * 1024) throw new InvalidOperationException("Discord returned an invalid RPC frame.");
            var data = json.DeserializeObject(Encoding.UTF8.GetString(ReadExact(length))) as Dictionary<string, object>; if (data != null && data.ContainsKey("evt") && (data["evt"] as string) == "ERROR") throw new InvalidOperationException("Discord rejected the RPC request."); return data;
        }
        byte[] ReadExact(int length)
        {
            byte[] data = new byte[length]; int offset = 0; while (offset < length) { int n = pipe.Read(data, offset, length - offset); if (n <= 0) throw new EndOfStreamException("Discord RPC disconnected."); offset += n; } return data;
        }
        string NextNonce() { return (++nonce).ToString(); }
        public void Dispose() { if (pipe != null) { try { pipe.Dispose(); } catch { } pipe = null; } }
    }

    internal sealed class DiscordConnectionForm : Form
    {
        readonly Label status = new Label(); readonly Button connect; readonly Button forget;
        internal DiscordConnectionForm(IWin32Window owner) {
            Text = "KiWeave - Discord connection"; Icon = Program.AppIcon(); StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 250); MinimumSize = new Size(560, 250); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            var root = UiStyle.Stack(); root.Padding = new Padding(24); Controls.Add(root); root.Controls.Add(UiStyle.Text("Discord connection", 18, true)); root.Controls.Add(UiStyle.Text("Authorizes once, then reuses an encrypted refresh token for this Windows account. Access tokens stay in memory.", 9, false));
            status.Text = DiscordIntegration.Status; status.AutoSize = true; status.ForeColor = UiStyle.Muted; status.Margin = new Padding(0, 18, 0, 18); root.Controls.Add(status);
            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
            connect = UiStyle.Button("Connect Discord", delegate { Connect(owner); }, true); buttons.Controls.Add(connect);
            forget = UiStyle.Button("Forget authorization", delegate { DiscordIntegration.ForgetAuthorization(); status.Text = DiscordIntegration.Status; forget.Enabled = DiscordIntegration.HasAuthorization; }, false); forget.Enabled = DiscordIntegration.HasAuthorization; buttons.Controls.Add(forget);
            root.Controls.Add(buttons); root.Controls.Add(UiStyle.Text("Authorization is encrypted for this Windows account and reused across KiWeave launches. Beta voice control may remain unavailable until the KiWeave Discord app is public/approved.", 8.5f, false));
        }
        async void Connect(IWin32Window owner)
        {
            try { connect.Enabled = false; bool ok = await DiscordIntegration.ConnectInteractive(owner, text => BeginInvoke((Action)(() => status.Text = text))); status.Text = ok ? DiscordIntegration.Status : "Not connected"; forget.Enabled = DiscordIntegration.HasAuthorization; } catch (Exception ex) { status.Text = ex.Message; MessageBox.Show(this, ex.Message, "Discord connection", MessageBoxButtons.OK, MessageBoxIcon.Information); } finally { connect.Enabled = true; }
        }
    }
}
