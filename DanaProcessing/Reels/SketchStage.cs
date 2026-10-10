using SkiaSharp;

namespace DanaProcessing.Reels
{
    /// <summary>
    /// Runs the user's sketch off-screen for the result part, in video time:
    /// AdvanceTo(t) renders exactly the frames the sketch would have drawn by
    /// t seconds at its own frame rate (so a 60 fps sketch in a 30 fps video
    /// still animates at its real speed), with Millis() following video time
    /// and the simulated input applied before every frame.
    ///
    /// Seeking backwards restarts the sketch from a fresh instance -- a sketch
    /// is a state machine, the only honest way to get "frame 40" is to draw
    /// frames 1..40.
    /// </summary>
    internal sealed class SketchStage : IDisposable
    {
        private readonly Func<Sketch> _factory;
        private readonly ReelScript _script;
        private readonly double _duration;

        private Sketch? _sketch;
        private SKSurface? _surface;
        private InputSimulator? _input;
        private int _frames;
        private double _fps = 60;
        private double _lastInputTime;

        public int Width { get; private set; } = 600;
        public int Height { get; private set; } = 400;
        public Exception? Error { get; private set; }
        public string ErrorContext { get; private set; } = "";
        public InputSimulator? Input => _input;
        /// <summary>Video time (seconds since the result started) the stage was last advanced to.</summary>
        public double Time { get; private set; }

        /// <summary>Most frames AdvanceTo() will draw in one call -- a long seek in the preview must not freeze the UI.</summary>
        public int MaxFramesPerAdvance { get; set; } = 400;

        public SketchStage(Func<Sketch> factory, ReelScript script, double duration)
        {
            _factory = factory;
            _script = script;
            _duration = duration;
            Reset();
        }

        public void Reset()
        {
            DisposeSketch();
            Error = null;
            ErrorContext = "";
            _frames = 0;
            _lastInputTime = 0;
            Time = 0;

            try
            {
                Reel.IsActive = true;
                _sketch = _factory();
            }
            catch (Exception ex)
            {
                Fail(ex, "constructor");
                return;
            }
            finally
            {
                Reel.IsActive = false;
            }

            _sketch.SizeChanged += OnSizeChanged;
            _sketch.VirtualMillis = 0;
            EnsureSurface(_sketch.Width, _sketch.Height);

            Run(_sketch.Setup, "Setup");

            _fps = Math.Clamp(_sketch.TargetFrameRate, 1, 240);
            EnsureSurface(_sketch.Width, _sketch.Height);
            _input = new InputSimulator(_script, _sketch.GetType(), Width, Height, _duration);

            // Start the mouse where the path starts, so frame 1 doesn't see a
            // jump from (0,0).
            if (_input.HasMouse)
            {
                var (x, y) = _input.PositionAt(0);
                _sketch.MouseX = _sketch.PMouseX = x;
                _sketch.MouseY = _sketch.PMouseY = y;
            }
        }

        private void OnSizeChanged(int w, int h) => EnsureSurface(w, h);

        private void EnsureSurface(int w, int h)
        {
            w = Math.Max(1, w);
            h = Math.Max(1, h);
            if (_surface != null && w == Width && h == Height)
                return;
            _surface?.Dispose();
            _surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
            _surface.Canvas.Clear(new SKColor(200, 200, 200)); // Processing's default grey, until the sketch paints
            Width = w;
            Height = h;
            _sketch?.SetCanvas(_surface.Canvas, _surface);
        }

        /// <summary>Draws every sketch frame due by <paramref name="seconds"/> of result time.</summary>
        public void AdvanceTo(double seconds)
        {
            if (seconds < Time - 1e-6)
                Reset();
            Time = seconds;

            if (_sketch == null || _surface == null || Error != null)
                return;

            int due = (int)Math.Floor(seconds * _fps + 1e-9) + 1; // frame 1 draws at t = 0
            int budget = MaxFramesPerAdvance;
            while (_frames < due && budget-- > 0 && Error == null)
            {
                double tau = _frames / _fps;
                _sketch.VirtualMillis = tau * 1000.0;

                if (_input != null)
                {
                    Run(() => _input.Apply(_sketch, _lastInputTime, tau), "input");
                    _lastInputTime = tau;
                }

                // noLoop(): draw the first frame only (Processing semantics).
                if (_sketch.IsLooping || _frames == 0)
                {
                    var canvas = _surface.Canvas;
                    canvas.Save();
                    Run(_sketch.RenderFrame, "Draw");
                    canvas.Restore();
                }
                _sketch.PMouseX = _sketch.MouseX;
                _sketch.PMouseY = _sketch.MouseY;
                _frames++;
            }
        }

        /// <summary>The sketch's current frame (null before anything could be drawn).</summary>
        public SKImage? Snapshot()
        {
            if (_surface == null)
                return null;
            _surface.Canvas.Flush();
            return _surface.Snapshot();
        }

        private void Run(Action a, string context)
        {
            if (Error != null)
                return;
            bool was = Reel.IsActive;
            Reel.IsActive = true; // only while the sketch's own code runs: a normal Run never sees it
            try
            {
                a();
            }
            catch (Exception ex)
            {
                Fail(ex, context);
            }
            finally
            {
                Reel.IsActive = was;
            }
        }

        private void Fail(Exception ex, string context)
        {
            Error = ex is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : ex;
            ErrorContext = context;
        }

        private void DisposeSketch()
        {
            if (_sketch != null)
            {
                _sketch.SizeChanged -= OnSizeChanged;
                (_sketch as IDisposable)?.Dispose();
                _sketch = null;
            }
        }

        public void Dispose()
        {
            DisposeSketch();
            _surface?.Dispose();
            _surface = null;
        }
    }
}
