#pragma warning disable CS0618 // SkiaSharp 3 marks the SKPaint text API obsolete; it still works and keeps this file usable on 2.88 too.
using SkiaSharp;

namespace DanaProcessing.Reels
{
    /// <summary>Size and position of a code card, shared by every scene that "keeps" onto it so the card never jumps.</summary>
    internal sealed class CardLayout
    {
        public readonly float FontSize, CharW, LineH, Gutter, PadX, PadY, HeaderH;
        public readonly SKRect Card;
        public readonly float BodyTop, ViewH;
        public readonly int Digits;

        public CardLayout(ReelTimeline t, List<ReelLine> allLines)
        {
            float S = t.S;
            var area = t.ContentRect;
            PadX = 30 * S;
            PadY = 26 * S;
            HeaderH = 66 * S;

            int maxCols = Math.Max(24, allLines.Count == 0 ? 0 : allLines.Max(l => l.Text.Length));
            Digits = Math.Max(2, allLines.Count.ToString().Length);

            using var probe = new SKPaint { Typeface = t.Theme.Mono, TextSize = 100 };
            float ratio = probe.MeasureText("M") / 100f;
            if (ratio <= 0.1f) ratio = 0.6f;

            float minFs = 21 * S, maxFs = (t.Script.Format == ReelFormat.Landscape ? 38 : 40) * S;
            float fs = maxFs;
            for (int pass = 0; pass < 3; pass++)
            {
                float gutter = t.Script.ShowLineNumbers ? Digits * ratio * fs + 34 * S : 0;
                float avail = area.Width - 2 * PadX - gutter;
                fs = Math.Clamp(avail / (maxCols * ratio), minFs, maxFs);
            }
            FontSize = fs;
            CharW = ratio * fs;
            LineH = fs * 1.55f;
            Gutter = t.Script.ShowLineNumbers ? Digits * CharW + 34 * S : 0;

            float contentH = Math.Max(1, allLines.Count) * LineH;
            float maxBody = area.Height - HeaderH - 2 * PadY;
            float bodyH = Math.Min(contentH, maxBody);
            bodyH = Math.Max(bodyH, Math.Min(maxBody, 4 * LineH));
            float cardH = HeaderH + 2 * PadY + bodyH;
            float top = area.Top + (area.Height - cardH) / 2;
            Card = new SKRect(area.Left, top, area.Right, top + cardH);
            BodyTop = top + HeaderH + PadY;
            ViewH = bodyH;
        }
    }

    /// <summary>One scene: a code card whose new lines appear with the scene's effect, a caption, and highlighted @mark lines.</summary>
    internal sealed class CodeSegment : ReelTimeline.Segment
    {
        private readonly ReelScene _scene;
        private readonly CardLayout _layout;
        private readonly List<ReelLine> _lines;
        private readonly int _first;               // index of this scene's first new line
        private readonly List<CodeToken>[] _tokens;
        private readonly double[] _lineStart;      // when each new line starts appearing (scene time)
        private readonly double[][] _charTimes;    // Type: when each char of each new line appears
        private readonly double _revealEnd;
        private readonly double _markStart = -1;
        private readonly List<(double Time, string? Text)> _captions = new();
        private readonly int _index;
        private readonly bool _captionOnly;

        public string? FinalCaption => _captions.Count > 0 ? _captions[^1].Text : null;

