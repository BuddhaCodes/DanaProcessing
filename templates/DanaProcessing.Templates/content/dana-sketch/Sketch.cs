// A DanaProcessing sketch: Setup() runs once, Draw() runs every frame.
// Move the mouse; click to change the colors.
// Reference: https://buddhacodes.github.io/DanaProcessing/reference/a-z.html

public class MySketch : Sketch
{
    float hue;

    public override void Setup()
    {
        Size(640, 420);
        ColorMode(ColorSpaceMode.HSB, 360, 100, 100);
        NoStroke();
    }

    public override void Draw()
    {
        Background(230, 30, 12);
        // Around the mouse once it moves; around the middle until then.
        float cx = MouseX == 0 && MouseY == 0 ? Width / 2f : MouseX;
        float cy = MouseX == 0 && MouseY == 0 ? Height / 2f : MouseY;
        for (int i = 0; i < 12; i++)
        {
            float t = FrameCount * 0.02f + i * 0.5f;
            float x = cx + Cos(t) * (40 + i * 12);
            float y = cy + Sin(t) * (40 + i * 12);
            Fill((hue + i * 25) % 360, 70, 100);
            Circle(x, y, 26 - i);
        }
    }

    public override void MousePressed() => hue = Random(360);
}
