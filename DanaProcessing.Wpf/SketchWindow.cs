using System.Windows;

namespace DanaProcessing
{
    /// <summary>
    /// A standalone window that runs a Sketch full-screen/full-window — the quick
    /// way to run a sketch without embedding it into a larger app. All the real
    /// engine logic lives in SketchCanvas; this is just a thin host for it.
    /// </summary>
    public class SketchWindow : Window
    {
        public SketchCanvas Canvas { get; }

        public SketchWindow(Sketch sketch, string title = "DanaProcessing Sketch")
        {
            Title = title;
            Width = sketch.Width;
            Height = sketch.Height;

            Canvas = new SketchCanvas(sketch);
            Content = Canvas;
        }
    }
}
