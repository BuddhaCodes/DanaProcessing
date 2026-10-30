using DanaProcessing.Tests.Support;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>Text after moving off SkiaSharp's obsolete paint-based text API: same metrics behavior, still drawn.</summary>
public class TextOnSkFontTests
{
    private sealed class Probe : Sketch
    {
        public override void Draw() { }
    }

    private static Probe Ready()
    {
        var sketch = new Probe();
        Offscreen.Run(sketch);
        return sketch;
    }

    [Fact]
    public void TextWidth_scales_with_TextSize()
    {
        var s = Ready();
        s.TextSize(16);
        float small = s.TextWidth("Hola mundo");
        s.TextSize(32);
        float big = s.TextWidth("Hola mundo");

        Assert.True(small > 0);
        Assert.InRange(big / small, 1.9f, 2.1f);
    }

    [Fact]
    public void TextWidth_of_multiline_text_is_its_widest_line()
    {
        var s = Ready();
        Assert.Equal(s.TextWidth("una línea larga"), s.TextWidth("corta\nuna línea larga\nx"));
    }

    [Fact]
    public void Ascent_and_descent_follow_the_size()
    {
        var s = Ready();
        s.TextSize(20);
        float a20 = s.TextAscent(), d20 = s.TextDescent();
        s.TextSize(40);

        Assert.True(a20 > 0 && d20 > 0);
        Assert.InRange(s.TextAscent() / a20, 1.9f, 2.1f);
    }

    [Fact]
    public void Text_puts_pixels_on_the_canvas()
    {
        var sketch = new Drawn(s =>
        {
            s.Background(0);
            s.Fill(255);
            s.TextSize(60);
            s.Text("W", 20, 80);
        });
        using var surface = Offscreen.Run(sketch);

        int bright = 0;
        for (int x = 0; x < 100; x++)
            for (int y = 0; y < 100; y++)
                if (Offscreen.IsBright(Offscreen.Pixel(surface, x, y))) bright++;
        Assert.True(bright > 100, $"only {bright} bright pixels");
    }

    [Fact]
    public void NoSmooth_text_has_no_partially_covered_pixels()
    {
        var sketch = new Drawn(s =>
        {
            s.NoSmooth();
            s.Background(0);
            s.Fill(255);
            s.TextSize(40);
            s.Text("Ag", 10, 60);
        });
        using var surface = Offscreen.Run(sketch);
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);

        foreach (var pixel in bitmap.Pixels)
            Assert.True(pixel.Red is 0 or 255, $"antialiased pixel {pixel}");
    }

    private sealed class Drawn : Sketch
    {
        private readonly Action<Sketch> _draw;
        public Drawn(Action<Sketch> draw) => _draw = draw;
        public override void Setup() => Size(100, 100);
        public override void Draw() => _draw(this);
    }
}

/// <summary>Sketch lifetime fixes: Dispose is safe, and Thread() failures are no longer silent.</summary>
public class SketchLifetimeTests
{
    private sealed class Plain : Sketch
    {
        public override void Draw() => Background(0);
    }

    [Fact]
    public void Dispose_is_safe_and_idempotent_for_a_2D_sketch()
    {
        var sketch = new Plain();
        Offscreen.Run(sketch);
        sketch.Dispose();
        sketch.Dispose();
    }

    private sealed class Crashy : Sketch
    {
        public override void Draw() { }
        private void Work() => throw new InvalidOperationException("background boom");
    }

    [Fact]
    public void Exception_in_Thread_method_is_logged()
    {
        var original = Console.Out;
        var captured = new StringWriter();
        var sync = TextWriter.Synchronized(captured);
        Console.SetOut(sync);
        try
        {
            new Crashy().Thread("Work");
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!captured.ToString().Contains("background boom") && DateTime.UtcNow < deadline)
                System.Threading.Thread.Sleep(20);
        }
        finally
        {
            Console.SetOut(original);
        }

        var log = captured.ToString();
        Assert.Contains("background boom", log);
        Assert.Contains("Thread(\"Work\")", log);
        Assert.Contains("InvalidOperationException", log); // the real exception, not TargetInvocationException
    }
}

/// <summary>The SKFont the engine now uses measures exactly like SkiaSharp's old paint-derived one did.</summary>
public class TextMatchesLegacyPaintTests
{
    [Theory]
    [InlineData(17.3f)]
    [InlineData(12f)]
    [InlineData(40f)]
    public void TextWidth_matches_the_old_paint_based_measurement(float size)
    {
#pragma warning disable CS0618 // the obsolete API is exactly what this compares against
        using var legacy = new SKPaint { IsAntialias = true, TextSize = size };
        float expected = legacy.MeasureText("Hola, DanaProcessing!");
#pragma warning restore CS0618
        using var pg = new PGraphics(10, 10);
        pg.BeginDraw();
        pg.TextSize(size);
        Assert.Equal(expected, pg.TextWidth("Hola, DanaProcessing!"), 3);
    }
}
