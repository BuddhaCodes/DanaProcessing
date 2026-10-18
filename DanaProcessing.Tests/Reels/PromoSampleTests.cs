using DanaProcessing.Ide.Compilation;
using DanaProcessing.Ide.Editor;
using DanaProcessing.Reels;
using DanaProcessing.Tests.Support;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Reels;

/// <summary>
/// The promo example: everything a 60-second promo needs, in one sketch you
/// can open from Examples and export with Sketch > Create reel -- 16:9, full
/// frame, with its own soundtrack. (The whole export is checked by rendering it
/// with the real exporter; here, the parts that can break quietly.)
/// </summary>
public class PromoSampleTests
{
    private static SketchSample Promo => SketchSamples.All.Single(s => s.Source.Contains("class DanaPromo"));

    [Fact]
    public void It_is_a_16_by_9_full_frame_reel_with_sound_and_nothing_else()
    {
        var script = ReelScript.Parse(Promo.Source);
        Assert.Empty(script.Warnings);
        Assert.True(script.ImportsModule);
        Assert.Equal(ReelFormat.Landscape, script.Format);
        Assert.True(script.ResultFull);
        Assert.Equal(61, script.ResultSeconds);          // 60 s of picture + the last chord ringing out
        Assert.True(script.RecordsAudio);            // its [LiveLoop]s are hidden from the reel, so audio: on says it
        Assert.Equal("", script.Outro);              // its own outro, not the default card
        Assert.Equal(ReelMouseMode.None, script.Mouse);
        Assert.Empty(script.Scenes);                 // no typed-code cards: the sketch draws its own
        Assert.Contains(SampleTag.Reels, Promo.Tags);
    }

    [Fact]
    public void It_compiles_and_its_first_frame_fills_the_video()
    {
        var result = SketchCompiler.Compile(Promo.Source);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        var type = result.Sketch!.GetType();
        using var timeline = ReelTimeline.Build(ReelScript.Parse(Promo.Source), () => (Sketch)Activator.CreateInstance(type)!);
        Assert.Equal(1920, timeline.Width);
        Assert.Equal(61, timeline.Duration, 3);

        using var surface = Offscreen.NewSurface(480, 270);
        surface.Canvas.Scale(0.25f);
        timeline.Render(surface.Canvas, 0.05);
        Assert.Null(timeline.SketchError);
        // The hook starts dark (the plasma fades up): no reel card or theme background around it.
        var corner = Offscreen.Pixel(surface, 2, 2);
        Assert.True(corner.Red < 60 && corner.Green < 60 && corner.Blue < 70, $"corner {corner}");
    }
}
