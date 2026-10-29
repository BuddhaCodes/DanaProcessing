using System.Diagnostics;
using System.Globalization;

namespace DanaProcessing.AvaloniaHost.Attach
{
    // =========================================================================
    // Event-driven waking. The first versions polled everything: the shell
    // signal file 4 times a second, the target's position once per frame, new
    // windows twice a second, and the host thread slept between frames whatever
    // happened. Now the things that can say "something changed" do (WinEvents,
    // FileSystemWatcher, the input hooks), and the host thread waits on an
    // event it wakes from at once. Polling stays only as a slow fallback.
    // =========================================================================

    /// <summary>When the host's frame loop runs a frame. Pure, so it's testable.</summary>
    internal static class FrameScheduler
    {
        /// <summary>Frames run on a wake can't be closer than this (a burst of wakes is one frame).</summary>
        public const double MinWakeSpacing = 0.002;

        /// <summary>
        /// Run a frame now? A wake (input, a shell signal, the target changing)
        /// runs one at once, then the regular rhythm continues from there.
        /// Otherwise frames come every <paramref name="interval"/>; after falling
        /// behind by more than a frame (a slow Draw, a breakpoint) it doesn't
        /// try to catch up.
        /// </summary>
        public static (bool Run, double Next) Decide(double now, double next, double interval, bool woken, double lastFrame)
        {
            if (woken && now - lastFrame >= MinWakeSpacing)
                return (true, now + interval);
            if (now < next)
                return (false, next);
            return (true, now - next > interval ? now + interval : next + interval);
        }

        /// <summary>
        /// The frame interval rounded to whole refreshes of the screen
        /// (<paramref name="period"/>, seconds): 60 fps on a 144 Hz screen is
        /// every 2 refreshes (72 fps), not 2, 3, 2, 2, 3... which judders.
        /// Without a usable period, the interval as is.
        /// </summary>
        public static double SnapToRefresh(double interval, double period)
        {
            if (!(period > 0.002 && period < 0.1))
                return interval;
            return period * Math.Max(1, Math.Round(interval / period));
        }

        /// <summary>
        /// Moves <paramref name="next"/> onto the screen's refresh grid: the
        /// first refresh (<paramref name="vblank"/> + n * period) at or after
        /// next - period/2, plus <paramref name="phase"/>. Frames then start
        /// just after a refresh and are on screen by the following one: one
        /// new picture per refresh interval, evenly, instead of a timer that
        /// slowly drifts against the screen (two frames in one refresh, none
        /// in the next -- a stutter even at a steady 60 fps).
        /// </summary>
        public static double AlignToRefresh(double next, double vblank, double period, double phase = 0.001)
        {
            if (!(period > 0.002 && period < 0.1) || double.IsNaN(vblank))
                return next;
            double n = Math.Ceiling((next - phase - vblank - period / 2) / period);
            return vblank + phase + n * period;
        }

        /// <summary>At most one overlay move between frames every this many seconds (~250 a second, more than any screen shows).</summary>
        public const double FastMoveSpacing = 0.004;

        /// <summary>Apply a pending move of the overlay now? Never more often than FastMoveSpacing, however many moves Windows reports.</summary>
        public static bool ShouldFastMove(bool pending, double now, double lastMove) =>
            pending && now - lastMove >= FastMoveSpacing;

        /// <summary>
        /// How long to wait for the next frame (or a wake), in ms: never more
        /// than 100, so a lost wake costs little, and only a few ms while a move
        /// of the overlay is pending.
        /// </summary>
        public static uint WaitMilliseconds(double now, double next, bool woken, bool movePending = false)
        {
            if (woken)
                return 1u;
            uint ms = (uint)Math.Clamp((int)Math.Ceiling((next - now) * 1000), 0, 100);
            return movePending ? Math.Min(ms, (uint)Math.Ceiling(FastMoveSpacing * 1000)) : ms;
        }
    }

    /// <summary>What a WinEvent means for the host.</summary>
    internal enum WinEventAction
    {
        Ignore,
        /// <summary>The target moved or resized: move the overlay right now.</summary>
        Moved,
        /// <summary>Something else about the target (or the foreground) changed: run a frame now.</summary>
        Wake,
    }

