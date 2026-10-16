using DanaProcessing.Tests.Support;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>Real rewind: snapshots of a sketch's state that later frames can't touch, and restoring them.</summary>
public class SketchStateTests
{
    public class Walker
    {
        public float X, Y;
        public List<PVector> Trail = new();
        public Walker? Partner;
    }

    public class World : Sketch
    {
        public int Ticks;
        public List<Walker> Walkers = new();
        public Dictionary<string, int> Counts = new();
        public int[,] Grid = new int[3, 3];
        public (int A, List<int> B) Pair = (1, new List<int> { 1 });
        public PImage? Shared;
        public List<float> Rolls = new();

        public override void Setup()
        {
            Size(100, 100);
            var a = new Walker();
            var b = new Walker { Partner = a };
            a.Partner = b;                          // a cycle
            Walkers.Add(a);
            Walkers.Add(b);
        }

        public override void Draw()
        {
            Ticks++;
            foreach (var w in Walkers)
            {
                w.X += 1;
                w.Trail.Add(new PVector(w.X, w.Y));
            }
            Counts["frames"] = Ticks;
            Grid[Ticks % 3, 1] = Ticks;
            Pair.B.Add(Ticks);
            Rolls.Add(Random(1));
        }
    }

    private static void Frame(Sketch s, SkiaSharp.SKSurface surface)
    {
        surface.Canvas.Save();
        s.RenderFrame();
        surface.Canvas.Restore();
    }

    [Fact]
    public void A_snapshot_is_a_deep_copy_later_frames_cannot_change()
    {
        var s = new World();
        s.ApplySeed(11);
        var surface = Offscreen.Run(s, 5);
        var snap = SketchState.Capture(s)!;
        Assert.Equal(5, snap.FrameCount);

        for (int i = 0; i < 7; i++)
            Frame(s, surface);
        Assert.Equal(12, s.Ticks);

        snap.Restore(s);
        Assert.Equal(5, s.Ticks);
        Assert.Equal(5, s.FrameCount);
        Assert.Equal(5, s.Walkers[0].Trail.Count);
        Assert.Equal(5f, s.Walkers[1].X);
        Assert.Equal(5, s.Counts["frames"]);
        Assert.Equal(5, s.Pair.B.Count - 1);
        Assert.Same(s.Walkers[0], s.Walkers[1].Partner);   // the cycle survives as a cycle
    }

    [Fact]
    public void Running_on_after_a_restore_repeats_the_same_frames()
    {
        var s = new World();
        s.ApplySeed(42);
        var surface = Offscreen.Run(s, 3);
        var snap = SketchState.Capture(s)!;

        for (int i = 0; i < 4; i++)
            Frame(s, surface);
        var firstTime = s.Rolls.Skip(3).ToList();

        snap.Restore(s);
        for (int i = 0; i < 4; i++)
            Frame(s, surface);
        Assert.Equal(firstTime, s.Rolls.Skip(3).ToList());   // the random generator went back too
    }

    [Fact]
    public void A_snapshot_can_be_restored_again_and_again()
    {
        var s = new World();
        var surface = Offscreen.Run(s, 2);
        var snap = SketchState.Capture(s)!;
        for (int round = 0; round < 3; round++)
        {
            Frame(s, surface);
            Frame(s, surface);
            snap.Restore(s);
            Assert.Equal(2, s.Ticks);
            Assert.Equal(2, s.Walkers[0].Trail.Count);
        }
    }

    [Fact]
    public void Native_wrappers_are_shared_not_copied()
    {
        var s = new World();
        Offscreen.Run(s, 1);
        s.Shared = s.LoadImage(CreateTinyPng());
        var snap = SketchState.Capture(s)!;
        var image = s.Shared;
        s.Shared = null;
        snap.Restore(s);
        Assert.Same(image, s.Shared);
    }

    private static byte[] CreateTinyPng()
    {
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(2, 2));
        using var data = surface.Snapshot().Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public class WorldEdited : Sketch
    {
        // "The same sketch after Hot Reload": same fields, new code.
        public int Ticks;
        public List<Walker> Walkers = new();
        public Dictionary<string, int> Counts = new();
        public int[,] Grid = new int[3, 3];
        public (int A, List<int> B) Pair = (1, new List<int> { 1 });
        public PImage? Shared;
        public List<float> Rolls = new();
        public override void Draw() => Ticks += 10;
    }

    [Fact]
    public void A_snapshot_restores_into_the_hot_reloaded_sketch_by_field_name()
    {
        var old = new World();
        Offscreen.Run(old, 4);
        var snap = SketchState.Capture(old)!;

        var edited = new WorldEdited();
        SketchHotReload.Transplant(old, edited);
        edited.Ticks = 99;
        snap.Restore(edited);
        Assert.Equal(4, edited.Ticks);
        Assert.Equal(4, edited.Walkers[0].Trail.Count);
        Assert.Equal(4, edited.FrameCount);
    }

    public class Huge : Sketch
    {
        public List<object> Stuff = Enumerable.Range(0, 300_000).Select(i => (object)new int[1]).ToList();
        public override void Draw() { }
    }

    [Fact]
    public void A_state_too_big_to_copy_gives_up_instead_of_stalling()
    {
        Assert.Null(SketchState.Capture(new Huge()));
    }
}
