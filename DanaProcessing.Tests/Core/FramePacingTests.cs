using DanaProcessing.AvaloniaHost;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>Frames on screen follow the display's refreshes, at the sketch's own frame rate.</summary>
public class FramePacingTests
{
    /// <summary>Frames drawn in <paramref name="seconds"/> on a screen refreshing at <paramref name="hz"/>, jittered a little.</summary>
    private static int FramesDrawn(double hz, int fps, double seconds = 10, double jitterMs = 0.8)
    {
        var random = new Random(7);
        double next = 0;
        int frames = 0;
        for (int i = 0; i < hz * seconds; i++)
        {
            double now = i / hz + (random.NextDouble() - 0.5) * jitterMs / 1000;
            var (due, n) = AvaloniaSketchCanvas.PaceFrame(now, next, fps);
            next = n;
            if (due) frames++;
        }
        return frames;
    }

    [Theory]
    [InlineData(60, 60)]
    [InlineData(144, 60)]
    [InlineData(120, 60)]
    [InlineData(60, 30)]
    [InlineData(144, 30)]
    [InlineData(165, 24)]
    public void Average_is_the_sketch_frame_rate(double hz, int fps)
    {
        int frames = FramesDrawn(hz, fps);
        Assert.InRange(frames, fps * 10 - 3, fps * 10 + 3);
    }

    [Fact]
    public void Never_faster_than_the_display()
    {
        Assert.InRange(FramesDrawn(60, 120), 597, 600);
    }

    [Fact]
    public void A_stall_restarts_the_schedule_instead_of_bursting()
    {
        var (due, next) = AvaloniaSketchCanvas.PaceFrame(0, 0, 60);
        Assert.True(due);
        (due, next) = AvaloniaSketchCanvas.PaceFrame(2.0, next, 60);   // two seconds frozen
        Assert.True(due);
        (due, _) = AvaloniaSketchCanvas.PaceFrame(2.0 + 1 / 240.0, next, 60);   // the very next refresh
        Assert.False(due);
    }
}
