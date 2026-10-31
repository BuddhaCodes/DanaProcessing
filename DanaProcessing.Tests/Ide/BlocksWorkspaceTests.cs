using DanaProcessing.Ide.Blocks;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>
/// Drives the block editor the way a child would -- press, drag, release --
/// in both views (tiles and lines), and checks what the program became.
/// Set DANA_BLOCKS_SHOTS to a folder to also save screenshots.
/// </summary>
public class BlocksWorkspaceTests
{
    private const int W = 1200, H = 760;

    public static IEnumerable<object[]> Views() => new[] { new object[] { BlockView.Tiles }, new object[] { BlockView.Lines } };

    private static BlocksWorkspace NewWorkspace(BlockProgram p, BlockView view, IReadOnlyCollection<string>? palette = null)
    {
        p.View = view;
        var ws = new BlocksWorkspace(p, palette) { ViewWidth = W, ViewHeight = H };
        Shot(ws, null); // paints once: the palette's buttons get their places
        return ws;
    }

    private static void Shot(BlocksWorkspace ws, string? name)
    {
        using var surface = SKSurface.Create(new SKImageInfo(W, H));
        ws.Paint(surface.Canvas);
        var dir = Environment.GetEnvironmentVariable("DANA_BLOCKS_SHOTS");
        if (name is null || string.IsNullOrEmpty(dir))
            return;
        Directory.CreateDirectory(dir);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(dir, $"{ws.View}-{name}.png"), data.ToArray());
    }

    /// <summary>Presses at <paramref name="from"/>, starts dragging, then moves to wherever <paramref name="to"/> says (asked only once the drag has started) and lets go.</summary>
    private static void Drag(BlocksWorkspace ws, SKPoint from, Func<SKPoint> to, string? shot = null)
    {
        ws.PointerDown(from);
        ws.PointerMove(new SKPoint(from.X + 12, from.Y + 8));
        var target = to();
        ws.PointerMove(new SKPoint((from.X + target.X) / 2, (from.Y + target.Y) / 2));
        ws.PointerMove(target);
        Shot(ws, shot);
        ws.PointerUp(target);
        Shot(ws, null);
    }

    private static void Click(BlocksWorkspace ws, SKPoint at)
    {
        ws.PointerDown(at);
        ws.PointerUp(at);
        Shot(ws, null);
    }

    private static SKPoint Center(SKRect r) => new(r.MidX, r.MidY);

    /// <summary>Drags a block from the palette into <paramref name="list"/> before <paramref name="index"/>.</summary>
    private static void FromPalette(BlocksWorkspace ws, string type, BlockCategory category, List<Block> list, int index, string? shot = null)
    {
        ws.ShowCategory(category);
        Shot(ws, null);
        var source = ws.PaletteRectOf(type) ?? throw new Exception("not on the palette: " + type);
        Drag(ws, Center(source), () => ws.ScreenPointOfTarget(list, index) ?? throw new Exception($"no place at index {index}"), shot);
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void A_child_builds_the_repeat_square_by_dragging(BlockView view)
    {
        var challenge = ChallengeCatalog.Get("repeat")!;
        var p = challenge.NewProgram();
        var ws = NewWorkspace(p, view, challenge.Palette);
        int changes = 0;
        ws.ProgramChanged += () => changes++;
        var script = p.Stacks[0].Blocks;
        Shot(ws, "01-start");

        FromPalette(ws, BlockCatalog.Repeat, BlockCategory.Control, script, 1);
        var repeat = Assert.Single(p.Stacks).Blocks[1];
        Assert.Equal(BlockCatalog.Repeat, repeat.Type);

        FromPalette(ws, BlockCatalog.Forward, BlockCategory.Turtle, repeat.Body!, 0);
        FromPalette(ws, BlockCatalog.Right, BlockCategory.Turtle, repeat.Body!, 1, shot: "02-dragging");
        Assert.Equal(new[] { BlockCatalog.Forward, BlockCatalog.Right }, repeat.Body!.Select(b => b.Type));
        Assert.Single(p.Stacks);
        Assert.Equal(3, changes);
        Shot(ws, "03-square");

        var result = ChallengeChecker.Check(challenge, p, HeadlessBlockRunner.Run(p).TurtleLines);
        Assert.True(result.Passed, result.Message);
        Assert.Equal(3, result.Stars);
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void Picking_a_value_from_the_picker_changes_the_slot(BlockView view)
    {
        var p = new BlockProgram();
        var stack = p.AddStack(40, 40, BlockCatalog.Start, BlockCatalog.Right, BlockCatalog.PenColor);
        var turn = stack.Blocks[1];
        var color = stack.Blocks[2];
        var ws = NewWorkspace(p, view);

        Click(ws, Center(ws.ScreenRectOfSlot(turn.Id, "angle")!.Value));
        Assert.NotEmpty(ws.PickerOptions);
        Shot(ws, "04-picker-angle");
        Click(ws, Center(ws.PickerOptions.First(o => o.Choice.Value == "144").Rect));
        Assert.Equal("144", turn.Value("angle"));
        Assert.Empty(ws.PickerOptions);

        Click(ws, Center(ws.ScreenRectOfSlot(color.Id, "color")!.Value));
        Shot(ws, "05-picker-colors");
        Click(ws, Center(ws.PickerOptions.First(o => o.Choice.Value == "green").Rect));
        Assert.Equal("green", color.Value("color"));
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void Clicking_outside_the_picker_closes_it_without_changing_anything(BlockView view)
    {
        var p = new BlockProgram();
        var turn = p.AddStack(40, 40, BlockCatalog.Start, BlockCatalog.Right).Blocks[1];
        var ws = NewWorkspace(p, view);
        Click(ws, Center(ws.ScreenRectOfSlot(turn.Id, "angle")!.Value));
        Click(ws, new SKPoint(W - 300, H - 60));
        Assert.Empty(ws.PickerOptions);
        Assert.Equal("90", turn.Value("angle"));
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void Dragging_a_block_takes_the_ones_after_it_and_the_palette_deletes_them_until_undo(BlockView view)
    {
        var p = new BlockProgram();
        var stack = p.AddStack(40, 40, BlockCatalog.Start, BlockCatalog.Forward, BlockCatalog.Right, BlockCatalog.Forward);
        var second = stack.Blocks[2];
        var ws = NewWorkspace(p, view);

        // Pull "girar" (and the "avanzar" after it) away to empty space.
        Drag(ws, Center(ws.ScreenRectOf(second.Id)!.Value), () => new SKPoint(W - 300, H - 110));
        Assert.Equal(2, p.Stacks.Count);
        Assert.Equal(2, stack.Blocks.Count);
        var loose = p.Stacks.Single(s => s != stack);
        Assert.Equal(new[] { BlockCatalog.Right, BlockCatalog.Forward }, loose.Blocks.Select(b => b.Type));
        Assert.Null(loose.Hat);
        Shot(ws, "06-loose");

        // Throw that column onto the palette: deleted.
        Drag(ws, Center(ws.ScreenRectOf(loose.Blocks[0].Id)!.Value), () => new SKPoint(100, 300));
        Assert.Single(p.Stacks);
        Assert.Equal(2, ws.Program.AllBlocks().Count());

        ws.Undo();
        Assert.Equal(2, ws.Program.Stacks.Count);
        ws.Undo();
        Assert.Single(ws.Program.Stacks);
        Assert.Equal(4, ws.Program.Stacks[0].Blocks.Count);
        ws.Redo();
        Assert.Equal(2, ws.Program.Stacks.Count);
        Assert.Equal(view, ws.Program.View); // undo is about blocks, never about the view
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void A_loose_column_goes_on_top_of_another_column_without_a_start(BlockView view)
    {
        var p = new BlockProgram();
        var lower = p.AddStack(400, 400, BlockCatalog.Forward);
        var upper = p.AddStack(40, 40, BlockCatalog.Right);
        var ws = NewWorkspace(p, view);

        Drag(ws, Center(ws.ScreenRectOf(upper.Blocks[0].Id)!.Value),
             () => ws.ScreenPointOfTarget(lower.Blocks, 0, prepend: true) ?? throw new Exception("no place on top"));
        var only = Assert.Single(p.Stacks);
        Assert.Equal(new[] { BlockCatalog.Right, BlockCatalog.Forward }, only.Blocks.Select(b => b.Type));
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void A_start_block_never_goes_after_another_block(BlockView view)
    {
        var p = new BlockProgram();
        var column = p.AddStack(40, 40, BlockCatalog.Start, BlockCatalog.Forward);
        var ws = NewWorkspace(p, view);
        ws.ShowCategory(BlockCategory.Events);
        Shot(ws, null);
        var source = ws.PaletteRectOf(BlockCatalog.Forever)!.Value;
        Drag(ws, Center(source), () => ws.ScreenPointOfTarget(column.Blocks, 2)!.Value);
        Assert.Equal(2, p.Stacks.Count);   // it became its own script
        Assert.Equal(2, column.Blocks.Count);
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void Selecting_and_deleting_a_block(BlockView view)
    {
        var p = new BlockProgram();
        var stack = p.AddStack(40, 40, BlockCatalog.Start, BlockCatalog.Forward, BlockCatalog.Home);
        var ws = NewWorkspace(p, view);
        int? selected = null;
        ws.SelectionChanged += id => selected = id;
        var r = ws.ScreenRectOf(stack.Blocks[2].Id)!.Value;
        Click(ws, new SKPoint(r.Left + 12, r.Top + 12));
        Assert.Equal(stack.Blocks[2].Id, selected);
        Shot(ws, "07-selected");
        ws.DeleteSelected();
        Assert.Equal(new[] { BlockCatalog.Start, BlockCatalog.Forward }, stack.Blocks.Select(b => b.Type));
        Assert.Null(selected);
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void Running_blocks_light_up(BlockView view)
    {
        var p = new BlockProgram();
        var stack = p.AddStack(40, 40, BlockCatalog.Start, BlockCatalog.Repeat);
        var fwd = p.NewBlock(BlockCatalog.Forward);
        stack.Blocks[1].Body!.Add(fwd);
        stack.Blocks[1].Body!.Add(p.NewBlock(BlockCatalog.Right));
        var ws = NewWorkspace(p, view);
        long now = Environment.TickCount64;

        using var plain = SKSurface.Create(new SKImageInfo(W, H));
        ws.Paint(plain.Canvas);
        ws.LastHit = id => id == fwd.Id ? now : 0;
        using var lit = SKSurface.Create(new SKImageInfo(W, H));
        ws.Paint(lit.Canvas);
        Shot(ws, "08-running");

        var r = ws.ScreenRectOf(fwd.Id)!.Value;
        var probe = new SKPointI((int)r.Left - 6, (int)r.MidY);
        Assert.NotEqual(Pixel(plain, probe), Pixel(lit, probe));
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void Panning_and_zooming_move_the_view_not_the_blocks(BlockView view)
    {
        var p = new BlockProgram();
        var stack = p.AddStack(40, 40, BlockCatalog.Start, BlockCatalog.Forward);
        var ws = NewWorkspace(p, view);
        var before = ws.ScreenRectOf(stack.Blocks[1].Id)!.Value;
        Drag(ws, new SKPoint(W - 200, H - 220), () => new SKPoint(W - 300, H - 270)); // empty space: pans
        var after = ws.ScreenRectOf(stack.Blocks[1].Id)!.Value;
        Assert.Equal(before.Left - 100, after.Left, 1);
        Assert.Equal(before.Top - 50, after.Top, 1);
        Assert.Equal(40, stack.X);

        ws.ZoomBy(2);
        Assert.Equal(after.Width * 2, ws.ScreenRectOf(stack.Blocks[1].Id)!.Value.Width, 1);
        ws.ResetZoom();
        Assert.Equal(before.Width, ws.ScreenRectOf(stack.Blocks[1].Id)!.Value.Width, 1);
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void A_challenge_palette_offers_only_its_blocks(BlockView view)
    {
        var challenge = ChallengeCatalog.Get("first-step")!;
        var ws = NewWorkspace(challenge.NewProgram(), view, challenge.Palette);
        Assert.Equal(new[] { BlockCategory.Events, BlockCategory.Turtle }, ws.VisibleCategories());
        ws.ShowCategory(BlockCategory.Turtle);
        Assert.NotNull(ws.PaletteRectOf(BlockCatalog.Forward));
        Assert.Null(ws.PaletteRectOf(BlockCatalog.Right));
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void Every_block_and_every_picker_paints(BlockView view)
    {
        var p = new BlockProgram();
        var all = p.AddStack(20, 20, BlockCatalog.Start);
        foreach (var def in BlockCatalog.All.Where(d => d.Shape != BlockShape.Hat))
        {
            var b = p.NewBlock(def.Type);
            b.Body?.Add(p.NewBlock(BlockCatalog.Forward));
            all.Blocks.Add(b);
        }
        double y = 400;
        foreach (var hat in BlockCatalog.All.Where(d => d.Shape == BlockShape.Hat && d.Type != BlockCatalog.Start))
        {
            p.AddStack(20, y, hat.Type);
            y += 160;
        }
        var ws = NewWorkspace(p, view);
        Shot(ws, "09-all-blocks");
        foreach (var c in BlockCatalog.Categories)
        {
            ws.ShowCategory(c);
            Shot(ws, "10-palette-" + c);
        }
        // Every slot's picker opens and paints.
        foreach (var b in all.Blocks.Skip(1).Where(b => b.Def.Slots.Count > 0).Take(30))
        {
            foreach (var slot in b.Def.Slots)
            {
                if (ws.ScreenRectOfSlot(b.Id, slot.Name) is not { } r || r.Right > W || r.Bottom > H || r.Left < ws.PaletteW)
                    continue; // off screen in this view: the others cover it
                Click(ws, Center(r));
                Assert.NotEmpty(ws.PickerOptions);
                Click(ws, new SKPoint(W - 40, 20));
            }
        }
    }

    [Fact]
    public void Switching_views_keeps_the_program_and_its_code()
    {
        var p = ChallengeCatalog.Get("star")!.NewProgram();
        var rep = p.NewBlock(BlockCatalog.Repeat);
        rep.Body!.Add(p.NewBlock(BlockCatalog.Forward));
        p.Stacks[0].Blocks.Add(rep);
        var code = BlockCodeGenerator.Generate(p).Source;
        var ws = NewWorkspace(p, BlockView.Tiles);
        int changes = 0;
        ws.ProgramChanged += () => changes++;

        ws.SetView(BlockView.Lines);
        Assert.Equal(BlockView.Lines, ws.View);
        Assert.Equal(BlockView.Lines, ws.Program.View);
        Assert.Equal(1, changes); // worth saving
        Assert.Equal(code, BlockCodeGenerator.Generate(ws.Program).Source);
        Assert.Equal(BlockView.Lines, BlockProgram.FromJson(ws.Program.ToJson()).View);
        Assert.Contains("\"View\": \"Lines\"", ws.Program.ToJson());

        ws.SetView(BlockView.Lines);
        Assert.Equal(1, changes); // already there: nothing happens
        ws.SetView(BlockView.Tiles);
        Assert.Equal(BlockView.Tiles, ws.Program.View);
    }

    [Fact]
    public void Old_files_without_a_view_open_as_tiles() =>
        Assert.Equal(BlockView.Tiles, BlockProgram.FromJson("""{ "Stacks": [] }""").View);

    [Fact]
    public void Challenges_open_in_their_own_view()
    {
        Assert.Equal(BlockView.Tiles, ChallengeCatalog.Get("first-step")!.NewProgram().View);
        Assert.Equal(BlockView.Lines, ChallengeCatalog.Get("points")!.NewProgram().View);
    }

    private static SKColor Pixel(SKSurface s, SKPointI p)
    {
        using var img = s.Snapshot();
        using var bmp = SKBitmap.FromImage(img);
        return bmp.GetPixel(p.X, p.Y);
    }
}

public class BlocksWorkspacePaintTests
{
    [Fact]
    public void Painting_after_a_resize_never_asks_to_be_painted_again()
    {
        // Avalonia throws if a control invalidates itself during its own render.
        var p = new BlockProgram { View = BlockView.Lines };
        p.AddStack(0, 0, BlockCatalog.Start, BlockCatalog.Forward);
        using var ws = new BlocksWorkspace(p) { ViewWidth = 900, ViewHeight = 600 };
        int invalidations = 0;
        ws.Invalidated += () => invalidations++;
        using var surface = SKSurface.Create(new SKImageInfo(1300, 700));
        ws.ViewWidth = 1300;
        ws.Paint(surface.Canvas);
        Assert.Equal(0, invalidations);
    }
}
