using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace DanaProcessing
{
    /// <summary>
    /// Something being loaded in the background by one of the sketch's Request...()
    /// methods (RequestJSONObject(url), RequestStrings(url), RequestGeoJSON(url)...).
    /// The call returns at once; check <see cref="Ready"/> in Draw() and use
    /// <see cref="Value"/> once it's true. Draw() never waits for the network.
    /// <code>
    /// Request&lt;JSONObject&gt; quakes;
    /// public override void Setup() =&gt; quakes = RequestJSONObject("https://...", cacheFor: TimeSpan.FromMinutes(5));
    /// public override void Draw()
    /// {
    ///     if (!quakes.Ready) { Text($"loading {quakes.Progress:P0}", 20, 20); return; }
    ///     var data = quakes.Value;
    /// }
    /// </code>
    /// </summary>
    public sealed class Request<T>
    {
        private T _value;
        private volatile bool _ready, _failed;

        internal Request(string source) => Source = source;

        /// <summary>The URL or path being loaded.</summary>
        public string Source { get; }

        /// <summary>True once <see cref="Value"/> is there to use.</summary>
        public bool Ready => _ready;

        /// <summary>True if it couldn't be loaded (and there was no cached copy to fall back on); <see cref="Error"/> says why.</summary>
        public bool Failed => _failed;

        /// <summary>True when it's finished either way: loaded or failed.</summary>
        public bool Done => _ready || _failed;

        /// <summary>The loaded value. Throws if it isn't <see cref="Ready"/> yet.</summary>
        public T Value => _ready ? _value : throw new InvalidOperationException(_failed
            ? $"No se pudo cargar '{Source}': {Error}"
            : $"'{Source}' todavía se está cargando: mirá Ready antes de usar Value.");

        /// <summary>0..1 while downloading, or -1 when the server doesn't say how big it is (then use <see cref="BytesRead"/>).</summary>
        public float Progress { get; internal set; } = -1;

        /// <summary>Bytes received so far.</summary>
        public long BytesRead { get; internal set; }

        /// <summary>Total size, when the server says it.</summary>
        public long? TotalBytes { get; internal set; }

        /// <summary>True when the value came from the disk cache instead of the network.</summary>
        public bool FromCache { get; internal set; }

        /// <summary>True when the download failed and an older cached copy was used instead.</summary>
        public bool Stale { get; internal set; }

        /// <summary>Why it failed (null otherwise).</summary>
        public string Error { get; internal set; }

        /// <summary>A short line for an on-screen status: "loading 40%", "212 KB", "cached", "failed: ...".</summary>
        public string Status =>
            _failed ? "failed: " + Error
            : _ready ? (Stale ? "offline: using an older copy" : FromCache ? "from cache" : "loaded")
            : Progress >= 0 ? $"loading {Progress * 100:0}%"
            : BytesRead > 0 ? $"loading {BytesRead / 1024:N0} KB" : "connecting...";

        internal void Complete(T value)
        {
            _value = value;
            Progress = 1;
            _ready = true;
        }

        internal void Fail(string error)
        {
            Error = error;
            _failed = true;
        }
    }

    /// <summary>
    /// Downloads for the sketch API: one shared HttpClient, a disk cache, and
    /// progress reporting. Paths that aren't URLs are just read from disk.
    /// </summary>
    internal static class Web
    {
        private static readonly Lazy<HttpClient> SharedClient = new(() =>
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DanaProcessing/1.0 (+https://github.com/BuddhaCodes/danaprocessing)");
            return http;
        });

        /// <summary>Where cached downloads live (one file per URL).</summary>
        internal static string CacheFolder { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DanaProcessing", "web-cache");

        public static bool IsUrl(string path) =>
            path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        internal static string CachePath(string url)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..24];
            string ext = Path.GetExtension(new Uri(url).AbsolutePath);
            if (ext.Length > 8) ext = "";
            return Path.Combine(CacheFolder, hash + ext);
        }

        /// <summary>Blocking download (for LoadStrings(url) and friends, like Processing).</summary>
        public static byte[] Get(string url) => GetAsync<byte[]>(url, null, null, CancellationToken.None).GetAwaiter().GetResult();

        /// <summary>
        /// Downloads (or reads from the cache when it's younger than <paramref name="cacheFor"/>),
        /// reporting progress into <paramref name="request"/>. If the download fails and any cached
        /// copy exists, that copy is used and the request is marked Stale.
        /// </summary>
        public static async Task<byte[]> GetAsync<T>(string url, TimeSpan? cacheFor, Request<T> request, CancellationToken ct)
        {
            string cache = cacheFor.HasValue ? CachePath(url) : null;
            if (cache != null && File.Exists(cache) && DateTime.UtcNow - File.GetLastWriteTimeUtc(cache) < cacheFor.Value)
            {
                if (request != null) request.FromCache = true;
                return await File.ReadAllBytesAsync(cache, ct).ConfigureAwait(false);
            }
            try
            {
                using var response = await SharedClient.Value.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                long? total = response.Content.Headers.ContentLength;
                if (request != null) request.TotalBytes = total;
                await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                var ms = new MemoryStream(total is > 0 and < int.MaxValue ? (int)total.Value : 64 * 1024);
                var buffer = new byte[64 * 1024];
                int read;
                while ((read = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    ms.Write(buffer, 0, read);
                    if (request != null)
                    {
                        request.BytesRead = ms.Length;
                        request.Progress = total > 0 ? Math.Min(1, ms.Length / (float)total.Value) : -1;
                    }
                }
                var bytes = ms.ToArray();
                if (cache != null)
                {
                    try
                    {
                        Directory.CreateDirectory(CacheFolder);
                        await File.WriteAllBytesAsync(cache, bytes, ct).ConfigureAwait(false);
                    }
                    catch (IOException) { }   // a cache we can't write is just a slower next run
                }
                return bytes;
            }
            catch (Exception) when (cache != null && File.Exists(cache) && !ct.IsCancellationRequested)
            {
                if (request != null) { request.FromCache = true; request.Stale = true; }
                return await File.ReadAllBytesAsync(cache, ct).ConfigureAwait(false);
            }
        }
    }

    public abstract partial class Sketch
    {
        // =====================================================================
        // The web. Every Load...() accepts a URL as well as a path, like in
        // Processing (it blocks until the download finishes, so do it in
        // Setup()). The Request...() versions load in the background and
        // return at once -- check .Ready in Draw() -- with an optional disk
        // cache (cacheFor) so the next run doesn't download again.
        // =====================================================================

        /// <summary>Bytes from a file or a URL.</summary>
        private byte[] ReadBytes(string pathOrUrl) =>
            Web.IsUrl(pathOrUrl) ? Web.Get(pathOrUrl) : File.ReadAllBytes(ResolveInputPath(pathOrUrl));

        private string ReadText(string pathOrUrl) => DecodeText(ReadBytes(pathOrUrl));

        /// <summary>UTF-8 (BOM or not), the encoding of practically every API and text file around.</summary>
        private static string DecodeText(byte[] bytes)
        {
            var text = Encoding.UTF8.GetString(bytes);
            return text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
        }

        private static string[] SplitLines(string text)
        {
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            return lines.Length > 0 && lines[^1].Length == 0 ? lines[..^1] : lines;
        }

        private Request<T> StartRequest<T>(string pathOrUrl, TimeSpan? cacheFor, Func<byte[], T> parse)
        {
            var request = new Request<T>(pathOrUrl);
            string local = Web.IsUrl(pathOrUrl) ? null : ResolveInputPath(pathOrUrl);
            Task.Run(async () =>
            {
                try
                {
                    byte[] bytes = local != null
                        ? await File.ReadAllBytesAsync(local).ConfigureAwait(false)
                        : await Web.GetAsync(pathOrUrl, cacheFor, request, CancellationToken.None).ConfigureAwait(false);
                    request.BytesRead = bytes.Length;
                    request.Complete(parse(bytes));
                }
                catch (Exception ex)
                {
                    request.Fail(ex is HttpRequestException h && h.StatusCode != null ? $"HTTP {(int)h.StatusCode.Value} {h.StatusCode}" : ex.Message);
                    DanaLogger.Warn($"Request('{pathOrUrl}') falló: {ex.Message}");
                }
            });
            return request;
        }

        /// <summary>Loads text in the background: from a URL (optionally cached on disk for <paramref name="cacheFor"/>) or a file.</summary>
        public Request<string> RequestText(string pathOrUrl, TimeSpan? cacheFor = null) => StartRequest(pathOrUrl, cacheFor, DecodeText);

        /// <summary>Loads lines of text in the background, like loadStrings() without the wait.</summary>
        public Request<string[]> RequestStrings(string pathOrUrl, TimeSpan? cacheFor = null) => StartRequest(pathOrUrl, cacheFor, b => SplitLines(DecodeText(b)));

        /// <summary>Loads raw bytes in the background, like loadBytes() without the wait.</summary>
        public Request<byte[]> RequestBytes(string pathOrUrl, TimeSpan? cacheFor = null) => StartRequest(pathOrUrl, cacheFor, b => b);

        /// <summary>Loads a JSON object in the background (an API response, usually), like loadJSONObject() without the wait.</summary>
        public Request<JSONObject> RequestJSONObject(string pathOrUrl, TimeSpan? cacheFor = null) => StartRequest(pathOrUrl, cacheFor, b => JSONObject.Parse(DecodeText(b)));

        /// <summary>Loads a JSON array in the background, like loadJSONArray() without the wait.</summary>
        public Request<JSONArray> RequestJSONArray(string pathOrUrl, TimeSpan? cacheFor = null) => StartRequest(pathOrUrl, cacheFor, b => JSONArray.Parse(DecodeText(b)));

        /// <summary>Loads XML (or RSS) in the background, like loadXML() without the wait.</summary>
        public Request<XML> RequestXML(string pathOrUrl, TimeSpan? cacheFor = null) => StartRequest(pathOrUrl, cacheFor, b => XML.Parse(DecodeText(b)));

        /// <summary>Loads a CSV table in the background, like loadTable() without the wait.</summary>
        public Request<Table> RequestTable(string pathOrUrl, string options = "", TimeSpan? cacheFor = null) => StartRequest(pathOrUrl, cacheFor, b => Table.ParseCsv(DecodeText(b), options));

        /// <summary>Loads an image in the background (decoded off the main thread too), with progress and an optional cache.</summary>
        public Request<PImage> RequestPImage(string pathOrUrl, TimeSpan? cacheFor = null) => StartRequest(pathOrUrl, cacheFor, DecodeImage);

        /// <summary>Loads GeoJSON (countries, earthquakes, routes...) in the background. See <see cref="GeoMap"/>.</summary>
        public Request<GeoMap> RequestGeoJSON(string pathOrUrl, TimeSpan? cacheFor = null) => StartRequest(pathOrUrl, cacheFor, b => GeoMap.Parse(DecodeText(b)));

        /// <summary>Loads GeoJSON from a file or URL (blocking: use it in Setup(), or RequestGeoJSON() to keep drawing). See <see cref="GeoMap"/>.</summary>
        public GeoMap LoadGeoJSON(string pathOrUrl) => GeoMap.Parse(ReadText(pathOrUrl));

        private static PImage DecodeImage(byte[] bytes)
        {
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            var bitmap = codec == null ? null : SKBitmap.Decode(codec);
            if (bitmap == null)
                throw new InvalidOperationException($"No se pudo decodificar la imagen ({bytes.Length} bytes): formato no reconocido.");
            return new PImage(bitmap);
        }
    }
}
