using System;

namespace DanaProcessing
{
    public abstract partial class Sketch
    {
        // =====================================================================
        // Constants — https://processing.org/reference/PI.html and siblings.
        // Angle-taking functions throughout DanaProcessing use radians (matching
        // Processing's actual convention), with the one deliberate exception of
        // Rotate(degrees) noted where it's declared in Sketch.cs.
        // =====================================================================
        public const float PI = MathF.PI;
        public const float TWO_PI = MathF.PI * 2f;
        public const float HALF_PI = MathF.PI / 2f;
        public const float QUARTER_PI = MathF.PI / 4f;

        /// <summary>Same value as TWO_PI — included since "tau" is how a lot of people think about a full turn nowadays.</summary>
        public const float TAU = TWO_PI;

        // =====================================================================
        // Trigonometry — https://processing.org/reference/sin_.html and siblings.
        // Same signatures as Processing's globals: radians in, radians out.
        // =====================================================================
        public float Sin(float angle) => MathF.Sin(angle);
        public float Cos(float angle) => MathF.Cos(angle);
        public float Tan(float angle) => MathF.Tan(angle);
        public float Asin(float value) => MathF.Asin(value);
        public float Acos(float value) => MathF.Acos(value);
        public float Atan(float value) => MathF.Atan(value);
        public float Atan2(float y, float x) => MathF.Atan2(y, x);

        /// <summary>Converts radians to degrees, like Processing's degrees().</summary>
        public float Degrees(float radians) => radians * (180f / PI);

        /// <summary>Converts degrees to radians, like Processing's radians().</summary>
        public float Radians(float degrees) => degrees * (PI / 180f);

        // =====================================================================
        // General math — Processing keeps these as free functions rather than
        // making you reach for System.Math/MathF directly; mirrored here for
        // the same feel. https://processing.org/reference/constrain_.html etc.
        // =====================================================================

        /// <summary>Clamps value into [min, max], like Processing's constrain().</summary>
        public float Constrain(float value, float min, float max) => value < min ? min : (value > max ? max : value);

        /// <summary>Clamps value into [min, max], like Processing's constrain().</summary>
        public int Constrain(int value, int min, int max) => value < min ? min : (value > max ? max : value);

        /// <summary>Distance between two 2D points, like Processing's dist(). For two PVectors, see PVector.Dist.</summary>
        public float Dist(float x1, float y1, float x2, float y2)
        {
            float dx = x2 - x1, dy = y2 - y1;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Distance between two 3D points, like Processing's dist().</summary>
        public float Dist(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            float dx = x2 - x1, dy = y2 - y1, dz = z2 - z1;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>Magnitude (distance from the origin) of a 2D vector given as loose components, like Processing's free-standing mag(x, y). For a PVector, use PVector.Mag() instead — this is just the same formula exposed as a global for when you have loose floats instead of a PVector.</summary>
        public float Mag(float x, float y) => MathF.Sqrt(x * x + y * y);

        /// <summary>Magnitude of a 3D vector given as loose components, like Processing's mag(x, y, z).</summary>
        public float Mag(float x, float y, float z) => MathF.Sqrt(x * x + y * y + z * z);

        /// <summary>Linear interpolation between two scalars, like Processing's lerp(). For vectors, see PVector.Lerp.</summary>
        public float Lerp(float start, float stop, float amt) => start + (stop - start) * amt;

        /// <summary>Normalizes value from [start, stop] into [0, 1] — the inverse of Lerp/Map, like Processing's norm().</summary>
        public float Norm(float value, float start, float stop) => (value - start) / (stop - start);

        public float Sq(float value) => value * value;
        public float Sqrt(float value) => MathF.Sqrt(value);
        public float Abs(float value) => MathF.Abs(value);
        public int Abs(int value) => Math.Abs(value);
        public float Ceil(float value) => MathF.Ceiling(value);
        public float Floor(float value) => MathF.Floor(value);
        public float Round(float value) => MathF.Round(value);
        public float Pow(float baseValue, float exponent) => MathF.Pow(baseValue, exponent);
        public float Log(float value) => MathF.Log(value);
        public float Exp(float value) => MathF.Exp(value);

        public float Max(float a, float b) => MathF.Max(a, b);
        public float Min(float a, float b) => MathF.Min(a, b);
        public int Max(int a, int b) => Math.Max(a, b);
        public int Min(int a, int b) => Math.Min(a, b);

        /// <summary>Largest of any number of values, like Processing's max(a, b, c, ...).</summary>
        public float Max(params float[] values)
        {
            float m = values[0];
            for (int i = 1; i < values.Length; i++)
            if (values[i] > m)
                m = values[i];
            return m;
        }

        /// <summary>Smallest of any number of values, like Processing's min(a, b, c, ...).</summary>
        public float Min(params float[] values)
        {
            float m = values[0];
            for (int i = 1; i < values.Length; i++)
            if (values[i] < m)
                m = values[i];
            return m;
        }
    }
}