using DanaProcessing.Reels;
using Xunit;

namespace DanaProcessing.Tests.Reels;

/// <summary>Long lines wrap instead of being cut off; typos stay on screen long enough to read.</summary>
public class ReelWrapTests
{
    // ---------------- Wrapping (pure logic) ----------------

    [Fact]
    public void A_long_line_wraps_at_spaces_and_every_row_fits()
    {
        const string line = "        X = Random(Width), Y = y, Speed = Random(0.5f, 2.5f), Size = Random(10, 60), Alpha = Random(80, 255),";
        var rows = CodeWrap.WrapLine(line, 50);
        Assert.True(rows.Count >= 3);
        Assert.All(rows, r => Assert.True(r.Length <= 50, $"'{r}' is {r.Length} wide"));
        Assert.All(rows.Skip(1), r => Assert.StartsWith(new string(' ', 12), r)); // one level deeper than the line
        // Nothing lost: the same words in the same order.
        Assert.Equal(Squash(line), Squash(string.Join(" ", rows)));
    }

    [Fact]
    public void Short_lines_are_left_alone()
    {
        Assert.Equal(new[] { "    Circle(x, y, 40);" }, CodeWrap.WrapLine("    Circle(x, y, 40);", 60));
    }

    [Fact]
    public void Wrapping_never_breaks_inside_a_string()
    {
        const string line = "Text(\"a string with many words in it that must stay together\", 10, 20); Println(\"two\");";
        var rows = CodeWrap.WrapLine(line, 70);
        Assert.Contains(rows, r => r.Contains("\"a string with many words in it that must stay together\""));
    }

    [Fact]
    public void A_wrapped_comment_keeps_its_slashes()
    {
        var rows = CodeWrap.WrapLine("Circle(x, y, 40); // a comment long enough to need a second row of its own", 40);
        Assert.True(rows.Count >= 2);
        Assert.All(rows.Skip(1), r => Assert.StartsWith("//", r.TrimStart()));
    }

    [Fact]
    public void Wrapped_scenes_keep_pauses_and_caption_changes_on_their_lines()
    {
        var script = ReelScript.Parse("""
            // @reel scene "s" effect=type
            int a = 1;
            // @reel pause 0.7
            // @reel caption: second
            float averyveryverylongname = Map(MouseX, 0, Width, 0, 100) + Map(MouseY, 0, Height, 0, 100); // @mark
            int b = 2;
            """);
        var scene = script.Scenes[0];
        var wrapped = CodeWrap.Wrap(scene, 40);
        Assert.True(wrapped.Lines.Count > scene.Lines.Count);
        Assert.Equal("int b = 2;", wrapped.Lines[^1].Text);
        Assert.Equal(1, wrapped.Pauses.Single().Line);                 // still before the long line
        Assert.Equal(1, wrapped.CaptionChanges.Single().Line);
        Assert.All(wrapped.Lines.Skip(1).Take(wrapped.Lines.Count - 2), l => Assert.True(l.Marked));
        Assert.False(wrapped.Lines[1].Continuation);
        Assert.True(wrapped.Lines[2].Continuation);
        Assert.Same(script.Scenes[0], CodeWrap.Wrap(script.Scenes[0], 200)); // nothing to wrap: no copy
    }

    private static string Squash(string s) => string.Join(" ", s.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    [Fact]
    public void A_silent_soundtrack_writes_no_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dana_{Guid.NewGuid():N}.wav");
        Assert.False(new ReelSoundtrack(null).WriteWav(path, 1));
        Assert.False(File.Exists(path));
    }

    // ---------------- On a real timeline (native Skia, CI) ----------------

    private const string Code = """
        // @reel scene "Typed" effect=type
        public class Bubbles : Sketch
        {
            public override void Setup() { Size(600, 600); }
            Bubble NewBubble(float y) => new Bubble { X = Random(Width), Y = y, Speed = Random(0.5f, 2.5f), Size = Random(10, 60), Alpha = Random(80, 255) };
        }
        // @reel result: off
        // @reel outro: off
        """;

    [Fact]
    public void Renders_typos_that_stay_long_enough_to_read()
    {
        using var t = ReelTimeline.Build(ReelScript.Parse("// @reel typos: 100%\n" + Code), null);
        var keys = t.KeyStrokes;
        int firstBackspace = keys.ToList().FindIndex(k => k.Kind == KeyStroke.Backspace);
        Assert.True(firstBackspace > 0, "a typo should be erased with backspace");
        Assert.True(keys[firstBackspace].Time - keys[firstBackspace - 1].Time >= 0.5, "the mistake should stay on screen for a beat");
    }

    [Fact]
    public void Renders_long_lines_wrapped_with_key_strokes_for_every_character()
    {
        var script = ReelScript.Parse(Code);
        using var t = ReelTimeline.Build(script, null);
        int typed = script.Scenes[0].Lines.Sum(l => l.Text.TrimStart().Length);
        Assert.True(t.KeyStrokes.Count(k => k.Kind is KeyStroke.Key or KeyStroke.Space) >= typed - 2);
        Assert.Contains(t.KeyStrokes, k => k.Kind == KeyStroke.Enter);
    }
}
