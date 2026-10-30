using DanaProcessing.AvaloniaHost.Attach;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// The event-driven parts of the attached-sketch host: when the frame loop
/// runs, which WinEvents matter, the speed numbers on the IDE card, and the
/// file watcher that brings shell signals in milliseconds instead of polls.
/// </summary>
public class AttachedEventDrivenTests
{
    private const double Frame60 = 1 / 60.0;

    [Fact]
    public void Frames_keep_their_rhythm_without_wakes()
    {
        Assert.Equal((false, 1.0), FrameScheduler.Decide(0.99, 1.0, Frame60, woken: false, lastFrame: 0.98));
        var (run, next) = FrameScheduler.Decide(1.0, 1.0, Frame60, false, 0.98);
        Assert.True(run);
        Assert.Equal(1.0 + Frame60, next, 9);
    }

    [Fact]
    public void A_wake_runs_a_frame_at_once_and_the_rhythm_restarts_from_it()
    {
        // Eco mode: next frame in 66 ms, but a key arrived 5 ms after the last one.
        var (run, next) = FrameScheduler.Decide(10.005, 10.066, 1 / 15.0, woken: true, lastFrame: 10.0);
        Assert.True(run);
        Assert.Equal(10.005 + 1 / 15.0, next, 9);
    }

    [Fact]
    public void A_burst_of_wakes_is_one_frame()
    {
        var (run, _) = FrameScheduler.Decide(10.0005, 10.016, Frame60, woken: true, lastFrame: 10.0);
        Assert.False(run); // closer than MinWakeSpacing to the last frame
        Assert.Equal(1u, FrameScheduler.WaitMilliseconds(10.0005, 10.016, woken: true)); // ...but soon
    }

    [Fact]
    public void Falling_behind_does_not_try_to_catch_up()
    {
        var (run, next) = FrameScheduler.Decide(11.0, 10.0, Frame60, false, 9.9); // a 1 s breakpoint
        Assert.True(run);
        Assert.Equal(11.0 + Frame60, next, 9);
    }

    [Theory]
    [InlineData(0.0, 0.0166, 17u)]
    [InlineData(0.0, 5.0, 100u)]   // never sleeps blind for long
    [InlineData(1.0, 0.5, 0u)]
    public void Wait_until_the_next_frame(double now, double next, uint ms)
    {
        Assert.Equal(ms, FrameScheduler.WaitMilliseconds(now, next, woken: false));
    }

    [Fact]
    public void A_drag_moves_the_overlay_at_most_every_few_milliseconds()
    {
        // The bug: moving the overlay inside every WinEvent callback (hundreds a
        // second while dragging) kept the message queue from ever emptying, so
        // no frame ran -- the sketch looked paused while the terminal moved.
        Assert.False(FrameScheduler.ShouldFastMove(pending: false, now: 1, lastMove: 0));
        Assert.True(FrameScheduler.ShouldFastMove(true, 1.000, 0.990));
        Assert.False(FrameScheduler.ShouldFastMove(true, 1.001, 1.000)); // too soon after the last one
        Assert.True(FrameScheduler.ShouldFastMove(true, 1.000 + FrameScheduler.FastMoveSpacing, 1.000));
    }

    [Fact]
    public void A_pending_move_shortens_the_wait_but_frames_keep_their_rhythm()
    {
        Assert.Equal(4u, FrameScheduler.WaitMilliseconds(0, 0.016, woken: false, movePending: true));
        Assert.Equal(17u, FrameScheduler.WaitMilliseconds(0, 0.0166, woken: false, movePending: false));
        // While moves keep arriving, frames still run when they're due.
        var (run, _) = FrameScheduler.Decide(0.0170, 0.0166, 1 / 60.0, woken: false, lastFrame: 0);
        Assert.True(run);
    }

    [Fact]
    public void A_simulated_drag_keeps_drawing_frames()
    {
        // 2 s of dragging with a move reported every millisecond: the loop as
        // the host runs it (messages pumped, at most one overlay move per
        // FastMoveSpacing, frames by the scheduler). Every 1/60 s a frame.
        double now = 0, next = 0, lastFrame = -1, lastMove = double.NegativeInfinity, interval = 1 / 60.0;
        int frames = 0, moves = 0;
        bool pending = false;
        for (int ms = 0; ms < 2000; ms++)
        {
            now = ms / 1000.0;
            pending = true; // Windows reported a move
            if (FrameScheduler.ShouldFastMove(pending, now, lastMove))
            {
                lastMove = now;
                pending = false;
                moves++;
            }
            var (run, n) = FrameScheduler.Decide(now, next, interval, woken: false, lastFrame);
            if (run)
            {
                next = n;
                lastFrame = now;
                frames++;
                pending = false;
            }
        }
        Assert.InRange(frames, 115, 125);   // ~60 fps while dragging
        Assert.InRange(moves, 300, 500);    // and the overlay follows ~250 times a second
    }

