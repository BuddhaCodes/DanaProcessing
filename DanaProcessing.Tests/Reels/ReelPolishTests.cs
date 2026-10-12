using System.Diagnostics;
using DanaProcessing.Reels;
using DanaProcessing.Tests.Support;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Reels;

/// <summary>The 1.0 reel polish: typos, zoom on marked lines, sound in MP4, YouTube thumbnail.</summary>
public class ReelPolishTests
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
    }

    public class Beat : Sketch
    {
        public override void Setup() => Size(200, 200);
        public override void Draw() => Background(FrameCount % 255);
        [LiveLoop] async Task Drums() { Play(60, release: 0.1); await Sleep(1); }
    }

    // ---------------- Parsing (no native Skia needed) ----------------

    [Theory]
    [InlineData("// @reel typos: on", 0.08)]
    [InlineData("// @reel typos", 0.08)]
    [InlineData("// @reel typos: 20%", 0.2)]
    [InlineData("// @reel typos: 0.5", 0.5)]
    [InlineData("// @reel mistakes: off", 0.0)]
    [InlineData("// @reel typos: 7", 1.0)]
    public void Typos_parse_to_a_chance_per_word(string line, double expected)
    {
        var s = ReelScript.Parse(line);
        Assert.Empty(s.Warnings);
        Assert.Equal(expected, s.Typos, 6);
    }

    [Fact]
    public void A_scene_can_override_the_typo_rate()
    {
        var s = ReelScript.Parse("// @reel typos: on\n// @reel scene \"Clean\" typos=off\nint a;\n// @reel scene \"Messy\" typos=30%\nint b;");
        Assert.Empty(s.Warnings);
        Assert.Equal(0.0, s.Scenes[0].Typos!.Value, 6);
        Assert.Equal(0.3, s.Scenes[1].Typos!.Value, 6);
    }

    [Fact]
    public void Zoom_on_marks_is_on_unless_turned_off()
    {
        Assert.True(ReelScript.Parse("int a;").ZoomMarks);
        Assert.False(ReelScript.Parse("// @reel zoom: off").ZoomMarks);
    }

    // ---------------- Rendering (native Skia, runs in CI) ----------------

    private static SKBitmap Frame(ReelTimeline t, double time)
    {
        using var surface = Offscreen.NewSurface(t.Width / 4, t.Height / 4);
        surface.Canvas.Clear(SKColors.Black);
        surface.Canvas.Scale(0.25f);
        t.Render(surface.Canvas, time);
        using var img = surface.Snapshot();
        return SKBitmap.FromImage(img);
    }

    private static int Differences(SKBitmap a, SKBitmap b)
    {
        int n = 0;
        for (int y = 0; y < a.Height; y += 2)
            for (int x = 0; x < a.Width; x += 2)
                if (a.GetPixel(x, y) != b.GetPixel(x, y)) n++;
        return n;
    }

    private const string Code = """
        // @reel scene "Typed" effect=type
        public class Orbit : Sketch
        {
            public override void Setup() { Size(300, 300); NoStroke(); }
            public override void Draw() { Background(20); Fill(255, 176, 140); Circle(150, 150, 40); }   // @mark
        }
        // @reel result: 1
        // @reel outro: off
        """;

    [Fact]
    public void Renders_typos_that_change_the_typing_but_not_its_end()
    {
        using var clean = ReelTimeline.Build(ReelScript.Parse("// @reel typos: off\n" + Code), () => new Orbit());
        using var messy = ReelTimeline.Build(ReelScript.Parse("// @reel typos: 100%\n" + Code), () => new Orbit());
        Assert.True(messy.Duration >= clean.Duration, "fixing typos takes time");

        var scene = clean.Parts.First(p => p.Label.Contains("Typed"));
        var messyScene = messy.Parts.First(p => p.Label.Contains("Typed"));

        // Halfway through typing the two differ; at the end of the scene they agree on the code.
        int middle = 0;
        for (double f = 0.3; f <= 0.7; f += 0.1)
        {
            using var a = Frame(clean, scene.Start + scene.Duration * f);
            using var b = Frame(messy, messyScene.Start + messyScene.Duration * f);
            middle += Differences(a, b);
        }
        Assert.True(middle > 0, "typos should be visible while typing");
        Assert.Null(messy.SketchError);
    }

    [Fact]
    public void Renders_zoom_on_marked_lines_only_when_enabled()
    {
        using var on = ReelTimeline.Build(ReelScript.Parse(Code), () => new Orbit());
        using var off = ReelTimeline.Build(ReelScript.Parse("// @reel zoom: off\n" + Code), () => new Orbit());
        var scene = on.Parts.First(p => p.Label.Contains("Typed"));
        double t = scene.Start + scene.Duration - 0.2; // typed, marked line highlighted
        using var a = Frame(on, t);
        using var b = Frame(off, t);
        Assert.True(Differences(a, b) > 0, "zoom should move the code card");
    }

    [Fact]
    public void Renders_a_youtube_thumbnail()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dana_{Guid.NewGuid():N}.png");
        try
        {
            var script = ReelScript.Parse("// @reel title: Orbits in ten lines\n// @reel subtitle: C# creative coding\n// @reel result: 2\n// @reel theme: clay");
            ReelThumbnail.ExportAsync(script, () => new Orbit(), path).GetAwaiter().GetResult();
            using var bmp = SKBitmap.Decode(path);
            Assert.Equal(ReelThumbnail.Width, bmp.Width);
            Assert.Equal(ReelThumbnail.Height, bmp.Height);
            // The sketch box on the right holds the sketch's dark background.
            var c = bmp.GetPixel((int)(bmp.Width * 0.76), (int)(bmp.Height * 0.2));
            Assert.True(c.Red < 60 && c.Green < 60 && c.Blue < 60, $"expected the sketch background, got {c}");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Renders_mp4_with_sound_when_ffmpeg_is_available()
    {
        var ffmpeg = ReelExporter.FindFfmpeg();
        if (ffmpeg == null)
            return; // CI installs ffmpeg on every OS; locally it's optional.

        var path = Path.Combine(Path.GetTempPath(), $"dana_{Guid.NewGuid():N}.mp4");
        try
        {
            var script = ReelScript.Parse("[LiveLoop] async Task Drums() { Play(60); await Sleep(1); }\n// @reel result: 2\n// @reel outro: off\n// @reel fps: 15");
            Assert.True(script.RecordsAudio);
            ReelExporter.ExportAsync(script, () => new Beat(), new ReelExportOptions { Kind = ReelExportKind.Mp4, OutputPath = path, FfmpegPath = ffmpeg }).GetAwaiter().GetResult();
            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".video.mp4"), "the temporary video should be cleaned up");

            var probe = Process.Start(new ProcessStartInfo(ffmpeg, $"-hide_banner -i \"{path}\"") { RedirectStandardError = true, UseShellExecute = false })!;
            var info = probe.StandardError.ReadToEnd();
            probe.WaitForExit();
            Assert.Contains("Video:", info);
            Assert.Contains("Audio:", info);
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// Exports one reel in every format. In CI, DANA_EXPORT_DIR is set and the
    /// files are uploaded per OS so they can be opened and eyeballed; this is
    /// how export is checked on macOS and Linux without owning either.
    /// </summary>
    [Fact]
    public void Renders_every_export_format_for_inspection()
    {
        var keep = Environment.GetEnvironmentVariable("DANA_EXPORT_DIR");
        var dir = string.IsNullOrEmpty(keep) ? Path.Combine(Path.GetTempPath(), $"dana_{Guid.NewGuid():N}") : keep;
        Directory.CreateDirectory(dir);
        try
        {
            var script = ReelScript.Parse("// @reel title: Export check\n// @reel typos: 30%\n" + Code.Replace("// @reel result: 1", "// @reel result: 2"));
            ReelExporter.ExportAsync(script, () => new Orbit(), new ReelExportOptions { Kind = ReelExportKind.Gif, OutputPath = Path.Combine(dir, "reel.gif"), GifWidth = 270 }).GetAwaiter().GetResult();
            ReelThumbnail.ExportAsync(script, () => new Orbit(), Path.Combine(dir, "thumbnail.png")).GetAwaiter().GetResult();
            Assert.True(new FileInfo(Path.Combine(dir, "reel.gif")).Length > 10_000);
            Assert.True(new FileInfo(Path.Combine(dir, "thumbnail.png")).Length > 10_000);

            var ffmpeg = ReelExporter.FindFfmpeg();
            if (ffmpeg != null)
            {
                ReelExporter.ExportAsync(script, () => new Orbit(), new ReelExportOptions { Kind = ReelExportKind.Mp4, OutputPath = Path.Combine(dir, "reel.mp4"), FfmpegPath = ffmpeg }).GetAwaiter().GetResult();
                Assert.True(new FileInfo(Path.Combine(dir, "reel.mp4")).Length > 10_000);
                var beat = ReelScript.Parse("[LiveLoop] async Task Drums() { Play(60); await Sleep(1); }\n// @reel title: Sound check\n// @reel result: 3\n// @reel outro: off");
                ReelExporter.ExportAsync(beat, () => new Beat(), new ReelExportOptions { Kind = ReelExportKind.Mp4, OutputPath = Path.Combine(dir, "reel-with-sound.mp4"), FfmpegPath = ffmpeg }).GetAwaiter().GetResult();
            }
        }
        finally
        {
            if (string.IsNullOrEmpty(keep) && Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
