using System;
using SkiaSharp;

namespace DanaProcessing
{
    /// <summary>
    /// A loaded/created font, equivalent to Processing's PFont. Get one via
    /// Sketch.CreateFont(family, size) (an installed system font, sized on
    /// creation) or Sketch.LoadFont(path, size) (a font file loaded
    /// directly), then activate it with TextFont(font).
    ///
    /// Processing's own loadFont() actually loads a pre-baked .vlw bitmap
    /// font produced by the PDE's Tools > Create Font — DanaProcessing has
    /// no equivalent pipeline, so LoadFont() here instead loads a real font
    /// file (.ttf/.otf/etc.) directly, which covers the same practical need
    /// (get a specific font onto the canvas) without the .vlw step.
    /// </summary>
    public sealed class PFont : IDisposable
    {
        internal SKTypeface Typeface { get; }

        /// <summary>The size this font was created/loaded at. TextFont(font) uses this unless you pass an explicit size override.</summary>
        public float Size { get; }

        internal PFont(SKTypeface typeface, float size)
        {
            Typeface = typeface;
            Size = size;
        }

        /// <summary>Creates a font from an installed system font family name, like Processing's createFont("Arial", 24). SKTypeface.FromFamilyName never throws for an unknown name — it silently falls back to the system default font instead — matching Processing's own tolerant behavior here.</summary>
        public static PFont CreateFromFamily(string family, float size)
        {
            var typeface = SKTypeface.FromFamilyName(family) ?? SKTypeface.Default;
            return new PFont(typeface, size);
        }

        /// <summary>Loads a font file (.ttf/.otf/etc.) from disk. Throws if the file doesn't exist or isn't a parseable font — see the class remark above re: Processing's actual .vlw-based loadFont().</summary>
        public static PFont LoadFromFile(string path, float size)
        {
            var typeface = SKTypeface.FromFile(path)
                ?? throw new InvalidOperationException($"No se pudo cargar la fuente: '{path}'. Verifica la ruta y el formato.");
            return new PFont(typeface, size);
        }

        /// <summary>Disposes the underlying typeface. Only call this once nothing is still drawing with the font (i.e. after TextFont() has moved on to something else) — GraphicsContext.Dispose() does NOT dispose a typeface set via TextFont(PFont, ...), precisely so it doesn't fight with this.</summary>
        public void Dispose() => Typeface.Dispose();
    }
}