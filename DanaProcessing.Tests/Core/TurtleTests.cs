using DanaProcessing.Tests.Support;
using Xunit;

namespace DanaProcessing.Tests.Core;

public class TurtleTests
{
    private static void Near(float expected, float actual, string what, float tol = 0.01f) =>
        Assert.True(MathF.Abs(expected - actual) <= tol, $"{what}: expected {expected}, got {actual}");

    [Fact]
    public void Starts_facing_up_and_forward_goes_up()
    {
        var t = new Turtle(100, 100);
        t.Forward(50);
        t.Finish();
        var line = Assert.Single(t.Lines);
        Near(100, line.X1, "x1"); Near(100, line.Y1, "y1");
        Near(100, line.X2, "x2"); Near(50, line.Y2, "y2 (up = smaller y)");
    }

    [Fact]
    public void Turning_right_90_then_forward_goes_right()
    {
        var t = new Turtle(0, 0);
        t.TurnRight(90);
        t.Forward(30);
        t.Finish();
        var line = Assert.Single(t.Lines);
        Near(30, line.X2, "x2"); Near(0, line.Y2, "y2");
    }

    [Fact]
    public void A_square_comes_back_to_the_start_with_four_lines()
    {
        var t = new Turtle(200, 200);
        for (int i = 0; i < 4; i++) { t.Forward(100); t.TurnRight(90); }
        Near(200, t.X, "planned x"); Near(200, t.Y, "planned y"); Near(0, t.Heading, "planned heading");
        Assert.False(t.IsDone);
        t.Finish();
        Assert.True(t.IsDone);
        Assert.Equal(4, t.Lines.Count);
        Assert.All(t.Lines, l => Near(100, l.Length, "side", 0.05f));
    }

    [Fact]
    public void Pen_up_moves_without_drawing_and_color_and_size_apply_to_later_lines()
    {
        var t = new Turtle(0, 0);
        t.PenUp(); t.Forward(10); t.PenDown();
        t.PenColor(255, 0, 0); t.PenSize(8);
        t.Forward(10);
        t.Finish();
        var line = Assert.Single(t.Lines);
        Near(-10, line.Y1, "starts where the pen went down");
        Assert.Equal(255, line.Color.R);
        Assert.Equal(8, line.Width);
    }

    [Fact]
    public void Turning_left_and_back_and_home()
    {
        var t = new Turtle(0, 0);
        t.TurnLeft(90); t.Forward(10);   // to (-10, 0)
        t.Back(20);                       // to (10, 0)
        t.Home();
        Near(0, t.X, "x"); Near(0, t.Y, "y"); Near(0, t.Heading, "heading");
        t.Finish();
        Assert.Equal(2, t.Lines.Count);
        Near(10, t.Lines[1].X2, "back walks backwards");
    }

    private sealed class TurtleSketch : Sketch
    {
        public Turtle T = null!;
        public long Now;
        public override void Setup()
        {
            Size(200, 200);
            VirtualMillis = 0; // a clock the test moves by hand
            T = new Turtle(this);
            T.Block = 7; T.Forward(80);
            T.Block = 8; T.TurnRight(90);
        }
        public override void Draw() { Background(255); T.Draw(); }
        public void Tick(long ms) { Now += ms; VirtualMillis = Now; }
    }

    [Fact]
    public void Draw_walks_gradually_and_lights_each_command_s_block()
    {
        var s = new TurtleSketch();
        var surface = Offscreen.Run(s, frames: 1);           // first frame: starts the clock
        Assert.Contains(s.T, s.Turtles);
        Assert.Equal(0, s.LastBlockHit(8));

        s.Tick(100); s.RenderFrame();                         // 0.1 s at 220 px/s = 22 px
        var partial = Assert.Single(s.T.Lines);
        Assert.InRange(partial.Length, 15, 30);
        Assert.NotEqual(0, s.LastBlockHit(7));
        Assert.Equal(0, s.LastBlockHit(8));                   // still walking: the turn hasn't started
        Assert.False(s.T.IsDone);

        for (int i = 0; i < 10; i++) { s.Tick(100); s.RenderFrame(); }
        Assert.True(s.T.IsDone);
        Near(80, Assert.Single(s.T.Lines).Length, "one walk is one line however many frames it took", 0.05f);
        Assert.NotEqual(0, s.LastBlockHit(8));

        surface.Canvas.Flush();
        static bool Inked(SkiaSharp.SKColor c) => c.Red + c.Green + c.Blue < 200;
        Assert.True(Inked(Offscreen.Pixel(surface, 100, 60)), "the line is on the canvas");
        Assert.False(Inked(Offscreen.Pixel(surface, 60, 60)), "and nowhere else");
    }

