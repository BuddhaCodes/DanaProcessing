using DanaProcessing.Ide.Blocks;
using DanaProcessing.Ide.Compilation;
using Xunit;

namespace DanaProcessing.Tests.Ide;

public class BlockCodeGeneratorTests
{
    private static readonly string[] Hats =
        { BlockCatalog.Start, BlockCatalog.Forever, BlockCatalog.Click, BlockCatalog.Key, BlockCatalog.MusicLoop, BlockCatalog.Define };

    private static void AssertCompiles(BlockProgram p, string what)
    {
        foreach (var instrument in new[] { false, true })
        {
            var code = BlockCodeGenerator.Generate(p, instrument);
            var result = SketchCompiler.Compile(code.Source);
            Assert.True(result.Success, $"{what} (instrument={instrument}):\n{string.Join("\n", result.Errors)}\n\n{code.Source}");
        }
    }

    public static IEnumerable<object[]> StatementTypes() =>
        BlockCatalog.All.Where(d => d.Shape != BlockShape.Hat).Select(d => new object[] { d.Type });

    [Theory]
    [MemberData(nameof(StatementTypes))]
    public void Every_block_compiles_under_every_hat_with_every_choice(string type)
    {
        var def = BlockCatalog.Get(type);
        foreach (var hat in Hats)
        {
            var p = new BlockProgram();
            var stack = p.AddStack(0, 0, hat);
            // One copy per choice of every slot, so every value's code is compiled.
            foreach (var slot in def.Slots.DefaultIfEmpty())
            {
                IEnumerable<SlotChoice?> choices = slot is null ? new SlotChoice?[] { null } : SlotChoices.For(slot.Kind).Select(c => (SlotChoice?)c);
                foreach (var choice in choices)
                {
                    var b = p.NewBlock(type);
                    if (slot is not null && choice is not null)
                        b.Values[slot.Name] = choice.Value;
                    if (b.Body is not null)
                        b.Body.Add(p.NewBlock(BlockCatalog.Forward));
                    stack.Blocks.Add(b);
                }
            }
            AssertCompiles(p, $"{type} under {hat}");
        }
    }

    [Fact]
    public void An_empty_program_is_still_a_sketch()
    {
        AssertCompiles(new BlockProgram(), "empty");
        var code = BlockCodeGenerator.Generate(new BlockProgram()).Source;
        Assert.Contains("public override void Setup()", code);
        Assert.Contains("public override void Draw()", code);
        Assert.DoesNotContain("Turtle", code);
    }

    [Fact]
    public void Nested_repeats_get_their_own_counters_and_compile()
    {
        var p = new BlockProgram();
        var stack = p.AddStack(0, 0, BlockCatalog.Start);
        var outer = p.NewBlock(BlockCatalog.Repeat);
        var middle = p.NewBlock(BlockCatalog.Repeat);
        var inner = p.NewBlock(BlockCatalog.Repeat);
        var deepest = p.NewBlock(BlockCatalog.Repeat);
        deepest.Body!.Add(p.NewBlock(BlockCatalog.Forward));
        inner.Body!.Add(deepest);
        middle.Body!.Add(inner);
        outer.Body!.Add(middle);
        stack.Blocks.Add(outer);
        AssertCompiles(p, "four nested repeats");
        var code = BlockCodeGenerator.Generate(p).Source;
        Assert.Contains("int i = 0", code);
        Assert.Contains("int j = 0", code);
        Assert.Contains("int k = 0", code);
        Assert.Contains("int n3 = 0", code);
    }

    [Fact]
    public void Clean_code_has_no_instrumentation_and_instrumented_code_marks_every_block()
    {
        var p = new BlockProgram();
        var stack = p.AddStack(0, 0, BlockCatalog.Forever, BlockCatalog.Circle);
        p.AddStack(0, 300, BlockCatalog.Start, BlockCatalog.Forward);

        var clean = BlockCodeGenerator.Generate(p).Source;
        Assert.DoesNotContain("BlockHit", clean);
        Assert.DoesNotContain(".Block =", clean);

        var run = BlockCodeGenerator.Generate(p, instrument: true).Source;
        var circle = stack.Blocks[1];
        Assert.Contains($"BlockHit({circle.Id});", run);
        Assert.Contains($"BlockHit({stack.Blocks[0].Id});", run);
        var forward = p.Stacks[1].Blocks[1];
        Assert.Contains($"tortuga.Block = {forward.Id};", run);       // lights up when it walks...
        Assert.DoesNotContain($"BlockHit({forward.Id});", run);        // ...not when it's queued
    }

