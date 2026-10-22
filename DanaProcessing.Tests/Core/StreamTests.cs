using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// Live feeds: ConnectWebSocket, ListenEvents (SSE), ListenWebhook and ConnectSignalR, each
/// against a small real server on localhost.
/// </summary>
public class StreamTests
{
    private sealed class Host : Sketch
    {
        public override void Draw() { }
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    /// <summary>Waits (up to 10 s) until the condition holds.</summary>
    private static void Eventually(Func<bool> condition, string what)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < until)
        {
            if (condition()) return;
            Thread.Sleep(20);
        }
        Assert.Fail("timed out waiting for " + what);
    }

    /// <summary>A WebSocket server: each connection is handed to <paramref name="session"/>.</summary>
    private static (string Url, HttpListener Listener) WebSocketServer(Func<WebSocket, int, Task> session)
    {
        int port = FreePort();
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        int n = 0;
        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); } catch { return; }
                if (!ctx.Request.IsWebSocketRequest) { ctx.Response.StatusCode = 400; ctx.Response.Close(); continue; }
                var ws = (await ctx.AcceptWebSocketAsync(null)).WebSocket;
                int index = n++;
                _ = Task.Run(() => session(ws, index));
            }
        });
        return ($"ws://localhost:{port}/", listener);
    }

    private static async Task<string> Receive(WebSocket ws)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();
        while (true)
        {
            var r = await ws.ReceiveAsync(buffer, CancellationToken.None);
            if (r.MessageType == WebSocketMessageType.Close) return null;
            sb.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
            if (r.EndOfMessage) return sb.ToString();
        }
    }

    private static Task Send(WebSocket ws, string text) =>
        ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);

    [Fact]
    public void A_websocket_receives_what_the_server_sends_and_sends_back()
    {
        var (url, listener) = WebSocketServer(async (ws, _) =>
        {
            await Send(ws, "hola");
            while (ws.State == WebSocketState.Open)
            {
                var msg = await Receive(ws);
                if (msg == null) break;
                await Send(ws, "echo:" + msg);
            }
        });
        using var server = listener;
        var feed = new Host().ConnectWebSocket(url);
        feed.Send("ping 1");                          // queued before it's even connected
        Eventually(() => feed.Pending >= 2, "two messages");
        Assert.True(feed.Connected);
        Assert.True(feed.Next(out var first));
        Assert.Equal("hola", first);
        Assert.True(feed.Next(out var second));
        Assert.Equal("echo:ping 1", second);
        Assert.False(feed.Next(out _));
        Assert.Equal(2, feed.Received);
        feed.Close();
        Assert.Equal(FeedState.Closed, feed.State);
    }

    [Fact]
    public void A_websocket_reconnects_when_the_server_drops_it()
    {
        var (url, listener) = WebSocketServer(async (ws, index) =>
        {
            await Send(ws, "connection " + index);
            if (index == 0)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            else
                await Receive(ws);
        });
        using var server = listener;
        var feed = new Host().ConnectWebSocket(url);
        Eventually(() => feed.Received >= 2, "a message from the second connection");
        var all = feed.ReadAll();
        Assert.Equal(new[] { "connection 0", "connection 1" }, all);
        feed.Close();
    }

    [Fact]
    public void Without_reconnect_a_refused_websocket_fails_with_its_error()
    {
        var feed = new Host().ConnectWebSocket($"ws://localhost:{FreePort()}/", reconnect: false);
        Eventually(() => feed.State == FeedState.Failed, "the failure");
        Assert.NotNull(feed.Error);
        Assert.StartsWith("failed", feed.Status);
    }

    [Fact]
    public void A_firehose_never_keeps_more_than_its_capacity()
    {
        var (url, listener) = WebSocketServer(async (ws, _) =>
        {
            for (int i = 0; i < 500; i++) await Send(ws, i.ToString());
            await Receive(ws);
        });
        using var __ = listener;
        var feed = new Host().ConnectWebSocket(url);
        feed.Capacity = 100;
        Eventually(() => feed.Received == 500, "500 messages");
        Assert.Equal(100, feed.Pending);
        Assert.Equal(400, feed.Dropped);
        Assert.True(feed.Next(out var oldest));
        Assert.Equal("400", oldest);                  // the oldest were the ones dropped
        feed.Close();
    }

    [Fact]
    public void Server_events_are_parsed_like_a_browser_does()
    {
        int port = FreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        string lastEventIdSeen = null;
        int connections = 0;
        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); } catch { return; }
                int n = connections++;
                if (n > 0) lastEventIdSeen = ctx.Request.Headers["Last-Event-ID"];
                ctx.Response.ContentType = "text/event-stream";
                ctx.Response.SendChunked = true;
                string body = n == 0
                    ? ": a comment, ignored\n\n" +
                      "data: plain\n\n" +
                      "event: tick\nid: 7\ndata: {\"n\": 1}\n\n" +
                      "data: line one\ndata:line two\n\n" +
                      "retry: 50\n\n"
                    : "event: again\ndata: back\n\n";
                var bytes = Encoding.UTF8.GetBytes(body);
                await ctx.Response.OutputStream.WriteAsync(bytes);
                await ctx.Response.OutputStream.FlushAsync();
                ctx.Response.Close();                 // ends the stream: the feed reconnects
            }
        });
        var feed = new Host().ListenEvents($"http://localhost:{port}/");
        Eventually(() => feed.Received >= 4, "four events");
        var events = feed.ReadAll();
        Assert.Equal("message", events[0].Type);
        Assert.Equal("plain", events[0].Data);
        Assert.Equal("tick", events[1].Type);
        Assert.Equal("7", events[1].Id);
        Assert.Equal(1, events[1].JSON().GetInt("n"));
        Assert.Equal("line one\nline two", events[2].Data);
        Assert.Equal("again", events[3].Type);
        Assert.Equal("7", lastEventIdSeen);            // it told the server where it left off
        feed.Close();
    }

    [Fact]
    public void A_webhook_turns_each_request_into_a_call_and_answers_ok()
    {
        int port = FreePort();
        var feed = new Host().ListenWebhook(port, "/github");
        Assert.True(feed.Connected, feed.Error);
        using var http = new HttpClient();
        var content = new StringContent("{\"action\":\"starred\",\"repo\":\"dana\"}", Encoding.UTF8, "application/json");
        content.Headers.Add("X-Event", "star");
        var response = http.PostAsync($"http://localhost:{port}/github/?from=test", content).Result;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Eventually(() => feed.Pending == 1, "the call");
        Assert.True(feed.Next(out var call));
        Assert.Equal("POST", call.Method);
        Assert.Equal("/github/", call.Path);
        Assert.Equal("from=test", call.Query);
        Assert.Equal("star", call.Headers["x-event"]);
        Assert.Equal("starred", call.JSON().GetString("action"));
        feed.Close();
        // Closing it stops its web server: the port is free again.
        using var again = new HttpListener();
        again.Prefixes.Add($"http://localhost:{port}/github/");
        again.Start();
    }

    [Fact]
    public void Closing_the_sketch_closes_its_feeds()
    {
        int port = FreePort();
        var sketch = new Host();
        var hook = sketch.ListenWebhook(port);
        var socket = sketch.ConnectWebSocket($"ws://localhost:{FreePort()}/");
        sketch.RaiseStopped();
        Assert.Equal(FeedState.Closed, hook.State);
        Eventually(() => socket.State == FeedState.Closed, "the socket to close");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        Assert.ThrowsAny<Exception>(() => http.GetAsync($"http://localhost:{port}/").Result);
    }

    [Fact]
    public void SignalR_handshakes_receives_invocations_and_invokes_the_hub()
    {
        const char RS = '\u001e';
        string handshake = null;
        var fromClient = new List<string>();
        var (url, listener) = WebSocketServer(async (ws, _) =>
        {
            handshake = await Receive(ws);
            await Send(ws, "{}" + RS);
            // A ping and two invocations in one frame, then one split across two frames.
            await Send(ws, "{\"type\":6}" + RS + "{\"type\":1,\"target\":\"span\",\"arguments\":[{\"service\":\"cart\",\"ms\":12.5}]}" + RS +
                           "{\"type\":1,\"target\":\"metrics\",\"arguments\":[\"gc\",3]}" + RS);
            await Send(ws, "{\"type\":1,\"target\":\"sp");
            await Send(ws, "lit\",\"arguments\":[]}" + RS);
            while (ws.State == WebSocketState.Open)
            {
                var msg = await Receive(ws);
                if (msg == null) break;
                lock (fromClient) fromClient.Add(msg);
            }
        });
        using var __ = listener;
        var hub = new Host().ConnectSignalR(url.Replace("ws://", "http://") + "hub");
        Eventually(() => hub.Received >= 3, "three invocations");
        Assert.Equal("{\"protocol\":\"json\",\"version\":1}" + RS, handshake);
        Assert.True(hub.Connected);
        var msgs = hub.ReadAll();
        Assert.Equal("span", msgs[0].Target);
        Assert.Equal("cart", msgs[0].Object(0).GetString("service"));
        Assert.Equal(12.5f, msgs[0].Object(0).GetFloat("ms"));
        Assert.Equal("metrics", msgs[1].Target);
        Assert.Equal("gc", msgs[1].Text(0));
        Assert.Equal(3, msgs[1].Number(1));
        Assert.Equal("split", msgs[2].Target);

        hub.Invoke("Chaos", "payments", 0.25);
        Eventually(() => { lock (fromClient) return fromClient.Count > 0; }, "the invocation at the hub");
        string sent;
        lock (fromClient) sent = fromClient[0];
        Assert.EndsWith(RS.ToString(), sent);
        var json = JSONObject.Parse(sent.TrimEnd(RS));
        Assert.Equal(1, json.GetInt("type"));
        Assert.Equal("Chaos", json.GetString("target"));
        Assert.Equal("payments", json.GetJSONArray("arguments").GetString(0));
        Assert.Equal(0.25f, json.GetJSONArray("arguments").GetFloat(1));
        hub.Close();
    }

    [Fact]
    public void A_hub_that_refuses_the_handshake_fails_with_its_reason()
    {
        var (url, listener) = WebSocketServer(async (ws, _) =>
        {
            await Receive(ws);
            await Send(ws, "{\"error\":\"Requested protocol 'json' is not available.\"}\u001e");
            await Receive(ws);
        });
        using var __ = listener;
        var hub = new Host().ConnectSignalR(url.Replace("ws://", "http://"), reconnect: false);
        Eventually(() => hub.State == FeedState.Failed, "the failure");
        Assert.Contains("not available", hub.Error);
        hub.Close();
    }
}
