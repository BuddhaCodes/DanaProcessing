using System.Diagnostics;
using System.Text;

namespace DanaProcessing.AvaloniaHost
{
    /// <summary>
    /// A per-second log of how frames are made, for finding out why a sketch
    /// runs slowly on one particular machine. Off unless a file named
    /// <c>dana.trace</c> sits next to the program (or DANA_FRAME_TRACE=1);
    /// then every second a line goes to <c>dana-trace.log</c> beside it (or
    /// in the temp folder if that's read-only): frames, how long Draw() and
    /// putting the frame on screen took, the gaps between frames, what the
    /// display loop and the timer did, garbage collections, CPU, and once,
    /// how the window is set up (GPU or software rendering, transparency,
    /// screen scaling, supersampling).
    /// </summary>
    internal sealed class FrameTrace
    {
        public static readonly bool Enabled =
            Environment.GetEnvironmentVariable("DANA_FRAME_TRACE") == "1" ||
            File.Exists(Path.Combine(AppContext.BaseDirectory, "dana.trace"));

        private static readonly object s_fileLock = new();
        private static string? s_path;

        private readonly string _name;
        private readonly object _lock = new();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private double _windowStart;
        private double _lastPaint = -1;
        private int _paints, _frames, _animationFrames, _timerTicks, _moves;
        private double _drawSum, _drawMax, _blitSum, _blitMax, _gapMax;
        private int _gen0, _gen1, _gen2;
        private TimeSpan _cpu;

        public FrameTrace(string name)
        {
            _name = name;
            (_gen0, _gen1, _gen2) = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
            _cpu = Process.GetCurrentProcess().TotalProcessorTime;
        }

        public double Now => _clock.Elapsed.TotalMilliseconds;

        /// <summary>Render thread: one PaintSketch, with what Draw() and the blit took (ms; draw -1 = no frame run).</summary>
        public void Paint(double drawMs, double blitMs)
        {
            lock (_lock)
            {
                double now = Now;
                if (_lastPaint >= 0)
                    _gapMax = Math.Max(_gapMax, now - _lastPaint);
                _lastPaint = now;
                _paints++;
                if (drawMs >= 0)
                {
                    _frames++;
                    _drawSum += drawMs;
                    _drawMax = Math.Max(_drawMax, drawMs);
                }
                _blitSum += blitMs;
                _blitMax = Math.Max(_blitMax, blitMs);
            }
            MaybeFlush();
        }

        public void AnimationFrame() { lock (_lock) _animationFrames++; MaybeFlush(); }
        public void TimerTick() { lock (_lock) _timerTicks++; MaybeFlush(); }
        public void WindowMoved() { lock (_lock) _moves++; }

        private void MaybeFlush()
        {
            string? line = null;
            lock (_lock)
            {
                double now = Now;
                if (now - _windowStart < 1000)
                    return;
                double secs = (now - _windowStart) / 1000;
                var p = Process.GetCurrentProcess();
                var cpu = p.TotalProcessorTime;
                double cpuPct = (cpu - _cpu).TotalMilliseconds / (now - _windowStart) / Environment.ProcessorCount * 100;
                int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
                line = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"{_name} t={now / 1000,6:0.0}s fps={_frames / secs,5:0.0} paints={_paints / secs,5:0.0} raf={_animationFrames / secs,5:0.0} timer={_timerTicks / secs,5:0.0} " +
                    $"draw={(_frames > 0 ? _drawSum / _frames : 0),5:0.0}/{_drawMax,5:0.0}ms blit={(_paints > 0 ? _blitSum / _paints : 0),5:0.0}/{_blitMax,5:0.0}ms gapMax={_gapMax,6:0.0}ms " +
                    $"moves={_moves / secs,5:0.0} gc={g0 - _gen0}/{g1 - _gen1}/{g2 - _gen2} cpu={cpuPct,5:0.0}% mem={p.WorkingSet64 / 1048576}MB");
                _windowStart = now;
                _paints = _frames = _animationFrames = _timerTicks = _moves = 0;
                _drawSum = _drawMax = _blitSum = _blitMax = _gapMax = 0;
                (_gen0, _gen1, _gen2, _cpu) = (g0, g1, g2, cpu);
            }
            Write(line);
        }

        /// <summary>A one-off line (the window's setup, a change in it).</summary>
        public static void Note(string text)
        {
            if (Enabled)
                Write(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"# {DateTime.Now:HH:mm:ss.fff} {text}"));
        }

        private static void Write(string line)
        {
            lock (s_fileLock)
            {
                try
                {
                    s_path ??= Pick();
                    File.AppendAllText(s_path, line + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // Nowhere to write: tracing just doesn't happen.
                }
            }
        }

        private static string Pick()
        {
            var beside = Path.Combine(AppContext.BaseDirectory, "dana-trace.log");
            try
            {
                File.AppendAllText(beside, $"# --- {DateTime.Now:yyyy-MM-dd HH:mm:ss} pid {Environment.ProcessId} {Environment.OSVersion} cpus={Environment.ProcessorCount} ---{Environment.NewLine}");
                return beside;
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "dana-trace.log");
            }
        }
    }
}
