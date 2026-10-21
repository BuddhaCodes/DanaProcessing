using System;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace DanaProcessing
{
    /// <summary>
    /// Bulk pixel reads and writes, in one pass instead of a GetPixel() call per
    /// pixel: Skia converts whatever the source holds (BGRA, premultiplied,
    /// grayscale...) to plain RGBA, which then becomes Colors.
    /// </summary>
    internal static class PixelIO
    {
        public static Color[] Read(SKPixmap source)
        {
            int w = source.Width, h = source.Height;
            var bytes = new byte[w * h * 4];
            var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                if (!source.ReadPixels(info, handle.AddrOfPinnedObject(), w * 4))
                    throw new InvalidOperationException("No se pudieron leer los píxeles.");
            }
            finally { handle.Free(); }
            var colors = new Color[w * h];
            for (int i = 0, b = 0; i < colors.Length; i++, b += 4)
                colors[i] = new Color(bytes[b], bytes[b + 1], bytes[b + 2], bytes[b + 3]);
            return colors;
        }

        public static Color[] Read(SKBitmap bitmap)
        {
            using var pixmap = bitmap.PeekPixels();
            return Read(pixmap);
        }

        /// <summary>A new RGBA bitmap holding these colors.</summary>
        public static SKBitmap ToBitmap(Color[] colors, int w, int h)
        {
            if (colors.Length != w * h)
                throw new ArgumentException($"Se esperaban {w * h} píxeles ({w} x {h}) y hay {colors.Length}.");
            var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            var bytes = new byte[w * h * 4];
            for (int i = 0, b = 0; i < colors.Length; i++, b += 4)
            {
                var c = colors[i];
                bytes[b] = c.R; bytes[b + 1] = c.G; bytes[b + 2] = c.B; bytes[b + 3] = c.A;
            }
            Marshal.Copy(bytes, 0, bitmap.GetPixels(), bytes.Length);
            return bitmap;
        }
    }
}
