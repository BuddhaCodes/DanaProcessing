using SkiaSharp;

namespace DanaProcessing.AvaloniaHost.Attach
{
    /// <summary>
    /// What an exported attached sketch forces, whatever its own Attach() says:
    /// one instance per terminal (Mode), drawing behind the terminal (Layer)
    /// with a backdrop under it (Backdrop) -- the way Abyss Tide lives in its
    /// own translucent Windows Terminal profile and is invisible in the rest.
    /// </summary>
    /// <param name="ProfileTitle">The terminal window's title while the export's own profile is the tab in front: the sketch only draws then (see TitleGate).</param>
    /// <param name="EachPane">With EachWindow: one sketch per Windows Terminal pane (split), not per window (see TerminalPanes); only the panes of the export's own profile when ProfileTitle is set.</param>
    /// <param name="UseGpu">Present frames through the GPU (DirectComposition), falling back to the CPU presenter if it can't.</param>
    public sealed record AttachedRunOverrides(AttachMode? Mode = null, AttachLayer? Layer = null, SKColor? Backdrop = null, string? ProfileTitle = null,
                                              bool UseGpu = false, bool EachPane = false)
    {
        public static AttachedRunOverrides None { get; } = new();
    }
}