        public CodeSegment(ReelTimeline t, ReelScene scene, CardLayout layout, List<ReelLine> prefix, int index, string? inheritedCaption = null) : base(t)
        {
            _scene = scene;
            _layout = layout;
            _index = index;
            _lines = prefix.Concat(scene.Lines).ToList();
            _first = prefix.Count;
            _captionOnly = _lines.Count == 0;

            var state = CSharpHighlighter.State.Normal;
            _tokens = _lines.Select(l => CSharpHighlighter.Tokenize(l.Text, ref state)).ToArray();

            int n = scene.Lines.Count;
            _lineStart = new double[n];
            _charTimes = new double[n][];

            double lead = 0.35;
            if (_captionOnly)
            {
                int words = (scene.Caption ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                _revealEnd = lead + Math.Clamp(words * 0.28, 0.8, 6);
            }
            else if (scene.Effect == CodeEffect.Type)
            {
                double speed = scene.Speed ?? t.Script.Speed;
                if (scene.Speed == null && !t.Script.SpeedExplicit)
                {
                    // Reels are short: a long scene types faster rather than
                    // dragging past ~9 s.
                    int chars = scene.Lines.Sum(l => l.Text.TrimStart().Length);
                    double est = chars / speed * 1.3;
                    if (est > 9)
                        speed = chars * 1.3 / 9;
                }
                double dt = 1.0 / speed;
                double time = lead;
                for (int i = 0; i < n; i++)
                {
                    foreach (var p in scene.Pauses.Where(p => p.Line == i))
                        time += p.Seconds;

                    var text = scene.Lines[i].Text;
                    _lineStart[i] = time;
                    var times = new double[text.Length];
                    int indent = 0;
                    while (indent < text.Length && text[indent] == ' ') indent++;
                    for (int j = 0; j < text.Length; j++)
                    {
                        if (j < indent)
                        {
                            times[j] = time; // the editor's auto-indent: all at once
                            continue;
                        }
                        char c = text[j];
                        double jitter = 0.55 + 0.9 * Hash(i * 131 + j * 31 + index * 7);
                        double step = dt * jitter;
                        if (c == ' ') step *= 0.6;
                        time += step;
                        times[j] = time;
                        if (c is ';' or '{' or '}' or ',') time += dt * 1.6;
                    }
                    _charTimes[i] = times;
                    time += text.Length == 0 ? dt * 2 : dt * 3.5; // Enter
                }
                _revealEnd = time;
            }
            else
            {
                double stagger = scene.Effect == CodeEffect.Lines ? Math.Min(0.085, 2.4 / Math.Max(1, n)) : 0;
                double time = scene.Effect == CodeEffect.Instant ? 0 : lead;
                for (int i = 0; i < n; i++)
                {
                    foreach (var p in scene.Pauses.Where(p => p.Line == i))
                        time += p.Seconds;
                    _lineStart[i] = time;
                    time += stagger;
                }
                double anim = scene.Effect switch { CodeEffect.Lines => 0.35, CodeEffect.Fade => 0.6, _ => 0 };
                _revealEnd = (n > 0 ? _lineStart[n - 1] : time) + anim;
            }

            bool hasMarks = scene.Lines.Any(l => l.Marked);
            if (hasMarks)
                _markStart = _revealEnd + 0.2;

            Duration = Math.Max(0.8, _revealEnd + (hasMarks ? 1.1 : 0) + scene.Hold);

            // Captions over time.
            if (scene.Caption != null)
                _captions.Add((0.15, scene.Caption));
            else if (inheritedCaption != null)
                _captions.Add((-10, inheritedCaption));
            foreach (var (line, text) in scene.CaptionChanges)
                _captions.Add((line < n ? _lineStart[line] : _revealEnd, text));
            _captions.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        public override bool ShowsHeader => true;
        public override string Label => string.IsNullOrWhiteSpace(_scene.Caption) ? $"Scene {_index + 1}" : _scene.Caption!;

        private static double Hash(int x)
        {
            unchecked
            {
                uint h = (uint)x * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFFF) / (double)0xFFFFFF;
            }
        }

        /// <summary>How many characters of line <paramref name="i"/> (index into _lines) are visible at scene time τ.</summary>
        private int VisibleChars(int i, double tau)
        {
            if (i < _first)
                return _lines[i].Text.Length;
            int k = i - _first;
            switch (_scene.Effect)
            {
                case CodeEffect.Type:
                {
                    var times = _charTimes[k];
                    if (tau < _lineStart[k]) return -1; // line not started
                    int lo = 0, hi = times.Length;
                    while (lo < hi)
                    {
                        int mid = (lo + hi) / 2;
                        if (times[mid] <= tau) lo = mid + 1; else hi = mid;
                    }
                    return lo;
                }
                default:
                    return tau >= _lineStart[k] ? _lines[i].Text.Length : -1;
            }
        }

        /// <summary>Line the eye should be on at τ (caret line, last revealed line...), as an index into _lines.</summary>
        private int FocusLine(double tau, out int caretCol)
        {
            caretCol = 0;
            int n = _scene.Lines.Count;
            if (n == 0)
            {
                caretCol = _lines.Count > 0 ? _lines[^1].Text.Length : 0;
                return _lines.Count - 1;
            }
            int last = -1;
            for (int k = 0; k < n; k++)
            {
                if (_lineStart[k] <= tau) last = k; else break;
            }
            if (last < 0)
            {
                if (_first > 0)
                {
                    caretCol = _lines[_first - 1].Text.Length;
                    return _first - 1;
                }
                return 0;
            }
            int i = _first + last;
            caretCol = Math.Max(0, VisibleChars(i, tau));
            return i;
        }

        private float ScrollAt(double tau)
        {
            var L = _layout;
            float contentH = _lines.Count * L.LineH;
            float max = Math.Max(0, contentH - L.ViewH);
            if (max <= 0)
                return 0;

            float Target(double x)
            {
                if (_scene.Effect is CodeEffect.Fade or CodeEffect.Instant)
                {
                    // Nothing is being "written": glide down the card while it's held.
                    double a = _revealEnd + 0.2, b = Math.Max(a + 0.1, Duration - 0.4);
                    float u = (float)Math.Clamp((x - a) / (b - a), 0, 1);
                    float from = Math.Clamp(_first * L.LineH - L.ViewH * 0.3f, 0, max);
                    return from + (max - from) * Ease.InOutCubic(u);
                }
                int line = FocusLine(x, out _);
                float focusBottom = (line + 1) * L.LineH;
                return Math.Clamp(focusBottom - L.ViewH * 0.78f, 0, max);
            }

            // Average over the last ~0.3 s: a deterministic low-pass, so the
            // camera eases instead of jumping a line at a time.
            float sum = 0;
            const int samples = 8;
            for (int k = 0; k < samples; k++)
                sum += Target(tau - 0.32 * k / (samples - 1));
            return sum / samples;
        }

        public override void Draw(SKCanvas canvas, double tau)
        {
            if (_captionOnly)
            {
                DrawCaptionCard(canvas, tau);
                return;
            }

            var th = T.Theme;
            var L = _layout;
            float S = T.S;
            var card = L.Card;
            float radius = 26 * S;

            // --- Panel ---
            using (var shadow = new SKPaint { IsAntialias = true, Color = th.Shadow.WithAlpha(120), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 34 * S) })
                canvas.DrawRoundRect(new SKRect(card.Left, card.Top + 22 * S, card.Right, card.Bottom + 22 * S), radius, radius, shadow);
            using (var panel = new SKPaint { IsAntialias = true, Color = th.Panel })
                canvas.DrawRoundRect(card, radius, radius, panel);

            canvas.Save();
            canvas.ClipRoundRect(new SKRoundRect(card, radius), SKClipOperation.Intersect, true);
            using (var header = new SKPaint { IsAntialias = true, Color = th.PanelHeader })
                canvas.DrawRect(new SKRect(card.Left, card.Top, card.Right, card.Top + L.HeaderH), header);
            canvas.Restore();

            using (var border = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * S, Color = th.PanelBorder })
                canvas.DrawRoundRect(card, radius, radius, border);

