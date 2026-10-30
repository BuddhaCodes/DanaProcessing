using DanaProcessing.Ide.Blocks;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>
/// Drives the block editor the way a child would -- press, drag, release --
/// and checks what the program became. Set DANA_BLOCKS_SHOTS to a folder to
/// also save screenshots of each step.
/// </summary>
public class BlocksWorkspaceTests
{
    private const int W = 1200, H = 760;

    private static BlocksWorkspace NewWorkspace(BlockProgram p, IReadOnlyCollection<string>? palette = null)
    {
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
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), data.ToArray());
    }

    private static void DragTo(BlocksWorkspace ws, SKPoint from, SKPoint to)
    {
        ws.PointerDown(from);
        var mid = new SKPoint((from.X + to.X) / 2, (from.Y + to.Y) / 2);
        ws.PointerMove(mid);
        ws.PointerMove(to);
        Shot(ws, null);
        ws.PointerUp(to);
        Shot(ws, null);
    }

    private static void Click(BlocksWorkspace ws, SKPoint at)
    {
        ws.PointerDown(at);
        ws.PointerUp(at);
        Shot(ws, null);
    }

    /// <summary>Drags a block from the palette so its top-left lands just below <paramref name="blockId"/>'s header.</summary>
    private static void DragFromPaletteBelow(BlocksWorkspace ws, string type, BlockCategory category, SKRect below)
    {
        ws.ShowCategory(category);
        Shot(ws, null);
        var source = ws.PaletteRectOf(type) ?? throw new Exception("not on the palette: " + type);
        var grab = new SKPoint(source.Left + 30, source.Top + 20);
        DragTo(ws, grab, new SKPoint(below.Left + 30 + 6, below.Bottom + 20 + 5));
    }

    [Fact]
    public void A_child_builds_the_repeat_square_by_dragging()
    {
        var challenge = ChallengeCatalog.Get("repeat")!;
        var p = challenge.NewProgram();
        var ws = NewWorkspace(p, challenge.Palette);
        int changes = 0;
        ws.ProgramChanged += () => changes++;
        var hat = p.Stacks[0].Blocks[0];
        Shot(ws, "01-start");

        DragFromPaletteBelow(ws, BlockCatalog.Repeat, BlockCategory.Control, ws.ScreenRectOf(hat.Id)!.Value);
        var repeat = Assert.Single(p.Stacks).Blocks[1];
        Assert.Equal(BlockCatalog.Repeat, repeat.Type);

        // Into the repeat's mouth: just below its header, indented by the arm.
        var header = ws.ScreenRectOf(repeat.Id)!.Value;
        DragFromPaletteBelow(ws, BlockCatalog.Forward, BlockCategory.Turtle,
            new SKRect(header.Left + BlockStyle.ArmW, header.Top, header.Right, header.Bottom));
        var forward = Assert.Single(repeat.Body!);

        DragFromPaletteBelow(ws, BlockCatalog.Right, BlockCategory.Turtle, ws.ScreenRectOf(forward.Id)!.Value);
        Assert.Equal(new[] { BlockCatalog.Forward, BlockCatalog.Right }, repeat.Body!.Select(b => b.Type));
        Assert.Single(p.Stacks);
        Assert.Equal(3, changes);
        Shot(ws, "02-square");

        var result = ChallengeChecker.Check(challenge, p, HeadlessBlockRunner.Run(p).TurtleLines);
        Assert.True(result.Passed, result.Message);
        Assert.Equal(3, result.Stars);
    }

    [Fact]
    public void Picking_a_value_from_the_picker_changes_the_slot()
    {
        var p = new BlockProgram();
        var stack = p.AddStack(40, 40, BlockCatalog.Start, BlockCatalog.Right);
        var turn = stack.Blocks[1];
        var ws = NewWorkspace(p);

        Click(ws, Center(ws.ScreenRectOfSlot(turn.Id, "angle")!.Value));
        Assert.NotEmpty(ws.PickerOptions);
        Shot(ws, "03-picker");
        var option = ws.PickerOptions.First(o => o.Choice.Value == "144");
        Click(ws, Center(option.Rect));
        Assert.Equal("144", turn.Value("angle"));
        Assert.Empty(ws.PickerOptions);

        // Colors too: the picker shows swatches.
        var color = p.NewBlock(BlockCatalog.PenColor);
        stack.Blocks.Add(color);
        ws.Load(p, null);
        Shot(ws, null);
        Click(ws, Center(ws.ScreenRectOfSlot(color.Id, "color")!.Value));
        Shot(ws, "04-colors");
        Click(ws, Center(ws.PickerOptions.First(o => o.Choice.Value == "green").Rect));
        Assert.Equal("green", color.Value("color"));
    }

    [Fact]
    public void Clicking_outside_the_picker_closes_it_without_changing_anything()
    {
        var p = new BlockProgram();
        var turn = p.AddStack(40, 40, BlockCatalog.Start, BlockCatalog.Right).Blocks[1];
        var ws = NewWorkspace(p);
        Click(ws, Center(ws.ScreenRectOfSlot(turn.Id, "angle")!.Value));
        Click(ws, new SKPoint(W - 200, H - 300));
        Assert.Empty(ws.PickerOptions);
        Assert.Equal("90", turn.Value("angle"));
    }

    [Fact]
    public void Dragging_a_block_takes_the_ones_below_it_and_the_palette_deletes_them_until_undo()
    {
        var p = new BlockProgram();
        var stack = p.AddStack(40, 40, BlockCatalog.Start, BlockCatalog.Forward, BlockCatalog.Right, BlockCatalog.Forward);
        var second = stack.Blocks[2];
        var ws = NewWorkspace(p);

        // Pull "girar" (and the "avanzar" under it) away to empty space.
        var r = ws.ScreenRectOf(second.Id)!.Value;
        DragTo(ws, new SKPoint(r.Left + 20, r.MidY), new SKPoint(900, 500));
        Assert.Equal(2, p.Stacks.Count);
        Assert.Equal(2, stack.Blocks.Count);
        var loose = p.Stacks.Single(s => s != stack);
        Assert.Equal(new[] { BlockCatalog.Right, BlockCatalog.Forward }, loose.Blocks.Select(b => b.Type));
        Assert.Null(loose.Hat);
        Shot(ws, "05-loose");

        // Throw that column onto the palette: deleted.
        r = ws.ScreenRectOf(loose.Blocks[0].Id)!.Value;
        DragTo(ws, new SKPoint(r.Left + 20, r.MidY), new SKPoint(150, 300));
        Assert.Single(p.Stacks);
        Assert.Equal(2, ws.Program.AllBlocks().Count());

        ws.Undo();
        Assert.Equal(2, ws.Program.Stacks.Count);
        ws.Undo();
        Assert.Single(ws.Program.Stacks);
        Assert.Equal(4, ws.Program.Stacks[0].Blocks.Count);
        ws.Redo();
        Assert.Equal(2, ws.Program.Stacks.Count);
    }

    [Fact]
    public void A_loose_column_snaps_back_on_top_of_another_hatless_column()
    {
        var p = new BlockProgram();
        var lower = p.AddStack(300, 300, BlockCatalog.Forward);
        var upper = p.AddStack(40, 40, BlockCatalog.Right);
        var ws = NewWorkspace(p);

        // Drop "girar" so its bottom meets "avanzar"'s top.
        var target = ws.ScreenRectOf(lower.Blocks[0].Id)!.Value;
        var r = ws.ScreenRectOf(upper.Blocks[0].Id)!.Value;
        DragTo(ws, new SKPoint(r.Left + 20, r.Top + 20), new SKPoint(target.Left + 20 + 3, target.Top - BlockStyle.RowH + 20 + 4));
        var only = Assert.Single(p.Stacks);
        Assert.Equal(new[] { BlockCatalog.Right, BlockCatalog.Forward }, only.Blocks.Select(b => b.Type));
    }

    [Fact]
    public void A_hat_never_snaps_below_a_block()
    {
        var p = new BlockProgram();
        var column = p.AddStack(40, 40, BlockCatalog.Start, BlockCatalog.Forward);
        var ws = NewWorkspace(p);
        ws.ShowCategory(BlockCategory.Events);
        Shot(ws, null);
        var source = ws.PaletteRectOf(BlockCatalog.Forever)!.Value;
        var below = ws.ScreenRectOf(column.Blocks[1].Id)!.Value;
        DragTo(ws, new SKPoint(source.Left + 30, source.Top + 20), new SKPoint(below.Left + 30, below.Bottom + 20));
        Assert.Equal(2, p.Stacks.Count);   // it became its own script
        Assert.Equal(2, column.Blocks.Count);
    }

    [Fact]
    public void Selecting_and_deleting_a_block()
    {
        var p = new BlockProgram();
        var stack = p.AddStack(40, 40, BlockCatalog.Start, BlockCatalog.Forward, BlockCatalog.Home);
        var ws = NewWorkspace(p);
        int? selected = null;
        ws.SelectionChanged += id => selected = id;
        Click(ws, new SKPoint(ws.ScreenRectOf(stack.Blocks[1].Id)!.Value.Left + 20, ws.ScreenRectOf(stack.Blocks[1].Id)!.Value.MidY));
        Assert.Equal(stack.Blocks[1].Id, selected);
        Shot(ws, "06-selected");
        ws.DeleteSelected();
        Assert.Equal(new[] { BlockCatalog.Start, BlockCatalog.Home }, stack.Blocks.Select(b => b.Type));
        Assert.Null(selected);
    }

    [Fact]
    public void Running_blocks_glow_with_the_blocks_around_them()
    {
        var p = new BlockProgram();
        var stack = p.AddStack(40, 40, BlockCatalog.Start, BlockCatalog.Repeat);
        var fwd = p.NewBlock(BlockCatalog.Forward);
        stack.Blocks[1].Body!.Add(fwd);
        stack.Blocks[1].Body!.Add(p.NewBlock(BlockCatalog.Right));
        var ws = NewWorkspace(p);
        long now = Environment.TickCount64;
        ws.LastHit = id => id == fwd.Id ? now : 0;

        using var lit = SKSurface.Create(new SKImageInfo(W, H));
        ws.Paint(lit.Canvas);
        Shot(ws, "07-glow");
        ws.LastHit = null;
        using var plain = SKSurface.Create(new SKImageInfo(W, H));
        ws.Paint(plain.Canvas);

        // Just left of the running block, inside the repeat's mouth: glow vs. paper.
        var r = ws.ScreenRectOf(fwd.Id)!.Value;
        var probe = new SKPointI((int)r.Left - 4, (int)r.MidY);
        Assert.NotEqual(Pixel(plain, probe), Pixel(lit, probe));
    }

    [Fact]
    public void Panning_and_zooming_move_the_view_not_the_blocks()
    {
        var p = new BlockProgram();
        var stack = p.AddStack(40, 40, BlockCatalog.Start);
        var ws = NewWorkspace(p);
        var before = ws.ScreenRectOf(stack.Blocks[0].Id)!.Value;
        DragTo(ws, new SKPoint(900, 600), new SKPoint(800, 550));     // empty space: pans
        var after = ws.ScreenRectOf(stack.Blocks[0].Id)!.Value;
        Assert.Equal(before.Left - 100, after.Left, 1);
        Assert.Equal(40, stack.X);

        ws.ZoomBy(2);
        Assert.Equal(after.Width * 2, ws.ScreenRectOf(stack.Blocks[0].Id)!.Value.Width, 1);
        ws.ResetZoom();
        Assert.Equal(before.Width, ws.ScreenRectOf(stack.Blocks[0].Id)!.Value.Width, 1);
    }

    [Fact]
    public void A_challenge_palette_offers_only_its_blocks()
    {
        var challenge = ChallengeCatalog.Get("first-step")!;
        var ws = NewWorkspace(challenge.NewProgram(), challenge.Palette);
        Assert.Equal(new[] { BlockCategory.Events, BlockCategory.Turtle }, ws.VisibleCategories());
        ws.ShowCategory(BlockCategory.Turtle);
        Assert.NotNull(ws.PaletteRectOf(BlockCatalog.Forward));
        Assert.Null(ws.PaletteRectOf(BlockCatalog.Right));
    }

    [Fact]
    public void Every_block_paints_without_throwing()
    {
        var p = new BlockProgram();
        double y = 20;
        foreach (var def in BlockCatalog.All)
        {
            var s = p.AddStack(20, y, def.Type);
            if (s.Blocks[0].Body is { } body)
                body.Add(p.NewBlock(BlockCatalog.Forward));
            y += 110;
        }
        var ws = NewWorkspace(p);
        foreach (var c in BlockCatalog.Categories)
        {
            ws.ShowCategory(c);
            Shot(ws, "08-palette-" + c);
        }
    }

    private static SKPoint Center(SKRect r) => new(r.MidX, r.MidY);

    private static SKColor Pixel(SKSurface s, SKPointI p)
    {
        using var img = s.Snapshot();
        using var bmp = SKBitmap.FromImage(img);
        return bmp.GetPixel(p.X, p.Y);
    }
}
