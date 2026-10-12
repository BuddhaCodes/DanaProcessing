using DanaProcessing.Reels;
using Xunit;

namespace DanaProcessing.Tests.Reels;

/// <summary>Long lines wrap instead of being cut off; typing makes keyboard sounds; typos stay on screen long enough to read.</summary>
public class ReelWrapAndSoundTests
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

    // ---------------- Keyboard sounds ----------------

    [Theory]
    [InlineData("int a;", 1.0)]
    [InlineData("// @reel keys: off", 0.0)]
    [InlineData("// @reel keys: 50%", 0.5)]
    [InlineData("// @reel keysound: on", 1.0)]
    public void Key_sounds_are_on_by_default_and_take_a_volume(string src, double expected)
    {
        var s = ReelScript.Parse(src);
        Assert.Empty(s.Warnings);
        Assert.Equal(expected, s.KeySounds, 6);
    }

    [Fact]
    public void Key_clicks_are_short_audible_and_never_clip()
    {
        var mix = new float[KeySounds.SampleRate * 2 * 2]; // 2 s stereo
        var strokes = Enumerable.Range(0, 30).Select(i => (i * 0.06, i % 6 == 5 ? KeyStroke.Space : KeyStroke.Key)).ToList();
        strokes.Add((1.9, KeyStroke.Enter));
        KeySounds.Mix(mix, strokes, 1);
        float peak = mix.Max(Math.Abs);
        Assert.InRange(peak, 0.05f, 1f);
        // Silence before the first key and between the last click and the Enter.
        Assert.All(mix.Skip(2 * (int)(1.85 * KeySounds.SampleRate)).Take(2 * (int)(0.04 * KeySounds.SampleRate)), v => Assert.Equal(0f, v));
    }

    [Fact]
    public void A_soundtrack_with_only_keys_writes_a_wav_and_a_silent_one_writes_nothing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dana_{Guid.NewGuid():N}.wav");
        try
        {
            Assert.False(new ReelSoundtrack(null, Array.Empty<(double, KeyStroke)>(), 1).WriteWav(path, 1));
            Assert.False(File.Exists(path));
            Assert.True(new ReelSoundtrack(null, new[] { (0.5, KeyStroke.Key) }, 1).WriteWav(path, 1));
            var wav = DanaProcessing.SampleBuffer.LoadWav(path);
            Assert.Equal(KeySounds.SampleRate, wav.Frames);
            Assert.True(wav.Left.Skip(KeySounds.SampleRate / 2).Take(2000).Any(v => v != 0));
        }
        finally { File.Delete(path); }
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

    [Fact]
    public void Renders_an_mp4_with_key_sounds_even_when_the_sketch_is_silent()
    {
        var ffmpeg = ReelExporter.FindFfmpeg();
        if (ffmpeg == null)
            return;
        var path = Path.Combine(Path.GetTempPath(), $"dana_{Guid.NewGuid():N}.mp4");
        try
        {
            ReelExporter.ExportAsync(ReelScript.Parse("// @reel fps: 15\n" + Code), null,
                new ReelExportOptions { Kind = ReelExportKind.Mp4, OutputPath = path, FfmpegPath = ffmpeg }).GetAwaiter().GetResult();
            var probe = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ffmpeg, $"-hide_banner -i \"{path}\"") { RedirectStandardError = true, UseShellExecute = false })!;
            var info = probe.StandardError.ReadToEnd();
            probe.WaitForExit();
            Assert.Contains("Audio:", info);
        }
        finally { File.Delete(path); }
    }
}
