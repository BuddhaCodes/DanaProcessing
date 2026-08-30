using System;
using SkiaSharp;
using SkiaSharp.Extended.Svg;
using SKSvg = SkiaSharp.Extended.Svg.SKSvg;

namespace DanaProcessing
{
    /// <summary>
    /// A loaded vector shape, equivalent to Processing's PShape — currently
    /// SVG-only, matching Processing's loadShape() for 2D vector graphics
    /// (Processing's OBJ/3D loading is out of scope for DanaProcessing). Get
    /// one via Sketch.LoadShape(path) (works from a PGraphics too, since it's
    /// defined on the shared GraphicsContext base), then draw it with
    /// Shape(shape, x, y).
    /// </summary>
    public sealed class PShape : IDisposable
    {
        internal SKPicture Picture { get; }

        /// <summary>Natural width, taken from the SVG's own canvas size — used when Shape() is called without an explicit w/h.</summary>
        public float Width { get; }

        /// <summary>Natural height, taken from the SVG's own canvas size.</summary>
        public float Height { get; }

        private PShape(SKPicture picture, float width, float height)
        {
            Picture = picture;
            Width = width;
            Height = height;
        }

        /// <summary>Loads an SVG file from disk. Throws if the file doesn't exist or isn't a parseable SVG.</summary>
        public static PShape LoadSvg(string path)
        {
            // Deliberately not wrapped in `using` — this SKSvg's Dispose()
            // also disposes the SKPicture it loaded, and PShape needs that
            // picture to outlive this method (it's what gets drawn later,
            // every frame, via Shape()). This leaks the small managed SKSvg
            // wrapper itself, not the picture — an acceptable trade for a
            // one-time load call.
            var svg = new SKSvg();

            SKPicture? picture;
            try
            {
                picture = svg.Load(path);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"No se pudo cargar el SVG: '{path}'. Verifica la ruta y el formato.", ex);
            }

            if (picture == null)
                throw new InvalidOperationException($"No se pudo cargar el SVG: '{path}'. Verifica la ruta y el formato.");

            // CanvasSize reflects the SVG's declared width/height (or its
            // viewBox) — more reliable than picture.CullRect, which can come
            // back zero for SVGs that omit an explicit viewBox.
            var size = svg.CanvasSize;
            return new PShape(picture, size.Width, size.Height);
        }

        public void Dispose() => Picture.Dispose();
    }
}