    [Fact]
    public void Loose_columns_without_a_hat_do_nothing()
    {
        var p = new BlockProgram();
        p.AddStack(0, 0, BlockCatalog.Forward, BlockCatalog.Circle);
        var code = BlockCodeGenerator.Generate(p).Source;
        Assert.DoesNotContain("Forward", code);
        Assert.DoesNotContain("Circle", code);
        Assert.DoesNotContain("Turtle", code);
    }

    [Fact]
    public void Line_map_points_each_block_at_its_own_code()
    {
        var p = new BlockProgram();
        var stack = p.AddStack(0, 0, BlockCatalog.Start, BlockCatalog.Repeat);
        var repeat = stack.Blocks[1];
        var forward = p.NewBlock(BlockCatalog.Forward);
        repeat.Body!.Add(forward);
        var code = BlockCodeGenerator.Generate(p);
        var lines = code.Source.Split('\n');

        var (f1, f2) = code.BlockLines[forward.Id];
        Assert.Equal(f1, f2);
        Assert.Contains("tortuga.Forward(100);", lines[f1 - 1]);

        var (r1, r2) = code.BlockLines[repeat.Id];
        Assert.Contains("for (int i = 0; i < 4; i++)", lines[r1 - 1]);
        Assert.True(r1 < f1 && f2 < r2, "the repeat covers what's inside it");
        Assert.Equal("}", lines[r2 - 1].Trim());

        Assert.Equal(forward.Id, code.BlockAtLine(f1));
        Assert.Equal(repeat.Id, code.BlockAtLine(r1));
    }

    [Fact]
    public void Several_columns_with_the_same_hat_run_top_to_bottom()
    {
        var p = new BlockProgram();
        p.AddStack(0, 400, BlockCatalog.Start, BlockCatalog.Home);
        p.AddStack(300, 0, BlockCatalog.Start, BlockCatalog.Forward);
        var code = BlockCodeGenerator.Generate(p).Source;
        Assert.True(code.IndexOf("Forward(") < code.IndexOf("Home("), code);
    }

    [Fact]
    public void Music_loops_become_live_loops_and_wait_by_themselves_if_asked_nothing()
    {
        var p = new BlockProgram();
        p.AddStack(0, 0, BlockCatalog.MusicLoop, BlockCatalog.Drum);
        p.AddStack(0, 200, BlockCatalog.MusicLoop, BlockCatalog.Note, BlockCatalog.Wait);
        var code = BlockCodeGenerator.Generate(p).Source;
        Assert.Contains("[LiveLoop]", code);
        Assert.Contains("async Task Musica1()", code);
        Assert.Contains("async Task Musica2()", code);
        Assert.Contains("await Sleep(1); //", code);   // the one with no "esperar"
        Assert.Equal(2, code.Split("await Sleep(").Length - 1);
    }

    [Theory]
    [InlineData("Mi dibujo", "MiDibujo")]
    [InlineData("la tortuga dibujó 2 casas", "LaTortugaDibujo2Casas")]
    [InlineData("3 estrellas", "Programa3Estrellas")]
    [InlineData("¡¡!!", "Programa")]
    [InlineData("sketch", "Sketch1")]
    [InlineData(null, "Programa")]
    public void Class_names_are_valid_csharp(string? name, string expected) =>
        Assert.Equal(expected, BlockCodeGenerator.ClassName(name));

    [Fact]
    public void Json_round_trip_keeps_everything_and_repairs_bad_files()
    {
        var p = new BlockProgram { Name = "Casa", ChallengeId = "square" };
        var stack = p.AddStack(12, 34, BlockCatalog.Start, BlockCatalog.Repeat);
        stack.Blocks[1].Values["times"] = "5";
        stack.Blocks[1].Body!.Add(p.NewBlock(BlockCatalog.Forward));

        var back = BlockProgram.FromJson(p.ToJson());
        Assert.Equal("Casa", back.Name);
        Assert.Equal("square", back.ChallengeId);
        Assert.Equal(12, back.Stacks[0].X);
        Assert.Equal("5", back.Stacks[0].Blocks[1].Value("times"));
        Assert.Single(back.Stacks[0].Blocks[1].Body!);
        Assert.Equal(BlockCodeGenerator.Generate(p).Source, BlockCodeGenerator.Generate(back).Source);

        // A file from a newer IDE (unknown block), a duplicated id and a bad value.
        const string json = """
        { "Stacks": [ { "X": 0, "Y": 0, "Blocks": [
            { "Id": 1, "Type": "event.start" },
            { "Id": 1, "Type": "turtle.forward", "Values": { "steps": "999999" } },
            { "Id": 2, "Type": "rocket.launch" } ] } ] }
        """;
        var repaired = BlockProgram.FromJson(json);
        var blocks = repaired.Stacks[0].Blocks;
        Assert.Equal(2, blocks.Count);
        Assert.NotEqual(blocks[0].Id, blocks[1].Id);
        Assert.Equal("100", blocks[1].Value("steps"));   // invalid value -> the slot's default
        Assert.True(repaired.NextId > blocks.Max(b => b.Id));
        AssertCompiles(repaired, "repaired file");
    }

