using DanaProcessing.Ide.Blocks;
using DanaProcessing.Ide.Compilation;
using DanaProcessing.Ide.Editor;
using Xunit;

namespace DanaProcessing.Tests.Ide;

public class BlockSamplesTests
{
    public static IEnumerable<object[]> Names() => BlockSamples.All.Select(s => new object[] { s.Name });

    [Theory]
    [MemberData(nameof(Names))]
    public void Every_blocks_example_opens_as_blocks_compiles_and_shows_something(string name)
    {
        var sample = BlockSamples.All.Single(s => s.Name == name);
        Assert.Contains(SampleTag.Blocks, sample.Tags);
        var program = sample.Blocks!();
        Assert.Equal(name, program.Name);
        Assert.NotEmpty(program.Scripts());
        Assert.True(SketchCompiler.Compile(sample.Source).Success);
        Assert.Equal(sample.Source, BlockCodeGenerator.Generate(program).Source);   // the preview runs what opens

        bool music = program.Scripts().Any(s => s.Hat!.Type == BlockCatalog.MusicLoop);
        bool turtle = program.AllBlocks().Any(b => b.Def.Category == BlockCategory.Turtle);
        if (turtle && !sample.Tags.Contains(SampleTag.Interaction))   // games draw once you play
            Assert.True(HeadlessBlockRunner.Run(program).TurtleLines.Count >= 12, "a turtle drawing with some lines in it");
        else if (!music && !turtle)
            Assert.NotNull(Classroom.Thumbnail(program));
    }

    [Fact]
    public void Blocks_examples_come_first_in_the_examples_window()
    {
        var all = SketchSamples.All;
        Assert.All(all.Take(BlockSamples.All.Count), s => Assert.NotNull(s.Blocks));
        Assert.All(all.Skip(BlockSamples.All.Count), s => Assert.Null(s.Blocks));
    }
}
