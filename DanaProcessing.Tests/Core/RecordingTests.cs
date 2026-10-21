using DanaProcessing.Tests.Support;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// BeginRaw()/EndRaw(): the extension picks PDF or SVG, like Processing's PDF and
/// SVG renderers, and an SVG can come out ready for a pen plotter.
/// </summary>
public class RecordingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dana_rec_" + Guid.NewGuid().ToString("N"));

    public RecordingTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private sealed class Poster : Sketch
    {
        private readonly Action<Sketch> _draw;
        public Poster(Action<Sketch> draw) => _draw = draw;
        public override void Setup() => Size(200, 100);
        public override void Draw() => _draw(this);
    }

    private static void Art(Sketch s)
    {
        s.Background(255);
        s.NoFill();
        s.Stroke(200, 0, 0);
        s.Line(10, 10, 190, 10);
        s.Stroke(0, 0, 200);
        s.Line(190, 90, 10, 90);
        s.Stroke(200, 0, 0);
        s.Line(10, 50, 190, 50);
    }

    [Fact]
    public void A_pdf_path_still_writes_a_pdf()
    {
        string path = Path.Combine(_dir, "poster.pdf");
        using var _ = Offscreen.Run(new Poster(s => { s.BeginRaw(path); Art(s); s.EndRaw(); }));
        Assert.StartsWith("%PDF", File.ReadAllText(path)[..4]);
    }

    [Fact]
    public void An_svg_path_writes_vectors_and_the_screen_is_left_alone()
    {
        string path = Path.Combine(_dir, "poster.svg");
        Sketch? sketch = null;
        using var surface = Offscreen.Run(new Poster(s =>
        {
            sketch = s;
            s.Background(0, 255, 0);
            s.BeginRaw(path);
            Assert.True(s.IsRecording);
            Art(s);
            s.EndRaw();
        }));
        Assert.False(sketch!.IsRecording);
        string svg = File.ReadAllText(path);
        Assert.Contains("<svg", svg);
        Assert.Contains("<path", svg);
        // Nothing drawn while recording reached the screen: it stays green.
        var middle = Offscreen.Pixel(surface, 100, 30);
        Assert.True(middle.Green > 200 && middle.Red < 50, $"screen pixel {middle}");
    }

    [Fact]
    public void Plotter_svg_groups_strokes_by_pen_and_drops_the_background()
    {
        string path = Path.Combine(_dir, "plot.svg");
        using var _ = Offscreen.Run(new Poster(s => { s.BeginRaw(path, plotter: true); Art(s); s.EndRaw(); }));
        string svg = File.ReadAllText(path);
        // Two stroke colors -> two pen layers (Inkscape groups).
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(svg, "inkscape:groupmode=\"layer\"").Count);
        Assert.DoesNotContain("fill=\"#FFFFFF\"", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plotter_output_is_only_for_svg()
    {
        string path = Path.Combine(_dir, "nope.pdf");
        Exception? caught = null;
        using var _ = Offscreen.Run(new Poster(s =>
        {
            try { s.BeginRaw(path, plotter: true); }
            catch (ArgumentException e) { caught = e; }
        }));
        Assert.NotNull(caught);
    }
}
