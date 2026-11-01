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
    public static class DiscordIntegration
    {
        // Public application id only. The refresh token is encrypted with the
        // current Windows account and kept out of configs, backups, logs, and Git.
        internal const string ClientId = "1555947532616597595";
        internal const string RedirectUri = "http://127.0.0.1:46721/callback/";
        static readonly object Gate = new object();
        static DiscordIpcClient client;
        static string accessToken = "";
        static bool authorizationRefreshWarning;
        static string refreshToken = LoadRefreshToken();
        static bool restoring;
        static bool reconnecting;
        static long reconnectGeneration;
        static long networkPolicyGeneration;
        static CancellationTokenSource reconnectAttemptCancellation;
        static System.Threading.Timer reconnectTimer;

        internal static bool Connected { get { lock (Gate) return client != null && client.IsConnected; } }
        internal static bool HasAuthorization { get { lock (Gate) return !String.IsNullOrEmpty(refreshToken) || Connected; } }
        public static string Status { get { lock (Gate) return StatusText(Connected, !String.IsNullOrEmpty(refreshToken), authorizationRefreshWarning); } }
        internal static string StatusText(bool connected, bool authorized, bool refreshWarning)
        {
            if (connected) return refreshWarning ? "Connected; authorization could not be saved" : "Connected";
            if (refreshWarning) return "Authorization refresh failed; reconnect to Discord.";
            return authorized ? "Authorized; waiting for Discord" : "Not connected";
        }

        internal static void Start(bool networkAllowed)
        {
            lock (Gate) { if (networkAllowed) StartReconnectMonitorLocked(); }
        }

        internal static bool ShouldMonitorReconnect(bool networkAllowed, bool hasAccessToken, bool hasRefreshToken)
        {
            return networkAllowed && (hasAccessToken || hasRefreshToken);
        }

        internal static bool ShouldCommitReconnect(long attemptGeneration, long currentGeneration, bool networkRequirementSatisfied, bool tokensUnchanged, bool alreadyConnected)
        {
            return attemptGeneration == currentGeneration && networkRequirementSatisfied && tokensUnchanged && !alreadyConnected;
        }

        internal static bool ShouldPersistRefreshedToken(bool refreshCompleted, bool tokensUnchanged)
        {
            return refreshCompleted && tokensUnchanged;
        }

        internal static bool ShouldAttemptImmediateVoiceReconnect(bool hasAccessToken, bool reconnectInProgress, bool authorizationRefreshInProgress)
        {
            return hasAccessToken && !reconnectInProgress && !authorizationRefreshInProgress;
        }

        internal static void SetNetworkAccess(bool enabled)
        {
            lock (Gate) {
                if (enabled) StartReconnectMonitorLocked();
                else { networkPolicyGeneration++; if (reconnectTimer != null) { reconnectTimer.Dispose(); reconnectTimer = null; } }
            }
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

        internal static Task<bool> ConnectInteractive(IWin32Window owner, Action<string> status)
        {
            return ConnectInteractive(owner, status, CancellationToken.None);
        }

        internal static async Task<bool> ConnectInteractive(IWin32Window owner, Action<string> status, CancellationToken callerCancellation)
        {
            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(NetworkPolicy.BeginRequest(), callerCancellation))
            {
                var cancellationToken = linkedCancellation.Token;
                lock (Gate) if (Connected) return true;
                string state, verifier; string url = CreateAuthorizationUrl(out state, out verifier);
                using (var listener = new HttpListener()) {
                listener.Prefixes.Add(RedirectUri);
                try { listener.Start(); } catch (Exception ex) { throw new InvalidOperationException("KiWeave could not open its private Discord callback: " + ex.Message); }
                using (cancellationToken.Register(delegate { try { listener.Stop(); } catch { } })) try {
                    cancellationToken.ThrowIfCancellationRequested();
                    using (var browser = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })) { }
                    status("Waiting for Discord authorization in your browser...");
                    HttpListenerContext context;
                    try { context = await Task.Run(() => listener.GetContext()).ConfigureAwait(true); }
                    catch { cancellationToken.ThrowIfCancellationRequested(); throw; }
                    cancellationToken.ThrowIfCancellationRequested();
                    string returnedState = context.Request.QueryString["state"] ?? "";
                    string code = context.Request.QueryString["code"] ?? "";
                    string error = context.Request.QueryString["error"] ?? "";
                    string html = String.IsNullOrEmpty(error) ? "<html><body>You can return to KiWeave.</body></html>" : "<html><body>Discord authorization was not completed.</body></html>";
                    byte[] htmlBytes = Encoding.UTF8.GetBytes(html); context.Response.ContentType = "text/html"; context.Response.ContentLength64 = htmlBytes.Length; using (Stream stream = context.Response.OutputStream) stream.Write(htmlBytes, 0, htmlBytes.Length);
                    if (!String.Equals(returnedState, state, StringComparison.Ordinal) || String.IsNullOrEmpty(code)) throw new InvalidOperationException(String.IsNullOrEmpty(error) ? "Discord returned an invalid authorization response." : "Discord authorization was declined: " + error);
                    status("Exchanging the short-lived authorization code...");
                    TokenSet tokens = await Task.Run(() => ExchangeCode(code, verifier, cancellationToken)).ConfigureAwait(true);
                    cancellationToken.ThrowIfCancellationRequested();
                    var next = new DiscordIpcClient(tokens.AccessToken); next.Connect();
                    lock (Gate) { reconnectGeneration++; if (client != null) client.Dispose(); client = next; accessToken = tokens.AccessToken; if (!String.IsNullOrEmpty(tokens.RefreshToken)) refreshToken = tokens.RefreshToken; authorizationRefreshWarning = false; SaveRefreshTokenLocked(); StartReconnectMonitorLocked(); }
                    status("Connected to Discord. Authorization will be reused on this Windows account."); return true;
                } finally { listener.Stop(); }
                }
            }
        }

        sealed class TokenSet
        {
            internal string AccessToken;
            internal string RefreshToken;
        }

        static TokenSet ExchangeCode(string code, string verifier, CancellationToken cancellationToken)
        {
            // KiWeave targets .NET Framework 4.8, but some Windows installations
            // still inherit an older ServicePointManager default. Discord requires
            // modern TLS, so make the protocol choice explicit for this HTTPS call.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string body = "client_id=" + Uri.EscapeDataString(ClientId) + "&grant_type=authorization_code&code=" + Uri.EscapeDataString(code) +
                "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) + "&code_verifier=" + Uri.EscapeDataString(verifier);
            return PostToken(body, cancellationToken);
        }

        static TokenSet RefreshAccessToken(string token)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string body = "client_id=" + Uri.EscapeDataString(ClientId) + "&grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(token);
            return PostToken(body);
        }

        static TokenSet RefreshAccessToken(string token, CancellationToken cancellationToken)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string body = "client_id=" + Uri.EscapeDataString(ClientId) + "&grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(token);
            return PostToken(body, cancellationToken);
        }

        static TokenSet PostToken(string body)
        {
            return PostToken(body, CancellationToken.None);
        }

        static TokenSet PostToken(string body, CancellationToken callerCancellation)
        {
            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(NetworkPolicy.BeginRequest(), callerCancellation))
            {
            var token = linkedCancellation.Token;
            var request = (HttpWebRequest)WebRequest.Create("https://discord.com/api/oauth2/token"); request.Method = "POST"; request.ContentType = "application/x-www-form-urlencoded"; request.UserAgent = "KiWeave/1.0"; request.Timeout = 8000; request.ReadWriteTimeout = 8000;
            using (token.Register(delegate { request.Abort(); })) {
                token.ThrowIfCancellationRequested();
                byte[] bytes = Encoding.UTF8.GetBytes(body); request.ContentLength = bytes.Length;
                using (Stream stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
                token.ThrowIfCancellationRequested();
                using (var response = (HttpWebResponse)request.GetResponse()) using (var stream = response.GetResponseStream()) {
                    string json = AtomicFile.ReadStreamTextBounded(stream, 65536, Encoding.UTF8);
                    var data = new JavaScriptSerializer { MaxJsonLength = 65536, RecursionLimit = 8 }.DeserializeObject(json) as Dictionary<string, object>;
                    token.ThrowIfCancellationRequested();
                    string access = data == null || !data.ContainsKey("access_token") ? "" : data["access_token"] as string;
                    if (String.IsNullOrEmpty(access)) throw new InvalidOperationException("Discord did not return an access token. Public Client/PKCE may not be enabled yet.");
                    return new TokenSet { AccessToken = access, RefreshToken = data.ContainsKey("refresh_token") ? data["refresh_token"] as string : "" };
                }
            }
            }
        }

        internal static bool TryToggleVoice(string field)
        {
            bool state; return TryToggleVoice(field, out state);
        }

        internal static bool TryToggleVoice(string field, out bool enabled)
        {
            enabled = false;
            DiscordIpcClient current;
            lock (Gate) {
                if (!NetworkPolicy.Enabled && accessToken.Length == 0) return false;
                current = Connected ? client : null;
            }

            for (int attempt = 0; attempt < 2; attempt++) {
                if (current == null && !TryReconnectVoiceClient(out current)) return false;
                if (current == null) return false;
                try { enabled = current.ToggleVoice(field); return true; }
                catch {
                    lock (Gate) {
                        if (ReferenceEquals(client, current)) client = null;
                    }
                    try { current.Dispose(); } catch { }
                    current = null;
                }
            }
            return false;
        }

        static bool TryReconnectVoiceClient(out DiscordIpcClient connected)
        {
            connected = null;
            string observedAccessToken;
            long generation;
            long observedNetworkPolicyGeneration;
            DiscordIpcClient previous;
            lock (Gate) {
                if (Connected) { connected = client; return true; }
                if (!ShouldAttemptImmediateVoiceReconnect(accessToken.Length > 0, reconnecting, restoring)) return false;
                reconnecting = true;
                generation = reconnectGeneration;
                observedNetworkPolicyGeneration = networkPolicyGeneration;
                observedAccessToken = accessToken;
                previous = client;
                client = null;
            }

            if (previous != null) { try { previous.Dispose(); } catch { } }
            DiscordIpcClient candidate = null;
            try {
                candidate = new DiscordIpcClient(observedAccessToken);
                candidate.Connect(250);
            }
            catch { if (candidate != null) { try { candidate.Dispose(); } catch { } candidate = null; } }

            DiscordIpcClient replaced = null;
            lock (Gate) {
                bool policyUnchanged = networkPolicyGeneration == observedNetworkPolicyGeneration;
                bool tokensUnchanged = String.Equals(accessToken, observedAccessToken, StringComparison.Ordinal);
                bool alreadyConnected = Connected;
                if (candidate != null && ShouldCommitReconnect(generation, reconnectGeneration, policyUnchanged, tokensUnchanged, alreadyConnected)) {
                    replaced = client;
                    client = candidate;
                    connected = candidate;
                    candidate = null;
                }
                else if (Connected) connected = client;
                reconnecting = false;
            }
            if (replaced != null) { try { replaced.Dispose(); } catch { } }
            if (candidate != null) { try { candidate.Dispose(); } catch { } }
            return connected != null;
        }

        static void StartReconnectMonitorLocked()
        {
            if (!ShouldMonitorReconnect(NetworkPolicy.Enabled, !String.IsNullOrEmpty(accessToken), !String.IsNullOrEmpty(refreshToken)) || reconnectTimer != null) return;
            reconnectTimer = new System.Threading.Timer(delegate { ReconnectIfNeeded(); }, null, 1500, 3000);
        }

        static void ReconnectIfNeeded()
        {
            string observedAccessToken, observedRefreshToken;
            long generation;
            long observedNetworkPolicyGeneration;
            bool tokenRefreshNeeded;
            CancellationTokenSource attemptCancellation;
            lock (Gate)
            {
                if (!NetworkPolicy.Enabled || restoring || reconnecting || Connected || (String.IsNullOrEmpty(accessToken) && String.IsNullOrEmpty(refreshToken))) return;
                reconnecting = true;
                generation = reconnectGeneration;
                observedNetworkPolicyGeneration = networkPolicyGeneration;
                observedAccessToken = accessToken;
                observedRefreshToken = refreshToken;
                tokenRefreshNeeded = String.IsNullOrEmpty(observedAccessToken) && !String.IsNullOrEmpty(observedRefreshToken);
                attemptCancellation = new CancellationTokenSource();
                reconnectAttemptCancellation = attemptCancellation;
                if (tokenRefreshNeeded) restoring = true;
            }

            TokenSet refreshed = null;
            DiscordIpcClient next = null;
            DiscordIpcClient previous = null;
            bool refreshFailed = false;
            try
            {
                attemptCancellation.Token.ThrowIfCancellationRequested();
                if (!DiscordDesktopIsRunning()) return;
                string candidateAccessToken = observedAccessToken;
                if (tokenRefreshNeeded)
                {
                    try { refreshed = RefreshAccessToken(observedRefreshToken, attemptCancellation.Token); candidateAccessToken = refreshed.AccessToken; }
                    catch { refreshFailed = true; return; }
                }
                if (!String.IsNullOrEmpty(candidateAccessToken))
                {
                    attemptCancellation.Token.ThrowIfCancellationRequested();
                    next = new DiscordIpcClient(candidateAccessToken);
                    next.Connect(1200);
                }
            }
            catch
            {
                // The monitor retries on its next bounded interval; failures must
                // not hold the shared status lock or disturb an existing session.
            }
            finally
            {
                lock (Gate)
                {
                    bool tokensUnchanged = String.Equals(accessToken, observedAccessToken, StringComparison.Ordinal) && String.Equals(refreshToken, observedRefreshToken, StringComparison.Ordinal);
                    bool alreadyConnected = client != null && client.IsConnected;
                    bool networkRequirementSatisfied = !tokenRefreshNeeded || refreshed != null ||
                        (NetworkPolicy.Enabled && networkPolicyGeneration == observedNetworkPolicyGeneration);
                    bool canPersistRotatedToken = ShouldPersistRefreshedToken(refreshed != null, tokensUnchanged);
                    if (canPersistRotatedToken)
                    {
                        accessToken = refreshed.AccessToken;
                        if (!String.IsNullOrEmpty(refreshed.RefreshToken)) refreshToken = refreshed.RefreshToken;
                        SaveRefreshTokenLocked();
                        authorizationRefreshWarning = false;
                    }
                    if (ShouldCommitReconnect(generation, reconnectGeneration, networkRequirementSatisfied, tokensUnchanged, alreadyConnected))
                    {
                        if (refreshFailed)
                        {
                            accessToken = "";
                            authorizationRefreshWarning = true;
                        }
                        else
                        {
                            if (next != null && next.IsConnected)
                            {
                                previous = client;
                                client = next;
                                next = null;
                                authorizationRefreshWarning = false;
                            }
                        }
                    }
                    if (tokenRefreshNeeded) restoring = false;
                    if (ReferenceEquals(reconnectAttemptCancellation, attemptCancellation)) reconnectAttemptCancellation = null;
                    reconnecting = false;
                }
                if (previous != null) try { previous.Dispose(); } catch { }
                if (next != null) try { next.Dispose(); } catch { }
                attemptCancellation.Dispose();
            }
        }

        static bool DiscordDesktopIsRunning()
        {
            return ProcessProbe.AnyRunning("Discord", "DiscordPTB", "DiscordCanary");
        }

        internal static void Disconnect()
        {
            lock (Gate) { reconnectGeneration++; try { if (reconnectAttemptCancellation != null) reconnectAttemptCancellation.Cancel(); } catch { } if (reconnectTimer != null) { reconnectTimer.Dispose(); reconnectTimer = null; } if (client != null) client.Dispose(); client = null; accessToken = ""; }
        }
        internal static void Reconnect()
        {
            lock (Gate)
            {
                reconnectGeneration++;
                try { if (reconnectAttemptCancellation != null) reconnectAttemptCancellation.Cancel(); } catch { }
                if (reconnectTimer != null) { reconnectTimer.Dispose(); reconnectTimer = null; }
                if (client != null) client.Dispose();
                client = null;
                if (NetworkPolicy.Enabled) StartReconnectMonitorLocked();
            }
        }

        internal static void ForgetAuthorization()
        {
            lock (Gate) { Disconnect(); refreshToken = ""; authorizationRefreshWarning = false; DeleteRefreshTokenLocked(); }
        }

        static string TokenPath { get { return Path.Combine(AppStorage.DataFolder, "discord.refresh.dpapi"); } }
        static string LoadRefreshToken()
        {
            try { return LoadProtectedToken(TokenPath); }
            catch { authorizationRefreshWarning = true; return ""; }
        }
        internal static string LoadProtectedToken(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A token path is required.", "path");
            if (!File.Exists(path)) return "";
            try
            {
                byte[] protectedBytes = AtomicFile.ReadAllBytesBounded(path, 64 * 1024);
                string token = Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser));
                if (String.IsNullOrWhiteSpace(token) || token.Length > 16 * 1024) throw new InvalidDataException();
                return token;
            }
            catch (Exception error) { throw new InvalidDataException("Saved Discord authorization could not be read; reconnect to Discord. The existing file was left unchanged.", error); }
        }
        internal static void SaveProtectedToken(string path, string token)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A token path is required.", "path");
            if (String.IsNullOrWhiteSpace(token) || token.Length > 16 * 1024) throw new ArgumentException("Discord returned an empty or oversized refresh token.", "token");
            byte[] protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
            AtomicFile.WriteAllBytes(path, protectedBytes, ".bak");
        }
        static void SaveRefreshTokenLocked()
        {
            if (String.IsNullOrEmpty(refreshToken)) return;
            try { SaveProtectedToken(TokenPath, refreshToken); authorizationRefreshWarning = false; }
            catch { authorizationRefreshWarning = true; }
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
        internal const int RpcResponseTimeoutMilliseconds = 3000;
        readonly string token; readonly Func<int, string> pipeNameAt; readonly object voiceGate = new object(); NamedPipeClientStream pipe; readonly JavaScriptSerializer json = new JavaScriptSerializer(); int nonce; bool? muteState; bool? deafState;
        internal bool IsConnected { get { return pipe != null && pipe.IsConnected; } }
        internal DiscordIpcClient(string token) : this(token, delegate(int index) { return "discord-ipc-" + index; }) { }
        internal DiscordIpcClient(string token, Func<int, string> pipeNameAt) { if (pipeNameAt == null) throw new ArgumentNullException("pipeNameAt"); this.token = token; this.pipeNameAt = pipeNameAt; }
        internal void Connect() { Connect(1200); }
        internal void Connect(int timeoutMilliseconds)
        {
            Exception last = null;
            for (int i = 0; i < 10; i++) {
                NamedPipeClientStream candidate = null;
                try { candidate = new NamedPipeClientStream(".", pipeNameAt(i), PipeDirection.InOut, PipeOptions.Asynchronous); candidate.Connect(timeoutMilliseconds); pipe = candidate; break; }
                catch (Exception ex) { last = ex; if (candidate != null) { try { candidate.Dispose(); } catch { } } pipe = null; }
            }
            if (!IsConnected) throw new InvalidOperationException("Discord's local RPC pipe was not available." + (last == null ? "" : " " + last.Message));
            try {
                Send(0, json.Serialize(new Dictionary<string, object> { { "v", 1 }, { "client_id", DiscordIntegration.ClientId } })); Read();
                Send(1, json.Serialize(new Dictionary<string, object> { { "cmd", "AUTHENTICATE" }, { "nonce", NextNonce() }, { "args", new Dictionary<string, object> { { "access_token", token } } } })); Read(); RefreshVoiceState();
            }
            catch { Dispose(); throw; }
        }
        internal void SetVoice(string field, bool value)
        {
            // Discord's voice mute/deafen fields are top-level SET_VOICE_SETTINGS
            // arguments, not members of the input-device object.
            var args = new Dictionary<string, object>(); args[field] = value;
            Send(1, json.Serialize(new Dictionary<string, object> { { "cmd", "SET_VOICE_SETTINGS" }, { "nonce", NextNonce() }, { "args", args } })); Read();
        }
        internal bool ToggleVoice(string field)
        {
            lock (voiceGate) {
                bool current;
                if (field == "mute" && muteState.HasValue) current = muteState.Value;
                else if (field == "deaf" && deafState.HasValue) current = deafState.Value;
                else current = GetVoice(field);
                SetVoice(field, !current);
                if (field == "mute") muteState = !current;
                if (field == "deaf") deafState = !current;
                return !current;
            }
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
            var deadline = Stopwatch.StartNew();
            byte[] header = ReadExact(8, deadline); int opcode = BitConverter.ToInt32(header, 0); int length = BitConverter.ToInt32(header, 4); if (length < 0 || length > 1024 * 1024) throw new InvalidOperationException("Discord returned an invalid RPC frame.");
            var data = json.DeserializeObject(Encoding.UTF8.GetString(ReadExact(length, deadline))) as Dictionary<string, object>;
            if (data != null && data.ContainsKey("evt") && (data["evt"] as string) == "ERROR") {
                string detail = ""; object error;
                if (data.TryGetValue("data", out error) && error is Dictionary<string, object>) {
                    var errorData = (Dictionary<string, object>)error; object message; object code;
                    if (errorData.TryGetValue("message", out message) && message != null) detail = message.ToString();
                    if (errorData.TryGetValue("code", out code) && code != null) detail += (detail.Length == 0 ? "" : " ") + "(code " + code + ")";
                }
                throw new InvalidOperationException("Discord rejected the RPC request." + (detail.Length == 0 ? "" : " " + detail));
            }
            return data;
        }
        byte[] ReadExact(int length, Stopwatch deadline)
        {
            byte[] data = new byte[length]; int offset = 0;
            while (offset < length) {
                int remaining = RpcResponseTimeoutMilliseconds - (int)Math.Min(Int32.MaxValue, deadline.ElapsedMilliseconds);
                if (remaining <= 0) { Dispose(); throw new TimeoutException("Discord RPC did not respond in time."); }
                int n = ReadWithTimeout(pipe, data, offset, length - offset, remaining);
                if (n <= 0) throw new EndOfStreamException("Discord RPC disconnected.");
                offset += n;
            }
            return data;
        }
        internal static int ReadWithTimeout(Stream stream, byte[] buffer, int offset, int count, int timeoutMilliseconds)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (timeoutMilliseconds <= 0) throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            Task<int> read = stream.ReadAsync(buffer, offset, count, CancellationToken.None);
            if (!read.Wait(timeoutMilliseconds)) {
                try { stream.Dispose(); } catch { }
                read.ContinueWith(task => { var ignored = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                throw new TimeoutException("Discord RPC did not respond in time.");
            }
            return read.GetAwaiter().GetResult();
        }
        string NextNonce() { return (++nonce).ToString(); }
        public void Dispose() { if (pipe != null) { try { pipe.Dispose(); } catch { } pipe = null; } }
    }

#if !WPF_REWRITE
    internal sealed class DiscordConnectionForm : Form
    {
        readonly Label status = new Label(); readonly Button connect; readonly Button forget;
        readonly CancellationTokenSource connectionLifetime = new CancellationTokenSource();
        internal DiscordConnectionForm(IWin32Window owner) {
            Text = "KiWeave - Discord connection"; Icon = Program.AppIcon(); StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 250); MinimumSize = new Size(560, 250); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink;
            var root = UiStyle.Stack(); root.Padding = new Padding(24); Controls.Add(root); root.Controls.Add(UiStyle.Text("Discord connection", 18, true)); root.Controls.Add(UiStyle.Text("Authorizes once, then reuses an encrypted refresh token for this Windows account. Access tokens stay in memory.", 9, false));
            status.Text = DiscordIntegration.Status; status.AutoSize = true; status.ForeColor = UiStyle.Muted; status.Margin = new Padding(0, 18, 0, 18); root.Controls.Add(status);
            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
            connect = UiStyle.Button("Connect Discord", delegate { Connect(owner); }, true); buttons.Controls.Add(connect);
            forget = UiStyle.Button("Forget authorization", delegate { DiscordIntegration.ForgetAuthorization(); status.Text = DiscordIntegration.Status; forget.Enabled = DiscordIntegration.HasAuthorization; }, false); forget.Enabled = DiscordIntegration.HasAuthorization; buttons.Controls.Add(forget);
            buttons.Controls.Add(UiStyle.Button("Discord setup guide", delegate { DocumentationForm.OpenDiscordSetupGuide(this); }, false));
            root.Controls.Add(buttons); root.Controls.Add(UiStyle.Text("Authorization is encrypted for this Windows account and reused across KiWeave launches. Beta voice control may remain unavailable until the KiWeave Discord app is public/approved.", 8.5f, false));
        }
        async void Connect(IWin32Window owner)
        {
            try
            {
                connect.Enabled = false;
                bool ok = await DiscordIntegration.ConnectInteractive(owner, PostStatus, connectionLifetime.Token);
                if (IsDisposed || Disposing) return;
                status.Text = ok ? DiscordIntegration.Status : "Not connected";
                forget.Enabled = DiscordIntegration.HasAuthorization;
            }
            catch (OperationCanceledException error) { if (!connectionLifetime.IsCancellationRequested) ShowConnectionError(error); }
            catch (Exception error) { ShowConnectionError(error); }
            finally { if (!IsDisposed && !Disposing) connect.Enabled = true; }
        }
        void ShowConnectionError(Exception error)
        {
            if (IsDisposed || Disposing) return;
            status.Text = error.Message;
            MessageBox.Show(this, error.Message, "Discord connection", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        void PostStatus(string text)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)(() => { if (!IsDisposed && !Disposing) status.Text = text; }));
            }
            catch (InvalidOperationException) { }
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            connectionLifetime.Cancel();
            connectionLifetime.Dispose();
            base.OnFormClosed(e);
        }
    }
#endif
}
