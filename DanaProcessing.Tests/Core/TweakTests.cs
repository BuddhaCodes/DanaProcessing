using DanaProcessing.Tests.Support;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>[Tweak] fields: the slider panel, how it shares the mouse with the sketch, and hot reload.</summary>
public class TweakTests
{
    // Panel geometry for a 400-wide sketch (see TweakPanel's constants):
    // left = 400 - 236 - 10 = 154, slider track from x=166 to x=378,
    // row 0 spans y=36..72, row 1 spans y=72..108.
    private const float TrackStart = 166, TrackEnd = 378;

    public class Tweaked : Sketch
    {
        [Tweak(0, 10)] public float Speed = 5;
        [Tweak] public bool On = true;
        public int Presses, Clicks;

        public override void Setup() => Size(400, 300);

        public override void Draw()
        {
            Background(255);
            Translate(100, 100); // left behind on purpose: must not move the panel
        }

        public override void MousePressed() => Presses++;
        public override void MouseClicked() => Clicks++;
    }

    public class TweakedEdited : Sketch
    {
        [Tweak(0, 10)] public float Speed = 5;
        [Tweak] public bool On = true;
        public int Presses, Clicks;

        public override void Setup() => Size(400, 300);
        public override void Draw() => Background(200);
    }

    public class Plain : Sketch
    {
        public override void Setup() => Size(400, 300);
        public override void Draw() => Background(255);
    }

    private static void Click(Sketch s, float x, float y)
    {
        s.MouseX = x;
        s.MouseY = y;
        s.RaiseMousePressed(MouseButtonKind.Left);
        s.RaiseMouseReleased(MouseButtonKind.Left);
        s.RaiseMouseClicked(MouseButtonKind.Left);
    }

    [Fact]
    public void Dragging_a_slider_sets_the_field_and_the_sketch_never_sees_it()
    {
        var s = new Tweaked();
        using var surface = Offscreen.Run(s);

        s.MouseX = TrackEnd;
        s.MouseY = 50;
        s.RaiseMousePressed(MouseButtonKind.Left);
        Assert.Equal(10f, s.Speed, 3);

        s.MouseX = TrackStart;
        s.RaiseMouseMoved();
        Assert.Equal(0f, s.Speed, 3);

        s.RaiseMouseReleased(MouseButtonKind.Left);
        s.RaiseMouseClicked(MouseButtonKind.Left);

        Assert.Equal(0, s.Presses);
        Assert.Equal(0, s.Clicks);
        Assert.False(s.IsMousePressed);
    }

    [Fact]
    public void Checkbox_toggles_and_clicks_outside_the_panel_reach_the_sketch()
    {
        var s = new Tweaked();
        using var surface = Offscreen.Run(s);

        Click(s, 300, 85);  // row 1: the bool
        Assert.False(s.On);
        Assert.Equal(0, s.Clicks);

        Click(s, 20, 200);  // plain canvas
        Assert.Equal(1, s.Presses);
        Assert.Equal(1, s.Clicks);
    }

    [Fact]
    public void Panel_draws_on_top_only_when_there_are_tweaks_and_it_is_not_hidden()
    {
        // (160, 70): inside the panel, left of the slider track.
        using (var surface = Offscreen.Run(new Tweaked()))
            Assert.True(Offscreen.Pixel(surface, 160, 70).Red < 100, "panel should cover the white background");

        using (var surface = Offscreen.Run(new Plain()))
            Assert.Equal(255, Offscreen.Pixel(surface, 160, 70).Red);

        var hidden = new Tweaked();
        hidden.HideTweaks();
        using (var surface = Offscreen.Run(hidden))
            Assert.Equal(255, Offscreen.Pixel(surface, 160, 70).Red);
    }

    [Fact]
    public void Folding_the_panel_keeps_values_and_frees_the_canvas_below()
    {
        var s = new Tweaked();
        using var surface = Offscreen.Run(s);

        Click(s, 200, 20); // header: fold
        Click(s, 300, 85); // where the checkbox was -- now plain canvas
        Assert.True(s.On);
        Assert.Equal(1, s.Clicks);
    }

    [Fact]
    public void Panel_follows_the_sketch_through_a_hot_reload()
    {
        var old = new Tweaked();
        using var surface = Offscreen.Run(old);
        old.Speed = 7;

        var next = new TweakedEdited();
        var plan = SketchHotReload.Analyze(old, next);
        Assert.True(plan.CanApply, string.Join("; ", plan.Reasons));
        SketchHotReload.Transplant(old, next);
        Assert.Equal(7f, next.Speed, 3);

        // The panel was copied over with the base-class fields; it must now
        // drive the NEW type's field, not the old one's.
        next.MouseX = TrackStart;
        next.MouseY = 50;
        next.RaiseMousePressed(MouseButtonKind.Left);
        Assert.Equal(0f, next.Speed, 3);
        Assert.Equal(7f, old.Speed, 3);
    }
}
