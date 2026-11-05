using DanaProcessing.AvaloniaHost;
using DanaProcessing.Tests.Support;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>The canvas's offscreen surface can be replaced while a sketch runs.</summary>
public class CanvasSurfaceTests
{
    private class Dots : Sketch
    {
        public override void Setup() => Size(120, 80);
        public override void Draw()
        {
            Background(10);
            Fill(255, 120, 0);
            Circle(60, 40, 30);
        }
    }

    /// <summary>
    /// A widget moved onto a screen with another scaling changes its
    /// supersampling, which replaces the surface the sketch draws into. The
    /// sketch must follow it: it used to keep drawing into the disposed one,
    /// which crashed the process in native code.
    /// </summary>
    [Fact]
    public void Changing_supersampling_mid_run_keeps_drawing_on_the_new_surface()
    {
        var canvas = new AvaloniaSketchCanvas(new Dots()) { RecordHistory = false };
        using var screen = Offscreen.NewSurface(120, 80);

        foreach (var scale in new[] { 1, 2, 1, 3, 2 })
        {
            canvas.SupersampleScale = scale;
            for (int i = 0; i < 3; i++)
            {
                screen.Canvas.Clear(SKColors.Transparent);
                canvas.PaintSketch(screen.Canvas, 1.0, 120, 80);
            }
            screen.Canvas.Flush();
            var middle = Offscreen.Pixel(screen, 60, 40);
            Assert.True(middle.Red > 200 && middle.Blue < 80, $"supersample {scale}x: the circle is drawn ({middle})");
        }
        canvas.StopSketch();
    }
}
