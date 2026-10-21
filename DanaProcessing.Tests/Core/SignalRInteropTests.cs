using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// ConnectSignalR against a real ASP.NET Core SignalR hub (not a hand-made fake): the
/// handshake, messages from the hub, calls to the hub, and server events from .NET 10's
/// TypedResults.ServerSentEvents.
/// </summary>
public class SignalRInteropTests
{
    public sealed class EchoHub : Hub
    {
        public override Task OnConnectedAsync() => Clients.Caller.SendAsync("welcome", new { name = "dana", n = 42 });
        public Task Shout(string text, int times) => Clients.All.SendAsync("shouted", string.Concat(Enumerable.Repeat(text, times)));
    }

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

    private static void Eventually(Func<bool> condition, string what)
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < until)
        {
            if (condition()) return;
            Thread.Sleep(20);
        }
        Assert.Fail("timed out waiting for " + what);
    }

    private static async Task<WebApplication> StartServer(int port)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR();
        var app = builder.Build();
        app.MapHub<EchoHub>("/hub");
        app.MapGet("/events", () => TypedResults.ServerSentEvents(Ticks()));
        await app.StartAsync();
        return app;
    }

    private static async IAsyncEnumerable<System.Net.ServerSentEvents.SseItem<string>> Ticks()
    {
        for (int i = 0; i < 3; i++)
        {
            yield return new System.Net.ServerSentEvents.SseItem<string>($"{{\"i\":{i}}}", "tick");
            await Task.Delay(10);
        }
    }

    [Fact]
    public void Talks_to_a_real_SignalR_hub_both_ways()
    {
        int port = FreePort();
        var app = StartServer(port).GetAwaiter().GetResult();
        try
        {
            var hub = new Host().ConnectSignalR($"http://localhost:{port}/hub");
            Eventually(() => hub.Received >= 1, "the welcome");
            Assert.True(hub.Next(out var welcome));
            Assert.Equal("welcome", welcome.Target);
            Assert.Equal("dana", welcome.Object(0).GetString("name"));
            Assert.Equal(42, welcome.Object(0).GetInt("n"));

            hub.Invoke("Shout", "ho", 3);
            Eventually(() => hub.Pending >= 1, "the shout back");
            Assert.True(hub.Next(out var shouted));
            Assert.Equal("shouted", shouted.Target);
            Assert.Equal("hohoho", shouted.Text(0));
            hub.Close();
        }
        finally { app.StopAsync().GetAwaiter().GetResult(); }
    }

    [Fact]
    public void Reads_dotnet_10_server_sent_events()
    {
        int port = FreePort();
        var app = StartServer(port).GetAwaiter().GetResult();
        try
        {
            var feed = new Host().ListenEvents($"http://localhost:{port}/events", reconnect: false);
            Eventually(() => feed.Received >= 3, "three ticks");
            var ticks = feed.ReadAll();
            Assert.All(ticks, t => Assert.Equal("tick", t.Type));
            Assert.Equal(new[] { 0, 1, 2 }, ticks.Select(t => t.JSON().GetInt("i")));
            feed.Close();
        }
        finally { app.StopAsync().GetAwaiter().GetResult(); }
    }
}