    [Fact]
    public void A_turtle_program_draws_its_shape_when_run_headless()
    {
        var p = new BlockProgram();
        var stack = p.AddStack(0, 0, BlockCatalog.Start, BlockCatalog.Repeat);
        stack.Blocks[1].Body!.Add(p.NewBlock(BlockCatalog.Forward));
        stack.Blocks[1].Body!.Add(p.NewBlock(BlockCatalog.Right));

        var run = HeadlessBlockRunner.Run(p);
        Assert.True(run.Compiled, string.Join("\n", run.Errors));
        Assert.Equal(4, run.TurtleLines.Count);
        Assert.All(run.TurtleLines, l => Assert.InRange(l.Length, 99.9f, 100.1f));
        Assert.Equal(300, run.TurtleLines[0].X1, 0.01f);   // starts in the middle of 600 x 400
        Assert.Equal(200, run.TurtleLines[0].Y1, 0.01f);
    }

    [Fact]
    public void Ancestors_and_locate_find_where_a_block_lives()
    {
        var p = new BlockProgram();
        var stack = p.AddStack(0, 0, BlockCatalog.Start, BlockCatalog.Repeat);
        var repeat = stack.Blocks[1];
        var iff = p.NewBlock(BlockCatalog.If);
        var fwd = p.NewBlock(BlockCatalog.Forward);
        iff.Body!.Add(fwd);
        repeat.Body!.Add(iff);

        Assert.Equal(new[] { iff.Id, repeat.Id, stack.Blocks[0].Id }, p.AncestorsOf(fwd.Id));
        var where = p.Locate(fwd.Id);
        Assert.NotNull(where);
        Assert.Same(iff.Body, where!.Value.List);
        Assert.Same(iff, where.Value.Owner);
        Assert.Null(p.Locate(12345));
    }

    // =================================================================
    // Variables, own blocks, random
    // =================================================================

    private static Block Add(BlockProgram p, List<Block> into, string type, params (string Slot, string Value)[] values)
    {
        var b = p.NewBlock(type);
        foreach (var (slot, value) in values)
            b.Values[slot] = value;
        into.Add(b);
        return b;
    }

