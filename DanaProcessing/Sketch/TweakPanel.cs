using System.Reflection;
using SkiaSharp;

namespace DanaProcessing
{
    /// <summary>
    /// The slider panel behind <see cref="TweakAttribute"/>. Drawn by the engine
    /// itself right after Draw() (see Sketch.RenderFrame) and fed mouse events
    /// before the sketch sees them (see Sketch.Input.cs), so it works the same in
    /// the IDE, in reels and in exported apps without any host changes.
    ///
    /// Hot-reload safe on purpose: SketchHotReload copies every base-class field,
    /// this panel included, onto the freshly compiled instance. So the panel
    /// never stores the sketch -- every call gets it as a parameter -- and it
    /// rebuilds its rows whenever the sketch's runtime Type changes (FieldInfos
    /// from the old compiled type can't read the new one). What carries over is
    /// only what should: folded/unfolded, visibility and auto ranges.
    /// </summary>
    internal sealed class TweakPanel
    {
        private const float PanelWidth = 236f;
        private const float Margin = 10f;
        private const float HeaderHeight = 26f;
        private const float RowHeight = 36f;
        private const float Pad = 12f;

        private enum RowKind { Float, Double, Int, Bool, ColorR, ColorG, ColorB }

        private sealed record Row(FieldInfo Field, RowKind Kind, string Label, float Min, float Max);

        private readonly List<Row> _rows = new();
        private readonly Dictionary<string, (float Min, float Max)> _autoRanges = new();
        private readonly HashSet<string> _touched = new();
        private Type? _builtFor;

        private int _dragRow = -1;
        private bool _captured;
        private bool _swallowClick;

        public bool Visible { get; set; } = true;
        public bool Collapsed { get; set; }

        // Paints are created once and reused every frame.
        private readonly SKPaint _bg = new() { Color = new SKColor(18, 20, 28, 225), IsAntialias = true };
        private readonly SKPaint _hover = new() { Color = new SKColor(255, 255, 255, 18), IsAntialias = true };
        private readonly SKPaint _track = new() { Color = new SKColor(255, 255, 255, 40), IsAntialias = true };
        private readonly SKPaint _fill = new() { Color = new SKColor(255, 170, 90), IsAntialias = true };
        private readonly SKPaint _knob = new() { Color = SKColors.White, IsAntialias = true };
        private readonly SKPaint _box = new() { Color = new SKColor(255, 255, 255, 140), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        private readonly SKPaint _title = new() { Color = new SKColor(255, 200, 140), IsAntialias = true, TextSize = 12, FakeBoldText = true };
        private readonly SKPaint _label = new() { Color = new SKColor(225, 228, 235), IsAntialias = true, TextSize = 12 };
        private readonly SKPaint _value = new() { Color = new SKColor(160, 166, 180), IsAntialias = true, TextSize = 12 };

        // ---------------------------------------------------------------
        // Rows
        // ---------------------------------------------------------------

        /// <summary>True when there's something to show: visible and at least one [Tweak] field.</summary>
        public bool IsActive(Sketch sketch)
        {
            if (!Visible)
                return false;
            Build(sketch);
            return _rows.Count > 0;
        }

        private void Build(Sketch sketch)
        {
            var type = sketch.GetType();
            if (type == _builtFor)
                return;

            _builtFor = type;
            _rows.Clear();
            _dragRow = -1;
            _captured = false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var field in type.GetFields(flags))
            {
                var attr = field.GetCustomAttribute<TweakAttribute>();
                if (attr == null)
                    continue;

                string label = attr.Label ?? field.Name.TrimStart('_');
                var ft = field.FieldType;

                if (ft == typeof(bool))
                {
                    _rows.Add(new Row(field, RowKind.Bool, label, 0, 1));
                }
                else if (ft == typeof(Color))
                {
                    float min = attr.HasRange ? attr.Min : 0;
                    float max = attr.HasRange ? attr.Max : 255;
                    _rows.Add(new Row(field, RowKind.ColorR, label + " R", min, max));
                    _rows.Add(new Row(field, RowKind.ColorG, label + " G", min, max));
                    _rows.Add(new Row(field, RowKind.ColorB, label + " B", min, max));
                }
                else if (ft == typeof(float) || ft == typeof(double) || ft == typeof(int))
                {
                    var kind = ft == typeof(float) ? RowKind.Float : ft == typeof(double) ? RowKind.Double : RowKind.Int;
                    var (min, max) = attr.HasRange ? (attr.Min, attr.Max) : AutoRange(sketch, field, kind);
                    if (max < min)
                        (min, max) = (max, min);
                    _rows.Add(new Row(field, kind, label, min, max));
                }
                else
                {
                    DanaLogger.Warn($"[Tweak] on '{field.Name}' ignored: only float, double, int, bool and Color fields can be tweaked ({ft.Name} can't).");
                }
            }
        }

