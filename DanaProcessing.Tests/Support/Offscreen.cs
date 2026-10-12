using SkiaSharp;

namespace DanaProcessing.Tests.Support;

/// <summary>Runs a sketch without a window: an in-memory surface, Setup(), then N frames.</summary>
internal static class Offscreen
{
    public static SKSurface Run(Sketch sketch, int frames = 1)
    {
        SKSurface surface = NewSurface(sketch.Width, sketch.Height);
        sketch.SetCanvas(surface.Canvas, surface);
        sketch.SizeChanged += (w, h) =>
        {
            surface = NewSurface(w, h);
            sketch.SetCanvas(surface.Canvas, surface);
        };
        sketch.Setup();
        for (int i = 0; i < frames; i++)
        {
            surface.Canvas.Save();
            sketch.RenderFrame();
            surface.Canvas.Restore();
        }
        surface.Canvas.Flush();
        return surface;
    }

    public static SKSurface NewSurface(int w, int h) =>
        SKSurface.Create(new SKImageInfo(Math.Max(1, w), Math.Max(1, h), SKColorType.Rgba8888, SKAlphaType.Premul));

    public static SKColor Pixel(SKSurface surface, int x, int y)
    {
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.GetPixel(x, y);
    }

    public static bool IsBright(SKColor c) => c.Red + c.Green + c.Blue > 600;
    public static bool IsDark(SKColor c) => c.Red + c.Green + c.Blue < 60;
}
