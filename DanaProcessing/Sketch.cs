using SkiaSharp;
using System.Xml.Linq;

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
        ///
        /// Pass RendererKind.Renderer3D to opt into the (not yet implemented)
        /// GPU 3D pipeline, mirroring Processing's size(w, h, P3D) — like
        /// Processing, the renderer can't change after it's been set once.
        /// </summary>
        public void Size(int w, int h, RendererKind renderer = RendererKind.Renderer2D)
        {
            if (renderer == RendererKind.Renderer3D)
                throw new NotImplementedException("RendererKind.Renderer3D todavía no está implementado — ver los comentarios de IGraphicsBackend para la hoja de ruta 3D.");
            SetRenderer(renderer);
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

        /// <summary>
        /// Halts the calling thread for ms milliseconds, like Processing's
        /// delay() — https://processing.org/reference/delay_.html. Draw()
        /// runs on whatever thread the host pumps its render loop on, so a
        /// long Delay() here blocks that same thread/frame — exactly like
        /// Processing blocking its own animation thread. NOT meant for
        /// pacing smooth animation (use FrameRate() for that); it's meant
        /// for sketches that only need to redraw occasionally and want to
        /// spend the rest of the time idle instead of burning CPU on frames
        /// nobody will see change (a slow-updating clock, a sensor readout,
        /// a battery-friendly ambient display...).
        /// </summary>
        public void Delay(int ms) => System.Threading.Thread.Sleep(Math.Max(0, ms));

        /// <summary>
        /// Runs a no-argument method of this sketch on a background thread
        /// and returns immediately, like Processing's thread(functionName) —
        /// https://processing.org/reference/thread_.html. Looks the method
        /// up by name via reflection (public or private, instance method),
        /// exactly like Processing's own string-based API. Useful for slow
        /// work (a big computation, a network call) that shouldn't block
        /// Draw() — write your results to a field the background method
        /// owns, and have Draw() just read that field each frame; there's no
        /// automatic locking here, so keep the handoff to something simple
        /// (a single field write/read, or your own lock) to avoid tearing.
        /// </summary>
        public void Thread(string methodName)
        {
            var method = GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            if (method == null)
                throw new ArgumentException($"Thread(): no se encontró un método sin parámetros llamado '{methodName}' en {GetType().Name}.", nameof(methodName));

            System.Threading.Tasks.Task.Run(() => method.Invoke(this, null));
        }

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

        /// <summary>
        /// Width of the sketch's own backing buffer in actual pixels, like
        /// Processing's pixelWidth — https://processing.org/reference/pixelWidth.html.
        /// Since DanaProcessing always draws at 1:1 (PixelDensity() is
        /// validated but has no effect — see its doc comment above), this is
        /// always equal to Width; it exists so sketches ported from
        /// Processing that read pixelWidth (e.g. when indexing into
        /// LoadPixels()'s array by hand) keep working without a rewrite.
        /// </summary>
        public int PixelWidth => Width;

        /// <summary>Height of the sketch's backing buffer in actual pixels — see PixelWidth.</summary>
        public int PixelHeight => Height;

        /// <summary>Whether the sketch is currently requesting fullscreen, like Processing's fullScreen() being active. Set by FullScreen(); a host should watch FullScreenRequested rather than poll this, but it's here for sketches that want to read their own state back.</summary>
        public bool IsFullScreen { get; private set; }

        /// <summary>Raised when the sketch calls FullScreen(fullScreen) — a host that owns an actual window should subscribe and enter/exit fullscreen accordingly, the same way it reacts to SizeChanged.</summary>
        public event Action<bool>? FullScreenRequested;

        /// <summary>Requests the sketch's window go fullscreen (or leave fullscreen), like Processing's fullScreen()/fullScreen(false) — https://processing.org/reference/fullScreen_.html. Call from Setup() (or anywhere) rather than only at startup; unlike real Processing, nothing stops you from toggling it back off later.</summary>
        public void FullScreen(bool fullScreen = true)
        {
            IsFullScreen = fullScreen;
            FullScreenRequested?.Invoke(fullScreen);
        }

        /// <summary>Raised when the sketch calls WindowMove(x, y) — like Processing's windowMove() — https://processing.org/reference/windowMove_.html. A host with an actual window should subscribe and reposition it.</summary>
        public event Action<int, int>? WindowMoveRequested;

        /// <summary>Requests the sketch's window move to (x, y) in screen coordinates, like Processing's windowMove().</summary>
        public void WindowMove(int x, int y) => WindowMoveRequested?.Invoke(x, y);

        /// <summary>Classic-API alias for WindowMove(x, y), like Processing's older setLocation() (pre-windowMove()).</summary>
        public void SetLocation(int x, int y) => WindowMove(x, y);

        /// <summary>Raised when the sketch calls WindowResize(w, h) — like Processing's windowResize() — https://processing.org/reference/windowResize_.html. Distinct from Size(w, h): Size() tells the host what buffer size to render at; this asks the host to resize the actual OS window around it.</summary>
        public event Action<int, int>? WindowResizeRequested;

        /// <summary>Requests the sketch's OS window be resized to (w, h), like Processing's windowResize().</summary>
        public void WindowResize(int w, int h) => WindowResizeRequested?.Invoke(w, h);

        /// <summary>Whether the sketch's window is currently allowed to be resized by the user, like Processing's windowResizable() state. Defaults to false, matching Processing's own default.</summary>
        public bool IsWindowResizable { get; private set; }

        /// <summary>Raised when the sketch calls WindowResizable(resizable) — like Processing's windowResizable() — https://processing.org/reference/windowResizable_.html.</summary>
        public event Action<bool>? WindowResizableChanged;

        /// <summary>Allows or forbids the user resizing the sketch's window, like Processing's windowResizable().</summary>
        public void WindowResizable(bool resizable)
        {
            IsWindowResizable = resizable;
            WindowResizableChanged?.Invoke(resizable);
        }

        /// <summary>Classic-API alias for WindowResizable(resizable), like Processing's older setResizable().</summary>
        public void SetResizable(bool resizable) => WindowResizable(resizable);

        /// <summary>The sketch's current window title, like Processing's windowTitle() state. Empty until WindowTitle()/SetTitle() is called.</summary>
        public string WindowTitleText { get; private set; } = "";

        /// <summary>Raised when the sketch calls WindowTitle(title) — like Processing's windowTitle() — https://processing.org/reference/windowTitle_.html.</summary>
        public event Action<string>? WindowTitleChanged;

        /// <summary>Sets the sketch's window title, like Processing's windowTitle().</summary>
        public void WindowTitle(string title)
        {
            WindowTitleText = title ?? "";
            WindowTitleChanged?.Invoke(WindowTitleText);
        }

        /// <summary>Classic-API alias for WindowTitle(title), like Processing's older setTitle().</summary>
        public void SetTitle(string title) => WindowTitle(title);

        /// <summary>Raised when the sketch calls WindowRatio(w, h) — like Processing's windowRatio() — https://processing.org/reference/windowRatio_.html. A host should constrain interactive resizing to this aspect ratio from here on.</summary>
        public event Action<int, int>? WindowRatioRequested;

        /// <summary>Locks the sketch's window to the w:h aspect ratio while the user resizes it, like Processing's windowRatio().</summary>
        public void WindowRatio(int w, int h) => WindowRatioRequested?.Invoke(w, h);

        /// <summary>
        /// Runs once, before Setup() — like Processing's settings() —
        /// https://processing.org/reference/settings_.html. In real
        /// Processing this exists only because size() is special-cased by
        /// the preprocessor and can't take a variable inside setup(); in
        /// plain C# there's no such restriction, so overriding this is
        /// entirely optional here — Size() works fine directly inside
        /// Setup() too. It's provided so sketches ported from Processing
        /// that rely on the setup-order guarantee (settings() strictly
        /// before setup()) keep behaving the same way. The host is
        /// responsible for calling Settings() immediately before Setup().
        /// </summary>
        public virtual void Settings() { }

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

        /// <summary>Parses an already-in-memory JSON string as a JSONObject, like Processing's parseJSONObject(string) — https://processing.org/reference/parseJSONObject_.html. Use this instead of LoadJSONObject() when the JSON came from somewhere other than a file on disk (a network response, a string you built by hand, text pasted into the sketch...).</summary>
        public JSONObject ParseJSONObject(string json) => JSONObject.Parse(json);

        /// <summary>Parses an already-in-memory JSON string as a JSONArray, like Processing's parseJSONArray(string) — see ParseJSONObject().</summary>
        public JSONArray ParseJSONArray(string json) => JSONArray.Parse(json);

        /// <summary>Parses an already-in-memory XML string, like Processing's parseXML(string) — https://processing.org/reference/parseXML_.html. Use this instead of LoadXML() when the XML didn't come from a file.</summary>
        public XML ParseXML(string xml) => ParseXML(xml);


        // ============================================================================
        // PATCH 2/2 — PXML.cs
        // Insertar inmediatamente después de:
        //     public static XML Load(string path) => new XML(XElement.Load(path));
        // ============================================================================

        /// <summary>Parses an XML string directly (no file involved), like Processing's parseXML(string). Throws if the text isn't well-formed XML.</summary>
        public static XML Parse(string xml) => new XML(XElement.Parse(xml));


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