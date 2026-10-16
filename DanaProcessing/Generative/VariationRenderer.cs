using System.Threading;
using SkiaSharp;

namespace DanaProcessing
{
    /// <summary>What changes from one variation to the next on the variations sheet.</summary>
    public enum VariationMode
    {
        /// <summary>A new seed each: same code, same [Tweak] values, different Random()/Noise().</summary>
        Seeds,
        /// <summary>Same seed, random [Tweak] values inside each slider's range.</summary>
        Tweaks,
        /// <summary>Both at once.</summary>
        SeedsAndTweaks,
    }

    /// <summary>One cell of the variations sheet: how it was made, and what it looked like.</summary>
    public sealed record Variation(int Index, int Seed, IReadOnlyDictionary<string, string> Tweaks, SKImage? Thumbnail, string? Error);

    /// <summary>
    /// The variations sheet: runs a sketch several times off-screen -- each with
    /// its own seed and/or its own [Tweak] values -- and keeps a thumbnail of
    /// each, so you can pick the best one and keep its values. Every run is a
    /// fresh instance in preview mode (no sound, no MIDI, no microphone, no
    /// panel), driven frame by frame in virtual time like a reel, so
    /// Millis()-based animation comes out the same on every machine.
    /// </summary>
    public static class VariationRenderer
    {
        /// <summary>
        /// The seeds and [Tweak] values for <paramref name="count"/> variations
        /// (cheap -- no drawing). <paramref name="baseSeed"/> also seeds the
        /// choice of random values, so the same arguments give the same sheet.
        /// <paramref name="currentTweaks"/> are the values you have now (from
        /// the running sketch): cell 0 uses them, and Seeds mode keeps them.
        /// </summary>
        public static List<(int Seed, IReadOnlyDictionary<string, string> Tweaks)> Plan(
            Func<Sketch> factory, int count, VariationMode mode, int baseSeed,
            IReadOnlyDictionary<string, string>? currentTweaks = null)
        {
            var current = currentTweaks ?? new Dictionary<string, string>();
            var rng = new Random(baseSeed);
            var plan = new List<(int, IReadOnlyDictionary<string, string>)>();
            for (int i = 0; i < count; i++)
            {
                int seed = mode == VariationMode.Tweaks ? baseSeed : (i == 0 ? baseSeed : rng.Next(1, 1_000_000));
                // Cell 0 is always "what you have now"; Seeds mode keeps your values everywhere.
                IReadOnlyDictionary<string, string> tweaks = current;
                if (mode != VariationMode.Seeds && i > 0)
                {
                    var probe = factory();
                    tweaks = probe.RandomizeTweaks(rng);
                    (probe as IDisposable)?.Dispose();
                }
                plan.Add((seed, tweaks));
            }
            return plan;
        }

        /// <summary>
        /// Draws one variation: a fresh sketch, seeded, with <paramref name="tweaks"/>
        /// applied before Setup(), run for <paramref name="frames"/> frames (at its
        /// own frame rate, in virtual time) and scaled so its longest side is at
        /// most <paramref name="thumbnailSize"/>. Never throws: a sketch that fails
        /// comes back with <see cref="Variation.Error"/> set.
        /// </summary>
        public static Variation Render(Func<Sketch> factory, int index, int seed, IReadOnlyDictionary<string, string> tweaks,
                                       int frames = 60, int thumbnailSize = 320, CancellationToken cancel = default)
        {
            Sketch? sketch = null;
            SKSurface? surface = null;
            try
            {
                sketch = factory();
                sketch.IgnoresAudio = true;
                sketch.ApplySeed(seed);
                if (tweaks.Count > 0)
                    sketch.ApplyTweakLiterals(tweaks);
                sketch.HideTweaks();

                surface = NewSurface(sketch.Width, sketch.Height);
                var s = sketch;
                sketch.SizeChanged += (w, h) =>
                {
                    surface?.Dispose();
                    surface = NewSurface(w, h);
                    s.SetCanvas(surface.Canvas, surface);
                };
                sketch.SetCanvas(surface.Canvas, surface);
                sketch.VirtualMillis = 0;
                sketch.Setup();

                double fps = Math.Clamp(sketch.TargetFrameRate, 1, 240);
                for (int i = 0; i < Math.Max(1, frames); i++)
                {
                    cancel.ThrowIfCancellationRequested();
                    if (i > 0 && !sketch.IsLooping)
                        break; // NoLoop(): one frame, like Processing
                    sketch.VirtualMillis = i * 1000.0 / fps;
                    var canvas = surface!.Canvas;
                    canvas.Save();
                    sketch.RenderFrame();
                    canvas.Restore();
                }

                surface!.Canvas.Flush();
                using var full = surface.Snapshot();
                return new Variation(index, seed, tweaks, Shrink(full, thumbnailSize), null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var inner = ex is System.Reflection.TargetInvocationException { InnerException: { } e } ? e : ex;
                return new Variation(index, seed, tweaks, null, $"{inner.GetType().Name}: {inner.Message}");
            }
            finally
            {
                try { sketch?.RaiseStopped(); } catch { /* a failing OnStop must not lose the sheet */ }
                (sketch as IDisposable)?.Dispose();
                surface?.Dispose();
            }
        }

        private static SKSurface NewSurface(int w, int h)
        {
            var surface = SKSurface.Create(new SKImageInfo(Math.Max(1, w), Math.Max(1, h), SKColorType.Rgba8888, SKAlphaType.Premul));
            surface.Canvas.Clear(new SKColor(200, 200, 200)); // Processing's default grey, until the sketch paints
            return surface;
        }

        private static SKImage Shrink(SKImage full, int maxSize)
        {
            float scale = Math.Min(1f, maxSize / (float)Math.Max(full.Width, full.Height));
            int w = Math.Max(1, (int)(full.Width * scale));
            int h = Math.Max(1, (int)(full.Height * scale));
            using var small = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
            small.Canvas.Scale(scale);
            small.Canvas.DrawImage(full, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            small.Canvas.Flush();
            return small.Snapshot();
        }
    }
}