            // Window dots + file tab
            float dy = card.Top + L.HeaderH / 2;
            using (var dot = new SKPaint { IsAntialias = true })
            {
                dot.Color = th.Dot1; canvas.DrawCircle(card.Left + 34 * S, dy, 9 * S, dot);
                dot.Color = th.Dot2; canvas.DrawCircle(card.Left + 62 * S, dy, 9 * S, dot);
                dot.Color = th.Dot3; canvas.DrawCircle(card.Left + 90 * S, dy, 9 * S, dot);
            }
            using (var file = new SKPaint { IsAntialias = true, Typeface = th.Mono, TextSize = 25 * S, Color = th.Muted })
                canvas.DrawText(T.Script.FileName, card.Left + 124 * S, dy + 9 * S, file);

            // --- Body ---
            float scroll = ScrollAt(tau);
            var body = new SKRect(card.Left, L.BodyTop - L.PadY * 0.6f, card.Right, L.BodyTop + L.ViewH + L.PadY * 0.6f);
            canvas.Save();
            canvas.ClipRect(body, SKClipOperation.Intersect, true);

            int focus = FocusLine(tau, out int caretCol);
            bool typing = _scene.Effect == CodeEffect.Type && tau < _revealEnd && tau >= (_lineStart.Length > 0 ? _lineStart[0] : 0);
            float markP = _markStart >= 0 ? Ease.InOutCubic((float)Math.Clamp((tau - _markStart) / 0.5, 0, 1)) : 0;

            float codeX = card.Left + L.PadX + L.Gutter;
            using var text = new SKPaint { IsAntialias = true, Typeface = th.Mono, TextSize = L.FontSize, SubpixelText = true };
            using var num = new SKPaint { IsAntialias = true, Typeface = th.Mono, TextSize = L.FontSize * 0.86f, TextAlign = SKTextAlign.Right };
            using var band = new SKPaint { IsAntialias = true };

            int firstVisible = Math.Max(0, (int)(scroll / L.LineH) - 1);
            int lastVisible = Math.Min(_lines.Count - 1, (int)((scroll + L.ViewH) / L.LineH) + 1);

