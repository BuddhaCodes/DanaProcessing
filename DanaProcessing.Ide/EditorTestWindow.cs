using Avalonia.Controls;
using DanaProcessing.Ide.Editor;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// Throwaway window to validate SketchEditorView in isolation, mirroring
    /// how AvaloniaSketchWindow validates the canvas. Point App.cs at this
    /// instead of AvaloniaSketchWindow while testing the editor; swap back
    /// once the editor is wired into the real IDE window.
    /// </summary>
    public class EditorTestWindow : Window
    {
        public SketchEditorView Editor { get; }

        public EditorTestWindow()
        {
            Title = "DanaProcessing IDE (prueba de editor)";
            Width = 900;
            Height = 600;

            Editor = new SketchEditorView();
            Content = Editor;
        }
    }
}
