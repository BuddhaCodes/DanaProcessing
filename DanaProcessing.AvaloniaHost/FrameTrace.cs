using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Threading;

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
    /// screen scaling, supersampling). Each line starts with the time of
    /// day and ends with the app in front (Windows), to match a slowdown to
    /// what was being done at the moment, and with how long the UI thread
    /// took to answer (ui=urgent/background ms, in=1 if Windows says input
    /// is waiting for it): a stalled UI thread and a slow display clock look
    /// alike in fps, not in these. vsync= is how often the platform's render
    /// timer ticked (Windows: WinUI's compositor), sleep1= how long a 1 ms
    /// Thread.Sleep really took (average/longest): Windows throttling the
    /// process's timers shows there.
    ///
    /// "Next to the program" means next to the .exe: an exported sketch
    /// unpacks itself to a cache folder in temp (AppContext.BaseDirectory),
    /// where nobody would put dana.trace or look for the log.
    /// </summary>
    internal sealed class FrameTrace
    {
        public static readonly bool Enabled =
            Environment.GetEnvironmentVariable("DANA_FRAME_TRACE") == "1" ||
            ProgramFolders().Any(dir => File.Exists(Path.Combine(dir, "dana.trace")));

        /// <summary>The .exe's folder, then AppContext.BaseDirectory if that's elsewhere (a single-file app unpacked to temp).</summary>
        private static IEnumerable<string> ProgramFolders()
        {
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (!string.IsNullOrEmpty(exeDir))
                yield return exeDir;
            if (string.IsNullOrEmpty(exeDir) || !SameFolder(exeDir, AppContext.BaseDirectory))
                yield return AppContext.BaseDirectory;
        }

        private static bool SameFolder(string a, string b) =>
            string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        private static readonly object s_fileLock = new();
        private static string? s_path;
        private static long s_renderTicks;
        private static readonly SleepProbe? s_sleep = Enabled ? new SleepProbe() : null;

        /// <summary>The platform's render timer ticked (see RenderClock.CountTicks).</summary>
        public static void RenderTick() => Interlocked.Increment(ref s_renderTicks);

        private readonly string _name;
        private readonly object _lock = new();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private double _windowStart;
        private double _lastPaint = -1;
        private int _paints, _frames, _animationFrames, _timerTicks, _moves;
        private double _drawSum, _drawMax, _blitSum, _blitMax, _gapMax;
        private int _gen0, _gen1, _gen2;
        private TimeSpan _cpu;
        private double _probeSent = -1, _probeUrgent = -1, _probeBackground = -1; // ms on _clock; -1: not yet
        private long _renderTicksAtStart = Interlocked.Read(ref s_renderTicks);
        private bool _probeInput;

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
                long renderTicks = Interlocked.Read(ref s_renderTicks);
                double vsync = (renderTicks - _renderTicksAtStart) / secs;
                _renderTicksAtStart = renderTicks;
                var (sleepAvg, sleepMax) = s_sleep?.Take() ?? (0, 0);
                line = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"{DateTime.Now:HH:mm:ss.f} {_name} t={now / 1000,6:0.0}s fps={_frames / secs,5:0.0} paints={_paints / secs,5:0.0} raf={_animationFrames / secs,5:0.0} timer={_timerTicks / secs,5:0.0} " +
                    $"draw={(_frames > 0 ? _drawSum / _frames : 0),5:0.0}/{_drawMax,5:0.0}ms blit={(_paints > 0 ? _blitSum / _paints : 0),5:0.0}/{_blitMax,5:0.0}ms gapMax={_gapMax,6:0.0}ms " +
                    $"moves={_moves / secs,5:0.0} gc={g0 - _gen0}/{g1 - _gen1}/{g2 - _gen2} cpu={cpuPct,5:0.0}% mem={p.WorkingSet64 / 1048576}MB fg={Foreground.Name()} {ProbeReport(now)} vsync={vsync:0.0} sleep1={sleepAvg:0.0}/{sleepMax:0.0}ms");
                _windowStart = now;
                _paints = _frames = _animationFrames = _timerTicks = _moves = 0;
                _drawSum = _drawMax = _blitSum = _blitMax = _gapMax = 0;
                (_gen0, _gen1, _gen2, _cpu) = (g0, g1, g2, cpu);
            }
            Write(line);
            Probe();
        }

        /// <summary>
        /// Asks the UI thread for two empty jobs, one urgent (Send) and one at
        /// the DispatcherTimer's own priority (Background), and notes when each
        /// runs -- reported on the next line. One probe at a time.
        /// </summary>
        private void Probe()
        {
            double sent;
            lock (_lock)
            {
                if (_probeSent >= 0 && _probeBackground < 0)
                    return; // the last one hasn't come back: the line says for how long
                sent = _probeSent = Now;
                _probeUrgent = _probeBackground = -1;
            }
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    bool input = Foreground.InputWaiting();
                    lock (_lock)
                        if (_probeSent == sent)
                            (_probeUrgent, _probeInput) = (Now - sent, input);
                }, DispatcherPriority.Send);
                Dispatcher.UIThread.Post(() =>
                {
                    lock (_lock)
                        if (_probeSent == sent)
                            _probeBackground = Now - sent;
                }, DispatcherPriority.Background);
            }
            catch
            {
                // dispatcher shutting down
            }
        }

        private string ProbeReport(double now)
        {
            if (_probeSent < 0)
                return "ui=-";
            string Ms(double v) => v >= 0 ? v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                                          : ">" + (now - _probeSent).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            return $"ui={Ms(_probeUrgent)}/{Ms(_probeBackground)}ms in={(_probeInput ? 1 : 0)}";
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

        /// <summary>Beside dana.trace if there is one (else beside the .exe); in temp if that can't be written.</summary>
        private static string Pick()
        {
            var folders = ProgramFolders().ToList();
            var dir = folders.FirstOrDefault(d => File.Exists(Path.Combine(d, "dana.trace"))) ?? folders[0];
            var header = $"# --- {DateTime.Now:yyyy-MM-dd HH:mm:ss} pid {Environment.ProcessId} {Environment.OSVersion} cpus={Environment.ProcessorCount} ---{Environment.NewLine}";
            foreach (var candidate in new[] { dir, Path.GetTempPath() })
            {
                var path = Path.Combine(candidate, "dana-trace.log");
                try
                {
                    File.AppendAllText(path, header);
                    return path;
                }
                catch
                {
                    // read-only: next
                }
            }
            return Path.Combine(Path.GetTempPath(), "dana-trace.log");
        }

        /// <summary>A background thread timing Thread.Sleep(1) over and over (only while tracing).</summary>
        private sealed class SleepProbe
        {
            private readonly object _lock = new();
            private double _sum, _max;
            private int _count;

            public SleepProbe()
            {
                new Thread(Run) { IsBackground = true, Name = "DanaProcessing trace sleep probe" }.Start();
            }

            private void Run()
            {
                var watch = new Stopwatch();
                while (true)
                {
                    watch.Restart();
                    Thread.Sleep(1);
                    double ms = watch.Elapsed.TotalMilliseconds;
                    lock (_lock)
                    {
                        _sum += ms;
                        _max = Math.Max(_max, ms);
                        _count++;
                    }
                    Thread.Sleep(20); // a sample every ~20 ms is plenty
                }
            }

            public (double Avg, double Max) Take()
            {
                lock (_lock)
                {
                    var result = (_count > 0 ? _sum / _count : 0, _max);
                    _sum = _max = 0;
                    _count = 0;
                    return result;
                }
            }
        }

        /// <summary>The process that owns the window in front (Windows; "-" elsewhere), its name looked up once per process.</summary>
        private static class Foreground
        {
            private static uint s_pid;
            private static string s_name = "-";

            public static string Name()
            {
                if (!OperatingSystem.IsWindows())
                    return "-";
                try
                {
                    GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
                    if (pid != s_pid)
                    {
                        s_pid = pid;
                        s_name = pid == 0 ? "none" : pid == Environment.ProcessId ? "self" : Process.GetProcessById((int)pid).ProcessName;
                    }
                }
                catch
                {
                    s_name = "?";
                }
                return s_name;
            }

            /// <summary>UI thread: does Windows say mouse or keyboard input is waiting in this thread's queue (GetQueueStatus)?</summary>
            public static bool InputWaiting()
            {
                if (!OperatingSystem.IsWindows())
                    return false;
                try
                {
                    return (GetQueueStatus(QsInput) >> 16) != 0; // high word: kinds of message in the queue now
                }
                catch
                {
                    return false;
                }
            }

            private const uint QsInput = 0x0007; // QS_KEY | QS_MOUSEMOVE | QS_MOUSEBUTTON

            [DllImport("user32.dll")]
            private static extern uint GetQueueStatus(uint flags);

            [DllImport("user32.dll")]
            private static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        }
    }
}
