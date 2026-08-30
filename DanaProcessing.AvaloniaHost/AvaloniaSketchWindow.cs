using Avalonia.Controls;
using DanaProcessing;

namespace DanaProcessing.AvaloniaHost
{
    /// <summary>
    /// Standalone Avalonia window that runs a Sketch full-window. Thin wrapper
    /// around AvaloniaSketchCanvas, same pattern as DanaProcessing.Wpf.SketchWindow.
    /// </summary>
    public class AvaloniaSketchWindow : Window
    {
        public AvaloniaSketchCanvas Canvas { get; }

        public AvaloniaSketchWindow(Sketch sketch, string title = "DanaProcessing Sketch")
        {
            Title = title;
            Width = sketch.Width;
            Height = sketch.Height;

            Canvas = new AvaloniaSketchCanvas(sketch);
            Content = Canvas;
        }
    }
}
