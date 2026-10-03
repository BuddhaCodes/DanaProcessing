using System;
using System.Collections.Concurrent;
using System.Runtime;
using System.Threading;

namespace DanaProcessing
{
    /// <summary>
    /// A note (or drum hit) that just became audible. Delivered to
    /// Sketch.OnNote() on the drawing thread at the moment it actually
    /// reaches the speakers -- not when the code that played it ran, which
    /// is ScheduleAhead earlier -- so visuals line up with what you hear.
    /// </summary>
    public readonly record struct NoteEvent(
        double Time,
        double Note,
        double Amp,
        Synth Synth,
        string? Sample,
        string? Loop);
}

namespace DanaProcessing.Audio
{
    /// <summary>
    /// DanaProcessing's own audio engine: one process-wide instance that
    /// outlives individual sketches (so pressing Run doesn't cut the music,
    /// just like Sonic Pi). Owns the audio thread, a fixed pool of voices,
    /// the master clock and the analysis taps (Amplitude, Waveform,
    /// Spectrum).
    ///
    /// Threading model:
    ///  * Any thread (live loops, Draw, input handlers) calls Schedule(),
    ///    which only enqueues a struct into a lock-free queue.
    ///  * The audio thread (inside the IAudioOutput) calls Render(): it
    ///    dequeues events into the preallocated voice pool, mixes, and
    ///    advances the clock. It never allocates, never locks, never calls
    ///    user code.
    ///  * The clock (Now) is the number of frames the audio thread has
    ///    rendered -- the one timeline everything else is measured against.
    /// </summary>
    public sealed class AudioEngine : IDisposable
    {
        private const int VoiceCount = 128;
        private const int RingSize = 4096;

        private static readonly object SharedGate = new();
        private static AudioEngine? _shared;

        /// <summary>The engine every sketch shares. Created on first use; the
        /// sound device itself only opens once something actually plays.</summary>
        public static AudioEngine Shared
        {
            get
            {
                lock (SharedGate)
                    return _shared ??= new AudioEngine(null);
            }
        }

        /// <summary>The shared engine if anything has created it yet, without creating it.</summary>
        internal static AudioEngine? SharedIfCreated
        {
            get
            {
                lock (SharedGate)
                    return _shared;
            }
        }

        /// <summary>
        /// Raised (from any thread) with a human-readable message whenever
        /// the audio side has something to tell the user: a live loop threw,
        /// forgot to Sleep, fell behind, or the device couldn't be opened.
        /// Static so a host (the IDE) can subscribe before the engine exists.
        /// </summary>
        public static event Action<string>? DiagnosticReported;

        internal static void Report(string message)
        {
            DanaLogger.Warn(message);
            DiagnosticReported?.Invoke(message);
        }

        /// <summary>
        /// Hosts call this once a sketch is live (after its Setup(), and after a
        /// hot-reload swap): its [LiveLoop] methods start, or -- if loops with
        /// the same names are already playing -- take over on their next pass.
        /// Does nothing at all (not even create the engine) for a sketch with
        /// no loops when nothing is playing. Never throws.
        /// </summary>
        public static void ActivateSketch(Sketch sketch)
        {
            try
            {
                var loops = LiveLoopRunner.FindLoops(sketch.GetType());
                var engine = loops.Count > 0 ? Shared : SharedIfCreated;
                engine?.Loops.Activate(sketch, loops);
            }
            catch (Exception ex)
            {
                Report($"No se pudieron iniciar los live loops: {ex.Message}");
            }
        }

        /// <summary>Stops every live loop and silences every voice -- the IDE's "Stop sound" button.</summary>
        public static void StopEverything()
        {
            var engine = SharedIfCreated;
            if (engine == null)
                return;
            engine.Loops.StopAll();
            engine.Silence();
        }

        private readonly Func<IAudioOutput>? _outputFactory;
        private readonly object _startGate = new();
        private IAudioOutput? _output;
        private Voice[] _voices = Array.Empty<Voice>();
        private readonly ConcurrentQueue<VoiceEvent> _pending = new();
        private readonly ConcurrentQueue<NoteEvent> _audible = new();

        private long _frame;               // frames rendered so far (the clock)
        private int _silenceRequests;
        private int _silenceSeen;

        private readonly float[] _ring = new float[RingSize];
        private int _ringPos;
        private float _amplitude;

        public LiveLoopRunner Loops { get; }

        public int SampleRate { get; private set; } = 48000;

        /// <summary>Beats per minute used by Sleep(). Sonic Pi's default is 60 (1 beat = 1 second).</summary>
        public double Bpm
        {
            get => Volatile.Read(ref _bpm);
            set => Volatile.Write(ref _bpm, Math.Clamp(value, 1, 999));
        }
        private double _bpm = 60;

        /// <summary>How far ahead of the clock live loops run, in seconds -- the
        /// cushion that absorbs thread-timing jitter so the rhythm stays tight.</summary>
        public double ScheduleAhead { get; set; } = 0.1;

        /// <summary>Master output gain, 0..1 (a soft clipper follows it).</summary>
        public float MasterVolume { get; set; } = 0.7f;

        public bool IsRunning => _output != null;

        /// <summary>What the engine is playing through (for the status bar / diagnostics).</summary>
        public string OutputDescription => _output?.Description ?? "detenido";

        /// <summary>Engine clock in seconds: how much audio has been rendered.</summary>
        public double Now => Volatile.Read(ref _frame) / (double)SampleRate;

        /// <summary>The clock minus the device's buffering: what is coming out of the speakers right now.</summary>
        public double AudibleNow => (Volatile.Read(ref _frame) - (_output?.LatencyFrames ?? 0)) / (double)SampleRate;

        /// <summary>Smoothed loudness of the master output, 0..1.</summary>
        public float Amplitude => Volatile.Read(ref _amplitude);

