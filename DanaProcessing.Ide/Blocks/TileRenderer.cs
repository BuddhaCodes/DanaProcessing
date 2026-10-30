using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// The "Fichas" view, for 6-8 year olds: each block is a big tile with an
    /// icon and its value drawn as a picture, tiles follow each other left to
    /// right, and "repetir"/"si" are trays that hold tiles. No reading needed.
    /// </summary>
    internal sealed class TileRenderer : IBlockRenderer
    {
        public const float TileH = 112;       // tile height, its solid edge included
        public const float TileMinW = 100;
        public const float Gap = 30;          // between tiles (the arrow lives here)
        public const float TrayPad = 14;
        public const float HandleW = 74;      // a tray's colored handle
        public const float EmptyW = 96;       // an empty tray's "+" spot
        private const float Edge = 6;
        private const float Radius = 22;
        private const float SlotH = 38;

        private readonly BlockStyle _style = new();

        public BlockView Kind => BlockView.Tiles;
        public float SnapRadius => 64;

        private static float SlotWidth(SlotKind kind) => kind switch
        {
            SlotKind.Steps => 76,
            SlotKind.Angle => 84,
            SlotKind.Count => 76,
            SlotKind.Color => 44,
            SlotKind.Thickness => 64,
            SlotKind.Size => 44,
            SlotKind.Place => 50,
            SlotKind.Note => 52,
            SlotKind.Drum => 44,
            SlotKind.Beats => 72,
            SlotKind.Instrument => 86,
            SlotKind.Condition => 64,
            _ => 60,
        };

        // =================================================================
        // Sizes
        // =================================================================

        private SKSize Measure(Block b)
        {
            var def = b.Def;
            switch (def.Shape)
            {
                case BlockShape.Hat:
                    // Wide enough for its little label ("al tocar una tecla"), for the grown-ups.
                    return new SKSize(Math.Max(TileMinW, _style.Small.MeasureText(def.Label) + 30), TileH);
                case BlockShape.CBlock:
                {
                    var inner = MeasureList(b.Body ?? new List<Block>());
                    float w = HandleW + TrayPad + Math.Max(inner.Width, EmptyW) + TrayPad;
                    float h = Math.Max(TileH, inner.Height) + TrayPad * 2;
                    return new SKSize(w, h);
                }
                default:
                {
                    float slots = def.Slots.Sum(s => SlotWidth(s.Kind)) + Math.Max(0, def.Slots.Count - 1) * 6;
                    return new SKSize(Math.Max(TileMinW, slots + 18), TileH);
                }
            }
        }

        private SKSize MeasureList(List<Block> list)
        {
            if (list.Count == 0)
                return new SKSize(0, 0);
            var sizes = list.Select(Measure).ToList();
            return new SKSize(sizes.Sum(s => s.Width) + Gap * (list.Count - 1), sizes.Max(s => s.Height));
        }

        // =================================================================
        // Layout
        // =================================================================

        public SceneLayout Layout(BlockProgram program, GeneratedCode clean, float visibleWidth)
        {
            var scene = new SceneLayout();
            foreach (var stack in program.Stacks)
            {
                var visuals = new List<BlockVisual>();
                float midY = (float)stack.Y + MeasureList(stack.Blocks).Height / 2;
                bool hat = stack.Hat is not null;
                if (!hat && stack.Blocks.Count > 0)
                    scene.Targets.Add(new SnapTarget(stack.Blocks, 0, stack, new SKPoint((float)stack.X - Gap / 2, midY), true, 0));
                // Index 0: never in front of a start block; for a column without one, the Prepend place above.
                Place(stack.Blocks, (float)stack.X, midY, 0, stack, visuals, scene.Targets, firstTarget: 1);
                scene.Stacks[stack] = visuals;
            }
            return scene;
        }

        public List<BlockVisual> LayoutLoose(List<Block> blocks, SKPoint topLeft)
        {
            var visuals = new List<BlockVisual>();
            Place(blocks, topLeft.X, topLeft.Y + MeasureList(blocks).Height / 2, 0, null, visuals, null, 0);
            return visuals;
        }

        /// <summary>Lays a list out left to right, centered on <paramref name="midY"/>; adds a snap place before each block from <paramref name="firstTarget"/> on, and after the last.</summary>
        private void Place(List<Block> list, float x, float midY, int depth, BlockStack? stack, List<BlockVisual> into, List<SnapTarget>? targets, int firstTarget)
        {
            float cx = x;
            for (int i = 0; i < list.Count; i++)
            {
                var size = Measure(list[i]);
                if (targets is not null && stack is not null && i >= firstTarget)
                    targets.Add(new SnapTarget(list, i, stack, new SKPoint(cx - Gap / 2, midY), false, depth));
                PlaceBlock(list[i], cx, midY - size.Height / 2, size, depth, stack, into, targets);
                cx += size.Width + Gap;
            }
            if (targets is not null && stack is not null)
            {
                var after = list.Count == 0 ? new SKPoint(x + EmptyW / 2, midY) : new SKPoint(cx - Gap / 2, midY);
                targets.Add(new SnapTarget(list, list.Count, stack, after, false, depth));
            }
        }

        private void PlaceBlock(Block b, float x, float top, SKSize size, int depth, BlockStack? stack, List<BlockVisual> into, List<SnapTarget>? targets)
        {
            var def = b.Def;
            var bounds = new SKRect(x, top, x + size.Width, top + size.Height);
            if (def.Shape == BlockShape.CBlock)
            {
                var handle = new SKRect(x, top, x + HandleW, top + size.Height);
                var visual = new BlockVisual { Block = b, Hit = handle, Bounds = bounds, Depth = depth };
                float slotTop = handle.MidY + 6;
                foreach (var slot in def.Slots)
                    visual.Slots.Add(new SlotBox(slot.Name, new SKRect(x + 6, slotTop, x + HandleW - 6, slotTop + SlotH)));
                into.Add(visual);
                Place(b.Body ?? new List<Block>(), x + HandleW + TrayPad, top + size.Height / 2, depth + 1, stack, into, targets, 0);
                return;
            }

            var tile = new BlockVisual { Block = b, Hit = bounds, Bounds = bounds, Depth = depth };
            float total = def.Slots.Sum(s => SlotWidth(s.Kind)) + Math.Max(0, def.Slots.Count - 1) * 6;
            float sx = x + (size.Width - total) / 2;
            float sy = top + 62;
            foreach (var slot in def.Slots)
            {
                float w = SlotWidth(slot.Kind);
                tile.Slots.Add(new SlotBox(slot.Name, new SKRect(sx, sy, sx + w, sy + SlotH)));
                sx += w + 6;
            }
            into.Add(tile);
        }

        public float SnapDistance(SKPoint pointer, SnapTarget target)
        {
            // Horizontal distance matters most: the caret sits between two tiles.
            float dx = pointer.X - target.Point.X, dy = (pointer.Y - target.Point.Y) * 0.6f;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        public (double X, double Y) NewStackPosition(BlockProgram program, SceneLayout layout, SKPoint topLeft, SKPoint pointer) => (topLeft.X, topLeft.Y);

        public (double X, double Y) PrependOrigin(BlockStack stack, List<Block> dragged)
        {
            var own = MeasureList(stack.Blocks);
            var add = MeasureList(dragged);
            // Keep the column's middle where it was; it grows to the left.
            return (stack.X - add.Width - Gap, stack.Y + own.Height / 2 - Math.Max(own.Height, add.Height) / 2);
        }

        // =================================================================
        // Painting
        // =================================================================

        public void PaintScene(SKCanvas canvas, BlockProgram program, SceneLayout layout, PaintContext context)
        {
            foreach (var stack in program.Stacks)
            {
                if (!layout.Stacks.TryGetValue(stack, out var visuals))
                    continue;
                byte alpha = stack.Hat is null ? (byte)140 : (byte)255;
                PaintVisuals(canvas, stack.Blocks, visuals, context, alpha);
            }
        }

        public void PaintLoose(SKCanvas canvas, List<BlockVisual> visuals, PaintContext context, byte alpha)
        {
            var top = visuals.Where(v => v.Depth == 0).Select(v => v.Block).ToList();
            PaintVisuals(canvas, top, visuals, context, alpha);
        }

        private void PaintVisuals(SKCanvas canvas, List<Block> topList, List<BlockVisual> visuals, PaintContext context, byte alpha)
        {
            var byBlock = visuals.ToDictionary(v => v.Block);
            foreach (var v in visuals)
                PaintBlock(canvas, v, context, alpha);
            PaintArrows(canvas, topList, byBlock, alpha);
        }

        /// <summary>A little arrow between consecutive tiles, and a "+" spot in empty trays.</summary>
        private void PaintArrows(SKCanvas canvas, List<Block> list, Dictionary<Block, BlockVisual> byBlock, byte alpha)
        {
            using var paint = new SKPaint
            {
                Color = new SKColor(185, 172, 149, alpha), IsAntialias = true, Style = SKPaintStyle.Stroke,
                StrokeWidth = 3.5f, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
            };
            for (int i = 0; i < list.Count; i++)
            {
                if (!byBlock.TryGetValue(list[i], out var v))
                    continue;
                if (i + 1 < list.Count && byBlock.TryGetValue(list[i + 1], out var next))
                {
                    float x = (v.Bounds.Right + next.Bounds.Left) / 2, y = (v.Bounds.MidY + next.Bounds.MidY) / 2 - Edge / 2;
                    canvas.DrawLine(x - 8, y, x + 6, y, paint);
                    canvas.DrawLine(x + 1, y - 6, x + 7, y, paint);
                    canvas.DrawLine(x + 1, y + 6, x + 7, y, paint);
                }
                if (list[i].Body is { } body)
                {
                    if (body.Count == 0)
                    {
                        var spot = new SKRect(v.Bounds.Left + HandleW + TrayPad, v.Bounds.MidY - 40, v.Bounds.Left + HandleW + TrayPad + EmptyW - 8, v.Bounds.MidY + 40);
                        using var dash = new SKPaint
                        {
                            Color = BlockStyle.CategoryColor(list[i].Def.Category).WithAlpha((byte)(alpha * 0.6f)), IsAntialias = true,
                            Style = SKPaintStyle.Stroke, StrokeWidth = 3, PathEffect = SKPathEffect.CreateDash(new[] { 8f, 6f }, 0),
                        };
                        canvas.DrawRoundRect(spot, 16, 16, dash);
                        using var plus = new SKPaint { Color = dash.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3.5f, StrokeCap = SKStrokeCap.Round };
                        canvas.DrawLine(spot.MidX - 10, spot.MidY, spot.MidX + 10, spot.MidY, plus);
                        canvas.DrawLine(spot.MidX, spot.MidY - 10, spot.MidX, spot.MidY + 10, plus);
                    }
                    else
                    {
                        PaintArrows(canvas, body, byBlock, alpha);
                    }
                }
            }
        }

        private void PaintBlock(SKCanvas canvas, BlockVisual v, PaintContext context, byte alpha)
        {
            var def = v.Block.Def;
            var color = BlockStyle.CategoryColor(def.Category).WithAlpha(alpha);
            var edge = BlockStyle.Darker(color).WithAlpha(alpha);
            var white = SKColors.White.WithAlpha(alpha);
            bool lit = context.Lit.Contains(v.Block.Id);
            bool selected = context.Selected == v.Block.Id;

            if (def.Shape == BlockShape.CBlock)
            {
                var tray = v.Bounds;
                using (var bg = new SKPaint { Color = BlockStyle.Tint(color, 0.8f).WithAlpha(alpha), IsAntialias = true })
                    canvas.DrawRoundRect(tray, 30, 30, bg);
                using (var rim = new SKPaint { Color = color.WithAlpha((byte)(alpha * 0.45f)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 })
                    canvas.DrawRoundRect(tray, 30, 30, rim);
                var handle = v.Hit;
                using (var rr = new SKRoundRect())
                {
                    rr.SetRectRadii(handle, new[] { new SKPoint(30, 30), new SKPoint(12, 12), new SKPoint(12, 12), new SKPoint(30, 30) });
                    using var paint = new SKPaint { Color = color, IsAntialias = true };
                    canvas.DrawRoundRect(rr, paint);
                    Highlight(canvas, rr, lit, selected);
                }
                BlockIcons.Draw(canvas, def.Type, new SKRect(handle.MidX - 20, handle.MidY - 48, handle.MidX + 20, handle.MidY - 8), white, 2.6f);
                PaintSlots(canvas, v, white, alpha);
                return;
            }

            var face = new SKRect(v.Bounds.Left, v.Bounds.Top, v.Bounds.Right, v.Bounds.Bottom - Edge);
            using var shape = new SKRoundRect();
            if (def.Shape == BlockShape.Hat)
                shape.SetRectRadii(face, new[] { new SKPoint(48, 48), new SKPoint(Radius, Radius), new SKPoint(Radius, Radius), new SKPoint(48, 48) });
            else
                shape.SetRect(face, Radius, Radius);

            using (var under = new SKRoundRect(shape))
            using (var edgePaint = new SKPaint { Color = edge, IsAntialias = true })
            {
                under.Offset(0, Edge);
                canvas.DrawRoundRect(under, edgePaint);
            }
            using (var paint = new SKPaint { Color = color, IsAntialias = true })
                canvas.DrawRoundRect(shape, paint);
            Highlight(canvas, shape, lit, selected);

            if (def.Shape == BlockShape.Hat)
            {
                BlockIcons.Draw(canvas, def.Type, new SKRect(face.MidX - 20, face.Top + 16, face.MidX + 20, face.Top + 56), white, 2.6f);
                using var ink = new SKPaint { Color = white, IsAntialias = true };
                canvas.DrawText(def.Label, face.MidX, face.Top + 82, SKTextAlign.Center, _style.Small, ink);
                return;
            }

            bool hasSlots = def.Slots.Count > 0;
            var iconBox = hasSlots
                ? new SKRect(face.MidX - 21, face.Top + 12, face.MidX + 21, face.Top + 54)
                : new SKRect(face.MidX - 26, face.MidY - 26, face.MidX + 26, face.MidY + 26);
            BlockIcons.Draw(canvas, def.Type, iconBox, white, 2.6f);
            PaintSlots(canvas, v, white, alpha);
        }

        private void PaintSlots(SKCanvas canvas, BlockVisual v, SKColor ink, byte alpha)
        {
            foreach (var slot in v.Slots)
            {
                using (var well = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(alpha * 0.14f)), IsAntialias = true })
                    canvas.DrawRoundRect(slot.Rect, 12, 12, well);
                var kind = v.Block.Def.Slot(slot.Name).Kind;
                ValueGlyph.Paint(canvas, _style, kind, v.Block.Value(slot.Name), SKRect.Inflate(slot.Rect, -4, -2), ink);
            }
        }

        private static void Highlight(SKCanvas canvas, SKRoundRect shape, bool lit, bool selected)
        {
            if (lit)
            {
                using var glow = new SKRoundRect(shape);
                glow.Inflate(6, 6);
                using var paint = new SKPaint { Color = BlockStyle.Glow, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 6 };
                canvas.DrawRoundRect(glow, paint);
            }
            if (selected)
            {
                using var paint = new SKPaint { Color = BlockStyle.Ink, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 };
                canvas.DrawRoundRect(shape, paint);
            }
        }

        public void PaintSnap(SKCanvas canvas, SnapTarget target, List<Block> dragged)
        {
            float h = Math.Max(TileH, MeasureList(dragged).Height) * 0.85f;
            var bar = new SKRect(target.Point.X - 5, target.Point.Y - h / 2, target.Point.X + 5, target.Point.Y + h / 2);
            using var paint = new SKPaint { Color = new SKColor(232, 180, 0), IsAntialias = true };
            canvas.DrawRoundRect(bar, 5, 5, paint);
            canvas.DrawCircle(target.Point.X, bar.Top - 10, 10, paint);
            using var plus = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, StrokeCap = SKStrokeCap.Round };
            canvas.DrawLine(target.Point.X - 5, bar.Top - 10, target.Point.X + 5, bar.Top - 10, plus);
            canvas.DrawLine(target.Point.X, bar.Top - 15, target.Point.X, bar.Top - 5, plus);
        }

        // =================================================================
        // Palette and picker
        // =================================================================

        public float PaletteWidth(IReadOnlyList<Block> templates) =>
            Math.Max(TileMinW * 2 + 16, templates.Select(t => Measure(t).Width).DefaultIfEmpty(0).Max()) + 24;

        public List<(Block Template, SKRect Rect)> LayoutPalette(IReadOnlyList<Block> templates, float left, float top, float width)
        {
            var result = new List<(Block, SKRect)>();
            float x = left + 12, y = top, rowH = 0;
            foreach (var t in templates)
            {
                var size = Measure(t);
                if (x > left + 12 && x + size.Width > left + width - 12)
                {
                    x = left + 12;
                    y += rowH + 16;
                    rowH = 0;
                }
                result.Add((t, new SKRect(x, y, x + size.Width, y + size.Height)));
                x += size.Width + 16;
                rowH = Math.Max(rowH, size.Height);
            }
            return result;
        }

        public void PaintPaletteItem(SKCanvas canvas, Block template, SKRect rect)
        {
            var visuals = LayoutLoose(new List<Block> { template }, new SKPoint(rect.Left, rect.Top));
            PaintLoose(canvas, visuals, new PaintContext(), 255);
        }

        public SKSize OptionSize(SlotKind kind, SlotChoice choice) => kind switch
        {
            SlotKind.Color => new SKSize(60, 60),
            SlotKind.Instrument => new SKSize(110, 56),
            _ => new SKSize(Math.Max(84, SlotWidth(kind) + 16), 64),
        };

        public void PaintOption(SKCanvas canvas, SlotKind kind, SlotChoice choice, SKRect rect, bool selected, SKColor accent)
        {
            if (kind == SlotKind.Color)
            {
                ValueGlyph.Paint(canvas, _style, kind, choice.Value, SKRect.Inflate(rect, -2, -2), BlockStyle.Ink);
                if (selected)
                {
                    using var ring = new SKPaint { Color = BlockStyle.Ink, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 };
                    canvas.DrawCircle(rect.MidX, rect.MidY, rect.Width / 2 - 1, ring);
                }
                return;
            }
            using (var bg = new SKPaint { Color = selected ? accent : new SKColor(244, 239, 230), IsAntialias = true })
                canvas.DrawRoundRect(rect, 16, 16, bg);
            var ink = selected ? SKColors.White : BlockStyle.Darker(accent, 0.62f);
            if (kind == SlotKind.Instrument)
            {
                using var paint = new SKPaint { Color = selected ? SKColors.White : BlockStyle.Ink, IsAntialias = true };
                canvas.DrawText(choice.Label, rect.MidX, BlockStyle.Baseline(_style.Text, rect.MidY), SKTextAlign.Center, _style.Text, paint);
                return;
            }
            ValueGlyph.Paint(canvas, _style, kind, choice.Value, SKRect.Inflate(rect, -6, -10), ink);
        }

        public void Dispose() => _style.Dispose();
    }
}