    [Fact]
    public void Step_by_step_waits_for_a_click_before_each_block()
    {
        var s = new TurtleSketch();
        Offscreen.Run(s, frames: 1);
        var stepper = new BlockStepper();
        try
        {
            // It was already allowed to start the walk before stepping was
            // switched on (frame 1): stepping starts from the next block.
            s.Tick(1000); s.RenderFrame();
            Turtle.Stepper = stepper;
            Assert.Equal(0, s.LastBlockHit(8));

            for (int i = 0; i < 5; i++) { s.Tick(200); s.RenderFrame(); }
            Assert.Equal(0, s.LastBlockHit(8));                 // the turn waits...
            Assert.True(stepper.Waiting);
            Assert.False(s.T.IsDone);
            Assert.Equal(-1, stepper.Current);

            stepper.Step();
            stepper.Step();                                      // a double click is still one step
            for (int i = 0; i < 4; i++) { s.Tick(100); s.RenderFrame(); } // ...until the click
            Assert.NotEqual(0, s.LastBlockHit(8));
            Assert.Equal(8, stepper.Current);
            Assert.False(stepper.Waiting);
            Assert.True(s.T.IsDone);
        }
        finally { Turtle.Stepper = null; }
    }

    [Fact]
    public void Finishing_at_once_ignores_step_by_step()
    {
        var t = new Turtle(0, 0);
        try
        {
            Turtle.Stepper = new BlockStepper();
            t.Block = 3; t.Forward(50);
            t.Finish();
            Assert.True(t.IsDone);
        }
        finally { Turtle.Stepper = null; }
    }

    [Fact]
    public void Slow_motion_factor_slows_every_turtle()
    {
        var s = new TurtleSketch();
        Offscreen.Run(s, frames: 1);
        try
        {
            Turtle.SpeedFactor = 0.25f;
            s.Tick(100); s.RenderFrame();
            Assert.InRange(Assert.Single(s.T.Lines).Length, 3, 8);
        }
        finally { Turtle.SpeedFactor = 1; }
    }

    [Fact]
    public void BlockHit_ignores_negative_ids_and_grows_for_big_ones()
    {
        var s = new TurtleSketch();
        s.BlockHit(-1);
        s.BlockHit(500);
        Assert.NotEqual(0, s.LastBlockHit(500));
        Assert.Equal(0, s.LastBlockHit(499));
        Assert.Equal(0, s.LastBlockHit(100000));
    }
}

public class TurtleHotReloadTests
{
    private sealed class Walker : Sketch
    {
        public Turtle T = null!;
        public override void Setup() { Size(100, 100); T = new Turtle(this); T.Forward(30); }
        public override void Draw() { Background(255); T.Draw(); }
    }

    [Fact]
    public void Hot_reload_moves_the_turtles_to_the_new_sketch()
    {
        var old = new Walker();
        DanaProcessing.Tests.Support.Offscreen.Run(old, frames: 1);
        var fresh = new Walker();
        var plan = SketchHotReload.Analyze(old, fresh);
        Assert.True(plan.CanApply);
        SketchHotReload.Transplant(old, fresh);
        Assert.Same(old.T, fresh.T);                 // same turtle, same walk so far...
        Assert.Same(fresh, fresh.T.Owner);           // ...drawing on the new sketch now
        Assert.Contains(fresh.T, fresh.Turtles);
    }
}