    private static readonly IntPtr Target = (IntPtr)0x1234, Other = (IntPtr)0x9999;

    [Fact]
    public void Moving_the_target_moves_the_overlay_directly()
    {
        Assert.Equal(WinEventAction.Moved, WinEventFilter.Classify(Win32.EVENT_OBJECT_LOCATIONCHANGE, Target, Win32.OBJID_WINDOW, 0, Target));
    }

    [Theory]
    [InlineData(Win32.EVENT_SYSTEM_MINIMIZESTART)]
    [InlineData(Win32.EVENT_SYSTEM_MINIMIZEEND)]
    [InlineData(Win32.EVENT_OBJECT_DESTROY)]
    [InlineData(Win32.EVENT_OBJECT_HIDE)]
    [InlineData(Win32.EVENT_OBJECT_CLOAKED)]
    [InlineData(Win32.EVENT_OBJECT_UNCLOAKED)]
    public void Other_changes_to_the_target_wake_the_loop(uint eventId)
    {
        Assert.Equal(WinEventAction.Wake, WinEventFilter.Classify(eventId, Target, Win32.OBJID_WINDOW, 0, Target));
    }

    [Fact]
    public void Any_foreground_change_wakes_the_loop()
    {
        Assert.Equal(WinEventAction.Wake, WinEventFilter.Classify(Win32.EVENT_SYSTEM_FOREGROUND, Other, Win32.OBJID_WINDOW, 0, Target));
    }

    [Theory]
    [InlineData(0x9999, 0, 0)]    // another window of the same app
    [InlineData(0x1234, -9, 0)]   // the caret, the cursor... inside the target
    [InlineData(0x1234, 0, 5)]    // a child element
    public void Everything_else_is_ignored(int hwnd, int idObject, int idChild)
    {
        Assert.Equal(WinEventAction.Ignore,
            WinEventFilter.Classify(Win32.EVENT_OBJECT_LOCATIONCHANGE, (IntPtr)hwnd, idObject, idChild, Target));
        Assert.Equal(WinEventAction.Ignore,
            WinEventFilter.Classify(Win32.EVENT_OBJECT_LOCATIONCHANGE, Target, 0, 0, IntPtr.Zero)); // no target yet
    }

    [Fact]
    public void Stats_count_frames_in_the_last_second_and_smooth_costs()
    {
        var m = new StatsMeter();
        for (int i = 0; i < 120; i++)
            m.Frame(i / 60.0);            // two seconds at 60 fps
        Assert.InRange(m.FramesPerSecond(119 / 60.0), 59, 61);
        m.Draw(10);
        m.Draw(0);                        // smoothed, not jumping to 0
        var s = m.Snapshot(2);
        Assert.InRange(s.DrawMs, 8, 10);
        Assert.Equal(-1, s.SignalLatencyMs); // nothing measured yet
    }

    [Fact]
    public void Stats_line_for_the_card()
    {
        Assert.Equal("60 fps · dibujo 3.1 ms · presentar 0.9 ms",
            new AttachedHostStats(60, 3.14, 0.9, -1, -1, -1).Format());
        Assert.Equal("15 fps · dibujo 2.0 ms · presentar 1.0 ms · señal 4 ms · tecla 2 ms · mover 1 ms",
            new AttachedHostStats(15, 2, 1, 4.2, 2, 1).Format());
    }

    [Fact]
    public void A_file_watcher_reports_a_write_in_milliseconds()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dana-watch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "shell-signal.txt");
        using var changed = new ManualResetEventSlim(false);
        using var watcher = new FileSignalWatcher(file, () => changed.Set());

        var sw = System.Diagnostics.Stopwatch.StartNew();
        File.WriteAllText(file, "error 2 1");
        Assert.True(changed.Wait(TimeSpan.FromSeconds(2)), "no notification within 2 s");
        Assert.True(sw.ElapsedMilliseconds < 1000, $"took {sw.ElapsedMilliseconds} ms");
    }
}
