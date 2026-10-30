using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// AttachedSketch's target events, motion and input routing -- the logic the
/// Windows host feeds every frame, driven here by hand with fake readings.
/// </summary>
public class AttachedSketchEventsTests
{
    public class Probe : AttachedSketch
    {
        public readonly List<string> Log = new();
        public override void Draw() { }
        public override void TargetMoved() => Log.Add("moved");
        public override void TargetFocused() => Log.Add("focused");
        public override void TargetBlurred() => Log.Add("blurred");
        public override void TargetShown() => Log.Add("shown");
        public override void TargetHidden() => Log.Add("hidden");
        public override void KeyPressed() => Log.Add($"press {Key}/{KeyCode}");
        public override void KeyTyped() => Log.Add($"typed {Key}");
        public override void KeyReleased() => Log.Add($"release {Key}");
        public override void MousePressed() => Log.Add($"down {MouseButton} {MouseX},{MouseY}");
        public override void MouseReleased() => Log.Add($"up {MouseButton}");
        public override void MouseClicked() => Log.Add($"click {MouseButton}");
        public override void MouseWheel(float delta) => Log.Add($"wheel {delta}");
        public override void MouseMoved() => Log.Add($"move {MouseX},{MouseY}");
    }

    private static readonly Action<Action, string> Run = (a, _) => a();
    private const double Frame = 1 / 60.0;

    private static TargetSample At(int x, int y, bool focused = true) => new(true, focused, x, y, Frame);

    [Fact]
    public void The_starting_state_fires_nothing()
    {
        var s = new Probe();
        s.Track(At(100, 100, focused: true), Run);
        Assert.Empty(s.Log);
        Assert.True(s.IsTargetFocused);
    }

    [Fact]
    public void Focus_changes_fire_focused_and_blurred()
    {
        var s = new Probe();
        s.Track(At(0, 0, focused: false), Run);
        s.Track(At(0, 0, focused: true), Run);
        s.Track(At(0, 0, focused: true), Run);
        s.Track(At(0, 0, focused: false), Run);
        Assert.Equal(new[] { "focused", "blurred" }, s.Log);
        Assert.False(s.IsTargetFocused);
    }

    [Fact]
    public void Hiding_and_showing_fire_once_each_and_stop_the_motion()
    {
        var s = new Probe();
        s.Track(At(0, 0), Run);
        s.Track(At(30, 0), Run);
        Assert.True(s.TargetVelocity.X > 0);

        s.Track(new TargetSample(false, false, 0, 0, Frame), Run);
        s.Track(new TargetSample(false, false, 0, 0, Frame), Run);
        Assert.Equal(0, s.TargetVelocity.X);
        Assert.Equal(0, s.TargetAcceleration.X);

        s.Track(At(500, 500), Run); // reappears elsewhere: no huge jump in velocity
        Assert.Equal(new[] { "moved", "hidden", "shown" }, s.Log);
        Assert.Equal(0, s.TargetVelocity.X);
    }

    [Fact]
    public void Moved_fires_only_when_the_position_changes()
    {
        var s = new Probe();
        s.Track(At(10, 10), Run);
        s.Track(At(10, 10), Run);
        s.Track(At(12, 10), Run);
        s.Track(At(12, 15), Run);
        Assert.Equal(new[] { "moved", "moved" }, s.Log);
    }

    [Fact]
    public void Steady_dragging_converges_to_the_real_speed()
    {
        var s = new Probe();
        // 300 px/s to the right: 5 px per frame at 60 fps.
        for (int i = 0; i < 120; i++)
            s.Track(At(i * 5, 0), Run);
        Assert.InRange(s.TargetVelocity.X, 290, 310);
        Assert.InRange(s.TargetVelocity.Y, -1, 1);
        Assert.True(s.IsTargetMoving);
        Assert.InRange(s.TargetAcceleration.X, -50, 50); // constant speed: no acceleration left
    }

