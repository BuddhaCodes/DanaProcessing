using DanaProcessing.Ide.Editor;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>"Apply tweaks to code": which initializers change, and how.</summary>
public class TweakSourceRewriterTests
{
    private static TweakValue Moved(string name, object value, string literal) => new(name, value, literal, Touched: true);
    private static TweakValue Untouched(string name, object value, string literal) => new(name, value, literal, Touched: false);

    private const string Source = @"public class MySketch : Sketch
{
    [Tweak(1, 6)] float freqX = 3f;      // moved
    [Tweak(0, 6.2832f)] float phase = 1.5708f;
    [Tweak] float half = MathF.PI / 2;
    [Tweak] int count;
    [Tweak] bool spin = true;
    [Tweak(Label = ""ink"")] Color ink = new Color(255, 170, 90);
    float freqY = 3f;                    // not a [Tweak]: never touched
    public override void Draw() { }
}";

    [Fact]
    public void Moved_values_replace_only_their_initializer()
    {
        string result = TweakSourceRewriter.Apply(Source, new[]
        {
            Moved("freqX", 4.25f, "4.25f"),
            Moved("freqY", 9f, "9f"),
        });

        Assert.Contains("[Tweak(1, 6)] float freqX = 4.25f;      // moved", result);
        Assert.Contains("float freqY = 3f;", result);
    }

    [Fact]
    public void Untouched_fields_keep_whatever_the_code_says()
    {
        var values = new[]
        {
            Untouched("half", MathF.PI / 2, "1.571f"),
            Untouched("freqX", 4.25f, "4.25f"),
        };
        Assert.Empty(TweakSourceRewriter.ComputeEdits(Source, values));
    }

    [Fact]
    public void A_value_equal_to_the_written_one_is_not_rewritten()
    {
        // 1.5708f compiled is exactly this float -- its 4-digit literal would
        // be "1.571f", which must NOT replace the more precise original.
        var values = new[]
        {
            Moved("phase", 1.5708f, "1.571f"),
            Moved("spin", true, "true"),
            Moved("ink", new Color(255, 170, 90), "new Color(255, 170, 90)"),
        };
        Assert.Empty(TweakSourceRewriter.ComputeEdits(Source, values));
    }

    [Fact]
    public void Bools_colors_and_missing_initializers()
    {
        string result = TweakSourceRewriter.Apply(Source, new[]
        {
            Moved("spin", false, "false"),
            Moved("ink", new Color(10, 20, 30), "new Color(10, 20, 30)"),
            Moved("count", 7, "7"),
        });

        Assert.Contains("[Tweak] bool spin = false;", result);
        Assert.Contains("Color ink = new Color(10, 20, 30);", result);
        Assert.Contains("[Tweak] int count = 7;", result);
    }

    [Fact]
    public void Sketch_writes_short_literals_and_tracks_touched_fields()
    {
        var sketch = new Tuned();
        var values = sketch.GetTweakValues();

        Assert.Equal("2.346f", values.Single(v => v.FieldName == nameof(Tuned.Speed)).CSharpLiteral);
        Assert.Equal("0.0025f", values.Single(v => v.FieldName == nameof(Tuned.Damping)).CSharpLiteral);
        Assert.Equal("new Color(1, 2, 3, 4)", values.Single(v => v.FieldName == nameof(Tuned.Ink)).CSharpLiteral);
        Assert.All(values, v => Assert.False(v.Touched));
        Assert.DoesNotContain(values, v => v.FieldName == nameof(Tuned.NotTweaked));
    }

    public class Tuned : Sketch
    {
        [Tweak] public float Speed = 2.3456789f;
        [Tweak] public float Damping = 0.0025f;
        [Tweak] public Color Ink = new Color(1, 2, 3, 4);
        public float NotTweaked = 1;
        public override void Draw() { }
    }
}
