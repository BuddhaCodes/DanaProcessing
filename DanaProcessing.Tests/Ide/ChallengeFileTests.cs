using DanaProcessing.Ide.Blocks;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>Challenges teachers make: built from their own answer, saved as .danareto, shared, played.</summary>
[Collection("ChallengeFiles")]
public class ChallengeFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "dana-retos-" + Guid.NewGuid().ToString("N"));
    private readonly string _oldFolder = ChallengeFiles.Folder;

    public ChallengeFileTests() => ChallengeFiles.Folder = _folder;

    public void Dispose()
    {
        ChallengeFiles.Folder = _oldFolder;
        ChallengeCatalog.ReloadTeacher();
        try { Directory.Delete(_folder, true); } catch { }
    }

    /// <summary>A teacher's pentagon: repetir 5 (avanzar 75, girar 72).</summary>
    private static BlockProgram Pentagon()
    {
        var p = new BlockProgram { View = BlockView.Tiles };
        var stack = p.AddStack(40, 40, BlockCatalog.Start);
        var rep = p.NewBlock(BlockCatalog.Repeat);
        rep.Values["times"] = "5";
        var f = p.NewBlock(BlockCatalog.Forward);
        f.Values["steps"] = "75";
        var r = p.NewBlock(BlockCatalog.Right);
        r.Values["angle"] = "72";
        rep.Body!.Add(f);
        rep.Body!.Add(r);
        stack.Blocks.Add(rep);
        return p;
    }

    private static ChallengeResult Play(Challenge c, BlockProgram p)
    {
        var run = HeadlessBlockRunner.Run(p);
        Assert.True(run.Compiled, string.Join("\n", run.Errors));
        return ChallengeChecker.Check(c, p, c.IsTurtle ? run.TurtleLines : null);
    }

    [Fact]
    public void A_drawing_challenge_from_the_teacher_s_answer_checks_the_child_s_drawing()
    {
        var file = ChallengeFile.FromSolution(Pentagon(), "El pentágono", "Dibujá un pentágono.", "Cinco lados, girá 72.", ChallengeCheck.Shape, BlockView.Tiles, "Profe Ana");
        Assert.Null(file.Problem());

        var loaded = ChallengeFile.FromJson(file.ToJson());          // as if opened from the file
        var c = loaded.ToChallenge();
        Assert.True(c.IsTurtle);
        Assert.Equal(5, c.TargetLines().Count);
        Assert.Equal(3, c.BestBlockCount);
        Assert.Equal(new[] { BlockCatalog.Start, BlockCatalog.Repeat, BlockCatalog.Forward, BlockCatalog.Right }, c.Palette.OrderBy(t => t == BlockCatalog.Start ? 0 : 1).ThenBy(t => t == BlockCatalog.Repeat ? 0 : t == BlockCatalog.Forward ? 1 : 2));

        var start = c.NewProgram();
        Assert.Equal(c.Id, start.ChallengeId);
        Assert.Single(start.AllBlocks());                              // just "al empezar"
        Assert.False(Play(c, start).Passed);

        // The child's answer, written out the long way: right drawing, more blocks.
        var child = new BlockProgram();
        var s = child.AddStack(0, 0, BlockCatalog.Start);
        for (int i = 0; i < 5; i++)
        {
            var f = child.NewBlock(BlockCatalog.Forward); f.Values["steps"] = "75"; s.Blocks.Add(f);
            var r = child.NewBlock(BlockCatalog.Right); r.Values["angle"] = "72"; s.Blocks.Add(r);
        }
        var result = Play(c, child);
        Assert.True(result.Passed, result.Message);
        Assert.Equal(1, result.Stars);                                 // 10 blocks against 3
        Assert.True(Play(c, Pentagon()).Passed);
    }

    [Fact]
    public void A_blocks_challenge_wants_the_same_kinds_of_blocks()
    {
        var song = new BlockProgram { View = BlockView.Lines };
        song.AddStack(40, 40, BlockCatalog.MusicLoop, BlockCatalog.Note, BlockCatalog.Wait, BlockCatalog.Drum, BlockCatalog.Wait);
        var c = ChallengeFile.FromSolution(song, "Nota y tambor", "Tocá una nota y un tambor.", "", ChallengeCheck.Blocks, BlockView.Lines).ToChallenge();
        Assert.False(c.IsTurtle);
        Assert.Equal(BlockView.Lines, c.NewProgram().View);
        Assert.Equal(BlockCatalog.MusicLoop, c.NewProgram().Stacks[0].Hat!.Type);   // starts from the solution's start blocks

        var onlyNotes = new BlockProgram();
        onlyNotes.AddStack(0, 0, BlockCatalog.MusicLoop, BlockCatalog.Note, BlockCatalog.Wait);
        Assert.False(ChallengeChecker.Check(c, onlyNotes, null).Passed);
        var both = new BlockProgram();
        both.AddStack(0, 0, BlockCatalog.MusicLoop, BlockCatalog.Drum, BlockCatalog.Wait, BlockCatalog.Note);
        Assert.True(ChallengeChecker.Check(c, both, null).Passed);
        Assert.False(string.IsNullOrWhiteSpace(c.Hint));               // no hint written: "ask your teacher"
    }

    [Fact]
    public void Unfinished_challenges_say_what_is_missing()
    {
        Assert.NotNull(ChallengeFile.FromSolution(Pentagon(), " ", "goal", "", ChallengeCheck.Shape, BlockView.Tiles).Problem());
        Assert.NotNull(ChallengeFile.FromSolution(Pentagon(), "t", "", "", ChallengeCheck.Shape, BlockView.Tiles).Problem());
        var empty = new BlockProgram();
        empty.AddStack(0, 0, BlockCatalog.Start);
        Assert.NotNull(ChallengeFile.FromSolution(empty, "t", "g", "", ChallengeCheck.Shape, BlockView.Tiles).Problem());
        var music = new BlockProgram();
        music.AddStack(0, 0, BlockCatalog.MusicLoop, BlockCatalog.Note);
        Assert.NotNull(ChallengeFile.FromSolution(music, "t", "g", "", ChallengeCheck.Shape, BlockView.Tiles).Problem()); // no turtle to compare
        Assert.Null(ChallengeFile.FromSolution(music, "t", "g", "", ChallengeCheck.Blocks, BlockView.Tiles).Problem());
    }

    [Fact]
    public void Shared_files_are_imported_into_the_folder_and_bad_ones_are_skipped()
    {
        var file = ChallengeFile.FromSolution(Pentagon(), "El pentágono: ¿podés?", "Dibujá un pentágono.", "", ChallengeCheck.Shape, BlockView.Tiles);
        var elsewhere = Path.Combine(Path.GetTempPath(), "shared-" + Guid.NewGuid().ToString("N") + ChallengeFile.Extension);
        File.WriteAllText(elsewhere, file.ToJson());
        try
        {
            var imported = ChallengeFiles.Import(elsewhere);
            Assert.Equal("file:" + file.Id, imported.Id);
            Directory.CreateDirectory(_folder);
            File.WriteAllText(Path.Combine(_folder, "roto" + ChallengeFile.Extension), "{ esto no es json");
            File.WriteAllText(Path.Combine(_folder, "viejo" + ChallengeFile.Extension), "{\"format\": 99, \"solution\": {}}");

            ChallengeCatalog.ReloadTeacher();
            var teacher = Assert.Single(ChallengeCatalog.Teacher);
            Assert.Equal("El pentágono: ¿podés?", teacher.Title);
            Assert.Same(teacher, ChallengeCatalog.Get(teacher.Id));
            Assert.Null(ChallengeCatalog.Next(teacher.Id));
            Assert.DoesNotContain(':', Path.GetFileName(teacher.SourceFile!));
            Assert.Throws<InvalidDataException>(() => ChallengeFile.FromJson("{\"format\": 99, \"solution\": {}}"));
        }
        finally
        {
            File.Delete(elsewhere);
        }
    }

    [Fact]
    public void Unknown_block_types_in_a_file_are_dropped_from_the_palette()
    {
        var file = ChallengeFile.FromSolution(Pentagon(), "t", "g", "", ChallengeCheck.Shape, BlockView.Tiles);
        file.Palette = new() { BlockCatalog.Start, BlockCatalog.Forward, "turtle.teleport" };
        var loaded = ChallengeFile.FromJson(file.ToJson());
        Assert.Equal(new[] { BlockCatalog.Start, BlockCatalog.Forward }, loaded.Palette);
    }
}
