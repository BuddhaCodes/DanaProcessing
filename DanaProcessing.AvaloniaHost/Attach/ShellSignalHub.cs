using System.Runtime.InteropServices;
using DanaProcessing;

namespace DanaProcessing.AvaloniaHost.Attach
{
    /// <summary>Something that wants shell signals: a host, with what routing needs to know about it.</summary>
    internal interface IShellSignalListener
    {
        /// <summary>The window it's on right now (zero while it has none).</summary>
        IntPtr TargetHandle { get; }

        /// <summary>When its window was last in front (host clock seconds; 0 = never).</summary>
        double LastFocusedAt { get; }

        /// <summary>
        /// One pane of a split terminal (TerminalPanes): when that pane last had
        /// the keyboard focus, on the fleet's scanner clock (0 = never, or not a pane).
        /// </summary>
        double PaneFocusedAt { get; }

        /// <summary>Called on a thread-pool thread: queue it and wake up. <paramref name="writtenUtc"/> is when the shell wrote it.</summary>
        void Deliver(ShellSignal signal, DateTime writtenUtc);
    }

    /// <summary>What routing knows about one listener (see ShellSignalHub.Route).</summary>
    /// <param name="Target">Its window (zero while it has none).</param>
    /// <param name="LastFocusedAt">When its window was last in front (0 = never).</param>
    /// <param name="PaneFocusedAt">For one pane of a split terminal: when that pane last had the keyboard focus (0 = never).</param>
    /// <param name="OwnsShell">The shell that sent the signal (its process id) was seen in this listener's pane before.</param>
    internal readonly record struct SignalRoute(IntPtr Target, double LastFocusedAt, double PaneFocusedAt = 0, bool OwnsShell = false);

    /// <summary>
    /// Watches ShellSignals.SignalFile and Abyss Tide's signal file and hands
    /// each new signal to the right listener, one watcher for the whole process
    /// however many sketches listen, only while someone listens.
    ///
    /// A FileSystemWatcher reports a write within milliseconds; a slow poll
    /// (once a second) is only the fallback for a missed notification or a
    /// folder that appears later (Abyss Tide installed afterwards).
    /// </summary>
    internal static class ShellSignalHub
    {
        private const int FallbackPollMs = 1000;

        private static readonly object s_lock = new();
        private static readonly object s_pollLock = new();
        private static readonly List<IShellSignalListener> s_listeners = new();
        private static Timer? s_fallback, s_pendingTimer;
        private static FileSignalWatcher? s_danaWatcher, s_abyssWatcher;
        private static DateTime s_danaStamp, s_abyssStamp;
        private static readonly SignalMerger s_merger = new();
        // Which listener each shell (process id) belongs to, learned from signals
        // that arrived while that listener's pane was the one in front: a long
        // command that ends after you moved to another pane still reaches its own.
        private static readonly Dictionary<int, IShellSignalListener> s_shellOwners = new();

        /// <summary>Starts sending signals to <paramref name="listener"/>. Signals written before this are ignored.</summary>
        public static void Subscribe(IShellSignalListener listener)
        {
            lock (s_lock)
            {
                if (s_listeners.Contains(listener))
                    return;
                s_listeners.Add(listener);
                if (s_fallback != null)
                    return;

                s_danaStamp = StampOf(ShellSignals.SignalFile);
                s_abyssStamp = StampOf(ShellSignals.AbyssTideSignalFile);
                s_fallback = new Timer(_ => Poll(), null, FallbackPollMs, FallbackPollMs);
                s_pendingTimer = new Timer(_ => Poll(), null, Timeout.Infinite, Timeout.Infinite);
                WatchFiles();
            }
        }

        public static void Unsubscribe(IShellSignalListener listener)
        {
            lock (s_lock)
            {
                s_listeners.Remove(listener);
                foreach (var pid in s_shellOwners.Where(kv => kv.Value == listener).Select(kv => kv.Key).ToList())
                    s_shellOwners.Remove(pid);
                if (s_listeners.Count > 0)
                    return;
                s_fallback?.Dispose();
                s_pendingTimer?.Dispose();
                s_danaWatcher?.Dispose();
                s_abyssWatcher?.Dispose();
                s_fallback = s_pendingTimer = null;
                s_danaWatcher = s_abyssWatcher = null;
            }
        }