        /// <param name="outputFactory">null = pick the best output for this OS. Tests pass their own.</param>
        internal AudioEngine(Func<IAudioOutput>? outputFactory)
        {
            _outputFactory = outputFactory;
            Loops = new LiveLoopRunner(this);
        }

        /// <summary>Opens the sound device (once). Cheap to call repeatedly.</summary>
        public void EnsureStarted()
        {
            if (_output != null)
                return;

            lock (_startGate)
            {
                if (_output != null)
                    return;

                _voices = new Voice[VoiceCount];
                for (int i = 0; i < VoiceCount; i++)
                    _voices[i] = new Voice();

                // Fewer, shorter GC pauses while music is playing. The audio
                // thread itself doesn't allocate, but a long blocking GC
                // still freezes it along with every other managed thread.
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

                var output = CreateOutput();
                try
                {
                    SampleRate = output.SampleRate;
                    output.Start(Render);
                }
                catch (Exception ex)
                {
                    output.Dispose();
                    Report($"No se pudo abrir la salida de audio ({ex.Message}). Los live loops siguen corriendo, pero sin sonido.");
                    output = new ClockOnlyOutput();
                    SampleRate = output.SampleRate;
                    output.Start(Render);
                }
                _output = output;
                AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
                DanaLogger.Info($"Audio: {output.Description}");
            }
        }

        private IAudioOutput CreateOutput()
        {
            if (_outputFactory != null)
                return _outputFactory();

            if (OperatingSystem.IsWindows())
                return new WinMmOutput();

            Report("La salida de audio todavía no está disponible en este sistema operativo (por ahora solo Windows). Los live loops y OnNote() funcionan igual, sin sonido.");
            return new ClockOnlyOutput();
        }

        /// <summary>Queues a sound. Thread-safe and allocation-free.</summary>
        internal void Schedule(in VoiceEvent e, in NoteEvent visual)
        {
            EnsureStarted();
            _pending.Enqueue(e);
            _audible.Enqueue(visual);
        }

        /// <summary>Engine frame for an engine time in seconds.</summary>
        internal long FrameAt(double seconds) => (long)Math.Round(seconds * SampleRate);

        /// <summary>Kills every sounding and pending voice at the next audio block.</summary>
        public void Silence()
        {
            Interlocked.Increment(ref _silenceRequests);
            while (_audible.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Hands every NoteEvent whose time has reached the speakers to
        /// <paramref name="handler"/>. Called on the drawing thread right
        /// before Draw(). Events that are already stale (e.g. Draw was paused
        /// while you typed) are dropped instead of arriving in a burst.
        /// </summary>
        internal void DispatchAudibleNotes(Action<NoteEvent> handler)
        {
            double now = AudibleNow;
            while (_audible.TryPeek(out var e) && e.Time <= now)
            {
                if (!_audible.TryDequeue(out e))
                    break;
                if (now - e.Time < 0.5)
                    handler(e);
            }
        }

        /// <summary>The audio thread's entry point. No allocations, no locks, no user code.</summary>
        internal void Render(float[] buffer, int frames)
        {
            long blockStart = _frame;

            int silence = Volatile.Read(ref _silenceRequests);
            if (silence != _silenceSeen)
            {
                _silenceSeen = silence;
                for (int i = 0; i < _voices.Length; i++)
                    _voices[i].Active = false;
                while (_pending.TryDequeue(out _)) { }
            }

            while (_pending.TryDequeue(out var e))
                Allocate().Start(e, SampleRate);

            for (int i = 0; i < _voices.Length; i++)
            {
                var v = _voices[i];
                if (v.Active)
                    v.Render(buffer, frames, blockStart);
            }

            // Master: gain, soft clip, analysis taps.
            float gain = MasterVolume;
            double sumSquares = 0;
            int ring = _ringPos;
            for (int i = 0; i < frames; i++)
            {
                float l = SoftClip(buffer[2 * i] * gain);
                float r = SoftClip(buffer[2 * i + 1] * gain);
                buffer[2 * i] = l;
                buffer[2 * i + 1] = r;

                float mono = 0.5f * (l + r);
                sumSquares += mono * mono;
                _ring[ring] = mono;
                ring = (ring + 1) & (RingSize - 1);
            }
            Volatile.Write(ref _ringPos, ring);

            float rms = (float)Math.Sqrt(sumSquares / Math.Max(1, frames));
            float amp = _amplitude;
            amp = rms > amp ? rms : amp * 0.85f + rms * 0.15f;
            Volatile.Write(ref _amplitude, amp);

            Volatile.Write(ref _frame, blockStart + frames);
        }

        private Voice Allocate()
        {
            Voice? oldest = null;
            for (int i = 0; i < _voices.Length; i++)
            {
                var v = _voices[i];
                if (!v.Active)
                    return v;
                if (oldest == null || v.StartFrame < oldest.StartFrame)
                    oldest = v;
            }
            return oldest!; // voice stealing: the oldest sound makes room
        }

        private static float SoftClip(float x)
        {
            // Cheap tanh-like curve: transparent below ~0.5, smooth limit at +-1.
            if (x > 3f) return 1f;
            if (x < -3f) return -1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }

        /// <summary>Copies the most recent mono output samples into <paramref name="dest"/> (oldest first).</summary>
        public void GetWaveform(float[] dest)
        {
            int n = Math.Min(dest.Length, RingSize);
            int end = Volatile.Read(ref _ringPos);
            int start = (end - n) & (RingSize - 1);
            for (int i = 0; i < n; i++)
                dest[i] = _ring[(start + i) & (RingSize - 1)];
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

        public void Dispose()
        {
            Loops.StopAll();
            _output?.Dispose();
        }
    }
}