    [Fact]
    public void A_sudden_stop_is_a_strong_backwards_acceleration()
    {
        var s = new Probe();
        int x = 0;
        for (int i = 0; i < 60; i++)
            s.Track(At(x += 10, 0), Run);   // 600 px/s
        for (int i = 0; i < 3; i++)
            s.Track(At(x, 0), Run);         // let go
        Assert.True(s.TargetAcceleration.X < -2000, $"ax = {s.TargetAcceleration.X}");

        for (int i = 0; i < 120; i++)
            s.Track(At(x, 0), Run);         // still for two seconds
        Assert.False(s.IsTargetMoving);
        Assert.InRange(s.TargetAcceleration.X, -1, 1);
    }

    [Fact]
    public void Acceleration_is_clamped_when_the_window_teleports()
    {
        var s = new Probe();
        s.Track(At(0, 0), Run);
        s.Track(At(5000, -5000), Run);
        Assert.Equal(AttachedSketch.TargetAccelerationLimit, s.TargetAcceleration.X);
        Assert.Equal(-AttachedSketch.TargetAccelerationLimit, s.TargetAcceleration.Y);
    }

    [Fact]
    public void Keys_reach_the_sketch_only_after_opting_in()
    {
        var s = new Probe();
        var down = new TargetInput(TargetInputKind.KeyDown, 'a', 65);
        s.DispatchTargetInput(down, Run);
        Assert.Empty(s.Log);

        s.ListenToTargetKeys();
        s.DispatchTargetInput(down, Run);
        Assert.True(s.IsKeyPressed);
        s.DispatchTargetInput(new TargetInput(TargetInputKind.KeyUp, 'a', 65), Run);
        Assert.False(s.IsKeyPressed);
        Assert.Equal(new[] { "press a/65", "typed a", "release a" }, s.Log);
    }

    [Fact]
    public void Coded_keys_are_pressed_but_not_typed()
    {
        var s = new Probe();
        s.ListenToTargetKeys();
        s.DispatchTargetInput(new TargetInput(TargetInputKind.KeyDown, Sketch.CODED, Sketch.UP), Run);
        Assert.Equal(new[] { $"press {Sketch.CODED}/{Sketch.UP}" }, s.Log);
    }

    [Fact]
    public void Clicks_and_the_wheel_reach_the_sketch_only_after_opting_in()
    {
        var s = new Probe();
        var down = new TargetInput(TargetInputKind.MouseDown, Button: MouseButtonKind.Left, X: 40, Y: 30);
        s.DispatchTargetInput(down, Run);
        Assert.Empty(s.Log);

        s.ListenToTargetMouse();
        s.DispatchTargetInput(down, Run);
        Assert.True(s.IsMousePressed);
        s.DispatchTargetInput(new TargetInput(TargetInputKind.MouseUp, Button: MouseButtonKind.Left, X: 41, Y: 30), Run);
        s.DispatchTargetInput(new TargetInput(TargetInputKind.Wheel, Wheel: -1), Run);
        Assert.False(s.IsMousePressed);
        Assert.Equal(new[] { "down Left 40,30", "up Left", "click Left", "wheel -1" }, s.Log);
    }

    [Fact]
    public void The_cursor_fires_mouse_moved_only_inside_the_area()
    {
        var s = new Probe();
        s.ApplyAttachedSize(200, 100, notify: false);
        s.TrackCursor(10, 10, Run);   // first reading: nothing to compare with
        s.TrackCursor(10, 10, Run);   // didn't move
        s.TrackCursor(20, 15, Run);   // moved inside
        s.TrackCursor(500, 15, Run);  // moved, but outside
        Assert.Equal(new[] { "move 20,15" }, s.Log);
        Assert.Equal(500, s.MouseX);  // position is still kept up to date
    }

    [Fact]
    public void Events_go_through_the_hosts_run_wrapper_by_name()
    {
        var s = new Probe();
        var names = new List<string>();
        Action<Action, string> run = (a, name) => names.Add(name);
        s.Track(At(0, 0, focused: false), run);
        s.Track(At(5, 0, focused: true), run);
        Assert.Equal(new[] { "TargetFocused", "TargetMoved" }, names);
    }
}
