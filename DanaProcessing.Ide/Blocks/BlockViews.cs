using System;
using System.Collections.Generic;
using SkiaSharp;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// How a block program is shown. Both views edit the very same program
    /// (and generate the very same C#), so a child can switch whenever they're
    /// ready -- the program comes along.
    /// </summary>
    public enum BlockView
    {
        /// <summary>Big picture tiles in a row, almost no words: for 6-8 year olds.</summary>
        Tiles,
        /// <summary>Sentences on ruled lines, each beside its line of C#: for 9-12 year olds.</summary>
        Lines,
    }

    /// <summary>Where one slot's value is drawn (world coordinates).</summary>
    internal sealed record SlotBox(string Name, SKRect Rect);

    /// <summary>One block as a view laid it out.</summary>
    internal sealed class BlockVisual
    {
        public required Block Block { get; init; }
        /// <summary>The part you press to pick it up (a tile, a row, a tray's handle).</summary>
        public SKRect Hit { get; init; }
        /// <summary>Everything it covers, what's inside it included.</summary>
        public SKRect Bounds { get; set; }
        public int Depth { get; init; }
        public List<SlotBox> Slots { get; } = new();
    }

    /// <summary>
    /// A place a dragged column can go: before index <see cref="Index"/> of
    /// <see cref="List"/>. <see cref="Point"/> is where the pointer has to be
    /// for it. <see cref="Prepend"/> marks the top of a column without a
    /// start block: the only kind of place a column starting with one can go.
    /// </summary>
    internal sealed record SnapTarget(List<Block> List, int Index, BlockStack Stack, SKPoint Point, bool Prepend, int Depth);

    /// <summary>A whole program laid out by a view.</summary>
    internal class SceneLayout
    {
        public Dictionary<BlockStack, List<BlockVisual>> Stacks { get; } = new();
        public List<SnapTarget> Targets { get; } = new();
    }

    /// <summary>What painting needs to know besides the layout.</summary>
    internal sealed class PaintContext
    {
        public HashSet<int> Lit { get; init; } = new();
        public int? Selected { get; init; }
        public GeneratedCode? Clean { get; init; }
    }

    /// <summary>
    /// A way of showing blocks. The workspace does everything else -- input,
    /// dragging, snapping, undo, the value picker -- the same for every view.
    /// </summary>
    internal interface IBlockRenderer : IDisposable
    {
        BlockView Kind { get; }

        /// <summary>The fonts this view draws with (the workspace's own labels follow them).</summary>
        BlockStyle Style { get; }

        /// <summary>How close (world units) the pointer has to be to a place for a dragged column to snap there.</summary>
        float SnapRadius { get; }

        SceneLayout Layout(BlockProgram program, GeneratedCode clean, float visibleWidth);

        /// <summary>A column that isn't part of the program (being dragged), laid out from <paramref name="topLeft"/>.</summary>
        List<BlockVisual> LayoutLoose(List<Block> blocks, SKPoint topLeft);

        float SnapDistance(SKPoint pointer, SnapTarget target);

        /// <summary>Where a column dropped on nothing should go, given where its top-left ended up.</summary>
        (double X, double Y) NewStackPosition(BlockProgram program, SceneLayout layout, SKPoint topLeft, SKPoint pointer);

        /// <summary>Where a hat-less column moves when <paramref name="dragged"/> goes on top of it.</summary>
        (double X, double Y) PrependOrigin(BlockStack stack, List<Block> dragged);

        void PaintScene(SKCanvas canvas, BlockProgram program, SceneLayout layout, PaintContext context);
        void PaintLoose(SKCanvas canvas, List<BlockVisual> visuals, PaintContext context, byte alpha);
        void PaintSnap(SKCanvas canvas, SnapTarget target, List<Block> dragged);

        /// <summary>Palette entries for <paramref name="templates"/>, from (left, top), within <paramref name="width"/>.</summary>
        List<(Block Template, SKRect Rect)> LayoutPalette(IReadOnlyList<Block> templates, float left, float top, float width);
        float PaletteWidth(IReadOnlyList<Block> templates);
        void PaintPaletteItem(SKCanvas canvas, Block template, SKRect rect);

        SKSize OptionSize(SlotKind kind, SlotChoice choice);
        void PaintOption(SKCanvas canvas, SlotKind kind, SlotChoice choice, SKRect rect, bool selected, SKColor accent);
    }
}
