using DanaProcessing.Reels;
using Xunit;

namespace DanaProcessing.Tests.Reels;

public class ReelScriptTests
{
    private const string Demo = """
        using DanaProcessing.Reels;

        // @reel title: Particles that chase your mouse
        // @reel subtitle: ~40 lines of C#
        // @reel format: vertical
        // @reel result: 8 caption="Move to steer · click to burst"

        // @reel hide
        using System.Collections.Generic;
        // @reel show

        // @reel scene "A particle" effect=type
        class Particle
        {
            public PVector Pos, Vel;
        }

        // @reel scene "The canvas" effect=lines
        public class ParticleChase : Sketch
        {
            public override void Setup()
            {
                Size(600, 600);
            }

            // @reel scene "Every frame" effect=type
            public override void Draw()
            {
                // @reel pause 0.4
                foreach (var p in particles)
                {
                    pull.SetMag(0.35f);   // @mark
                    p.Vel.Limit(7);       // @mark
                }
            }

            // @reel scene "A click" keep
            public override void MousePressed() => Burst(MouseX, MouseY, 80);
        }
        """;

    [Fact]
    public void Reads_header_settings()
    {
        var s = ReelScript.Parse(Demo);
        Assert.True(s.ImportsModule);
        Assert.True(s.HasMarkers);
        Assert.Equal("Particles that chase your mouse", s.Title);
        Assert.Equal("~40 lines of C#", s.Subtitle);
        Assert.Equal(ReelFormat.Vertical, s.Format);
        Assert.Equal(8, s.ResultSeconds);
        Assert.Equal("Move to steer · click to burst", s.ResultCaption);
        Assert.Empty(s.Warnings);
        Assert.True(s.SourceUsesMouse);
    }

    [Fact]
    public void Splits_scenes_and_hides_meta_lines()
    {
        var s = ReelScript.Parse(Demo);
        Assert.Equal(4, s.Scenes.Count);
        var all = s.Scenes.SelectMany(x => x.Lines).Select(l => l.Text).ToList();
        Assert.DoesNotContain(all, l => l.Contains("@reel") || l.Contains("using System.Collections") || l.Contains("using DanaProcessing.Reels"));
        Assert.Equal("class Particle", s.Scenes[0].Lines[0].Text);
        Assert.Equal(CodeEffect.Lines, s.Scenes[1].Effect);
    }

    [Fact]
    public void Marks_pauses_and_keep_chains()
    {
        var s = ReelScript.Parse(Demo);
        var draw = s.Scenes[2];
        Assert.Equal(2, draw.Lines.Count(l => l.Marked));
        Assert.All(draw.Lines, l => Assert.DoesNotContain("@mark", l.Text));
        Assert.Single(draw.Pauses);
        Assert.StartsWith("foreach", draw.Lines[draw.Pauses[0].Line].Text.TrimStart());
        Assert.True(s.Scenes[3].Keep);
        // A keep chain shares one indentation: the class's closing brace sits at column 0.
        Assert.Equal("}", s.Scenes[3].Lines[^1].Text);
        Assert.StartsWith("    public override void Draw()", draw.Lines[0].Text);
    }

    [Fact]
    public void Dedents_a_scene_on_its_own()
    {
        var s = ReelScript.Parse("""
            // @reel scene "body"
                    int x = 1;
                    int y = 2;
            """);
        Assert.Equal("int x = 1;", s.Scenes[0].Lines[0].Text);
    }

    [Fact]
    public void Options_inputs_and_warnings()
    {
        var p = ReelScript.Parse(string.Join("\n",
            "// @reel format square", "// @reel theme: dark", "// @reel speed fast", "// @reel lang es", "// @reel fps 60",
            "// @reel scene \"Hola mundo\" effect=fade transition=wipe hold=2 speed=30",
            "void A() {}",
            "// @reel caption: Ahora la segunda parte",
            "void B() {}",
            "// @reel move 10,10 -> 200,200 -> 300,50 at=1 dur=2",
            "// @reel drag 100,100 -> 400,300 at 3s over 1.5s",
            "// @reel click 50,60 at=5",
            "// @reel key space at=6",
            "// @reel bogus thing",
            "// @reel click nowhere at=2",
            "// @reel result off",
            "// @reel outro: off",
            "// @reel scene caption=\"Solo texto, sin código\""));

        Assert.Equal(ReelFormat.Square, p.Format);
        Assert.Equal((1080, 1080), (p.Width, p.Height));
        Assert.Equal(ReelThemeKind.Dark, p.Theme);
        Assert.Equal(80, p.Speed);
        Assert.True(p.SpeedExplicit);
        Assert.Equal("es", p.Lang);
        Assert.Equal(60, p.Fps);

        var scene = p.Scenes[0];
        Assert.Equal("Hola mundo", scene.Caption);
        Assert.Equal(CodeEffect.Fade, scene.Effect);
        Assert.Equal(ReelTransition.Wipe, scene.Transition);
        Assert.Equal(2, scene.Hold);
        Assert.Equal(30, scene.Speed);
        Assert.Equal((1, "Ahora la segunda parte"), scene.CaptionChanges.Single());

        Assert.Equal(4, p.Input.Count);
        Assert.Equal(ReelInputKind.Move, p.Input[0].Kind);
        Assert.Equal(3, p.Input[0].Points.Count);
        Assert.Equal((1.0, 2.0), (p.Input[0].At, p.Input[0].Duration));
        Assert.Equal(ReelInputKind.Drag, p.Input[1].Kind);
        Assert.Equal((3.0, 1.5), (p.Input[1].At, p.Input[1].Duration));
        Assert.Equal("space", p.Input[3].Key);

        Assert.Equal(2, p.Warnings.Count);
        Assert.Contains(p.Warnings, w => w.Contains("bogus"));
        Assert.Contains(p.Warnings, w => w.Contains("nowhere"));

        Assert.Equal(0, p.ResultSeconds);
        Assert.Equal("", p.Outro);
        Assert.Equal(2, p.Scenes.Count);
        Assert.Empty(p.Scenes[1].Lines);
        Assert.Equal("Solo texto, sin código", p.Scenes[1].Caption);
    }

    [Theory]
    [InlineData("// @reel format: 16:9", ReelFormat.Landscape)]
    [InlineData("// @reel format: 1:1", ReelFormat.Square)]
    [InlineData("// @reel format: tiktok", ReelFormat.Vertical)]
    public void Format_aliases(string line, ReelFormat expected) =>
        Assert.Equal(expected, ReelScript.Parse(line).Format);

    [Fact]
    public void Unclosed_hide_warns()
    {
        var s = ReelScript.Parse("// @reel hide\nint x;");
        Assert.Contains(s.Warnings, w => w.Contains("hide"));
        Assert.Empty(s.Scenes);
    }

    [Fact]
    public void Plain_sketch_has_no_markers()
    {
        var s = ReelScript.Parse("public class A : Sketch { public override void Draw() {} }");
        Assert.False(s.ImportsModule);
        Assert.False(s.HasMarkers);
        Assert.Single(s.Scenes); // all code becomes one implicit scene
    }
}
