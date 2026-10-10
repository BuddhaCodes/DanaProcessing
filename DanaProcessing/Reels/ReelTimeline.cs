#pragma warning disable CS0618 // SkiaSharp 3 marks the SKPaint text API obsolete; it still works and keeps this file usable on 2.88 too.
using SkiaSharp;

namespace DanaProcessing.Reels
{
    /// <summary>
    /// The whole reel as a function of time: Render(canvas, t) draws frame t
    /// (seconds) at the reel's full resolution (Width x Height). The preview
    /// and the exporter both just call Render -- nothing here knows whether
    /// it's being watched or encoded.
    ///
    /// Order: [title card] -> one card per scene (typed, revealed, captioned,
    /// marked lines highlighted) -> [result: the sketch running, with
    /// simulated input] -> [outro]. Each part enters with its transition.
    /// </summary>
    public sealed class ReelTimeline : IDisposable
    {
        internal const double TransitionSeconds = 0.5;

        public ReelScript Script { get; }
        public int Width { get; }
        public int Height { get; }
        public int Fps => Script.Fps;
        /// <summary>Total length in seconds.</summary>
        public double Duration { get; }
        /// <summary>Start time and kind of every part, e.g. for a scrubber with chapter ticks.</summary>
        public IReadOnlyList<(double Start, double Duration, string Label)> Parts { get; }

        internal ReelTheme Theme { get; }
        internal float S { get; }   // 1 unit = 1 px at 1080 on the short side
        internal float MarginX { get; }
        internal SKRect ContentRect { get; }
        internal SKRect CaptionRect { get; }

        private readonly List<Segment> _segments = new();
        private readonly SketchStage? _stage;

        private ReelTimeline(ReelScript script, Func<Sketch>? sketchFactory)
        {
            Script = script;
            Width = script.Width;
            Height = script.Height;
            Theme = ReelTheme.Create(script.Theme);
            S = Math.Min(Width, Height) / 1080f;

            bool landscape = script.Format == ReelFormat.Landscape;
            bool vertical = script.Format == ReelFormat.Vertical;
            MarginX = (landscape ? 150 : vertical ? 60 : 64) * S;
            float headerBottom = (vertical ? 250 : 190) * S;
            float captionH = (vertical ? 330 : landscape ? 170 : 200) * S;
            float brandH = (vertical ? 90 : 70) * S;
            ContentRect = new SKRect(MarginX, headerBottom, Width - MarginX, Height - captionH - brandH);
            CaptionRect = new SKRect(MarginX, ContentRect.Bottom + 24 * S, Width - MarginX, Height - brandH);

            // --- Build the parts ---
            if (!string.IsNullOrWhiteSpace(script.Title))
                _segments.Add(new TitleSegment(this));

            // Scenes that "keep" chain onto the previous one: same card, same
            // font size, so the layout is computed per chain.
            var chains = new List<List<ReelScene>>();
            foreach (var scene in script.Scenes)
            {
                if (scene.Keep && chains.Count > 0 && chains[^1][^1].Lines.Count > 0)
                    chains[^1].Add(scene);
                else
                    chains.Add(new List<ReelScene> { scene });
            }

            int codeIndex = 0;
            foreach (var chain in chains)
            {
                var layout = new CardLayout(this, chain.SelectMany(s => s.Lines).ToList());
                var prefix = new List<ReelLine>();
                string? lastCaption = null;
                for (int k = 0; k < chain.Count; k++)
                {
                    var scene = chain[k];
                    var seg = new CodeSegment(this, scene, layout, prefix.ToList(), codeIndex++, k > 0 ? lastCaption : null);
                    lastCaption = seg.FinalCaption;
                    seg.Transition = k > 0 ? ReelTransition.None
                        : scene.Transition ?? (_segments.Count == 0 ? ReelTransition.Fade : ReelTransition.Slide);
                    _segments.Add(seg);
                    prefix.AddRange(scene.Lines);
                }
            }

            if (script.ResultSeconds > 0 && sketchFactory != null)
            {
                _stage = new SketchStage(sketchFactory, script, script.ResultSeconds);
                _segments.Add(new ResultSegment(this, _stage) { Transition = script.ResultTransition });
            }

            if (script.Outro != "")
                _segments.Add(new OutroSegment(this) { Transition = ReelTransition.Fade });

            if (_segments.Count == 0)
                _segments.Add(new OutroSegment(this));

            if (_segments[0] is not TitleSegment && _segments[0].Transition != ReelTransition.None)
                _segments[0].Transition = ReelTransition.Fade; // fade in from the background

            double t = 0;
            var parts = new List<(double, double, string)>();
            foreach (var s in _segments)
            {
                s.Start = t;
                t += s.Duration;
                parts.Add((s.Start, s.Duration, s.Label));
            }
            Duration = t;
            Parts = parts;
        }

