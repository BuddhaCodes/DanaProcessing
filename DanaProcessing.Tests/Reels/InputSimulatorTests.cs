using System.Runtime.CompilerServices;
using DanaProcessing.Reels;
using Xunit;

namespace DanaProcessing.Tests.Reels;

public class InputSimulatorTests
{
    public class Clicky : Sketch
    {
        public int Presses, Clicks, Drags, Moves, Keys;
        public char LastKey;
        public override void Draw() { }
        public override void MousePressed() => Presses++;
        public override void MouseClicked() => Clicks++;
        public override void MouseDragged() => Drags++;
        public override void MouseMoved() => Moves++;
        public override void KeyPressed() { Keys++; LastKey = Key; }
    }

    public class Passive : Sketch { public override void Draw() { } }

    // The simulator only touches input state and callbacks, so the sketch's
    // drawing setup (and the native Skia library behind it) isn't needed.
    private static T Bare<T>() where T : Sketch => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static void Play(InputSimulator sim, Sketch s, double seconds, int fps = 60)
    {
        double prev = 0;
        for (int f = 0; f <= seconds * fps; f++)
        {
            double t = f / (double)fps;
            sim.Apply(s, prev, t);
            prev = t;
            s.PMouseX = s.MouseX;
            s.PMouseY = s.MouseY;
        }
    }

    private static ReelScript Scripted() => ReelScript.Parse(string.Join("\n",
        "// @reel move 10,10 -> 200,200 -> 300,50 at=1 dur=2",
        "// @reel drag 100,100 -> 400,300 at 3s over 1.5s",
        "// @reel click 50,60 at=5",
        "// @reel key space at=6"));

    [Fact]
    public void Scripted_events_fire_like_a_real_host()
    {
        var c = Bare<Clicky>();
        var sim = new InputSimulator(Scripted(), typeof(Clicky), 600, 600, 8);
        Play(sim, c, 8);
        Assert.Equal(2, c.Presses);       // drag + click
        Assert.Equal(1, c.Clicks);        // only the click, not the drag
        Assert.True(c.Drags > 30);
        Assert.True(c.Moves > 30);
        Assert.Equal(1, c.Keys);
        Assert.Equal(' ', c.LastKey);
    }

    [Fact]
    public void Scripted_path_is_followed()
    {
        var sim = new InputSimulator(Scripted(), typeof(Clicky), 600, 600, 8);
        var mid = sim.PositionAt(2.0);
        Assert.Equal(mid.X, mid.Y, 2);    // still on the first, diagonal leg
        Assert.True(mid.X > 100);
        Assert.Equal((300f, 50f), sim.PositionAt(3.0));
        Assert.Equal((10f, 10f), sim.PositionAt(0));
        Assert.Equal((50f, 60f), sim.PositionAt(7.9));
        Assert.True(sim.PressedAt(3.5));
        Assert.False(sim.PressedAt(4.6));
    }

    [Fact]
    public void Auto_mode_clicks_and_drags_inside_the_canvas()
    {
        var c = Bare<Clicky>();
        var sim = new InputSimulator(ReelScript.Parse("// @reel result 8"), typeof(Clicky), 600, 400, 8);
        Assert.True(sim.HasMouse);
        float minX = float.MaxValue, maxX = float.MinValue;
        double prev = 0;
        for (int f = 0; f <= 480; f++)
        {
            double t = f / 60.0;
            sim.Apply(c, prev, t);
            prev = t;
            minX = Math.Min(minX, c.MouseX);
            maxX = Math.Max(maxX, c.MouseX);
        }
        Assert.Equal(2, c.Clicks);
        Assert.True(c.Presses >= 3);
        Assert.True(c.Drags > 20);
        Assert.InRange(minX, 0, 600);
        Assert.InRange(maxX, 0, 600);
        Assert.True(maxX - minX > 250);
    }

    [Fact]
    public void No_cursor_for_sketches_that_ignore_the_mouse_or_when_turned_off()
    {
        Assert.False(new InputSimulator(ReelScript.Parse(""), typeof(Passive), 600, 400, 8).HasMouse);
        Assert.False(new InputSimulator(ReelScript.Parse("// @reel mouse off"), typeof(Clicky), 600, 400, 8).HasMouse);
    }

    [Theory]
    [InlineData("space", ' ', ' ')]
    [InlineData("up", Sketch.CODED, Sketch.UP)]
    [InlineData("enter", Sketch.ENTER, 10)]
    [InlineData("a", 'a', 'A')]
    public void Key_names_map_to_processing_keys(string name, char key, int code) =>
        Assert.Equal((key, code), InputSimulator.MapKey(name));
}
