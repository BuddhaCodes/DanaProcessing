using DanaProcessing.AvaloniaHost.Attach;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// Smoother frames: on the screen's refresh grid (like Windows Terminal's own
/// swap chain pacing), a count of late frames on the card, and an export in
/// its own terminal profile that only draws while that profile's tab is in front.
/// </summary>
[Collection("UiText language")]
public class AttachedPacingTests
{
    private const double P60 = 1 / 60.0, P144 = 1 / 144.0;

    [Theory]
    [InlineData(1 / 60.0, 1 / 60.0, 1 / 60.0)]      // 60 fps on 60 Hz: every refresh
    [InlineData(1 / 60.0, 1 / 144.0, 2 / 144.0)]    // 60 fps on 144 Hz: every 2nd refresh (72 fps), evenly
    [InlineData(1 / 15.0, 1 / 60.0, 4 / 60.0)]      // eco on 60 Hz
    [InlineData(1 / 240.0, 1 / 60.0, 1 / 60.0)]     // never faster than the screen
    [InlineData(1 / 60.0, 0, 1 / 60.0)]             // no period: as is
    [InlineData(1 / 60.0, 0.5, 1 / 60.0)]           // nonsense period: as is
    public void Interval_snaps_to_whole_refreshes(double interval, double period, double expected) =>
        Assert.Equal(expected, FrameScheduler.SnapToRefresh(interval, period), 9);

    [Fact]
    public void Next_frame_lands_just_after_a_refresh()
    {
        double vblank = 10.0;   // a refresh happened at 10 s
        double next = FrameScheduler.AlignToRefresh(10.020, vblank, P60);
        Assert.Equal(10.0 + 1 * P60 + 0.001, next, 9);   // 10.0177: nearest refresh to 10.020, plus the phase
        // Already on the grid: stays.
        Assert.Equal(10.0 + 3 * P60 + 0.001, FrameScheduler.AlignToRefresh(10.0 + 3 * P60 + 0.001, vblank, P60), 9);
        // A vblank reported from the future (or the past) works the same.
        Assert.Equal(10.0 + 2 * P144 + 0.001, FrameScheduler.AlignToRefresh(10.014, 10.0 + 50 * P144, P144), 9);
        // No usable grid: unchanged.
        Assert.Equal(10.020, FrameScheduler.AlignToRefresh(10.020, double.NaN, P60));
        Assert.Equal(10.020, FrameScheduler.AlignToRefresh(10.020, vblank, 0));
    }

    [Fact]
    public void A_steady_loop_on_the_grid_draws_once_per_refresh()
    {
        // Simulate: a 60 fps target on a 60 Hz screen whose clock is offset from ours.
        double vblank = 0.0042, next = 0, last = double.NaN;
        var gaps = new List<double>();
        for (int i = 0; i < 120; i++)
        {
            double now = next;
            var (_, n) = FrameScheduler.Decide(now, next, P60, woken: false, lastFrame: last);
            next = FrameScheduler.AlignToRefresh(n, vblank, P60);
            if (!double.IsNaN(last)) gaps.Add(now - last);
            last = now;
        }
        Assert.All(gaps.Skip(2), g => Assert.Equal(P60, g, 6));
    }

    [Fact]
    public void Dwm_timing_converts_to_the_host_clock()
    {
        long f = 10_000_000; // 10 MHz QPC
        Assert.True(VBlankClock.Convert(qpcVBlank: 1_000_000, qpcPeriod: 166_667, qpcNow: 1_050_000, nowSeconds: 3.0, frequency: f,
                                        out double vblank, out double period));
        Assert.Equal(0.0166667, period, 6);
        Assert.Equal(3.0 - 0.005, vblank, 9);    // 5 ms before now
        Assert.False(VBlankClock.Convert(0, 166_667, 1, 0, f, out _, out _));
        Assert.False(VBlankClock.Convert(1, 0, 1, 0, f, out _, out _));
        Assert.Equal(292, VBlankClock.TimingInfoSize);   // sizeof(DWM_TIMING_INFO), 1-byte packing
    }

    [Fact]
    public void Late_frames_are_counted_and_shown()
    {
        var meter = new StatsMeter();
        double t = 0;
        for (int i = 0; i < 30; i++) { t += P60; meter.Frame(t, P60); }
        Assert.Equal(0, meter.Snapshot(t).LateFrames);

        t += 0.050; meter.Frame(t, P60);        // a 50 ms hiccup
        t += P60; meter.Frame(t, P60);
        t += 0.034; meter.Frame(t, P60);        // a 34 ms one
        var s = meter.Snapshot(t);
        Assert.Equal(2, s.LateFrames);
        Assert.Equal(50, s.WorstFrameMs, 3);
        Assert.Contains("2 tardíos (peor 50 ms)", s.Format());

        t += 0.8; meter.Frame(t, P60);          // a long pause (eco, hidden) isn't a stutter
        t += 0.5; meter.Frame(t, P60);          // and a second later the hiccups have aged out
        Assert.Equal(0, meter.Snapshot(t).LateFrames);
        Assert.DoesNotContain("tardíos", meter.Snapshot(t).Format());
    }

    [Fact]
    public void Late_frames_in_english()
    {
        bool before = DanaProcessing.UiText.English;
        DanaProcessing.UiText.English = true;
        try { Assert.Contains("3 late (worst 41 ms)", new AttachedHostStats(55, 3, 1, -1, -1, -1, 3, 41).Format()); }
        finally { DanaProcessing.UiText.English = before; }
    }

    [Theory]
    [InlineData("Blobs", "Blobs", true)]                   // our tab in front
    [InlineData("Administrator: Windows PowerShell", "Blobs", false)]   // another tab
    [InlineData("Terminal", "Blobs", true)]                // showTitleInTitlebar off: can't tell, draw
    [InlineData(null, "Blobs", true)]                      // couldn't read it: draw
    [InlineData("anything", null, true)]                   // not a profile export: always draw
    public void Draws_only_while_its_profile_tab_is_in_front(string? title, string? profileTitle, bool draws) =>
        Assert.Equal(draws, TitleGate.Allows(title, profileTitle));

    [Fact]
    public void A_title_change_of_the_target_wakes_the_host()
    {
        var target = new IntPtr(42);
        Assert.Equal(WinEventAction.Wake, WinEventFilter.Classify(Win32.EVENT_OBJECT_NAMECHANGE, target, Win32.OBJID_WINDOW, Win32.CHILDID_SELF, target));
        Assert.Equal(WinEventAction.Ignore, WinEventFilter.Classify(Win32.EVENT_OBJECT_NAMECHANGE, target, 5, 3, target));   // a child's name
    }
}
