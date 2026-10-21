// PulseShop: a tiny online store whose heartbeat you can watch.
//
// Five services -- gateway, catalog, cart, inventory, payments -- plus a database, all in
// one ASP.NET Core app but calling each other over real HTTP on Kestrel, so every hop is a
// real request with a real W3C trace. A built-in load generator plays the customers.
//
// The app watches itself with nothing but what .NET ships:
// - an ActivityListener sees every request in and out (ASP.NET Core's and HttpClient's
//   own Activities) and turns them into hops: who called whom, how long it took, how it
//   ended;
// - the runtime: GC collections per generation, heap, allocation rate, thread pool queue,
//   threads, CPU.
// and streams it all live:
// - SignalR hub at /pulse -- "hops", "metrics", "chaos" -- which also takes commands;
// - server-sent events at /live (TypedResults.ServerSentEvents, new in .NET 10);
// - a chaos panel at / for a phone: add latency, make a service fail, starve the thread
//   pool with sync-over-async, storm the GC, spike the load.
//
// Run:  dotnet run   (then open http://localhost:5199 and the Dana sketch "Inside Kestrel")

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
string url = builder.Configuration["urls"] ?? "http://localhost:5199";
builder.WebHost.UseUrls(url);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddSignalR();
builder.Services.AddSingleton<Chaos>();
builder.Services.AddSingleton<Telemetry>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Telemetry>());
builder.Services.AddHostedService<Customers>();
builder.Services.AddHttpClient("mesh", c => c.BaseAddress = new Uri(url));
builder.Services.AddHttpClient("internet", c => c.BaseAddress = new Uri(url));
builder.Services.ConfigureHttpClientDefaults(h => h.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { MaxConnectionsPerServer = 512 }));

var app = builder.Build();

