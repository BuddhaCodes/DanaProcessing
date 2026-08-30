using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using DanaProcessing;

namespace DanaProcessing.AvaloniaHost
{
    /// <summary>
    /// Avalonia equivalent of DanaProcessing.Wpf's SketchCanvas — an embeddable
    /// control that hosts a Sketch, cross-platform (Windows/Mac/Linux).
    ///
    /// Unlike WPF (which has a ready-made SKElement via SkiaSharp.Views.WPF),
    /// Avalonia exposes its internal Skia surface through a "lease" API instead,
    /// and that lease only hands out an SKCanvas — never the SKSurface that owns
    /// it (Avalonia manages that surface internally and doesn't expose it). Since
    /// Sketch.Save()/SaveFrame() need a real SKSurface to read pixels back from,
    /// this control keeps its own offscreen SKSurface, has the sketch draw into
    /// that instead of the leased canvas, and blits the result onto the leased
    /// canvas afterward. One extra copy per frame, but it's what makes Save()
    /// work under Avalonia at all.
    /// </summary>
    public class AvaloniaSketchCanvas : Control
    {
        private Sketch _sketch;
        private readonly DispatcherTimer _timer;
        private bool _didSetup = false;
        private int _lastKnownFrameRate = -1;

        private bool _crashed = false;
        private Exception? _crashException = null;
        private string _crashContext = "";

        // --- Our own offscreen surface, sized in DIPs (matching what we pass
        // to _sketch.Size(...)) — recreated whenever that size changes. This
        // is what gives Sketch.Surface something real to read pixels from. ---
        private SKSurface? _offscreenSurface;
        private int _offscreenWidth = -1;
        private int _offscreenHeight = -1;

        public AvaloniaSketchCanvas(Sketch sketch)
        {
            _sketch = sketch;
            Focusable = true;

            PointerMoved += OnPointerMoved;
            PointerPressed += (s, e) => Focus(); // click-to-focus, like a canvas in a web page
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;

            _timer = new DispatcherTimer();
            SetTimerInterval(sketch.TargetFrameRate);
            _timer.Tick += (s, e) =>
            {
                if (_sketch.TargetFrameRate != _lastKnownFrameRate)
                    SetTimerInterval(_sketch.TargetFrameRate);

                if (_sketch.IsLooping || _crashed)
                    InvalidateVisual();
            };
            _timer.Start();
        }

        private void SetTimerInterval(int fps)
        {
            _lastKnownFrameRate = fps;
            _timer.Interval = TimeSpan.FromSeconds(1.0 / fps);
        }

        /// <summary>
        /// Swaps in a new sketch and resets all engine state, so it starts fresh
        /// (Setup() runs again on the next frame). Used by the IDE's "Run" button.
        /// </summary>
        public void LoadSketch(Sketch newSketch)
        {
            _sketch = newSketch;
            _didSetup = false;
            _crashed = false;
            _crashException = null;
            _crashContext = "";
            _lastKnownFrameRate = -1; // forces the timer interval to resync on the next tick
        }

