using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DanaProcessing
{
    /// <summary>Where a <see cref="LiveFeed{T}"/> is: connecting, open, waiting to reconnect, closed by you, or given up.</summary>
    public enum FeedState { Connecting, Open, Reconnecting, Closed, Failed }

    /// <summary>
    /// Messages arriving in the background -- from a WebSocket, a stream of server events, a
    /// webhook, a SignalR hub. The connection lives on its own thread and never makes Draw()
    /// wait: messages pile up in a queue and Draw() takes them when it's ready.
    /// <code>
    /// LiveFeed&lt;string&gt; trades;
    /// public override void Setup() =&gt; trades = ConnectWebSocket("wss://...");
    /// public override void Draw()
    /// {
    ///     while (trades.Next(out var msg)) { var t = JSONObject.Parse(msg); ... }
    ///     Text($"{trades.Status} · {trades.PerSecond:0} msg/s", 20, 20);
    /// }
    /// </code>
    /// It reconnects by itself when the connection drops (waiting a little longer each time),
    /// and closes when the sketch stops. A firehose can't eat the memory: past
    /// <see cref="Capacity"/> waiting messages the oldest are dropped (and counted).
    /// </summary>
    public class LiveFeed<T>
    {
        private readonly ConcurrentQueue<T> _queue = new();
        private long _received, _dropped;
        private long _windowStart = Environment.TickCount64;
        private int _windowCount;
        private volatile string _error;
        private volatile int _state = (int)FeedState.Connecting;
        internal readonly CancellationTokenSource Cancel = new();

        internal LiveFeed(string source) => Source = source;

        /// <summary>The URL (or the local address, for a webhook) this feed listens to.</summary>
        public string Source { get; }

        public FeedState State { get => (FeedState)_state; internal set => _state = (int)value; }

        /// <summary>True while the connection is open (a webhook: while it's listening).</summary>
        public bool Connected => State == FeedState.Open;

        /// <summary>The last thing that went wrong (kept while it reconnects), or null.</summary>
        public string Error { get => _error; internal set => _error = value; }

        /// <summary>A short line for the screen: "open · 12 msg/s", "reconnecting: ...".</summary>
        public string Status => State switch
        {
            FeedState.Open => $"open · {PerSecond:0.#} msg/s",
            FeedState.Connecting => "connecting...",
            FeedState.Reconnecting => "reconnecting" + (Error != null ? ": " + Error : "..."),
            FeedState.Closed => "closed",
            _ => "failed" + (Error != null ? ": " + Error : ""),
        };

        /// <summary>Messages received since it started.</summary>
        public long Received => Interlocked.Read(ref _received);

        /// <summary>Messages thrown away because more than <see cref="Capacity"/> were waiting.</summary>
        public long Dropped => Interlocked.Read(ref _dropped);

        /// <summary>Messages waiting to be read.</summary>
        public int Pending => _queue.Count;

        /// <summary>How many messages may wait before the oldest are dropped (10,000).</summary>
        public int Capacity { get; set; } = 10_000;

        private float _rate;
        private long _lastPush;

        /// <summary>Messages per second, over the last second or so.</summary>
        public float PerSecond => Environment.TickCount64 - Interlocked.Read(ref _lastPush) > 2000 ? 0 : _rate;

        /// <summary>Takes the oldest waiting message; false when there's none.</summary>
        public bool Next(out T message) => _queue.TryDequeue(out message);

        /// <summary>Takes every waiting message (at most <paramref name="max"/>), oldest first.</summary>
        public List<T> ReadAll(int max = int.MaxValue)
        {
            var list = new List<T>();
            while (list.Count < max && _queue.TryDequeue(out var m)) list.Add(m);
            return list;
        }

        /// <summary>Closes the connection for good (the sketch stopping does it too).</summary>
        public void Close()
        {
            State = FeedState.Closed;
            try { Cancel.Cancel(); } catch (ObjectDisposedException) { }
        }

        internal void Push(T message)
        {
            _queue.Enqueue(message);
            Interlocked.Increment(ref _received);
            while (_queue.Count > Capacity && _queue.TryDequeue(out _))
                Interlocked.Increment(ref _dropped);
            long now = Environment.TickCount64;
            Interlocked.Exchange(ref _lastPush, now);
            lock (_queue)
            {
                _windowCount++;
                long span = now - _windowStart;
                if (span >= 1000)
                {
                    _rate = _windowCount * 1000f / span;
                    _windowCount = 0;
                    _windowStart = now;
                }
            }
        }
    }

    /// <summary>A WebSocket as a <see cref="LiveFeed{T}"/> of text messages, that you can also <see cref="Send"/> to.</summary>
    public sealed class WebSocketFeed : LiveFeed<string>
    {
        internal readonly ConcurrentQueue<string> Outgoing = new();
        internal readonly SemaphoreSlim OutgoingSignal = new(0);

        internal WebSocketFeed(string url) : base(url) { }

        /// <summary>Sends a text message. Sent at once when connected; otherwise as soon as it is.</summary>
        public void Send(string text)
        {
            if (Outgoing.Count > 10_000) return;
            Outgoing.Enqueue(text);
            OutgoingSignal.Release();
        }

        /// <summary>Sends a JSON object.</summary>
        public void Send(JSONObject json) => Send(json.ToString());
    }

    /// <summary>One event from a server event stream (SSE, "text/event-stream").</summary>
    public sealed class ServerEvent
    {
        /// <summary>The event's type ("message" when the server didn't name one).</summary>
        public string Type { get; internal set; } = "message";

        /// <summary>Its data: the "data:" lines, joined by line breaks.</summary>
        public string Data { get; internal set; } = "";

        /// <summary>Its id, if the server gave one.</summary>
        public string Id { get; internal set; }

        /// <summary>The data read as a JSON object.</summary>
        public JSONObject JSON() => JSONObject.Parse(Data);

        public override string ToString() => $"{Type}: {Data}";
    }

    /// <summary>One request that reached a webhook (see ListenWebhook).</summary>
    public sealed class WebhookCall
    {
        public string Method { get; internal set; } = "";
        public string Path { get; internal set; } = "";
        public string Query { get; internal set; } = "";
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string Body { get; internal set; } = "";
        public DateTime Time { get; internal set; }

        /// <summary>The body read as a JSON object (most webhooks send one).</summary>
        public JSONObject JSON() => JSONObject.Parse(Body);

        public override string ToString() => $"{Method} {Path} ({Body.Length} bytes)";
    }

    /// <summary>A message from a SignalR hub: the hub called <see cref="Target"/> with <see cref="Arguments"/>.</summary>
    public sealed class HubMessage
    {
        /// <summary>The client method the hub called (Clients.All.SendAsync("Target", ...)).</summary>
        public string Target { get; internal set; } = "";

        /// <summary>The arguments it sent, as JSON.</summary>
        public JSONArray Arguments { get; internal set; } = new JSONArray();

        /// <summary>Argument <paramref name="i"/> as a JSON object.</summary>
        public JSONObject Object(int i) => Arguments.GetJSONObject(i);

        /// <summary>Argument <paramref name="i"/> as text.</summary>
        public string Text(int i) => Arguments.GetString(i);

        /// <summary>Argument <paramref name="i"/> as a number.</summary>
        public float Number(int i) => Arguments.GetFloat(i);

        public override string ToString() => $"{Target}({Arguments})";
    }

    /// <summary>
    /// A SignalR hub as a <see cref="LiveFeed{T}"/>: what the hub sends arrives as
    /// <see cref="HubMessage"/>s, and <see cref="Invoke"/> calls the hub's methods. Speaks
    /// SignalR's JSON protocol over a WebSocket directly -- no packages needed.
    /// </summary>
    public sealed class SignalRFeed : LiveFeed<HubMessage>
    {
        internal readonly WebSocketFeed Socket;

        internal SignalRFeed(string url, WebSocketFeed socket) : base(url) => Socket = socket;

        /// <summary>Calls a method on the hub (fire and forget), like connection.SendAsync(method, args...).</summary>
        public void Invoke(string method, params object[] args)
        {
            var msg = new JsonObject
            {
                ["type"] = 1,
                ["target"] = method,
                ["arguments"] = JsonSerializer.SerializeToNode(args ?? Array.Empty<object>()),
            };
            Socket.Send(msg.ToJsonString() + SignalRProtocol.Separator);
        }
    }

    internal static class SignalRProtocol
    {
        public const char Separator = '\u001e';
        public const string Handshake = "{\"protocol\":\"json\",\"version\":1}\u001e";
        public const string Ping = "{\"type\":6}\u001e";
    }

    public abstract partial class Sketch
    {
        private readonly List<Action> _feedClosers = new();

        private T Track<T>(T feed, Action close)
        {
            lock (_feedClosers) _feedClosers.Add(close);
            return feed;
        }

        /// <summary>Closes every feed the sketch opened: runs when it stops.</summary>
        internal void CloseFeeds()
        {
            Action[] closers;
            lock (_feedClosers) { closers = _feedClosers.ToArray(); _feedClosers.Clear(); }
            foreach (var c in closers) { try { c(); } catch { } }
        }

        // =====================================================================
        // WebSocket
        // =====================================================================

        /// <summary>
        /// Opens a WebSocket (ws:// or wss://) in the background. Read what arrives with
        /// feed.Next(out msg) in Draw(), send with feed.Send(text). Reconnects by itself when
        /// it drops, unless <paramref name="reconnect"/> is false.
        /// </summary>
        public WebSocketFeed ConnectWebSocket(string url, bool reconnect = true)
        {
            var feed = new WebSocketFeed(url);
            Task.Run(() => Streams.RunWebSocket(feed, new Uri(url), reconnect, onOpen: null, onText: feed.Push));
            return Track(feed, feed.Close);
        }

        // =====================================================================
        // Server events (SSE)
        // =====================================================================

        /// <summary>
        /// Listens to a stream of server events ("text/event-stream", SSE): Wikipedia's live
        /// edits, a .NET API's TypedResults.ServerSentEvents, Firebase... Each event arrives as
        /// a <see cref="ServerEvent"/> (its type, data and id). Reconnects by itself, telling
        /// the server the last event id it saw.
        /// </summary>
        public LiveFeed<ServerEvent> ListenEvents(string url, bool reconnect = true)
        {
            var feed = new LiveFeed<ServerEvent>(url);
            Task.Run(() => Streams.RunEvents(feed, url, reconnect));
            return Track(feed, feed.Close);
        }

        // =====================================================================
        // Webhooks
        // =====================================================================

        /// <summary>
        /// Starts a small web server on this computer, at http://localhost:<paramref name="port"/><paramref name="path"/>,
        /// and turns every request it gets into a <see cref="WebhookCall"/>: point a service's
        /// webhook at it (from the internet, through a tunnel like smee.io or ngrok). Answers
        /// each call with 200 OK.
        /// </summary>
        public LiveFeed<WebhookCall> ListenWebhook(int port, string path = "/")
        {
            if (!path.StartsWith('/')) path = "/" + path;
            if (!path.EndsWith('/')) path += "/";
            var feed = new LiveFeed<WebhookCall>($"http://localhost:{port}{path}");
            var listener = new HttpListener();
            listener.Prefixes.Add(feed.Source);
            try
            {
                listener.Start();
                feed.State = FeedState.Open;
            }
            catch (Exception ex)
            {
                feed.Error = ex.Message;
                feed.State = FeedState.Failed;
                DanaLogger.Warn($"ListenWebhook({port}) falló: {ex.Message}");
                return feed;
            }
            Task.Run(() => Streams.RunWebhook(feed, listener));
            return Track(feed, () => { feed.Close(); try { listener.Stop(); listener.Close(); } catch { } });
        }

        // =====================================================================
        // SignalR
        // =====================================================================

        /// <summary>
        /// Connects to an ASP.NET Core SignalR hub (http(s)://host/hub). Every hub call to the
        /// clients arrives as a <see cref="HubMessage"/> -- its Target and Arguments -- and
        /// feed.Invoke("Method", args...) calls the hub. JSON protocol over WebSockets, keeps
        /// itself alive with pings and reconnects by itself.
        /// </summary>
        public SignalRFeed ConnectSignalR(string hubUrl, bool reconnect = true)
        {
            var ws = new UriBuilder(hubUrl);
            ws.Scheme = ws.Scheme == "https" ? "wss" : ws.Scheme == "http" ? "ws" : ws.Scheme;
            if ((ws.Scheme == "wss" && ws.Port == 443) || (ws.Scheme == "ws" && ws.Port == 80)) ws.Port = -1;
            var socket = new WebSocketFeed(ws.Uri.ToString());
            var feed = new SignalRFeed(hubUrl, socket);
            Task.Run(() => Streams.RunSignalR(feed, ws.Uri, reconnect));
            return Track(feed, () => { feed.Close(); socket.Close(); });
        }
    }

    /// <summary>The background loops behind the feeds.</summary>
    internal static class Streams
    {
        private static readonly Lazy<HttpClient> EventsClient = new(() =>
        {
            var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DanaProcessing/1.0 (+https://github.com/BuddhaCodes/danaprocessing)");
            return http;
        });

        /// <summary>The wait before reconnecting: 0.5 s, then doubling up to 10 s.</summary>
        internal static TimeSpan Backoff(int attempt) => TimeSpan.FromMilliseconds(Math.Min(10_000, 500 * Math.Pow(2, Math.Max(0, attempt))));

        /// <summary>
        /// Runs a WebSocket until the feed closes: connect, read text messages into onText, send
        /// what's queued; on a drop, wait and connect again (when allowed).
        /// </summary>
        internal static async Task RunWebSocket(WebSocketFeed feed, Uri uri, bool reconnect, Func<ClientWebSocket, CancellationToken, Task> onOpen, Action<string> onText,
                                                Func<CancellationToken, Task> keepAlive = null)
        {
            var ct = feed.Cancel.Token;
            int attempt = 0;
            while (!ct.IsCancellationRequested)
            {
                using var socket = new ClientWebSocket();
                socket.Options.SetRequestHeader("User-Agent", "DanaProcessing/1.0");
                using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
                try
                {
                    feed.State = attempt == 0 ? FeedState.Connecting : FeedState.Reconnecting;
                    await socket.ConnectAsync(uri, ct).ConfigureAwait(false);
                    feed.State = FeedState.Open;
                    feed.Error = null;
                    attempt = 0;
                    // Anything the protocol must say first (SignalR's handshake) goes before the queue.
                    if (onOpen != null) await onOpen(socket, ct).ConfigureAwait(false);
                    var sender = SendLoop(feed, socket, session.Token);
                    var pinger = keepAlive?.Invoke(session.Token);
                    await ReceiveLoop(socket, onText, ct).ConfigureAwait(false);
                    session.Cancel();
                    try { await sender.ConfigureAwait(false); } catch (OperationCanceledException) { }
                    if (pinger != null) try { await pinger.ConfigureAwait(false); } catch (OperationCanceledException) { }
                    if (!ct.IsCancellationRequested) feed.Error = "the server closed the connection";
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    session.Cancel();
                    feed.Error = ex.Message;
                }
                if (ct.IsCancellationRequested) break;
                if (!reconnect) { feed.State = FeedState.Failed; return; }
                feed.State = FeedState.Reconnecting;
                try { await Task.Delay(Backoff(attempt++), ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            }
            if (feed.State != FeedState.Failed) feed.State = FeedState.Closed;
        }

        private static async Task ReceiveLoop(ClientWebSocket socket, Action<string> onText, CancellationToken ct)
        {
            var buffer = new byte[64 * 1024];
            var message = new MemoryStream();
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false); } catch { }
                    return;
                }
                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage) continue;
                onText(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
                message.SetLength(0);
            }
        }

        private static async Task SendLoop(WebSocketFeed feed, ClientWebSocket socket, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await feed.OutgoingSignal.WaitAsync(ct).ConfigureAwait(false);
                while (socket.State == WebSocketState.Open && feed.Outgoing.TryPeek(out var text))
                {
                    await socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
                    feed.Outgoing.TryDequeue(out _);
                }
            }
        }

        // ---- SignalR -------------------------------------------------------------------------

        internal static Task RunSignalR(SignalRFeed feed, Uri uri, bool reconnect)
        {
            var socket = feed.Socket;
            var pending = new StringBuilder();
            bool handshaken = false;
            // The two feeds share one life: closing the hub closes the socket.
            feed.Cancel.Token.Register(() => socket.Close());

            Task OnOpen(ClientWebSocket ws, CancellationToken ct)
            {
                handshaken = false;
                pending.Clear();
                return ws.SendAsync(Encoding.UTF8.GetBytes(SignalRProtocol.Handshake), WebSocketMessageType.Text, true, ct);
            }

            void OnText(string text)
            {
                pending.Append(text);
                int cut;
                while ((cut = IndexOf(pending, SignalRProtocol.Separator)) >= 0)
                {
                    string record = pending.ToString(0, cut);
                    pending.Remove(0, cut + 1);
                    if (!handshaken)
                    {
                        handshaken = true;
                        var hs = JsonNode.Parse(record) as JsonObject;
                        if (hs != null && hs["error"] is JsonNode err) { feed.Error = err.ToString(); feed.State = FeedState.Failed; socket.Close(); return; }
                        else { feed.State = FeedState.Open; feed.Error = null; }
                        continue;
                    }
                    Record(feed, record);
                }
            }

            async Task KeepAlive(CancellationToken ct)
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
                    socket.Send(SignalRProtocol.Ping);
                }
            }

            return RunSignalRLoop(feed, socket, uri, reconnect, OnOpen, OnText, KeepAlive);
        }

        private static async Task RunSignalRLoop(SignalRFeed feed, WebSocketFeed socket, Uri uri, bool reconnect,
            Func<ClientWebSocket, CancellationToken, Task> onOpen, Action<string> onText, Func<CancellationToken, Task> keepAlive)
        {
            // Mirror the socket's state into the hub feed while it runs.
            var mirror = Task.Run(async () =>
            {
                while (!feed.Cancel.IsCancellationRequested)
                {
                    if (socket.State != FeedState.Open && feed.State == FeedState.Open) feed.State = FeedState.Reconnecting;
                    if (socket.Error != null && feed.State != FeedState.Open) feed.Error = socket.Error;
                    try { await Task.Delay(100, feed.Cancel.Token).ConfigureAwait(false); } catch (OperationCanceledException) { }
                }
            });
            await RunWebSocket(socket, uri, reconnect, onOpen, onText, keepAlive).ConfigureAwait(false);
            if (socket.State == FeedState.Failed && feed.State != FeedState.Closed) feed.State = FeedState.Failed;
            else if (feed.State != FeedState.Failed) feed.State = FeedState.Closed;
        }

        private static int IndexOf(StringBuilder sb, char c)
        {
            for (int i = 0; i < sb.Length; i++) if (sb[i] == c) return i;
            return -1;
        }

        private static void Record(SignalRFeed feed, string record)
        {
            if (string.IsNullOrWhiteSpace(record)) return;
            if (JsonNode.Parse(record) is not JsonObject msg) return;
            int type = msg["type"]?.GetValue<int>() ?? 0;
            switch (type)
            {
                case 1:   // invocation: the hub called a client method
                    feed.Push(new HubMessage
                    {
                        Target = msg["target"]?.GetValue<string>() ?? "",
                        Arguments = JSONArray.Parse(msg["arguments"]?.ToJsonString() ?? "[]"),
                    });
                    break;
                case 7:   // close: the server is ending the connection
                    feed.Error = msg["error"]?.GetValue<string>() ?? "the hub closed the connection";
                    break;
                // 6 ping, 3 completion, 2 stream item: nothing to show.
            }
        }

        // ---- server events --------------------------------------------------------------------

        internal static async Task RunEvents(LiveFeed<ServerEvent> feed, string url, bool reconnect)
        {
            var ct = feed.Cancel.Token;
            int attempt = 0;
            string lastId = null;
            TimeSpan? retry = null;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    feed.State = attempt == 0 ? FeedState.Connecting : FeedState.Reconnecting;
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Accept.ParseAdd("text/event-stream");
                    request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                    if (lastId != null) request.Headers.TryAddWithoutValidation("Last-Event-ID", lastId);
                    using var response = await EventsClient.Value.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    feed.State = FeedState.Open;
                    feed.Error = null;
                    attempt = 0;
                    using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), Encoding.UTF8);
                    var data = new StringBuilder();
                    string type = null, id = null;
                    bool hasData = false;
                    while (!ct.IsCancellationRequested)
                    {
                        string line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                        if (line == null) break;                                   // the server ended the stream
                        if (line.Length == 0)
                        {
                            // A blank line dispatches the event gathered so far.
                            if (id != null) lastId = id;
                            if (hasData)
                                feed.Push(new ServerEvent { Type = string.IsNullOrEmpty(type) ? "message" : type, Data = data.ToString(), Id = id ?? lastId });
                            data.Clear(); type = null; id = null; hasData = false;
                            continue;
                        }
                        if (line[0] == ':') continue;                              // a comment (servers use them as heartbeats)
                        int colon = line.IndexOf(':');
                        string field = colon < 0 ? line : line[..colon];
                        string value = colon < 0 ? "" : line[(colon + 1)..];
                        if (value.StartsWith(' ')) value = value[1..];
                        switch (field)
                        {
                            case "data":
                                if (hasData) data.Append('\n');
                                data.Append(value);
                                hasData = true;
                                break;
                            case "event": type = value; break;
                            case "id": if (!value.Contains('\0')) id = value; break;
                            case "retry": if (int.TryParse(value, out int ms)) retry = TimeSpan.FromMilliseconds(ms); break;
                        }
                    }
                    if (!ct.IsCancellationRequested) feed.Error = "the server ended the stream";
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    feed.Error = ex is HttpRequestException h && h.StatusCode != null ? $"HTTP {(int)h.StatusCode.Value} {h.StatusCode}" : ex.Message;
                }
                if (ct.IsCancellationRequested) break;
                if (!reconnect) { feed.State = FeedState.Failed; return; }
                feed.State = FeedState.Reconnecting;
                try { await Task.Delay(retry ?? Backoff(attempt++), ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            }
            if (feed.State != FeedState.Failed) feed.State = FeedState.Closed;
        }

        // ---- webhooks -------------------------------------------------------------------------

        internal static async Task RunWebhook(LiveFeed<WebhookCall> feed, HttpListener listener)
        {
            var ct = feed.Cancel.Token;
            while (!ct.IsCancellationRequested && listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync().ConfigureAwait(false); }
                catch { break; }                                                   // stopped
                try
                {
                    var req = ctx.Request;
                    var call = new WebhookCall
                    {
                        Method = req.HttpMethod,
                        Path = req.Url?.AbsolutePath ?? "",
                        Query = req.Url?.Query.TrimStart('?') ?? "",
                        Time = DateTime.Now,
                    };
                    foreach (string key in req.Headers.AllKeys)
                        if (key != null) call.Headers[key] = req.Headers[key];
                    using (var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
                        call.Body = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                    feed.Push(call);
                    var ok = Encoding.UTF8.GetBytes("ok");
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/plain";
                    ctx.Response.ContentLength64 = ok.Length;
                    await ctx.Response.OutputStream.WriteAsync(ok, ct).ConfigureAwait(false);
                    ctx.Response.Close();
                }
                catch (Exception ex)
                {
                    feed.Error = ex.Message;
                    try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
                }
            }
            if (feed.State != FeedState.Failed) feed.State = FeedState.Closed;
        }
    }
}
