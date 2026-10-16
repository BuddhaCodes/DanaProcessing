using System;
using System.Threading;

namespace DanaProcessing.Audio
{
    /// <summary>
    /// The analysis taps behind Amplitude, Waveform() and Spectrum(): a ring of
    /// the latest mono samples plus a smoothed loudness. One writer thread (the
    /// audio thread, or the microphone's capture thread) and any number of
    /// readers. Writing never allocates and never locks.
    /// </summary>
    internal sealed class AudioAnalyzer
    {
        public const int RingSize = 4096;

        private readonly float[] _ring = new float[RingSize];
        private int _ringPos;
        private float _amplitude;

        public int SampleRate { get; set; } = 48000;

        /// <summary>Smoothed loudness (RMS with a fast attack and a slow release), 0..1.</summary>
        public float Amplitude => Volatile.Read(ref _amplitude);

        /// <summary>Forgets everything heard so far (before a new writer starts).</summary>
        public void Reset()
        {
            Array.Clear(_ring);
            Volatile.Write(ref _amplitude, 0f);
        }

        /// <summary>Writer thread: one block of mono samples. Updates the ring and the loudness.</summary>
        public void Write(ReadOnlySpan<float> mono)
        {
            if (mono.Length == 0)
                return;
            double sumSquares = 0;
            int ring = _ringPos;
            foreach (float v in mono)
            {
                sumSquares += v * v;
                _ring[ring] = v;
                ring = (ring + 1) & (RingSize - 1);
            }
            Volatile.Write(ref _ringPos, ring);
            UpdateAmplitude((float)Math.Sqrt(sumSquares / mono.Length));
        }

        /// <summary>Writer thread, sample by sample (the engine's master loop); call <see cref="UpdateAmplitude"/> once per block.</summary>
        public void WriteSample(float mono)
        {
            int ring = _ringPos;
            _ring[ring] = mono;
            Volatile.Write(ref _ringPos, (ring + 1) & (RingSize - 1));
        }

        public void UpdateAmplitude(float rms)
        {
            float amp = _amplitude;
            amp = rms > amp ? rms : amp * 0.85f + rms * 0.15f;
            Volatile.Write(ref _amplitude, amp);
        }

        /// <summary>Copies the most recent samples into <paramref name="dest"/> (oldest first).</summary>
        public void GetWaveform(float[] dest)
        {
            int n = Math.Min(dest.Length, RingSize);
            int end = Volatile.Read(ref _ringPos);
            int start = (end - n) & (RingSize - 1);
            for (int i = 0; i < n; i++)
                dest[i] = _ring[(start + i) & (RingSize - 1)];
            if (n < dest.Length)
                Array.Clear(dest, n, dest.Length - n);
        }

        [ThreadStatic] private static float[]? _fftSamples;
        [ThreadStatic] private static double[]? _fftRe;
        [ThreadStatic] private static double[]? _fftIm;

        /// <summary>
        /// Fills <paramref name="bands"/> with the current spectrum, split into
        /// bands.Length log-spaced bands from 30 Hz to 16 kHz. Each value is
        /// 0..1, mapped from -60 dB (0) to 0 dB (1), ready to use as a height
        /// or brightness. Runs on the caller's thread (a 1024-point FFT).
        /// </summary>
        public void GetSpectrum(float[] bands)
        {
            const int N = 1024;
            var samples = _fftSamples ??= new float[N];
            var re = _fftRe ??= new double[N];
            var im = _fftIm ??= new double[N];

            GetWaveform(samples);
            for (int i = 0; i < N; i++)
            {
                double hann = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (N - 1));
                re[i] = samples[i] * hann;
                im[i] = 0;
            }
            Fft(re, im);

            double binHz = SampleRate / (double)N;
            double lo = Math.Log(30), hi = Math.Log(16000);
            for (int b = 0; b < bands.Length; b++)
            {
                double f0 = Math.Exp(lo + (hi - lo) * b / bands.Length);
                double f1 = Math.Exp(lo + (hi - lo) * (b + 1) / bands.Length);
                int k0 = Math.Clamp((int)(f0 / binHz), 1, N / 2 - 1);
                int k1 = Math.Clamp((int)Math.Ceiling(f1 / binHz), k0 + 1, N / 2);

                double peak = 0;
                for (int k = k0; k < k1; k++)
                {
                    double mag = Math.Sqrt(re[k] * re[k] + im[k] * im[k]) * 4.0 / N; // x2 one-sided, x2 Hann gain
                    if (mag > peak)
                        peak = mag;
                }

                double db = 20 * Math.Log10(peak + 1e-9);
                bands[b] = (float)Math.Clamp((db + 60) / 60, 0, 1);
            }
        }

        private static void Fft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                    j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                double wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, b = a + len / 2;
                        double tr = re[b] * cr - im[b] * ci;
                        double ti = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - tr;
                        im[b] = im[a] - ti;
                        re[a] += tr;
                        im[a] += ti;
                        double ncr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = ncr;
                    }
                }
            }
        }
    }
}
