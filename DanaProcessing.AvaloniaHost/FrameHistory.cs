using System;
using System.Collections.Generic;
using System.Threading;
using SkiaSharp;

namespace DanaProcessing.AvaloniaHost
{
    /// <summary>
    /// The last seconds of a running sketch, frame by frame -- what the
    /// canvas's rewind (Freeze/StepBack/ScrubTo) shows. Frames are stored at the
    /// sketch's LOGICAL size (supersampling already averaged away).
    ///
    /// Two tiers, so rewind reaches back about 10 seconds instead of about one:
    /// the newest frames stay as they are (<see cref="RawBudgetBytes"/>, so
    /// stepping around "just now" is instant and exact), and as they age a
    /// background thread compresses them -- JPEG, or PNG when the frame has
    /// transparency -- into <see cref="CompressedBudgetBytes"/>, up to
    /// <see cref="MaxFrames"/> in all. A 720x520 frame goes from ~1.5 MB to
    /// ~0.1-0.2 MB. When the computer can't compress as fast as the sketch
    /// draws (big sketches, slow machines), the older part keeps every other
    /// frame or so instead of falling behind: the reach in seconds stays, the
    /// old part is just less smooth. Frames can also carry a SketchState --
    /// the sketch's fields at that frame -- which is what lets the canvas
    /// continue running from a rewound frame (AvaloniaSketchCanvas.ResumeFromHere).
    ///
    /// Threading: Push/Get are called from the render thread, the rest from
    /// any thread, and everything is under one lock. Images are only ever
    /// disposed on the render thread (inside Push/Get) or by the compressor
    /// once nobody can see them -- an image Get() returned stays valid until
    /// the next Push/Get. Clear() (UI thread, e.g. on Run) just flags.
    /// </summary>
    public sealed class FrameHistory
    {
        private sealed class Frame
        {
            public SKImage? Raw;      // the picture as drawn; released once compressed
            public byte[]? Encoded;   // JPEG/PNG once compressed
            public int Width, Height, FrameCount;
            public SketchState? State;
            public bool Queued;       // waiting for (or in) the compressor
            public bool Busy;         // the compressor is reading Raw right now
            public bool Removed;      // out of the list; whoever holds Raw last disposes it
            public bool ReleaseRaw;   // compressed: Raw goes on the render thread's next call
            public bool EncodeFailed;

            public long RawBytes => Raw == null || ReleaseRaw ? 0 : (long)Width * Height * 4;
        }

        private readonly object _lock = new();
        private readonly List<Frame> _frames = new();
        private readonly Queue<Frame> _toEncode = new();
        private bool _workerRunning;
        private bool _clearRequested;
        private int _truncateAfter = -1;
        private Frame? _decodedFor;
        private SKImage? _decoded;

        /// <summary>Uncompressed newest frames, at most (the instant, exact part).</summary>
        public long RawBudgetBytes { get; set; } = 64L * 1024 * 1024;

        /// <summary>Compressed older frames, at most.</summary>
        public long CompressedBudgetBytes { get; set; } = 160L * 1024 * 1024;

        /// <summary>Frames kept at most (600 = 10 seconds at 60 fps).</summary>
        public int MaxFrames { get; set; } = 600;

        public int JpegQuality { get; set; } = 90;

        /// <summary>Frames waiting to be compressed, at most; past this, aging frames are dropped instead (thinning the old part).</summary>
        public int MaxEncodeBacklog { get; set; } = 3;

        /// <summary>Objects kept across all states at most (old frames lose their state first; their pictures stay).</summary>
        public long StateBudgetObjects { get; set; } = 3_000_000;

        public int Count
        {
            get { lock (_lock) return _clearRequested ? 0 : _frames.Count; }
        }

        /// <summary>How many frames are already compressed (the rest are raw).</summary>
        public int CompressedCount
        {
            get
            {
                lock (_lock)
                {
                    if (_clearRequested)
                        return 0;
                    int n = 0;
                    foreach (var f in _frames)
                        if (f.Encoded != null)
                            n++;
                    return n;
                }
            }
        }