        /// <summary>Builds the timeline for a script. <paramref name="sketchFactory"/> makes a fresh instance of the sketch for the result part (null = no result part).</summary>
        public static ReelTimeline Build(ReelScript script, Func<Sketch>? sketchFactory) => new(script, sketchFactory);

        /// <summary>Problem the result part ran into (an exception in the sketch), if any.</summary>
        public Exception? SketchError => _stage?.Error;

        /// <summary>Draws frame <paramref name="t"/> (seconds) onto <paramref name="canvas"/>, in reel pixels (0..Width, 0..Height).</summary>
        public void Render(SKCanvas canvas, double t)
        {
            t = Math.Clamp(t, 0, Math.Max(0, Duration - 1e-6));

            int index = _segments.Count - 1;
            for (int i = 0; i < _segments.Count; i++)
            {
                if (t < _segments[i].Start + _segments[i].Duration)
                {
                    index = i;
                    break;
                }
            }
            var seg = _segments[index];
            double local = t - seg.Start;

            canvas.Save();
            DrawBackground(canvas, t);

            // Header (progress + title) belongs to the code/result parts only;
            // it fades in/out instead of travelling with the transitions.
            float headerAlpha = seg.ShowsHeader ? 1 : 0;
            Segment? prev = index > 0 ? _segments[index - 1] : null;
            bool inTransition = prev != null && seg.Transition != ReelTransition.None && local < TransitionSeconds;
            if (index == 0 && seg.Transition == ReelTransition.Fade && local < TransitionSeconds)
                inTransition = true; // first part fades in from the bare background

            if (inTransition)
            {
                float p = Ease.InOutCubic((float)(local / TransitionSeconds));
                if (prev != null)
                    headerAlpha = Lerp(prev.ShowsHeader ? 1 : 0, headerAlpha, p);
                DrawTransition(canvas, prev, seg, local, p);
            }
            else
            {
                seg.Draw(canvas, local);
            }

            if (headerAlpha > 0.001f)
                DrawHeader(canvas, t, headerAlpha);
            DrawBrand(canvas, seg, local);
            canvas.Restore();
        }

        private void DrawTransition(SKCanvas canvas, Segment? prev, Segment next, double local, float p)
        {
            float w = Width, h = Height;
            switch (next.Transition)
            {
                case ReelTransition.Slide:
                    if (prev != null)
                    {
                        canvas.Save();
                        canvas.Translate(-p * w, 0);
                        prev.Draw(canvas, prev.Duration);
                        canvas.Restore();
                    }
                    canvas.Save();
                    canvas.Translate((1 - p) * w, 0);
                    next.Draw(canvas, local);
                    canvas.Restore();
                    break;

                case ReelTransition.Zoom:
                    if (prev != null)
                        WithLayer(canvas, 1 - p, () =>
                        {
                            canvas.Scale(1 + 0.12f * p, 1 + 0.12f * p, w / 2, h / 2);
                            prev.Draw(canvas, prev.Duration);
                        });
                    WithLayer(canvas, p, () =>
                    {
                        float s = 0.86f + 0.14f * p;
                        canvas.Scale(s, s, w / 2, h / 2);
                        next.Draw(canvas, local);
                    });
                    break;

                case ReelTransition.Wipe:
                {
                    // Circle opening from the center: a "spotlight" reveal.
                    if (prev != null)
                        prev.Draw(canvas, prev.Duration);
                    float r = p * MathF.Sqrt(w * w + h * h) / 2;
                    canvas.Save();
                    using (var path = new SKPath())
                    {
                        path.AddCircle(w / 2, h / 2, Math.Max(0.5f, r));
                        canvas.ClipPath(path, SKClipOperation.Intersect, true);
                    }
                    DrawBackground(canvas, next.Start + local);
                    next.Draw(canvas, local);
                    canvas.Restore();
                    break;
                }

                default: // Fade
                    if (prev != null)
                        WithLayer(canvas, 1 - p, () => prev.Draw(canvas, prev.Duration));
                    WithLayer(canvas, p, () =>
                    {
                        canvas.Translate(0, (1 - p) * 24 * S);
                        next.Draw(canvas, local);
                    });
                    break;
            }
        }

