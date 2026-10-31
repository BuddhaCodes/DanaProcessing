using DanaProcessing.Ide.Blocks;
using DanaProcessing.Ide.Compilation;
using Xunit;

namespace DanaProcessing.Tests.Ide;

public class ChallengeTests
{
    // A tiny way to write block programs: a script is a hat plus blocks.
    private sealed record B(string Type, Dictionary<string, string>? Values = null, B[]? Body = null);

    private static B F(int steps) => new(BlockCatalog.Forward, new() { ["steps"] = steps.ToString() });
    private static B R(int angle) => new(BlockCatalog.Right, new() { ["angle"] = angle.ToString() });
    private static B L(int angle) => new(BlockCatalog.Left, new() { ["angle"] = angle.ToString() });
    private static B Rep(int times, params B[] body) => new(BlockCatalog.Repeat, new() { ["times"] = times.ToString() }, body);
    private static B Of(string type, string? slot = null, string? value = null) =>
        new(type, slot is null ? null : new() { [slot] = value! });

    private static BlockProgram Program(Challenge c, params (string Hat, B[] Blocks)[] scripts)
    {
        var p = new BlockProgram { ChallengeId = c.Id };
        double y = 0;
        foreach (var (hat, blocks) in scripts)
        {
            var stack = p.AddStack(0, y, hat);
            stack.Blocks.AddRange(blocks.Select(b => Make(p, b)));
            y += 300;
        }
        return p;
    }

    private static Block Make(BlockProgram p, B b)
    {
        var block = p.NewBlock(b.Type);
        foreach (var (k, v) in b.Values ?? new())
            block.Values[k] = v;
        foreach (var child in b.Body ?? Array.Empty<B>())
            block.Body!.Add(Make(p, child));
        return block;
    }

    private static ChallengeResult Try(Challenge c, BlockProgram p)
    {
        IReadOnlyList<TurtleLine>? lines = null;
        if (c.IsTurtle)
        {
            var run = HeadlessBlockRunner.Run(p);
            Assert.True(run.Compiled, string.Join("\n", run.Errors));
            lines = run.TurtleLines;
        }
        return ChallengeChecker.Check(c, p, lines);
    }

    private static Challenge C(string id) => ChallengeCatalog.Get(id) ?? throw new Exception(id);

    private static (string, B[]) Start(params B[] blocks) => (BlockCatalog.Start, blocks);

