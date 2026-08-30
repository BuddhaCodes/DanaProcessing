using System;

namespace DanaProcessing
{
    public abstract partial class Sketch
    {
        // =====================================================================
        // Random — https://processing.org/reference/randomSeed_.html and
        // randomGaussian_.html. Random(max)/Random(min,max) already live in
        // the main Sketch.cs and share the same _rand field this reseeds.
        // =====================================================================

        /// <summary>Reseeds the random generator so Random()/RandomGaussian() become repeatable, like Processing's randomSeed().</summary>
        public void RandomSeed(int seed) => _rand = new Random(seed);

        private float? _spareGaussian;

        /// <summary>A normally-distributed random value (mean 0, standard deviation 1), like Processing's randomGaussian(). Uses the Box-Muller transform, which produces two values per call — the second is cached and returned on the next call instead of being thrown away.</summary>
        public float RandomGaussian()
        {
            if (_spareGaussian.HasValue)
            {
                var cached = _spareGaussian.Value;
                _spareGaussian = null;
                return cached;
            }

            double u1 = 1.0 - _rand.NextDouble(); // (0, 1], avoids log(0)
            double u2 = _rand.NextDouble();
            double mag = Math.Sqrt(-2.0 * Math.Log(u1));

            _spareGaussian = (float)(mag * Math.Sin(2.0 * Math.PI * u2));
            return (float)(mag * Math.Cos(2.0 * Math.PI * u2));
        }

        // =====================================================================
        // Noise — https://processing.org/reference/noiseSeed_.html and
        // noiseDetail_.html. Noise(x)/Noise(x,y) already live in the main
        // Sketch.cs and call into PerlinNoise, which now supports both of these.
        // =====================================================================

        /// <summary>Reseeds the noise generator so Noise() becomes repeatable, like Processing's noiseSeed().</summary>
        public void NoiseSeed(int seed) => PerlinNoise.Seed(seed);

        /// <summary>Sets how many octaves Noise() sums and how quickly each one's contribution falls off (0-1), like Processing's noiseDetail(). Defaults to 4 octaves at a 0.5 falloff, matching Processing.</summary>
        public void NoiseDetail(int octaves, float falloff = 0.5f) => PerlinNoise.SetDetail(octaves, falloff);
    }
}
