using System;
using System.Collections.Generic;
using System.Linq;
using DanaProcessing.Ide.Localization;
using SkiaSharp;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// The "Renglones" view, for 9-12 year olds: the program reads like a
    /// notebook -- one sentence per ruled line, indented inside "repetir" and
    /// "si", with a closing line -- and each line sits right beside the line
    /// of C# it becomes. Same blocks, same program as the tiles: only the
    /// picture changes, so it's the step from blocks to code.
    /// </summary>
    internal sealed class LineRenderer : IBlockRenderer
    {
        public const float RowH = 52;
        public const float X0 = 76;           // where depth-0 sentences start
        public const float Indent = 34;
        private const float ChipH = 34;
        private const float ChipPad = 10;
        private const float PartGap = 8;
        private const float CodeW = 440;
        private const float BetweenScripts = 26;

        private static readonly SKColor PaperColor = new(253, 252, 248);
        private static readonly SKColor Rule = new(236, 231, 220);
        private static readonly SKColor Margin = new(241, 184, 168);
        private static readonly SKColor CodeBg = new(34, 32, 28);
        private static readonly SKColor CodeInk = new(233, 228, 218);
        private static readonly SKColor CodeMuted = new(181, 172, 156);

        private readonly BlockStyle _style = new();

        public BlockView Kind => BlockView.Lines;
        public float SnapRadius => 34;

        private enum RowKind { Header, End, Empty }

        private sealed record Row(float Top, int Depth, RowKind Kind, Block? Block, BlockStack Stack, bool Loose, string Code);

        /// <summary>The scene plus what only this view needs: every line, and where the C# column starts.</summary>
        private sealed class LineScene : SceneLayout
        {
            public List<Row> Rows { get; } = new();
            public float CodeX { get; set; }
        }

        // =================================================================
        // Measuring a sentence
        // =================================================================

        private sealed record Part(string? Text, string? Slot, float X, float W);

        /// <summary>The pieces of a block's sentence, relative to where it starts, and its total width.</summary>
        private (List<Part> Parts, float Width) Sentence(Block b)
        {
            var def = b.Def;
            var parts = new List<Part>();
            float x = 32; // after the icon
            bool first = true;
            foreach (var (text, slot) in def.Parts())
            {
                if (text is not null)
                {
                    var t = text.Trim();
                    if (t.Length == 0)
                        continue;
                    if (first && def.Shape == BlockShape.Hat)
                        t = char.ToUpper(t[0]) + t[1..];
                    float w = _style.Text.MeasureText(t);
                    parts.Add(new Part(t, null, x, w));
                    x += w + PartGap;
                }
                else if (slot is not null)
                {
                    float w = ChipWidth(b, slot);
                    parts.Add(new Part(null, slot, x, w));
                    x += w + PartGap;
                }
                first = false;
            }
            if (def.Shape != BlockShape.Statement)
            {
                // "repetir 4 veces:" / "Al empezar:" -- what follows is inside it.
                float w = _style.Text.MeasureText(":");
                parts.Add(new Part(":", null, x - PartGap + 2, w));
                x += w + 2 - PartGap;
            }
            return (parts, x);
        }

        private float ChipWidth(Block b, string slot)
        {
            var choice = SlotChoices.Get(b.Def.Slot(slot).Kind, b.Value(slot));
            float w = _style.Number.MeasureText(choice.Label) + ChipPad * 2;
            if (choice.Swatch is not null)
                w += 22;
            return Math.Max(ChipH + 8, w);
        }

        // =================================================================
        // Layout
        // =================================================================

        public SceneLayout Layout(BlockProgram program, GeneratedCode clean, float visibleWidth)
        {
            var scene = new LineScene();
            var musicNumbers = program.Scripts().Where(s => s.Hat!.Type == BlockCatalog.MusicLoop)
                                      .Select((s, i) => (s.Hat!.Id, i + 1)).ToDictionary(p => p.Item1, p => p.Item2);
            float y = 0;
            foreach (var stack in Ordered(program))
            {
                var visuals = new List<BlockVisual>();
                bool hat = stack.Hat is not null;
                if (!hat && stack.Blocks.Count > 0)
                    scene.Targets.Add(new SnapTarget(stack.Blocks, 0, stack, new SKPoint(X0, y), true, 0));
                PlaceList(stack.Blocks, 0, stack, hatAtTop: hat, firstTarget: 1, ref y, visuals, scene, clean, musicNumbers, loose: !hat);
                scene.Stacks[stack] = visuals;
                y += BetweenScripts;
            }
            float right = scene.Stacks.Values.SelectMany(v => v).Select(v => v.Hit.Right).DefaultIfEmpty(X0 + 300).Max();
            scene.CodeX = Math.Max(right + 70, visibleWidth - CodeW - 10);
            return scene;
        }

        /// <summary>Scripts in reading order (top to bottom, then left to right): in a notebook, position is just order.</summary>
        private static IEnumerable<BlockStack> Ordered(BlockProgram program) => program.Stacks.OrderBy(s => s.Y).ThenBy(s => s.X);

        private void PlaceList(List<Block> list, int baseDepth, BlockStack stack, bool hatAtTop, int firstTarget, ref float y,
                               List<BlockVisual> into, LineScene? scene, GeneratedCode? clean, Dictionary<int, int>? music, bool loose)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int depth = hatAtTop && i > 0 ? baseDepth + 1 : baseDepth;
                if (scene is not null && i >= firstTarget)
                    scene.Targets.Add(new SnapTarget(list, i, stack, new SKPoint(X0 + depth * Indent, y), false, depth));
                PlaceBlock(list[i], depth, stack, ref y, into, scene, clean, music, loose && i == 0, loose);
            }
            if (scene is not null)
            {
                int depth = hatAtTop && list.Count > 0 ? baseDepth + 1 : baseDepth;
                scene.Targets.Add(new SnapTarget(list, list.Count, stack, new SKPoint(X0 + depth * Indent, y), false, depth));
            }
        }

        private void PlaceBlock(Block b, int depth, BlockStack stack, ref float y, List<BlockVisual> into,
                                LineScene? scene, GeneratedCode? clean, Dictionary<int, int>? music, bool firstLoose, bool loose)
        {
            float x = X0 + depth * Indent;
            var (parts, width) = Sentence(b);
            float top = y;
            var visual = new BlockVisual { Block = b, Hit = new SKRect(x - 6, top + 4, x + width + 14, top + RowH - 4), Depth = depth };
            float chipTop = top + (RowH - ChipH) / 2;
            foreach (var p in parts.Where(p => p.Slot is not null))
                visual.Slots.Add(new SlotBox(p.Slot!, new SKRect(x + p.X, chipTop, x + p.X + p.W, chipTop + ChipH)));
            into.Add(visual);

            string code = clean is null ? "" : CodeFor(b, depth, clean, music);
            if (firstLoose)
                code = Loc.Tr("// no corre: le falta un comienzo", "// doesn't run: it has no start block");
            else if (loose)
                code = "";
            scene?.Rows.Add(new Row(top, depth, RowKind.Header, b, stack, loose, code));
            y += RowH;

            if (b.Def.Shape == BlockShape.CBlock)
            {
                var body = b.Body ?? new List<Block>();
                if (body.Count == 0)
                {
                    scene?.Targets.Add(new SnapTarget(body, 0, stack, new SKPoint(X0 + (depth + 1) * Indent, y), false, depth + 1));
                    scene?.Rows.Add(new Row(y, depth + 1, RowKind.Empty, b, stack, loose, ""));
                    y += RowH;
                }
                else
                {
                    PlaceList(body, depth + 1, stack, hatAtTop: false, firstTarget: 0, ref y, into, scene, clean, music, loose);
                }
                scene?.Rows.Add(new Row(y, depth, RowKind.End, b, stack, loose, loose ? "" : new string(' ', depth * 4) + "}"));
                y += RowH;
            }
            visual.Bounds = new SKRect(visual.Hit.Left, top, visual.Hit.Right, y);
        }

        /// <summary>This block's own line(s) of the generated C#, indented as deep as its sentence.</summary>
        private static string CodeFor(Block b, int depth, GeneratedCode clean, Dictionary<int, int>? music)
        {
            string pad = new(' ', depth * 4);
            switch (b.Type)
            {
                case BlockCatalog.Start: return "void Setup() {";
                case BlockCatalog.Forever: return "void Draw() {";
                case BlockCatalog.Click: return "void MousePressed() {";
                case BlockCatalog.Key: return "void KeyPressed() {";
                case BlockCatalog.MusicLoop: return $"async Task Musica{(music != null && music.TryGetValue(b.Id, out var n) ? n : 1)}() {{";
            }
            if (!clean.BlockLines.TryGetValue(b.Id, out var range))
                return "";
            var lines = clean.Source.Split('\n');
            if (b.Def.Shape == BlockShape.CBlock)
                return pad + lines[range.First - 1].Trim() + " {";
            var own = new List<string>();
            for (int i = range.First; i <= range.Last && i <= lines.Length; i++)
                own.Add(pad + lines[i - 1].Trim());
            return string.Join("\n", own);
        }

        public List<BlockVisual> LayoutLoose(List<Block> blocks, SKPoint topLeft)
        {
            var visuals = new List<BlockVisual>();
            float y = topLeft.Y;
            // Laid out at depth 0 relative to the pointer's spot.
            float shift = topLeft.X - X0;
            PlaceList(blocks, 0, new BlockStack(), hatAtTop: blocks.Count > 0 && blocks[0].Def.Shape == BlockShape.Hat, firstTarget: 0, ref y, visuals, null, null, null, loose: false);
            return visuals.Select(v => Shifted(v, shift)).ToList();
        }

        private static BlockVisual Shifted(BlockVisual v, float dx)
        {
            var moved = new BlockVisual
            {
                Block = v.Block, Depth = v.Depth,
                Hit = new SKRect(v.Hit.Left + dx, v.Hit.Top, v.Hit.Right + dx, v.Hit.Bottom),
                Bounds = new SKRect(v.Bounds.Left + dx, v.Bounds.Top, v.Bounds.Right + dx, v.Bounds.Bottom),
            };
            foreach (var s in v.Slots)
                moved.Slots.Add(s with { Rect = new SKRect(s.Rect.Left + dx, s.Rect.Top, s.Rect.Right + dx, s.Rect.Bottom) });
            return moved;
        }

        public float SnapDistance(SKPoint pointer, SnapTarget target)
        {
            // Mostly the gap between two lines; how far right says how deep.
            float dy = pointer.Y - target.Point.Y, dx = (pointer.X - target.Point.X) * 0.35f;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        public (double X, double Y) NewStackPosition(BlockProgram program, SceneLayout layout, SKPoint topLeft, SKPoint pointer)
        {
            // In a notebook a new script goes between the scripts above and below where it was dropped.
            var ordered = Ordered(program).ToList();
            var tops = ordered.Select(s => layout.Stacks.TryGetValue(s, out var v) && v.Count > 0 ? v[0].Bounds.Top : 0f).ToList();
            int k = tops.Count(t => t < pointer.Y);
            double y = ordered.Count == 0 ? 0
                : k == 0 ? ordered[0].Y - 10
                : k == ordered.Count ? ordered[^1].Y + 10
                : (ordered[k - 1].Y + ordered[k].Y) / 2;
            return (0, y);
        }

        public (double X, double Y) PrependOrigin(BlockStack stack, List<Block> dragged) => (stack.X, stack.Y);

        // =================================================================
        // Painting
        // =================================================================

        public void PaintScene(SKCanvas canvas, BlockProgram program, SceneLayout layout, PaintContext context)
        {
            var scene = (LineScene)layout;
            const float Far = 100000;

            using (var paper = new SKPaint { Color = PaperColor })
                canvas.DrawRect(new SKRect(-Far, -Far, scene.CodeX - 20, Far), paper);
            using (var code = new SKPaint { Color = CodeBg })
                canvas.DrawRect(new SKRect(scene.CodeX - 20, -Far, scene.CodeX + CodeW + Far, Far), code);
            using (var margin = new SKPaint { Color = Margin, StrokeWidth = 2 })
                canvas.DrawLine(X0 - 18, -Far, X0 - 18, Far, margin);
            using (var csharp = new SKPaint { Color = CodeMuted, IsAntialias = true })
                canvas.DrawText("C#", scene.CodeX + CodeW - 30, -14, SKTextAlign.Right, _style.Small, csharp);

            using var rule = new SKPaint { Color = Rule, StrokeWidth = 1 };
            using var number = new SKPaint { Color = new SKColor(140, 132, 118), IsAntialias = true };
            int n = 1;
            foreach (var row in scene.Rows)
            {
                canvas.DrawLine(-Far, row.Top + RowH, scene.CodeX - 20, row.Top + RowH, rule);
                canvas.DrawText(n++.ToString(), X0 - 30, BlockStyle.Baseline(_style.Mono, row.Top + RowH / 2), SKTextAlign.Right, _style.Mono, number);

                if (row.Block is { } b)
                {
                    bool lit = row.Kind == RowKind.Header && context.Lit.Contains(b.Id);
                    bool selected = context.Selected == b.Id;
                    if (selected || lit)
                    {
                        // The sentence and its C# light up together: yellow on the
                        // paper, a lighter stripe (and the same marker) on the dark code.
                        using var fill = new SKPaint { Color = BlockStyle.Glow.WithAlpha(selected ? (byte)110 : (byte)70) };
                        canvas.DrawRect(new SKRect(X0 - 22, row.Top, scene.CodeX - 20, row.Top + RowH), fill);
                        using var codeFill = new SKPaint { Color = SKColors.White.WithAlpha(selected ? (byte)34 : (byte)22) };
                        canvas.DrawRect(new SKRect(scene.CodeX - 20, row.Top, scene.CodeX + CodeW + 2000, row.Top + RowH), codeFill);
                        using var mark = new SKPaint { Color = selected ? new SKColor(232, 180, 0) : BlockStyle.CategoryColor(b.Def.Category) };
                        canvas.DrawRect(new SKRect(X0 - 22, row.Top, X0 - 18, row.Top + RowH), mark);
                        canvas.DrawRect(new SKRect(scene.CodeX - 20, row.Top, scene.CodeX - 16, row.Top + RowH), mark);
                    }
                }

                byte alpha = row.Loose ? (byte)140 : (byte)255;
                float x = X0 + row.Depth * Indent;
                switch (row.Kind)
                {
                    case RowKind.Header:
                        PaintSentence(canvas, row.Block!, x, row.Top, alpha);
                        break;
                    case RowKind.End:
                    {
                        using var muted = new SKPaint { Color = BlockStyle.Muted.WithAlpha(alpha), IsAntialias = true };
                        string text = row.Block!.Type == BlockCatalog.If ? Loc.Tr("fin de si", "end of if") : Loc.Tr("fin de repetir", "end of repeat");
                        canvas.DrawText(text, x + 4, BlockStyle.Baseline(_style.TextLight, row.Top + RowH / 2), SKTextAlign.Left, _style.TextLight, muted);
                        break;
                    }
                    case RowKind.Empty:
                    {
                        var spot = new SKRect(x, row.Top + 9, x + 250, row.Top + RowH - 9);
                        using var dash = new SKPaint
                        {
                            Color = new SKColor(201, 193, 178, alpha), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2,
                            PathEffect = SKPathEffect.CreateDash(new[] { 6f, 5f }, 0),
                        };
                        canvas.DrawRoundRect(spot, 10, 10, dash);
                        using var muted = new SKPaint { Color = BlockStyle.Muted.WithAlpha(alpha), IsAntialias = true };
                        canvas.DrawText(Loc.Tr("soltá frases acá", "drop sentences here"), spot.Left + 14, BlockStyle.Baseline(_style.Small, spot.MidY), SKTextAlign.Left, _style.Small, muted);
                        break;
                    }
                }

                PaintCode(canvas, row.Code, scene.CodeX, row.Top, row.Loose);
            }
        }

        private void PaintCode(SKCanvas canvas, string code, float codeX, float top, bool comment)
        {
            if (code.Length == 0)
                return;
            var lines = code.Split('\n');
            using var ink = new SKPaint { Color = comment ? CodeMuted : CodeInk, IsAntialias = true };
            float lineH = 17;
            float first = top + RowH / 2 - (lines.Length - 1) * lineH / 2;
            for (int i = 0; i < lines.Length; i++)
                canvas.DrawText(lines[i], codeX, BlockStyle.Baseline(_style.Mono, first + i * lineH), SKTextAlign.Left, _style.Mono, ink);
        }

        private void PaintSentence(SKCanvas canvas, Block b, float x, float top, byte alpha)
        {
            var def = b.Def;
            var color = BlockStyle.CategoryColor(def.Category);
            var deep = BlockStyle.Darker(color, 0.55f).WithAlpha(alpha);
            float mid = top + RowH / 2;
            BlockIcons.Draw(canvas, def.Type, new SKRect(x, mid - 11, x + 22, mid + 11), deep, 2.4f);

            var (parts, _) = Sentence(b);
            using var ink = new SKPaint { Color = (def.Shape == BlockShape.Hat ? deep : BlockStyle.Ink).WithAlpha(alpha), IsAntialias = true };
            foreach (var p in parts)
            {
                if (p.Text is not null)
                {
                    canvas.DrawText(p.Text, x + p.X, BlockStyle.Baseline(_style.Text, mid), SKTextAlign.Left, _style.Text, ink);
                    continue;
                }
                var chip = new SKRect(x + p.X, mid - ChipH / 2, x + p.X + p.W, mid + ChipH / 2);
                PaintChip(canvas, b, p.Slot!, chip, alpha);
            }
        }

        private void PaintChip(SKCanvas canvas, Block b, string slot, SKRect chip, byte alpha)
        {
            var color = BlockStyle.CategoryColor(b.Def.Category);
            var choice = SlotChoices.Get(b.Def.Slot(slot).Kind, b.Value(slot));
            using (var bg = new SKPaint { Color = BlockStyle.Tint(color, 0.8f).WithAlpha(alpha), IsAntialias = true })
                canvas.DrawRoundRect(chip, 8, 8, bg);
            float tx = chip.MidX;
            if (choice.Swatch is { } sw)
            {
                using var swatch = new SKPaint { Color = new SKColor(sw.R, sw.G, sw.B, alpha), IsAntialias = true };
                canvas.DrawCircle(chip.Left + ChipPad + 8, chip.MidY, 8, swatch);
                using var ring = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(alpha / 5)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
                canvas.DrawCircle(chip.Left + ChipPad + 8, chip.MidY, 8, ring);
                tx += 11;
            }
            using var ink = new SKPaint { Color = BlockStyle.Darker(color, 0.5f).WithAlpha(alpha), IsAntialias = true };
            canvas.DrawText(choice.Label, tx, BlockStyle.Baseline(_style.Number, chip.MidY), SKTextAlign.Center, _style.Number, ink);
        }

        public void PaintLoose(SKCanvas canvas, List<BlockVisual> visuals, PaintContext context, byte alpha)
        {
            if (visuals.Count == 0)
                return;
            // A floating strip of paper with the sentences on it.
            float left = visuals.Min(v => v.Hit.Left) - 14, top = visuals.Min(v => v.Bounds.Top);
            float right = visuals.Max(v => v.Hit.Right) + 14, bottom = visuals.Max(v => v.Bounds.Bottom);
            var card = new SKRect(left, top, right, bottom);
            using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 45), ImageFilter = SKImageFilter.CreateBlur(10, 10) })
                canvas.DrawRoundRect(SKRect.Inflate(card, 2, 2), 12, 12, shadow);
            using (var paper = new SKPaint { Color = PaperColor.WithAlpha(alpha), IsAntialias = true })
                canvas.DrawRoundRect(card, 12, 12, paper);

            foreach (var v in visuals)
            {
                PaintSentence(canvas, v.Block, v.Hit.Left + 6, v.Hit.Top - 4, alpha);
                if (v.Block.Def.Shape == BlockShape.CBlock)
                {
                    using var muted = new SKPaint { Color = BlockStyle.Muted.WithAlpha(alpha), IsAntialias = true };
                    string text = v.Block.Type == BlockCatalog.If ? Loc.Tr("fin de si", "end of if") : Loc.Tr("fin de repetir", "end of repeat");
                    canvas.DrawText(text, v.Hit.Left + 10, BlockStyle.Baseline(_style.TextLight, v.Bounds.Bottom - RowH / 2), SKTextAlign.Left, _style.TextLight, muted);
                }
            }
        }

        public void PaintSnap(SKCanvas canvas, SnapTarget target, List<Block> dragged)
        {
            using var paint = new SKPaint { Color = new SKColor(232, 180, 0), IsAntialias = true, StrokeWidth = 4, StrokeCap = SKStrokeCap.Round };
            canvas.DrawLine(target.Point.X, target.Point.Y, target.Point.X + 320, target.Point.Y, paint);
            canvas.DrawCircle(target.Point.X, target.Point.Y, 7, paint);
        }

        // =================================================================
        // Palette and picker
        // =================================================================

        /// <summary>"avanzar … pasos": the sentence with its blanks still open.</summary>
        private static string ChipText(Block t)
        {
            var parts = t.Def.Parts().Select(p => p.Text?.Trim() ?? "…").Where(s => s.Length > 0);
            return string.Join(" ", parts);
        }

        public float PaletteWidth(IReadOnlyList<Block> templates) =>
            templates.Select(t => _style.Text.MeasureText(ChipText(t)) + 64).DefaultIfEmpty(200).Max() + 24;

        public List<(Block Template, SKRect Rect)> LayoutPalette(IReadOnlyList<Block> templates, float left, float top, float width)
        {
            var result = new List<(Block, SKRect)>();
            float y = top;
            foreach (var t in templates)
            {
                float w = _style.Text.MeasureText(ChipText(t)) + 64;
                result.Add((t, new SKRect(left + 12, y, left + 12 + w, y + 46)));
                y += 46 + 12;
            }
            return result;
        }

        public void PaintPaletteItem(SKCanvas canvas, Block template, SKRect rect)
        {
            var color = BlockStyle.CategoryColor(template.Def.Category);
            var deep = BlockStyle.Darker(color, 0.55f);
            using (var bg = new SKPaint { Color = SKColors.White, IsAntialias = true })
                canvas.DrawRoundRect(rect, rect.Height / 2, rect.Height / 2, bg);
            using (var border = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f })
                canvas.DrawRoundRect(SKRect.Inflate(rect, -1, -1), rect.Height / 2, rect.Height / 2, border);
            BlockIcons.Draw(canvas, template.Type, new SKRect(rect.Left + 16, rect.MidY - 10, rect.Left + 36, rect.MidY + 10), deep, 2.4f);
            using var ink = new SKPaint { Color = deep, IsAntialias = true };
            canvas.DrawText(ChipText(template), rect.Left + 46, BlockStyle.Baseline(_style.Text, rect.MidY), SKTextAlign.Left, _style.Text, ink);
        }

        public SKSize OptionSize(SlotKind kind, SlotChoice choice) =>
            kind == SlotKind.Color
                ? new SKSize(84, 76)
                : new SKSize(Math.Max(56, _style.Number.MeasureText(choice.Label) + 32), 48);

        public void PaintOption(SKCanvas canvas, SlotKind kind, SlotChoice choice, SKRect rect, bool selected, SKColor accent)
        {
            using (var bg = new SKPaint { Color = selected ? accent : new SKColor(244, 239, 230), IsAntialias = true })
                canvas.DrawRoundRect(rect, 10, 10, bg);
            using var ink = new SKPaint { Color = selected ? BlockStyle.Darker(accent, 0.35f) : BlockStyle.Ink, IsAntialias = true };
            if (choice.Swatch is { } sw)
            {
                using var swatch = new SKPaint { Color = new SKColor(sw.R, sw.G, sw.B), IsAntialias = true };
                canvas.DrawCircle(rect.MidX, rect.Top + 26, 16, swatch);
                using var ring = new SKPaint { Color = new SKColor(0, 0, 0, 50), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
                canvas.DrawCircle(rect.MidX, rect.Top + 26, 16, ring);
                canvas.DrawText(choice.Label, rect.MidX, rect.Bottom - 10, SKTextAlign.Center, _style.Small, ink);
                return;
            }
            canvas.DrawText(choice.Label, rect.MidX, BlockStyle.Baseline(_style.Number, rect.MidY), SKTextAlign.Center, _style.Number, ink);
        }

        public void Dispose() => _style.Dispose();
    }
}