            for (int i = firstVisible; i <= lastVisible; i++)
            {
                int visible = VisibleChars(i, tau);
                if (visible < 0)
                    continue;

                float top = L.BodyTop + i * L.LineH - scroll;
                float alpha = 1, offsetY = 0;
                if (i >= _first && _scene.Effect is CodeEffect.Lines or CodeEffect.Fade)
                {
                    float dur = _scene.Effect == CodeEffect.Lines ? 0.35f : 0.6f;
                    float p = Ease.OutCubic((float)Math.Clamp((tau - _lineStart[i - _first]) / dur, 0, 1));
                    alpha = p;
                    if (_scene.Effect == CodeEffect.Lines)
                        offsetY = (1 - p) * L.LineH * 0.8f;
                }

                bool marked = i >= _first && _lines[i].Marked;
                if (_markStart >= 0 && !marked)
                    alpha *= 1 - 0.55f * markP; // everything else steps back

                top += offsetY;
                float baseline = top + L.LineH * 0.5f + L.FontSize * 0.36f;

                if (typing && i == focus)
                {
                    band.Color = th.CurrentLine;
                    canvas.DrawRect(new SKRect(card.Left, top, card.Right, top + L.LineH), band);
                }

                if (marked && markP > 0)
                {
                    float sweep = card.Width * markP;
                    band.Color = th.Mark.WithAlpha((byte)(46 * markP));
                    canvas.DrawRect(new SKRect(card.Left, top, card.Left + sweep, top + L.LineH), band);
                    band.Color = th.Mark.WithAlpha((byte)(255 * markP));
                    canvas.DrawRect(new SKRect(card.Left, top, card.Left + 6 * S, top + L.LineH), band);
                }

                if (T.Script.ShowLineNumbers)
                {
                    num.Color = (i == focus && (typing || marked)) || (marked && markP > 0.5f) ? th.Accent.WithAlpha((byte)(255 * alpha)) : th.LineNumber.WithAlpha((byte)(255 * alpha));
                    canvas.DrawText((i + 1).ToString(), codeX - 22 * S, baseline, num);
                }

                var line = _lines[i].Text;
                foreach (var tok in _tokens[i])
                {
                    if (tok.Start >= visible)
                        break;
                    int len = Math.Min(tok.Length, visible - tok.Start);
                    text.Color = th.ColorOf(tok.Kind).WithAlpha((byte)(255 * alpha));
                    canvas.DrawText(line.Substring(tok.Start, len), codeX + tok.Start * L.CharW, baseline, text);
                }
            }

            // Caret: solid while typing, blinking when idle.
            if (_scene.Effect == CodeEffect.Type && _scene.Lines.Count > 0 && (tau < _markStart || _markStart < 0))
            {
                bool on = typing || ((tau * 1.7) % 1.0) < 0.55;
                if (on && tau >= 0.15)
                {
                    float top = L.BodyTop + focus * L.LineH - scroll;
                    float x = codeX + caretCol * L.CharW + 1 * S;
                    using var caret = new SKPaint { IsAntialias = true, Color = th.Cursor };
                    canvas.DrawRoundRect(new SKRect(x, top + L.LineH * 0.16f, x + Math.Max(3, 4 * S), top + L.LineH * 0.84f), 2 * S, 2 * S, caret);
                }
            }

            canvas.Restore();

            // Scrolled content fades under the header instead of being cut.
            if (scroll > 1)
            {
                using var fade = new SKPaint();
                fade.Shader = SKShader.CreateLinearGradient(new SKPoint(0, body.Top), new SKPoint(0, body.Top + L.LineH),
                    new[] { th.Panel, th.Panel.WithAlpha(0) }, null, SKShaderTileMode.Clamp);
                canvas.DrawRect(new SKRect(card.Left + 2 * S, body.Top, card.Right - 2 * S, body.Top + L.LineH), fade);
            }

            // --- Caption ---
            var (captionText, since) = CaptionAt(tau);
            T.DrawCaption(canvas, captionText, since);
        }

        private (string? Text, double Age) CaptionAt(double tau)
        {
            string? text = null;
            double since = 0;
            foreach (var (time, t) in _captions)
            {
                if (time > tau) break;
                text = t;
                since = tau - time;
            }
            return (text, since);
        }

        private void DrawCaptionCard(SKCanvas canvas, double tau)
        {
            // A scene with words but no code: a big statement card.
            var th = T.Theme;
            float S = T.S;
            var area = T.ContentRect;
            using var paint = new SKPaint { IsAntialias = true, Typeface = th.UiBold, TextSize = 72 * S, Color = th.Text, TextAlign = SKTextAlign.Left };
            var lines = ReelTimeline.Wrap(_scene.Caption ?? "", paint, area.Width, 6);
            float lh = paint.TextSize * 1.18f;
            float y = area.MidY - lines.Count * lh / 2 + paint.TextSize;
            int w = 0;
            foreach (var line in lines)
            {
                float x = area.Left;
                foreach (var word in line.Split(' '))
                {
                    float p = Ease.OutCubic((float)Math.Clamp((tau - 0.3 - w * 0.09) / 0.4, 0, 1));
                    paint.Color = th.Text.WithAlpha((byte)(255 * p));
                    canvas.DrawText(word, x, y + (1 - p) * 30 * S, paint);
                    x += paint.MeasureText(word + " ");
                    w++;
                }
                y += lh;
            }
        }
    }
}