    /// <summary>Which WinEvents matter. Pure, so it's testable.</summary>
    internal static class WinEventFilter
    {
        public static WinEventAction Classify(uint eventId, IntPtr hwnd, int idObject, int idChild, IntPtr target)
        {
            if (eventId == Win32.EVENT_SYSTEM_FOREGROUND)
                return WinEventAction.Wake; // focus moved: focus events, eco mode
            if (target == IntPtr.Zero || hwnd != target || idObject != Win32.OBJID_WINDOW || idChild != Win32.CHILDID_SELF)
                return WinEventAction.Ignore; // a control inside the window, a tooltip, another window of the same app
            return eventId == Win32.EVENT_OBJECT_LOCATIONCHANGE ? WinEventAction.Moved : WinEventAction.Wake;
        }
    }

    /// <summary>
    /// WinEvent hooks for one target window, on the calling thread (which must
    /// pump messages: out-of-context hooks are delivered through its queue).
    /// Watches the target's process for move/resize, show/hide, minimize,
    /// cloak and destroy, plus the foreground window system-wide.
    /// </summary>
    internal sealed class WinEventHooks : IDisposable
    {
        private readonly Win32.WinEventProc _proc; // kept alive while hooked
        private readonly Action<uint, IntPtr, int, int, uint> _handler;
        private readonly List<IntPtr> _hooks = new();

        public WinEventHooks(Action<uint, IntPtr, int, int, uint> handler)
        {
            _handler = handler;
            _proc = (_, eventId, hwnd, idObject, idChild, _, time) =>
            {
                try { _handler(eventId, hwnd, idObject, idChild, time); }
                catch (Exception ex) { DanaLogger.ErrorFromException(ex, "WinEvent"); }
            };
        }

        /// <summary>The window being watched (zero = none).</summary>
        public IntPtr Target { get; private set; }

        /// <summary>Watches <paramref name="target"/> instead (zero stops watching). Cheap when it's the same one.</summary>
        public void Watch(IntPtr target)
        {
            if (target == Target)
                return;
            Unhook();
            Target = target;
            if (target == IntPtr.Zero)
                return;

            Win32.GetWindowThreadProcessId(target, out uint pid);
            // Narrow ranges in the target's process only: a range like 0x8001-0x8018
            // would also deliver every name/value change (a terminal's title changes a lot).
            Hook(Win32.EVENT_OBJECT_DESTROY, Win32.EVENT_OBJECT_HIDE, pid);
            Hook(Win32.EVENT_OBJECT_LOCATIONCHANGE, Win32.EVENT_OBJECT_LOCATIONCHANGE, pid);
            Hook(Win32.EVENT_OBJECT_CLOAKED, Win32.EVENT_OBJECT_UNCLOAKED, pid);
            Hook(Win32.EVENT_SYSTEM_MINIMIZESTART, Win32.EVENT_SYSTEM_MINIMIZEEND, pid);
            Hook(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND, 0);
            if (WatchTitle)
                Hook(Win32.EVENT_OBJECT_NAMECHANGE, Win32.EVENT_OBJECT_NAMECHANGE, pid);   // tab switches (the window title is the active tab's)
        }

        /// <summary>Also report the target's title changing (only when someone needs it: titles of the terminal's children change a lot).</summary>
        public bool WatchTitle { get; init; }

        private void Hook(uint min, uint max, uint pid)
        {
            var h = Win32.SetWinEventHook(min, max, IntPtr.Zero, _proc, pid, 0, Win32.WINEVENT_OUTOFCONTEXT);
            if (h != IntPtr.Zero)
                _hooks.Add(h);
        }

        private void Unhook()
        {
            foreach (var h in _hooks)
                Win32.UnhookWinEvent(h);
            _hooks.Clear();
        }

        public void Dispose()
        {
            Unhook();
            Target = IntPtr.Zero;
        }
    }

