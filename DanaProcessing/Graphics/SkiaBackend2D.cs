using System;
using SkiaSharp;

namespace DanaProcessing
{
    /// <summary>
    /// The only IGraphicsBackend implemented today: a plain offscreen Skia
    /// raster surface. This is exactly what PGraphics's constructor used to
    /// build inline — pulled out here so a future Renderer3D backend can
    /// sit next to it behind the same interface without touching PGraphics
    /// itself again.
    /// </summary>
    internal sealed class SkiaBackend2D : IGraphicsBackend
    {
        public RendererKind Kind => RendererKind.Renderer2D;
        public SKCanvas Canvas { get; }
        public SKSurface Surface { get; }

        private SkiaBackend2D(SKSurface surface)
        {
            Surface = surface;
            Canvas = surface.Canvas;
        }

        public static SkiaBackend2D Create(int width, int height)
        {
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            var surface = SKSurface.Create(info)
                ?? throw new InvalidOperationException($"No se pudo crear una superficie offscreen de {width}x{height}.");
            return new SkiaBackend2D(surface);
        }

        public void Dispose() => Surface.Dispose();

        // 2D drawing already happens straight onto Canvas every call — there's
        // no separate GPU framebuffer to bind or read back, so both frame
        // hooks are no-ops. Exists purely to satisfy IGraphicsBackend.
        public void BeginFrame() { }
        public void EndFrame() { }
    }
}