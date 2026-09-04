using SkiaSharp;
using System;

namespace DanaProcessing
{
    /// <summary>
    /// A loaded image, equivalent to Processing's PImage. Get one via
    /// Sketch.LoadImage(path), then draw it with Sketch.Image(img, x, y).
    /// </summary>
    public class PImage : IDisposable
    {
        internal SKBitmap Bitmap { get; private set; }

        /// <summary>
        /// True once the bitmap backing this image is actually ready. Always
        /// true for every normal PImage (LoadImage(), Get(), etc). Only ever
        /// false for the placeholder Sketch.RequestImage() hands back while
        /// its background load is still in flight — Width/Height read as 0
        /// during that window, like Processing's own requestImage() result
        /// before the load finishes.
        /// </summary>
        public bool IsLoaded { get; private set; } = true;

        public int Width => IsLoaded ? Bitmap.Width : 0;
        public int Height => IsLoaded ? Bitmap.Height : 0;

        internal PImage(SKBitmap bitmap)
        {
            Bitmap = bitmap;
        }

        /// <summary>Builds an unloaded placeholder — 0x0 until ReplaceBitmap() swaps in the real bitmap. Used by Sketch.RequestImage(); nothing else should need this.</summary>
        internal static PImage CreatePlaceholder()
        {
            var img = new PImage(new SKBitmap(1, 1)) { IsLoaded = false };
            return img;
        }

        /// <summary>Swaps in the real bitmap once a background RequestImage() load finishes, disposing the placeholder bitmap it replaces.</summary>
        internal void ReplaceBitmap(SKBitmap bitmap)
        {
            Bitmap.Dispose();
            Bitmap = bitmap;
            IsLoaded = true;
        }

        /// <summary>
        /// Applies another image as an alpha mask, mutating this image in
        /// place, like Processing's mask(). If the mask image has varying
        /// alpha, that alpha is used directly; otherwise (a fully-opaque
        /// mask, the common case of "just a grayscale PNG") its per-pixel
        /// brightness is used instead — matching Processing's own fallback.
        /// Multiplies into this image's existing alpha rather than
        /// overwriting it, so masking an already-transparent image only
        /// ever removes more, never adds opacity back.
        ///
        /// Performance note: this walks every pixel via GetPixel/SetPixel
        /// (twice — once to check which mode the mask is in, once to apply
        /// it), which is simple and correct but not fast. Fine for a one-time
        /// setup call; avoid calling this every frame on a large image.
        /// </summary>
        public void Mask(PImage maskImage)
        {
            if (maskImage.Width != Width || maskImage.Height != Height)
                throw new ArgumentException("La máscara debe tener las mismas dimensiones que la imagen que se está enmascarando.");

            bool maskHasAlphaVariation = false;
            for (int y = 0; y < Height && !maskHasAlphaVariation; y++)
                for (int x = 0; x < Width; x++)
                    if (maskImage.Bitmap.GetPixel(x, y).Alpha != 255)
                    { maskHasAlphaVariation = true; break; }

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var pixel = Bitmap.GetPixel(x, y);
                    var maskPixel = maskImage.Bitmap.GetPixel(x, y);
                    byte maskValue = maskHasAlphaVariation
                        ? maskPixel.Alpha
                        : (byte)((maskPixel.Red + maskPixel.Green + maskPixel.Blue) / 3);
                    byte newAlpha = (byte)(pixel.Alpha * maskValue / 255);
                    Bitmap.SetPixel(x, y, new SKColor(pixel.Red, pixel.Green, pixel.Blue, newAlpha));
                }
            }
        }

        public void Dispose() => Bitmap.Dispose();
    }
}