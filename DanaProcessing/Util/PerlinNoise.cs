using System;

namespace DanaProcessing
{
    /// <summary>
    /// Classic Perlin noise (Ken Perlin's reference algorithm, public domain).
    /// Powers Sketch.Noise(x) / Sketch.Noise(x, y) for smooth pseudo-random values,
    /// the same tool Processing's noise() provides for organic motion.
    ///
    /// Default behavior sums 4 octaves at a 0.5 falloff — the same defaults
    /// Processing's own noiseDetail() starts with — rather than the single flat
    /// octave this used to compute. Call Sketch.NoiseSeed()/NoiseDetail() to
    /// change either.
    /// </summary>
    internal static class PerlinNoise
    {
        private static readonly int[] Permutation = {
            151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,
            8,99,37,240,21,10,23,190,6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,
            35,11,32,57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,74,165,71,
            134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,
            55,46,245,40,244,102,143,54,65,25,63,161,1,216,80,73,209,76,132,187,208,89,18,
            169,200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,52,217,226,250,
            124,123,5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,
            28,42,223,183,170,213,119,248,152,2,44,154,163,70,221,153,101,155,167,43,172,9,
            129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,218,246,97,228,251,
            34,242,193,238,210,144,12,191,179,162,241,81,51,145,235,249,14,239,107,49,192,
            214,31,181,199,106,157,184,84,204,176,215,205,3
        };

        private static readonly int[] P = new int[512];

        // Defaults matching Processing's own noiseDetail() starting values.
        private static int _octaves = 4;
        private static float _falloff = 0.5f;

        static PerlinNoise()
        {
            for (int i = 0; i < 256; i++)
                P[i] = P[i + 256] = Permutation[i % Permutation.Length];
        }

        /// <summary>Reshuffles the permutation table from the given seed, so Noise() becomes repeatable. Called by Sketch.NoiseSeed().</summary>
        public static void Seed(int seed)
        {
            var rnd = new Random(seed);
            var perm = new int[256];
            for (int i = 0; i < 256; i++)
                perm[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (perm[i], perm[j]) = (perm[j], perm[i]);
            }
            for (int i = 0; i < 256; i++)
                P[i] = P[i + 256] = perm[i];
        }

        /// <summary>Sets octave count and falloff for the multi-octave sum below. Called by Sketch.NoiseDetail().</summary>
        public static void SetDetail(int octaves, float falloff)
        {
            _octaves = Math.Max(1, octaves);
            _falloff = falloff;
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static float Lerp(float t, float a, float b) => a + t * (b - a);

        private static float Grad(int hash, float x, float y)
        {
            int h = hash & 7;
            float u = h < 4 ? x : y;
            float v = h < 4 ? y : x;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        /// <summary>Single-octave raw noise in roughly [-1, 1] — the original implementation, now just one ingredient of the multi-octave sum below.</summary>
        private static float RawNoiseSigned(float x, float y)
        {
            int xi = (int)MathF.Floor(x) & 255;
            int yi = (int)MathF.Floor(y) & 255;
            float xf = x - MathF.Floor(x);
            float yf = y - MathF.Floor(y);

            float u = Fade(xf);
            float v = Fade(yf);

            int aa = P[P[xi] + yi];
            int ab = P[P[xi] + yi + 1];
            int ba = P[P[xi + 1] + yi];
            int bb = P[P[xi + 1] + yi + 1];

            float x1 = Lerp(u, Grad(aa, xf, yf), Grad(ba, xf - 1, yf));
            float x2 = Lerp(u, Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1));

            return Lerp(v, x1, x2);
        }

        /// <summary>Sums _octaves layers of RawNoiseSigned at doubling frequency and _falloff-scaled amplitude, then remaps the result to Processing-style [0, 1].</summary>
        public static float Noise(float x, float y)
        {
            float total = 0f;
            float frequency = 1f;
            float amplitude = 1f;
            float maxValue = 0f;

            for (int i = 0; i < _octaves; i++)
            {
                total += RawNoiseSigned(x * frequency, y * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= _falloff;
                frequency *= 2f;
            }

            float normalized = total / maxValue; // back to roughly [-1, 1]
            return (normalized + 1f) / 2f;         // remap to Processing-style [0, 1]
        }
    }
}