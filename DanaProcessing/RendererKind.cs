namespace DanaProcessing
{
    /// <summary>
    /// Which rendering pipeline a Sketch or PGraphics uses — chosen once via
    /// Size(w, h, renderer) or CreateGraphics(w, h, renderer) and locked for
    /// that object's lifetime, mirroring Processing's own size(w, h, P3D).
    /// See GraphicsContext.Renderer and IGraphicsBackend for how this plugs
    /// into the actual drawing pipeline.
    /// </summary>
    public enum RendererKind
    {
        /// <summary>The default and, for now, the only implemented backend: 2D drawing via Skia (SKCanvas/SKSurface).</summary>
        Renderer2D,

        /// <summary>
        /// A GPU-backed 3D pipeline — camera, lights, Box()/Sphere(), real
        /// depth — not implemented yet. Requesting it throws
        /// NotImplementedException until a real IGraphicsBackend for it
        /// exists; see the 3D roadmap notes near IGraphicsBackend.
        /// </summary>
        Renderer3D
    }
}