using DanaProcessing.Audio;

namespace DanaProcessing.Reels
{
    /// <summary>What a typed key was, for its sound.</summary>
    public enum KeyStroke { Key, Space, Enter, Backspace }

    /// <summary>
    /// Keyboard sounds for typed code, synthesized (no sound files): a short
    /// filtered click plus a low "thock" of the key bottoming out. Space and
    /// Enter are deeper and louder, Backspace a little drier, and every
    /// press varies slightly in pitch and level so a run of keys doesn't
    /// sound like a machine gun.
    /// </summary>
    internal static class KeySounds
    {
        public const int SampleRate = 48000;
        private const int Variants = 6;
        private static readonly float[][][] Bank = Build();

        private static float[][][] Build()
        {
            var bank = new float[4][][];
            for (int kind = 0; kind < 4; kind++)
            {
                bank[kind] = new float[Variants][];
                for (int v = 0; v < Variants; v++)
                    bank[kind][v] = Synthesize((KeyStroke)kind, v);
            }
            return bank;
        }

        private static float[] Synthesize(KeyStroke kind, int variant)
        {
            var rng = new Random(1000 * (int)kind + variant);
            double len = kind is KeyStroke.Space or KeyStroke.Enter ? 0.09 : 0.06;
            int n = (int)(len * SampleRate);
            var data = new float[n];

            double thockHz = kind switch { KeyStroke.Space => 120, KeyStroke.Enter => 105, KeyStroke.Backspace => 175, _ => 160 } * (0.92 + 0.16 * rng.NextDouble());
            double clickTone = kind == KeyStroke.Backspace ? 0.18 : 0.12 + 0.06 * rng.NextDouble(); // brightness of the click
            double loud = kind switch { KeyStroke.Space => 1.0, KeyStroke.Enter => 1.1, _ => 0.8 } * (0.85 + 0.3 * rng.NextDouble());

            double lp = 0, hpPrev = 0, hp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SampleRate;
                // Click: noise through a one-pole band (low-pass, then remove the lows).
                double noise = rng.NextDouble() * 2 - 1;
                lp += clickTone * (noise - lp);
                hp = 0.9 * (hp + lp - hpPrev);
                hpPrev = lp;
                double click = hp * Math.Exp(-t / 0.006);
                // A second, quieter click as the key returns.
                double back = t > 0.028 ? hp * 0.35 * Math.Exp(-(t - 0.028) / 0.004) : 0;
                // Thock: a damped low sine.
                double thock = Math.Sin(2 * Math.PI * thockHz * t) * Math.Exp(-t / 0.012) * 0.6;
                double attack = Math.Min(1, t / 0.0006);
                data[i] = (float)((click * 1.6 + back + thock) * loud * attack * 0.5);
            }
            return data;
        }

        private static float[] For(KeyStroke kind, int seed) => Bank[(int)kind][(int)((uint)seed % Variants)];

        private static int Seed(double time) => (int)(time * 1000.0 * 7.31);

        /// <summary>Adds the clicks for <paramref name="strokes"/> (seconds) into an interleaved stereo buffer at <see cref="SampleRate"/>.</summary>
        public static void Mix(float[] stereo, IEnumerable<(double Time, KeyStroke Kind)> strokes, double volume)
        {
            if (volume <= 0) return;
            int frames = stereo.Length / 2;
            foreach (var (time, kind) in strokes)
            {
                var s = For(kind, Seed(time));
                long start = (long)Math.Round(time * SampleRate);
                // A touch of stereo spread, as if the keys were in front of you.
                double pan = ((uint)Seed(time) % 100 / 100.0 - 0.5) * 0.3;
                float l = (float)(volume * (1 - pan) * 0.5), r = (float)(volume * (1 + pan) * 0.5);
                for (int i = 0; i < s.Length; i++)
                {
                    long f = start + i;
                    if (f < 0 || f >= frames) continue;
                    stereo[f * 2] += s[i] * l * 2;
                    stereo[f * 2 + 1] += s[i] * r * 2;
                }
            }
        }

        private static readonly SampleBuffer?[] Samples = new SampleBuffer?[4 * Variants];

        /// <summary>The click for one key as a sample, for live playback (the preview).</summary>
        public static SampleBuffer Sample(KeyStroke kind, double time)
        {
            int v = (int)((uint)Seed(time) % Variants), k = (int)kind * Variants + v;
            return Samples[k] ??= SampleBuffer.FromMono(Bank[(int)kind][v], SampleRate, "key");
        }
    }
}
