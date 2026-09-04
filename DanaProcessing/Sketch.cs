using SkiaSharp;

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
        /// initial size (like Processing's size(w, h)) — a host should treat this
        /// as the sketch telling IT how big to be, not the other way around.
        /// Raises SizeChanged so a host can react (resize itself, re-run layout)
        /// the moment the size actually changes, rather than only finding out
        /// on the next frame it happens to render.
        /// </summary>
        public void Size(int w, int h)
        {
            if (w == Width && h == Height)
                return;
            Width = w;
            Height = h;
            SizeChanged?.Invoke(w, h);
        }

        /// <summary>
        /// Raised whenever Size(w, h) actually changes the canvas dimensions
        /// (not raised if the new size is the same as the current one). A host
        /// that embeds the sketch (like AvaloniaSketchCanvas) should listen to
        /// this so IT sizes itself to match the sketch, instead of stretching
        /// the sketch to fill whatever space the host's own layout happens to
        /// give it.
        /// </summary>
        public event Action<int, int>? SizeChanged;

        public int TargetFrameRate { get; private set; } = 60;
        public bool IsLooping { get; private set; } = true;

        public void FrameRate(int fps) => TargetFrameRate = Math.Max(1, fps);
        public void NoLoop() => IsLooping = false;
        public void Loop() => IsLooping = true;

        /// <summary>Raised when the sketch calls Redraw() — meaningful only while NoLoop() is in effect, like Processing's redraw() forcing exactly one extra frame. The host should subscribe and render a single frame in response; looping sketches can ignore this since they're already rendering continuously.</summary>
        public event Action? RedrawRequested;

        /// <summary>Requests a single extra frame be drawn even while NoLoop() is in effect, like Processing's redraw().</summary>
        public void Redraw() => RedrawRequested?.Invoke();

        /// <summary>Raised when the sketch calls Exit(), like Processing's exit() — DanaProcessing doesn't own a process to terminate itself, so whatever hosts the sketch (a window, a canvas control) should subscribe and close/stop accordingly.</summary>
        public event Action? ExitRequested;

        /// <summary>Requests the sketch/host shut down, like Processing's exit().</summary>
        public void Exit() => ExitRequested?.Invoke();

        /// <summary>Width of the screen/display the sketch is running on, like Processing's displayWidth. 0 until the host sets it — a host with no meaningful "display" concept (e.g. rendering into an arbitrary embedded control) can simply leave this unset.</summary>
        public int DisplayWidth { get; internal set; }

        /// <summary>Height of the screen/display the sketch is running on, like Processing's displayHeight.</summary>
        public int DisplayHeight { get; internal set; }

        public virtual void WindowResized() { }

        /// <summary>Whether the sketch's window currently has input focus, like Processing's focused variable. Defaults to true; a host that embeds the sketch should update this via SetFocused() as its window gains/loses focus.</summary>
        public bool Focused { get; internal set; } = true;

        internal void SetFocused(bool focused) => Focused = focused;

        /// <summary>Ratio between physical and logical pixels on the display the sketch is running on (2 for a typical "Retina"/HiDPI display, 1 otherwise), like Processing's displayDensity(). Fixed at 1 here — DanaProcessing draws everything at logical-pixel resolution and leaves any HiDPI scaling to the host — call PixelDensity() only to match Processing's API shape; it has no effect.</summary>
        public int DisplayDensity() => 1;

        /// <summary>
        /// Like Processing's pixelDensity(density) — requests the sketch's
        /// backing buffer be rendered at `density` pixels per logical pixel
        /// so it looks sharp on HiDPI displays. DanaProcessing's canvas size
        /// is entirely host-controlled (see Size()/SizeChanged), so this
        /// can't actually resize anything from in here; it only validates
        /// the argument the way Processing does (1 or 2), for sketches
        /// ported from Processing that call it defensively in Setup().
        /// </summary>
        public void PixelDensity(int density)
        {
            if (density != 1 && density != 2)
                throw new ArgumentException("PixelDensity() solo acepta 1 o 2, igual que Processing.");
        }

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

        /// <summary>
        /// Starts loading an image on a background thread and returns
        /// immediately, like Processing's requestImage(). The returned
        /// PImage has IsLoaded == false (and Width/Height read as 0) until
        /// the background decode finishes, at which point it silently swaps
        /// in the real bitmap — check IsLoaded (or watch for Width/Height
        /// becoming nonzero) in Draw() before using it, the same way a
        /// Processing sketch checks img.width != 0. A decode failure leaves
        /// the placeholder permanently unloaded and logs the error via
        /// DanaLogger, rather than throwing on a background thread where
        /// nothing could catch it.
        /// </summary>
        public PImage RequestImage(string path)
        {
            var placeholder = PImage.CreatePlaceholder();
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var bitmap = SKBitmap.Decode(path);
                    if (bitmap == null)
                        throw new InvalidOperationException($"No se pudo cargar la imagen: '{path}'. Verifica la ruta y el formato.");
                    placeholder.ReplaceBitmap(bitmap);
                }
                catch (Exception ex)
                {
                    DanaLogger.ErrorFromException(ex, $"RequestImage('{path}') falló en segundo plano");
                }
            });
            return placeholder;
        }

        // =====================================================================
        // Fonts — https://processing.org/reference/createFont_.html and
        // loadFont_.html. See the PFont class remark for how loadFont() here
        // differs from Processing's own .vlw-based version.
        // =====================================================================

        /// <summary>Creates a font from an installed system font family at a given size, like Processing's createFont(name, size). Call TextFont(font) to actually start drawing with it.</summary>
        public PFont CreateFont(string fontFamily, float size) => PFont.CreateFromFamily(fontFamily, size);

        /// <summary>Loads a font file from disk at a given size, like Processing's loadFont(path) — see the PFont class remark for the one difference from Processing's own (.vlw-based) loadFont().</summary>
        public PFont LoadFont(string path, float size = 32) => PFont.LoadFromFile(path, size);

        // =====================================================================
        // Data — https://processing.org/reference/loadStrings_.html and
        // siblings (loadBytes/loadJSONObject/loadJSONArray/loadXML/loadTable
        // and their save* counterparts). See PJson.cs/PXml.cs/PTable.cs for
        // the JSONObject/JSONArray/XML/Table types themselves.
        // =====================================================================

        /// <summary>Reads a text file as an array of lines, like Processing's loadStrings().</summary>
        public string[] LoadStrings(string path) => File.ReadAllLines(path);

        /// <summary>Writes an array of lines to a text file, one per line, like Processing's saveStrings().</summary>
        public void SaveStrings(string path, string[] lines) => File.WriteAllLines(path, lines);

        /// <summary>Reads a file's raw bytes, like Processing's loadBytes().</summary>
        public byte[] LoadBytes(string path) => File.ReadAllBytes(path);

        /// <summary>Writes raw bytes to a file, like Processing's saveBytes().</summary>
        public void SaveBytes(string path, byte[] data) => File.WriteAllBytes(path, data);

        /// <summary>Loads a JSON file as a JSONObject, like Processing's loadJSONObject(path). Throws if the file's top-level value isn't a JSON object — use LoadJSONArray() for a file whose top level is an array.</summary>
        public JSONObject LoadJSONObject(string path) => JSONObject.Load(path);

        /// <summary>Writes a JSONObject to a file, pretty-printed, like Processing's saveJSONObject(json, path).</summary>
        public void SaveJSONObject(JSONObject json, string path) => json.Save(path);

        /// <summary>Loads a JSON file as a JSONArray, like Processing's loadJSONArray(path).</summary>
        public JSONArray LoadJSONArray(string path) => JSONArray.Load(path);

        /// <summary>Writes a JSONArray to a file, pretty-printed, like Processing's saveJSONArray(json, path).</summary>
        public void SaveJSONArray(JSONArray json, string path) => json.Save(path);

        /// <summary>Loads an XML file, like Processing's loadXML(path).</summary>
        public XML LoadXML(string path) => XML.Load(path);

        /// <summary>Writes an XML element (and its children) to a file, like Processing's saveXML(xml, path).</summary>
        public void SaveXML(XML xml, string path) => xml.Save(path);

        /// <summary>Loads a CSV file as a Table, like Processing's loadTable(path, options). `options` supports "header" for a first line naming the columns.</summary>
        public Table LoadTable(string path, string options = "") => Table.LoadCsv(path, options);

        /// <summary>Writes a Table to a CSV file, like Processing's saveTable(table, path).</summary>
        public void SaveTable(Table table, string path) => table.SaveCsv(path);

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

        /// <summary>Writes text to the console with no trailing newline and none of Println()'s timestamp/coloring, like Processing's print() — meant for building up a line piece by piece across several calls (finish it with Println() to add the newline).</summary>
        public void Print(object message) => Console.Write(message);

        public void Println(object message) => DanaLogger.Info(message?.ToString() ?? "null");
        public void LogWarning(object message) => DanaLogger.Warn(message?.ToString() ?? "null");
        public void LogError(object message) => DanaLogger.Error(message?.ToString() ?? "null");
    }
}