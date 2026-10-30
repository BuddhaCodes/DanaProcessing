using SkiaSharp;

namespace DanaProcessing.Tests.Support;

/// <summary>
/// Drives an AttachedSketch the way AttachedSketchHost does -- Settings(),
/// the target's size, Setup(), then per frame: Track(), the cursor, input,
/// RenderFrame() -- but into an in-memory surface, with a fake target the
/// test moves, focuses, hides and types into. No Windows needed.
/// </summary>
internal sealed class AttachedDriver
{
    private static readonly Action<Action, string> Run = (a, _) => a();

    public AttachedSketch Sketch { get; }
    public SKSurface Surface { get; private set; }
    public int X, Y;
    public bool Focused = true, Showing = true;
    public float CursorX = -100, CursorY = -100;

    public AttachedDriver(AttachedSketch sketch, int width = 800, int height = 450, int? seed = null)
    {
        Sketch = sketch;
        if (seed is { } s)
            sketch.ApplySeed(s); // two drivers with the same seed start out identical
        sketch.Settings();
        sketch.ApplyAttachedSize(width, height, notify: false);
        Surface = Offscreen.NewSurface(width, height);
        sketch.SetCanvas(Surface.Canvas, Surface);
        sketch.Setup();
    }

    /// <summary>One frame: the target's state, the cursor, queued input, then Draw().</summary>
    public void Frame(params TargetInput[] input)
    {
        Sketch.Track(new TargetSample(Showing, Focused, X, Y, 1 / 60.0), Run);
        if (!Showing)
            return; // the host doesn't draw while the target is hidden
        Sketch.TrackCursor(CursorX, CursorY, Run);
        foreach (var i in input)
            Sketch.DispatchTargetInput(i, Run);
        Surface.Canvas.Save();
        Sketch.RenderFrame();
        Surface.Canvas.Restore();
    }

    public void Frames(int n)
    {
        for (int i = 0; i < n; i++)
            Frame();
    }

    /// <summary>The target changed size: new surface, WindowResized().</summary>
    public void Resize(int width, int height)
    {
        Surface = Offscreen.NewSurface(width, height);
        Sketch.SetCanvas(Surface.Canvas, Surface);
        Sketch.ApplyAttachedSize(width, height, notify: true);
    }

    public void Type(string text)
    {
        foreach (char c in text)
        {
            Frame(new TargetInput(TargetInputKind.KeyDown, c, char.ToUpperInvariant(c)),
                  new TargetInput(TargetInputKind.KeyUp, c, char.ToUpperInvariant(c)));
        }
    }

    /// <summary>A command finished in the fake terminal's shell (or a custom signal), then one frame.</summary>
    public void Shell(ShellSignal signal)
    {
        Sketch.DispatchShellSignal(signal, Run);
        Frame();
    }

    public void CommandFailed(int exitCode = 1) => Shell(new ShellSignal(ShellSignalKind.Error, exitCode, "", 0));

    public void CommandSucceeded() => Shell(new ShellSignal(ShellSignalKind.Ok, 0, "", 0));

    public void Click(float x, float y) =>
        Frame(new TargetInput(TargetInputKind.MouseDown, Button: MouseButtonKind.Left, X: x, Y: y),
              new TargetInput(TargetInputKind.MouseUp, Button: MouseButtonKind.Left, X: x, Y: y));

    /// <summary>How many pixels aren't fully transparent -- an attached sketch's canvas starts (and Clear()s to) transparent.</summary>
    public int PaintedPixels()
    {
        using var image = Surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        int n = 0;
        for (int y = 0; y < bitmap.Height; y += 2)
            for (int x = 0; x < bitmap.Width; x += 2)
                if (bitmap.GetPixel(x, y).Alpha > 0)
                    n++;
        return n;
    }
}
