using System.Xml.Linq;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>Print export: vector recording played back big, band by band; the streaming PNG writer; plotter SVG.</summary>
public class PrintExportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dana_print_" + Guid.NewGuid().ToString("N"));
    private static readonly IReadOnlyDictionary<string, string> NoTweaks = new Dictionary<string, string>();

    public PrintExportTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string PathFor(string name) => Path.Combine(_dir, name);

    // -----------------------------------------------------------------
    // Streaming PNG writer
    // -----------------------------------------------------------------

    [Fact]
    public void The_streaming_writer_makes_a_normal_png_band_by_band()
    {
        const int w = 300, h = 170;
        var rgba = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                rgba[i] = (byte)x; rgba[i + 1] = (byte)y; rgba[i + 2] = (byte)(x ^ y); rgba[i + 3] = (byte)(255 - (x % 7));
            }

        var path = PathFor("bands.png");
        using (var file = File.Create(path))
        using (var png = new PngStreamWriter(file, w, h, new[] { new KeyValuePair<string, string>("Comment", "bands") }))
        {
            png.WriteRows(rgba.AsSpan(0, 64 * w * 4), 64);
            png.WriteRows(rgba.AsSpan(64 * w * 4, 64 * w * 4), 64);
            png.WriteRows(rgba.AsSpan(128 * w * 4), h - 128);
        }

        using var decoded = SKBitmap.Decode(path);
        Assert.Equal(w, decoded.Width);
        Assert.Equal(h, decoded.Height);
        using var unpremul = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        decoded.CopyTo(unpremul, SKColorType.Rgba8888);
        foreach (var (x, y) in new[] { (0, 0), (299, 0), (150, 100), (299, 169), (7, 64) })
        {
            int i = (y * w + x) * 4;
            var c = decoded.GetPixel(x, y);
            Assert.Equal((rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]), (c.Red, c.Green, c.Blue, c.Alpha));
        }
        Assert.Equal("bands", PngText.Read(File.ReadAllBytes(path))["Comment"]);
    }

    // -----------------------------------------------------------------
    // Vector PNG export
    // -----------------------------------------------------------------

    public class Poster : Sketch
    {
        [Tweak(10, 80)] public float Radius = 40;
        public override void Setup() => Size(200, 120);
        public override void Draw()
        {
            Background(255);
            NoStroke();
            Fill(200, 30, 30);
            Circle(100, 60, Radius * 2);
            Fill(0);
            TextSize(14);
            Text("print", 10, 110);
        }
    }

    [Fact]
    public void A_png_at_4x_matches_drawing_the_sketch_at_4x_directly()
    {
        var path = PathFor("poster.png");
        var result = PrintExport.SavePng(() => new Poster(), seed: 5, NoTweaks, frames: 1, scale: 4, path);

        Assert.True(result.Vector);
        Assert.Null(result.Warning);
        Assert.Equal((800, 480), (result.Width, result.Height));

        // Ground truth: the same sketch drawn straight into a 4x canvas.
        var truth = new Poster();
        truth.HideTweaks(); // the export leaves the [Tweak] panel out too
        using var surface = SKSurface.Create(new SKImageInfo(800, 480, SKColorType.Rgba8888, SKAlphaType.Premul));
        truth.SetCanvas(surface.Canvas, surface);
        truth.Setup();
        surface.Canvas.Scale(4);
        truth.RenderFrame();
        using var expected = SKBitmap.FromImage(surface.Snapshot());
        using var actual = SKBitmap.Decode(path);

        int different = 0;
        for (int y = 0; y < 480; y += 3)
            for (int x = 0; x < 800; x += 3)
            {
                var a = actual.GetPixel(x, y);
                var e = expected.GetPixel(x, y);
                if (Math.Abs(a.Red - e.Red) + Math.Abs(a.Green - e.Green) + Math.Abs(a.Blue - e.Blue) > 30)
                    different++;
            }
        Assert.True(different < 20, $"{different} pixels differ");

        // Edges stay sharp: right next to the circle's edge it's already pure red or pure white.
        var inside = actual.GetPixel(400, 240 - 4 * 40 + 6);
        var outside = actual.GetPixel(400, 240 - 4 * 40 - 6);
        Assert.Equal((200, 30, 30), ((int)inside.Red, (int)inside.Green, (int)inside.Blue));
        Assert.Equal((255, 255, 255), ((int)outside.Red, (int)outside.Green, (int)outside.Blue));
    }

    [Fact]
    public void The_print_png_carries_seed_tweaks_and_size()
    {
        var path = PathFor("meta.png");
        PrintExport.SavePng(() => new Poster(), 777, new Dictionary<string, string> { ["Radius"] = "25f" }, 1, 2, path);
        var info = SketchImageInfo.Read(path)!;
        Assert.Equal(777, info.Seed);
        Assert.Equal("25f", info.Tweaks["Radius"]);
        Assert.Equal("400x240", PngText.Read(File.ReadAllBytes(path))["DanaProcessing.PrintSize"]);

        using var decoded = SKBitmap.Decode(path);
        Assert.Equal(new SKColor(255, 255, 255), decoded.GetPixel(200, 120 - 2 * 25 - 4)); // smaller circle: 25, not 40
    }

    public class Trail : Sketch
    {
        public override void Setup()
        {
            Size(100, 40);
            Background(255);
        }
        public override void Draw()
        {
            NoStroke();
            Fill(0);
            Circle(5 + (FrameCount - 1) * 10, 20, 6); // never clears: frames pile up
        }
    }

    [Fact]
    public void Every_frame_up_to_the_chosen_one_is_drawn()
    {
        var path = PathFor("trail.png");
        PrintExport.SavePng(() => new Trail(), 1, NoTweaks, frames: 8, scale: 2, path);
        using var image = SKBitmap.Decode(path);
        for (int f = 0; f < 8; f++)
            Assert.Equal(SKColors.Black, image.GetPixel((5 + f * 10) * 2, 40));
        Assert.Equal(SKColors.White, image.GetPixel((5 + 8 * 10) * 2, 40)); // frame 9 never ran
    }

    [Fact]
    public void Posters_bigger_than_one_band_stream_through()
    {
        var path = PathFor("big.png");
        var result = PrintExport.SavePng(() => new Poster(), 1, NoTweaks, 1, scale: 12, path); // 2400 x 1440: 3 bands
        Assert.Equal((2400, 1440), (result.Width, result.Height));
        using var image = SKBitmap.Decode(path);
        Assert.Equal(new SKColor(200, 30, 30), image.GetPixel(1200, 720));
        Assert.Equal(SKColors.White, image.GetPixel(10, 10));
    }

    public class PixelReader : Sketch
    {
        public override void Setup() => Size(60, 40);
        public override void Draw()
        {
            Background(0, 0, 255);
            var c = Get(5, 5);           // reads pixels back: can't be a vector recording
            Fill(c.R, c.G, c.B);
            Rect(10, 10, 20, 20);
        }
    }

    [Fact]
    public void A_sketch_that_reads_pixels_is_exported_as_pixels_and_says_so()
    {
        var path = PathFor("pixels.png");
        var result = PrintExport.SavePng(() => new PixelReader(), 1, NoTweaks, 1, 3, path);
        Assert.False(result.Vector);
        Assert.NotNull(result.Warning);
        Assert.Equal((180, 120), (result.Width, result.Height));
        using var image = SKBitmap.Decode(path);
        Assert.Equal(new SKColor(0, 0, 255), image.GetPixel(60, 60)); // the square took Get()'s blue
        Assert.Equal(new SKColor(0, 0, 255), image.GetPixel(10, 10));
    }

    // -----------------------------------------------------------------
    // SVG + plotter
    // -----------------------------------------------------------------

    [Fact]
    public void Svg_export_is_real_vectors()
    {
        var path = PathFor("poster.svg");
        var result = PrintExport.SaveSvg(() => new Poster(), 1, NoTweaks, 1, path);
        var doc = XDocument.Load(path);
        Assert.Equal("svg", doc.Root!.Name.LocalName);
        Assert.Contains(doc.Descendants(), e => e.Name.LocalName is "ellipse" or "circle" or "path");
        Assert.Equal((200, 120), (result.Width, result.Height));
    }

    public class Hatching : Sketch
    {
        public override void Setup() => Size(400, 300);
        public override void Draw()
        {
            Background(255);
            StrokeWeight(1);
            // Lines in a deliberately bad order: far left, far right, far left...
            for (int i = 0; i < 20; i++)
            {
                float x = i % 2 == 0 ? 10 + i * 2 : 380 - i * 2;
                Stroke(i < 10 ? 0 : 255, 0, 0);
                Line(x, 20, x, 280);
            }
            NoStroke();
            Fill(0, 0, 255);
            Rect(150, 100, 50, 50);   // a filled square becomes an outline
        }
    }

    [Fact]
    public void Plotter_svg_drops_the_background_groups_pens_and_shortens_travel()
    {
        var path = PathFor("plot.svg");
        var result = PrintExport.SaveSvg(() => new Hatching(), 1, NoTweaks, 1, path, new PlotterOptions());
        var doc = XDocument.Load(path);
        var layers = doc.Root!.Elements().Where(e => e.Name.LocalName == "g").ToList();

        Assert.Equal(3, layers.Count);                                   // black, red, blue pens
        Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName == "rect" && e.Attribute("width")?.Value == "400");
        Assert.All(doc.Descendants().Where(e => e.Name.LocalName is "path" or "rect" or "line"),
                   e => Assert.Equal("none", e.Attribute("fill")?.Value));
        Assert.Contains("Plotter:", result.Warning);

        var plotter = PlotterSvg.Optimize(File.ReadAllText(PathFor("plot.svg")).Replace("pen-", "x-"));
        Assert.True(plotter.TravelAfter <= plotter.TravelBefore);
    }

    [Fact]
    public void Nearest_neighbour_ordering_and_flipping_cut_the_pen_up_distance()
    {
        // Ten vertical lines drawn left-right-left-right, all going down.
        var lines = string.Join("\n", Enumerable.Range(0, 10).Select(i =>
        {
            int x = i % 2 == 0 ? i * 5 : 500 - i * 5;
            return $"<path fill=\"none\" stroke=\"black\" d=\"M{x} 0L{x} 300\"/>";
        }));
        string svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"500\" height=\"300\">{lines}</svg>";

        var result = PlotterSvg.Optimize(svg);
        Assert.Equal(10, result.Elements);
        Assert.Equal(1, result.Pens);
        Assert.True(result.TravelAfter < result.TravelBefore / 3, $"{result.TravelBefore:0} -> {result.TravelAfter:0}");

        // Flipped lines go up: consecutive lines start where the previous one ended (zig-zag).
        var ds = XDocument.Parse(result.Svg).Descendants().Where(e => e.Name.LocalName == "path").Select(e => e.Attribute("d")!.Value).ToList();
        Assert.Contains(ds, d => d.EndsWith(" 0"));
    }
}
