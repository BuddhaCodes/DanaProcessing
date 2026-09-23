namespace DanaProcessing
{
    /// <summary>
    /// Process-wide default for how many MSAA samples a new Renderer3DBackend
    /// requests when none is given explicitly to Create() — the only lever
    /// available for this, since Sketch.Size(w, h, renderer) is the sketch's
    /// own public API and shouldn't grow an AA parameter just for this. A
    /// host (the IDE, or an exported sketch's own startup code) sets this
    /// once before Setup() runs; DanaProcessing itself never changes it.
    ///
    /// Defaults to 4x — a reasonable quality/perf tradeoff that benefits
    /// exported standalone sketches too, which have no settings screen of
    /// their own to configure this from. The IDE's own Options window lets
    /// the user override this (and the separate 2D supersampling setting)
    /// for its own live preview — see DanaProcessing.Ide.Theme.RenderingSettings.
    /// </summary>
    public static class Renderer3DSettings
    {
        public static int DefaultSampleCount { get; set; } = 4;
    }
}
