using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// Sizes, fonts and colors of the block editor. Everything is big on
    /// purpose: it's made for 6-8 year olds with a mouse or a finger.
    /// </summary>
    internal sealed class BlockStyle : IDisposable
    {
        public const float RowH = 50;          // a statement block's height
        public const float PadX = 12;          // left/right padding inside a block
        public const float Gap = 8;            // between the parts of a label
        public const float SlotH = 34;         // a value pill
        public const float SlotPadX = 12;
        public const float ArmW = 22;          // the left arm of a C block
        public const float MouthMin = 34;      // an empty C block's mouth
        public const float FooterH = 22;       // the bottom of a C block
        public const float HatCap = 18;        // the hump on top of a hat block
        public const float NotchX = 16, NotchW = 28, NotchD = 6;
        public const float Radius = 9;
        public const float MinW = 110;
        public const float SwatchD = 24;

        public static readonly SKColor Ink = new(45, 42, 36);
        public static readonly SKColor Paper = new(248, 246, 242);
        public static readonly SKColor Glow = new(255, 228, 92);

        public SKFont Text { get; }
        public SKFont Slot { get; }
        public SKFont Small { get; }
        private readonly SKTypeface _textFace;
        private readonly Dictionary<int, SKFont> _iconFonts = new();

        public BlockStyle()
        {
            _textFace = SKTypeface.FromFamilyName("Inter", SKFontStyle.Bold) ?? SKTypeface.Default;
            Text = new SKFont(_textFace, 16.5f) { Edging = SKFontEdging.Antialias, Subpixel = true };
            Slot = new SKFont(_textFace, 16) { Edging = SKFontEdging.Antialias, Subpixel = true };
            Small = new SKFont(SKTypeface.FromFamilyName("Inter", SKFontStyle.Normal) ?? SKTypeface.Default, 12) { Edging = SKFontEdging.Antialias };
        }

        public float Measure(string text, SKFont font) => font.MeasureText(text);

        /// <summary>A font that can draw <paramref name="icon"/> (emoji need a color-emoji font).</summary>
        public SKFont IconFont(string icon, float size = 21)
        {
            int cp = char.ConvertToUtf32(icon, 0);
            int key = cp * 64 + (int)size;
            if (!_iconFonts.TryGetValue(key, out var font))
            {
                var face = _textFace.ContainsGlyph(cp) ? _textFace : SKFontManager.Default.MatchCharacter(cp) ?? _textFace;
                font = new SKFont(face, size) { Edging = SKFontEdging.Antialias };
                _iconFonts[key] = font;
            }
            return font;
        }

        public static SKColor CategoryColor(BlockCategory c)
        {
            var (r, g, b) = BlockCatalog.CategoryColor(c);
            return new SKColor(r, g, b);
        }

        public static SKColor Darker(SKColor c, float k = 0.78f) =>
            new((byte)(c.Red * k), (byte)(c.Green * k), (byte)(c.Blue * k), c.Alpha);

        public void Dispose()
        {
            Text.Dispose();
            Slot.Dispose();
            Small.Dispose();
            foreach (var f in _iconFonts.Values)
                f.Dispose();
        }
    }

    /// <summary>Where one slot's pill is, in world coordinates.</summary>
    internal sealed record SlotBox(string Name, SKRect Rect);

    /// <summary>One block laid out: its outline, its label parts and slots, and (C blocks) its mouth.</summary>
    internal sealed class BlockBox
    {
        public required Block Block { get; init; }
        /// <summary>Top-left of the block (the hat's hump goes above <see cref="Top"/>... no: a hat's Top includes its hump).</summary>
        public float X { get; init; }
        public float Top { get; init; }
        public float Width { get; init; }
        /// <summary>The row with the label (for a hat, below its hump).</summary>
        public float HeaderTop { get; init; }
        /// <summary>Total height, a C block's body and footer included.</summary>
        public float Height { get; init; }
        public float MouthHeight { get; init; }
        public List<SlotBox> Slots { get; } = new();
        public List<(string Text, float X)> Words { get; } = new();
        public float IconX { get; init; }
        /// <summary>Nesting depth (0 = directly in a stack), for drawing.</summary>
        public int Depth { get; init; }

        public SKRect Bounds => new(X, Top, X + Width, Top + Height);
        public SKRect HeaderRect => new(X, HeaderTop, X + Width, HeaderTop + BlockStyle.RowH);
        public float Bottom => Top + Height;
    }

    /// <summary>A place a dragged column can snap into: before index <see cref="Index"/> of <see cref="List"/>.</summary>
    internal sealed record SnapTarget(List<Block> List, int Index, BlockStack Stack, SKPoint Point, bool Prepend, bool InsideC);

    /// <summary>
    /// Measures and positions blocks. Pure geometry (world coordinates, no
    /// zoom or scrolling): the workspace and tests share it.
    /// </summary>
    internal static class BlockLayout
    {
        /// <summary>How wide a block's label row is, and where each part goes (relative to the block's left edge).</summary>
        public static (float Width, List<(string Text, float X)> Words, List<(string Slot, float X, float W)> Slots, float IconX) MeasureHeader(Block block, BlockStyle style)
        {
            var def = block.Def;
            var words = new List<(string, float)>();
            var slots = new List<(string, float, float)>();
            float x = BlockStyle.PadX;
            float iconX = x;
            x += style.Measure(def.Icon, style.IconFont(def.Icon)) + BlockStyle.Gap;
            foreach (var (text, slot) in def.Parts())
            {
                if (text is not null)
                {
                    var t = text.Trim();
                    if (t.Length == 0)
                        continue;
                    words.Add((t, x));
                    x += style.Measure(t, style.Text) + BlockStyle.Gap;
                }
                else if (slot is not null)
                {
                    float w = SlotWidth(block, slot, style);
                    slots.Add((slot, x, w));
                    x += w + BlockStyle.Gap;
                }
            }
            x += BlockStyle.PadX - BlockStyle.Gap;
            float min = def.Shape == BlockShape.CBlock ? BlockStyle.MinW + 40 : def.Shape == BlockShape.Hat ? BlockStyle.MinW + 30 : BlockStyle.MinW;
            return (Math.Max(min, x), words, slots, iconX);
        }

        public static float SlotWidth(Block block, string slot, BlockStyle style)
        {
            var kind = block.Def.Slot(slot).Kind;
            var choice = SlotChoices.Get(kind, block.Value(slot));
            // A color is shown, not named: young children know it by sight,
            // and it keeps blocks with three slots short enough for the palette.
            if (choice.Swatch is not null)
                return BlockStyle.SwatchD + 18;
            return Math.Max(BlockStyle.SlotH + 6, style.Measure(choice.Label, style.Slot) + BlockStyle.SlotPadX * 2);
        }

        /// <summary>Lays out a list of blocks top to bottom from (x, y); returns the height they take.</summary>
        public static float LayoutList(List<Block> list, float x, float y, int depth, BlockStyle style, List<BlockBox> into)
        {
            float top = y;
            foreach (var block in list)
            {
                var box = LayoutBlock(block, x, y, depth, style, into);
                y += box.Height;
            }
            return y - top;
        }

        public static BlockBox LayoutBlock(Block block, float x, float y, int depth, BlockStyle style, List<BlockBox> into)
        {
            var def = block.Def;
            var (width, words, slots, iconX) = MeasureHeader(block, style);
            float headerTop = def.Shape == BlockShape.Hat ? y + BlockStyle.HatCap : y;
            float mouth = 0;
            int at = into.Count;
            into.Add(null!); // keeps parents before children in paint order
            if (def.Shape == BlockShape.CBlock)
            {
                float inner = LayoutList(block.Body ?? new List<Block>(), x + BlockStyle.ArmW, headerTop + BlockStyle.RowH, depth + 1, style, into);
                mouth = Math.Max(BlockStyle.MouthMin, inner);
                // A C block is at least as wide as what's inside it, plus its arm.
                float innerWidth = into.Skip(at + 1).Where(b => b.Depth == depth + 1).Select(b => b.Width).DefaultIfEmpty(0).Max();
                width = Math.Max(width, innerWidth + BlockStyle.ArmW + 18);
            }
            float height = def.Shape switch
            {
                BlockShape.Hat => BlockStyle.HatCap + BlockStyle.RowH,
                BlockShape.CBlock => BlockStyle.RowH + mouth + BlockStyle.FooterH,
                _ => BlockStyle.RowH,
            };
            var box = new BlockBox
            {
                Block = block, X = x, Top = y, Width = width, HeaderTop = headerTop,
                Height = height, MouthHeight = mouth, IconX = x + iconX, Depth = depth,
            };
            foreach (var (t, wx) in words)
                box.Words.Add((t, x + wx));
            float slotTop = headerTop + (BlockStyle.RowH - BlockStyle.SlotH) / 2;
            foreach (var (name, sx, sw) in slots)
                box.Slots.Add(new SlotBox(name, new SKRect(x + sx, slotTop, x + sx + sw, slotTop + BlockStyle.SlotH)));
            into[at] = box;
            return box;
        }

        /// <summary>Lays out a whole stack at its position.</summary>
        public static List<BlockBox> LayoutStack(BlockStack stack, BlockStyle style)
        {
            var boxes = new List<BlockBox>();
            LayoutList(stack.Blocks, (float)stack.X, (float)stack.Y, 0, style, boxes);
            return boxes;
        }

        public static float StackHeight(List<Block> blocks, BlockStyle style)
        {
            var boxes = new List<BlockBox>();
            return LayoutList(blocks, 0, 0, 0, style, boxes);
        }

        /// <summary>
        /// Every place a column starting with <paramref name="draggedStartsWithHat"/>
        /// (and <paramref name="draggedHeight"/> tall) could snap to.
        /// </summary>
        public static List<SnapTarget> SnapTargets(BlockProgram program, Dictionary<BlockStack, List<BlockBox>> layout, bool draggedStartsWithHat, float draggedHeight)
        {
            var targets = new List<SnapTarget>();
            foreach (var stack in program.Stacks)
            {
                if (!layout.TryGetValue(stack, out var boxes) || boxes.Count == 0)
                    continue;

                // On top of a column without a hat: the dragged blocks become its new top.
                if (stack.Hat is null)
                    targets.Add(new SnapTarget(stack.Blocks, 0, stack, new SKPoint((float)stack.X, (float)stack.Y - draggedHeight), true, false));

                if (draggedStartsWithHat)
                    continue; // a hat only ever goes on top

                AddListTargets(stack.Blocks, boxes, stack, inside: false, targets);
            }
            return targets;
        }

        private static void AddListTargets(List<Block> list, List<BlockBox> boxes, BlockStack stack, bool inside, List<SnapTarget> targets)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var box = boxes.First(b => ReferenceEquals(b.Block, list[i]));
                // Below this block (= before the next one).
                targets.Add(new SnapTarget(list, i + 1, stack, new SKPoint(box.X, box.Bottom), false, inside));
                if (list[i].Body is { } body)
                {
                    // Inside the mouth, at the top...
                    targets.Add(new SnapTarget(body, 0, stack, new SKPoint(box.X + BlockStyle.ArmW, box.HeaderTop + BlockStyle.RowH), false, true));
                    // ...and after each block already in it.
                    AddListTargets(body, boxes, stack, inside: true, targets);
                }
            }
        }

        // =================================================================
        // Outlines
        // =================================================================

        /// <summary>The outline of a block, Scratch style: a notch on top, a bump below, a mouth for C blocks, a hump for hats.</summary>
        public static SKPath Outline(BlockBox box)
        {
            float x = box.X, w = box.Width, r = BlockStyle.Radius;
            float nx = BlockStyle.NotchX, nw = BlockStyle.NotchW, nd = BlockStyle.NotchD;
            var p = new SKPath();
            var shape = box.Block.Def.Shape;

            float top = box.HeaderTop;
            if (shape == BlockShape.Hat)
            {
                // A hump over the left part, then a flat top.
                p.MoveTo(x, top);
                p.CubicTo(x + 20, top - BlockStyle.HatCap - 4, x + 90, top - BlockStyle.HatCap - 4, x + 110, top);
                p.LineTo(x + w - r, top);
            }
            else
            {
                p.MoveTo(x + r, top);
                TopNotch(p, x, top);
                p.LineTo(x + w - r, top);
            }
            p.QuadTo(x + w, top, x + w, top + r);

            float bottom;
            if (shape == BlockShape.CBlock)
            {
                float mouthTop = top + BlockStyle.RowH;
                float mouthBottom = mouthTop + box.MouthHeight;
                float ax = x + BlockStyle.ArmW;
                p.LineTo(x + w, mouthTop - r);
                p.QuadTo(x + w, mouthTop, x + w - r, mouthTop);
                // the header's underside, with a bump for the first block inside
                p.LineTo(ax + nx + nw, mouthTop);
                p.LineTo(ax + nx + nw - 4, mouthTop + nd);
                p.LineTo(ax + nx + 4, mouthTop + nd);
                p.LineTo(ax + nx, mouthTop);
                p.LineTo(ax + r, mouthTop);
                p.QuadTo(ax, mouthTop, ax, mouthTop + r);
                p.LineTo(ax, mouthBottom - r);
                p.QuadTo(ax, mouthBottom, ax + r, mouthBottom);
                // the footer's top, with a notch for the last block inside
                p.LineTo(ax + nx, mouthBottom);
                p.LineTo(ax + nx + 4, mouthBottom + nd);
                p.LineTo(ax + nx + nw - 4, mouthBottom + nd);
                p.LineTo(ax + nx + nw, mouthBottom);
                p.LineTo(x + w - r, mouthBottom);
                p.QuadTo(x + w, mouthBottom, x + w, mouthBottom + r);
                bottom = mouthBottom + BlockStyle.FooterH;
            }
            else
            {
                bottom = top + BlockStyle.RowH;
            }

            p.LineTo(x + w, bottom - r);
            p.QuadTo(x + w, bottom, x + w - r, bottom);
            // the bump that fits into the next block's notch
            p.LineTo(x + nx + nw, bottom);
            p.LineTo(x + nx + nw - 4, bottom + nd);
            p.LineTo(x + nx + 4, bottom + nd);
            p.LineTo(x + nx, bottom);
            p.LineTo(x + r, bottom);
            p.QuadTo(x, bottom, x, bottom - r);
            if (shape == BlockShape.Hat)
            {
                p.LineTo(x, top);
            }
            else
            {
                p.LineTo(x, top + r);
                p.QuadTo(x, top, x + r, top);
            }
            p.Close();
            return p;
        }

        private static void TopNotch(SKPath p, float x, float top)
        {
            float nx = BlockStyle.NotchX, nw = BlockStyle.NotchW, nd = BlockStyle.NotchD;
            p.LineTo(x + nx, top);
            p.LineTo(x + nx + 4, top + nd);
            p.LineTo(x + nx + nw - 4, top + nd);
            p.LineTo(x + nx + nw, top);
        }
    }
}