// Every request is tagged with the service it belongs to: the first part of its path.
app.Use(async (ctx, next) =>
{
    string service = Mesh.ServiceOf(ctx.Request.Path);
    var span = Activity.Current;
    span?.SetTag(Telemetry.ServiceTag, service);
    var chaos = ctx.RequestServices.GetRequiredService<Chaos>();
    try
    {
        if (Mesh.Services.Contains(service))
        {
            await chaos.Delay(service, ctx.RequestAborted);
            if (chaos.ShouldFail(service))
            {
                ctx.Response.StatusCode = 503;
                await ctx.Response.WriteAsync($"{service} is having a bad day");
                return;
            }
        }
        await next();
    }
    catch (Exception) when (!ctx.Response.HasStarted)
    {
        ctx.Response.StatusCode = 500;                         // a call further down failed: so does this one
    }
    finally
    {
        span?.SetTag(Telemetry.StatusTag, ctx.Response.StatusCode);
        span?.SetTag(Telemetry.RouteTag, (ctx.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? ctx.Request.Path.Value);
    }
});

app.MapHub<PulseHub>("/pulse");

// ---- the services -------------------------------------------------------------------------
app.MapGet("/gateway/browse", async (IHttpClientFactory f, CancellationToken ct) =>
{
    var mesh = f.CreateClient("mesh");
    var products = await mesh.GetFromJsonAsync<Product[]>("/catalog/products", ct) ?? [];
    var pick = products[Random.Shared.Next(products.Length)];
    var stock = await mesh.GetFromJsonAsync<int>($"/inventory/stock/{pick.Id}", ct);
    return Results.Ok(new { pick.Name, pick.Price, stock });
});

app.MapPost("/gateway/cart", async (IHttpClientFactory f, CancellationToken ct) =>
{
    var mesh = f.CreateClient("mesh");
    int id = Random.Shared.Next(1, 40);
    (await mesh.GetAsync($"/catalog/products/{id}", ct)).EnsureSuccessStatusCode();
    (await mesh.PostAsync($"/inventory/reserve/{id}", null, ct)).EnsureSuccessStatusCode();
    (await mesh.PostAsync($"/cart/items/{id}", null, ct)).EnsureSuccessStatusCode();
    return Results.Ok();
});

app.MapPost("/gateway/checkout", async (IHttpClientFactory f, CancellationToken ct) =>
{
    var mesh = f.CreateClient("mesh");
    var total = await mesh.GetFromJsonAsync<decimal>("/cart/total", ct);
    (await mesh.PostAsync($"/payments/charge?amount={total}", null, ct)).EnsureSuccessStatusCode();
    (await mesh.PostAsync("/inventory/commit", null, ct)).EnsureSuccessStatusCode();
    (await mesh.DeleteAsync("/cart/items", ct)).EnsureSuccessStatusCode();
    return Results.Ok(new { total });
});

app.MapGet("/catalog/products", async (Chaos c) => { await Db.Query("select * from products", 4, c); return Catalog.Products; });
app.MapGet("/catalog/products/{id:int}", async (int id, Chaos c) => { await Db.Query("select product", 2, c); return Catalog.Products[id % Catalog.Products.Length]; });

app.MapGet("/inventory/stock/{id:int}", async (int id, Chaos c) =>
{
    if (c.Starve) Db.QueryBlocking("select stock", 300);  // sync over async: holds a thread pool thread for 300 ms
    else await Db.Query("select stock", 2, c);
    return 10 + id % 7;
});
app.MapPost("/inventory/reserve/{id:int}", async (int id, Chaos c) => { await Db.Query("update stock", 5, c); return Results.Ok(); });
app.MapPost("/inventory/commit", async (Chaos c) => { await Db.Query("commit reservation", 6, c); return Results.Ok(); });

app.MapPost("/cart/items/{id:int}", async (int id, Chaos c) => { await Db.Query("insert cart item", 3, c); return Results.Ok(); });
app.MapGet("/cart/total", async (Chaos c) => { await Db.Query("sum cart", 3, c); return Math.Round(20 + Random.Shared.NextDouble() * 200, 2); });
app.MapDelete("/cart/items", async (Chaos c) => { await Db.Query("delete cart", 2, c); return Results.Ok(); });

app.MapPost("/payments/charge", async (decimal amount, Chaos c) =>
{
    await Task.Delay(Random.Shared.Next(15, 45));          // the card network is far away
    await Db.Query("insert payment", 4, c);
    return Results.Ok(new { amount, approved = true });
});

// ---- the heartbeat, as server-sent events ---------------------------------------------------
app.MapGet("/live", (Telemetry t, CancellationToken ct) => TypedResults.ServerSentEvents(t.Subscribe(ct)));

// ---- the chaos panel (and its endpoints, for anything that isn't SignalR) -------------------
app.MapGet("/", () => Results.Content(Panel.Html, "text/html"));
app.MapPost("/chaos/{what}", (string what, string? service, double? value, Chaos chaos, IHubContext<PulseHub> hub) =>
{
    chaos.Apply(what, service, value ?? 0);
    return hub.Clients.All.SendAsync("chaos", chaos.State());
});

app.Run();

// =============================================================================================
record Product(int Id, string Name, decimal Price);

static class Catalog
{
    public static readonly Product[] Products = Enumerable.Range(0, 40)
        .Select(i => new Product(i, $"Thing #{i}", Math.Round(5 + i * 3.7m, 2))).ToArray();
}

static class Mesh
{
    public static readonly string[] Services = ["gateway", "catalog", "cart", "inventory", "payments"];

    public static string ServiceOf(PathString path)
    {
        var p = path.Value ?? "";
        int end = p.IndexOf('/', 1);
        string first = end > 0 ? p[1..end] : p.TrimStart('/');
        return Services.Contains(first) ? first : first == "" ? "panel" : first;
    }
}

/// <summary>The database: a little latency and contention, each query its own Activity.</summary>
static class Db
{
    public static readonly ActivitySource Source = new("PulseShop.Db");
    static readonly SemaphoreSlim Connections = new(32);           // a connection pool

    public static async Task Query(string sql, int ms, Chaos chaos)
    {
        await Connections.WaitAsync();
        try
        {
            using var span = Source.StartActivity("db " + sql, ActivityKind.Client);
            span?.SetTag(Telemetry.ServiceTag, "db");
            await Task.Delay(Math.Max(1, ms + Random.Shared.Next(0, ms) + chaos.LatencyOf("db")));
            if (chaos.ShouldFail("db")) { span?.SetStatus(ActivityStatusCode.Error); throw new InvalidOperationException("deadlock victim"); }
            if (chaos.GcStorm) Garbage();
        }
        finally { Connections.Release(); }
    }

    public static void QueryBlocking(string sql, int ms)
    {
        using var span = Source.StartActivity("db " + sql + " (blocking)", ActivityKind.Client);
        span?.SetTag(Telemetry.ServiceTag, "db");
        Task.Delay(ms).Wait();                                     // the classic mistake
    }

    /// <summary>Lots of short-lived objects and a few big ones: the GC has to work.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Garbage()
    {
        for (int i = 0; i < 200; i++) _ = new byte[1024 * (1 + i % 8)];
        if (Random.Shared.Next(20) == 0) _ = new byte[200_000];     // straight to the large object heap
    }
}

/// <summary>What the chaos panel and the hub can break, and how.</summary>
sealed class Chaos
{
    readonly ConcurrentDictionary<string, int> _latency = new();
    readonly ConcurrentDictionary<string, double> _failure = new();
    public volatile int Load = 40;                                 // customer requests per second
    public volatile bool Starve, GcStorm;
    long _spikeUntil;

    public int EffectiveLoad => Environment.TickCount64 < Interlocked.Read(ref _spikeUntil) ? Load * 8 : Load;
    public int LatencyOf(string s) => _latency.GetValueOrDefault(s);
    public bool ShouldFail(string s) => Random.Shared.NextDouble() < _failure.GetValueOrDefault(s);

    public Task Delay(string service, CancellationToken ct)
    {
        int ms = LatencyOf(service);
        return ms > 0 ? Task.Delay(ms + Random.Shared.Next(0, ms / 2 + 1), ct) : Task.CompletedTask;
    }

    public void Apply(string what, string? service, double value)
    {
        switch (what.ToLowerInvariant())
        {
            case "load": Load = Math.Clamp((int)value, 0, 2000); break;
            case "latency": if (service != null) _latency[service] = Math.Clamp((int)value, 0, 5000); break;
            case "fail": if (service != null) _failure[service] = Math.Clamp(value, 0, 1); break;
            case "starve": Starve = value > 0; break;
            case "gc": GcStorm = value > 0; break;
            case "spike": Interlocked.Exchange(ref _spikeUntil, Environment.TickCount64 + (long)(Math.Clamp(value, 1, 60) * 1000)); break;
            case "reset": _latency.Clear(); _failure.Clear(); Starve = GcStorm = false; Load = 40; Interlocked.Exchange(ref _spikeUntil, 0); break;
        }
    }

    public object State() => new
    {
        load = Load,
        spike = EffectiveLoad != Load,
        starve = Starve,
        gcStorm = GcStorm,
        latency = _latency.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value),
        failure = _failure.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value),
    };
}