    [Fact]
    public void A_variable_grows_a_spiral()
    {
        // guardar 10 en lado; repetir 8: avanzar lado, girar 90, cambiar lado en +10
        var p = new BlockProgram();
        var script = p.AddStack(0, 0, BlockCatalog.Start).Blocks;
        Add(p, script, BlockCatalog.VarSet, ("value", "10"), ("var", "lado"));
        var loop = Add(p, script, BlockCatalog.Repeat, ("times", "8"));
        Add(p, loop.Body!, BlockCatalog.Forward, ("steps", "var:lado"));
        Add(p, loop.Body!, BlockCatalog.Right, ("angle", "90"));
        Add(p, loop.Body!, BlockCatalog.VarChange, ("var", "lado"), ("amount", "10"));

        var code = BlockCodeGenerator.Generate(p).Source;
        Assert.Contains("float lado = 0;", code);
        Assert.Contains("lado = 10;", code);
        Assert.Contains("tortuga.Forward(lado);", code);
        Assert.Contains("lado = lado + 10;", code);
        Assert.DoesNotContain("contador", code); // only the variables that are used

        var run = HeadlessBlockRunner.Run(p);
        Assert.True(run.Compiled, string.Join("\n", run.Errors));
        var lengths = run.TurtleLines.Select(l => (int)Math.Round(l.Length)).ToArray();
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 60, 70, 80 }, lengths);
    }

    [Fact]
    public void Changing_by_a_negative_amount_subtracts()
    {
        var p = new BlockProgram();
        var script = p.AddStack(0, 0, BlockCatalog.Start).Blocks;
        Add(p, script, BlockCatalog.VarChange, ("var", "contador"), ("amount", "-5"));
        Add(p, script, BlockCatalog.VarShow, ("var", "contador"));
        var code = BlockCodeGenerator.Generate(p).Source;
        Assert.Contains("contador = contador - 5;", code);
        Assert.Contains("Text(\"contador: \" + contador", code);
        AssertCompiles(p, "subtract and show");
    }

    [Fact]
    public void Teaching_the_turtle_a_new_word_makes_a_method_and_using_it_calls_it()
    {
        // para hacer un cuadrado: repetir 4 (avanzar 50, girar 90)
        // al empezar: hacer un cuadrado, avanzar 100, hacer un cuadrado
        var p = new BlockProgram();
        var define = p.AddStack(300, 0, BlockCatalog.Define);
        define.Blocks[0].Values["fn"] = "square";
        var loop = Add(p, define.Blocks, BlockCatalog.Repeat, ("times", "4"));
        Add(p, loop.Body!, BlockCatalog.Forward, ("steps", "50"));
        Add(p, loop.Body!, BlockCatalog.Right, ("angle", "90"));

        var main = p.AddStack(0, 0, BlockCatalog.Start).Blocks;
        Add(p, main, BlockCatalog.Call, ("fn", "square"));
        Add(p, main, BlockCatalog.PenUp);
        Add(p, main, BlockCatalog.Forward, ("steps", "100"));
        Add(p, main, BlockCatalog.PenDown);
        Add(p, main, BlockCatalog.Call, ("fn", "square"));

        var generated = BlockCodeGenerator.Generate(p);
        Assert.Contains("void Cuadrado()", generated.Source);
        Assert.Equal(2, generated.Source.Split("Cuadrado();").Length - 1);
        Assert.True(generated.BlockLines.ContainsKey(define.Blocks[0].Id), "the define block maps to its method");

        var run = HeadlessBlockRunner.Run(p);
        Assert.True(run.Compiled, string.Join("\n", run.Errors));
        Assert.Equal(8, run.TurtleLines.Count); // two squares, the walk between them with the pen up
    }

    [Fact]
    public void A_word_that_uses_itself_is_left_out_instead_of_looping_forever()
    {
        var p = new BlockProgram();
        var a = p.AddStack(300, 0, BlockCatalog.Define);
        a.Blocks[0].Values["fn"] = "flower";
        Add(p, a.Blocks, BlockCatalog.Forward, ("steps", "10"));
        Add(p, a.Blocks, BlockCatalog.Call, ("fn", "flower"));          // itself
        Add(p, a.Blocks, BlockCatalog.Call, ("fn", "jump"));            // through "jump"
        var b = p.AddStack(600, 0, BlockCatalog.Define);
        b.Blocks[0].Values["fn"] = "jump";
        Add(p, b.Blocks, BlockCatalog.Call, ("fn", "flower"));
        var main = p.AddStack(0, 0, BlockCatalog.Start).Blocks;
        Add(p, main, BlockCatalog.Call, ("fn", "flower"));
        Add(p, main, BlockCatalog.Call, ("fn", "trick"));               // never defined

        var code = BlockCodeGenerator.Generate(p).Source;
        Assert.Contains("no puede usarse dentro de sí mismo", code);
        Assert.Contains("todavía no tiene bloques", code);
        Assert.DoesNotContain("MiTruco();", code);
        var run = HeadlessBlockRunner.Run(p);                           // finishes: no endless recursion
        Assert.True(run.Compiled, string.Join("\n", run.Errors));
        Assert.Single(run.TurtleLines);
    }

    [Fact]
    public void Random_and_arrow_key_choices_compile()
    {
        var p = new BlockProgram();
        var forever = p.AddStack(0, 0, BlockCatalog.Forever).Blocks;
        foreach (var cond in new[] { "right", "left", "up", "down", "space", "mouseRight", "mouseTop", "counter10" })
        {
            var ifBlock = Add(p, forever, BlockCatalog.If, ("cond", cond));
            Add(p, ifBlock.Body!, BlockCatalog.Forward, ("steps", "random"));
            Add(p, ifBlock.Body!, BlockCatalog.Right, ("angle", "random"));
        }
        var rep = Add(p, forever, BlockCatalog.Repeat, ("times", "random"));
        Add(p, rep.Body!, BlockCatalog.VarSet, ("value", "random"), ("var", "angulo"));
        AssertCompiles(p, "random and keys");
        Assert.Contains("float contador = 0;", BlockCodeGenerator.Generate(p).Source);
    }
}
