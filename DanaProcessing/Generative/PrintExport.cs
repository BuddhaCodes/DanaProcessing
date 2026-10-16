using System.Threading;
using SkiaSharp;

namespace DanaProcessing
{
    /// <summary>What <see cref="PrintExport"/> wrote, and how.</summary>
    public sealed record PrintExportResult(string Path, int Width, int Height, bool Vector, string? Warning);

    /// <summary>
    /// Print-quality output of a sketch: a PNG at 2x-8x (or any size) for a
    /// poster, or an SVG for a pen plotter.
    ///
    /// How: a fresh instance of the sketch -- same seed, same [Tweak] values --
    /// runs its first N frames off-screen while every drawing call is recorded
    /// as vectors (an SKPicture), instead of being turned into pixels. That
    /// recording is then played back at the big size one horizontal band at a
    /// time, straight into a streaming PNG writer, so shapes and text come out
    /// sharp at any size and memory stays at one band. Sketches that read
    /// pixels back (LoadPixels(), Get(), Filter()...) can't be recorded that
    /// way; they're drawn as pixels at the big size instead (and say so).
    /// </summary>
    public static class PrintExport
    {
        /// <summary>Output rows drawn per band (memory = width * BandRows * 4 bytes).</summary>
        public const int BandRows = 512;

        /// <summary>Largest side a non-vector fallback may have (it needs the whole image in memory).</summary>
        public const int MaxRasterSide = 12000;

        /// <summary>
        /// The first <paramref name="frames"/> frames of a fresh sketch as one
        /// vector recording. Null (with <paramref name="error"/>) if the sketch
        /// can't be recorded -- it read pixels back, or threw.
        /// </summary>
        internal static SKPicture? Record(Func<Sketch> factory, int seed, IReadOnlyDictionary<string, string> tweaks, int frames,
                                         out int width, out int height, out Exception? error)
        {
            width = height = 0;
            error = null;
            Sketch? sketch = null;
            var recorder = new SKPictureRecorder();
            try
            {
                sketch = Prepare(factory, seed, tweaks);
                // Size() inside Setup() decides the canvas size; the recording
                // starts big enough for any size and is cropped on playback.
                var canvas = recorder.BeginRecording(new SKRect(0, 0, 16384, 16384));
                canvas.Clear(new SKColor(200, 200, 200)); // Processing's grey, until the sketch paints
                sketch.SetCanvas(canvas, null);
                sketch.Setup();
                RunFrames(sketch, frames, () => canvas, 1f);
                width = sketch.Width;
                height = sketch.Height;
                return recorder.EndRecording();
            }
            catch (Exception ex)
            {
                error = ex is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : ex;
                recorder.EndRecording()?.Dispose();
                return null;
            }
            finally
            {
                Finish(sketch);
                recorder.Dispose();
            }
        }

