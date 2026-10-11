using System.Runtime.CompilerServices;
using DanaProcessing.Tests.Support;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>The 1.0 API decisions, pinned down so they can't drift.</summary>
public class ApiTests
{
    private sealed class Drawn : Sketch
    {
        private readonly Action<Sketch> _draw;
        public Drawn(Action<Sketch> draw) => _draw = draw;
        public override void Setup() => Size(100, 100);
        public override void Draw() => _draw(this);
    }

    private static void Bar(Sketch s, Action rotate)
    {
        s.Background(0);
        s.NoStroke();
        s.Fill(255);
        s.Translate(50, 50);
        rotate();
        s.Rect(0, -5, 40, 10); // a bar pointing right, before rotation
    }

    [Fact]
    public void Rotate_takes_radians()
    {
        Sketch? sk = null;
        sk = new Drawn(s => Bar(s, () => s.Rotate(Sketch.HALF_PI)));
        using var surface = Offscreen.Run(sk);
        Assert.True(Offscreen.IsBright(Offscreen.Pixel(surface, 50, 80)), "a quarter turn points the bar down");
        Assert.True(Offscreen.IsDark(Offscreen.Pixel(surface, 80, 50)));
    }

    [Fact]
    public void RotateDegrees_matches_Rotate()
    {
        using var surface = Offscreen.Run(new Drawn(s => Bar(s, () => s.RotateDegrees(90))));
        Assert.True(Offscreen.IsBright(Offscreen.Pixel(surface, 50, 80)));
        Assert.True(Offscreen.IsDark(Offscreen.Pixel(surface, 80, 50)));
    }

    [Fact]
    public void FillHSB_hue_wraps_and_alpha_is_float()
    {
        using var a = Offscreen.Run(new Drawn(s => { s.Background(0); s.NoStroke(); s.FillHSB(370, 100, 100); s.Rect(0, 0, 100, 100); }));
        using var b = Offscreen.Run(new Drawn(s => { s.Background(0); s.NoStroke(); s.FillHSB(10, 100, 100); s.Rect(0, 0, 100, 100); }));
        Assert.Equal(Offscreen.Pixel(a, 50, 50), Offscreen.Pixel(b, 50, 50));

        using var half = Offscreen.Run(new Drawn(s => { s.Background(0); s.NoStroke(); s.FillHSB(0, 0, 100, 127.5f); s.Rect(0, 0, 100, 100); }));
        Assert.InRange(Offscreen.Pixel(half, 50, 50).Red, 120, 136);
    }

    [Fact]
    public void Fill_honours_ColorMode_HSB()
    {
        using var viaMode = Offscreen.Run(new Drawn(s => { s.ColorMode(ColorSpaceMode.HSB, 360, 100, 100); s.Background(0); s.NoStroke(); s.Fill(120, 100, 100); s.Rect(0, 0, 100, 100); }));
        var c = Offscreen.Pixel(viaMode, 50, 50);
        Assert.True(c.Green > 240 && c.Red < 15 && c.Blue < 15, c.ToString());
    }

    [Fact]
    public void PVector_operators_are_pure()
    {
        var a = new PVector(1, 2, 3);
        var b = new PVector(10, 20, 30);
        var sum = a + b;
        Assert.Equal((11f, 22f, 33f), (sum.X, sum.Y, sum.Z));
        Assert.Equal((1f, 2f, 3f), (a.X, a.Y, a.Z));
        var d = b - a;
        Assert.Equal((9f, 18f, 27f), (d.X, d.Y, d.Z));
        Assert.Equal(2f, (a * 2).X);
        Assert.Equal(2f, (2 * a).X);
        Assert.Equal(5f, (b / 2).X);
        Assert.Equal(-1f, (-a).X);
        var p = a;
        p += b;
        Assert.Equal(11f, p.X);
    }

    [Fact]
    public void PVector_random_vectors_are_unit_length()
    {
        for (int i = 0; i < 200; i++)
        {
            Assert.Equal(1f, PVector.Random2D().Mag(), 4);
            var v = PVector.Random3D();
            Assert.Equal(1f, v.Mag(), 4);
        }
    }

    [Fact]
    public void ParseXML_parses_instead_of_recursing()
    {
        var sketch = (Sketch)RuntimeHelpers.GetUninitializedObject(typeof(Drawn));
        var xml = sketch.ParseXML("<root><item id=\"7\"/></root>");
        Assert.NotNull(xml);
        Assert.NotNull(XML.Parse("<a/>"));
    }

    [Fact]
    public void Map_and_constrain_follow_processing()
    {
        var s = (Sketch)RuntimeHelpers.GetUninitializedObject(typeof(Drawn));
        Assert.Equal(50f, s.Map(5, 0, 10, 0, 100));
        Assert.Equal(10f, s.Constrain(42, 0, 10));
        Assert.Equal(Sketch.PI / 2, s.Radians(90), 5);
    }
}