        /// <summary>Under s_lock. Watchers for the folders that exist; the fallback poll retries the rest.</summary>
        private static void WatchFiles()
        {
            try
            {
                if (s_danaWatcher == null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ShellSignals.SignalFile)!);
                    s_danaWatcher = new FileSignalWatcher(ShellSignals.SignalFile, PollSoon);
                }
            }
            catch (Exception ex) { DanaLogger.ErrorFromException(ex, "Vigilando señales del shell"); }
            try
            {
                if (s_abyssWatcher == null && Directory.Exists(Path.GetDirectoryName(ShellSignals.AbyssTideSignalFile)))
                    s_abyssWatcher = new FileSignalWatcher(ShellSignals.AbyssTideSignalFile, PollSoon);
            }
            catch { /* Abyss Tide's folder is optional */ }
        }

        private static void PollSoon() => ThreadPool.QueueUserWorkItem(_ => Poll());

        /// <summary>
        /// Who gets a signal, as indexes into <paramref name="listeners"/>:
        /// 1. the listener whose pane the sending shell was seen in before (OwnsShell);
        /// 2. else the listeners on the window in front (the shell where you just
        ///    pressed Enter) -- and of a split terminal, only the pane with the
        ///    keyboard focus, not every pane of the window;
        /// 3. else those on the window in front most recently (again only its focused pane);
        /// 4. else everyone.
        /// </summary>
        public static IReadOnlyList<int> Route(IReadOnlyList<SignalRoute> listeners, IntPtr foreground)
        {
            var owners = new List<int>();
            for (int i = 0; i < listeners.Count; i++)
                if (listeners[i].OwnsShell)
                    owners.Add(i);
            if (owners.Count > 0)
                return owners;

            var front = new List<int>();
            for (int i = 0; i < listeners.Count; i++)
                if (listeners[i].Target != IntPtr.Zero && listeners[i].Target == foreground)
                    front.Add(i);
            if (front.Count > 0)
                return FocusedPane(listeners, front);

            int latest = -1;
            for (int i = 0; i < listeners.Count; i++)
                if (listeners[i].LastFocusedAt > 0 && (latest < 0 || listeners[i].LastFocusedAt > listeners[latest].LastFocusedAt))
                    latest = i;
            if (latest >= 0)
            {
                var window = new List<int>();
                for (int i = 0; i < listeners.Count; i++)
                    if (i == latest || (listeners[i].Target != IntPtr.Zero && listeners[i].Target == listeners[latest].Target))
                        window.Add(i);
                return FocusedPane(listeners, window);
            }

            return Enumerable.Range(0, listeners.Count).ToArray();
        }

        /// <summary>Of one window's listeners, the pane that had the keyboard focus last; all of them if none is a pane.</summary>
        private static IReadOnlyList<int> FocusedPane(IReadOnlyList<SignalRoute> listeners, List<int> window)
        {
            int best = -1;
            foreach (int i in window)
                if (listeners[i].PaneFocusedAt > 0 && (best < 0 || listeners[i].PaneFocusedAt > listeners[best].PaneFocusedAt))
                    best = i;
            return best >= 0 ? new[] { best } : window;
        }

        /// <summary>
        /// Whether to remember that the shell belongs to the one listener a
        /// signal went to: only when that listener's window is the one in front
        /// (you just ran the command there), not a guess from an older focus.
        /// </summary>
        public static bool ShouldLearnShell(int processId, IReadOnlyList<int> routed, IReadOnlyList<SignalRoute> listeners, IntPtr foreground) =>
            processId != 0 && routed.Count == 1 && !listeners[routed[0]].OwnsShell
            && listeners[routed[0]].Target != IntPtr.Zero && listeners[routed[0]].Target == foreground;

        private static DateTime StampOf(string path)
        {
            try { return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue; }
            catch { return DateTime.MinValue; }
        }

        /// <summary>
        /// A new signal in <paramref name="path"/> since <paramref name="stamp"/>,
        /// or null. The stamp only moves once the file was read: a read that
        /// races the shell's write is retried by the next notification or poll.
        /// </summary>
        private static (ShellSignal Signal, DateTime Written)? ReadIfChanged(string path, ref DateTime stamp)
        {
            var written = StampOf(path);
            if (written <= stamp)
                return null;
            string text;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
                text = reader.ReadToEnd();
            stamp = written;
            return ShellSignal.Parse(text) is { } signal ? (signal, written) : null;
        }

        private static void Poll()
        {
            var deliver = new List<(ShellSignal Signal, DateTime Written)>();
            bool pending;
            lock (s_pollLock)
            {
                double now = Environment.TickCount64 / 1000.0;
                // Dana's file first: when both hooks report the same command,
                // its line (with the exit code) is the one that counts.
                try
                {
                    if (ReadIfChanged(ShellSignals.SignalFile, ref s_danaStamp) is { } dana)
                        deliver.AddRange(s_merger.Offer(SignalSource.Dana, dana.Signal, dana.Written, now));
                }
                catch { /* the shell may be mid-write; the next change gets another try */ }
                try
                {
                    if (ReadIfChanged(ShellSignals.AbyssTideSignalFile, ref s_abyssStamp) is { } abyss)
                        deliver.AddRange(s_merger.Offer(SignalSource.AbyssTide, abyss.Signal, abyss.Written, now));
                }
                catch { }
                deliver.AddRange(s_merger.Tick(now));
                pending = s_merger.HasPending;
            }

            lock (s_lock)
            {
                // A held-back Abyss Tide signal goes out after a short wait, not at the next poll.
                if (pending)
                    s_pendingTimer?.Change((int)(SignalMerger.Wait * 1000) + 10, Timeout.Infinite);
                // Abyss Tide's folder may have appeared since.
                if (s_abyssWatcher == null && s_fallback != null)
                    WatchFiles();
            }
            if (deliver.Count == 0)
                return;

            try
            {
                IShellSignalListener[] listeners;
                lock (s_lock)
                    listeners = s_listeners.ToArray();
                if (listeners.Length == 0)
                    return;

                var foreground = OperatingSystem.IsWindows() ? Win32.GetForegroundWindow() : IntPtr.Zero;
                foreach (var (signal, written) in deliver)
                {
                    IShellSignalListener? owner;
                    lock (s_lock)
                        s_shellOwners.TryGetValue(signal.ProcessId, out owner);
                    var info = listeners.Select(l => new SignalRoute(l.TargetHandle, l.LastFocusedAt, l.PaneFocusedAt,
                        signal.ProcessId != 0 && ReferenceEquals(l, owner))).ToList();
                    var routed = Route(info, foreground);
                    if (ShouldLearnShell(signal.ProcessId, routed, info, foreground))
                        lock (s_lock)
                            s_shellOwners[signal.ProcessId] = listeners[routed[0]];
                    foreach (int i in routed)
                        listeners[i].Deliver(signal, written);
                }
            }
            catch { }
        }
    }

    /// <summary>Which hook wrote a signal.</summary>
    internal enum SignalSource { Dana, AbyssTide }

    /// <summary>
    /// With both Dana's and Abyss Tide's prompt hooks installed, every command
    /// is reported twice -- by two wrappers of the same prompt, so within a
    /// millisecond or two. A signal from Abyss Tide waits a moment: if Dana's
    /// own hook reports the same kind of result meanwhile (or just did), the
    /// Abyss Tide one is dropped -- Dana's carries the exit code. Alone, Abyss
    /// Tide's signals arrive Wait seconds late. Pure, so it's testable.
    /// </summary>
    internal sealed class SignalMerger
    {
        /// <summary>Two reports of the same kind within this many seconds are the same command.</summary>
        public const double SameCommandWindow = 1.0;

        /// <summary>How long an Abyss Tide signal waits for Dana's report of the same command.</summary>
        public const double Wait = 0.05;

        private (ShellSignal Signal, DateTime Written)? _pending;
        private double _pendingAt;
        private ShellSignalKind? _lastDanaKind;
        private double _lastDanaAt = double.NegativeInfinity;

        /// <summary>An Abyss Tide signal is waiting.</summary>
        public bool HasPending => _pending != null;

        /// <summary>A new signal from <paramref name="source"/> at <paramref name="now"/> (seconds). Returns what to deliver now.</summary>
        public IReadOnlyList<(ShellSignal Signal, DateTime Written)> Offer(SignalSource source, ShellSignal signal, DateTime written, double now)
        {
            if (source == SignalSource.Dana)
            {
                if (_pending is { } p && p.Signal.Kind == signal.Kind && now - _pendingAt < SameCommandWindow)
                    _pending = null; // the same command, reported by both: keep Dana's
                if (signal.Kind != ShellSignalKind.Custom)
                {
                    _lastDanaKind = signal.Kind;
                    _lastDanaAt = now;
                }
                return new[] { (signal, written) };
            }

            if (_lastDanaKind == signal.Kind && now - _lastDanaAt < SameCommandWindow)
                return Array.Empty<(ShellSignal, DateTime)>(); // Dana's hook already reported it
            var flushed = Flush();
            _pending = (signal, written);
            _pendingAt = now;
            return flushed;
        }

        /// <summary>Delivers an Abyss Tide signal that waited long enough.</summary>
        public IReadOnlyList<(ShellSignal Signal, DateTime Written)> Tick(double now) =>
            _pending != null && now - _pendingAt >= Wait ? Flush() : Array.Empty<(ShellSignal, DateTime)>();

        private IReadOnlyList<(ShellSignal Signal, DateTime Written)> Flush()
        {
            if (_pending is not { } p)
                return Array.Empty<(ShellSignal, DateTime)>();
            _pending = null;
            return new[] { p };
        }
    }

    /// <summary>Battery state, for eco mode.</summary>
    internal static class Power
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
            public int BatteryLifeTime, BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

        /// <summary>On battery (not plugged in) with less than 25 % left. False on desktops and off Windows.</summary>
        public static bool IsLowBattery()
        {
            if (!OperatingSystem.IsWindows())
                return false;
            try
            {
                return GetSystemPowerStatus(out var s) && IsLow(s.ACLineStatus, s.BatteryLifePercent);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>ACLineStatus 0 = on battery; percent 255 = unknown (no battery).</summary>
        public static bool IsLow(byte acLineStatus, byte percent) => acLineStatus == 0 && percent < 25;
    }
}