        /// <summary>
        /// Writes a PNG of the sketch's frame <paramref name="frames"/> at
        /// <paramref name="scale"/> times its size (fractions allowed: a 1000-px
        /// sketch at 7.2x prints 7200 px). The PNG carries the seed and [Tweak]
        /// values like any PNG a sketch saves.
        /// </summary>
        public static PrintExportResult SavePng(Func<Sketch> factory, int seed, IReadOnlyDictionary<string, string> tweaks, int frames,
                                                float scale, string path, IProgress<double>? progress = null, CancellationToken cancel = default)
        {
            scale = Math.Clamp(scale, 0.1f, 64f);
            using var picture = Record(factory, seed, tweaks, frames, out int w, out int h, out var recordError);
            if (picture == null)
                return SavePngRaster(factory, seed, tweaks, frames, scale, path, recordError, progress, cancel);

            int outW = Math.Max(1, (int)Math.Round(w * scale));
            int outH = Math.Max(1, (int)Math.Round(h * scale));
            var metadata = Metadata(factory, seed, tweaks, frames, outW, outH);

            using var file = File.Create(path);
            using (var png = new PngStreamWriter(file, outW, outH, metadata))
            {
                // Surfaces are premultiplied; ReadPixels converts each band to
                // the straight alpha PNG wants.
                using var band = SKSurface.Create(new SKImageInfo(outW, BandRows, SKColorType.Rgba8888, SKAlphaType.Premul))
                                 ?? throw new InvalidOperationException("No hay memoria para una banda de ese ancho.");
                using var pixels = new SKBitmap(new SKImageInfo(outW, BandRows, SKColorType.Rgba8888, SKAlphaType.Unpremul));
                for (int top = 0; top < outH; top += BandRows)
                {
                    cancel.ThrowIfCancellationRequested();
                    int rows = Math.Min(BandRows, outH - top);
                    var canvas = band.Canvas;
                    canvas.Clear(SKColors.Transparent);
                    canvas.Save();
                    canvas.Translate(0, -top);
                    canvas.Scale(scale);
                    canvas.ClipRect(new SKRect(0, 0, w, h));
                    canvas.DrawPicture(picture);
                    canvas.Restore();
                    canvas.Flush();
                    band.ReadPixels(pixels.Info, pixels.GetPixels(), pixels.RowBytes, 0, 0);
                    png.WriteRows(pixels.GetPixelSpan(), rows);
                    progress?.Report(Math.Min(1.0, (top + rows) / (double)outH));
                }
            }
            return new PrintExportResult(path, outW, outH, true, null);
        }

        /// <summary>Fallback for sketches that read pixels: draw everything at the big size in memory.</summary>
        private static PrintExportResult SavePngRaster(Func<Sketch> factory, int seed, IReadOnlyDictionary<string, string> tweaks, int frames,
                                                       float scale, string path, Exception? why, IProgress<double>? progress, CancellationToken cancel)
        {
            Sketch? sketch = null;
            SKSurface? surface = null;
            try
            {
                sketch = Prepare(factory, seed, tweaks);
                float effective = scale;
                surface = NewScaledSurface(sketch.Width, sketch.Height, ref effective);
                var s = sketch;
                sketch.SizeChanged += (nw, nh) =>
                {
                    surface?.Dispose();
                    effective = scale;
                    surface = NewScaledSurface(nw, nh, ref effective);
                    s.SetCanvas(surface.Canvas, surface);
                };
                sketch.SetCanvas(surface.Canvas, surface);
                surface.Canvas.Save();
                surface.Canvas.Scale(effective);
                sketch.Setup();
                surface.Canvas.Restore();
                RunFrames(sketch, frames, () => surface!.Canvas, effective, cancel);
                surface!.Canvas.Flush();

                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                var bytes = PngText.Add(data.ToArray(), Metadata(factory, seed, tweaks, frames, image.Width, image.Height));
                File.WriteAllBytes(path, bytes);
                progress?.Report(1);

                string warning = $"Exportado como píxeles, no como vectores: el sketch lee píxeles ({why?.GetType().Name}: {why?.Message}).";
                if (effective < scale)
                    warning += $" Se limitó a {effective:0.##}x para no pasar de {MaxRasterSide} px.";
                return new PrintExportResult(path, image.Width, image.Height, false, warning);
            }
            finally
            {
                Finish(sketch);
                surface?.Dispose();
            }
        }

        /// <summary>
        /// Writes an SVG of the sketch's frame <paramref name="frames"/>: real
        /// vectors at the sketch's size (scale it in any vector editor). With
        /// <paramref name="plotter"/>, it's tidied for a pen plotter -- see
        /// <see cref="PlotterSvg"/>.
        /// </summary>
        public static PrintExportResult SaveSvg(Func<Sketch> factory, int seed, IReadOnlyDictionary<string, string> tweaks, int frames,
                                                string path, PlotterOptions? plotter = null)
        {
            using var picture = Record(factory, seed, tweaks, frames, out int w, out int h, out var error);
            if (picture == null)
                throw new InvalidOperationException($"Este sketch no se puede exportar como SVG: lee píxeles o falló ({error?.GetType().Name}: {error?.Message}).", error);

            string svg = ToSvg(picture, w, h);
            string? warning = null;
            if (plotter != null)
            {
                var result = PlotterSvg.Optimize(svg, plotter);
                svg = result.Svg;
                warning = $"Plotter: {result.Elements} trazos en {result.Pens} pluma(s); recorrido en el aire {result.TravelBefore:0} -> {result.TravelAfter:0} px.";
            }
            File.WriteAllText(path, svg);
            return new PrintExportResult(path, w, h, true, warning);
        }

