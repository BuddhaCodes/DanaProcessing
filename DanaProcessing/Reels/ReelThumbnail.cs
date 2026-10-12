#pragma warning disable CS0618 // SkiaSharp 3 marks the SKPaint text API obsolete; it still works.
using SkiaSharp;

namespace DanaProcessing.Reels
{
    /// <summary>
    /// A YouTube thumbnail (1280x720 PNG) for a reel: the title big on the
    /// left, the running sketch on the right, in the reel's theme -- so the
    /// video and its thumbnail obviously belong together.
    /// </summary>
    public static class ReelThumbnail
    {
        public const int Width = 1280, Height = 720;

        /// <summary>Renders the thumbnail. <paramref name="sketchFactory"/> makes a fresh sketch, run for a moment so there's something to see.</summary>
        public static Task ExportAsync(ReelScript script, Func<Sketch>? sketchFactory, string path)
            => Task.Run(() =>
            {
                using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul))
                                    ?? throw new InvalidOperationException("Could not create the thumbnail surface.");
                Draw(surface.Canvas, script, sketchFactory);
                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var file = File.Create(path);
                data.SaveTo(file);
            });

        internal static void Draw(SKCanvas canvas, ReelScript script, Func<Sketch>? sketchFactory)
        {
            var th = ReelTheme.Create(script.Theme);
            float w = Width, h = Height;

            using (var bg = new SKPaint())
            {
                bg.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(w, h),
                    new[] { th.BackgroundTop, th.BackgroundBottom }, null, SKShaderTileMode.Clamp);
                canvas.DrawRect(0, 0, w, h, bg);
            }
            using (var glow = new SKPaint { IsAntialias = true })
            {
                glow.Shader = SKShader.CreateRadialGradient(new SKPoint(w * 0.78f, h * 0.5f), h * 0.75f,
                    new[] { th.GlowA.WithAlpha(70), th.GlowA.WithAlpha(0) }, null, SKShaderTileMode.Clamp);
                canvas.DrawRect(0, 0, w, h, glow);
            }

            // --- The sketch, on the right ---
            var box = new SKRect(w * 0.52f, h * 0.12f, w - 60, h * 0.88f);
            if (sketchFactory != null)
            {
                double at = Math.Clamp(script.ResultSeconds * 0.6, 0.5, 4);
                using var stage = new SketchStage(sketchFactory, script, Math.Max(at, script.ResultSeconds));
                stage.AdvanceTo(at);
                using var img = stage.Snapshot();
                if (img != null && stage.Error == null)
                {
                    float scale = Math.Min(box.Width / img.Width, box.Height / img.Height);
                    var dest = SKRect.Create(box.MidX - img.Width * scale / 2, box.MidY - img.Height * scale / 2, img.Width * scale, img.Height * scale);
                    using (var shadow = new SKPaint { IsAntialias = true, Color = th.Shadow.WithAlpha(140), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 24) })
                        canvas.DrawRoundRect(new SKRect(dest.Left, dest.Top + 14, dest.Right, dest.Bottom + 14), 22, 22, shadow);
                    canvas.Save();
                    canvas.ClipRoundRect(new SKRoundRect(dest, 22), SKClipOperation.Intersect, true);
                    canvas.Translate(dest.Left, dest.Top);
                    canvas.Scale(scale);
                    using (var smooth = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true })
                        canvas.DrawImage(img, 0, 0, smooth);
                    canvas.Restore();
                    using var border = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = th.Accent };
                    canvas.DrawRoundRect(dest, 22, 22, border);
                }
            }

            // --- Words, on the left ---
            float left = 64, maxW = w * 0.52f - left - 30;
            using var badge = new SKPaint { IsAntialias = true, Typeface = th.UiBold, TextSize = 30, Color = th.OnAccent };
            const string tag = "C#";
            float bw = badge.MeasureText(tag) + 36;
            using (var bb = new SKPaint { IsAntialias = true, Color = th.Accent })
                canvas.DrawRoundRect(new SKRect(left, 70, left + bw, 120), 25, 25, bb);
            canvas.DrawText(tag, left + 18, 106, badge);

            using var title = new SKPaint { IsAntialias = true, Typeface = th.UiBold, TextSize = 78, Color = th.Text };
            var words = string.IsNullOrWhiteSpace(script.Title) ? "Creative coding in C#" : script.Title!;
            var lines = ReelTimeline.Wrap(words, title, maxW, 4);
            while (lines.Count > 3 && title.TextSize > 48)
            {
                title.TextSize -= 6;
                lines = ReelTimeline.Wrap(words, title, maxW, 4);
            }
            float y = 160 + title.TextSize;
            foreach (var line in lines)
            {
                canvas.DrawText(line, left, y, title);
                y += title.TextSize * 1.08f;
            }

            using (var bar = new SKPaint { IsAntialias = true, Color = th.Accent })
                canvas.DrawRoundRect(new SKRect(left, y - title.TextSize * 0.55f, left + 140, y - title.TextSize * 0.55f + 10), 5, 5, bar);

            if (!string.IsNullOrWhiteSpace(script.Subtitle))
            {
                using var sub = new SKPaint { IsAntialias = true, Typeface = th.Ui, TextSize = 32, Color = th.Muted };
                float sy = y + 30;
                foreach (var l in ReelTimeline.Wrap(script.Subtitle!, sub, maxW, 2))
                {
                    canvas.DrawText(l, left, sy, sub);
                    sy += 42;
                }
            }

            using var brand = new SKPaint { IsAntialias = true, Typeface = th.UiBold, TextSize = 30, Color = th.Text.WithAlpha(220) };
            using (var dot = new SKPaint { IsAntialias = true, Color = th.Accent })
                canvas.DrawCircle(left + 9, h - 62, 9, dot);
            canvas.DrawText("DanaProcessing", left + 28, h - 52, brand);
        }
    }
}