    /// <summary>A good answer for every challenge -- and it must use only that challenge's palette.</summary>
    private static BlockProgram Answer(Challenge c) => c.Id switch
    {
        "first-step" => Program(c, Start(F(100))),
        "corner" => Program(c, Start(F(100), R(90), F(100))),
        "square" => Program(c, Start(F(100), R(90), F(100), R(90), F(100), R(90), F(100))),
        "repeat" => Program(c, Start(Rep(4, F(100), R(90)))),
        "stairs" => Program(c, Start(Rep(3, F(50), R(90), F(50), L(90)))),
        "triangle" => Program(c, Start(Rep(3, F(100), R(120)))),
        "star" => Program(c, Start(Rep(5, F(100), R(144)))),
        "jump" => Program(c, Start(F(50), Of(BlockCatalog.PenUp), F(50), Of(BlockCatalog.PenDown), F(50))),
        "colors" => Program(c, Start(Of(BlockCatalog.PenColor, "color", "red"), F(100), R(90), Of(BlockCatalog.PenColor, "color", "blue"),
                                     Rep(3, F(100), R(90)))),
        "beat" => Program(c, (BlockCatalog.MusicLoop, new[]
        {
            Of(BlockCatalog.Drum, "drum", "kick"), Of(BlockCatalog.Wait, "beats", "1"),
            Of(BlockCatalog.Drum, "drum", "snare"), Of(BlockCatalog.Wait, "beats", "1"),
        })),
        "song" => Program(c, (BlockCatalog.MusicLoop, new[]
        {
            Of(BlockCatalog.Note, "note", "C4"), Of(BlockCatalog.Wait, "beats", "1"),
            Of(BlockCatalog.Note, "note", "D4"), Of(BlockCatalog.Wait, "beats", "1"),
            Of(BlockCatalog.Note, "note", "E4"), Of(BlockCatalog.Wait, "beats", "1"),
        })),
        "paint" => Program(c, (BlockCatalog.Forever, new[] { Of(BlockCatalog.Circle, "place", "mouse") })),
        "points" => Program(c, (BlockCatalog.Click, new[] { Of(BlockCatalog.ScoreAdd) }),
                               (BlockCatalog.Forever, new[] { Of(BlockCatalog.Background), Of(BlockCatalog.ScoreShow) })),
        "hexagon" => Program(c, Start(Rep(6, F(75), R(60)))),
        "windmill" => Program(c, Start(Rep(4, Rep(4, F(50), R(90)), R(90)))),
        "house" => Program(c, Start(F(100), R(30), F(100), R(120), F(100), R(30), F(100), R(90), F(100))),
        "rock" => Program(c, (BlockCatalog.MusicLoop, new[]
        {
            Of(BlockCatalog.Drum, "drum", "kick"), Of(BlockCatalog.Wait, "beats", "0.5"),
            Of(BlockCatalog.Drum, "drum", "hat"), Of(BlockCatalog.Wait, "beats", "0.5"),
            Of(BlockCatalog.Drum, "drum", "snare"), Of(BlockCatalog.Wait, "beats", "0.5"),
            Of(BlockCatalog.Drum, "drum", "hat"), Of(BlockCatalog.Wait, "beats", "0.5"),
        })),
        "scale" => Program(c, (BlockCatalog.MusicLoop, new[] { "C4", "D4", "E4", "F4", "G4" }
            .SelectMany(n => new[] { Of(BlockCatalog.Note, "note", n), Of(BlockCatalog.Wait, "beats", "0.5") }).ToArray())),
        "duet" => Program(c,
            (BlockCatalog.MusicLoop, new[] { Of(BlockCatalog.Drum, "drum", "kick"), Of(BlockCatalog.Wait, "beats", "1") }),
            (BlockCatalog.MusicLoop, new[] { Of(BlockCatalog.Note, "note", "E4"), Of(BlockCatalog.Wait, "beats", "1") })),
        "spiral" => Program(c, Start(
            new B(BlockCatalog.VarSet, new() { ["value"] = "10", ["var"] = "lado" }),
            Rep(12, new B(BlockCatalog.Forward, new() { ["steps"] = "var:lado" }), R(90),
                    new B(BlockCatalog.VarChange, new() { ["var"] = "lado", ["amount"] = "10" })))),
        "two-squares" => Program(c,
            Start(Of(BlockCatalog.Call), Of(BlockCatalog.PenUp), R(90), F(75), L(90), Of(BlockCatalog.PenDown), Of(BlockCatalog.Call)),
            (BlockCatalog.Define, new[] { Rep(4, F(50), R(90)) })),
        "flower" => Program(c,
            Start(Rep(12, Of(BlockCatalog.Call), R(30))),
            (BlockCatalog.Define, new[] { Rep(4, F(75), R(90)) })),
        "arrows" => Program(c, (BlockCatalog.Forever, new[]
        {
            new B(BlockCatalog.If, new() { ["cond"] = "right" }, new[] { F(10) }),
            new B(BlockCatalog.If, new() { ["cond"] = "left" }, new[] { L(30) }),
        })),
        _ => throw new Exception("no answer written for " + c.Id),
    };

    public static IEnumerable<object[]> Ids() => ChallengeCatalog.All.Select(c => new object[] { c.Id });

