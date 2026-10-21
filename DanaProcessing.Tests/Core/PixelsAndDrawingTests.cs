using DanaProcessing.Ide.Compilation;
using DanaProcessing.Tests.Support;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// PImage pixels, reading a PGraphics after EndDraw(), rounded rects, point clouds,
/// text size snapping, the optimized-compile option and Random() from other threads.
/// </summary>
public class PixelsAndDrawingTests
{
    private sealed class Drawn : Sketch
    {
        private readonly Action<Sketch> _draw;
        public Drawn(Action<Sketch> draw) => _draw = draw;
        public override void Setup() => Size(100, 100);
        public override void Draw() => _draw(this);
    }

    private static PImage Png(int w, int h, Action<SKCanvas> paint, SKColorType type = SKColorType.Rgba8888)
    {
        using var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var c = new SKCanvas(bmp)) paint(c);
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = data.ToArray();
        if (type == SKColorType.Gray8)
        {
            using var gray = new SKBitmap(new SKImageInfo(w, h, SKColorType.Gray8, SKAlphaType.Opaque));
            using (var c = new SKCanvas(gray)) c.DrawBitmap(bmp, 0, 0);
            using var gimg = SKImage.FromBitmap(gray);
            bytes = gimg.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        }
        return new Drawn(_ => { }).LoadImage(bytes);
    }

    [Fact]
    public void PImage_has_pixels_like_processing()
    {
        var img = Png(4, 2, c => { c.Clear(SKColors.Black); c.DrawPoint(3, 1, new SKPaint { Color = new SKColor(200, 100, 50) }); });
        img.LoadPixels();
        Assert.Equal(8, img.Pixels.Length);
        var p = img.Pixels[1 * 4 + 3];
        Assert.Equal((200, 100, 50, 255), ((int)p.R, (int)p.G, (int)p.B, (int)p.A));
        Assert.Equal(p, img.Get(3, 1));
        Assert.Equal(0, img.Get(9, 9).A);                           // outside: transparent

        img.Pixels[0] = new Color(10, 20, 30);
        img.UpdatePixels();
        Assert.Equal(new Color(10, 20, 30), img.Get(0, 0));
        img.Set(1, 0, new Color(1, 2, 3));
        Assert.Equal(new Color(1, 2, 3), img.Get(1, 0));
    }

    [Fact]
    public void A_grayscale_png_reads_as_gray_colors()
    {
        // Elevation maps and masks are often 8-bit grayscale PNGs.
        var img = Png(2, 1, c => { c.Clear(SKColors.Black); c.DrawPoint(1, 0, new SKPaint { Color = new SKColor(128, 128, 128) }); }, SKColorType.Gray8);
        img.LoadPixels();
        Assert.Equal(0, img.Pixels[0].R);
        Assert.InRange((int)img.Pixels[1].R, 126, 130);
        Assert.Equal(img.Pixels[1].R, img.Pixels[1].G);
    }

    [Fact]
    public void A_PGraphics_can_be_read_after_EndDraw()
    {
        using var pg = new PGraphics(10, 10);
        pg.BeginDraw();
        pg.Background(0);
        pg.NoStroke();
        pg.Fill(255, 0, 0);
        pg.Rect(5, 0, 5, 10);
        pg.EndDraw();
        pg.LoadPixels();                                            // used to throw outside BeginDraw/EndDraw
        Assert.Equal(255, pg.Pixels[2 * 10 + 7].R);
        Assert.Equal(0, pg.Get(1, 1).R);
    }

    [Fact]
    public void Rect_takes_corner_radii_like_processing()
    {
        using var square = Offscreen.Run(new Drawn(s => { s.Background(0); s.NoStroke(); s.Fill(255); s.Rect(10, 10, 80, 80); }));
        using var rounded = Offscreen.Run(new Drawn(s => { s.Background(0); s.NoStroke(); s.Fill(255); s.Rect(10, 10, 80, 80, 20); }));
        using var oneCorner = Offscreen.Run(new Drawn(s => { s.Background(0); s.NoStroke(); s.Fill(255); s.Rect(10, 10, 80, 80, 0, 0, 30, 0); }));
        Assert.True(Offscreen.IsBright(Offscreen.Pixel(square, 11, 11)));
        Assert.True(Offscreen.IsDark(Offscreen.Pixel(rounded, 11, 11)));          // the corner is cut
        Assert.True(Offscreen.IsBright(Offscreen.Pixel(rounded, 50, 11)));        // the edge isn't
        Assert.True(Offscreen.IsBright(Offscreen.Pixel(oneCorner, 11, 11)));      // only bottom-right is round
        Assert.True(Offscreen.IsDark(Offscreen.Pixel(oneCorner, 88, 88)));
    }

    [Fact]
    public void A_point_cloud_draws_in_one_batch_and_hides_the_far_side_of_a_ball()
    {
        var cloud = new PointCloud();
        cloud.Add(0, 0, -5, new Color(255, 0, 0), 1f);     // in front of the ball, facing the camera
        cloud.Add(0, 0, 5, new Color(0, 255, 0), 1f);      // behind it
        cloud.OccluderCenter = new PVector(0, 0, 0);
        cloud.OccluderRadius = 4.9f;
        var view = new View3D { Eye = new PVector(0, 0, -20), Target = new PVector(0, 0, 0), FovY = 40 };
        using var surface = Offscreen.Run(new Drawn(s => { s.Background(0); s.Points(cloud, view); }));
        Assert.Equal(1, cloud.Drawn);
        var middle = Offscreen.Pixel(surface, 50, 50);
        Assert.True(middle.Red > 200 && middle.Green < 50, $"the near point is drawn, red: {middle}");

        // Without the occluder both are drawn, the near one on top (depth sorted).
        cloud.OccluderRadius = 0;
        using var both = Offscreen.Run(new Drawn(s => { s.Background(0); s.Points(cloud, view); }));
        Assert.Equal(2, cloud.Drawn);
        Assert.True(Offscreen.Pixel(both, 50, 50).Red > 200);
    }

    [Fact]
    public void View3D_projects_like_a_camera()
    {
        var view = View3D.Orbit(0, 0, 10);                 // on -z, looking at the origin
        var (x, y, depth) = view.Project(new PVector(0, 0, 0), 200, 100);
        Assert.Equal((100f, 50f, 10f), (x, y, depth));
        var (rx, ry, _) = view.Project(new PVector(1, 1, 0), 200, 100);
        Assert.True(rx > 100 && ry < 50, "x to the right, y up");
        Assert.True(view.Project(new PVector(0, 0, -20), 200, 100).Depth <= 0, "behind the camera");
    }

    [Fact]
    public void TextSizeSnap_rounds_sizes_so_glyphs_can_be_reused()
    {
        using var pg = new PGraphics(10, 10);
        pg.BeginDraw();
        pg.TextSize(17.3f);
        float exact = pg.TextWidth("MMMMMMMMMM");
        pg.TextSize(18);
        float eighteen = pg.TextWidth("MMMMMMMMMM");
        Assert.NotEqual(exact, eighteen, 2);                                  // unsnapped by default
        pg.TextSizeSnap(2);
        pg.TextSize(17.3f);
        Assert.Equal(eighteen, pg.TextWidth("MMMMMMMMMM"), 3);                // 17.3 -> 18
        pg.EndDraw();
    }

    [Fact]
    public void The_optimized_compile_is_opt_in_and_still_runs_the_sketch()
    {
        const string src = """
            public class Sum : Sketch
            {
                public int Total;
                public override void Draw() { for (int i = 1; i <= 100; i++) Total += i; }
            }
            """;
        foreach (bool optimize in new[] { false, true })
        {
            var r = SketchCompiler.Compile(src, optimize: optimize);
            Assert.True(r.Success, string.Join("\n", r.Errors));
            var attrs = r.Sketch!.GetType().Assembly.GetCustomAttributes(typeof(System.Diagnostics.DebuggableAttribute), false);
            bool jitOptimizerOff = attrs.Cast<System.Diagnostics.DebuggableAttribute>().Any(a => a.IsJITOptimizerDisabled);
            Assert.Equal(!optimize, jitOptimizerOff);
            r.Sketch.Draw();
            Assert.Equal(5050, (int)r.Sketch.GetType().GetField("Total")!.GetValue(r.Sketch)!);
        }
        Assert.False(new DanaProcessing.Ide.Theme.RenderingSettings().OptimizeCode);
    }

    [Fact]
    public void Random_can_be_called_from_several_threads()
    {
        // Unsynchronized, System.Random shared between threads breaks and starts returning 0.
        var s = new Drawn(_ => { });
        s.RandomSeed(1);
        Parallel.For(0, 8, _ => { for (int i = 0; i < 200_000; i++) s.Random(1); });
        var after = Enumerable.Range(0, 1000).Select(_ => s.Random(1)).ToList();
        Assert.True(after.Distinct().Count() > 900, "still random after concurrent use");
    }
}
