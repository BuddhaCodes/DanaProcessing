namespace DanaProcessing.Audio
{
    /// <summary>
    /// Loudness the way YouTube, Spotify and ffmpeg's ebur128 measure it
    /// (ITU-R BS.1770 / EBU R128: K-weighting, 400 ms blocks, absolute and
    /// relative gates), and normalization to a target. Used by reel exports so
    /// a video comes out at a standard level (-14 LUFS by default) instead of
    /// however loud the sketch happened to play.
    /// </summary>
    public static class Loudness
    {
        /// <summary>Integrated loudness in LUFS of interleaved stereo samples; -infinity for silence.</summary>
        public static double IntegratedLufs(float[] stereo, int sampleRate)
        {
            var blocks = BlockPowers(stereo, sampleRate);
            return Gate(blocks);
        }

        /// <summary>The largest absolute sample value (0..1+).</summary>
        public static float SamplePeak(float[] samples)
        {
            float peak = 0;
            foreach (var s in samples)
                peak = Math.Max(peak, Math.Abs(s));
            return peak;
        }

        /// <summary>
        /// Brings interleaved stereo <paramref name="stereo"/> to <paramref name="targetLufs"/>, in place:
        /// one overall gain, then a look-ahead limiter so no sample goes past
        /// <paramref name="ceilingDb"/> dBFS (a little under 0 leaves room for the
        /// AAC encoder's own overshoot). Silence is left alone.
        /// Returns the loudness measured before and the gain applied, in dB.
        /// </summary>
        public static (double MeasuredLufs, double GainDb) Normalize(float[] stereo, int sampleRate,
                                                                     double targetLufs = -14, double ceilingDb = -1.5)
        {
            double measured = IntegratedLufs(stereo, sampleRate);
            if (double.IsNegativeInfinity(measured) || double.IsNaN(measured))
                return (measured, 0);

            // Gain, then limit; limiting shaves off a little energy, so measure
            // again and top up (a few passes converge to within 0.1 LU).
            float ceiling = (float)Math.Pow(10, ceilingDb / 20);
            double totalDb = 0, current = measured;
            for (int pass = 0; pass < 4 && Math.Abs(targetLufs - current) > 0.05; pass++)
            {
                double stepDb = targetLufs - current;
                float gain = (float)Math.Pow(10, stepDb / 20);
                for (int i = 0; i < stereo.Length; i++)
                    stereo[i] *= gain;
                Limit(stereo, sampleRate, ceiling);
                totalDb += stepDb;
                current = IntegratedLufs(stereo, sampleRate);
            }
            return (measured, totalDb);
        }

        // ------------------------------------------------------------------

        /// <summary>Mean square of the K-weighted signal, both channels summed, for each 400 ms block (75 % overlap).</summary>
        private static List<double> BlockPowers(float[] stereo, int rate)
        {
            int frames = stereo.Length / 2;
            var kl = new KWeighting(rate);
            var kr = new KWeighting(rate);
            int step = Math.Max(1, rate / 10);           // 100 ms
            int stepsPerBlock = 4;                       // 400 ms
            var stepSums = new List<double>();
            double acc = 0;
            int inStep = 0;
            for (int i = 0; i < frames; i++)
            {
                double l = kl.Process(stereo[2 * i]);
                double r = kr.Process(stereo[2 * i + 1]);
                acc += l * l + r * r;
                if (++inStep == step)
                {
                    stepSums.Add(acc);
                    acc = 0;
                    inStep = 0;
                }
            }
            var blocks = new List<double>();
            for (int b = 0; b + stepsPerBlock <= stepSums.Count; b++)
            {
                double sum = 0;
                for (int k = 0; k < stepsPerBlock; k++)
                    sum += stepSums[b + k];
                blocks.Add(sum / (step * stepsPerBlock));
            }
            return blocks;
        }

        private static double BlockLufs(double power) => -0.691 + 10 * Math.Log10(power);

        private static double Gate(List<double> blocks)
        {
            var aboveAbsolute = blocks.Where(p => p > 0 && BlockLufs(p) > -70).ToList();
            if (aboveAbsolute.Count == 0)
                return double.NegativeInfinity;
            double relative = BlockLufs(aboveAbsolute.Average()) - 10;
            var gated = aboveAbsolute.Where(p => BlockLufs(p) > relative).ToList();
            return gated.Count == 0 ? double.NegativeInfinity : BlockLufs(gated.Average());
        }

        /// <summary>
        /// Look-ahead peak limiter: the gain starts coming down ~2 ms before a
        /// peak (so there's no click), is never above what keeps that peak under
        /// <paramref name="ceiling"/>, and recovers over ~80 ms after it.
        /// </summary>
        private static void Limit(float[] stereo, int rate, float ceiling)
        {
            int frames = stereo.Length / 2;
            if (frames == 0)
                return;
            var need = new float[frames];
            bool any = false;
            for (int i = 0; i < frames; i++)
            {
                float peak = Math.Max(Math.Abs(stereo[2 * i]), Math.Abs(stereo[2 * i + 1]));
                need[i] = peak > ceiling ? ceiling / peak : 1f;
                any |= need[i] < 1f;
            }
            if (!any)
                return;

            // Backwards: ramp down ahead of each peak (attack ~2 ms).
            float attack = 1f / Math.Max(1, (int)(rate * 0.002));
            var gain = new float[frames];
            gain[frames - 1] = need[frames - 1];
            for (int i = frames - 2; i >= 0; i--)
                gain[i] = Math.Min(need[i], gain[i + 1] + attack);
            // Forwards: come back up slowly after it (release ~80 ms), never above what's needed.
            float release = 1f - (float)Math.Exp(-1.0 / (rate * 0.08));
            for (int i = 1; i < frames; i++)
                gain[i] = Math.Min(gain[i], gain[i - 1] + (1 - gain[i - 1]) * release);
            for (int i = 0; i < frames; i++)
            {
                stereo[2 * i] *= gain[i];
                stereo[2 * i + 1] *= gain[i];
            }
        }

        /// <summary>BS.1770's K-weighting: a high shelf (+4 dB above ~1.7 kHz, the head's effect) then a ~38 Hz high-pass.</summary>
        private sealed class KWeighting
        {
            private readonly Biquad _shelf, _highPass;

            public KWeighting(int rate)
            {
                {
                    double f0 = 1681.974450955533, g = 3.999843853973347, q = 0.7071752369554196;
                    double k = Math.Tan(Math.PI * f0 / rate);
                    double vh = Math.Pow(10, g / 20), vb = Math.Pow(vh, 0.4996667741545416);
                    double a0 = 1 + k / q + k * k;
                    _shelf = new Biquad((vh + vb * k / q + k * k) / a0, 2 * (k * k - vh) / a0, (vh - vb * k / q + k * k) / a0,
                                        2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0);
                }
                {
                    double f0 = 38.13547087602444, q = 0.5003270373238773;
                    double k = Math.Tan(Math.PI * f0 / rate);
                    double a0 = 1 + k / q + k * k;
                    _highPass = new Biquad(1, -2, 1, 2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0);
                }
            }

            public double Process(double x) => _highPass.Process(_shelf.Process(x));
        }

        private sealed class Biquad
        {
            private readonly double _b0, _b1, _b2, _a1, _a2;
            private double _x1, _x2, _y1, _y2;

            public Biquad(double b0, double b1, double b2, double a1, double a2)
            {
                _b0 = b0; _b1 = b1; _b2 = b2; _a1 = a1; _a2 = a2;
            }

            public double Process(double x)
            {
                double y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
                _x2 = _x1; _x1 = x;
                _y2 = _y1; _y1 = y;
                return y;
            }
        }
    }
}
