using System.Net;
using System.Text;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// Load...() with URLs (like Processing) and the background Request...() versions:
/// progress, the disk cache, and falling back to the cache when the network fails.
/// A tiny HttpListener on localhost plays the web server.
/// </summary>
public class WebAndDataTests : IDisposable
{
    private sealed class Bare : Sketch
    {
        public override void Draw() { }
    }

    private HttpListener _server = new();
    private readonly string _base;
    private int _hits;
    private volatile bool _down;
    private readonly string _cacheBefore = Web.CacheFolder;
    private readonly string _cache = Path.Combine(Path.GetTempPath(), $"dana_webcache_{Guid.NewGuid():N}");

    public WebAndDataTests()
    {
        Web.CacheFolder = _cache;
        // A free port: try a few until one binds.
        for (int attempt = 0; ; attempt++)
        {
            int port = 20000 + Random.Shared.Next(30000);
            try
            {
                _server.Prefixes.Clear();
                _server.Prefixes.Add($"http://127.0.0.1:{port}/");
                _server.Start();
                _base = $"http://127.0.0.1:{port}/";
                break;
            }
            catch (HttpListenerException) when (attempt < 20)
            {
                // A port in use leaves the listener disposed: start over with a fresh one.
                _server = new HttpListener();
            }
        }
        Task.Run(Serve);
    }

    private async Task Serve()
    {
        while (_server.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _server.GetContextAsync(); } catch { return; }
            Interlocked.Increment(ref _hits);
            var path = ctx.Request.Url!.AbsolutePath;
            byte[] body;
            if (_down) { ctx.Response.StatusCode = 503; body = Array.Empty<byte>(); }
            else if (path == "/data.json") body = Encoding.UTF8.GetBytes("{\"name\":\"Dana\",\"items\":[1,2,3]}");
            else if (path == "/lines.txt") body = Encoding.UTF8.GetBytes("﻿one\r\ntwo\nthree\n");
            else if (path == "/table.csv") body = Encoding.UTF8.GetBytes("city,people\nHavana,2100000\nMadrid,3300000\n");
            else if (path == "/big.bin") body = new byte[300_000];
            else if (path == "/geo.json") body = Encoding.UTF8.GetBytes(GeoTests.SampleCollection);
            else { ctx.Response.StatusCode = 404; body = Array.Empty<byte>(); }
            ctx.Response.ContentLength64 = body.Length;
            await ctx.Response.OutputStream.WriteAsync(body);
            ctx.Response.Close();
        }
    }

    public void Dispose()
    {
        _server.Stop();
        Web.CacheFolder = _cacheBefore;
        if (Directory.Exists(_cache)) Directory.Delete(_cache, true);
    }

    private static T Wait<T>(Request<T> r)
    {
        var until = DateTime.Now.AddSeconds(10);
        while (!r.Done && DateTime.Now < until) Thread.Sleep(10);
        Assert.True(r.Done, "the request never finished");
        return r.Ready ? r.Value : default!;
    }

    [Fact]
    public void Load_functions_take_urls_like_processing()
    {
        var s = new Bare();
        Assert.Equal("Dana", s.LoadJSONObject(_base + "data.json").GetString("name"));
        Assert.Equal(new[] { "one", "two", "three" }, s.LoadStrings(_base + "lines.txt"));   // BOM and \r\n handled
        var table = s.LoadTable(_base + "table.csv", "header");
        Assert.Equal(2, table.RowCount);
        Assert.Equal("Madrid", table.GetRow(1).GetString("city"));
        Assert.Equal(300_000, s.LoadBytes(_base + "big.bin").Length);
    }

    [Fact]
    public void Requests_return_at_once_and_report_progress_and_status()
    {
        var s = new Bare();
        var r = s.RequestBytes(_base + "big.bin");
        Assert.False(r.Ready);                        // returned before the download finished
        var bytes = Wait(r);
        Assert.Equal(300_000, bytes.Length);
        Assert.Equal(1, r.Progress, 3);
        Assert.Equal(300_000, r.TotalBytes);
        Assert.Equal("loaded", r.Status);

        var json = Wait(s.RequestJSONObject(_base + "data.json"));
        Assert.Equal(3, json.GetJSONArray("items").Size());
    }

    [Fact]
    public void A_missing_page_fails_with_a_readable_error_and_Value_says_so()
    {
        var r = new Bare().RequestText(_base + "nope");
        Wait(r);
        Assert.True(r.Failed);
        Assert.Contains("404", r.Error);
        var ex = Assert.Throws<InvalidOperationException>(() => r.Value);
        Assert.Contains("nope", ex.Message);
    }

    [Fact]
    public void The_cache_saves_downloads_and_covers_for_the_network_going_down()
    {
        var s = new Bare();
        var first = s.RequestText(_base + "data.json", cacheFor: TimeSpan.FromHours(1));
        Wait(first);
        Assert.False(first.FromCache);
        int hits = _hits;

        var second = s.RequestText(_base + "data.json", cacheFor: TimeSpan.FromHours(1));
        Assert.Equal(first.Value, Wait(second));
        Assert.True(second.FromCache);
        Assert.Equal(hits, _hits);                    // no request at all

        // Expired cache + server down: the old copy is used, and marked stale.
        _down = true;
        var third = s.RequestText(_base + "data.json", cacheFor: TimeSpan.Zero);
        Assert.Equal(first.Value, Wait(third));
        Assert.True(third.Stale);
        Assert.Contains("older copy", third.Status);
    }

    [Fact]
    public void Files_work_with_Request_too()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dana_req_{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "a\nb");
        try { Assert.Equal(new[] { "a", "b" }, Wait(new Bare().RequestStrings(path))); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void GeoJSON_comes_from_urls_too()
    {
        var map = Wait(new Bare().RequestGeoJSON(_base + "geo.json"));
        Assert.Equal(3, map.Count);
    }

    [Fact]
    public void A_json_object_lists_its_keys_like_processing()
    {
        var o = JSONObject.Parse("{\"payments\": 400, \"db\": 120}");
        Assert.Equal(new[] { "payments", "db" }, o.Keys());
        Assert.Equal(2, o.Size());
        Assert.Empty(new JSONObject().Keys());
    }
}
