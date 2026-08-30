using SkiaSharp;
using System;

namespace DanaProcessing
{
    /// <summary>
    /// Base class that a "sketch" (a Processing-style program) inherits from.
    /// Override Setup() to run once at start, and Draw() to run every frame.
    ///
    /// All drawing state and operations (Fill/Stroke/Rect/Ellipse/Text/
    /// beginShape/...) live in the shared GraphicsContext base class — see
    /// GraphicsContext.cs — so they behave identically whether you're drawing
    /// straight into the sketch or into an offscreen PGraphics buffer. This
    /// file only adds what's specific to *running* a sketch: lifecycle,
    /// per-frame state, and the canvas hookup.
    ///
    /// This class is `partial`: as we keep adding pieces of the Processing
    /// reference (https://processing.org/reference/), each area gets its own
    /// file — see Sketch.MathTrig.cs, Sketch.Input.cs, and
    /// Sketch.RandomNoise.cs alongside this file.
    /// </summary>
    public abstract partial class Sketch : GraphicsContext
    {
        protected Sketch()
        {
            Width = 600;
            Height = 400;
        }

        // --- Lifecycle state ---
        public int FrameCount { get; internal set; }

        /// <summary>
        /// Sets the sketch's canvas size. Call this from Setup() to request your
        /// initial size (like Processing's size(w, h)). The host also calls this
        /// internally to keep Width/Height in sync when the surface is resized.
        /// </summary>
        public void Size(int w, int h)
        {
            Width = w;
            Height = h;
        }

        public int TargetFrameRate { get; private set; } = 60;
        public bool IsLooping { get; private set; } = true;

        public void FrameRate(int fps) => TargetFrameRate = Math.Max(1, fps);
        public void NoLoop() => IsLooping = false;
        public void Loop() => IsLooping = true;

        public virtual void WindowResized() { }

        // --- Mouse state (position only — buttons/events live in Sketch.Input.cs) ---
        public float MouseX { get; internal set; }
        public float MouseY { get; internal set; }
        public float PMouseX { get; internal set; }
        public float PMouseY { get; internal set; }

        // --- Keyboard state (printable key — KeyCode/special keys live in Sketch.Input.cs) ---
        public char Key { get; internal set; }
        public bool IsKeyPressed { get; internal set; }

        /// <summary>
        /// Set internally by the host before Draw() runs each frame. Pass the
        /// SKSurface that owns `canvas` (not just the canvas itself) so
        /// Save()/SaveFrame() can read pixels back from it — a host that
        /// draws into a raster SKSurface each frame (the normal case) should
        /// pass that surface here. Pass null for `surface` only if the host
        /// has no CPU-readable surface to offer; Save() will then throw if
        /// called.
        /// </summary>
        internal void SetCanvas(SKCanvas canvas, SKSurface? surface = null)
        {
            Canvas = canvas;
            Surface = surface;
        }

        public virtual void Setup() { }
        public abstract void Draw();

        public virtual void KeyPressed() { }
        public virtual void KeyReleased() { }

        // =====================================================================
        // Image loading — drawing an already-loaded PImage/PGraphics/PShape
        // is shared logic in GraphicsContext.
        // =====================================================================

        /// <summary>Loads an image from disk. Throws if the file doesn't exist or isn't a decodable image.</summary>
        public PImage LoadImage(string path)
        {
            var bitmap = SKBitmap.Decode(path);
            if (bitmap == null)
                throw new InvalidOperationException($"No se pudo cargar la imagen: '{path}'. Verifica la ruta y el formato.");
            return new PImage(bitmap);
        }

        // =====================================================================
        // Utility functions (Processing-style)
        // =====================================================================

        // Not readonly: RandomSeed() (see Sketch.RandomNoise.cs) needs to swap
        // this out for a freshly-seeded instance.
        private static Random _rand = new Random();

        public float Random(float max) => (float)(_rand.NextDouble() * max);
        public float Random(float min, float max) => min + (float)(_rand.NextDouble() * (max - min));

        public float Map(float value, float start1, float stop1, float start2, float stop2) =>
            start2 + (stop2 - start2) * ((value - start1) / (stop1 - start1));

        public float Noise(float x) => PerlinNoise.Noise(x, 0);
        public float Noise(float x, float y) => PerlinNoise.Noise(x, y);

        // =====================================================================
        // Saving output — Save() itself lives on GraphicsContext (shared with
        // PGraphics); this adds Processing's frame-numbered variant, which
        // needs FrameCount.
        // =====================================================================

        /// <summary>
        /// Saves the current frame to an image file, substituting a run of
        /// '#' characters in the pattern with the current FrameCount,
        /// zero-padded to match the number of '#'s — like Processing's
        /// saveFrame(). E.g. with FrameCount 42, "frames/out-####.png"
        /// becomes "frames/out-0042.png". A pattern with no '#' saves once
        /// to that exact path every call (each call overwrites the last).
        /// Defaults to "screen-####.png" — Processing's own default is
        /// "screen-####.tif", but DanaProcessing has no TIFF encoder, so PNG
        /// is the more broadly useful stand-in.
        /// </summary>
        public void SaveFrame(string pattern = "screen-####.png") => Save(ResolveFramePattern(pattern, FrameCount));

        private static string ResolveFramePattern(string pattern, int frameCount)
        {
            int hashStart = pattern.IndexOf('#');
            if (hashStart == -1)
                return pattern;

            int hashEnd = hashStart;
            while (hashEnd < pattern.Length && pattern[hashEnd] == '#')
                hashEnd++;
            int hashLength = hashEnd - hashStart;

            string number = frameCount.ToString().PadLeft(hashLength, '0');
            return pattern.Substring(0, hashStart) + number + pattern.Substring(hashEnd);
        }

        // =====================================================================
        // Logging
        // =====================================================================

        public void Println(object message) => DanaLogger.Info(message?.ToString() ?? "null");
        public void LogWarning(object message) => DanaLogger.Warn(message?.ToString() ?? "null");
        public void LogError(object message) => DanaLogger.Error(message?.ToString() ?? "null");
    }
}