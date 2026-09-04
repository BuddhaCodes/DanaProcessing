using System;
using System.Globalization;

namespace DanaProcessing
{
    public abstract partial class Sketch
    {
        // =====================================================================
        // Conversion — https://processing.org/reference/int_.html and siblings
        // (float(), boolean(), byte(), char(), str(), hex(), unhex()). In
        // Java, Processing needed these because primitive conversions weren't
        // always implicit and there was no operator overloading; C# already
        // has `(int)`/`(float)` casts and Convert.ToXxx, so these exist purely
        // for 1:1 API parity with sketch code ported from Processing — prefer
        // a plain C# cast in new DanaProcessing code.
        // =====================================================================

        /// <summary>Converts a float to an int by truncating toward zero, like Processing's int(float) — NOT rounding (use Round() first if you want that).</summary>
        public int Int(float value) => (int)value;

        /// <summary>Parses a string as an int, like Processing's int(string). Returns 0 if it doesn't parse, matching Processing's tolerant behavior (Processing actually returns null/NaN-ish MIN value on failure for some overloads, but 0 is the practical, commonly-relied-on fallback used across the reference examples).</summary>
        public int Int(string value) => int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

        /// <summary>Converts a bool to 1 or 0, like Processing's int(boolean).</summary>
        public int Int(bool value) => value ? 1 : 0;

        /// <summary>Converts an int to a float, like Processing's float(int) — provided for symmetry/parity even though C# already does this implicitly.</summary>
        public float Float(int value) => value;

        /// <summary>Parses a string as a float, like Processing's float(string). Returns Float.NaN on failure, matching Processing's own documented behavior for unparsable input.</summary>
        public float Float(string value) => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : float.NaN;

        /// <summary>Converts a number to a boolean: nonzero is true, zero is false, like Processing's boolean(int/float).</summary>
        public bool Boolean(float value) => value != 0;

        /// <summary>Parses "true"/"false" (case-insensitive) as a boolean, like Processing's boolean(string). Anything else is false.</summary>
        public bool Boolean(string value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

        /// <summary>Converts an int to a byte, like Processing's byte(int) — wraps using two's-complement truncation to match Java/Processing's own byte() rather than throwing on overflow.</summary>
        public byte Byte(int value) => unchecked((byte)value);

        /// <summary>Converts a float to a byte via truncation then the same wraparound as Byte(int), like Processing's byte(float).</summary>
        public byte Byte(float value) => Byte((int)value);

        /// <summary>Converts a char to a byte (its low 8 bits), like Processing's byte(char).</summary>
        public byte Byte(char value) => unchecked((byte)value);

        /// <summary>Converts a number to its character, like Processing's char(int) — e.g. Char(65) is 'A'.</summary>
        public char Char(int value) => (char)value;

        /// <summary>Converts a byte to its character, like Processing's char(byte).</summary>
        public char Char(byte value) => (char)value;

        // Str() overloads: C# already renders every one of these types
        // reasonably via ToString(), so these mainly exist so ported sketch
        // code that calls Str(x) compiles without edits.
        public string Str(int value) => value.ToString(CultureInfo.InvariantCulture);
        public string Str(float value) => value.ToString(CultureInfo.InvariantCulture);
        public string Str(bool value) => value ? "true" : "false";
        public string Str(char value) => value.ToString();

        /// <summary>Converts a number to its hexadecimal string, zero-padded to 8 digits, like Processing's hex(int).</summary>
        public string Hex(int value) => unchecked((uint)value).ToString("X8", CultureInfo.InvariantCulture);

        /// <summary>Converts a number to hex, zero-padded to `digits` characters, like Processing's hex(int, digits).</summary>
        public string Hex(int value, int digits) => unchecked((uint)value).ToString("X" + Math.Max(1, digits), CultureInfo.InvariantCulture);

        /// <summary>Converts a byte to a 2-digit hex string, like Processing's hex(byte).</summary>
        public string Hex(byte value) => value.ToString("X2", CultureInfo.InvariantCulture);

        /// <summary>Converts a char to a 4-digit hex string, like Processing's hex(char).</summary>
        public string Hex(char value) => ((int)value).ToString("X4", CultureInfo.InvariantCulture);

        /// <summary>Parses a hexadecimal string back into an int, like Processing's unhex(string). Ignores an optional leading "0x"/"0X". Returns 0 if the text isn't valid hex.</summary>
        public int Unhex(string value)
        {
            if (value == null)
                return 0;
            var trimmed = value.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(2);
            return uint.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? unchecked((int)v) : 0;
        }
    }
}