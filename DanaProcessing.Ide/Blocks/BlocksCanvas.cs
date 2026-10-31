using System.Collections.Generic;
using System.Linq;
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// Hosts a <see cref="BlocksWorkspace"/> in Avalonia: hands it pointer and
    /// keyboard input in pixels and lets it paint straight onto Avalonia's
    /// Skia canvas (the same lease AvaloniaSketchCanvas uses).
    /// </summary>
    internal sealed class BlocksCanvas : Control
    {
        private readonly DispatcherTimer _glowTimer;

        public BlocksCanvas(BlocksWorkspace workspace)
        {
            Workspace = workspace;
            Focusable = true;
            ClipToBounds = true;
            workspace.Invalidated += RequestPaint;

            // While a sketch runs, blocks light up and fade: repaint ~30 times a second.
            _glowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _glowTimer.Tick += (_, _) => InvalidateVisual();
        }

        public BlocksWorkspace Workspace { get; }

        /// <summary>Turns the glow animation on (a sketch with these blocks is running) or off.</summary>
        public bool Animating
        {
            get => _glowTimer.IsEnabled;
            set
            {
                if (value) _glowTimer.Start(); else _glowTimer.Stop();
                InvalidateVisual();
            }
        }

        private void RequestPaint()
        {
            if (Dispatcher.UIThread.CheckAccess())
                InvalidateVisual();
            else
                Dispatcher.UIThread.Post(InvalidateVisual);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _glowTimer.Stop();
        }

        public override void Render(DrawingContext context)
        {
            Workspace.ViewWidth = (float)Bounds.Width;
            Workspace.ViewHeight = (float)Bounds.Height;

            // Avalonia draws custom operations on its render thread, while the
            // workspace (its program, layout and fonts) lives on the UI thread.
            // So the workspace paints HERE, on the UI thread, into a recording;
            // the render thread only plays back that finished, immutable picture.
            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height));
            Workspace.Paint(canvas);
            context.Custom(new DrawOperation(new Rect(Bounds.Size), recorder.EndRecording()));
        }

        private static SKPoint Pos(PointerEventArgs e, Visual relativeTo)
        {
            var p = e.GetPosition(relativeTo);
            return new SKPoint((float)p.X, (float)p.Y);
        }

        // Fingers on the canvas right now (touch screens), by pointer id. One
        // finger works like the mouse; a second one switches to pinch/pan
        // until both are lifted.
        private readonly Dictionary<int, SKPoint> _fingers = new();
        private int? _primary;
        private bool _pinching;

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus();
            bool touch = e.Pointer.Type is PointerType.Touch or PointerType.Pen;
            if (!touch && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;
            e.Pointer.Capture(this);
            e.Handled = true;
            var p = Pos(e, this);

            if (e.Pointer.Type == PointerType.Touch)
            {
                _fingers[e.Pointer.Id] = p;
                if (_fingers.Count == 2 && !_pinching)
                {
                    var two = _fingers.Values.Take(2).ToArray();
                    _pinching = Workspace.PinchStart(two[0], two[1]);
                    return;
                }
                if (_fingers.Count > 1 || _pinching)
                    return; // a third finger, or one landing while a block is carried
            }
            _primary = e.Pointer.Id;
            Workspace.PointerDown(p, touch);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var p = Pos(e, this);
            if (e.Pointer.Type == PointerType.Touch && _fingers.ContainsKey(e.Pointer.Id))
            {
                _fingers[e.Pointer.Id] = p;
                if (_pinching)
                {
                    var two = _fingers.Values.Take(2).ToArray();
                    if (two.Length == 2)
                        Workspace.PinchMove(two[0], two[1]);
                    return;
                }
            }
            if (_primary is null || _primary == e.Pointer.Id || e.Pointer.Type == PointerType.Mouse)
                Workspace.PointerMove(p);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            e.Pointer.Capture(null);
            e.Handled = true;
            var p = Pos(e, this);
            if (e.Pointer.Type == PointerType.Touch)
            {
                _fingers.Remove(e.Pointer.Id);
                if (_pinching)
                {
                    // Lifting one of two fingers ends the pinch; the other one
                    // does nothing more until it's lifted too.
                    if (_fingers.Count < 2)
                        Workspace.PinchEnd();
                    if (_fingers.Count == 0)
                        _pinching = false;
                    _primary = null;
                    return;
                }
            }
            if (_primary is null || _primary == e.Pointer.Id)
            {
                Workspace.PointerUp(p);
                _primary = null;
            }
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            _fingers.Remove(e.Pointer.Id);
            if (_fingers.Count == 0 && _pinching)
            {
                Workspace.PinchEnd();
                _pinching = false;
            }
            // The window lost the pointer mid-drag (alt-tab...): finish where it is.
            if (Workspace.IsDragging && (_primary is null || _primary == e.Pointer.Id))
            {
                Workspace.PointerUp(new SKPoint(Workspace.PaletteW + 200, 200));
                _primary = null;
            }
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            bool zoom = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
            Workspace.Wheel(Pos(e, this), (float)e.Delta.Y, zoom);
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
            switch (e.Key)
            {
                case Key.Delete or Key.Back:
                    Workspace.DeleteSelected();
                    e.Handled = true;
                    break;
                case Key.Z when ctrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                case Key.Y when ctrl:
                    Workspace.Redo();
                    e.Handled = true;
                    break;
                case Key.Z when ctrl:
                    Workspace.Undo();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Workspace.Select(null);
                    e.Handled = true;
                    break;
            }
        }

        private sealed class DrawOperation : ICustomDrawOperation
        {
            private readonly SKPicture _picture;

            public DrawOperation(Rect bounds, SKPicture picture)
            {
                Bounds = bounds;
                _picture = picture;
            }

            public Rect Bounds { get; }
            // Avalonia disposes a custom operation on the render thread once it's
            // done with it (when newer render data replaces it).
            public void Dispose() => _picture.Dispose();
            public bool Equals(ICustomDrawOperation? other) => false;
            public bool HitTest(Point p) => Bounds.Contains(p);

            public void Render(ImmediateDrawingContext context)
            {
                var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                if (lease is null)
                    return;
                using var api = lease.Lease();
                var canvas = api.SkCanvas;
                canvas.Save();
                canvas.ClipRect(new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height));
                canvas.DrawPicture(_picture);
                canvas.Restore();
            }
        }
    }
}
