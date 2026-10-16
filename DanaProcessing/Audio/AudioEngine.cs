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

        /// <summary>
        /// Makes <paramref name="engine"/> the shared one (null = none yet) and
        /// returns the previous one. Used while a reel renders its sound
        /// offline, so the sketch's Play()/Sample()/live loops talk to it.
        /// </summary>
        internal static AudioEngine? SwapShared(AudioEngine? engine)
        {
            lock (SharedGate)
            {
                var previous = _shared;
                _shared = engine;
                return previous;
            }
        }

        /// <summary>Set for an engine whose time is driven by hand (OfflineAudio) instead of a sound card.</summary>
        internal OfflineClock? Offline { get; init; }

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

        // Amplitude / Waveform / Spectrum of the master output (the same
        // analysis the microphone input uses -- see AudioAnalyzer).
        private readonly AudioAnalyzer _analyzer = new();

        public LiveLoopRunner Loops { get; }

        public int SampleRate { get; private set; } = 48000;

        /// <summary>Beats per minute used by Sleep(). Sonic Pi's default is 60 (1 beat = 1 second).</summary>
        public double Bpm
        {
            get => Volatile.Read(ref _bpm);
            set => SetBpmAt(value, Now);
        }
        private double _bpm = 60;

        // The beat clock behind Sketch.Beat(): a piecewise-linear map from
        // engine seconds to beats. Each tempo change starts a new segment at the
        // moment it takes effect (for a live loop, its logical time -- slightly
        // in the future), so the clock stays continuous; the previous segment
        // still answers for times before that.
        private readonly object _beatGate = new();
        private (double Time, double Beats, double Bpm) _segment = (0, 0, 60);
        private (double Time, double Beats, double Bpm) _previousSegment = (0, 0, 60);

        /// <summary>
        /// Changes the tempo from engine time <paramref name="seconds"/> on. A
        /// live loop passes its own logical time, so the change lands exactly
        /// where its next notes do.
        /// </summary>
        internal void SetBpmAt(double bpm, double seconds)
        {
            bpm = Math.Clamp(bpm, 1, 999);
            lock (_beatGate)
            {
                double beats = BeatsAtLocked(seconds);
                _previousSegment = _segment;
                _segment = (seconds, beats, bpm);
                Volatile.Write(ref _bpm, bpm);
            }
        }

        /// <summary>Musical position, in beats, at engine time <paramref name="seconds"/>.</summary>
        public double BeatsAt(double seconds)
        {
            lock (_beatGate)
                return BeatsAtLocked(seconds);
        }

        private double BeatsAtLocked(double seconds)
        {
            var seg = seconds >= _segment.Time ? _segment : _previousSegment;
            return seg.Beats + (seconds - seg.Time) * seg.Bpm / 60.0;
        }

        /// <summary>
        /// Makes engine time <paramref name="seconds"/> fall on the start of a
        /// bar (the next multiple of <paramref name="beatsPerBar"/> beats, so the
        /// clock never runs backwards). Live loops starting from silence call
        /// this, which is what keeps Beat()/OnBeat() in step with them.
        /// </summary>
        internal void AlignBarAt(double seconds, int beatsPerBar = 4)
        {
            lock (_beatGate)
            {
                double beats = BeatsAtLocked(seconds);
                _previousSegment = _segment;
                _segment = (seconds, Math.Ceiling(beats / beatsPerBar - 1e-9) * beatsPerBar, _segment.Bpm);
            }
        }

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
        public float Amplitude => _analyzer.Amplitude;

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
            if (OperatingSystem.IsMacOS())
                return new CoreAudioOutput();
            if (OperatingSystem.IsLinux())
                return new AlsaOutput(); // if libasound is missing, EnsureStarted() falls back to the clock-only output

            Report("La salida de audio no está disponible en este sistema operativo. Los live loops y OnNote() funcionan igual, sin sonido.");
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
            for (int i = 0; i < frames; i++)
            {
                float l = SoftClip(buffer[2 * i] * gain);
                float r = SoftClip(buffer[2 * i + 1] * gain);
                buffer[2 * i] = l;
                buffer[2 * i + 1] = r;

                float mono = 0.5f * (l + r);
                sumSquares += mono * mono;
                _analyzer.WriteSample(mono);
            }
            _analyzer.UpdateAmplitude((float)Math.Sqrt(sumSquares / Math.Max(1, frames)));

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
        public void GetWaveform(float[] dest) => _analyzer.GetWaveform(dest);

        /// <summary>
        /// Fills <paramref name="bands"/> with the current spectrum, split into
        /// bands.Length log-spaced bands from 30 Hz to 16 kHz. Each value is
        /// 0..1, mapped from -60 dB (0) to 0 dB (1), ready to use as a height
        /// or brightness. Runs on the caller's thread (a 1024-point FFT).
        /// </summary>
        public void GetSpectrum(float[] bands)
        {
            _analyzer.SampleRate = SampleRate;
            _analyzer.GetSpectrum(bands);
        }

        public void Dispose()
        {
            Loops.StopAll();
            _output?.Dispose();
        }
    }
}
