using Avalonia.Controls;
using DanaProcessing;

namespace DanaProcessing.AvaloniaHost
{
    /// <summary>
    /// Standalone Avalonia window that runs a Sketch full-window. Thin wrapper
    /// around AvaloniaSketchCanvas.
    /// </summary>
    public class AvaloniaSketchWindow : Window
    {
        public AvaloniaSketchCanvas Canvas { get; }

        public AvaloniaSketchWindow(Sketch sketch, string title = "DanaProcessing Sketch")
        {
            Title = title;

            // NOT `Width = sketch.Width; Height = sketch.Height;` here: Setup()
            // (where a sketch actually calls Size(w, h)) doesn't run at
            // construction time — AvaloniaSketchCanvas runs it lazily, during its
            // own first MeasureOverride (see the remark there on why). Reading
            // sketch.Width/Height this early would still be whatever placeholder
            // value a fresh Sketch starts with, so the window would open at some
            // unrelated default size with the canvas's real (correctly sized)
            // content floating in the corner of it — exactly the black-bordered
            // window this class used to produce. SizeToContent fits the window to
            // whatever the canvas's own MeasureOverride reports instead, which is
            // correct precisely because it's driven by the same layout pass that
            // makes Setup() run in the first place, and keeps re-fitting if a
            // sketch calls Size() again later to resize itself. CanResize = false
            // makes that guarantee permanent — dragging the window bigger than
            // the canvas is exactly how the black borders came back otherwise.
            SizeToContent = SizeToContent.WidthAndHeight;
            CanResize = false;

            Canvas = new AvaloniaSketchCanvas(sketch);
            Content = Canvas;
        }
    }
}