    /// <summary>
    /// What the IDE card shows about a host's speed: frames per second, what a
    /// frame costs (the sketch's Draw(), putting it on screen), and how long
    /// things take to reach the sketch (a shell signal from when the shell
    /// wrote it, a key from when the hook saw it, the overlay following a
    /// move from when Windows reported it). -1 = nothing measured yet.
    /// </summary>
    public readonly record struct AttachedHostStats(
        double Fps, double DrawMs, double PresentMs, double SignalLatencyMs, double InputLatencyMs, double MoveLatencyMs,
        int LateFrames = 0, double WorstFrameMs = 0)
    {
        public static AttachedHostStats None { get; } = new(0, 0, 0, -1, -1, -1);

        /// <summary>One line for the IDE card: "60 fps · dibujo 3.1 ms · presentar 0.9 ms · señal 4 ms · tecla 2 ms · mover 1 ms".</summary>
        public string Format()
        {
            var c = CultureInfo.InvariantCulture;
            var parts = new List<string>
            {
                string.Format(c, "{0:0} fps", Fps),
                string.Format(c, UiText.Tr("dibujo {0:0.0} ms", "draw {0:0.0} ms"), DrawMs),
                string.Format(c, UiText.Tr("presentar {0:0.0} ms", "present {0:0.0} ms"), PresentMs),
            };
            if (SignalLatencyMs >= 0)
                parts.Add(string.Format(c, UiText.Tr("señal {0:0} ms", "signal {0:0} ms"), SignalLatencyMs));
            if (InputLatencyMs >= 0)
                parts.Add(string.Format(c, UiText.Tr("tecla {0:0} ms", "key {0:0} ms"), InputLatencyMs));
            if (MoveLatencyMs >= 0)
                parts.Add(string.Format(c, UiText.Tr("mover {0:0} ms", "move {0:0} ms"), MoveLatencyMs));
            // Stutter you can see: frames that came much later than they should have, in the last second.
            if (LateFrames > 0)
                parts.Add(string.Format(c, UiText.Tr("{0} tardíos (peor {1:0} ms)", "{0} late (worst {1:0} ms)"), LateFrames, WorstFrameMs));
            return string.Join(" · ", parts);
        }
    }

    /// <summary>Collects AttachedHostStats on the host thread. Smoothed costs, latest latencies.</summary>
    internal sealed class StatsMeter
    {
        private const double Smoothing = 0.1;
        private readonly Queue<double> _frames = new();
        private readonly Queue<(double At, double Ms)> _late = new();
        private double _lastFrame = double.NaN;
        private double _draw, _present;
        private double _signal = -1, _input = -1, _move = -1;

        /// <summary>
        /// A frame went on screen at <paramref name="now"/> (seconds).
        /// <paramref name="budget"/> is how far apart frames should be: one that
        /// came over 1.5x that after the previous counts as late (a visible stutter).
        /// A gap over half a second isn't a stutter (pause, eco, hidden window).
        /// </summary>
        public void Frame(double now, double budget = 0)
        {
            if (!double.IsNaN(_lastFrame) && budget > 0)
            {
                double gap = now - _lastFrame;
                if (gap > budget * 1.5 && gap < 0.5)
                    _late.Enqueue((now, gap * 1000));
            }
            _lastFrame = now;
            _frames.Enqueue(now);
            Trim(now);
        }

        private void Trim(double now)
        {
            while (_frames.Count > 0 && now - _frames.Peek() > 1.0)
                _frames.Dequeue();
            while (_late.Count > 0 && now - _late.Peek().At > 1.0)
                _late.Dequeue();
        }

        public void Draw(double ms) => _draw = Smooth(_draw, ms);
        public void Present(double ms) => _present = Smooth(_present, ms);
        public void Signal(double ms) => _signal = Math.Max(0, ms);
        public void Input(double ms) => _input = Math.Max(0, ms);
        public void Move(double ms) => _move = Math.Max(0, ms);

        /// <summary>Frames in the last second (counted at <paramref name="now"/>).</summary>
        public double FramesPerSecond(double now)
        {
            Trim(now);
            return _frames.Count;
        }

        public AttachedHostStats Snapshot(double now)
        {
            double fps = FramesPerSecond(now);
            double worst = 0;
            foreach (var (_, ms) in _late)
                worst = Math.Max(worst, ms);
            return new(fps, _draw, _present, _signal, _input, _move, _late.Count, worst);
        }

        private static double Smooth(double old, double value) => old <= 0 ? value : old + (value - old) * Smoothing;

