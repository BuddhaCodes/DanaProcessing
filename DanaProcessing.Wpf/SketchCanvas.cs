using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace DanaProcessing
{
    /// <summary>
    /// A WPF UserControl that hosts a Sketch — the DanaProcessing equivalent of an
    /// HTML &lt;canvas&gt; element. Drop it into any existing WPF/WinUI layout:
    ///
    ///   var canvas = new SketchCanvas(new MySketch());
    ///   someGrid.Children.Add(canvas);
    ///
    /// It drives the full Setup/Draw lifecycle, forwards mouse/keyboard input,
    /// and shows the visual error overlay if the sketch code throws — all the
    /// same behavior SketchWindow gives you standalone, just embeddable.
    /// </summary>
    public class SketchCanvas : UserControl
    {
        private readonly Sketch _sketch;
        private readonly SKElement _skElement;
        private readonly DispatcherTimer _timer;
        private bool _didSetup = false;
        private int _lastKnownFrameRate = -1;

        private bool _crashed = false;
        private Exception? _crashException = null;
        private string _crashContext = "";

        public SketchCanvas(Sketch sketch)
        {
            _sketch = sketch;
            Focusable = true;
            IsTabStop = true;

            _skElement = new SKElement();
            _skElement.PaintSurface += OnPaintSurface;
            _skElement.MouseMove += OnMouseMove;
            _skElement.MouseDown += (s, e) => Focus(); // click-to-focus, like a canvas in a web page

            Content = _skElement;

            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;

            _timer = new DispatcherTimer();
            SetTimerInterval(sketch.TargetFrameRate);
            _timer.Tick += (s, e) =>
            {
                if (_sketch.TargetFrameRate != _lastKnownFrameRate)
                    SetTimerInterval(_sketch.TargetFrameRate);

                if (_sketch.IsLooping || _crashed)
                    _skElement.InvalidateVisual();
            };
            _timer.Start();

            Loaded += (s, e) => Focus();
            Unloaded += (s, e) => _timer.Stop(); // don't keep ticking if removed from the visual tree
        }

        private void SetTimerInterval(int fps)
        {
            _lastKnownFrameRate = fps;
            _timer.Interval = TimeSpan.FromSeconds(1.0 / fps);
        }

        /// <summary>Runs a piece of user-sketch code safely; on exception, enters the crashed/overlay state.</summary>
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

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(_skElement);

            // WPF gives mouse position in DIPs (logical pixels), but SkiaSharp's
            // canvas is sized in actual device pixels. On a scaled display (125%,
            // 150%, etc.) these differ, causing a mismatch between where the mouse
            // is and where drawing happens. Scale DIPs -> pixels to correct it.
            double scaleX = _skElement.ActualWidth > 0 ? _skElement.CanvasSize.Width / _skElement.ActualWidth : 1.0;
            double scaleY = _skElement.ActualHeight > 0 ? _skElement.CanvasSize.Height / _skElement.ActualHeight : 1.0;

            _sketch.PMouseX = _sketch.MouseX;
            _sketch.PMouseY = _sketch.MouseY;
            _sketch.MouseX = (float)(pos.X * scaleX);
            _sketch.MouseY = (float)(pos.Y * scaleY);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            _sketch.IsKeyPressed = true;
            SetSketchKeyFrom(e.Key);
            RunSafely(_sketch.KeyPressed, "KeyPressed");
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
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
                _sketch.Key = '\0'; // unmapped key; extend as needed
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;

            // e.Surface is a real SKSurface (SKElement always draws into a
            // raster/CPU surface), so we can pass it straight through — this
            // is what lets Save()/SaveFrame() read pixels back later.
            _sketch.SetCanvas(canvas, e.Surface);

            int newWidth = e.Info.Width;
            int newHeight = e.Info.Height;
            bool sizeChanged = newWidth != _sketch.Width || newHeight != _sketch.Height;

            _sketch.Size(newWidth, newHeight);

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
                DrawErrorOverlay(canvas, _crashException, _crashContext, newWidth, newHeight);
            }
        }

        /// <summary>
        /// Draws a readable error panel directly with SkiaSharp (independent of the
        /// sketch's own Fill/Stroke state, since that state may be mid-corruption).
        /// </summary>
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

            canvas.DrawText("Corrige el codigo y vuelve a ejecutar dotnet run.", x, height - 20, footerPaint);
        }

        private static IEnumerable<string> WrapText(string text, int maxCharsPerLine)
        {
            for (int i = 0; i < text.Length; i += maxCharsPerLine)
                yield return text.Substring(i, Math.Min(maxCharsPerLine, text.Length - i));
        }
    }
}