        /// <summary>Plays a recording into Skia's SVG backend.</summary>
        internal static string ToSvg(SKPicture picture, int width, int height)
        {
            using var stream = new MemoryStream();
            using (var svgCanvas = SKSvgCanvas.Create(new SKRect(0, 0, width, height), stream))
            {
                svgCanvas.ClipRect(new SKRect(0, 0, width, height));
                svgCanvas.DrawPicture(picture);
            }
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

        private static Sketch Prepare(Func<Sketch> factory, int seed, IReadOnlyDictionary<string, string> tweaks)
        {
            var sketch = factory();
            sketch.IgnoresAudio = true; // no sound, MIDI or microphone while exporting
            sketch.ApplySeed(seed);
            if (tweaks.Count > 0)
                sketch.ApplyTweakLiterals(tweaks);
            sketch.HideTweaks();        // the panel is not part of the artwork
            sketch.VirtualMillis = 0;
            return sketch;
        }

        /// <summary>Frames 1..frames in virtual time, like a reel (Millis() follows the frame rate).</summary>
        private static void RunFrames(Sketch sketch, int frames, Func<SKCanvas> canvasForFrame, float scale, CancellationToken cancel = default)
        {
            double fps = Math.Clamp(sketch.TargetFrameRate, 1, 240);
            for (int i = 0; i < Math.Max(1, frames); i++)
            {
                cancel.ThrowIfCancellationRequested();
                if (i > 0 && !sketch.IsLooping)
                    break;
                sketch.VirtualMillis = i * 1000.0 / fps;
                var canvas = canvasForFrame();
                int save = canvas.Save();
                if (scale != 1f)
                    canvas.Scale(scale);
                try
                {
                    sketch.RenderFrame();
                }
                finally
                {
                    canvas.RestoreToCount(save);
                }
            }
        }

        private static SKSurface NewScaledSurface(int w, int h, ref float scale)
        {
            float maxScale = MaxRasterSide / (float)Math.Max(1, Math.Max(w, h));
            scale = Math.Min(scale, maxScale);
            var surface = SKSurface.Create(new SKImageInfo(Math.Max(1, (int)Math.Round(w * scale)), Math.Max(1, (int)Math.Round(h * scale)),
                                                           SKColorType.Rgba8888, SKAlphaType.Premul))
                          ?? throw new InvalidOperationException("No hay memoria para una imagen de ese tamaño.");
            surface.Canvas.Clear(new SKColor(200, 200, 200));
            return surface;
        }

        private static IEnumerable<KeyValuePair<string, string>> Metadata(Func<Sketch> factory, int seed, IReadOnlyDictionary<string, string> tweaks,
                                                                          int frames, int width, int height)
        {
            var probe = factory();
            probe.IgnoresAudio = true;
            probe.ApplySeed(seed);
            if (tweaks.Count > 0)
                probe.ApplyTweakLiterals(tweaks);
            probe.FrameCount = Math.Max(1, frames);
            var list = SketchImageInfo.Describe(probe).ToList();
            list.Add(new("DanaProcessing.PrintSize", $"{width}x{height}"));
            (probe as IDisposable)?.Dispose();
            return list;
        }

        private static void Finish(Sketch? sketch)
        {
            if (sketch == null)
                return;
            try { sketch.RaiseStopped(); } catch { /* an OnStop bug must not lose the export */ }
            (sketch as IDisposable)?.Dispose();
        }
    }
}
