using DanaProcessing.AvaloniaHost.Attach;
using DanaProcessing.Ide.Export;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// Eco mode, one sketch per window, the status card's badges, shell-signal
/// routing, the tray, and the export check -- everything that doesn't need a desktop.
/// </summary>
public class AttachedRuntimeTests
{
    public class Plain : AttachedSketch
    {
        public override void Draw() { }
    }

    // --- eco ---------------------------------------------------------------------

    [Fact]
    public void Eco_is_on_by_default_at_15_fps_and_can_be_turned_off()
    {
        var s = new Plain();
        Assert.True(s.IsEcoEnabled);
        Assert.Equal(15, s.EcoFrameRate);
        s.EcoMode(fps: 5);
        Assert.Equal(5, s.EcoFrameRate);
        s.NoEco();
        Assert.False(s.IsEcoEnabled);
    }

    [Theory]
    //          enabled focused moving idle  battery covered unfocusedOpt throttle
    [InlineData(true,   false,  false, 3.0, false,  false,  false,       false)]  // not in front but visible (beside another window): full speed
    [InlineData(true,   false,  false, 3.0, false,  true,   false,       true)]   // covered by another window, idle
    [InlineData(true,   false,  false, 1.0, false,  true,   false,       false)]  // covered, but not idle for long enough
    [InlineData(true,   false,  false, 3.0, false,  false,  true,        true)]   // EcoMode(whenUnfocused: true): the old rule
    [InlineData(true,   true,   false, 60,  false,  false,  true,        false)]  // you're looking at it
    [InlineData(true,   false,  true,  60,  false,  true,   true,        false)]  // it's being dragged
    [InlineData(true,   true,   true,  0,   true,   false,  false,       true)]   // low battery wins
    [InlineData(false,  false,  false, 60,  true,   true,   true,        false)]  // eco off: never
    public void Eco_policy(bool enabled, bool focused, bool moving, double idle, bool lowBattery, bool covered, bool whenUnfocused, bool throttle)
    {
        Assert.Equal(throttle, EcoPolicy.Throttle(enabled, focused, moving, idle, lowBattery, covered, whenUnfocused));
    }

    [Fact]
    public void Eco_when_unfocused_is_opt_in()
    {
        var s = new Plain();
        Assert.False(s.IsEcoWhenUnfocused);
        s.EcoMode(whenUnfocused: true);
        Assert.True(s.IsEcoWhenUnfocused && s.IsEcoEnabled);
        s.EcoMode();
        Assert.False(s.IsEcoWhenUnfocused);
    }

    [Theory]
    //           outer               inner              covers
    [InlineData(0, 0, 1920, 1080,    100, 100, 900, 700, true)]    // a maximized window over the terminal
    [InlineData(0, 0, 1920, 1080,    0, 0, 1920, 1080,  true)]     // exactly the same area
    [InlineData(0, 0, 960, 1080,     900, 100, 1500, 700, false)]  // side by side, partly overlapping: still seen
    [InlineData(1920, 0, 3840, 1080, 100, 100, 900, 700, false)]   // another monitor
    [InlineData(0, 0, 1920, 1080,    100, 100, 100, 100, false)]   // empty target
    public void Covered_means_entirely_covered(int ol, int ot, int or, int ob, int il, int it, int ir, int ib, bool covers) =>
        Assert.Equal(covers, DanaProcessing.AvaloniaHost.Attach.WindowTracker.Covers(
            new() { Left = ol, Top = ot, Right = or, Bottom = ob }, new() { Left = il, Top = it, Right = ir, Bottom = ib }));

    [Fact]
    public void Eco_frame_rate_never_speeds_a_sketch_up()
    {
        Assert.Equal(15, EcoPolicy.FrameRate(true, 60, 15));
        Assert.Equal(10, EcoPolicy.FrameRate(true, 10, 15));   // already slower than eco
        Assert.Equal(60, EcoPolicy.FrameRate(false, 60, 15));
        Assert.Equal(240, EcoPolicy.FrameRate(false, 1000, 15));
    }

    [Theory]
    [InlineData(0, 10, true)]     // on battery, 10 %
    [InlineData(0, 40, false)]
    [InlineData(1, 10, false)]    // plugged in
    [InlineData(0, 255, false)]   // no battery
    public void Low_battery(byte ac, byte percent, bool low)
    {
        Assert.Equal(low, Power.IsLow(ac, percent));
    }

    [Fact]
    public void DeltaTime_starts_at_one_frame()
    {
        Assert.Equal(1 / 60f, new Plain().DeltaTime);
    }

    // --- one per window ------------------------------------------------------------

    [Fact]
    public void Front_is_the_default_and_EachWindow_is_asked_for_in_Attach()
    {
        var s = new Plain();
        Assert.Equal(AttachMode.Front, s.Mode);
        Assert.Equal(0, s.InstanceNumber);
        s.Attach(WindowTarget.Terminal, mode: AttachMode.EachWindow);
        Assert.Equal(AttachMode.EachWindow, s.Mode);
        Assert.True(AttachedSketch.MaxInstances >= 4);
    }

