using SkiaSharp;

namespace DanaProcessing
{
    /// <summary>
    /// An RGBA color — DanaProcessing's equivalent of Processing's color type.
    /// Processing packs color into a bit-shifted int (0xAARRGGBB) and exposes
    /// it via a color(r,g,b) *function* because Java has no operator
    /// overloading and wanted colors to fit in a primitive. C# has neither
    /// constraint, so this is a proper struct with real constructors instead
    /// — build one with `new Color(r, g, b)` rather than calling a function
    /// named the same as the type (which C# doesn't allow within the same
    /// class that also needs to reference the type — see the ShapeMode/
    /// ShapeAlignMode naming note elsewhere in the codebase for the same
    /// collision in a different spot).
    ///
    /// Wraps SkiaSharp's SKColor internally, but that wrapping is invisible
    /// from sketch code — the Skia property is internal, so a sketch never
    /// needs `using SkiaSharp;` just to hold onto a color, matching
    /// Processing's own self-contained color API.
    /// </summary>
    public readonly struct Color
    {
        internal SKColor Skia { get; }

        /// <summary>Builds a color from RGB components (each 0-255), always interpreted as RGB regardless of the current ColorMode — matches Processing's color(r,g,b) used outside colorMode(HSB).</summary>
        public Color(byte r, byte g, byte b, byte a = 255) : this(new SKColor(r, g, b, a)) { }

        internal Color(SKColor skia) => Skia = skia;

        public byte R => Skia.Red;
        public byte G => Skia.Green;
        public byte B => Skia.Blue;
        public byte A => Skia.Alpha;

        /// <summary>Builds a color from Hue (0-360), Saturation (0-100), Brightness (0-100), regardless of the current ColorMode — matches Processing's color(h,s,b) used inside colorMode(HSB).</summary>
        public static Color FromHsb(float h, float s, float br, byte a = 255) => new Color(SKColor.FromHsv(h, s, br, a));

        public override string ToString() => $"Color({R}, {G}, {B}, {A})";

        public static bool operator ==(Color a, Color b) => a.Skia == b.Skia;
        public static bool operator !=(Color a, Color b) => a.Skia != b.Skia;
        public override bool Equals(object? obj) => obj is Color other && this == other;
        public override int GetHashCode() => Skia.GetHashCode();
    }
}