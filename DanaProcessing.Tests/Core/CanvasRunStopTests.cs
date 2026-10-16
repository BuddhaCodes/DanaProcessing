using DanaProcessing.AvaloniaHost;
using Xunit;
using static DanaProcessing.AvaloniaHost.AvaloniaSketchCanvas;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// When the canvas runs a frame. In the IDE (RunsWhenHidden) a sketch keeps
/// going while you're in the code -- in step with its sound -- and only Stop
/// stops it; an exported app (the default) still rests while minimized.
/// </summary>
public class CanvasRunStopTests
{
    private static FrameAction Decide(bool visible, bool runsWhenHidden = true, bool looping = true, bool crashed = false,
                                      bool paused = false, bool frozen = false, bool stopped = false) =>
        DecideFrame(looping, crashed, paused, frozen, stopped, visible, runsWhenHidden);

    [Fact]
    public void A_visible_sketch_renders()
    {
        Assert.Equal(FrameAction.Render, Decide(visible: true));
        Assert.Equal(FrameAction.Render, Decide(visible: true, runsWhenHidden: false));
    }

    [Fact]
    public void Hidden_it_keeps_running_in_the_IDE_but_rests_in_an_exported_app()
    {
        Assert.Equal(FrameAction.RunHidden, Decide(visible: false));
        Assert.Equal(FrameAction.None, Decide(visible: false, runsWhenHidden: false));
    }

    [Fact]
    public void Stop_wins_over_everything()
    {
        Assert.Equal(FrameAction.None, Decide(visible: true, stopped: true));
        Assert.Equal(FrameAction.None, Decide(visible: false, stopped: true));
        Assert.Equal(FrameAction.None, Decide(visible: true, crashed: true, stopped: true));
    }

    [Fact]
    public void Paused_rewound_and_NoLoop_draw_nothing()
    {
        Assert.Equal(FrameAction.None, Decide(visible: true, paused: true));
        Assert.Equal(FrameAction.None, Decide(visible: true, frozen: true));
        Assert.Equal(FrameAction.None, Decide(visible: true, looping: false));
    }

    [Fact]
    public void A_crash_overlay_repaints_only_when_it_can_be_seen()
    {
        Assert.Equal(FrameAction.Render, Decide(visible: true, looping: false, crashed: true));
        Assert.Equal(FrameAction.None, Decide(visible: false, crashed: true));
    }
}
