using DanaProcessing.Ide.Blocks;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>The way back: the C# the blocks make turns into the same blocks again.</summary>
public class CodeToBlocksTests
{
    private static Block Add(BlockProgram p, List<Block> into, string type, params (string Slot, string Value)[] values)
    {
        var b = p.NewBlock(type);
        foreach (var (slot, value) in values)
            b.Values[slot] = value;
        into.Add(b);
        return b;
    }

    /// <summary>Generate -> convert -> generate gives the same code: nothing lost, nothing added.</summary>
    private static CodeToBlocksResult RoundTrip(BlockProgram p)
    {
        var code = BlockCodeGenerator.Generate(p).Source;
        var back = CodeToBlocks.Convert(code);
        Assert.True(back.Exact, "skipped: " + string.Join(" | ", back.Skipped.Select(s => $"{s.Line}: {s.Code}")) + " rounded: " + string.Join(", ", back.Rounded) + "\n" + code);
        back.Program.Name = p.Name;
        Assert.Equal(code, BlockCodeGenerator.Generate(back.Program).Source);
        return back;
    }

    [Fact]
    public void Every_block_with_every_choice_comes_back()
    {
        foreach (var def in BlockCatalog.All.Where(d => d.Shape != BlockShape.Hat && d.Type != BlockCatalog.Wait))
        {
            var p = new BlockProgram { Name = "Todo" };
            var setup = p.AddStack(0, 0, BlockCatalog.Start).Blocks;
            foreach (var slot in def.Slots.DefaultIfEmpty())
            {
                var choices = slot is null ? new SlotChoice?[] { null } : SlotChoices.For(slot.Kind).Select(c => (SlotChoice?)c);
                foreach (var choice in choices)
                {
                    var b = Add(p, setup, def.Type);
                    if (slot is not null && choice is not null)
                        b.Values[slot.Name] = choice.Value;
                    b.Body?.Add(p.NewBlock(BlockCatalog.Forward));
                }
            }
            // "hacer" needs its word to exist to become a call.
            if (def.Type == BlockCatalog.Call)
                foreach (var fn in SlotChoices.For(SlotKind.Function))
                {
                    var d = p.AddStack(0, 0, BlockCatalog.Define);
                    d.Blocks[0].Values["fn"] = fn.Value;
                    Add(p, d.Blocks, BlockCatalog.Forward);
                }
            RoundTrip(p);
        }
    }

    [Fact]
    public void Every_hat_and_music_come_back()
    {
        var p = new BlockProgram { Name = "Hats" };
        Add(p, p.AddStack(0, 0, BlockCatalog.Start).Blocks, BlockCatalog.Forward);
        Add(p, p.AddStack(0, 200, BlockCatalog.Forever).Blocks, BlockCatalog.Circle, ("place", "mouse"));
        Add(p, p.AddStack(0, 400, BlockCatalog.Click).Blocks, BlockCatalog.ScoreAdd);
        Add(p, p.AddStack(0, 600, BlockCatalog.Key).Blocks, BlockCatalog.Right);
        var music = p.AddStack(0, 800, BlockCatalog.MusicLoop).Blocks;
        Add(p, music, BlockCatalog.Instrument, ("instrument", "Saw"));
        Add(p, music, BlockCatalog.Note, ("note", "E4"));
        Add(p, music, BlockCatalog.Wait, ("beats", "0.5"));
        Add(p, music, BlockCatalog.Drum, ("drum", "snare"));
        Add(p, music, BlockCatalog.Wait, ("beats", "2"));
        Add(p, p.AddStack(0, 1000, BlockCatalog.MusicLoop).Blocks, BlockCatalog.Drum); // no wait: the blocks add "await Sleep(1)" by themselves
        var back = RoundTrip(p);
        Assert.Equal(new[] { BlockCatalog.Start, BlockCatalog.Forever, BlockCatalog.Click, BlockCatalog.Key, BlockCatalog.MusicLoop, BlockCatalog.MusicLoop },
                     back.Program.Stacks.Select(s => s.Hat!.Type));
        Assert.Single(back.Program.Stacks[^1].Blocks.Skip(1));   // the automatic wait isn't a block
    }

    [Fact]
    public void Nested_repeats_ifs_and_new_words_come_back()
    {
        var p = new BlockProgram { Name = "Anidado" };
        var define = p.AddStack(0, 0, BlockCatalog.Define);
        define.Blocks[0].Values["fn"] = "flower";
        var outer = Add(p, define.Blocks, BlockCatalog.Repeat, ("times", "12"));
        var inner = Add(p, outer.Body!, BlockCatalog.Repeat, ("times", "var:contador"));
        Add(p, inner.Body!, BlockCatalog.Forward, ("steps", "var:lado"));
        Add(p, outer.Body!, BlockCatalog.Right, ("angle", "random"));
        var forever = p.AddStack(0, 300, BlockCatalog.Forever).Blocks;
        var ifRight = Add(p, forever, BlockCatalog.If, ("cond", "right"));
        Add(p, ifRight.Body!, BlockCatalog.Call, ("fn", "flower"));
        Add(p, ifRight.Body!, BlockCatalog.VarChange, ("var", "lado"), ("amount", "-5"));
        RoundTrip(p);
    }

    [Fact]
    public void Hand_written_code_comes_back_with_its_numbers_rounded_and_the_rest_reported()
    {
        // Code written by hand, the way a child would after "Pasar a código":
        // different spacing, a counter called "vuelta", numbers that aren't choices.
        const string code = """
            public class Mio : Sketch
            {
                Turtle tortuga;
                public override void Setup()
                {
                    Size(600, 400);
                    tortuga = new Turtle(this);
                    for (int vuelta = 0;vuelta<4;vuelta++)
                    {
                        tortuga.Forward( 80 );
                        tortuga.TurnRight(90);
                    }
                    Println("listo");
                }
                public override void Draw() { Background(255); tortuga.Draw(); }
            }
            """;
        var result = CodeToBlocks.Convert(code);
        var setup = result.Program.Stacks.Single();
        Assert.Equal(BlockCatalog.Start, setup.Hat!.Type);
        var repeat = Assert.Single(setup.Blocks.Skip(1));
        Assert.Equal(BlockCatalog.Repeat, repeat.Type);
        Assert.Equal("4", repeat.Value("times"));
        Assert.Equal("75", repeat.Body![0].Value("steps"));            // 80 isn't a choice: the closest
        Assert.Contains("80 → 75", result.Rounded);
        var skipped = Assert.Single(result.Skipped);                  // Println has no block
        Assert.Equal(13, skipped.Line);
        Assert.Contains("Println", skipped.Code);
        Assert.False(result.Exact);
    }

    [Fact]
    public void Code_without_a_sketch_class_reports_everything()
    {
        var result = CodeToBlocks.Convert("var x = 3;");
        Assert.Equal(0, result.BlockCount);
        Assert.NotEmpty(result.Skipped);
    }
}
