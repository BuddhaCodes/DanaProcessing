using DanaProcessing.Tests.Support;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>The F3 performance panel: what it measures and how it draws.</summary>
public class StatsTests
{
    public class Busy : Sketch
    {
        public byte[]? Garbage;
        public override void Setup() => Size(300, 200);
        public override void Draw()
        {
            Background(255);
            Garbage = new byte[20_000];              // ~20 KB allocated every frame
            var until = System.Diagnostics.Stopwatch.GetTimestamp() + System.Diagnostics.Stopwatch.Frequency / 500;
            while (System.Diagnostics.Stopwatch.GetTimestamp() < until) { } // ~2 ms of work
        }
    }

    [Fact]
    public void Draw_time_and_allocations_are_measured_every_frame()
    {
        var s = new Busy();
        Offscreen.Run(s, 10);
        Assert.InRange(s.DrawTime, 1.8, 50);
        Assert.False(s.IsStatsVisible);              // measured even while hidden

        var stats = typeof(Sketch).GetProperty("Stats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(s)!;
        double bytes = (double)stats.GetType().GetProperty("AverageAllocatedBytes")!.GetValue(stats)!;
        Assert.InRange(bytes, 20_000, 60_000);
    }

    [Fact]
    public void Fps_comes_from_the_time_between_frames()
    {
        var s = new Busy();
        var surface = Offscreen.Run(s, 1);
        for (int i = 0; i < 6; i++)
        {
            Thread.Sleep(20);
            surface.Canvas.Save();
            s.RenderFrame();
            surface.Canvas.Restore();
        }
        Assert.InRange(s.ActualFrameRate, 20, 50);   // ~22 ms per frame -> ~45 fps
    }

    [Fact]
    public void The_panel_draws_top_left_only_while_shown()
    {
        var s = new Busy();
        var surface = Offscreen.Run(s, 2);
        Assert.True(Offscreen.IsBright(Offscreen.Pixel(surface, 200, 25)));

        s.ShowStats();
        surface.Canvas.Save();
        s.RenderFrame();
        surface.Canvas.Restore();
        Assert.True(s.IsStatsVisible);
        Assert.True(Offscreen.Pixel(surface, 200, 25).Red < 80); // the panel's dark, slightly see-through background

        s.HideStats();
        surface.Canvas.Save();
        s.RenderFrame();
        surface.Canvas.Restore();
        Assert.True(Offscreen.IsBright(Offscreen.Pixel(surface, 200, 25)));
    }

    [Fact]
    public void Toggling_works_like_F3()
    {
        var s = new Busy();
        s.ToggleStats();
        Assert.True(s.IsStatsVisible);
        s.ToggleStats();
        Assert.False(s.IsStatsVisible);
    }
}