        /// <summary>
        /// 0..2x the starting value (-2x..2x if negative), remembered by field name
        /// so the slider doesn't jump around after a hot reload.
        /// </summary>
        private (float, float) AutoRange(Sketch sketch, FieldInfo field, RowKind kind)
        {
            if (_autoRanges.TryGetValue(field.Name, out var known))
                return known;

            float v = ReadNumber(sketch, field, kind);
            float m = MathF.Abs(v);
            if (m < 1e-6f)
                m = 1f;
            var range = v < 0 ? (-2f * m, 2f * m) : (0f, 2f * m);
            if (kind == RowKind.Int && range.Item2 - range.Item1 < 10)
                range = v < 0 ? (-10f, 10f) : (0f, 10f);

            _autoRanges[field.Name] = range;
            return range;
        }

        private static float ReadNumber(Sketch sketch, FieldInfo field, RowKind kind)
        {
            object? raw = field.GetValue(sketch);
            return kind switch
            {
                RowKind.Float => raw is float f ? f : 0f,
                RowKind.Double => raw is double d ? (float)d : 0f,
                RowKind.Int => raw is int i ? i : 0f,
                RowKind.Bool => raw is bool b && b ? 1f : 0f,
                RowKind.ColorR => raw is Color c ? c.R : 0f,
                RowKind.ColorG => raw is Color c2 ? c2.G : 0f,
                RowKind.ColorB => raw is Color c3 ? c3.B : 0f,
                _ => 0f,
            };
        }

