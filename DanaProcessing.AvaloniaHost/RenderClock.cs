using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Rendering;
using DanaProcessing.AvaloniaHost.Attach;

namespace DanaProcessing.AvaloniaHost
{
    /// <summary>
    /// Avalonia's clock for drawing windows, replaced on Windows.
    ///
    /// Avalonia renders when its render timer ticks, and on Windows that's a
    /// System.Threading.Timer every 16 ms -- a timer whose precision is the
    /// process's timer resolution. Windows keeps that fine for the app in
    /// front and coarsens it for the others (15.6 ms or worse, and coalesced):
    /// a widget ran smoothly while it was the active window, and dropped to a
    /// stutter as soon as another window was clicked (or got better while
    /// some app playing sound raised the resolution system-wide).
    ///
    /// This clock is a thread of its own, above normal priority, waiting on a
    /// high-resolution waitable timer (precise to well under a millisecond
    /// whatever the timer resolution, foreground or not), and ticking on the
    /// screen's refresh -- DWM's vblank, the same clock attached sketches use
    /// -- so frames are evenly spaced. Avalonia's render loop gets it in place
    /// of its timer. Nothing changes on other systems, or if Avalonia's loop
    /// isn't laid out as expected.
    /// </summary>
    internal sealed class RenderClock
    {
        private static int s_installed;