    [Theory]
    [MemberData(nameof(Ids))]
    public void A_good_answer_passes_with_three_stars(string id)
    {
        var c = C(id);
        var answer = Answer(c);
        Assert.All(answer.AllBlocks(), b => Assert.Contains(b.Type, c.Palette));   // reachable from the palette
        var result = Try(c, answer);
        Assert.True(result.Passed, result.Message);
        Assert.Equal(3, result.Stars);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void The_starting_program_does_not_pass_and_compiles_with_its_guide(string id)
    {
        var c = C(id);
        var start = c.NewProgram();
        Assert.Equal(id, start.ChallengeId);
        Assert.All(start.AllBlocks(), b => Assert.Contains(b.Type, c.Palette));
        Assert.False(Try(c, start).Passed);

        var code = BlockCodeGenerator.Generate(Answer(c), instrument: true, scene: c.Scene());
        var compiled = SketchCompiler.Compile(code.Source);
        Assert.True(compiled.Success, string.Join("\n", compiled.Errors) + "\n" + code.Source);
        if (c.IsTurtle)
            Assert.Contains("Stroke(255, 196, 61, 110)", code.Source);
        Assert.DoesNotContain("Stroke(255, 196, 61, 110)", BlockCodeGenerator.Generate(Answer(c), scene: c.Scene()).Source);
    }

    [Fact]
    public void A_wrong_turn_misses_the_path()
    {
        var c = C("corner");
        var r = Try(c, Program(c, Start(F(100), L(90), F(100))));
        Assert.False(r.Passed);
    }

    [Fact]
    public void Walking_past_the_end_is_drawing_outside_the_path()
    {
        var c = C("first-step");
        var r = Try(c, Program(c, Start(F(150))));
        Assert.False(r.Passed);
        Assert.Contains("fuera", r.Message + "outside");
    }

    [Fact]
    public void Stopping_short_misses_a_piece()
    {
        var c = C("first-step");
        var r = Try(c, Program(c, Start(F(50))));
        Assert.False(r.Passed);
    }

    [Fact]
    public void The_repeat_challenge_wants_a_repeat_even_when_the_square_is_right()
    {
        var c = C("repeat");
        var longHand = Program(c, Start(F(100), R(90), F(100), R(90), F(100), R(90), F(100)));
        var r = Try(c, longHand);
        Assert.False(r.Passed);
        Assert.Contains("repetir", r.Message + "repeat");
    }

    [Fact]
    public void More_blocks_than_needed_earns_fewer_stars()
    {
        var c = C("square");
        var longest = Program(c, Start(
            F(50), F(50), R(90), F(50), F(50), R(90), F(50), F(50), R(90), F(50), F(50), R(90), F(10), Of(BlockCatalog.Back, "steps", "10"),
            F(10), Of(BlockCatalog.Back, "steps", "10")));   // 16 blocks for a 7-block job
        var r = Try(c, longest);
        Assert.True(r.Passed, r.Message);
        Assert.Equal(1, r.Stars);
    }

    [Fact]
    public void Drawing_a_square_twice_the_same_size_on_top_still_counts()
    {
        var c = C("repeat");
        var r = Try(c, Program(c, Start(Rep(8, F(100), R(90)))));
        Assert.True(r.Passed, r.Message);
    }

    [Fact]
    public void Jumping_needs_the_pen_lifted()
    {
        var c = C("jump");
        var r = Try(c, Program(c, Start(F(150))));
        Assert.False(r.Passed);
    }

    [Fact]
    public void One_color_is_not_colored_pencils()
    {
        var c = C("colors");
        var r = Try(c, Program(c, Start(Of(BlockCatalog.PenColor, "color", "red"), Rep(4, F(100), R(90)))));
        Assert.False(r.Passed);
    }

    [Fact]
    public void Music_rules_explain_what_is_missing()
    {
        var c = C("beat");
        var oneDrum = Program(c, (BlockCatalog.MusicLoop, new[] { Of(BlockCatalog.Drum), Of(BlockCatalog.Drum) }));
        Assert.False(Try(c, oneDrum).Passed);
        var noWaits = Program(c, (BlockCatalog.MusicLoop, new[] { Of(BlockCatalog.Drum, "drum", "kick"), Of(BlockCatalog.Drum, "drum", "snare") }));
        var r = Try(c, noWaits);
        Assert.False(r.Passed);
        Assert.Contains("esperar", r.Message + "wait");
    }

    [Fact]
    public void Painting_the_background_every_frame_wipes_the_trail()
    {
        var c = C("paint");
        var r = Try(c, Program(c, (BlockCatalog.Forever, new[] { Of(BlockCatalog.Background), Of(BlockCatalog.Circle, "place", "mouse") })));
        Assert.False(r.Passed);
    }

    [Fact]
    public void Blocks_lying_around_without_a_hat_do_not_count()
    {
        var c = C("first-step");
        var p = c.NewProgram();
        p.AddStack(300, 300, BlockCatalog.Forward);  // not under "al empezar"
        Assert.False(Try(c, p).Passed);
    }

    [Fact]
    public void Progress_keeps_the_best_result()
    {
        var file = Path.Combine(Path.GetTempPath(), "dana-progress-" + Guid.NewGuid().ToString("N") + ".json");
        var old = ChallengeProgress.FilePath;
        try
        {
            ChallengeProgress.FilePath = file;
            Assert.Empty(ChallengeProgress.Load());
            Assert.True(ChallengeProgress.Record("square", 2));
            Assert.False(ChallengeProgress.Record("square", 1));
            Assert.True(ChallengeProgress.Record("square", 3));
            Assert.Equal(3, ChallengeProgress.Load()["square"]);
            File.WriteAllText(file, "{ not json");
            Assert.Empty(ChallengeProgress.Load());
        }
        finally
        {
            ChallengeProgress.FilePath = old;
            File.Delete(file);
        }
    }

    [Fact]
    public void Challenges_have_unique_ids_and_a_next_one()
    {
        Assert.Equal(ChallengeCatalog.All.Count, ChallengeCatalog.All.Select(c => c.Id).Distinct().Count());
        Assert.Equal("corner", ChallengeCatalog.Next("first-step")!.Id);
        Assert.Null(ChallengeCatalog.Next(ChallengeCatalog.All[^1].Id));
        Assert.All(ChallengeCatalog.All, c => Assert.All(c.Palette, t => Assert.True(BlockCatalog.Exists(t), t)));
    }
}
