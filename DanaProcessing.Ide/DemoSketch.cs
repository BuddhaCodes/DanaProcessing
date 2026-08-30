using DanaProcessing;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// Minimal sketch just to validate AvaloniaSketchCanvas works end to end,
    /// mirroring the very first WPF prototype we validated at the start of this project.
    /// </summary>
    public class DemoSketch : Sketch
    {
        public override void Setup()
        {
            Size(600, 400);
        }

        public override void Draw()
        {
            Background(20, 20, 30);
            NoStroke();
            Fill(100, 200, 255);
            Ellipse(MouseX, MouseY, 60, 60);
            Fill(255, 255, 255);
            TextSize(14);
            Text("Avalonia host funcionando — mismo Sketch, otro framework de UI.", 10, Height - 15);
        }
    }
}
