using System;

namespace DanaProcessing
{
    public abstract partial class Sketch
    {
        // =====================================================================
        // Random — https://processing.org/reference/randomSeed_.html and
        // randomGaussian_.html. Random(max)/Random(min,max) already live in
        // the main Sketch.cs and draw from Rng below.
        //
        // Every sketch has a seed. The IDE picks one before Setup() (a new one
        // per Run, or the same one while 🔒 is on, or one read back from a PNG);
        // a sketch run any other way picks a random one the first time it
        // needs randomness. Random(), RandomGaussian() and Noise() all follow
        // it, it's written into every PNG the sketch saves, and the same seed
        // gives the same drawing again -- as long as the sketch only uses these
        // (not new System.Random()).
        // =====================================================================

        // Per sketch (it used to be one static generator shared by every
        // sketch, so a preview could change what the IDE's sketch drew).
        private Random? _rng;
        private PerlinNoise? _noise;
        private int _seed;
        private bool _seeded;

        private Random Rng
        {
            get
            {
                if (_rng == null)
                    EnsureSeeded();
                return _rng!;
            }
        }

        private PerlinNoise NoiseGen => _noise ??= new PerlinNoise();

        // The generator of the sketch whose code last ran on this thread, so
        // helpers that aren't sketch methods -- PVector.Random2D()/Random3D(),
        // FloatList/IntList/StringList.Shuffle() -- follow the seed too.
        [ThreadStatic] private static Random? _ambient;

        /// <summary>The running sketch's generator on this thread (System.Random.Shared outside any sketch).</summary>
        internal static Random AmbientRandom => _ambient ?? System.Random.Shared;

        /// <summary>RenderFrame: this thread's helpers now draw from this sketch's generator.</summary>
        internal void MakeAmbientRandom() => _ambient = Rng;

        private void EnsureSeeded()
        {
            if (!_seeded)
                ApplySeed(System.Random.Shared.Next(1, 1_000_000));
        }

        /// <summary>
        /// The seed this sketch's Random(), RandomGaussian() and Noise() follow.
        /// Run the same sketch with the same seed and it draws the same thing.
        /// </summary>
        public int Seed
        {
            get
            {
                EnsureSeeded();
                return _seed;
            }
        }

        /// <summary>Host hook (the IDE's 🎲 / 🔒, the variations sheet): seeds Random() AND Noise() before Setup() runs.</summary>
        internal void ApplySeed(int seed)
        {
            _seed = seed;
            _seeded = true;
            _rng = new Random(seed);
            _ambient = _rng;
            _spareGaussian = null;
            NoiseGen.Seed(seed);
        }

        /// <summary>Reseeds the random generator so Random()/RandomGaussian() become repeatable, like Processing's randomSeed(). Also becomes the sketch's <see cref="Seed"/>.</summary>
        public void RandomSeed(int seed)
        {
            _seed = seed;
            _seeded = true;
            _rng = new Random(seed);
            _ambient = _rng;
            _spareGaussian = null;
        }

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

            double u1 = 1.0 - Rng.NextDouble(); // (0, 1], avoids log(0)
            double u2 = Rng.NextDouble();
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
        public void NoiseSeed(int seed)
        {
            EnsureSeeded(); // so a later first Random() can't reseed the noise behind your back
            NoiseGen.Seed(seed);
        }

        /// <summary>Sets how many octaves Noise() sums and how quickly each one's contribution falls off (0-1), like Processing's noiseDetail(). Defaults to 4 octaves at a 0.5 falloff, matching Processing.</summary>
        public void NoiseDetail(int octaves, float falloff = 0.5f) => NoiseGen.SetDetail(octaves, falloff);
    }
}