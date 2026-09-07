using System;
using SkiaSharp;

namespace DanaProcessing
{
    /// <summary>
    /// Owns the actual render target a GraphicsContext draws into. Today
    /// that's always a plain Skia raster surface (see SkiaBackend2D) — this
    /// interface exists so a future GPU-based Renderer3D backend can sit
    /// next to it without PGraphics, or anything built on GraphicsContext,
    /// needing to change.
    ///
    /// Only PGraphics uses this directly. Sketch's canvas is supplied
    /// per-frame by whatever hosts it (see Sketch.SetCanvas) regardless of
    /// renderer — presenting a frame to a window is a host-integration
    /// concern layered on top of whichever backend actually produced the
    /// pixels, not something this interface tries to solve.
    ///
    /// 3D roadmap: a GPU backend (candidate libraries: Silk.NET.OpenGL,
    /// Veldrid) will implement this interface too. Its Canvas/Surface would
    /// most likely come from copying a rendered GPU framebuffer into a Skia
    /// surface each frame, so Save()/SaveFrame()/Get()/LoadPixels() keep
    /// working identically no matter which backend produced the pixels.
    /// RendererKind.Renderer3D throws NotImplementedException until that
    /// backend exists — see GraphicsContext.SetRenderer().
    /// </summary>
    internal interface IGraphicsBackend : IDisposable
    {
        RendererKind Kind { get; }
        SKCanvas Canvas { get; }
        SKSurface Surface { get; }

        /// <summary>
        /// Called at the start of a drawing batch (PGraphics.BeginDraw()).
        /// SkiaBackend2D no-ops — its Canvas is already live and ready to
        /// draw into directly. A GPU backend uses this to bind its
        /// framebuffer and clear the depth buffer for the new frame.
        /// </summary>
        void BeginFrame();

        /// <summary>
        /// Called at the end of a drawing batch (PGraphics.EndDraw()).
        /// SkiaBackend2D no-ops. A GPU backend uses this to read the
        /// framebuffer it just rendered back into its Surface/Canvas, so
        /// that Get()/Save()/LoadPixels() see the up-to-date pixels no
        /// matter which backend produced them.
        /// </summary>
        void EndFrame();
    }
}