/// <summary>The hub: streams the heartbeat out, takes chaos in.</summary>
sealed class PulseHub(Chaos chaos) : Hub
{
    public override Task OnConnectedAsync() => Clients.Caller.SendAsync("chaos", chaos.State());
    public Task SetLoad(int rps) => Do("load", null, rps);
    public Task Latency(string service, int ms) => Do("latency", service, ms);
    public Task Fail(string service, double rate) => Do("fail", service, rate);
    public Task Starve(bool on) => Do("starve", null, on ? 1 : 0);
    public Task GcStorm(bool on) => Do("gc", null, on ? 1 : 0);
    public Task Spike(int seconds) => Do("spike", null, seconds);
    public Task Reset() => Do("reset", null, 0);

    Task Do(string what, string? service, double value)
    {
        chaos.Apply(what, service, value);
        return Clients.All.SendAsync("chaos", chaos.State());
    }
}

/// <summary>The customers: a steady stream of browsing, carts and checkouts against the gateway.</summary>
sealed class Customers(IHttpClientFactory factory, Chaos chaos) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(1500, ct);                                // let Kestrel start
        var http = factory.CreateClient("internet");
        var inFlight = new SemaphoreSlim(400);
        double budget = 0;
        var clock = Stopwatch.StartNew();
        double last = 0;
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(10, ct);
            double now = clock.Elapsed.TotalSeconds;
            budget += (now - last) * chaos.EffectiveLoad;
            last = now;
            while (budget >= 1)
            {
                budget--;
                if (!inFlight.Wait(0)) break;                      // too many waiting: the customers give up
                double r = Random.Shared.NextDouble();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var _ = r < 0.6 ? await http.GetAsync("/gateway/browse", ct)
                                     : r < 0.85 ? await http.PostAsync("/gateway/cart", null, ct)
                                     : await http.PostAsync("/gateway/checkout", null, ct);
                    }
                    catch { }
                    finally { inFlight.Release(); }
                }, ct);
            }
        }
    }
}

/// <summary>
/// Watches the app's own Activities and the runtime, and sends what it sees to the hub (and
/// to every /live listener) a few times a second.
/// </summary>
sealed class Telemetry(IHubContext<PulseHub> hub, Chaos chaos) : BackgroundService
{
    public const string ServiceTag = "pulse.service", StatusTag = "pulse.status", RouteTag = "pulse.route";