        private static void WriteNumber(Sketch sketch, Row row, float value)
        {
            switch (row.Kind)
            {
                case RowKind.Float:
                    row.Field.SetValue(sketch, value);
                    break;
                case RowKind.Double:
                    row.Field.SetValue(sketch, (double)value);
                    break;
                case RowKind.Int:
                    row.Field.SetValue(sketch, (int)MathF.Round(value));
                    break;
                case RowKind.Bool:
                    row.Field.SetValue(sketch, value >= 0.5f);
                    break;
                case RowKind.ColorR:
                case RowKind.ColorG:
                case RowKind.ColorB:
                {
                    var c = row.Field.GetValue(sketch) is Color current ? current : new Color(0, 0, 0);
                    byte channel = (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
                    var next = row.Kind switch
                    {
                        RowKind.ColorR => new Color(channel, c.G, c.B, c.A),
                        RowKind.ColorG => new Color(c.R, channel, c.B, c.A),
                        _ => new Color(c.R, c.G, channel, c.A),
                    };
                    row.Field.SetValue(sketch, next);
                    break;
                }
            }
        }

        // ---------------------------------------------------------------
        // Geometry
        // ---------------------------------------------------------------

        private static float Left(Sketch sketch) => sketch.Width - PanelWidth - Margin;

        private float PanelHeight => HeaderHeight + (Collapsed ? 0 : _rows.Count * RowHeight + 6);

        private bool InPanel(Sketch sketch, float x, float y)
        {
            float left = Left(sketch);
            return x >= left && x <= left + PanelWidth && y >= Margin && y <= Margin + PanelHeight;
        }

        /// <summary>Row index under (x, y), -1 for the header, or -2 for none.</summary>
        private int HitRow(Sketch sketch, float x, float y)
        {
            if (!InPanel(sketch, x, y))
                return -2;
            float local = y - Margin;
            if (local < HeaderHeight)
                return -1;
            if (Collapsed)
                return -2;
            int row = (int)((local - HeaderHeight) / RowHeight);
            return row >= 0 && row < _rows.Count ? row : -2;
        }

        private static (float X0, float X1) Track(Sketch sketch)
        {
            float left = Left(sketch);
            return (left + Pad, left + PanelWidth - Pad);
        }

        private void SetFromMouse(Sketch sketch, int rowIndex, float mouseX)
        {
            var row = _rows[rowIndex];
            var (x0, x1) = Track(sketch);
            float t = Math.Clamp((mouseX - x0) / (x1 - x0), 0f, 1f);
            WriteNumber(sketch, row, row.Min + (row.Max - row.Min) * t);
            _touched.Add(row.Field.Name);
        }

        /// <summary>True once this field's slider/checkbox has been moved (survives hot reload, like the rest of the panel). See Sketch.GetTweakValues.</summary>
        public bool WasTouched(string fieldName) => _touched.Contains(fieldName);

        // ---------------------------------------------------------------
        // Input -- each returns true when the panel used the event, in which
        // case the sketch's own callback must NOT fire (otherwise dragging a
        // slider would also paint on the canvas underneath it).
        // ---------------------------------------------------------------

        public bool OnPress(Sketch sketch, float x, float y)
        {
            if (!IsActive(sketch))
                return false;

            int hit = HitRow(sketch, x, y);
            if (hit == -2)
                return InPanel(sketch, x, y) && Capture();

            if (hit == -1)
            {
                Collapsed = !Collapsed;
                return Capture();
            }

            var row = _rows[hit];
            if (row.Kind == RowKind.Bool)
            {
                WriteNumber(sketch, row, ReadNumber(sketch, row.Field, row.Kind) >= 0.5f ? 0f : 1f);
                _touched.Add(row.Field.Name);
            }
            else
            {
                _dragRow = hit;
                SetFromMouse(sketch, hit, x);
            }
            return Capture();
        }

        private bool Capture()
        {
            _captured = true;
            return true;
        }

        public bool OnMove(Sketch sketch, float x)
        {
            if (!_captured)
                return false;
            if (_dragRow >= 0 && _dragRow < _rows.Count && IsActive(sketch))
                SetFromMouse(sketch, _dragRow, x);
            return true;
        }

        public bool OnRelease()
        {
            if (!_captured)
                return false;
            _captured = false;
            _dragRow = -1;
            _swallowClick = true; // the host fires MouseClicked right after this
            return true;
        }

        public bool OnClick()
        {
            if (!_swallowClick)
                return false;
            _swallowClick = false;
            return true;
        }

        // ---------------------------------------------------------------
        // Drawing (raw Skia, so the sketch's own Fill/Stroke/TextSize state
        // is never touched)
        // ---------------------------------------------------------------

        public void Draw(Sketch sketch, SKCanvas canvas)
        {
            if (!IsActive(sketch))
                return;

            float left = Left(sketch);
            float top = Margin;
            canvas.DrawRoundRect(new SKRect(left, top, left + PanelWidth, top + PanelHeight), 8, 8, _bg);

            canvas.DrawText(Collapsed ? "+  TWEAK" : "-  TWEAK", left + Pad, top + 17, _title);
            string count = _rows.Count == 1 ? "1 value" : $"{_rows.Count} values";
            canvas.DrawText(count, left + PanelWidth - Pad - _value.MeasureText(count), top + 17, _value);

            if (Collapsed)
                return;

            int hover = _captured ? _dragRow : HitRow(sketch, sketch.MouseX, sketch.MouseY);
            var (x0, x1) = Track(sketch);

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                float rowTop = top + HeaderHeight + i * RowHeight;

                if (i == hover)
                    canvas.DrawRoundRect(new SKRect(left + 4, rowTop, left + PanelWidth - 4, rowTop + RowHeight), 6, 6, _hover);

                canvas.DrawText(row.Label, x0, rowTop + 14, _label);
                float v = ReadNumber(sketch, row.Field, row.Kind);

                if (row.Kind == RowKind.Bool)
                {
                    var box = new SKRect(x1 - 14, rowTop + 4, x1, rowTop + 18);
                    canvas.DrawRoundRect(box, 3, 3, _box);
                    if (v >= 0.5f)
                    {
                        box.Inflate(-3, -3);
                        canvas.DrawRoundRect(box, 2, 2, _fill);
                    }
                    continue;
                }

                string text = FormatValue(row.Kind, v);
                canvas.DrawText(text, x1 - _value.MeasureText(text), rowTop + 14, _value);

                float t = row.Max > row.Min ? Math.Clamp((v - row.Min) / (row.Max - row.Min), 0f, 1f) : 0f;
                float knobX = x0 + (x1 - x0) * t;
                float trackY = rowTop + 25;

                canvas.DrawRoundRect(new SKRect(x0, trackY - 2, x1, trackY + 2), 2, 2, _track);

                var fillColor = row.Kind switch
                {
                    RowKind.ColorR => new SKColor(240, 80, 80),
                    RowKind.ColorG => new SKColor(90, 210, 120),
                    RowKind.ColorB => new SKColor(90, 150, 255),
                    _ => new SKColor(255, 170, 90),
                };
                _fill.Color = fillColor;
                canvas.DrawRoundRect(new SKRect(x0, trackY - 2, knobX, trackY + 2), 2, 2, _fill);
                _fill.Color = new SKColor(255, 170, 90);

                canvas.DrawCircle(knobX, trackY, i == hover ? 7 : 6, _knob);
            }
        }

        private static string FormatValue(RowKind kind, float v)
        {
            if (kind == RowKind.Int || kind == RowKind.ColorR || kind == RowKind.ColorG || kind == RowKind.ColorB)
                return ((int)MathF.Round(v)).ToString();
            float a = MathF.Abs(v);
            return a >= 100 ? v.ToString("0") : a >= 10 ? v.ToString("0.0") : v.ToString("0.00");
        }
    }
}
