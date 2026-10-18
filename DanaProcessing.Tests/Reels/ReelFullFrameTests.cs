using DanaProcessing.Reels;
using DanaProcessing.Tests.Support;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Reels;

/// <summary>
/// "// @reel result: N full": the running sketch is the whole video -- what a
/// promo, a music video or a visualizer needs. No header, "Result" pill, card,
/// shadow or watermark; a sketch the size of the format fills every pixel.
/// </summary>
public class ReelFullFrameTests
{
    /// <summary>Paints the whole canvas one flat color per second: easy to check every pixel.</summary>
    public class Flat : Sketch
    {
        private readonly int _w, _h;
        public Flat() : this(1920, 1080) { }
        public Flat(int w, int h) { _w = w; _h = h; }
        public override void Setup() => Size(_w, _h);
        public override void Draw() => Background(Millis() < 1000 ? 200 : 40, 60, 90);
    }

    private static SKColor[] Corners(ReelTimeline t, double time)
    {
        using var surface = Offscreen.NewSurface(t.Width / 4, t.Height / 4);
        surface.Canvas.Scale(0.25f);
        t.Render(surface.Canvas, time);
        int w = t.Width / 4 - 1, h = t.Height / 4 - 1;
        return new[]
        {
            Offscreen.Pixel(surface, 1, 1), Offscreen.Pixel(surface, w - 1, 1),
            Offscreen.Pixel(surface, 1, h - 1), Offscreen.Pixel(surface, w - 1, h - 1),
            Offscreen.Pixel(surface, w / 2, h / 2), Offscreen.Pixel(surface, w / 2, h - 4), // where the pill/watermark would be
        };
    }

    private static void AssertNear(SKColor expected, SKColor actual)
    {
        Assert.InRange(actual.Red, expected.Red - 3, expected.Red + 3);
        Assert.InRange(actual.Green, expected.Green - 3, expected.Green + 3);
        Assert.InRange(actual.Blue, expected.Blue - 3, expected.Blue + 3);
    }

    [Theory]
    [InlineData("// @reel result: 60 full", true)]
    [InlineData("// @reel result: 8 frame=full caption=\"Hi\"", true)]
    [InlineData("// @reel result: 8", false)]
    [InlineData("// @reel result: 8 caption=\"full\"", false)] // the word in a caption isn't the option
    public void The_marker_turns_it_on(string marker, bool full)
    {
        var script = ReelScript.Parse(marker);
        Assert.Empty(script.Warnings);
        Assert.Equal(full, script.ResultFull);
    }

    [Fact]
    public void A_16_by_9_sketch_fills_every_pixel_of_a_landscape_reel_from_the_first_frame()
    {
        var script = ReelScript.Parse("// @reel format: landscape\n// @reel result: 2 full\n// @reel outro: off");
        using var timeline = ReelTimeline.Build(script, () => new Flat());
        Assert.Equal(1920, timeline.Width);
        Assert.Equal(1080, timeline.Height);
        Assert.Single(timeline.Parts);
        Assert.Equal(2, timeline.Duration, 3);

        // Frame 0 is already the sketch (no fade from the theme's background)...
        foreach (var px in Corners(timeline, 0))
            AssertNear(new SKColor(200, 60, 90), px);
        // ...and it keeps running, edge to edge, with nothing drawn over it.
        foreach (var px in Corners(timeline, 1.5))
            AssertNear(new SKColor(40, 60, 90), px);
        Assert.Null(timeline.SketchError);
    }

    [Fact]
    public void A_sketch_of_another_shape_is_letterboxed_on_black_not_stretched()
    {
        var script = ReelScript.Parse("// @reel format: landscape\n// @reel result: 1 full\n// @reel outro: off");
        using var timeline = ReelTimeline.Build(script, () => new Flat(600, 600));
        using var surface = Offscreen.NewSurface(480, 270);
        surface.Canvas.Scale(0.25f);
        timeline.Render(surface.Canvas, 0.5);
        AssertNear(SKColors.Black, Offscreen.Pixel(surface, 10, 135));     // side bar
        AssertNear(new SKColor(200, 60, 90), Offscreen.Pixel(surface, 240, 135)); // the square sketch, centered
    }

    [Fact]
    public void The_normal_result_still_has_its_card_and_pill()
    {
        var script = ReelScript.Parse("// @reel format: landscape\n// @reel result: 2\n// @reel outro: off");
        using var timeline = ReelTimeline.Build(script, () => new Flat());
        var corners = Corners(timeline, 1.5);
        Assert.DoesNotContain(corners.Take(4), px => Math.Abs(px.Red - 40) < 4 && Math.Abs(px.Green - 60) < 4 && Math.Abs(px.Blue - 90) < 4);
    }

    [Fact]
    public void Full_frame_works_after_typed_code_and_before_the_outro()
    {
        var script = ReelScript.Parse("""
            // @reel format: landscape
            // @reel result: 2 full
            // @reel scene "Code first"
            public override void Draw() => Background(40, 60, 90);
            """);
        using var timeline = ReelTimeline.Build(script, () => new Flat());
        Assert.Equal(new[] { "Code", "Result", "Outro" }, timeline.Parts.Select(p => p.Label.StartsWith("Code") || p.Label.StartsWith("Scene") ? "Code" : p.Label).Distinct().ToArray());
        var result = timeline.Parts.First(p => p.Label == "Result");
        foreach (var px in Corners(timeline, result.Start + result.Duration - 0.2))
            AssertNear(new SKColor(40, 60, 90), px);
    }
}