    /// <summary>One hop: <c>From</c> called <c>To</c>; how long it took and how it ended.</summary>
    public record Hop(string Trace, string From, string To, string Route, double Ms, int Status, bool Root);

    readonly ConcurrentQueue<Hop> _hops = new();
    readonly ConcurrentDictionary<string, string> _serviceOfSpan = new();     // server span id -> its service
    readonly ConcurrentDictionary<string, string> _callerOfClientSpan = new(); // client span id -> the service that made it
    readonly List<Channel<SseItem<string>>> _listeners = [];
    readonly List<double> _latencies = [];
    ActivityListener? _listener;

    public async IAsyncEnumerable<SseItem<string>> Subscribe([EnumeratorCancellation] CancellationToken ct)
    {
        var ch = Channel.CreateBounded<SseItem<string>>(new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (_listeners) _listeners.Add(ch);
        try
        {
            await foreach (var item in ch.Reader.ReadAllAsync(ct)) yield return item;
        }
        finally { lock (_listeners) _listeners.Remove(ch); }
    }

    public override Task StartAsync(CancellationToken ct)
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name is "Microsoft.AspNetCore" or "System.Net.Http" or "PulseShop.Db",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = Started,
            ActivityStopped = Stopped,
        };
        ActivitySource.AddActivityListener(_listener);
        return base.StartAsync(ct);
    }

    void Started(Activity a)
    {
        // An outgoing HTTP call: remember which service made it (its parent is that service's request).
        if (a.Kind == ActivityKind.Client && a.Source.Name == "System.Net.Http")
            _callerOfClientSpan[a.SpanId.ToString()] = a.Parent?.GetTagItem(ServiceTag) as string ?? "internet";
    }

    void Stopped(Activity a)
    {
        if (a.Source.Name == "Microsoft.AspNetCore" && a.Kind == ActivityKind.Server)
        {
            string service = a.GetTagItem(ServiceTag) as string ?? "?";
            if (!Mesh.Services.Contains(service)) return;          // the hub, the panel: not the shop
            string parent = a.ParentSpanId.ToString();
            string from = _callerOfClientSpan.TryRemove(parent, out var caller) ? caller : "internet";
            int status = a.GetTagItem(StatusTag) is int sc ? sc : 200;
            string route = a.GetTagItem(RouteTag) as string ?? a.DisplayName;
            _hops.Enqueue(new Hop(a.TraceId.ToString(), from, service, route, a.Duration.TotalMilliseconds, status, from == "internet"));
        }
        else if (a.Source.Name == "PulseShop.Db")
        {
            string from = a.Parent?.GetTagItem(ServiceTag) as string ?? "?";
            _hops.Enqueue(new Hop(a.TraceId.ToString(), from, "db", a.DisplayName, a.Duration.TotalMilliseconds,
                                  a.Status == ActivityStatusCode.Error ? 500 : 200, false));
        }
        else if (a.Source.Name == "System.Net.Http")
            _callerOfClientSpan.TryRemove(a.SpanId.ToString(), out _);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var proc = Process.GetCurrentProcess();
        var cpuClock = Stopwatch.StartNew();
        TimeSpan lastCpu = proc.TotalProcessorTime;
        long lastAlloc = GC.GetTotalAllocatedBytes();
        int roots = 0, errors = 0, tick = 0;
        var window = new Queue<(double T, int Roots, int Errors)>();
        var clock = Stopwatch.StartNew();
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        while (await timer.WaitForNextTickAsync(ct))
        {
            // Hops: every 100 ms, a batch.
            var batch = new List<Hop>();
            while (batch.Count < 2000 && _hops.TryDequeue(out var h))
            {
                batch.Add(h);
                if (h.Root)
                {
                    roots++;
                    if (h.Status >= 500) errors++;
                    lock (_latencies) { _latencies.Add(h.Ms); if (_latencies.Count > 2000) _latencies.RemoveRange(0, 1000); }
                }
            }
            if (batch.Count > 0) await Send("hops", batch, ct);

            // Metrics: 4 times a second.
            if (++tick % 3 != 0) continue;
            double now = clock.Elapsed.TotalSeconds;
            window.Enqueue((now, roots, errors));
            while (window.Count > 1 && now - window.Peek().T > 2) window.Dequeue();
            var (t0, r0, e0) = window.Peek();
            double span = Math.Max(0.25, now - t0);
            double[] lat;
            lock (_latencies) { lat = _latencies.TakeLast(400).Order().ToArray(); }
            proc.Refresh();
            var cpu = proc.TotalProcessorTime;
            double cpuPct = (cpu - lastCpu).TotalMilliseconds / Math.Max(1, cpuClock.Elapsed.TotalMilliseconds) / Environment.ProcessorCount * 100;
            lastCpu = cpu; cpuClock.Restart();
            long alloc = GC.GetTotalAllocatedBytes();
            double allocMBps = (alloc - lastAlloc) / 1048576.0 / 0.3;
            lastAlloc = alloc;
            ThreadPool.GetAvailableThreads(out int freeWorkers, out _);
            ThreadPool.GetMaxThreads(out int maxWorkers, out _);
            await Send("metrics", new
            {
                rps = (roots - r0) / span,
                errorRate = roots - r0 > 0 ? (errors - e0) / (double)(roots - r0) : 0,
                p50 = Percentile(lat, 0.5),
                p99 = Percentile(lat, 0.99),
                gen0 = GC.CollectionCount(0),
                gen1 = GC.CollectionCount(1),
                gen2 = GC.CollectionCount(2),
                heapMB = GC.GetTotalMemory(false) / 1048576.0,
                allocMBps,
                threadPoolQueue = ThreadPool.PendingWorkItemCount,
                threads = ThreadPool.ThreadCount,
                busyThreads = maxWorkers - freeWorkers,
                cpu = cpuPct,
                load = chaos.EffectiveLoad,
            }, ct);
        }
    }

    static double Percentile(double[] sorted, double p) => sorted.Length == 0 ? 0 : sorted[Math.Min(sorted.Length - 1, (int)(p * sorted.Length))];

    async Task Send(string what, object payload, CancellationToken ct)
    {
        await hub.Clients.All.SendAsync(what, payload, ct);
        var item = new SseItem<string>(JsonSerializer.Serialize(payload, JsonSerializerOptions.Web), what);
        lock (_listeners) foreach (var l in _listeners) l.Writer.TryWrite(item);
    }

    public override void Dispose()
    {
        _listener?.Dispose();
        base.Dispose();
    }
}

