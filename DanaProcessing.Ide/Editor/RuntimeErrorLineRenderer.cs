using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// Paints a soft red band behind the line where the running sketch threw
    /// (see SketchCrashInfo), plus a solid marker on its left edge. Unlike
    /// the compile-time squiggles this is about where the program *was* when
    /// it failed, so it's one whole line, and it goes away on the next edit.
    /// </summary>
    public sealed class RuntimeErrorLineRenderer : IBackgroundRenderer
    {
        private static readonly IBrush Band = new SolidColorBrush(Avalonia.Media.Color.Parse("#33E86A6A"));
        private static readonly IBrush Marker = new SolidColorBrush(Avalonia.Media.Color.Parse("#E86A6A"));

        /// <summary>1-based line, 0 = nothing to show.</summary>
        public int Line { get; set; }
        public string Message { get; set; } = "";

        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (Line <= 0 || textView.Document == null || Line > textView.Document.LineCount)
                return;
            textView.EnsureVisualLines();
            var line = textView.Document.GetLineByNumber(Line);
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, line))
            {
                var full = new Rect(0, rect.Top, textView.Bounds.Width, rect.Height);
                drawingContext.FillRectangle(Band, full);
                drawingContext.FillRectangle(Marker, new Rect(0, rect.Top, 3, rect.Height));
            }
        }
    }
}
