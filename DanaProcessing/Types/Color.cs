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
    /// named the same as the type. That's specifically because a type can't
    /// have a member with its own name (C# error CS0542) — this struct
    /// itself could never declare a method called Color(...) — NOT because
    /// no class anywhere is allowed to. GraphicsContext (a different type)
    /// does exactly that: GraphicsContext.Color(r, g, b) is the actual
    /// ColorMode-aware equivalent of Processing's color(...) function, and
    /// it coexists with `new Color(...)` here without any ambiguity, since
    /// `new` always forces the constructor and type-positions never consider
    /// method groups. See the ShapeMode() declaration in GraphicsContext.cs
    /// for the actual ShapeMode/ShapeAlignMode naming note — that one really
    /// was just a naming choice (to keep the enum and the method visually
    /// distinct), not a hard compiler constraint like this one.
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