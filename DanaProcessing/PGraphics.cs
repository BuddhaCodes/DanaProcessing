using System;
using SkiaSharp;

namespace DanaProcessing
{
    /// <summary>
    /// An offscreen drawing surface, equivalent to Processing's PGraphics.
    /// Get one via Sketch.CreateGraphics(w, h) (or off another buffer, to
    /// nest them), draw into it between BeginDraw()/EndDraw() using the same
    /// Fill/Stroke/Rect/Ellipse/Text/beginShape/... vocabulary as Sketch
    /// itself — it all lives in the shared GraphicsContext base class — then
    /// draw the result elsewhere with Image(pg, x, y).
    /// </summary>
    public sealed class PGraphics : GraphicsContext
    {
        private readonly SKSurface _surface;
        private bool _drawing;
        private bool _disposed;

        /// <summary>Use Sketch.CreateGraphics(w, h) instead of calling this directly, matching Processing's createGraphics().</summary>
        internal PGraphics(int width, int height)
        {
            Width = width;
            Height = height;
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            Surface = SKSurface.Create(info)
                ?? throw new InvalidOperationException($"No se pudo crear una superficie offscreen de {width}x{height}.");
            Canvas = Surface.Canvas;
        }

        /// <summary>Marks the start of a batch of drawing calls, like Processing's beginDraw(). Not strictly required by the software renderer, but catches the common bug of drawing into a buffer that's already mid-frame elsewhere.</summary>
        public void BeginDraw()
        {
            ThrowIfDisposed();
            if (_drawing)
                throw new InvalidOperationException("BeginDraw() ya fue llamado; falta un EndDraw().");
            _drawing = true;
        }

        /// <summary>Marks the end of a batch of drawing calls, like Processing's endDraw().</summary>
        public void EndDraw()
        {
            ThrowIfDisposed();
            if (!_drawing)
                throw new InvalidOperationException("EndDraw() llamado sin un BeginDraw() previo.");
            Canvas.Flush();
            _drawing = false;
        }

        protected override void EnsureReady()
        {
            ThrowIfDisposed();
            if (!_drawing)
                throw new InvalidOperationException("Llamada de dibujo fuera de BeginDraw()/EndDraw().");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PGraphics));
        }

        /// <summary>Snapshots the current pixels as a standalone PImage, like Processing's get(). Image(PGraphics, x, y, ...) uses SnapshotForDraw() instead — call Get() when you want a frozen copy while continuing to draw into this buffer afterward.</summary>
        public PImage Get()
        {
            ThrowIfDisposed();
            using var image = Surface!.Snapshot();
            return new PImage(SKBitmap.FromImage(image));
        }

        internal SKImage SnapshotForDraw()
        {
            ThrowIfDisposed();
            return Surface!.Snapshot();
        }

        public override void Dispose()
        {
            if (_disposed)
                return;
            base.Dispose();
            Surface?.Dispose();
            _disposed = true;
        }
    }
}