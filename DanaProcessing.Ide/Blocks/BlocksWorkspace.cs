using System;
using System.Collections.Generic;
using System.Linq;
using DanaProcessing.Ide.Localization;
using SkiaSharp;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// The block editor itself: the palette on the left, the workspace on the
    /// right, dragging and snapping, the value pickers, undo -- and drawing all
    /// of it with Skia. It knows nothing about Avalonia: a host feeds it
    /// pointer events in pixels and asks it to paint, which is also how tests
    /// drive it and take screenshots of it.
    /// </summary>
    internal sealed class BlocksWorkspace : IDisposable
    {
        private const float CategoryW = 82;

        /// <summary>
        /// Width of the palette: wide enough for the widest block it offers
        /// (worked out once per program, so it doesn't jump between categories).
        /// </summary>
        public float PaletteW { get; private set; } = 330;
        private const float DragThreshold = 5;
        private const float SnapDistance = 34;
        private const int GlowMs = 400;

        private readonly BlockStyle _style = new();

        public BlocksWorkspace(BlockProgram program, IReadOnlyCollection<string>? palette = null) => Load(program, palette);

        public BlockProgram Program { get; private set; } = new();

        /// <summary>The blocks offered on the palette (a challenge offers only a few); null = all of them.</summary>
        public IReadOnlyCollection<string>? Palette { get; private set; }

        /// <summary>After anything that changes the program (blocks moved, added, removed, a value picked).</summary>
        public event Action? ProgramChanged;

        /// <summary>The selected block changed (null = none): the host highlights its code.</summary>
        public event Action<int?>? SelectionChanged;

        /// <summary>The workspace wants to be painted again.</summary>
        public event Action? Invalidated;

        public int? SelectedBlockId { get; private set; }

        /// <summary>When a block last ran (Environment.TickCount64 ms; 0 = never), from the running sketch -- or null when nothing runs.</summary>
        public Func<int, long>? LastHit { get; set; }

        public float ViewWidth { get; set; } = 1000;
        public float ViewHeight { get; set; } = 700;

        private float _scrollX = -30, _scrollY = -30, _zoom = 1;
        private BlockCategory _category = BlockCategory.Events;
        private float _paletteScroll;

        private Dictionary<BlockStack, List<BlockBox>> _layout = new();
        private List<(Block Template, BlockBox Box)> _paletteBoxes = new();
        private readonly List<(BlockCategory Category, SKRect Rect)> _categoryRects = new();
        private readonly Stack<string> _undo = new(), _redo = new();

        // A press that hasn't turned into a click or a drag yet.
        private sealed record Press(SKPoint Start, Block? Block, string? Slot, Block? Template, bool Pan, SKPoint ScrollAtStart);
        private Press? _press;

        private sealed class Drag
        {
            public required List<Block> Blocks { get; init; }
            public SKPoint Grab;          // pointer minus the column's top-left, in world units
            public SKPoint Pointer;       // screen
            public SnapTarget? Snap;
            public List<SnapTarget> Targets = new();
            public float Height;
        }
        private Drag? _drag;

        private sealed class Picker
        {
            public required Block Block { get; init; }
            public required string Slot { get; init; }
            public SKRect Panel;
            public List<(SlotChoice Choice, SKRect Rect)> Options = new();
        }
        private Picker? _picker;

        private readonly List<(string Action, SKRect Rect)> _buttons = new();

        // =================================================================
        // Program
        // =================================================================

        public void Load(BlockProgram program, IReadOnlyCollection<string>? palette)
        {
            Program = program;
            Palette = palette;
            _undo.Clear();
            _redo.Clear();
            _drag = null;
            _press = null;
            _picker = null;
            SelectedBlockId = null;
            PaletteW = Math.Clamp(WidestPaletteBlock() + CategoryW + 40, 300, 560);
            var categories = VisibleCategories();
            _category = categories.Contains(_category) ? _category : categories.FirstOrDefault();
            if (Program.Stacks.Count > 0)
            {
                _scrollX = (float)Program.Stacks.Min(s => s.X) - 30;
                _scrollY = (float)Program.Stacks.Min(s => s.Y) - 30;
            }
            Relayout();
        }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        private void Checkpoint()
        {
            _undo.Push(Program.ToJson());
            while (_undo.Count > 60)
            {
                var keep = _undo.Reverse().Skip(1).ToList();
                _undo.Clear();
                foreach (var s in keep) _undo.Push(s);
            }
            _redo.Clear();
        }

        public void Undo() => Swap(_undo, _redo);
        public void Redo() => Swap(_redo, _undo);

        private void Swap(Stack<string> from, Stack<string> to)
        {
            if (from.Count == 0)
                return;
            to.Push(Program.ToJson());
            var restored = BlockProgram.FromJson(from.Pop());
            restored.ChallengeId = Program.ChallengeId;
            Program = restored;
            _picker = null;
            Changed();
        }

        private void Changed()
        {
            Relayout();
            ProgramChanged?.Invoke();
        }

        private void Relayout()
        {
            _layout = Program.Stacks.ToDictionary(s => s, s => BlockLayout.LayoutStack(s, _style));
            LayoutPalette();
            Invalidated?.Invoke();
        }

        /// <summary>Deletes the selected block (and what's inside it, for a C block).</summary>
        public void DeleteSelected()
        {
            if (SelectedBlockId is not { } id || Program.Locate(id) is not { } where)
                return;
            Checkpoint();
            where.List.RemoveAt(where.Index);
            if (where.Stack.Blocks.Count == 0)
                Program.Stacks.Remove(where.Stack);
            Select(null);
            Changed();
        }

        public void Select(int? id)
        {
            if (SelectedBlockId == id)
                return;
            SelectedBlockId = id;
            SelectionChanged?.Invoke(id);
            Invalidated?.Invoke();
        }

        // =================================================================
        // Coordinates
        // =================================================================

        private SKPoint ToWorld(SKPoint screen) => new((screen.X - PaletteW) / _zoom + _scrollX, screen.Y / _zoom + _scrollY);
        private SKPoint ToScreen(SKPoint world) => new(PaletteW + (world.X - _scrollX) * _zoom, (world.Y - _scrollY) * _zoom);

        public void ZoomBy(float factor, SKPoint? around = null)
        {
            var center = around ?? new SKPoint(PaletteW + (ViewWidth - PaletteW) / 2, ViewHeight / 2);
            var before = ToWorld(center);
            _zoom = Math.Clamp(_zoom * factor, 0.5f, 2f);
            var after = ToWorld(center);
            _scrollX += before.X - after.X;
            _scrollY += before.Y - after.Y;
            Invalidated?.Invoke();
        }

        public void ResetZoom()
        {
            _zoom = 1;
            if (Program.Stacks.Count > 0)
            {
                _scrollX = (float)Program.Stacks.Min(s => s.X) - 30;
                _scrollY = (float)Program.Stacks.Min(s => s.Y) - 30;
            }
            Invalidated?.Invoke();
        }

        // =================================================================
        // Palette
        // =================================================================

        public IReadOnlyList<BlockCategory> VisibleCategories() =>
            BlockCatalog.Categories.Where(c => BlockCatalog.InCategory(c).Any(d => Palette is null || Palette.Contains(d.Type))).ToList();

        public void ShowCategory(BlockCategory category)
        {
            _category = category;
            _paletteScroll = 0;
            LayoutPalette();
            Invalidated?.Invoke();
        }

        private float WidestPaletteBlock()
        {
            var scratch = new BlockProgram();
            return BlockCatalog.All.Where(d => Palette is null || Palette.Contains(d.Type))
                .Select(d => BlockLayout.MeasureHeader(scratch.NewBlock(d.Type), _style).Width)
                .DefaultIfEmpty(200).Max();
        }

        private void LayoutPalette()
        {
            var scratch = new BlockProgram { NextId = -100000 };
            _paletteBoxes = new List<(Block, BlockBox)>();
            float y = 24 + _paletteScroll;
            foreach (var def in BlockCatalog.InCategory(_category).Where(d => Palette is null || Palette.Contains(d.Type)))
            {
                var template = scratch.NewBlock(def.Type);
                var boxes = new List<BlockBox>();
                var box = BlockLayout.LayoutBlock(template, CategoryW + 18, y, 0, _style, boxes);
                _paletteBoxes.Add((template, box));
                y += box.Height + 20;
            }
        }

        // =================================================================
        // Pointer
        // =================================================================

        public void PointerDown(SKPoint p)
        {
            if (_picker is { } picker)
            {
                foreach (var (choice, rect) in picker.Options)
                {
                    if (rect.Contains(p))
                    {
                        if (picker.Block.Value(picker.Slot) != choice.Value)
                        {
                            Checkpoint();
                            picker.Block.Values[picker.Slot] = choice.Value;
                            _picker = null;
                            Changed();
                        }
                        else
                        {
                            _picker = null;
                            Invalidated?.Invoke();
                        }
                        return;
                    }
                }
                _picker = null;
                Invalidated?.Invoke();
                if (picker.Panel.Contains(p))
                    return;
            }

            foreach (var (action, rect) in _buttons)
            {
                if (!rect.Contains(p))
                    continue;
                switch (action)
                {
                    case "zoom+": ZoomBy(1.2f); break;
                    case "zoom-": ZoomBy(1 / 1.2f); break;
                    case "zoom=": ResetZoom(); break;
                }
                return;
            }

            if (p.X < PaletteW)
            {
                foreach (var (category, rect) in _categoryRects)
                {
                    if (rect.Contains(p))
                    {
                        ShowCategory(category);
                        return;
                    }
                }
                foreach (var (template, box) in _paletteBoxes)
                {
                    using var outline = BlockLayout.Outline(box);
                    if (outline.Contains(p.X, p.Y))
                    {
                        _press = new Press(p, null, null, template, false, default);
                        return;
                    }
                }
                return;
            }

            var world = ToWorld(p);
            var (block, slot) = HitBlock(world);
            _press = block is null
                ? new Press(p, null, null, null, true, new SKPoint(_scrollX, _scrollY))
                : new Press(p, block, slot, null, false, default);
        }

        public void PointerMove(SKPoint p)
        {
            if (_drag is { } drag)
            {
                drag.Pointer = p;
                drag.Snap = FindSnap(drag);
                Invalidated?.Invoke();
                return;
            }

            if (_press is not { } press)
                return;

            float moved = SKPoint.Distance(p, press.Start);
            if (press.Pan)
            {
                _scrollX = press.ScrollAtStart.X - (p.X - press.Start.X) / _zoom;
                _scrollY = press.ScrollAtStart.Y - (p.Y - press.Start.Y) / _zoom;
                Invalidated?.Invoke();
                return;
            }

            if (moved < DragThreshold)
                return;

            if (press.Template is { } template)
                StartDragFromPalette(template, press.Start);
            else if (press.Block is { } block)
                StartDragFromWorkspace(block, press.Start);
            _press = null;
            if (_drag is { } started)
            {
                started.Pointer = p;
                started.Snap = FindSnap(started);
                Invalidated?.Invoke();
            }
        }

        public void PointerUp(SKPoint p)
        {
            if (_drag is { } drag)
            {
                drag.Pointer = p;
                drag.Snap = FindSnap(drag);
                Drop(drag);
                _drag = null;
                return;
            }

            if (_press is { } press && !press.Pan)
            {
                if (press.Block is { } block)
                {
                    if (press.Slot is { } slot)
                        OpenPicker(block, slot);
                    else
                        Select(SelectedBlockId == block.Id ? null : block.Id);
                }
            }
            _press = null;
        }

        public void Wheel(SKPoint p, float dy, bool zoom)
        {
            if (zoom)
            {
                ZoomBy(dy > 0 ? 1.1f : 1 / 1.1f, p);
                return;
            }
            if (p.X < PaletteW)
            {
                float content = _paletteBoxes.Count == 0 ? 0 : _paletteBoxes[^1].Box.Bottom - _paletteScroll + 40;
                _paletteScroll = Math.Clamp(_paletteScroll + dy * 40, Math.Min(0, ViewHeight - content), 0);
                LayoutPalette();
            }
            else
            {
                _scrollY -= dy * 40 / _zoom;
            }
            Invalidated?.Invoke();
        }

        /// <summary>The innermost block under <paramref name="world"/>, and the slot under it if any.</summary>
        private (Block? Block, string? Slot) HitBlock(SKPoint world)
        {
            for (int s = Program.Stacks.Count - 1; s >= 0; s--)
            {
                if (!_layout.TryGetValue(Program.Stacks[s], out var boxes))
                    continue;
                for (int i = boxes.Count - 1; i >= 0; i--)
                {
                    var box = boxes[i];
                    using var outline = BlockLayout.Outline(box);
                    if (!outline.Contains(world.X, world.Y))
                        continue;
                    foreach (var slot in box.Slots)
                    {
                        var r = slot.Rect;
                        r.Inflate(4, 4); // a little forgiving for small hands
                        if (r.Contains(world))
                            return (box.Block, slot.Name);
                    }
                    return (box.Block, null);
                }
            }
            return (null, null);
        }

        private void StartDragFromPalette(Block template, SKPoint start)
        {
            var block = Program.NewBlock(template.Type);
            var box = _paletteBoxes.First(b => ReferenceEquals(b.Template, template)).Box;
            // Grab it at the same spot of the block (the palette is drawn at
            // zoom 1, the workspace at its own zoom: keep the spot proportional).
            var grab = new SKPoint((start.X - box.X) / _zoom, (start.Y - box.Top) / _zoom);
            BeginDrag(new List<Block> { block }, grab, start, checkpoint: true);
        }

        private void StartDragFromWorkspace(Block block, SKPoint start)
        {
            if (Program.Locate(block.Id) is not { } where)
                return;
            var box = _layout[where.Stack].First(b => ReferenceEquals(b.Block, block));
            var world = ToWorld(start);
            var grab = new SKPoint(world.X - box.X, world.Y - box.Top);

            Checkpoint();
            List<Block> taken;
            if (where.Owner is null && where.Index == 0)
            {
                // The top of a column (or its hat): the whole column comes along.
                taken = new List<Block>(where.Stack.Blocks);
                Program.Stacks.Remove(where.Stack);
            }
            else
            {
                // This block and everything below it in the same list, like Scratch.
                taken = where.List.GetRange(where.Index, where.List.Count - where.Index);
                where.List.RemoveRange(where.Index, taken.Count);
            }
            BeginDrag(taken, grab, start, checkpoint: false);
            Relayout();
        }

        private void BeginDrag(List<Block> blocks, SKPoint grab, SKPoint pointer, bool checkpoint)
        {
            if (checkpoint)
                Checkpoint();
            Select(null);
            float height = BlockLayout.StackHeight(blocks, _style);
            bool hat = blocks[0].Def.Shape == BlockShape.Hat;
            _drag = new Drag
            {
                Blocks = blocks, Grab = grab, Pointer = pointer, Height = height,
                Targets = BlockLayout.SnapTargets(Program, _layout, hat, height),
            };
        }

        private SKPoint DragTopLeft(Drag drag)
        {
            var w = ToWorld(drag.Pointer);
            return new SKPoint(w.X - drag.Grab.X, w.Y - drag.Grab.Y);
        }

        private SnapTarget? FindSnap(Drag drag)
        {
            if (drag.Pointer.X < PaletteW)
                return null;
            var tl = DragTopLeft(drag);
            SnapTarget? best = null;
            float bestDistance = SnapDistance;
            foreach (var t in drag.Targets)
            {
                float d = SKPoint.Distance(tl, t.Point);
                if (d < bestDistance)
                {
                    best = t;
                    bestDistance = d;
                }
            }
            return best;
        }

        private void Drop(Drag drag)
        {
            if (drag.Pointer.X < PaletteW)
            {
                // Back onto the palette: it's gone (undo brings it back).
                Changed();
                return;
            }

            if (drag.Snap is { } snap)
            {
                snap.List.InsertRange(snap.Index, drag.Blocks);
                if (snap.Prepend)
                {
                    snap.Stack.X = snap.Point.X;
                    snap.Stack.Y = snap.Point.Y;
                }
            }
            else
            {
                var tl = DragTopLeft(drag);
                Program.Stacks.Add(new BlockStack { X = tl.X, Y = tl.Y, Blocks = drag.Blocks });
            }
            Changed();
        }

        private void OpenPicker(Block block, string slot)
        {
            if (Program.Locate(block.Id) is not { } where)
                return;
            var box = _layout[where.Stack].First(b => ReferenceEquals(b.Block, block));
            var slotRect = box.Slots.First(s => s.Name == slot).Rect;
            var anchor = new SKRect(ToScreen(new SKPoint(slotRect.Left, slotRect.Top)).X, ToScreen(new SKPoint(slotRect.Left, slotRect.Top)).Y,
                                    ToScreen(new SKPoint(slotRect.Right, slotRect.Bottom)).X, ToScreen(new SKPoint(slotRect.Right, slotRect.Bottom)).Y);

            var kind = block.Def.Slot(slot).Kind;
            var picker = new Picker { Block = block, Slot = slot };
            const float pad = 12, gap = 8;
            float maxRow = 470, x = 0, y = 0, rowH = 0, widest = 0;
            var placed = new List<(SlotChoice, SKRect)>();
            foreach (var choice in SlotChoices.For(kind))
            {
                float w, h;
                if (kind == SlotKind.Color) { w = 64; h = 64; }
                else { w = Math.Max(52, _style.Measure(choice.Label, _style.Slot) + 30); h = 48; }
                if (x > 0 && x + w > maxRow) { x = 0; y += rowH + gap; rowH = 0; }
                placed.Add((choice, new SKRect(x, y, x + w, y + h)));
                x += w + gap;
                rowH = Math.Max(rowH, h);
                widest = Math.Max(widest, x - gap);
            }
            float panelW = widest + pad * 2, panelH = y + rowH + pad * 2;
            float left = Math.Clamp(anchor.Left, PaletteW + 6, Math.Max(PaletteW + 6, ViewWidth - panelW - 6));
            float top = anchor.Bottom + 8;
            if (top + panelH > ViewHeight - 6)
                top = Math.Max(6, anchor.Top - panelH - 8);
            picker.Panel = new SKRect(left, top, left + panelW, top + panelH);
            foreach (var (choice, r) in placed)
                picker.Options.Add((choice, new SKRect(left + pad + r.Left, top + pad + r.Top, left + pad + r.Right, top + pad + r.Bottom)));
            _picker = picker;
            Invalidated?.Invoke();
        }

        // =================================================================
        // Painting
        // =================================================================

        /// <summary>Paints the whole editor into a view of <see cref="ViewWidth"/> x <see cref="ViewHeight"/> pixels.</summary>
        public void Paint(SKCanvas canvas)
        {
            long now = Environment.TickCount64;
            var lit = LitBlocks(now);

            canvas.Clear(BlockStyle.Paper);
            canvas.Save();
            canvas.ClipRect(new SKRect(PaletteW, 0, ViewWidth, ViewHeight));
            PaintGrid(canvas);

            canvas.Save();
            canvas.Translate(PaletteW, 0);
            canvas.Scale(_zoom);
            canvas.Translate(-_scrollX, -_scrollY);
            foreach (var stack in Program.Stacks)
            {
                if (!_layout.TryGetValue(stack, out var boxes))
                    continue;
                bool runs = stack.Hat is not null;
                foreach (var box in boxes)
                    PaintBlock(canvas, box, lit, alpha: runs ? (byte)255 : (byte)150);
            }
            if (_drag is { Snap: { } snap } d)
                PaintGhost(canvas, d, snap);
            canvas.Restore();

            PaintEmptyHint(canvas);
            PaintZoomButtons(canvas);
            canvas.Restore();

            PaintPalette(canvas);

            if (_drag is { } drag)
            {
                canvas.Save();
                canvas.Translate(PaletteW, 0);
                canvas.Scale(_zoom);
                canvas.Translate(-_scrollX, -_scrollY);
                var tl = DragTopLeft(drag);
                var boxes = new List<BlockBox>();
                BlockLayout.LayoutList(drag.Blocks, tl.X, tl.Y, 0, _style, boxes);
                using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 50), ImageFilter = SKImageFilter.CreateBlur(8, 8), IsAntialias = true })
                {
                    foreach (var box in boxes.Where(b => b.Depth == 0))
                    {
                        using var o = BlockLayout.Outline(box);
                        o.Offset(4, 6);
                        canvas.DrawPath(o, shadow);
                    }
                }
                foreach (var box in boxes)
                    PaintBlock(canvas, box, lit, 255);
                canvas.Restore();
            }

            if (_picker is { } picker)
                PaintPicker(canvas, picker);
        }

        /// <summary>Blocks that ran within the last moment, plus the C blocks and hats around them.</summary>
        private HashSet<int> LitBlocks(long now)
        {
            var lit = new HashSet<int>();
            if (LastHit is not { } hit)
                return lit;
            foreach (var b in Program.AllBlocks())
            {
                long t = hit(b.Id);
                if (t != 0 && now - t < GlowMs)
                {
                    lit.Add(b.Id);
                    foreach (var a in Program.AncestorsOf(b.Id))
                        lit.Add(a);
                }
            }
            return lit;
        }

        private void PaintGrid(SKCanvas canvas)
        {
            using var dot = new SKPaint { Color = new SKColor(0, 0, 0, 22), IsAntialias = true };
            float step = 28 * _zoom;
            float ox = PaletteW + (-_scrollX * _zoom) % step;
            float oy = (-_scrollY * _zoom) % step;
            for (float x = ox; x < ViewWidth; x += step)
                for (float y = oy; y < ViewHeight; y += step)
                    if (x >= PaletteW)
                        canvas.DrawCircle(x, y, 1.4f, dot);
        }

        private void PaintBlock(SKCanvas canvas, BlockBox box, HashSet<int> lit, byte alpha)
        {
            var def = box.Block.Def;
            var color = BlockStyle.CategoryColor(def.Category);
            using var outline = BlockLayout.Outline(box);

            if (lit.Contains(box.Block.Id))
            {
                using var glow = new SKPaint
                {
                    Color = BlockStyle.Glow, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 10,
                    MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5),
                };
                canvas.DrawPath(outline, glow);
            }

            using (var fill = new SKPaint { Color = color.WithAlpha(alpha), IsAntialias = true })
                canvas.DrawPath(outline, fill);

            bool selected = SelectedBlockId == box.Block.Id;
            using (var stroke = new SKPaint
            {
                Color = selected ? BlockStyle.Ink : BlockStyle.Darker(color).WithAlpha(alpha),
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = selected ? 3.5f : 1.5f,
            })
                canvas.DrawPath(outline, stroke);

            float mid = box.HeaderTop + BlockStyle.RowH / 2;
            using var ink = new SKPaint { Color = BlockStyle.Ink.WithAlpha(alpha), IsAntialias = true };

            var iconFont = _style.IconFont(def.Icon);
            canvas.DrawText(def.Icon, box.IconX, Baseline(iconFont, mid), SKTextAlign.Left, iconFont, ink);
            foreach (var (text, x) in box.Words)
                canvas.DrawText(text, x, Baseline(_style.Text, mid), SKTextAlign.Left, _style.Text, ink);

            foreach (var slot in box.Slots)
                PaintSlot(canvas, box.Block, slot, alpha);
        }

        private void PaintSlot(SKCanvas canvas, Block block, SlotBox slot, byte alpha)
        {
            var kind = block.Def.Slot(slot.Name).Kind;
            var choice = SlotChoices.Get(kind, block.Value(slot.Name));
            var r = slot.Rect;
            using (var pill = new SKPaint { Color = SKColors.White.WithAlpha(alpha), IsAntialias = true })
                canvas.DrawRoundRect(r, BlockStyle.SlotH / 2, BlockStyle.SlotH / 2, pill);
            using (var edge = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(alpha / 6)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
                canvas.DrawRoundRect(r, BlockStyle.SlotH / 2, BlockStyle.SlotH / 2, edge);

            if (choice.Swatch is { } sw)
            {
                using var swatch = new SKPaint { Color = new SKColor(sw.R, sw.G, sw.B, alpha), IsAntialias = true };
                canvas.DrawCircle(r.MidX, r.MidY, BlockStyle.SwatchD / 2, swatch);
                using var ring = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(alpha / 4)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
                canvas.DrawCircle(r.MidX, r.MidY, BlockStyle.SwatchD / 2, ring);
                return;
            }
            using var ink = new SKPaint { Color = BlockStyle.Ink.WithAlpha(alpha), IsAntialias = true };
            canvas.DrawText(choice.Label, r.MidX, Baseline(_style.Slot, r.MidY), SKTextAlign.Center, _style.Slot, ink);
        }

        private static float Baseline(SKFont font, float middle)
        {
            var m = font.Metrics;
            return middle - (m.Ascent + m.Descent) / 2;
        }

        private void PaintGhost(SKCanvas canvas, Drag drag, SnapTarget snap)
        {
            var boxes = new List<BlockBox>();
            BlockLayout.LayoutList(drag.Blocks, snap.Point.X, snap.Point.Y, 0, _style, boxes);
            using var ghost = new SKPaint { Color = new SKColor(0, 0, 0, 40), IsAntialias = true };
            foreach (var box in boxes.Where(b => b.Depth == 0))
            {
                using var o = BlockLayout.Outline(box);
                canvas.DrawPath(o, ghost);
            }
        }

        private void PaintEmptyHint(SKCanvas canvas)
        {
            if (Program.Stacks.Count > 0 || _drag is not null)
                return;
            using var ink = new SKPaint { Color = new SKColor(45, 42, 36, 110), IsAntialias = true };
            canvas.DrawText(Loc.Tr("Arrastrá bloques desde la izquierda", "Drag blocks from the left"),
                PaletteW + (ViewWidth - PaletteW) / 2, ViewHeight / 2, SKTextAlign.Center, _style.Text, ink);
        }

        private void PaintZoomButtons(SKCanvas canvas)
        {
            _buttons.Clear();
            float size = 40, x = ViewWidth - size - 14, y = ViewHeight - (size + 8) * 3 - 6;
            foreach (var (action, label) in new[] { ("zoom+", "+"), ("zoom=", "="), ("zoom-", "−") })
            {
                var r = new SKRect(x, y, x + size, y + size);
                _buttons.Add((action, r));
                using (var bg = new SKPaint { Color = SKColors.White, IsAntialias = true })
                    canvas.DrawRoundRect(r, 12, 12, bg);
                using (var edge = new SKPaint { Color = new SKColor(0, 0, 0, 40), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
                    canvas.DrawRoundRect(r, 12, 12, edge);
                using var ink = new SKPaint { Color = BlockStyle.Ink, IsAntialias = true };
                canvas.DrawText(label, r.MidX, Baseline(_style.Text, r.MidY), SKTextAlign.Center, _style.Text, ink);
                y += size + 8;
            }
        }

        private void PaintPalette(SKCanvas canvas)
        {
            var panel = new SKRect(0, 0, PaletteW, ViewHeight);
            bool trash = _drag is { } d && d.Pointer.X < PaletteW;
            using (var bg = new SKPaint { Color = trash ? new SKColor(253, 232, 232) : SKColors.White })
                canvas.DrawRect(panel, bg);
            using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 28) })
                canvas.DrawRect(new SKRect(PaletteW - 1, 0, PaletteW, ViewHeight), shadow);

            // Category buttons.
            using (var col = new SKPaint { Color = new SKColor(250, 245, 238) })
                canvas.DrawRect(new SKRect(0, 0, CategoryW, ViewHeight), col);
            _categoryRects.Clear();
            float y = 14;
            foreach (var category in VisibleCategories())
            {
                var rect = new SKRect(4, y, CategoryW - 4, y + 72);
                _categoryRects.Add((category, rect));
                if (category == _category)
                {
                    using var sel = new SKPaint { Color = new SKColor(232, 221, 208), IsAntialias = true };
                    canvas.DrawRoundRect(rect, 14, 14, sel);
                }
                using (var dot = new SKPaint { Color = BlockStyle.CategoryColor(category), IsAntialias = true })
                    canvas.DrawCircle(rect.MidX, y + 25, 19, dot);
                var icon = BlockCatalog.CategoryIcon(category);
                var iconFont = _style.IconFont(icon, 19);
                using (var ink = new SKPaint { Color = BlockStyle.Ink, IsAntialias = true })
                {
                    canvas.DrawText(icon, rect.MidX, Baseline(iconFont, y + 25), SKTextAlign.Center, iconFont, ink);
                    canvas.DrawText(BlockCatalog.CategoryName(category), rect.MidX, y + 62, SKTextAlign.Center, _style.Small, ink);
                }
                y += 78;
            }

            // The blocks of the chosen category.
            canvas.Save();
            canvas.ClipRect(new SKRect(CategoryW, 0, PaletteW, ViewHeight));
            foreach (var (_, box) in _paletteBoxes)
                PaintBlock(canvas, box, new HashSet<int>(), 255);
            canvas.Restore();

            if (trash)
            {
                using var veil = new SKPaint { Color = new SKColor(253, 232, 232, 230) };
                canvas.DrawRect(panel, veil);
                var font = _style.IconFont("🗑", 54);
                using var ink = new SKPaint { Color = new SKColor(200, 70, 70), IsAntialias = true };
                canvas.DrawText("🗑", PaletteW / 2, ViewHeight / 2 - 10, SKTextAlign.Center, font, ink);
                canvas.DrawText(Loc.Tr("Soltá acá para borrar", "Drop here to delete"), PaletteW / 2, ViewHeight / 2 + 40, SKTextAlign.Center, _style.Text, ink);
            }
        }

        private void PaintPicker(SKCanvas canvas, Picker picker)
        {
            using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 60), ImageFilter = SKImageFilter.CreateBlur(14, 14) })
                canvas.DrawRoundRect(SKRect.Inflate(picker.Panel, 2, 2), 18, 18, shadow);
            using (var bg = new SKPaint { Color = SKColors.White, IsAntialias = true })
                canvas.DrawRoundRect(picker.Panel, 18, 18, bg);

            string current = picker.Block.Value(picker.Slot);
            var kind = picker.Block.Def.Slot(picker.Slot).Kind;
            var accent = BlockStyle.CategoryColor(picker.Block.Def.Category);
            foreach (var (choice, r) in picker.Options)
            {
                bool on = choice.Value == current;
                if (choice.Swatch is { } sw)
                {
                    using var fill = new SKPaint { Color = new SKColor(sw.R, sw.G, sw.B), IsAntialias = true };
                    canvas.DrawCircle(r.MidX, r.MidY, r.Width / 2 - 4, fill);
                    using var ring = new SKPaint
                    {
                        Color = on ? BlockStyle.Ink : new SKColor(0, 0, 0, 50), IsAntialias = true,
                        Style = SKPaintStyle.Stroke, StrokeWidth = on ? 4 : 1.5f,
                    };
                    canvas.DrawCircle(r.MidX, r.MidY, r.Width / 2 - 4, ring);
                }
                else
                {
                    using var fill = new SKPaint { Color = on ? accent : new SKColor(246, 242, 236), IsAntialias = true };
                    canvas.DrawRoundRect(r, 14, 14, fill);
                    using var ink = new SKPaint { Color = BlockStyle.Ink, IsAntialias = true };
                    canvas.DrawText(choice.Label, r.MidX, Baseline(_style.Slot, r.MidY), SKTextAlign.Center, _style.Slot, ink);
                }
            }
            _ = kind;
        }

        // =================================================================
        // For tests and the host
        // =================================================================

        /// <summary>Where a block's header is on screen right now (null if it isn't laid out).</summary>
        internal SKRect? ScreenRectOf(int blockId)
        {
            foreach (var boxes in _layout.Values)
                foreach (var box in boxes)
                    if (box.Block.Id == blockId)
                    {
                        var a = ToScreen(new SKPoint(box.X, box.HeaderTop));
                        var b = ToScreen(new SKPoint(box.X + box.Width, box.HeaderTop + BlockStyle.RowH));
                        return new SKRect(a.X, a.Y, b.X, b.Y);
                    }
            return null;
        }

        /// <summary>Where a palette block of <paramref name="type"/> is on screen (null if not on the current page).</summary>
        internal SKRect? PaletteRectOf(string type) =>
            _paletteBoxes.Where(p => p.Template.Type == type).Select(p => (SKRect?)p.Box.HeaderRect).FirstOrDefault();

        /// <summary>Where a slot's pill is on screen.</summary>
        internal SKRect? ScreenRectOfSlot(int blockId, string slot)
        {
            foreach (var boxes in _layout.Values)
                foreach (var box in boxes)
                    if (box.Block.Id == blockId && box.Slots.FirstOrDefault(s => s.Name == slot) is { } sb)
                    {
                        var a = ToScreen(new SKPoint(sb.Rect.Left, sb.Rect.Top));
                        var b = ToScreen(new SKPoint(sb.Rect.Right, sb.Rect.Bottom));
                        return new SKRect(a.X, a.Y, b.X, b.Y);
                    }
            return null;
        }

        internal IReadOnlyList<(SlotChoice Choice, SKRect Rect)> PickerOptions => _picker?.Options ?? new();

        internal bool IsDragging => _drag is not null;

        public void Dispose() => _style.Dispose();
    }
}
