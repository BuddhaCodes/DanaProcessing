using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// Fonts and colors shared by both block views. Everything is big on
    /// purpose: it's made for children with a mouse or a finger.
    /// </summary>
    internal sealed class BlockStyle : IDisposable
    {
        public static readonly SKColor Ink = new(43, 38, 32);
        public static readonly SKColor Muted = new(110, 101, 90);
        public static readonly SKColor Paper = new(255, 252, 245);
        public static readonly SKColor Glow = new(255, 217, 90);

        public SKFont Text { get; }
        public SKFont TextLight { get; }
        public SKFont Small { get; }
        public SKFont Number { get; }
        public SKFont Mono { get; }
        public SKFont MonoBold { get; }

        /// <summary>
        /// Fichas use Andika (drawn for children learning to read: a single-
        /// story "a", an "l" with a tail, no I/l/1 mix-ups); Renglones use
        /// Atkinson Hyperlegible, made for low-vision readers and a little more
        /// grown-up. The C# is IBM Plex Mono. All three ship inside the IDE
        /// (SIL Open Font License), so every computer shows the same letters.
        /// </summary>
        public BlockStyle(BlockView view = BlockView.Tiles)
        {
            bool kids = view == BlockView.Tiles;
            var regular = kids ? BundledFonts.AndikaRegular : BundledFonts.AtkinsonRegular;
            var bold = kids ? BundledFonts.AndikaBold : BundledFonts.AtkinsonBold;
            Text = Font(kids ? bold : regular, 17);
            TextLight = Font(regular, 15);
            Small = Font(kids ? bold : regular, kids ? 12.5f : 12);
            Number = Font(bold, 15.5f);
            Mono = Font(BundledFonts.PlexMono, 13);
            MonoBold = Font(BundledFonts.PlexMonoSemiBold, 13);
        }

        private static SKFont Font(SKTypeface face, float size) =>
            new(face, size) { Edging = SKFontEdging.Antialias, Subpixel = true, Hinting = SKFontHinting.Slight };

        public static float Baseline(SKFont font, float middle)
        {
            var m = font.Metrics;
            return middle - (m.Ascent + m.Descent) / 2;
        }

        public static SKColor CategoryColor(BlockCategory c)
        {
            var (r, g, b) = BlockCatalog.CategoryColor(c);
            return new SKColor(r, g, b);
        }

        /// <summary>A darker shade, for the solid "edge" under a tile and for text on a pale tint.</summary>
        public static SKColor Darker(SKColor c, float k = 0.72f) =>
            new((byte)(c.Red * k), (byte)(c.Green * k), (byte)(c.Blue * k), c.Alpha);

        /// <summary>A pale tint of a category color (trays, value chips).</summary>
        public static SKColor Tint(SKColor c, float k = 0.84f) =>
            new((byte)(c.Red + (255 - c.Red) * k), (byte)(c.Green + (255 - c.Green) * k), (byte)(c.Blue + (255 - c.Blue) * k), c.Alpha);

        public void Dispose()
        {
            Text.Dispose();
            TextLight.Dispose();
            Small.Dispose();
            Number.Dispose();
            Mono.Dispose();
            MonoBold.Dispose();
        }
    }

    /// <summary>
    /// The typefaces embedded in the IDE (Theme/Fonts). Loaded once and kept
    /// for the life of the app -- fonts share them, so they're never disposed.
    /// If a resource is somehow missing, the system's default face stands in.
    /// </summary>
    internal static class BundledFonts
    {
        private static readonly Lazy<SKTypeface> _andikaRegular = Load("Andika-Regular.ttf");
        private static readonly Lazy<SKTypeface> _andikaBold = Load("Andika-Bold.ttf");
        private static readonly Lazy<SKTypeface> _atkinsonRegular = Load("AtkinsonHyperlegible-Regular.ttf");
        private static readonly Lazy<SKTypeface> _atkinsonBold = Load("AtkinsonHyperlegible-Bold.ttf");
        private static readonly Lazy<SKTypeface> _plexMono = Load("IBMPlexMono-Regular.ttf");
        private static readonly Lazy<SKTypeface> _plexMonoSemiBold = Load("IBMPlexMono-SemiBold.ttf");

        public static SKTypeface AndikaRegular => _andikaRegular.Value;
        public static SKTypeface AndikaBold => _andikaBold.Value;
        public static SKTypeface AtkinsonRegular => _atkinsonRegular.Value;
        public static SKTypeface AtkinsonBold => _atkinsonBold.Value;
        public static SKTypeface PlexMono => _plexMono.Value;
        public static SKTypeface PlexMonoSemiBold => _plexMonoSemiBold.Value;

        private static Lazy<SKTypeface> Load(string file) => new(() =>
        {
            using var stream = typeof(BundledFonts).Assembly.GetManifestResourceStream("DanaProcessing.Ide.Fonts." + file);
            if (stream == null)
                return SKTypeface.Default;
            using var data = SKData.Create(stream);
            return SKTypeface.FromData(data) ?? SKTypeface.Default;
        });
    }

    /// <summary>
    /// Line icons for every block and category, as SVG path data on a 24x24
    /// grid -- drawn by Skia, so they look the same on every computer
    /// (emoji depend on the fonts each system happens to have).
    /// </summary>
    internal static class BlockIcons
    {
        private sealed record Icon(string Path, bool Filled = false);

        private const string Play = "M7 4.5v15l12.5-7.5z";
        private const string Up = "M12 20V4M5 11l7-7 7 7";
        private const string Down = "M12 4v16M5 13l7 7 7-7";
        private const string TurnRight = "M20 12a8 8 0 1 1-2.6-5.9M20 4v5h-5";
        private const string TurnLeft = "M4 12a8 8 0 1 0 2.6-5.9M4 4v5h5";
        private const string Repeat = "M17 3l3 3-3 3M4 11V9a3 3 0 0 1 3-3h13M7 21l-3-3 3-3M20 13v2a3 3 0 0 1-3 3H4";
        private const string Note = "M9 18V5l10-2v13M9 18a3 3 0 1 1-6 0a3 3 0 1 1 6 0M19 16a3 3 0 1 1-6 0a3 3 0 1 1 6 0";
        private const string Star = "M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1-4.4-4.3 6.1-.9z";

        private static readonly Dictionary<string, Icon> ByType = new()
        {
            [BlockCatalog.Start] = new(Play, Filled: true),
            [BlockCatalog.Forever] = new("M7 8a4 4 0 1 0 0 8c2.5 0 3.5-2 5-4s2.5-4 5-4a4 4 0 1 1 0 8c-2.5 0-3.5-2-5-4s-2.5-4-5-4z"),
            [BlockCatalog.Click] = new("M6 3l13 7-6 1.6L10.5 18z"),
            [BlockCatalog.Key] = new("M3 7h18v10H3zM7 11h.01M11 11h.01M15 11h.01M8 14h8"),
            [BlockCatalog.MusicLoop] = new(Note),
            [BlockCatalog.Forward] = new(Up),
            [BlockCatalog.Back] = new(Down),
            [BlockCatalog.Right] = new(TurnRight),
            [BlockCatalog.Left] = new(TurnLeft),
            [BlockCatalog.PenUp] = new("M14 5l5 5-9 9H5v-5zM17 21V15M14 18l3-3 3 3"),
            [BlockCatalog.PenDown] = new("M14 3l5 5-9 9H5v-5zM3 21h18"),
            [BlockCatalog.PenColor] = new("M12 3c-4 5-6 8-6 11a6 6 0 0 0 12 0c0-3-2-6-6-11z"),
            [BlockCatalog.PenSize] = new("M4 6h16M4 12h16M4 18h16"),
            [BlockCatalog.Home] = new("M3 11l9-8 9 8M5 10v10h14V10M10 20v-5h4v5"),
            [BlockCatalog.Background] = new("M3 5h18v14H3zM3 15l5-5 5 5 3-3 5 5"),
            [BlockCatalog.Circle] = new("M12 4a8 8 0 1 0 0 16a8 8 0 1 0 0-16"),
            [BlockCatalog.Square] = new("M5 5h14v14H5z"),
            [BlockCatalog.Repeat] = new(Repeat),
            [BlockCatalog.If] = new("M12 3l9 9-9 9-9-9z"),
            [BlockCatalog.Note] = new("M9 18V5l10-2v13M9 18a3 3 0 1 1-6 0a3 3 0 1 1 6 0M19 16a3 3 0 1 1-6 0a3 3 0 1 1 6 0"),
            [BlockCatalog.Drum] = new("M4 9c0-2.5 16-2.5 16 0v7c0 2.5-16 2.5-16 0zM4 9c0 2.5 16 2.5 16 0M8 3l3 5M16 3l-3 5"),
            [BlockCatalog.Wait] = new("M7 3h10M7 21h10M8 3c0 5 8 6 8 9s-8 4-8 9M16 3c0 5-8 6-8 9s8 4 8 9"),
            [BlockCatalog.Instrument] = new("M3 12c2.5-6 4.5 6 7 0s4.5 6 7 0 3 3 4 0"),
            [BlockCatalog.ScoreReset] = new("M4 4v6h6M4.5 10a8 8 0 1 1 1.9 7.5"),
            [BlockCatalog.ScoreAdd] = new(Star),
            [BlockCatalog.VarSet] = new("M4 8l8-4 8 4v8l-8 4-8-4z M4 8l8 4 8-4 M12 12v8"),
            [BlockCatalog.VarChange] = new("M4 8l8-4 8 4v8l-8 4-8-4z M9 12h6 M12 9v6"),
            [BlockCatalog.VarShow] = new("M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12zM12 9a3 3 0 1 0 0 6a3 3 0 1 0 0-6"),
            [BlockCatalog.Define] = new("M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z M18 15l.8 2.2L21 18l-2.2.8L18 21l-.8-2.2L15 18l2.2-.8z"),
            [BlockCatalog.Call] = new("M4 20L15 9 M13 7l4 4 M18 3v3 M16.5 4.5h3 M20 9v2 M19 10h2 M10 3v2 M9 4h2"),
            [BlockCatalog.ScoreShow] = new("M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12zM12 9a3 3 0 1 0 0 6a3 3 0 1 0 0-6"),
        };

        private static readonly Dictionary<string, SKPath> Paths = new();

        private static SKPath PathFor(string data)
        {
            lock (Paths)
            {
                if (!Paths.TryGetValue(data, out var path))
                {
                    path = SKPath.ParseSvgPathData(data);
                    Paths[data] = path;
                }
                return path;
            }
        }

        /// <summary>Draws the icon of a block type inside <paramref name="rect"/>.</summary>
        public static void Draw(SKCanvas canvas, string type, SKRect rect, SKColor color, float weight = 2.4f)
        {
            if (!ByType.TryGetValue(type, out var icon))
                return;
            DrawPath(canvas, icon.Path, icon.Filled, rect, color, weight);
        }

        /// <summary>Draws a category's icon (the turtle is drawn, not a path).</summary>
        public static void DrawCategory(SKCanvas canvas, BlockCategory category, SKRect rect, SKColor color)
        {
            switch (category)
            {
                case BlockCategory.Turtle: DrawTurtle(canvas, rect, white: color == SKColors.White); break;
                case BlockCategory.Events: DrawPath(canvas, Play, true, rect, color, 2.4f); break;
                case BlockCategory.Draw: DrawPath(canvas, "M9 3a6 6 0 1 0 0 12a6 6 0 1 0 0-12M12 12h9v9h-9z", false, rect, color, 2.2f); break;
                case BlockCategory.Control: DrawPath(canvas, Repeat, false, rect, color, 2.4f); break;
                case BlockCategory.Music: DrawPath(canvas, Note, false, rect, color, 2.2f); break;
                case BlockCategory.Score: DrawPath(canvas, Star, false, rect, color, 2.2f); break;
                case BlockCategory.Variables: DrawPath(canvas, "M4 8l8-4 8 4v8l-8 4-8-4z M4 8l8 4 8-4 M12 12v8", false, rect, color, 2.2f); break;
                case BlockCategory.MyBlocks: DrawPath(canvas, "M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z M18 15l.8 2.2L21 18l-2.2.8L18 21l-.8-2.2L15 18l2.2-.8z", false, rect, color, 2.2f); break;
            }
        }

        public static void DrawPath(SKCanvas canvas, string data, bool filled, SKRect rect, SKColor color, float weight)
        {
            var path = PathFor(data);
            canvas.Save();
            canvas.Translate(rect.Left, rect.Top);
            canvas.Scale(rect.Width / 24f, rect.Height / 24f);
            using var paint = new SKPaint
            {
                Color = color, IsAntialias = true,
                Style = filled ? SKPaintStyle.Fill : SKPaintStyle.Stroke,
                StrokeWidth = weight, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
            };
            canvas.DrawPath(path, paint);
            canvas.Restore();
        }

        /// <summary>The turtle seen from above: the same little animal the sketch draws (all white on a colored button).</summary>
        public static void DrawTurtle(SKCanvas canvas, SKRect rect, bool white = false)
        {
            float s = Math.Min(rect.Width, rect.Height) / 40f;
            float cx = rect.MidX, cy = rect.MidY + 2 * s;
            using var legs = new SKPaint { Color = white ? SKColors.White : new SKColor(120, 190, 120), IsAntialias = true };
            using var shell = new SKPaint { Color = white ? SKColors.White : new SKColor(62, 155, 98), IsAntialias = true };
            using var plate = new SKPaint { Color = white ? new SKColor(92, 196, 126) : new SKColor(124, 197, 150), IsAntialias = true };
            foreach (var (dx, dy) in new[] { (-11f, -8f), (11f, -8f), (-11f, 9f), (11f, 9f) })
                canvas.DrawOval(cx + dx * s, cy + dy * s, 4.5f * s, 3.5f * s, legs);
            canvas.DrawOval(cx, cy - 15 * s, 5 * s, 5.5f * s, legs);
            canvas.DrawOval(cx, cy, 12 * s, 13 * s, shell);
            canvas.DrawOval(cx, cy, 6 * s, 7 * s, plate);
        }
    }

    /// <summary>
    /// Draws a slot's value as a picture, for children who don't read yet:
    /// a step is a bar as long as the walk, an angle is a slice of a circle,
    /// a count is dots, a color is the color, a place is a dot on a little
    /// canvas. Numbers stay small beside them, so they're learned on the way.
    /// </summary>
    internal static class ValueGlyph
    {
        public static void Paint(SKCanvas canvas, BlockStyle style, SlotKind kind, string value, SKRect r, SKColor ink)
        {
            using var fill = new SKPaint { Color = ink, IsAntialias = true };
            using var faint = new SKPaint { Color = ink.WithAlpha((byte)(ink.Alpha * 0.35f)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
            using var line = new SKPaint { Color = ink, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.4f, StrokeCap = SKStrokeCap.Round };
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n);
            var choice = SlotChoices.Get(kind, value);

            // A variable or "al azar" where a number goes: a word in a pill
            // (a die for random), the same in every kind of slot.
            if (SlotChoices.IsSpecial(value) || kind is SlotKind.Variable or SlotKind.Function)
            {
                var pill = new SKRect(r.Left + 2, r.MidY - 13, r.Right - 2, r.MidY + 13);
                using (var bg = new SKPaint { Color = SKColors.White.WithAlpha((byte)(ink.Alpha * 0.92f)), IsAntialias = true })
                    canvas.DrawRoundRect(pill, 13, 13, bg);
                if (value == "random")
                {
                    var die = new SKRect(pill.MidX - 11, pill.MidY - 11, pill.MidX + 11, pill.MidY + 11);
                    using var dieInk = new SKPaint { Color = BlockStyle.Ink, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
                    canvas.DrawRoundRect(die, 4, 4, dieInk);
                    using var dot = new SKPaint { Color = BlockStyle.Ink, IsAntialias = true };
                    canvas.DrawCircle(die.Left + 6, die.Top + 6, 2.2f, dot);
                    canvas.DrawCircle(die.MidX, die.MidY, 2.2f, dot);
                    canvas.DrawCircle(die.Right - 6, die.Bottom - 6, 2.2f, dot);
                }
                else
                {
                    var font = style.Small;
                    string label = choice.Label;
                    while (label.Length > 3 && font.MeasureText(label) > pill.Width - 10)
                        label = label[..^2] + "…";
                    Text(canvas, font, label, pill.MidX, pill.MidY, BlockStyle.Ink);
                }
                return;
            }

            switch (kind)
            {
                case SlotKind.Steps:
                {
                    float maxW = r.Width - 4;
                    float w = Math.Max(6, maxW * Math.Min(1, n / 200f));
                    var bar = new SKRect(r.MidX - maxW / 2, r.Top + 4, r.MidX - maxW / 2 + w, r.Top + 14);
                    using (var track = new SKPaint { Color = ink.WithAlpha((byte)(ink.Alpha * 0.25f)), IsAntialias = true })
                        canvas.DrawRoundRect(new SKRect(bar.Left, bar.Top, bar.Left + maxW, bar.Bottom), 5, 5, track);
                    canvas.DrawRoundRect(bar, 5, 5, fill);
                    Text(canvas, style.Number, value, r.MidX, r.Bottom - 9, ink);
                    break;
                }
                case SlotKind.Angle:
                {
                    float rad = Math.Min(r.Height, 34) / 2 - 1;
                    float cx = r.MidX - 18, cy = r.MidY;
                    var oval = new SKRect(cx - rad, cy - rad, cx + rad, cy + rad);
                    canvas.DrawOval(oval, faint);
                    using (var wedge = new SKPath())
                    {
                        wedge.MoveTo(cx, cy);
                        wedge.ArcTo(oval, -90, n, false);
                        wedge.Close();
                        canvas.DrawPath(wedge, fill);
                    }
                    Text(canvas, style.Number, value + "°", cx + rad + 20, cy, ink);
                    break;
                }
                case SlotKind.Count:
                {
                    int count = (int)n;
                    if (count <= 6)
                    {
                        float d = 10, gap = 5, total = count * d + (count - 1) * gap;
                        for (int i = 0; i < count; i++)
                            canvas.DrawCircle(r.MidX - total / 2 + d / 2 + i * (d + gap), r.MidY, d / 2, fill);
                    }
                    else
                    {
                        Text(canvas, style.Number, "×" + count, r.MidX, r.MidY, ink);
                    }
                    break;
                }
                case SlotKind.Color:
                {
                    var (cr, cg, cb) = choice.Swatch ?? (0, 0, 0);
                    using var swatch = new SKPaint { Color = new SKColor(cr, cg, cb), IsAntialias = true };
                    float rad = Math.Min(r.Height, 30) / 2;
                    canvas.DrawCircle(r.MidX, r.MidY, rad, swatch);
                    using var ring = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 };
                    canvas.DrawCircle(r.MidX, r.MidY, rad, ring);
                    using var edge = new SKPaint { Color = new SKColor(0, 0, 0, 40), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
                    canvas.DrawCircle(r.MidX, r.MidY, rad + 1.5f, edge);
                    break;
                }
                case SlotKind.Thickness:
                {
                    using var thick = new SKPaint { Color = ink, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Clamp(n, 1.5f, 12), StrokeCap = SKStrokeCap.Round };
                    canvas.DrawLine(r.Left + 8, r.MidY, r.Right - 8, r.MidY, thick);
                    break;
                }
                case SlotKind.Size:
                {
                    float d = n switch { <= 20 => 8, <= 50 => 14, <= 100 => 21, _ => 29 };
                    canvas.DrawCircle(r.MidX, r.MidY, Math.Min(d, r.Height - 4) / 2, fill);
                    break;
                }
                case SlotKind.Place:
                {
                    var frame = new SKRect(r.MidX - 19, r.MidY - 13, r.MidX + 19, r.MidY + 13);
                    canvas.DrawRoundRect(frame, 4, 4, faint);
                    switch (value)
                    {
                        case "center": canvas.DrawCircle(frame.MidX, frame.MidY, 4, fill); break;
                        case "top": canvas.DrawCircle(frame.MidX, frame.Top + 6, 4, fill); break;
                        case "bottom": canvas.DrawCircle(frame.MidX, frame.Bottom - 6, 4, fill); break;
                        case "random":
                            canvas.DrawCircle(frame.Left + 9, frame.Top + 7, 3, fill);
                            canvas.DrawCircle(frame.MidX, frame.MidY, 3, fill);
                            canvas.DrawCircle(frame.Right - 9, frame.Bottom - 7, 3, fill);
                            break;
                        case "mouse":
                            BlockIcons.DrawPath(canvas, "M6 3l13 7-6 1.6L10.5 18z", true, new SKRect(frame.MidX - 9, frame.MidY - 9, frame.MidX + 9, frame.MidY + 9), ink, 2);
                            break;
                    }
                    break;
                }
                case SlotKind.Note:
                {
                    var key = new SKRect(r.MidX - 22, r.Top + 2, r.MidX + 22, r.Bottom - 2);
                    using (var white = new SKPaint { Color = SKColors.White, IsAntialias = true })
                        canvas.DrawRoundRect(key, 6, 6, white);
                    canvas.DrawRoundRect(key, 6, 6, faint);
                    Text(canvas, style.Number, choice.Label, key.MidX, key.MidY, BlockStyle.Ink);
                    break;
                }
                case SlotKind.Drum:
                {
                    var box = new SKRect(r.MidX - 14, r.MidY - 14, r.MidX + 14, r.MidY + 14);
                    switch (value)
                    {
                        case "kick": canvas.DrawCircle(box.MidX, box.MidY, 13, fill); break;
                        case "snare": canvas.DrawOval(box.MidX, box.MidY, 14, 7, fill); canvas.DrawLine(box.Left, box.MidY + 9, box.Right, box.MidY + 9, line); break;
                        default: canvas.DrawLine(box.MidX, box.Top, box.MidX, box.Bottom, line); canvas.DrawOval(box.MidX, box.Top + 7, 14, 3.5f, fill); break;
                    }
                    break;
                }
                case SlotKind.Beats:
                {
                    float rad = Math.Min(r.Height, 30) / 2 - 1;
                    int circles = n >= 2 ? 2 : 1;
                    float part = n >= 1 ? 1 : n;
                    for (int i = 0; i < circles; i++)
                    {
                        float cx = r.MidX + (circles == 1 ? 0 : (i == 0 ? -rad - 3 : rad + 3));
                        var oval = new SKRect(cx - rad, r.MidY - rad, cx + rad, r.MidY + rad);
                        canvas.DrawOval(oval, faint);
                        using var slice = new SKPath();
                        slice.MoveTo(cx, r.MidY);
                        slice.ArcTo(oval, -90, 360 * part - 0.01f, false);
                        slice.Close();
                        canvas.DrawPath(slice, fill);
                    }
                    break;
                }
                case SlotKind.Condition:
                {
                    var box = new SKRect(r.MidX - 13, r.MidY - 13, r.MidX + 13, r.MidY + 13);
                    switch (value)
                    {
                        case "mouse": BlockIcons.DrawPath(canvas, "M12 3a6 6 0 0 0-6 6v6a6 6 0 0 0 12 0V9a6 6 0 0 0-6-6zM12 3v6", false, box, ink, 2.2f); break;
                        case "key": BlockIcons.DrawPath(canvas, "M4 6h16v12H4zM8 14h8", false, box, ink, 2.2f); break;
                        case "coin": canvas.DrawCircle(box.MidX, box.MidY, 12, faint); Text(canvas, style.Number, "?", box.MidX, box.MidY, ink); break;
                        case "right": BlockIcons.DrawPath(canvas, "M4 12h16M14 6l6 6-6 6", false, box, ink, 2.6f); break;
                        case "left": BlockIcons.DrawPath(canvas, "M20 12H4M10 6l-6 6 6 6", false, box, ink, 2.6f); break;
                        case "up": BlockIcons.DrawPath(canvas, "M12 20V4M6 10l6-6 6 6", false, box, ink, 2.6f); break;
                        case "down": BlockIcons.DrawPath(canvas, "M12 4v16M6 14l6 6 6-6", false, box, ink, 2.6f); break;
                        case "space": BlockIcons.DrawPath(canvas, "M3 10v4h18v-4", false, box, ink, 2.6f); break;
                        case "mouseRight":
                        case "mouseTop":
                        {
                            var frame = new SKRect(r.MidX - 19, r.MidY - 13, r.MidX + 19, r.MidY + 13);
                            canvas.DrawRoundRect(frame, 4, 4, faint);
                            var half = value == "mouseRight" ? new SKRect(frame.MidX, frame.Top, frame.Right, frame.Bottom) : new SKRect(frame.Left, frame.Top, frame.Right, frame.MidY);
                            using (var shade = new SKPaint { Color = ink.WithAlpha((byte)(ink.Alpha * 0.45f)), IsAntialias = true })
                                canvas.DrawRoundRect(half, 4, 4, shade);
                            break;
                        }
                        case "counter10":
                            Text(canvas, style.Number, "≥10", box.MidX, box.MidY, ink);
                            break;
                        default:
                            BlockIcons.DrawPath(canvas, "M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1-4.4-4.3 6.1-.9z", false, new SKRect(box.Left - 8, box.Top, box.Right - 8, box.Bottom), ink, 2);
                            Text(canvas, style.Number, "10", box.Right + 8, box.MidY, ink);
                            break;
                    }
                    break;
                }
                default:
                    Text(canvas, style.Small, choice.Label, r.MidX, r.MidY, ink);
                    break;
            }
        }

        private static void Text(SKCanvas canvas, SKFont font, string text, float x, float middle, SKColor color)
        {
            using var paint = new SKPaint { Color = color, IsAntialias = true };
            canvas.DrawText(text, x, BlockStyle.Baseline(font, middle), SKTextAlign.Center, font, paint);
        }
    }
}