        /// <summary>Render thread only. Takes ownership of <paramref name="image"/>.</summary>
        public void Push(SKImage image, int frameCount, SketchState? state = null)
        {
            lock (_lock)
            {
                DisposeIfRequested();
                _frames.Add(new Frame { Raw = image, Width = image.Width, Height = image.Height, FrameCount = frameCount, State = state });

                // 1) Raw tier over budget: the oldest raw frames go to the
                // compressor -- or, when it's behind, out (thinning the old
                // part rather than letting memory grow). The newest frame
                // always stays raw.
                long raw = 0;
                foreach (var f in _frames)
                    if (!f.Queued)
                        raw += f.RawBytes;
                for (int i = 0; i < _frames.Count - 1 && raw > RawBudgetBytes; i++)
                {
                    var f = _frames[i];
                    if (f.Queued || f.Encoded != null || f.Raw == null)
                        continue;
                    raw -= f.RawBytes;
                    if (!f.EncodeFailed && _toEncode.Count < MaxEncodeBacklog)
                    {
                        f.Queued = true;
                        _toEncode.Enqueue(f);
                    }
                    else
                    {
                        RemoveAt(i);
                        i--;
                    }
                }

                // 2) Overall caps: frame count, then compressed bytes.
                while (_frames.Count > Math.Max(1, MaxFrames))
                    RemoveAt(0);
                long encoded = 0;
                foreach (var f in _frames)
                    encoded += f.Encoded?.LongLength ?? 0;
                while (encoded > CompressedBudgetBytes && _frames.Count > 1 && _frames[0].Encoded is { } oldest)
                {
                    encoded -= oldest.LongLength;
                    RemoveAt(0);
                }

                // 3) States: newest first, until the object budget is spent.
                long objects = 0;
                for (int i = _frames.Count - 1; i >= 0; i--)
                {
                    if (_frames[i].State is not { } st)
                        continue;
                    objects += st.ObjectCount;
                    if (objects > StateBudgetObjects)
                        _frames[i].State = null;
                }

                StartCompressorIfNeeded();
            }
        }

        /// <summary>The newest frame at or before <paramref name="index"/> that carries a state, or -1.</summary>
        public int StateIndexAtOrBefore(int index)
        {
            lock (_lock)
            {
                if (_clearRequested)
                    return -1;
                for (int i = Math.Min(index, _frames.Count - 1); i >= 0; i--)
                    if (_frames[i].State != null)
                        return i;
                return -1;
            }
        }

        public SketchState? StateAt(int index)
        {
            lock (_lock)
                return !_clearRequested && index >= 0 && index < _frames.Count ? _frames[index].State : null;
        }

        /// <summary>Forgets every frame after <paramref name="index"/> (the timeline forks from there). Applied on the render thread's next call.</summary>
        public void TruncateAfter(int index)
        {
            lock (_lock)
                _truncateAfter = index;
        }

        /// <summary>
        /// Render thread only: the picture of frame <paramref name="index"/>,
        /// decompressed if needed. It stays valid until the next Push/Get on
        /// that thread.
        /// </summary>
        public SKImage? Get(int index)
        {
            lock (_lock)
            {
                DisposeIfRequested();
                if (index < 0 || index >= _frames.Count)
                    return null;
                var f = _frames[index];
                if (f.Raw != null)
                    return f.Raw;
                if (ReferenceEquals(_decodedFor, f) && _decoded != null)
                    return _decoded;
                DropDecoded();
                if (f.Encoded == null)
                    return null;
                try
                {
                    using var bitmap = SKBitmap.Decode(f.Encoded);
                    _decoded = bitmap == null ? null : SKImage.FromBitmap(bitmap);
                }
                catch (Exception)
                {
                    _decoded = null;
                }
                _decodedFor = _decoded == null ? null : f;
                return _decoded;
            }
        }

        /// <summary>The sketch's FrameCount when frame <paramref name="index"/> was drawn, or -1.</summary>
        public int FrameCountAt(int index)
        {
            lock (_lock)
                return !_clearRequested && index >= 0 && index < _frames.Count ? _frames[index].FrameCount : -1;
        }

        public void Clear()
        {
            lock (_lock)
                _clearRequested = true;
        }

