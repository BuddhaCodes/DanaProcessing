using System;

namespace DanaProcessing
{
    /// <summary>
    /// A 3D vector, equivalent to Processing's PVector —
    /// https://processing.org/reference/PVector.html. Useful for position,
    /// velocity, and acceleration in sketches (particle systems, steering,
    /// or plain 3D math for custom BeginShape()/Vertex()/Normal() geometry).
    /// Z defaults to 0 via the 2-argument constructor, so existing 2D sketch
    /// code that only ever used PVector(x, y) keeps working unchanged —
    /// Heading()/Rotate() are also unchanged from before and still only
    /// make sense for a vector lying in the XY plane (Z is ignored by both,
    /// same as real Processing's own documented behavior for 2D use).
    /// </summary>
    public struct PVector
    {
        public float X;
        public float Y;
        public float Z;

        public PVector(float x, float y)
        {
            X = x;
            Y = y;
            Z = 0f;
        }

        public PVector(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        // --- Instance operations (mutate this vector, matching Processing's style) ---

        public PVector Add(PVector v) { X += v.X; Y += v.Y; Z += v.Z; return this; }
        public PVector Sub(PVector v) { X -= v.X; Y -= v.Y; Z -= v.Z; return this; }
        public PVector Mult(float n) { X *= n; Y *= n; Z *= n; return this; }
        public PVector Div(float n) { X /= n; Y /= n; Z /= n; return this; }

        public float Mag() => MathF.Sqrt(X * X + Y * Y + Z * Z);
        public float MagSq() => X * X + Y * Y + Z * Z;

        public PVector Normalize()
        {
            float m = Mag();
            if (m != 0)
            { X /= m; Y /= m; Z /= m; }
            return this;
        }

        public PVector Limit(float max)
        {
            if (MagSq() > max * max)
            {
                Normalize();
                Mult(max);
            }
            return this;
        }

        /// <summary>Scales this vector to the given magnitude while keeping its direction, like Processing's setMag().</summary>
        public PVector SetMag(float mag)
        {
            Normalize();
            Mult(mag);
            return this;
        }

        /// <summary>Rotates this vector by the given angle in radians AROUND THE Z AXIS (i.e. within the XY plane), like Processing's rotate() — meant for 2D vectors; Z is left untouched. For a genuine 3D rotation, rotate the point with GraphicsContext's own RotateX/Y/Z() instead.</summary>
        public PVector Rotate(float angleRadians)
        {
            float cos = MathF.Cos(angleRadians);
            float sin = MathF.Sin(angleRadians);
            float newX = X * cos - Y * sin;
            float newY = X * sin + Y * cos;
            X = newX;
            Y = newY;
            return this;
        }

        /// <summary>Overwrites all three components at once, like Processing's set(x, y, z). Z defaults to 0 if omitted.</summary>
        public PVector Set(float x, float y, float z = 0f)
        {
            X = x;
            Y = y;
            Z = z;
            return this;
        }

        /// <summary>Moves this vector amt (0-1) of the way toward v, mutating this vector, like Processing's instance lerp(). For a non-mutating version, see the static Lerp below.</summary>
        public PVector Lerp(PVector v, float amt)
        {
            X += (v.X - X) * amt;
            Y += (v.Y - Y) * amt;
            Z += (v.Z - Z) * amt;
            return this;
        }

        /// <summary>Angle of this vector in the XY plane, in radians, like Processing's heading(). Ignores Z — meant for 2D vectors, matching Processing's own documented behavior.</summary>
        public float Heading() => MathF.Atan2(Y, X);

        public PVector Copy() => new PVector(X, Y, Z);

        /// <summary>Returns [X, Y, Z], like Processing's array() — handy when something wants a plain float[].</summary>
        public float[] Array() => new[] { X, Y, Z };

        // --- Static operations (return a new vector, don't mutate inputs) ---

        public static PVector Add(PVector a, PVector b) => new PVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static PVector Sub(PVector a, PVector b) => new PVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static PVector Mult(PVector a, float n) => new PVector(a.X * n, a.Y * n, a.Z * n);
        public static PVector Div(PVector a, float n) => new PVector(a.X / n, a.Y / n, a.Z / n);

        public static float Dist(PVector a, PVector b) => Sub(a, b).Mag();
        public static float Dot(PVector a, PVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        /// <summary>The 3D cross product of a and b, like Processing's static PVector.cross(a, b) — perpendicular to both, following the right-hand rule. For two vectors lying in the XY plane (Z=0), this comes out pointing purely along Z, which is exactly what you want for a 2D face's "up" normal.</summary>
        public static PVector Cross(PVector a, PVector b) => new PVector(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

        /// <summary>The cross product of this vector and v, like Processing's instance cross(v) — does NOT mutate this vector (unlike most other instance methods here), matching Processing's own cross() semantics, which always returns a new PVector.</summary>
        public readonly PVector Cross(PVector v) => Cross(this, v);

        public static PVector FromAngle(float angleRadians) =>
            new PVector(MathF.Cos(angleRadians), MathF.Sin(angleRadians));

        /// <summary>Linearly interpolates between two vectors without mutating either, like Processing's static lerp().</summary>
        public static PVector Lerp(PVector a, PVector b, float amt) =>
            new PVector(a.X + (b.X - a.X) * amt, a.Y + (b.Y - a.Y) * amt, a.Z + (b.Z - a.Z) * amt);

        /// <summary>Angle between two vectors, in radians, like Processing's angleBetween(). Works in 3D (uses the full dot product/magnitude, not just X/Y). Returns 0 if either vector has zero length.</summary>
        public static float AngleBetween(PVector a, PVector b)
        {
            float mags = a.Mag() * b.Mag();
            if (mags == 0)
                return 0f;
            float cos = Dot(a, b) / mags;
            cos = cos < -1f ? -1f : (cos > 1f ? 1f : cos); // guard against float drift pushing acos out of domain
            return MathF.Acos(cos);
        }

        public override string ToString() => $"[{X:0.###}, {Y:0.###}, {Z:0.###}]";
    }
}