        /// <summary>Puts the clock in Avalonia's render loop, once per process (UI thread, after the platform is up). True if it's in.</summary>
        public static bool EnsureInstalled()
        {
            // DANA_RENDER_CLOCK=0 turns it off; =1 forces it on any system (for testing).
            var setting = Environment.GetEnvironmentVariable("DANA_RENDER_CLOCK");
            if (setting == "0" || (!OperatingSystem.IsWindows() && setting != "1"))
                return false;
            if (Interlocked.Exchange(ref s_installed, 1) == 1)
                return false;
            try
            {
                // AvaloniaLocator.Current is hidden from compilation too: ask it by reflection.
                var locator = typeof(AvaloniaLocator).GetProperty("Current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                var getService = locator?.GetType().GetMethod("GetService", new[] { typeof(Type) });
                var loop = getService?.Invoke(locator, new object[] { typeof(IRenderLoop) });
                if (loop == null)
                    return Fail("no render loop");
                var type = loop.GetType();
                var timerField = type.GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic);
                var lockField = type.GetField("_timerLock", BindingFlags.Instance | BindingFlags.NonPublic);
                if (timerField?.GetValue(loop) is not IRenderTimer old || lockField?.GetValue(loop) is not { } gate)
                    return Fail($"unexpected render loop {type.FullName}");
                if (old.GetType() != typeof(DefaultRenderTimer) && setting != "1")
                    return Fail($"render timer is {old.GetType().Name}, left alone"); // a platform's own (vsync) timer

                // IRenderTimer can't be implemented at compile time (Avalonia's
                // reference assemblies hide it); at run time it's a plain
                // interface, which a DispatchProxy implements.
                var clock = new RenderClock();
                var timer = DispatchProxy.Create<IRenderTimer, TimerProxy>();
                ((TimerProxy)(object)timer).Clock = clock;
                lock (gate) // the loop starts and stops its timer under this lock
                {
                    var tickProperty = typeof(IRenderTimer).GetProperty("Tick")!;
                    var tick = (Action<TimeSpan>?)tickProperty.GetValue(old);
                    tickProperty.SetValue(old, null);
                    timerField.SetValue(loop, timer);
                    if (tick != null)
                        clock.Tick = tick;
                }
                FrameTrace.Note("render clock: high-resolution timer on the display's refresh, in place of Avalonia's 16 ms timer");
                return true;
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        private static bool Fail(string why)
        {
            FrameTrace.Note("render clock: not installed (" + why + ")");
            return false;
        }

        // ---------------------------------------------------------------------

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly AutoResetEvent _wake = new(false);
        private readonly VBlankClock _vblank = new();
        private volatile Action<TimeSpan>? _tick;
        private Thread? _thread;

        /// <summary>
        /// What Avalonia's render loop sets: a callback to start ticking, null
        /// to stop (always under its lock, so never concurrently). One thread
        /// for the life of the process; stopped, it just waits.
        /// </summary>
        public Action<TimeSpan>? Tick
        {
            get => _tick;
            set
            {
                _tick = value;
                if (value == null)
                    return;
                if (_thread == null)
                {
                    _thread = new Thread(Run) { IsBackground = true, Name = "DanaProcessing render clock", Priority = ThreadPriority.AboveNormal };
                    _thread.Start();
                }
                _wake.Set();
            }
        }

        /// <summary>IRenderTimer for Avalonia's render loop, forwarding to a RenderClock.</summary>
        public class TimerProxy : DispatchProxy
        {
            internal RenderClock? Clock;

            protected override object? Invoke(MethodInfo? method, object?[]? args)
            {
                var clock = Clock!;
                switch (method?.Name)
                {
                    case "get_Tick": return clock.Tick;
                    case "set_Tick": clock.Tick = (Action<TimeSpan>?)args![0]; return null;
                    case "get_RunsInBackground": return true; // ticks on its own thread, like Avalonia's timer
                    default: throw new NotSupportedException("IRenderTimer." + method?.Name);
                }
            }
        }

        private void Run()
        {
            using var timer = WaitableTimer.Create();
            double next = 0;
            while (true)
            {
                var tick = _tick;
                if (tick == null)
                {
                    _wake.WaitOne(); // stopped: nothing to draw until the loop wakes us
                    next = 0;
                    continue;
                }

                double now = _clock.Elapsed.TotalSeconds;
                double period = 1.0 / 60;
                bool synced = _vblank.TryGet(_clock, out double vblank, out double refresh);
                if (synced)
                    period = refresh;
                next = NextTick(now, next, period, synced ? vblank : double.NaN);

                double wait = next - now;
                if (wait > 0.0002)
                    timer.Wait(wait);
                try
                {
                    tick(_clock.Elapsed);
                }
                catch (Exception ex)
                {
                    DanaLogger.ErrorFromException(ex, "Render clock tick");
                }
            }
        }

        /// <summary>
        /// When the next tick is: one refresh after the last, on the screen's
        /// vblank grid when DWM tells where it is (half a millisecond after a
        /// vblank, so a frame is ready for the next one); never in the past by
        /// more than a refresh, so a slow frame doesn't cause a burst.
        /// </summary>
        internal static double NextTick(double now, double last, double period, double vblank)
        {
            double next = last <= 0 ? now : last + period;
            if (next < now - period)
                next = now;
            if (!double.IsNaN(vblank))
                next = FrameScheduler.AlignToRefresh(next, vblank, period, phase: 0.0005);
            return next;
        }

        /// <summary>A high-resolution waitable timer (Windows 10 1803+), or an ordinary one before that.</summary>
        private sealed class WaitableTimer : IDisposable
        {
            private const uint HighResolution = 0x2;      // CREATE_WAITABLE_TIMER_HIGH_RESOLUTION
            private const uint AllAccess = 0x1F0003;      // TIMER_ALL_ACCESS
            private readonly IntPtr _handle;

            private WaitableTimer(IntPtr handle) => _handle = handle;

            public static WaitableTimer Create()
            {
                if (!OperatingSystem.IsWindows())
                    return new WaitableTimer(IntPtr.Zero); // Thread.Sleep (testing only)
                var handle = CreateWaitableTimerExW(IntPtr.Zero, null, HighResolution, AllAccess);
                if (handle == IntPtr.Zero)
                    handle = CreateWaitableTimerExW(IntPtr.Zero, null, 0, AllAccess);
                return new WaitableTimer(handle);
            }

            public void Wait(double seconds)
            {
                if (_handle == IntPtr.Zero)
                {
                    Thread.Sleep(Math.Max(1, (int)(seconds * 1000)));
                    return;
                }
                long due = -(long)(seconds * 10_000_000); // relative, in 100 ns units
                if (SetWaitableTimer(_handle, ref due, 0, IntPtr.Zero, IntPtr.Zero, false))
                    WaitForSingleObject(_handle, 1000);
                else
                    Thread.Sleep(Math.Max(1, (int)(seconds * 1000)));
            }

            public void Dispose()
            {
                if (_handle != IntPtr.Zero)
                    CloseHandle(_handle);
            }

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern IntPtr CreateWaitableTimerExW(IntPtr attributes, string? name, uint flags, uint access);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool SetWaitableTimer(IntPtr timer, ref long dueTime, int period, IntPtr completion, IntPtr arg, bool resume);

            [DllImport("kernel32.dll")]
            private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

            [DllImport("kernel32.dll")]
            private static extern bool CloseHandle(IntPtr handle);
        }
    }
}
