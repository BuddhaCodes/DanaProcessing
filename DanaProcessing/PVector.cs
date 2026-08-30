using System;

namespace DanaProcessing
{
    /// <summary>
    /// A 2D vector, equivalent to Processing's PVector. Useful for position,
    /// velocity, and acceleration in sketches (e.g. particle systems).
    /// </summary>
    public struct PVector
    {
        public float X;
        public float Y;

        public PVector(float x, float y)
        {
            X = x;
            Y = y;
        }

        // --- Instance operations (mutate this vector, matching Processing's style) ---

        public PVector Add(PVector v) { X += v.X; Y += v.Y; return this; }
        public PVector Sub(PVector v) { X -= v.X; Y -= v.Y; return this; }
        public PVector Mult(float n) { X *= n; Y *= n; return this; }
        public PVector Div(float n) { X /= n; Y /= n; return this; }

        public float Mag() => MathF.Sqrt(X * X + Y * Y);
        public float MagSq() => X * X + Y * Y;

        public PVector Normalize()
        {
            float m = Mag();
            if (m != 0) { X /= m; Y /= m; }
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

        /// <summary>Rotates this vector by the given angle in radians, like Processing's rotate().</summary>
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

        /// <summary>Overwrites both components at once, like Processing's set().</summary>
        public PVector Set(float x, float y)
        {
            X = x;
            Y = y;
            return this;
        }

        /// <summary>Moves this vector amt (0-1) of the way toward v, mutating this vector, like Processing's instance lerp(). For a non-mutating version, see the static Lerp below.</summary>
        public PVector Lerp(PVector v, float amt)
        {
            X += (v.X - X) * amt;
            Y += (v.Y - Y) * amt;
            return this;
        }

        public float Heading() => MathF.Atan2(Y, X);

        public PVector Copy() => new PVector(X, Y);

        /// <summary>Returns [X, Y], like Processing's array() — handy when something wants a plain float[].</summary>
        public float[] Array() => new[] { X, Y };

        // --- Static operations (return a new vector, don't mutate inputs) ---

        public static PVector Add(PVector a, PVector b) => new PVector(a.X + b.X, a.Y + b.Y);
        public static PVector Sub(PVector a, PVector b) => new PVector(a.X - b.X, a.Y - b.Y);
        public static PVector Mult(PVector a, float n) => new PVector(a.X * n, a.Y * n);
        public static PVector Div(PVector a, float n) => new PVector(a.X / n, a.Y / n);

        public static float Dist(PVector a, PVector b) => Sub(a, b).Mag();
        public static float Dot(PVector a, PVector b) => a.X * b.X + a.Y * b.Y;

        public static PVector FromAngle(float angleRadians) =>
            new PVector(MathF.Cos(angleRadians), MathF.Sin(angleRadians));

        /// <summary>Linearly interpolates between two vectors without mutating either, like Processing's static lerp().</summary>
        public static PVector Lerp(PVector a, PVector b, float amt) =>
            new PVector(a.X + (b.X - a.X) * amt, a.Y + (b.Y - a.Y) * amt);

        /// <summary>Angle between two vectors, in radians, like Processing's angleBetween(). Returns 0 if either vector has zero length.</summary>
        public static float AngleBetween(PVector a, PVector b)
        {
            float mags = a.Mag() * b.Mag();
            if (mags == 0) return 0f;
            float cos = Dot(a, b) / mags;
            cos = cos < -1f ? -1f : (cos > 1f ? 1f : cos); // guard against float drift pushing acos out of domain
            return MathF.Acos(cos);
        }

        public override string ToString() => $"[{X:0.###}, {Y:0.###}]";
    }
}