        /// <summary>Waits until the compressor has caught up (tests; true if it did within the time).</summary>
        public bool WaitForCompression(int timeoutMs = 10_000)
        {
            var deadline = Environment.TickCount64 + timeoutMs;
            lock (_lock)
            {
                while (_workerRunning)
                {
                    var left = deadline - Environment.TickCount64;
                    if (left <= 0)
                        return false;
                    Monitor.Wait(_lock, (int)left);
                }
                return true;
            }
        }

        // ------------------------------------------------------------- inside

        /// <summary>Under the lock, render thread.</summary>
        private void RemoveAt(int index)
        {
            var f = _frames[index];
            _frames.RemoveAt(index);
            f.Removed = true;
            f.State = null;
            if (!f.Busy)
            {
                f.Raw?.Dispose();
                f.Raw = null;
            }
            if (ReferenceEquals(_decodedFor, f))
                DropDecoded();
        }

        private void DropDecoded()
        {
            _decoded?.Dispose();
            _decoded = null;
            _decodedFor = null;
        }

        /// <summary>Under the lock, render thread: carries out Clear/TruncateAfter and frees pictures that were compressed.</summary>
        private void DisposeIfRequested()
        {
            if (_clearRequested)
            {
                while (_frames.Count > 0)
                    RemoveAt(_frames.Count - 1);
                DropDecoded();
                _clearRequested = false;
                _truncateAfter = -1;
            }
            if (_truncateAfter >= 0)
            {
                for (int i = _frames.Count - 1; i > _truncateAfter; i--)
                    RemoveAt(i);
                _truncateAfter = -1;
            }
            foreach (var f in _frames)
            {
                if (f.ReleaseRaw && !f.Busy)
                {
                    f.Raw?.Dispose();
                    f.Raw = null;
                    f.ReleaseRaw = false;
                }
            }
        }

        private void StartCompressorIfNeeded()
        {
            if (_workerRunning || _toEncode.Count == 0)
                return;
            _workerRunning = true;
            ThreadPool.QueueUserWorkItem(_ => CompressLoop());
        }

        private void CompressLoop()
        {
            while (true)
            {
                Frame f;
                SKImage image;
                int quality;
                lock (_lock)
                {
                    while (_toEncode.Count > 0 && (_toEncode.Peek().Removed || _toEncode.Peek().Raw == null))
                        _toEncode.Dequeue().Queued = false;
                    if (_toEncode.Count == 0)
                    {
                        _workerRunning = false;
                        Monitor.PulseAll(_lock);
                        return;
                    }
                    f = _toEncode.Dequeue();
                    f.Busy = true;
                    image = f.Raw!;
                    quality = JpegQuality;
                }

                byte[]? bytes = null;
                try
                {
                    bytes = Encode(image, quality);
                }
                catch (Exception)
                {
                    // Stays raw; the next Push drops it if the raw tier is full.
                }

                lock (_lock)
                {
                    f.Busy = false;
                    f.Queued = false;
                    if (f.Removed)
                    {
                        f.Raw?.Dispose();
                        f.Raw = null;
                    }
                    else if (bytes != null)
                    {
                        f.Encoded = bytes;
                        f.ReleaseRaw = true;
                    }
                    else
                    {
                        f.EncodeFailed = true;
                    }
                }
            }
        }

        /// <summary>JPEG for opaque frames (small, fast); PNG when there's transparency, which JPEG would turn black.</summary>
        public static byte[]? Encode(SKImage image, int jpegQuality)
        {
            bool opaque = IsOpaque(image);
            using var data = image.Encode(opaque ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, opaque ? jpegQuality : 100);
            return data?.ToArray();
        }

        public static bool IsOpaque(SKImage image)
        {
            if (image.AlphaType == SKAlphaType.Opaque)
                return true;
            using var pixmap = image.PeekPixels();
            if (pixmap == null || pixmap.BytesPerPixel != 4)
                return false;
            var span = pixmap.GetPixelSpan();
            int rowBytes = pixmap.RowBytes;
            int alpha = pixmap.ColorType is SKColorType.Rgba8888 or SKColorType.Bgra8888 ? 3 : -1;
            if (alpha < 0)
                return false;
            for (int y = 0; y < pixmap.Height; y++)
            {
                int row = y * rowBytes;
                for (int x = 0; x < pixmap.Width; x++)
                    if (span[row + x * 4 + alpha] != 255)
                        return false;
            }
            return true;
        }
    }
}
