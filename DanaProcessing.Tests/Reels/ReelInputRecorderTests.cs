using DanaProcessing.Reels;
using Xunit;

namespace DanaProcessing.Tests.Reels;

/// <summary>Recorded interaction -> // @reel markers, and back through the real parser.</summary>
public class ReelInputRecorderTests
{
    private static RecordedInput Move(double t, float x, float y) => new(t, RecordedInputKind.Move, x, y);
    private static RecordedInput Down(double t, float x, float y) => new(t, RecordedInputKind.Down, x, y);
    private static RecordedInput Up(double t, float x, float y) => new(t, RecordedInputKind.Up, x, y);

    [Fact]
    public void Moves_clicks_drags_and_keys_become_markers_in_time_order()
    {
        var markers = ReelInputRecorder.ToMarkers(new[]
        {
            Move(0.0, 10, 10), Move(0.5, 100, 50), Move(1.0, 200, 100),
            Down(1.2, 200, 100), Up(1.3, 201, 100),                       // a click
            Down(2.0, 50, 50), Move(2.5, 150, 80), Up(3.0, 300, 120),     // a drag
            new RecordedInput(3.5, RecordedInputKind.Key, 0, 0, "space"),
        });

        Assert.Equal(4, markers.Count);
        Assert.Equal("// @reel move 10,10 -> 100,50 -> 200,100 at=0 dur=1", markers[0]);
        Assert.Equal("// @reel click 200,100 at=1.2", markers[1]);
        Assert.Equal("// @reel drag 50,50 -> 150,80 -> 300,120 at=2 dur=1", markers[2]);
        Assert.Equal("// @reel key space at=3.5", markers[3]);
    }

    [Fact]
    public void Long_paths_are_thinned_but_keep_both_ends()
    {
        var events = Enumerable.Range(0, 100).Select(i => Move(i * 0.05, i * 3, 20)).ToList();
        var marker = Assert.Single(ReelInputRecorder.ToMarkers(events));
        Assert.StartsWith("// @reel move 0,20 -> ", marker);
        Assert.Contains("-> 297,20 at=0 dur=4.95", marker);
        Assert.True(marker.Split("->").Length <= 14);
    }

    [Fact]
    public void Markers_parse_back_into_the_same_input_actions()
    {
        var markers = ReelInputRecorder.ToMarkers(new[]
        {
            Move(0.0, 10, 10), Move(1.0, 200, 100),
            Down(1.5, 200, 100), Up(1.6, 200, 100),
        });
        string source = string.Join("\n", markers) + "\npublic class MySketch : Sketch { public override void Draw() { } }\n";

        var script = ReelScript.Parse(source);

        Assert.DoesNotContain(script.Warnings, w => w.Contains("point") || w.Contains("needs"));
        Assert.Equal(2, script.Input.Count);
        Assert.Contains(script.Input, a => a.Kind == ReelInputKind.Move && a.Points.Count == 2 && a.Duration == 1);
        Assert.Contains(script.Input, a => a.Kind == ReelInputKind.Click && a.At == 1.5);
    }
}