        internal static void WithLayer(SKCanvas canvas, float alpha, Action draw)
        {
            alpha = Math.Clamp(alpha, 0, 1);
            if (alpha <= 0.002f)
                return;
            if (alpha >= 0.998f)
            {
                canvas.Save();
                draw();
                canvas.Restore();
                return;
            }
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)(alpha * 255)) };
            canvas.SaveLayer(paint);
            draw();
            canvas.Restore();
        }

        /// <summary>
        /// The slow drifting glows. On by default; the GIF export turns it off,
        /// because a background where every pixel changes every frame defeats
        /// GIF's "only store what changed" trick and multiplies the file size.
        /// </summary>
        public bool AnimateBackground { get; set; } = true;

        private void DrawBackground(SKCanvas canvas, double t)
        {
            if (!AnimateBackground)
                t = 0;
            var th = Theme;
            using (var paint = new SKPaint())
            {
                paint.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, Height),
                    new[] { th.BackgroundTop, th.BackgroundBottom }, null, SKShaderTileMode.Clamp);
                canvas.DrawRect(0, 0, Width, Height, paint);
            }

            // Two slow, soft glows -- enough life that a still frame of code
            // still feels like video.
            DrawGlow(canvas, th.GlowA, Width * (0.18f + 0.06f * (float)Math.Sin(t * 0.35)), Height * (0.16f + 0.04f * (float)Math.Cos(t * 0.27)), Math.Max(Width, Height) * 0.55f, 0.20f);
            DrawGlow(canvas, th.GlowB, Width * (0.86f + 0.05f * (float)Math.Cos(t * 0.31)), Height * (0.82f + 0.05f * (float)Math.Sin(t * 0.23)), Math.Max(Width, Height) * 0.5f, 0.14f);
        }

        private static void DrawGlow(SKCanvas canvas, SKColor c, float x, float y, float r, float alpha)
        {
            using var paint = new SKPaint { IsAntialias = true };
            paint.Shader = SKShader.CreateRadialGradient(new SKPoint(x, y), r,
                new[] { c.WithAlpha((byte)(alpha * 255)), c.WithAlpha(0) }, null, SKShaderTileMode.Clamp);
            canvas.DrawCircle(x, y, r, paint);
        }

        private void DrawHeader(SKCanvas canvas, double t, float alpha)
        {
            var th = Theme;
            byte A(float a) => (byte)Math.Clamp(a * alpha * 255, 0, 255);

            float y = (Script.Format == ReelFormat.Vertical ? 70 : 48) * S;

            if (Script.ShowProgress)
            {
                var bars = _segments.Where(s => s is CodeSegment or ResultSegment).ToList();
                if (bars.Count > 0)
                {
                    float gap = 8 * S, barH = 7 * S;
                    float total = Width - 2 * MarginX;
                    float bw = (total - gap * (bars.Count - 1)) / bars.Count;
                    using var bg = new SKPaint { IsAntialias = true, Color = th.Text.WithAlpha(A(0.16f)) };
                    using var fg = new SKPaint { IsAntialias = true, Color = th.Accent.WithAlpha(A(1)) };
                    for (int i = 0; i < bars.Count; i++)
                    {
                        float x = MarginX + i * (bw + gap);
                        var r = new SKRect(x, y, x + bw, y + barH);
                        canvas.DrawRoundRect(r, barH / 2, barH / 2, bg);
                        float f = (float)Math.Clamp((t - bars[i].Start) / bars[i].Duration, 0, 1);
                        if (f > 0)
                            canvas.DrawRoundRect(new SKRect(x, y, x + Math.Max(barH, bw * f), y + barH), barH / 2, barH / 2, fg);
                    }
                }
                y += 30 * S;
            }

            if (!string.IsNullOrWhiteSpace(Script.Title))
            {
                using var title = new SKPaint { IsAntialias = true, Typeface = Theme.UiBold, TextSize = 44 * S, Color = th.Text.WithAlpha(A(1)) };
                var text = Fit(Script.Title!, title, Width - 2 * MarginX);
                canvas.DrawText(text, MarginX, y + 52 * S, title);
                if (!string.IsNullOrWhiteSpace(Script.Subtitle) && Script.Format == ReelFormat.Vertical)
                {
                    using var sub = new SKPaint { IsAntialias = true, Typeface = Theme.Ui, TextSize = 30 * S, Color = th.Muted.WithAlpha(A(1)) };
                    canvas.DrawText(Fit(Script.Subtitle!, sub, Width - 2 * MarginX), MarginX, y + 98 * S, sub);
                }
            }
        }

        private void DrawBrand(SKCanvas canvas, Segment seg, double local)
        {
            if (seg is OutroSegment)
                return;
            var th = Theme;
            using var paint = new SKPaint { IsAntialias = true, Typeface = th.UiBold, TextSize = 26 * S, Color = th.Muted.WithAlpha(190) };
            const string word = "DanaProcessing";
            float w = paint.MeasureText(word);
            float dot = 7 * S;
            float x = (Width - w - dot * 3) / 2;
            float y = Height - (Script.Format == ReelFormat.Vertical ? 46 : 34) * S;
            using var dp = new SKPaint { IsAntialias = true, Color = th.Accent };
            canvas.DrawCircle(x + dot, y - 9 * S, dot, dp);
            canvas.DrawText(word, x + dot * 3, y, paint);
        }

        internal static string Fit(string text, SKPaint paint, float maxWidth)
        {
            if (paint.MeasureText(text) <= maxWidth)
                return text;
            for (int n = text.Length - 1; n > 0; n--)
            {
                var candidate = text.Substring(0, n).TrimEnd() + "…";
                if (paint.MeasureText(candidate) <= maxWidth)
                    return candidate;
            }
            return "…";
        }

        internal static List<string> Wrap(string text, SKPaint paint, float maxWidth, int maxLines)
        {
            var result = new List<string>();
            foreach (var paragraph in text.Split('\n'))
            {
                var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var line = "";
                foreach (var word in words)
                {
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    if (paint.MeasureText(candidate) <= maxWidth || line.Length == 0)
                        line = candidate;
                    else
                    {
                        result.Add(line);
                        line = word;
                    }
                }
                if (line.Length > 0)
                    result.Add(line);
            }
            if (result.Count > maxLines)
            {
                result = result.Take(maxLines).ToList();
                result[^1] = Fit(result[^1] + " …", paint, maxWidth);
            }
            return result;
        }

        internal static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>
        /// The caption pill under the card. <paramref name="age"/> = seconds
        /// since this caption text appeared (drives its entrance).
        /// </summary>
        internal void DrawCaption(SKCanvas canvas, string? caption, double age, float alpha = 1)
        {
            if (string.IsNullOrWhiteSpace(caption))
                return;
            var th = Theme;
            float p = Ease.OutCubic((float)Math.Clamp(age / 0.35, 0, 1));
            float a = p * alpha;
            if (a <= 0.01f)
                return;

            bool vertical = Script.Format == ReelFormat.Vertical;
            using var paint = new SKPaint { IsAntialias = true, Typeface = th.UiBold, TextSize = (vertical ? 46 : 38) * S, Color = th.CaptionText.WithAlpha((byte)(a * 255)) };
            float padX = 34 * S, padY = 22 * S;
            float maxTextW = CaptionRect.Width - 2 * padX;
            var lines = Wrap(caption!, paint, maxTextW, vertical ? 3 : 2);
            float lh = paint.TextSize * 1.25f;
            float textW = lines.Max(l => paint.MeasureText(l));
            float boxW = textW + 2 * padX;
            float boxH = lines.Count * lh + 2 * padY;
            float cx = Width / 2f;
            float cy = CaptionRect.MidY + (1 - p) * 26 * S;
            var box = new SKRect(cx - boxW / 2, cy - boxH / 2, cx + boxW / 2, cy + boxH / 2);

            using (var shadow = new SKPaint { IsAntialias = true, Color = th.Shadow.WithAlpha((byte)(a * 70)), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 18 * S) })
                canvas.DrawRoundRect(new SKRect(box.Left, box.Top + 10 * S, box.Right, box.Bottom + 10 * S), 28 * S, 28 * S, shadow);
            using (var bg = new SKPaint { IsAntialias = true, Color = th.CaptionBg.WithAlpha((byte)(a * 255)) })
                canvas.DrawRoundRect(box, 28 * S, 28 * S, bg);

            paint.TextAlign = SKTextAlign.Center;
            float y = box.Top + padY + paint.TextSize * 0.95f;
            foreach (var l in lines)
            {
                canvas.DrawText(l, cx, y, paint);
                y += lh;
            }
        }

        public void Dispose() => _stage?.Dispose();

        // =====================================================================
        // Parts
        // =====================================================================

        internal abstract class Segment
        {
            protected readonly ReelTimeline T;
            protected Segment(ReelTimeline t) => T = t;
            public double Start;
            public double Duration;
            public ReelTransition Transition = ReelTransition.Fade;
            public virtual bool ShowsHeader => false;
            public abstract string Label { get; }
            public abstract void Draw(SKCanvas canvas, double local);
        }

        // --- Title card ---------------------------------------------------

        private sealed class TitleSegment : Segment
        {
            public TitleSegment(ReelTimeline t) : base(t)
            {
                Duration = 2.6;
                Transition = ReelTransition.None;
            }

            public override string Label => "Title";

            public override void Draw(SKCanvas canvas, double local)
            {
                var th = T.Theme;
                float S = T.S;
                float w = T.Width;
                using var paint = new SKPaint { IsAntialias = true, Typeface = th.UiBold, TextSize = (T.Script.Format == ReelFormat.Landscape ? 104 : 96) * S, Color = th.Text };
                float maxW = w - 2 * T.MarginX - 40 * S;
                var lines = Wrap(T.Script.Title!, paint, maxW, 4);
                float lh = paint.TextSize * 1.12f;
                float blockH = lines.Count * lh;
                float top = T.Height * 0.44f - blockH / 2;

                // Word by word, rising into place.
                int wordIndex = 0;
                float y = top + paint.TextSize;
                foreach (var line in lines)
                {
                    var words = line.Split(' ');
                    float lineW = paint.MeasureText(line);
                    float x = (w - lineW) / 2;
                    foreach (var word in words)
                    {
                        float start = 0.15f + wordIndex * 0.07f;
                        float p = Ease.OutCubic(Math.Clamp(((float)local - start) / 0.45f, 0, 1));
                        paint.Color = th.Text.WithAlpha((byte)(p * 255));
                        canvas.DrawText(word, x, y + (1 - p) * 40 * S, paint);
                        x += paint.MeasureText(word + " ");
                        wordIndex++;
                    }
                    y += lh;
                }

                // Accent underline grows from the center.
                float up = Ease.OutCubic(Math.Clamp(((float)local - 0.45f) / 0.5f, 0, 1));
                float uw = 180 * S * up;
                using (var accent = new SKPaint { IsAntialias = true, Color = th.Accent })
                    canvas.DrawRoundRect(new SKRect(w / 2 - uw / 2, top + blockH + 26 * S, w / 2 + uw / 2, top + blockH + 36 * S), 5 * S, 5 * S, accent);

                if (!string.IsNullOrWhiteSpace(T.Script.Subtitle))
                {
                    float sp = Ease.OutCubic(Math.Clamp(((float)local - 0.65f) / 0.5f, 0, 1));
                    using var sub = new SKPaint { IsAntialias = true, Typeface = th.Ui, TextSize = 44 * S, Color = th.Muted.WithAlpha((byte)(sp * 255)), TextAlign = SKTextAlign.Center };
                    float sy = top + blockH + 100 * S + (1 - sp) * 20 * S;
                    foreach (var l in Wrap(T.Script.Subtitle!, sub, maxW, 3))
                    {
                        canvas.DrawText(l, w / 2, sy, sub);
                        sy += sub.TextSize * 1.3f;
                    }
                }

                // Small "C# · DanaProcessing" tag above the title.
                float tp = Ease.OutCubic(Math.Clamp((float)local / 0.4f, 0, 1));
                using (var tag = new SKPaint { IsAntialias = true, Typeface = th.Mono, TextSize = 30 * S, Color = th.Accent.WithAlpha((byte)(tp * 255)), TextAlign = SKTextAlign.Center })
                    canvas.DrawText("C# · creative coding", w / 2, top - 50 * S, tag);
            }
        }

        // --- Outro card ---------------------------------------------------

        private sealed class OutroSegment : Segment
        {
            public OutroSegment(ReelTimeline t) : base(t) => Duration = 2.0;

            public override string Label => "Outro";

            public override void Draw(SKCanvas canvas, double local)
            {
                var th = T.Theme;
                float S = T.S, w = T.Width, h = T.Height;
                float p = Ease.OutBack(Math.Clamp((float)local / 0.6f, 0, 1));
                canvas.Save();
                float s = 0.9f + 0.1f * p;
                canvas.Scale(s, s, w / 2, h / 2);

                using var word = new SKPaint { IsAntialias = true, Typeface = th.UiBold, TextSize = 84 * S, Color = th.Text };
                const string brand = "DanaProcessing";
                float bw = word.MeasureText(brand);
                float dot = 16 * S;
                float x = (w - bw - dot * 3) / 2;
                float y = h * 0.47f;
                using (var dp = new SKPaint { IsAntialias = true, Color = th.Accent })
                    canvas.DrawCircle(x + dot, y - 28 * S, dot, dp);
                canvas.DrawText(brand, x + dot * 3, y, word);

                var line = string.IsNullOrWhiteSpace(T.Script.Outro)
                    ? (T.Script.Lang == "es" ? "Creative coding en C#, al estilo Processing" : "Creative coding in C#, the Processing way")
                    : T.Script.Outro!;
                float lp = Math.Clamp(((float)local - 0.3f) / 0.5f, 0, 1);
                using var sub = new SKPaint { IsAntialias = true, Typeface = th.Ui, TextSize = 40 * S, Color = th.Muted.WithAlpha((byte)(lp * 255)), TextAlign = SKTextAlign.Center };
                float sy = y + 80 * S;
                foreach (var l in Wrap(line, sub, w - 2 * T.MarginX, 3))
                {
                    canvas.DrawText(l, w / 2, sy, sub);
                    sy += sub.TextSize * 1.3f;
                }
                using var url = new SKPaint { IsAntialias = true, Typeface = th.Mono, TextSize = 28 * S, Color = th.Accent.WithAlpha((byte)(lp * 255)), TextAlign = SKTextAlign.Center };
                canvas.DrawText("buddhacodes.github.io/DanaProcessing", w / 2, sy + 30 * S, url);
                canvas.Restore();
            }
        }

        // --- The running sketch ---------------------------------------------

        private sealed class ResultSegment : Segment
        {
            private readonly SketchStage _stage;

            public ResultSegment(ReelTimeline t, SketchStage stage) : base(t)
            {
                _stage = stage;
                Duration = t.Script.ResultSeconds;
            }

            public override bool ShowsHeader => true;
            public override string Label => "Result";

            public override void Draw(SKCanvas canvas, double local)
            {
                var th = T.Theme;
                float S = T.S;
                _stage.AdvanceTo(Math.Max(0, local));

                // "▶ Result" pill
                var label = T.Script.Lang == "es" ? "Resultado" : "Result";
                using var lp = new SKPaint { IsAntialias = true, Typeface = th.UiBold, TextSize = 34 * S, Color = th.OnAccent };
                float lw = lp.MeasureText(label);
                var area = T.ContentRect;
                var pill = new SKRect(area.Left, area.Top, area.Left + lw + 96 * S, area.Top + 64 * S);
                using (var pb = new SKPaint { IsAntialias = true, Color = th.Accent })
                    canvas.DrawRoundRect(pill, 32 * S, 32 * S, pb);
                using (var tri = new SKPath())
                {
                    float cx = pill.Left + 36 * S, cy = pill.MidY;
                    tri.MoveTo(cx - 9 * S, cy - 12 * S);
                    tri.LineTo(cx + 13 * S, cy);
                    tri.LineTo(cx - 9 * S, cy + 12 * S);
                    tri.Close();
                    using var tp = new SKPaint { IsAntialias = true, Color = th.OnAccent };
                    canvas.DrawPath(tri, tp);
                }
                canvas.DrawText(label, pill.Left + 62 * S, pill.MidY + 12 * S, lp);

                // The sketch, fitted into the content area under the pill.
                var box = new SKRect(area.Left, pill.Bottom + 28 * S, area.Right, area.Bottom);
                float sw = _stage.Width, sh = _stage.Height;
                float scale = Math.Min(box.Width / sw, box.Height / sh);
                float dw = sw * scale, dh = sh * scale;
                var dest = SKRect.Create(box.MidX - dw / 2, box.MidY - dh / 2, dw, dh);
                float radius = 26 * S;

                using (var shadow = new SKPaint { IsAntialias = true, Color = th.Shadow.WithAlpha(110), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 30 * S) })
                    canvas.DrawRoundRect(new SKRect(dest.Left, dest.Top + 18 * S, dest.Right, dest.Bottom + 18 * S), radius, radius, shadow);

                canvas.Save();
                canvas.ClipRoundRect(new SKRoundRect(dest, radius), SKClipOperation.Intersect, true);
                using (var image = _stage.Snapshot())
                {
                    if (image != null)
                    {
                        canvas.Save();
                        canvas.Translate(dest.Left, dest.Top);
                        canvas.Scale(scale);
                        using (var smooth = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true })
                            canvas.DrawImage(image, 0, 0, smooth);
                        canvas.Restore();
                    }
                }
                if (_stage.Error != null)
                    DrawError(canvas, dest, _stage.Error, _stage.ErrorContext);
                canvas.Restore();

                using (var border = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * S, Color = th.PanelBorder })
                    canvas.DrawRoundRect(dest, radius, radius, border);

                DrawInputOverlay(canvas, dest, scale, local);

                T.DrawCaption(canvas, T.Script.ResultCaption, local - 0.3);
            }

            private void DrawInputOverlay(SKCanvas canvas, SKRect dest, float scale, double local)
            {
                var input = _stage.Input;
                if (input == null)
                    return;
                float S = T.S;
                var th = T.Theme;

                if (input.HasMouse)
                {
                    foreach (var (x, y, age) in input.RecentPresses(local))
                    {
                        float p = (float)(age / 0.7);
                        using var ring = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 5 * S * (1 - p) + 1, Color = th.Accent.WithAlpha((byte)((1 - p) * 230)) };
                        canvas.DrawCircle(dest.Left + x * scale, dest.Top + y * scale, (14 + 70 * Ease.OutCubic(p)) * S, ring);
                    }

                    var (mx, my) = input.PositionAt(local);
                    bool pressed = input.PressedAt(local);
                    DrawCursor(canvas, dest.Left + mx * scale, dest.Top + my * scale, pressed);
                }

                if (input.RecentKey(local) is { } key)
                {
                    float p = (float)(key.Age / 0.9);
                    float a = p < 0.7f ? 1 : 1 - (p - 0.7f) / 0.3f;
                    using var kp = new SKPaint { IsAntialias = true, Typeface = th.UiBold, TextSize = 40 * S, Color = th.Text.WithAlpha((byte)(a * 255)), TextAlign = SKTextAlign.Center };
                    float kw = Math.Max(90 * S, kp.MeasureText(key.Label) + 50 * S);
                    float pop = Ease.OutBack(Math.Clamp((float)key.Age / 0.25f, 0, 1));
                    var r = SKRect.Create(dest.MidX - kw / 2, dest.Bottom - 120 * S, kw, 84 * S);
                    canvas.Save();
                    canvas.Scale(0.8f + 0.2f * pop, 0.8f + 0.2f * pop, r.MidX, r.MidY);
                    using (var kb = new SKPaint { IsAntialias = true, Color = th.Panel.WithAlpha((byte)(a * 235)) })
                        canvas.DrawRoundRect(r, 18 * S, 18 * S, kb);
                    using (var ks = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * S, Color = th.Accent.WithAlpha((byte)(a * 255)) })
                        canvas.DrawRoundRect(r, 18 * S, 18 * S, ks);
                    canvas.DrawText(key.Label, r.MidX, r.MidY + 14 * S, kp);
                    canvas.Restore();
                }
            }

            private void DrawCursor(SKCanvas canvas, float x, float y, bool pressed)
            {
                float S = T.S * (pressed ? 0.88f : 1f) * 1.25f;
                using var path = new SKPath();
                // Classic arrow pointer, tip at (0,0).
                path.MoveTo(0, 0);
                path.LineTo(0, 34);
                path.LineTo(8.5f, 26);
                path.LineTo(14.5f, 39);
                path.LineTo(20, 36.5f);
                path.LineTo(14, 24);
                path.LineTo(25, 24);
                path.Close();
                canvas.Save();
                canvas.Translate(x, y);
                canvas.Scale(S);
                using (var shadow = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 90), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3) })
                {
                    canvas.Save();
                    canvas.Translate(2, 3);
                    canvas.DrawPath(path, shadow);
                    canvas.Restore();
                }
                using (var fill = new SKPaint { IsAntialias = true, Color = pressed ? T.Theme.Accent : SKColors.White })
                    canvas.DrawPath(path, fill);
                using (var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, StrokeJoin = SKStrokeJoin.Round, Color = new SKColor(20, 20, 20) })
                    canvas.DrawPath(path, stroke);
                canvas.Restore();
            }

            private void DrawError(SKCanvas canvas, SKRect r, Exception ex, string context)
            {
                float S = T.S;
                using (var bg = new SKPaint { Color = new SKColor(30, 8, 8, 230) })
                    canvas.DrawRect(r, bg);
                using var title = new SKPaint { IsAntialias = true, Typeface = T.Theme.UiBold, TextSize = 34 * S, Color = new SKColor(255, 140, 140) };
                using var body = new SKPaint { IsAntialias = true, Typeface = T.Theme.Mono, TextSize = 26 * S, Color = new SKColor(255, 215, 215) };
                float y = r.Top + 60 * S;
                canvas.DrawText($"{context}(): {ex.GetType().Name}", r.Left + 30 * S, y, title);
                y += 50 * S;
                foreach (var l in Wrap(ex.Message, body, r.Width - 60 * S, 6))
                {
                    canvas.DrawText(l, r.Left + 30 * S, y, body);
                    y += 36 * S;
                }
            }
        }
    }

    internal static class Ease
    {
        public static float OutCubic(float t) { t = Math.Clamp(t, 0, 1); var u = 1 - t; return 1 - u * u * u; }
        public static float InOutCubic(float t) { t = Math.Clamp(t, 0, 1); return t < 0.5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2; }
        public static float OutBack(float t)
        {
            t = Math.Clamp(t, 0, 1);
            const float c1 = 1.70158f, c3 = c1 + 1;
            return 1 + c3 * MathF.Pow(t - 1, 3) + c1 * MathF.Pow(t - 1, 2);
        }
    }
}
