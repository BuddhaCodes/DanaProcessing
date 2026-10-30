using SkiaSharp;

namespace DanaProcessing
{
    /// <summary>
    /// A paint plus the font to draw text with -- SkiaSharp 3 moved size,
    /// typeface and bold off SKPaint onto SKFont, and the old
    /// DrawText(text, x, y, SKPaint) overload is obsolete. The overlay panels
    /// (Stats, Tweak, the canvas's own HUD) keep one of these per text style
    /// instead of a text-carrying SKPaint, and draw through the extension below.
    /// </summary>
    internal sealed class TextStyle : IDisposable
    {
        public SKPaint Paint { get; }
        public SKFont Font { get; }

        public TextStyle(SKColor color, float size, bool bold = false)
        {
            Paint = new SKPaint { Color = color, IsAntialias = true };
            // The same font SkiaSharp derived from the old text paint (SKPaint.ToFont()):
            // antialiased, linear metrics, not subpixel-positioned.
            Font = new SKFont { Size = size, Embolden = bold, Edging = SKFontEdging.Antialias, LinearMetrics = true };
        }

        public float MeasureText(string text) => Font.MeasureText(text, Paint);

        public void Dispose()
        {
            Paint.Dispose();
            Font.Dispose();
        }
    }

    internal static class TextStyleCanvasExtensions
    {
        /// <summary>Left-aligned text with its baseline at (x, y), like the old paint-based overload.</summary>
        public static void DrawText(this SKCanvas canvas, string text, float x, float y, TextStyle style) =>
            canvas.DrawText(text, x, y, SKTextAlign.Left, style.Font, style.Paint);
    }
}