        /// <summary>Milliseconds between two Stopwatch timestamps.</summary>
        public static double Ms(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>
    /// Calls back as soon as a file changes (FileSystemWatcher on its folder),
    /// for the shell signal files. The folder must exist. Raised on a
    /// thread-pool thread, possibly several times per write.
    /// </summary>
    internal sealed class FileSignalWatcher : IDisposable
    {
        private readonly FileSystemWatcher _watcher;

        public FileSignalWatcher(string filePath, Action changed)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
            _watcher = new FileSystemWatcher(dir, Path.GetFileName(filePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
            };
            FileSystemEventHandler handler = (_, _) => changed();
            _watcher.Changed += handler;
            _watcher.Created += handler;
            _watcher.Renamed += (_, _) => changed();
            _watcher.EnableRaisingEvents = true;
        }

        public void Dispose() => _watcher.Dispose();
    }
}

namespace DanaProcessing.AvaloniaHost.Attach
{
    /// <summary>
    /// When the screen refreshes, from DWM (DwmGetCompositionTimingInfo): the
    /// last vertical blank and the refresh period, in the host clock's seconds.
    /// Windows Terminal itself paces its frames to the display (its swap
    /// chain's frame-latency waitable object); a layered window has no swap
    /// chain, so the host asks DWM and starts its frames just after a refresh.
    /// </summary>
    internal sealed class VBlankClock
    {
        // DWM_TIMING_INFO is declared with 1-byte packing: 292 bytes.
        // rateRefresh at 4, qpcRefreshPeriod at 12, rateCompose at 20, qpcVBlank at 28.
        internal const int TimingInfoSize = 292;
        private readonly byte[] _info = new byte[TimingInfoSize];
        private bool _failed;

        /// <summary>
        /// The last vblank and the period, converted to <paramref name="clock"/>'s
        /// seconds (Stopwatch: same QPC timebase as DWM). False if DWM can't say.
        /// </summary>
        public bool TryGet(Stopwatch clock, out double vblank, out double period)
        {
            vblank = double.NaN;
            period = 0;
            if (_failed)
                return false;
            try
            {
                BitConverter.TryWriteBytes(_info.AsSpan(0, 4), TimingInfoSize);
                if (Win32.DwmGetCompositionTimingInfo(IntPtr.Zero, _info) != 0)
                    return false;
                return Convert(BitConverter.ToInt64(_info, 28), BitConverter.ToInt64(_info, 12),
                               Stopwatch.GetTimestamp(), clock.Elapsed.TotalSeconds, Stopwatch.Frequency, out vblank, out period);
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                _failed = true; // not on this system: the timer alone paces
                return false;
            }
        }

        /// <summary>QPC values to the host clock's seconds. Pure, so it's testable.</summary>
        internal static bool Convert(long qpcVBlank, long qpcPeriod, long qpcNow, double nowSeconds, long frequency,
                                     out double vblank, out double period)
        {
            vblank = double.NaN;
            period = 0;
            if (qpcVBlank <= 0 || qpcPeriod <= 0 || frequency <= 0)
                return false;
            period = (double)qpcPeriod / frequency;
            vblank = nowSeconds + (double)(qpcVBlank - qpcNow) / frequency;
            return period > 0.002 && period < 0.1;
        }
    }

    /// <summary>
    /// For an export in its own Windows Terminal profile: is that profile's
    /// tab the one in front? Windows Terminal titles its window after the
    /// active tab (TerminalPage::Title), and the profile fixes its tab's title
    /// (tabTitle + suppressApplicationTitle). When another tab is in front the
    /// sketch -- invisible behind an opaque profile anyway -- stops drawing.
    /// </summary>
    internal static class TitleGate
    {
        /// <summary>
        /// True: draw. Unknown title, our title, or "Terminal" (showTitleInTitlebar
        /// off: the title says nothing about the tab) all count as ours.
        /// </summary>
        public static bool Allows(string? windowTitle, string? profileTitle) =>
            string.IsNullOrEmpty(profileTitle)
            || string.IsNullOrEmpty(windowTitle)
            || windowTitle == profileTitle
            || windowTitle == "Terminal";

        /// <summary>The window's title, read without sending it a message.</summary>
        public static string? Read(IntPtr hwnd)
        {
            try
            {
                var sb = new System.Text.StringBuilder(256);
                int n = Win32.InternalGetWindowText(hwnd, sb, sb.Capacity);
                return n > 0 ? sb.ToString() : null;
            }
            catch { return null; }
        }
    }
}