    [Fact]
    public void Fleet_reports_unsupported_off_Windows_and_refuses_hot_reload_before_starting()
    {
        using var fleet = new AttachedSketchFleet(new Plain(), connectsAudio: false);
        Assert.False(fleet.HotReload(new Plain()).CanApply);
        if (OperatingSystem.IsWindows())
            return; // the real thing needs a desktop
        fleet.Start();
        Assert.Equal(AttachState.Unsupported, fleet.State);
        Assert.Empty(fleet.Hosts);
    }

    [Fact]
    public void Status_badges_say_what_the_sketch_uses()
    {
        Assert.Empty(AttachedSketchFleet.StatusBadges(AttachMode.Front, 1, false, false, false, false, false));
        Assert.Equal(new[] { "3 VENTANAS", "TECLADO", "RATÓN", "SHELL", "ECO" },
            AttachedSketchFleet.StatusBadges(AttachMode.EachWindow, 3, true, true, true, true, false));
        Assert.Equal(new[] { "PAUSA", "1 VENTANA" },
            AttachedSketchFleet.StatusBadges(AttachMode.EachWindow, 1, false, false, false, false, true));
    }

    // --- shell signal routing ---------------------------------------------------------

    private static SignalRoute W(int target, double focusedAt, double paneFocusedAt = 0, bool owns = false) =>
        new((IntPtr)target, focusedAt, paneFocusedAt, owns);

    [Fact]
    public void A_signal_goes_to_the_window_in_front()
    {
        var listeners = new[] { W(10, 5), W(20, 9), W(30, 1) };
        Assert.Equal(new[] { 2 }, ShellSignalHub.Route(listeners, (IntPtr)30));
    }

    [Fact]
    public void Otherwise_to_the_one_in_front_most_recently_otherwise_to_everyone()
    {
        var listeners = new[] { W(10, 5), W(20, 9) };
        Assert.Equal(new[] { 1 }, ShellSignalHub.Route(listeners, (IntPtr)99));
        var never = new[] { W(10, 0), W(0, 0) };
        Assert.Equal(new[] { 0, 1 }, ShellSignalHub.Route(never, (IntPtr)99));
        Assert.Empty(ShellSignalHub.Route(Array.Empty<SignalRoute>(), IntPtr.Zero));
    }

    [Fact]
    public void In_a_split_terminal_only_the_focused_pane_gets_the_signal()
    {
        // Three panes on window 10 (pane 1 has the keyboard focus), one other window.
        var listeners = new[] { W(10, 5, paneFocusedAt: 2), W(10, 5, paneFocusedAt: 7), W(10, 5, paneFocusedAt: 0), W(20, 9) };
        Assert.Equal(new[] { 1 }, ShellSignalHub.Route(listeners, (IntPtr)10));
        // The terminal isn't in front any more: still its focused pane, not the whole window.
        var later = new[] { W(10, 9, 2), W(10, 9, 7), W(20, 3) };
        Assert.Equal(new[] { 1 }, ShellSignalHub.Route(later, (IntPtr)99));
        // Panes unknown (UI Automation couldn't say): the whole window, as before.
        Assert.Equal(new[] { 0, 1 }, ShellSignalHub.Route(new[] { W(10, 5), W(10, 5), W(20, 1) }, (IntPtr)10));
    }

    [Fact]
    public void A_shell_seen_in_a_pane_keeps_going_to_that_pane()
    {
        // A long command in pane 0 ends after you clicked pane 1: pane 0 owns that shell.
        var listeners = new[] { W(10, 5, 2, owns: true), W(10, 5, 7) };
        Assert.Equal(new[] { 0 }, ShellSignalHub.Route(listeners, (IntPtr)10));

        // Learned only from a signal routed to one listener of the window in front.
        var fresh = new[] { W(10, 5, 2), W(10, 5, 7) };
        Assert.True(ShellSignalHub.ShouldLearnShell(1234, new[] { 1 }, fresh, (IntPtr)10));
        Assert.False(ShellSignalHub.ShouldLearnShell(0, new[] { 1 }, fresh, (IntPtr)10));          // no pid (Abyss Tide)
        Assert.False(ShellSignalHub.ShouldLearnShell(1234, new[] { 1 }, fresh, (IntPtr)99));       // a guess, not in front
        Assert.False(ShellSignalHub.ShouldLearnShell(1234, new[] { 0, 1 }, fresh, (IntPtr)10));    // couldn't tell which pane
    }

    // --- tray and export ---------------------------------------------------------------

    [Fact]
    public void Tray_tooltip_is_short()
    {
        Assert.Equal("Rain", AttachedSketchTray.Tooltip("Rain", AttachState.Attached, "Sobre Windows Terminal"));
        Assert.Equal("Rain — En pausa.", AttachedSketchTray.Tooltip("Rain", AttachState.Paused, "En pausa."));
        Assert.True(AttachedSketchTray.Tooltip("Rain", AttachState.Searching, new string('x', 400)).Length <= 120);
    }

    [Theory]
    [InlineData("public class Rain : AttachedSketch { }", true)]
    [InlineData("public class Rain:AttachedSketch{}", true)]
    [InlineData("public class Rain : Sketch { }", false)]
    [InlineData("// not an AttachedSketchy thing\npublic class A : Sketch { }", false)]
    public void Export_knows_an_attached_sketch_becomes_a_tray_app(string source, bool tray)
    {
        Assert.Equal(tray, ExportOptionsWindow.IsAttachedSketchSource(source));
    }
}