static class Panel
{
    public const string Html = """
<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>PulseShop chaos</title>
<style>
 body{font-family:system-ui,sans-serif;background:#0b1020;color:#e8ecff;margin:0;padding:16px;max-width:520px}
 h1{font-size:22px;margin:4px 0 2px} p{color:#9aa6c8;margin:0 0 14px}
 .g{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-bottom:14px}
 button{font:600 15px system-ui;padding:14px;border:0;border-radius:12px;background:#1d2747;color:#e8ecff}
 button.hot{background:#c0392b} button.warn{background:#d97a1c} button.ok{background:#1f8a5b}
 h2{font-size:14px;color:#9aa6c8;margin:10px 0 6px;text-transform:uppercase;letter-spacing:.08em}
</style></head><body>
<h1>Break PulseShop</h1><p>Every tap shows up live in Dana.</p>
<h2>Load</h2><div class="g">
 <button onclick="c('load',null,20)">20 req/s</button><button onclick="c('load',null,120)">120 req/s</button>
 <button class="warn" onclick="c('spike',null,8)">Spike ×8 (8 s)</button><button onclick="c('load',null,400)">400 req/s</button></div>
<h2>Slow down</h2><div class="g">
 <button onclick="c('latency','payments',400)">Payments +400 ms</button><button onclick="c('latency','db',120)">Database +120 ms</button>
 <button onclick="c('latency','inventory',250)">Inventory +250 ms</button><button onclick="c('latency','catalog',150)">Catalog +150 ms</button></div>
<h2>Break</h2><div class="g">
 <button class="hot" onclick="c('fail','payments',0.3)">Payments fail 30%</button><button class="hot" onclick="c('fail','db',0.1)">DB deadlocks 10%</button>
 <button class="hot" onclick="c('starve',null,1)">Starve the thread pool</button><button class="hot" onclick="c('gc',null,1)">GC storm</button></div>
<div class="g"><button class="ok" onclick="c('reset',null,0)">Everything back to normal</button></div>
<script>function c(w,s,v){fetch('/chaos/'+w+'?'+(s?'service='+s+'&':'')+'value='+v,{method:'POST'})}</script>
</body></html>
""";
}
