using DanaProcessing.Ide.Blocks;
using DanaProcessing.Ide.Compilation;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>The C# the blocks make, with its names in English for an English IDE.</summary>
public class CodeWordsTests
{
    private static BlockProgram Everything()
    {
        var p = new BlockProgram { Name = "Mine" };
        var define = p.AddStack(0, 0, BlockCatalog.Define);
        var rep = p.NewBlock(BlockCatalog.Repeat);
        rep.Values["times"] = "var:contador";
        var fwd = p.NewBlock(BlockCatalog.Forward);
        fwd.Values["steps"] = "var:lado";
        rep.Body!.Add(fwd);
        define.Blocks.Add(rep);
        var start = p.AddStack(0, 300, BlockCatalog.Start, BlockCatalog.VarSet, BlockCatalog.Call, BlockCatalog.ScoreAdd, BlockCatalog.VarShow);
        start.Blocks[1].Values["var"] = "angulo";
        var music = p.AddStack(0, 600, BlockCatalog.MusicLoop, BlockCatalog.Note);
        return p;
    }

    [Fact]
    public void English_code_names_things_in_english_and_still_compiles()
    {
        var p = Everything();
        var en = BlockCodeGenerator.Generate(p, english: true).Source;
        var es = BlockCodeGenerator.Generate(p, english: false).Source;

        Assert.Contains("Turtle turtle;", en);
        Assert.Contains("int score = 0;", en);
        Assert.Contains("float side = 0;", en);
        Assert.Contains("float angle = 0;", en);
        Assert.Contains("void MakeSquare()", en);
        Assert.Contains("MakeSquare();", en);
        Assert.Contains("turtle.Forward(side);", en);
        Assert.Contains("Music1()", en);
        Assert.DoesNotContain("tortuga", en);
        Assert.Contains("tortuga.Forward(lado);", es);
        Assert.Equal(es.Split('\n').Length, en.Split('\n').Length);   // the block-to-line map still holds

        foreach (var instrument in new[] { false, true })
        {
            var compiled = SketchCompiler.Compile(BlockCodeGenerator.Generate(p, instrument, english: true).Source);
            Assert.True(compiled.Success, string.Join("\n", compiled.Errors));
        }
    }

    [Fact]
    public void Only_whole_names_outside_strings_and_comments_change()
    {
        const string code = "tortuga.Forward(lado); Text(\"lado: \" + lado, 1, 2); // tortuga\nladoExtra = 1; Musica12(); Musicas();";
        var en = CodeWords.ToEnglish(code);
        Assert.Equal("turtle.Forward(side); Text(\"lado: \" + side, 1, 2); // tortuga\nladoExtra = 1; Music12(); Musicas();", en);
        Assert.Equal(code, CodeWords.ToSpanish(en));
        Assert.Equal(code, CodeWords.ToSpanish(code));               // Spanish passes through
    }

    [Fact]
    public void English_code_turns_back_into_the_same_blocks()
    {
        var p = Everything();
        var back = CodeToBlocks.Convert(BlockCodeGenerator.Generate(p, english: true).Source);
        Assert.True(back.Exact, string.Join(" | ", back.Skipped.Select(s => s.Code)) + string.Join(",", back.Rounded));
        back.Program.Name = p.Name;
        Assert.Equal(BlockCodeGenerator.Generate(p, english: false).Source, BlockCodeGenerator.Generate(back.Program, english: false).Source);
    }
}
