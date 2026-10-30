using System;
using System.Collections.Generic;
using System.Linq;
using DanaProcessing.Ide.Localization;
using SkiaSharp;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// The block editor itself: the palette on the left, the workspace on the
    /// right, pressing, dragging, snapping, the value picker, undo, panning
    /// and zoom. What blocks LOOK like is up to the current view
    /// (<see cref="IBlockRenderer"/>: tiles or lines). It knows nothing about
    /// Avalonia: a host feeds it pointer events in pixels and asks it to paint,
    /// which is also how tests drive it and take screenshots of it.
    /// </summary>
    internal sealed class BlocksWorkspace : IDisposable
    {
        private const float CategoryW = 76;
        private const float DragThreshold = 5;
        private const int GlowMs = 400;

        private IBlockRenderer _renderer = new TileRenderer();

        public BlocksWorkspace(BlockProgram program, IReadOnlyCollection<string>? palette = null) => Load(program, palette);

        public BlockProgram Program { get; private set; } = new();

        /// <summary>The blocks offered on the palette (a challenge offers only a few); null = all of them.</summary>
        public IReadOnlyCollection<string>? Palette { get; private set; }

        /// <summary>After anything that changes the program (blocks moved, added, removed, a value picked, the view).</summary>
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

        /// <summary>Width of the palette (category buttons + the current view's blocks), fixed per program and view.</summary>
        public float PaletteW { get; private set; } = 320;

        public BlockView View => _renderer.Kind;

        private float _scrollX, _scrollY, _zoom = 1;
        private BlockCategory _category = BlockCategory.Events;
        private float _paletteScroll;
        private float _laidOutWidth = -1;

        private SceneLayout _scene = new();
        private List<(Block Template, SKRect Rect)> _paletteItems = new();
        private readonly List<(BlockCategory Category, SKRect Rect)> _categoryRects = new();
        private readonly Stack<string> _undo = new(), _redo = new();

        // A press that hasn't turned into a click or a drag yet.
        private sealed record Press(SKPoint Start, Block? Block, string? Slot, Block? Template, SKRect TemplateRect, bool Pan, SKPoint ScrollAtStart);
        private Press? _press;

        private sealed class Drag
        {
            public required List<Block> Blocks { get; init; }
            public SKPoint Grab;          // pointer minus the column's top-left, in world units
            public SKPoint Pointer;       // screen
            public SnapTarget? Snap;
            public bool StartsWithHat;
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
        // Program and view
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
            UseRenderer(program.View);
            var categories = VisibleCategories();
            _category = categories.Contains(_category) ? _category : categories.FirstOrDefault();
            Relayout();
            ScrollHome();
        }

        /// <summary>Shows the same program the other way (tiles / lines). It's saved with the program.</summary>
        public void SetView(BlockView view)
        {
            if (view == _renderer.Kind)
                return;
            _picker = null;
            Program.View = view;
            UseRenderer(view);
            Relayout();
            ScrollHome();
            ProgramChanged?.Invoke();
        }

        private void UseRenderer(BlockView view)
        {
            if (_renderer.Kind != view)
            {
                _renderer.Dispose();
                _renderer = view == BlockView.Lines ? new LineRenderer() : new TileRenderer();
            }
            PaletteW = Math.Clamp(CategoryW + _renderer.PaletteWidth(PaletteTemplates(all: true)), 260, 520);
            _zoom = 1;
        }

        private void ScrollHome()
        {
            var visuals = _scene.Stacks.Values.SelectMany(v => v).ToList();
            _scrollX = (visuals.Count == 0 ? 0 : visuals.Min(v => v.Bounds.Left)) - 40;
            _scrollY = (visuals.Count == 0 ? 0 : visuals.Min(v => v.Bounds.Top)) - 40;
            if (View == BlockView.Lines)
                _scrollX = -10;
            Invalidated?.Invoke();
        }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        private void Checkpoint()
        {
            _undo.Push(Program.ToJson());
            if (_undo.Count > 60)
            {
                var keep = _undo.Take(60).Reverse().ToList();
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
            restored.View = Program.View; // undo is about blocks, not about how they're shown
            Program = restored;
            _picker = null;
            Changed();
        }

        private void Changed()
        {
            Relayout();
            ProgramChanged?.Invoke();
        }

        private float VisibleWorldWidth => (ViewWidth - PaletteW) / _zoom;

        /// <param name="notify">False while painting: asking to be painted again from inside a paint is an error in Avalonia.</param>
        private void Relayout(bool notify = true)
        {
            var clean = BlockCodeGenerator.Generate(Program);
            _laidOutWidth = VisibleWorldWidth;
            _scene = _renderer.Layout(Program, clean, _laidOutWidth);
            LayoutPalette();
            if (notify)
                Invalidated?.Invoke();
        }

        /// <summary>Deletes the selected block (and what's inside it, for repetir/si).</summary>
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
        // Coordinates and zoom
        // =================================================================

        private SKPoint ToWorld(SKPoint screen) => new((screen.X - PaletteW) / _zoom + _scrollX, screen.Y / _zoom + _scrollY);
        private SKPoint ToScreen(SKPoint world) => new(PaletteW + (world.X - _scrollX) * _zoom, (world.Y - _scrollY) * _zoom);

        private SKRect ToScreen(SKRect world)
        {
            var a = ToScreen(new SKPoint(world.Left, world.Top));
            var b = ToScreen(new SKPoint(world.Right, world.Bottom));
            return new SKRect(a.X, a.Y, b.X, b.Y);
        }

        public void ZoomBy(float factor, SKPoint? around = null)
        {
            var center = around ?? new SKPoint(PaletteW + (ViewWidth - PaletteW) / 2, ViewHeight / 2);
            var before = ToWorld(center);
            _zoom = Math.Clamp(_zoom * factor, 0.5f, 2f);
            var after = ToWorld(center);
            _scrollX += before.X - after.X;
            _scrollY += before.Y - after.Y;
            Relayout();
        }

        public void ResetZoom()
        {
            _zoom = 1;
            Relayout();
            ScrollHome();
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

        private List<Block> PaletteTemplates(bool all)
        {
            var scratch = new BlockProgram { NextId = -100000 };
            return BlockCatalog.All
                .Where(d => all || d.Category == _category)
                .Where(d => Palette is null || Palette.Contains(d.Type))
                .Select(d => scratch.NewBlock(d.Type))
                .ToList();
        }

        private void LayoutPalette() =>
            _paletteItems = _renderer.LayoutPalette(PaletteTemplates(all: false), CategoryW, 20 + _paletteScroll, PaletteW - CategoryW);

        // =================================================================
        // Pointer
        // =================================================================

        public void PointerDown(SKPoint p)
        {
            if (_picker is { } picker)
            {
                foreach (var (choice, rect) in picker.Options)
                {
                    if (!rect.Contains(p))
                        continue;
                    _picker = null;
                    if (picker.Block.Value(picker.Slot) != choice.Value)
                    {
                        Checkpoint();
                        picker.Block.Values[picker.Slot] = choice.Value;
                        Changed();
                    }
                    else
                    {
                        Invalidated?.Invoke();
                    }
                    return;
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
                foreach (var (template, rect) in _paletteItems)
                {
                    if (rect.Contains(p))
                    {
                        _press = new Press(p, null, null, template, rect, false, default);
                        return;
                    }
                }
                return;
            }

            var (block, slot) = HitBlock(ToWorld(p));
            _press = block is null
                ? new Press(p, null, null, null, default, true, new SKPoint(_scrollX, _scrollY))
                : new Press(p, block, slot, null, default, false, default);
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

            if (press.Pan)
            {
                _scrollX = press.ScrollAtStart.X - (p.X - press.Start.X) / _zoom;
                _scrollY = press.ScrollAtStart.Y - (p.Y - press.Start.Y) / _zoom;
                Invalidated?.Invoke();
                return;
            }

            if (SKPoint.Distance(p, press.Start) < DragThreshold)
                return;

            if (press.Template is { } template)
                StartDragFromPalette(template, press.TemplateRect, press.Start);
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
                _drag = null;
                Drop(drag);
                return;
            }

            if (_press is { Pan: false } press && press.Block is { } block)
            {
                if (press.Slot is { } slot)
                    OpenPicker(block, slot);
                else
                    Select(SelectedBlockId == block.Id ? null : block.Id);
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
                float content = _paletteItems.Count == 0 ? 0 : _paletteItems.Max(i => i.Rect.Bottom) - _paletteScroll + 40;
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
            foreach (var stack in Enumerable.Reverse(Program.Stacks))
            {
                if (!_scene.Stacks.TryGetValue(stack, out var visuals))
                    continue;
                for (int i = visuals.Count - 1; i >= 0; i--)
                {
                    var v = visuals[i];
                    foreach (var slot in v.Slots)
                    {
                        var r = slot.Rect;
                        r.Inflate(4, 4); // forgiving for small hands
                        if (r.Contains(world))
                            return (v.Block, slot.Name);
                    }
                    if (v.Hit.Contains(world))
                        return (v.Block, null);
                }
            }
            return (null, null);
        }

        private void StartDragFromPalette(Block template, SKRect rect, SKPoint start)
        {
            Checkpoint();
            var block = Program.NewBlock(template.Type);
            // Picked up at the same spot of the block (the palette is drawn at zoom 1).
            var grab = new SKPoint((start.X - rect.Left) / _zoom, (start.Y - rect.Top) / _zoom);
            BeginDrag(new List<Block> { block }, grab, start);
        }

        private void StartDragFromWorkspace(Block block, SKPoint start)
        {
            if (Program.Locate(block.Id) is not { } where)
                return;
            var visual = _scene.Stacks[where.Stack].First(v => ReferenceEquals(v.Block, block));
            var world = ToWorld(start);
            var grab = new SKPoint(world.X - visual.Hit.Left, world.Y - visual.Hit.Top);

            Checkpoint();
            List<Block> taken;
            if (where.Owner is null && where.Index == 0)
            {
                // The first block of a column (its start block, say): the whole column comes along.
                taken = new List<Block>(where.Stack.Blocks);
                Program.Stacks.Remove(where.Stack);
            }
            else
            {
                // This block and everything after it in the same list.
                taken = where.List.GetRange(where.Index, where.List.Count - where.Index);
                where.List.RemoveRange(where.Index, taken.Count);
            }
            Relayout();
            BeginDrag(taken, grab, start);
        }

        private void BeginDrag(List<Block> blocks, SKPoint grab, SKPoint pointer)
        {
            Select(null);
            _drag = new Drag
            {
                Blocks = blocks, Grab = grab, Pointer = pointer,
                StartsWithHat = blocks[0].Def.Shape == BlockShape.Hat,
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
            var pointer = ToWorld(drag.Pointer);
            SnapTarget? best = null;
            float bestDistance = _renderer.SnapRadius;
            foreach (var t in _scene.Targets)
            {
                // A column starting with "al empezar" & co. only goes on top of a column that has none.
                if (drag.StartsWithHat && !t.Prepend)
                    continue;
                float d = _renderer.SnapDistance(pointer, t);
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
                if (snap.Prepend)
                {
                    var (x, y) = _renderer.PrependOrigin(snap.Stack, drag.Blocks);
                    snap.Stack.X = x;
                    snap.Stack.Y = y;
                }
                snap.List.InsertRange(snap.Index, drag.Blocks);
            }
            else
            {
                var (x, y) = _renderer.NewStackPosition(Program, _scene, DragTopLeft(drag), ToWorld(drag.Pointer));
                Program.Stacks.Add(new BlockStack { X = x, Y = y, Blocks = drag.Blocks });
            }
            Changed();
        }

        private void OpenPicker(Block block, string slot)
        {
            if (Program.Locate(block.Id) is not { } where)
                return;
            var visual = _scene.Stacks[where.Stack].First(v => ReferenceEquals(v.Block, block));
            var anchor = ToScreen(visual.Slots.First(s => s.Name == slot).Rect);

            var kind = block.Def.Slot(slot).Kind;
            var picker = new Picker { Block = block, Slot = slot };
            const float pad = 14, gap = 8, maxRow = 480;
            float titleH = TitleHeight;
            float x = 0, y = 0, rowH = 0, widest = 0;
            var placed = new List<(SlotChoice, SKRect)>();
            foreach (var choice in SlotChoices.For(kind))
            {
                var size = _renderer.OptionSize(kind, choice);
                if (x > 0 && x + size.Width > maxRow) { x = 0; y += rowH + gap; rowH = 0; }
                placed.Add((choice, new SKRect(x, y, x + size.Width, y + size.Height)));
                x += size.Width + gap;
                rowH = Math.Max(rowH, size.Height);
                widest = Math.Max(widest, x - gap);
            }
            float panelW = Math.Max(widest, _renderer.Style.Text.MeasureText(PickerQuestion(kind)) + 8) + pad * 2;
            float panelH = titleH + y + rowH + pad * 2;
            float left = Math.Clamp(anchor.Left, PaletteW + 6, Math.Max(PaletteW + 6, ViewWidth - panelW - 6));
            float top = anchor.Bottom + 10;
            if (top + panelH > ViewHeight - 6)
                top = Math.Max(6, anchor.Top - panelH - 10);
            picker.Panel = new SKRect(left, top, left + panelW, top + panelH);
            foreach (var (choice, r) in placed)
                picker.Options.Add((choice, new SKRect(left + pad + r.Left, top + titleH + pad + r.Top, left + pad + r.Right, top + titleH + pad + r.Bottom)));
            _picker = picker;
            Invalidated?.Invoke();
        }

        // =================================================================
        // Painting
        // =================================================================

        /// <summary>Paints the whole editor into a view of <see cref="ViewWidth"/> x <see cref="ViewHeight"/> pixels.</summary>
        public void Paint(SKCanvas canvas)
        {
            if (Math.Abs(VisibleWorldWidth - _laidOutWidth) > 1)
                Relayout(notify: false); // resized: the lines view keeps its code column at the right edge

            var context = new PaintContext { Lit = LitBlocks(Environment.TickCount64), Selected = SelectedBlockId };

            canvas.Clear(BlockStyle.Paper);
            canvas.Save();
            canvas.ClipRect(new SKRect(PaletteW, 0, ViewWidth, ViewHeight));
            PaintGrid(canvas);

            canvas.Save();
            canvas.Translate(PaletteW, 0);
            canvas.Scale(_zoom);
            canvas.Translate(-_scrollX, -_scrollY);
            _renderer.PaintScene(canvas, Program, _scene, context);
            if (_drag is { Snap: { } snap } d)
                _renderer.PaintSnap(canvas, snap, d.Blocks);
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
                var visuals = _renderer.LayoutLoose(drag.Blocks, DragTopLeft(drag));
                _renderer.PaintLoose(canvas, visuals, new PaintContext(), 235);
                if (drag.Snap is { } over)
                    _renderer.PaintSnap(canvas, over, drag.Blocks); // where it'll land stays visible over the block in hand
                canvas.Restore();
            }

            if (_picker is { } picker)
                PaintPicker(canvas, picker);
        }

        /// <summary>Blocks that ran within the last moment, plus the trays and start blocks around them.</summary>
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
            float ox = PaletteW + ((-_scrollX * _zoom) % step + step) % step;
            float oy = ((-_scrollY * _zoom) % step + step) % step;
            for (float x = ox; x < ViewWidth; x += step)
                for (float y = oy; y < ViewHeight; y += step)
                    canvas.DrawCircle(x, y, 1.4f, dot);
        }

        private void PaintEmptyHint(SKCanvas canvas)
        {
            if (Program.Stacks.Count > 0 || _drag is not null)
                return;
            using var ink = new SKPaint { Color = BlockStyle.Muted, IsAntialias = true };
            canvas.DrawText(Loc.Tr("Arrastrá bloques desde la izquierda", "Drag blocks from the left"),
                PaletteW + (ViewWidth - PaletteW) / 2, ViewHeight / 2, SKTextAlign.Center, _renderer.Style.Text, ink);
        }

        private void PaintZoomButtons(SKCanvas canvas)
        {
            _buttons.Clear();
            float size = 44, x = ViewWidth - size - 14, y = ViewHeight - (size + 8) * 3 - 6;
            foreach (var action in new[] { "zoom+", "zoom=", "zoom-" })
            {
                var r = new SKRect(x, y, x + size, y + size);
                _buttons.Add((action, r));
                using (var bg = new SKPaint { Color = SKColors.White, IsAntialias = true })
                    canvas.DrawRoundRect(r, 14, 14, bg);
                using (var edge = new SKPaint { Color = new SKColor(0, 0, 0, 40), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
                    canvas.DrawRoundRect(r, 14, 14, edge);
                using var ink = new SKPaint { Color = BlockStyle.Ink, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.6f, StrokeCap = SKStrokeCap.Round };
                float c = 8;
                switch (action)
                {
                    case "zoom+": canvas.DrawLine(r.MidX - c, r.MidY, r.MidX + c, r.MidY, ink); canvas.DrawLine(r.MidX, r.MidY - c, r.MidX, r.MidY + c, ink); break;
                    case "zoom-": canvas.DrawLine(r.MidX - c, r.MidY, r.MidX + c, r.MidY, ink); break;
                    default: canvas.DrawRoundRect(new SKRect(r.MidX - c, r.MidY - c, r.MidX + c, r.MidY + c), 3, 3, ink); break;
                }
                y += size + 8;
            }
        }

        private void PaintPalette(SKCanvas canvas)
        {
            var panel = new SKRect(0, 0, PaletteW, ViewHeight);
            bool trash = _drag is { } d && d.Pointer.X < PaletteW;
            using (var bg = new SKPaint { Color = SKColors.White })
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
                var rect = new SKRect(6, y, CategoryW - 6, y + 70);
                _categoryRects.Add((category, rect));
                var color = BlockStyle.CategoryColor(category);
                if (category == _category)
                {
                    using var sel = new SKPaint { Color = BlockStyle.Tint(color, 0.7f), IsAntialias = true };
                    canvas.DrawRoundRect(rect, 16, 16, sel);
                }
                using (var dot = new SKPaint { Color = color, IsAntialias = true })
                    canvas.DrawCircle(rect.MidX, y + 25, 19, dot);
                var icon = new SKRect(rect.MidX - 11, y + 14, rect.MidX + 11, y + 36);
                if (category == BlockCategory.Turtle)
                    BlockIcons.DrawTurtle(canvas, SKRect.Inflate(icon, 4, 4), white: true);
                else
                    BlockIcons.DrawCategory(canvas, category, icon, SKColors.White);
                using (var ink = new SKPaint { Color = BlockStyle.Ink, IsAntialias = true })
                    canvas.DrawText(BlockCatalog.CategoryName(category), rect.MidX, y + 61, SKTextAlign.Center, _renderer.Style.Small, ink);
                y += 76;
            }

            canvas.Save();
            canvas.ClipRect(new SKRect(CategoryW, 0, PaletteW, ViewHeight));
            foreach (var (template, rect) in _paletteItems)
                _renderer.PaintPaletteItem(canvas, template, rect);
            canvas.Restore();

            if (trash)
            {
                using var veil = new SKPaint { Color = new SKColor(253, 232, 232, 235) };
                canvas.DrawRect(panel, veil);
                var red = new SKColor(196, 64, 64);
                BlockIcons.DrawPath(canvas, "M4 7h16M10 11v6M14 11v6M5 7l1 13h12l1-13M9 7V4h6v3", false,
                    new SKRect(PaletteW / 2 - 28, ViewHeight / 2 - 70, PaletteW / 2 + 28, ViewHeight / 2 - 14), red, 2);
                using var ink = new SKPaint { Color = red, IsAntialias = true };
                canvas.DrawText(Loc.Tr("Soltá acá para borrar", "Drop here to delete"), PaletteW / 2, ViewHeight / 2 + 20, SKTextAlign.Center, _renderer.Style.Text, ink);
            }
        }

        private const float TitleHeight = 30;

        /// <summary>What the picker asks, in a child's words.</summary>
        internal static string PickerQuestion(SlotKind kind) => kind switch
        {
            SlotKind.Steps => Loc.Tr("¿Cuántos pasos?", "How many steps?"),
            SlotKind.Angle => Loc.Tr("¿Cuánto gira?", "How far to turn?"),
            SlotKind.Count => Loc.Tr("¿Cuántas veces?", "How many times?"),
            SlotKind.Thickness => Loc.Tr("¿Qué tan gruesa?", "How thick?"),
            SlotKind.Color => Loc.Tr("¿Qué color?", "Which color?"),
            SlotKind.Size => Loc.Tr("¿Qué tamaño?", "What size?"),
            SlotKind.Place => Loc.Tr("¿Dónde?", "Where?"),
            SlotKind.Note => Loc.Tr("¿Qué nota?", "Which note?"),
            SlotKind.Drum => Loc.Tr("¿Qué tambor?", "Which drum?"),
            SlotKind.Beats => Loc.Tr("¿Cuánto dura?", "How long?"),
            SlotKind.Instrument => Loc.Tr("¿Qué instrumento?", "Which instrument?"),
            SlotKind.Condition => Loc.Tr("¿Cuándo?", "When?"),
            _ => "",
        };

        private void PaintPicker(SKCanvas canvas, Picker picker)
        {
            using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 60), ImageFilter = SKImageFilter.CreateBlur(14, 14) })
                canvas.DrawRoundRect(SKRect.Inflate(picker.Panel, 2, 2), 20, 20, shadow);
            using (var bg = new SKPaint { Color = SKColors.White, IsAntialias = true })
                canvas.DrawRoundRect(picker.Panel, 20, 20, bg);

            string current = picker.Block.Value(picker.Slot);
            var kind = picker.Block.Def.Slot(picker.Slot).Kind;
            var accent = BlockStyle.CategoryColor(picker.Block.Def.Category);
            using (var title = new SKPaint { Color = BlockStyle.Darker(accent, 0.6f), IsAntialias = true })
            {
                var font = _renderer.Style.Text;
                float baseline = BlockStyle.Baseline(font, picker.Panel.Top + 14 + TitleHeight / 2 - 2);
                canvas.DrawText(PickerQuestion(kind), picker.Panel.Left + 16, baseline, SKTextAlign.Left, font, title);
            }
            foreach (var (choice, r) in picker.Options)
                _renderer.PaintOption(canvas, kind, choice, r, choice.Value == current, accent);
        }

        // =================================================================
        // For tests and the host
        // =================================================================

        /// <summary>Where a block's grab area (tile, row, handle) is on screen right now.</summary>
        internal SKRect? ScreenRectOf(int blockId) =>
            _scene.Stacks.Values.SelectMany(v => v).Where(v => v.Block.Id == blockId).Select(v => (SKRect?)ToScreen(v.Hit)).FirstOrDefault();

        /// <summary>Where a palette entry of <paramref name="type"/> is on screen (null if not on the current page).</summary>
        internal SKRect? PaletteRectOf(string type) =>
            _paletteItems.Where(p => p.Template.Type == type).Select(p => (SKRect?)p.Rect).FirstOrDefault();

        /// <summary>Where a slot's value is on screen.</summary>
        internal SKRect? ScreenRectOfSlot(int blockId, string slot) =>
            _scene.Stacks.Values.SelectMany(v => v).Where(v => v.Block.Id == blockId)
                  .SelectMany(v => v.Slots).Where(s => s.Name == slot).Select(s => (SKRect?)ToScreen(s.Rect)).FirstOrDefault();

        /// <summary>Where the pointer has to be (on screen) to drop into <paramref name="list"/> before <paramref name="index"/>.</summary>
        internal SKPoint? ScreenPointOfTarget(List<Block> list, int index, bool prepend = false) =>
            _scene.Targets.Where(t => ReferenceEquals(t.List, list) && t.Index == index && t.Prepend == prepend)
                  .Select(t => (SKPoint?)ToScreen(t.Point)).FirstOrDefault();

        internal IReadOnlyList<(SlotChoice Choice, SKRect Rect)> PickerOptions => _picker?.Options ?? new();

        internal bool IsDragging => _drag is not null;

        public void Dispose()
        {
            _renderer.Dispose();
        }
    }
}
