using DanaProcessing.Reels;
using DanaProcessing.Tests.Support;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Reels;

/// <summary>Actually draws reels (needs the native Skia library): every effect, transition, format and theme.</summary>
public class ReelRenderTests
{
    public class Orbit : Sketch
    {
        public override void Setup() { Size(300, 300); NoStroke(); }
        public override void Draw()
        {
            Background(20);
            Fill(255, 176, 140);
            Circle(150 + Cos(FrameCount * 0.1f) * 80, 150 + Sin(FrameCount * 0.1f) * 80, 40);
        }
        public override void MousePressed() => Background(255);
    }

    public class Crashes : Sketch
    {
        public override void Draw() { if (FrameCount > 3) throw new InvalidOperationException("boom in Draw"); }
    }

    private static string Script(string header) => header + """

        // @reel title: Render test
        // @reel subtitle: every part of a reel
        // @reel result: 2 caption="It runs"
        // @reel scene "Typed" effect=type
        public class Orbit : Sketch
        {
            public override void Setup() { Size(300, 300); }   // @mark
        // @reel scene "Lines" effect=lines transition=slide
            public override void Draw()
            {
                Background(20);
        // @reel caption: A caption that changes
                Circle(150, 150, 40);
            }
        // @reel scene "Fade" effect=fade transition=zoom
        }
        // @reel scene "Instant" effect=instant transition=wipe keep
        // done
        // @reel scene "Just words, no code"
        // @reel click 150,150 at=0.5
        """;

    private static List<SKColor> RenderAll(ReelTimeline t, double step, float scale = 0.25f)
    {
        var samples = new List<SKColor>();
        using var surface = Offscreen.NewSurface((int)(t.Width * scale), (int)(t.Height * scale));
        for (double time = 0; time < t.Duration; time += step)
        {
            surface.Canvas.Clear(SKColors.Black);
            surface.Canvas.Save();
            surface.Canvas.Scale(scale);
            t.Render(surface.Canvas, time);
            surface.Canvas.Restore();
            samples.Add(Offscreen.Pixel(surface, (int)(t.Width * scale / 2), (int)(t.Height * scale / 2)));
        }
        return samples;
    }

    [Theory]
    [InlineData("// @reel format: vertical\n// @reel theme: clay")]
    [InlineData("// @reel format: square\n// @reel theme: dark")]
    [InlineData("// @reel format: landscape\n// @reel theme: light\n// @reel lang: es")]
    public void Every_part_renders_in_every_format_and_theme(string header)
    {
        var script = ReelScript.Parse(Script(header));
        Assert.Empty(script.Warnings);
        using var timeline = ReelTimeline.Build(script, () => new Orbit());
        Assert.InRange(timeline.Duration, 5, 60);
        Assert.Contains(timeline.Parts, p => p.Label == "Title");
        Assert.Contains(timeline.Parts, p => p.Label == "Result");
        Assert.Contains(timeline.Parts, p => p.Label == "Outro");

        var centre = RenderAll(timeline, 0.25);
        Assert.Null(timeline.SketchError);
        Assert.True(centre.Distinct().Count() > 5, "the picture should change over time");
    }

    [Fact]
    public void Seeking_backwards_restarts_the_sketch()
    {
        using var timeline = ReelTimeline.Build(ReelScript.Parse("// @reel result: 2\n// @reel outro: off"), () => new Orbit());
        using var surface = Offscreen.NewSurface(timeline.Width / 4, timeline.Height / 4);
        surface.Canvas.Scale(0.25f);
        timeline.Render(surface.Canvas, 1.5);
        timeline.Render(surface.Canvas, 0.2); // back in time: must not throw or freeze
        timeline.Render(surface.Canvas, 1.9);
        Assert.Null(timeline.SketchError);
    }

    [Fact]
    public void A_crashing_sketch_is_shown_not_thrown()
    {
        using var timeline = ReelTimeline.Build(ReelScript.Parse("// @reel result: 1"), () => new Crashes());
        RenderAll(timeline, 0.2);
        Assert.NotNull(timeline.SketchError);
        Assert.Contains("boom", timeline.SketchError!.Message);
    }

    [Fact]
    public void Gif_export_writes_every_frame()
    {
        var script = ReelScript.Parse("// @reel scene \"x\"\nint x = 1;\n// @reel result: 1\n// @reel outro: off");
        var path = Path.Combine(Path.GetTempPath(), $"dana_{Guid.NewGuid():N}.gif");
        try
        {
            ReelExporter.ExportAsync(script, () => new Orbit(), new ReelExportOptions { Kind = ReelExportKind.Gif, OutputPath = path, GifWidth = 180, GifFps = 10 }).GetAwaiter().GetResult();
            using var timeline = ReelTimeline.Build(script, () => new Orbit());
            var frames = MiniGifDecoder.Decode(File.ReadAllBytes(path), out int w, out int h, out _);
            Assert.Equal(180, w);
            Assert.Equal(320, h);
            Assert.Equal((int)Math.Ceiling(timeline.Duration * 10 - 1e-6), frames.Count);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Png_export_writes_a_frame_sequence()
    {
        var script = ReelScript.Parse("// @reel result: 0.5\n// @reel outro: off\n// @reel fps: 12");
        var dir = Path.Combine(Path.GetTempPath(), $"dana_{Guid.NewGuid():N}");
        try
        {
            ReelExporter.ExportAsync(script, () => new Orbit(), new ReelExportOptions { Kind = ReelExportKind.Png, OutputPath = dir }).GetAwaiter().GetResult();
            Assert.Equal(6, Directory.GetFiles(dir, "frame_*.png").Length);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Millis_follows_video_time_inside_a_reel()
    {
        long seen = -1;
        var probe = new MillisProbe(m => seen = m);
        using var timeline = ReelTimeline.Build(ReelScript.Parse("// @reel result: 3\n// @reel outro: off"), () => probe);
        using var surface = Offscreen.NewSurface(108, 192);
        surface.Canvas.Scale(0.1f);
        timeline.Render(surface.Canvas, 2.0);
        Assert.InRange(seen, 1950, 2000); // video time, whatever the wall clock did
    }

    public class MillisProbe : Sketch
    {
        private readonly Action<long> _report;
        public MillisProbe(Action<long> report) => _report = report;
        public override void Draw() => _report(Millis());
    }
}
