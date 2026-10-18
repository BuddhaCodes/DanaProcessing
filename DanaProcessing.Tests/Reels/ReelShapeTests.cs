using DanaProcessing.Ide;
using DanaProcessing.Reels;
using Xunit;

namespace DanaProcessing.Tests.Reels;

/// <summary>The Create reel window's shape picker: every format, labelled with its ratio and pixels, and it really resizes the video.</summary>
public class ReelShapeTests
{
    [Fact]
    public void The_picker_offers_every_format_with_its_ratio_and_size()
    {
        Assert.Equal(Enum.GetValues<ReelFormat>().OrderBy(f => f), ReelWindow.Shapes.OrderBy(f => f));
        Assert.Contains("16:9", ReelWindow.ShapeLabel(ReelFormat.Landscape));
        Assert.Contains("1920×1080", ReelWindow.ShapeLabel(ReelFormat.Landscape));
        Assert.Contains("9:16", ReelWindow.ShapeLabel(ReelFormat.Vertical));
        Assert.Contains("1:1", ReelWindow.ShapeLabel(ReelFormat.Square));
    }

    [Theory]
    [InlineData(ReelFormat.Vertical, 1080, 1920)]
    [InlineData(ReelFormat.Square, 1080, 1080)]
    [InlineData(ReelFormat.Landscape, 1920, 1080)]
    public void Picking_a_shape_changes_the_video_without_touching_the_code(ReelFormat format, int w, int h)
    {
        const string code = "// @reel format: vertical\n// @reel scene \"x\"\nint a = 1;";
        var script = ReelScript.Parse(code);
        script.Format = format;                       // what the picker does
        using var timeline = ReelTimeline.Build(script, null);
        Assert.Equal(w, timeline.Width);
        Assert.Equal(h, timeline.Height);
        Assert.Equal(ReelFormat.Vertical, ReelScript.Parse(code).Format); // the code still says vertical
    }
}