        private void RunSafely(Action userCode, string context)
        {
            if (_crashed)
                return;
            try
            {
                userCode();
            }
            catch (Exception ex)
            {
                _crashed = true;
                _crashException = ex;
                _crashContext = context;
                DanaLogger.ErrorFromException(ex, $"Error en {context}()");
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            // Both pointer coordinates and our draw calls live in DIPs here
            // (the sketch draws into our own offscreen surface at DIP size;
            // the scaling-to-physical-pixels only happens when we blit that
            // surface onto the leased canvas), so no manual DPI conversion
            // is needed here — unlike WPF.
            var pos = e.GetPosition(this);
            _sketch.PMouseX = _sketch.MouseX;
            _sketch.PMouseY = _sketch.MouseY;
            _sketch.MouseX = (float)pos.X;
            _sketch.MouseY = (float)pos.Y;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            _sketch.IsKeyPressed = true;
            SetSketchKeyFrom(e.Key);
            RunSafely(_sketch.KeyPressed, "KeyPressed");
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            _sketch.IsKeyPressed = false;
            SetSketchKeyFrom(e.Key);
            RunSafely(_sketch.KeyReleased, "KeyReleased");
        }

        private void SetSketchKeyFrom(Key key)
        {
            if (key >= Key.A && key <= Key.Z)
                _sketch.Key = (char)('a' + (key - Key.A));
            else if (key >= Key.D0 && key <= Key.D9)
                _sketch.Key = (char)('0' + (key - Key.D0));
            else if (key == Key.Space)
                _sketch.Key = ' ';
            else if (key == Key.OemPlus || key == Key.Add)
                _sketch.Key = '+';
            else if (key == Key.OemMinus || key == Key.Subtract)
                _sketch.Key = '-';
            else
                _sketch.Key = '\0';
        }

        public override void Render(DrawingContext context)
        {
            context.Custom(new SketchDrawOperation(new Rect(Bounds.Size), this));
        }

        /// <summary>
        /// Makes sure _offscreenSurface exists and matches (width, height) in
        /// DIPs, recreating it (and disposing the old one) if the size changed
        /// or it doesn't exist yet. No-op if the size is unchanged.
        /// </summary>
        private void EnsureOffscreenSurface(int width, int height)
        {
            if (_offscreenSurface != null && width == _offscreenWidth && height == _offscreenHeight)
                return;

            _offscreenSurface?.Dispose();
            _offscreenSurface = null;

            if (width <= 0 || height <= 0)
                return; // control not laid out yet; try again next frame

            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            _offscreenSurface = SKSurface.Create(info);
            _offscreenWidth = width;
            _offscreenHeight = height;
        }

        /// <summary>Called by SketchDrawOperation once it has a real (leased) SKCanvas in hand.</summary>
        /// <summary>Called by SketchDrawOperation once it has a real (leased) SKCanvas in hand.</summary>
        internal void PaintSketch(SKCanvas leasedCanvas, double scaling, int width, int height)
        {
            EnsureOffscreenSurface(width, height);
            if (_offscreenSurface == null)
                return; // width/height not yet valid — nothing to draw

            var offscreenCanvas = _offscreenSurface.Canvas;

            // The sketch draws into our own offscreen surface — not the leased
            // canvas — so Sketch.Surface is real and Save()/SaveFrame() work.
            _sketch.SetCanvas(offscreenCanvas, _offscreenSurface);

            bool sizeChanged = width != _sketch.Width || height != _sketch.Height;
            _sketch.Size(width, height);

            // IMPORTANT: unlike the leased canvas (fresh matrix every frame),
            // this offscreen canvas is OUR OWN and persists across frames —
            // so any Translate()/Rotate() the sketch makes outside a
            // PushMatrix()/PopMatrix() pair (like Draw()'s opening Translate)
            // would otherwise accumulate forever, one frame's transform
            // compounding on the last. Save()/Restore() around the whole
            // per-frame draw resets it back to identity for the next frame,
            // the same way a brand-new canvas would each time.
            offscreenCanvas.Save();

            if (!_didSetup)
            {
                RunSafely(_sketch.Setup, "Setup");
                _didSetup = true;
            }
            else if (sizeChanged)
            {
                RunSafely(_sketch.WindowResized, "WindowResized");
            }

            if (!_crashed)
            {
                RunSafely(() =>
                {
                    _sketch.Draw();
                    _sketch.FrameCount++;
                }, "Draw");
            }

            if (_crashed && _crashException != null)
            {
                // Drawn into the offscreen surface too, so it gets the same
                // scaling treatment below and is itself save-able.
                DrawErrorOverlay(offscreenCanvas, _crashException, _crashContext, width, height);
            }

            offscreenCanvas.Restore();
            offscreenCanvas.Flush();

            // Blit the finished offscreen frame onto the real, leased canvas,
            // applying the DPI scale only at this final step.
            leasedCanvas.Save();
            leasedCanvas.Scale((float)scaling);
            leasedCanvas.DrawSurface(_offscreenSurface, 0, 0);
            leasedCanvas.Restore();
        }

        private void DrawErrorOverlay(SKCanvas canvas, Exception ex, string context, int width, int height)
        {
            canvas.Clear(new SKColor(30, 8, 8));

            using var borderPaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(220, 60, 60), StrokeWidth = 4, IsAntialias = true };
            canvas.DrawRect(new SKRect(4, 4, width - 4, height - 4), borderPaint);

            using var titlePaint = new SKPaint { Color = new SKColor(255, 120, 120), TextSize = 22, IsAntialias = true, FakeBoldText = true };
            using var bodyPaint = new SKPaint { Color = new SKColor(255, 210, 210), TextSize = 15, IsAntialias = true };
            using var dimPaint = new SKPaint { Color = new SKColor(200, 150, 150), TextSize = 12, IsAntialias = true };
            using var footerPaint = new SKPaint { Color = new SKColor(180, 180, 180), TextSize = 12, IsAntialias = true };

            float x = 24, y = 44;
            canvas.DrawText("Error en el sketch — ejecucion pausada", x, y, titlePaint);

            y += 34;
            canvas.DrawText($"En {context}():  {ex.GetType().Name}", x, y, bodyPaint);

            y += 24;
            foreach (var line in WrapText(ex.Message, 64))
            {
                canvas.DrawText(line, x, y, bodyPaint);
                y += 20;
            }

            y += 12;
            if (ex.StackTrace != null)
            {
                var lines = ex.StackTrace.Split('\n');
                int maxLines = Math.Min(lines.Length, 8);
                for (int i = 0; i < maxLines; i++)
                {
                    if (y > height - 40)
                        break;
                    canvas.DrawText(lines[i].Trim(), x, y, dimPaint);
                    y += 16;
                }
            }

            canvas.DrawText("Corrige el codigo y prueba de nuevo.", x, height - 20, footerPaint);
        }

        private static IEnumerable<string> WrapText(string text, int maxCharsPerLine)
        {
            for (int i = 0; i < text.Length; i += maxCharsPerLine)
                yield return text.Substring(i, Math.Min(maxCharsPerLine, text.Length - i));
        }
    }

    /// <summary>
    /// Custom draw operation that leases Avalonia's internal Skia surface and
    /// hands the real SKCanvas to the owning AvaloniaSketchCanvas for a frame.
    /// This is Avalonia's documented interop point for direct Skia drawing.
    /// </summary>
    internal class SketchDrawOperation : ICustomDrawOperation
    {
        private readonly AvaloniaSketchCanvas _owner;
        public Rect Bounds { get; }

        public SketchDrawOperation(Rect bounds, AvaloniaSketchCanvas owner)
        {
            Bounds = bounds;
            _owner = owner;
        }

        public void Dispose() { }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => Bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null)
                return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            // NOTE: unlike WPF, Avalonia's lease canvas coordinate space needs
            // verification against RenderScaling on your actual machine/display.
            // Starting at 1.0 (no scaling); if shapes appear offset or the wrong
            // size on a scaled display, this is the first thing to adjust —
            // see the note in chat about testing this.
            double scaling = 1.0;

            _owner.PaintSketch(canvas, scaling, (int)Bounds.Width, (int)Bounds.Height);
        }
    }
}