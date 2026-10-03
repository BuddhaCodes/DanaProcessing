using System.Collections.Concurrent;
using DanaProcessing.Audio;

namespace DanaProcessing
{
    /// <summary>
    /// Sound: DanaProcessing's own live-coding audio engine, modeled on how
    /// Sonic Pi works (live loops, Sleep in beats, synths, samples) but
    /// written from scratch in C# and running inside the sketch's own
    /// process -- so what you hear and what you draw share one clock.
    ///
    /// <code>
    /// [LiveLoop]
    /// async Task Bass()
    /// {
    ///     UseSynth(Synth.Saw);
    ///     Play("E2", release: 0.3, cutoff: 80);
    ///     await Sleep(0.5);
    /// }
    ///
    /// public override void Draw() => Ellipse(Width / 2, Height / 2, Amplitude * 800, Amplitude * 800);
    /// </code>
    /// </summary>
    public abstract partial class Sketch
    {
        private static readonly ConcurrentDictionary<string, SampleBuffer> SampleCache = new(StringComparer.OrdinalIgnoreCase);

        private Synth _defaultSynth = Synth.Sine;
        private Action<NoteEvent>? _onNoteHandler;

        /// <summary>Tempo for Sleep(), in beats per minute. Shared by every loop. Default 60 (1 beat = 1 second), like Sonic Pi.</summary>
        public double Bpm
        {
            get => AudioEngine.Shared.Bpm;
            set => AudioEngine.Shared.Bpm = value;
        }

        /// <summary>
        /// Waits <paramref name="beats"/> beats. Inside a [LiveLoop] this is
        /// musical time -- exact, never drifting, in sync with every other
        /// loop -- so always `await` it. Outside a loop it's a plain delay.
        /// </summary>
        public Task Sleep(double beats) => AudioEngine.Shared.Loops.Sleep(beats);

        /// <summary>Synth used by the following Play() calls in this loop (or in the sketch, outside loops).</summary>
        public void UseSynth(Synth synth)
        {
            var ctx = LiveLoopRunner.Current.Value;
            if (ctx != null)
                ctx.Synth = synth;
            else
                _defaultSynth = synth;
        }

        /// <summary>
        /// Plays a note. <paramref name="note"/> is a MIDI number (60 = C4,
        /// fractions allowed); see the string overload for names like "C4".
        /// The envelope is Sonic Pi's: attack -> decay -> sustain -> release,
        /// each in seconds (they don't change with Bpm).
        /// <paramref name="cutoff"/> is a low-pass filter as a MIDI note
        /// (Sonic Pi style: 80 ≈ 830 Hz, 110 ≈ 4.7 kHz); 0 = no filter.
        /// <paramref name="res"/> is filter resonance, 0..1.
        /// </summary>
        public void Play(double note, double amp = 1, double pan = 0,
                         double attack = 0, double decay = 0, double sustain = 0, double release = 1,
                         double sustainLevel = 1, double cutoff = 0, double res = 0, Synth? synth = null)
            => PlayCore(note, amp, pan, attack, decay, sustain, release, sustainLevel, cutoff, res, synth);

        /// <summary>Plays a note by name: "C4", "F#3", "Eb2" (octave defaults to 4).</summary>
        public void Play(string note, double amp = 1, double pan = 0,
                         double attack = 0, double decay = 0, double sustain = 0, double release = 1,
                         double sustainLevel = 1, double cutoff = 0, double res = 0, Synth? synth = null)
            => PlayCore(Notes.Parse(note), amp, pan, attack, decay, sustain, release, sustainLevel, cutoff, res, synth);

        /// <summary>Plays several notes at once -- e.g. Play(Notes.Chord("C4", "minor")).</summary>
        public void Play(int[] notes, double amp = 1, double pan = 0,
                         double attack = 0, double decay = 0, double sustain = 0, double release = 1,
                         double sustainLevel = 1, double cutoff = 0, double res = 0, Synth? synth = null)
        {
            foreach (var n in notes)
                PlayCore(n, amp, pan, attack, decay, sustain, release, sustainLevel, cutoff, res, synth);
        }

        private void PlayCore(double note, double amp, double pan, double attack, double decay, double sustain,
                              double release, double sustainLevel, double cutoff, double res, Synth? synth)
        {
            var engine = AudioEngine.Shared;
            engine.EnsureStarted();

            var ctx = LiveLoopRunner.Current.Value;
            var chosen = synth ?? ctx?.Synth ?? _defaultSynth;
            double when = ctx != null ? ctx.Time + engine.ScheduleAhead : engine.Now;

            var e = new VoiceEvent
            {
                StartFrame = engine.FrameAt(when),
                Synth = chosen,
                Frequency = Notes.ToFrequency(note),
                Amp = Math.Max(0, amp),
                Pan = pan,
                Attack = Math.Max(0, attack),
                Decay = Math.Max(0, decay),
                Sustain = Math.Max(0, sustain),
                Release = Math.Max(0, release),
                SustainLevel = Math.Clamp(sustainLevel, 0, 1),
                Cutoff = cutoff > 0 ? Notes.ToFrequency(cutoff) : 0,
                Resonance = res,
                Rate = 1,
            };
            engine.Schedule(e, new NoteEvent(when, note, amp, chosen, null, ctx?.Name));
        }

        /// <summary>
        /// Plays a sample. Built-in drums need no files: "kick" (or "bd"),
        /// "snare" ("sn"), "hat" ("hh"). Anything else is a path to a .wav
        /// file (loaded once and cached -- call LoadSample() in Setup() to
        /// avoid the first-play load). <paramref name="rate"/> 2 = an octave
        /// up and twice as fast, 0.5 = an octave down.
        /// </summary>
        public void Sample(string name, double amp = 1, double rate = 1, double pan = 0)
        {
            var drum = name.ToLowerInvariant() switch
            {
                "kick" or "bd" => Synth.Kick,
                "snare" or "sn" => Synth.Snare,
                "hat" or "hh" or "hihat" => (Synth?)Synth.Hat,
                _ => null,
            };

            if (drum is { } d)
                ScheduleSample(null, d, name, amp, rate, pan);
            else
                ScheduleSample(LoadSample(name), Synth.Sine, name, amp, rate, pan);
        }

        /// <summary>Plays a sample you loaded (or built) yourself.</summary>
        public void Sample(SampleBuffer buffer, double amp = 1, double rate = 1, double pan = 0)
            => ScheduleSample(buffer, Synth.Sine, buffer.Name, amp, rate, pan);

        /// <summary>Loads a .wav file into memory (cached by path), ready for Sample(...).</summary>
        public SampleBuffer LoadSample(string path)
            => SampleCache.GetOrAdd(Path.GetFullPath(path), SampleBuffer.LoadWav);

        private void ScheduleSample(SampleBuffer? buffer, Synth drum, string label, double amp, double rate, double pan)
        {
            var engine = AudioEngine.Shared;
            engine.EnsureStarted();

            var ctx = LiveLoopRunner.Current.Value;
            double when = ctx != null ? ctx.Time + engine.ScheduleAhead : engine.Now;

            var e = new VoiceEvent
            {
                StartFrame = engine.FrameAt(when),
                Synth = drum,
                Sample = buffer,
                Amp = Math.Max(0, amp),
                Pan = pan,
                Rate = rate,
            };
            engine.Schedule(e, new NoteEvent(when, 0, amp, drum, label.ToLowerInvariant(), ctx?.Name));
        }

        /// <summary>How loud the sketch's sound is right now, 0..1 (smoothed). 0 while nothing has played.</summary>
        public float Amplitude => AudioEngine.SharedIfCreated?.Amplitude ?? 0f;

        /// <summary>Fills <paramref name="dest"/> with the latest output samples (-1..1), oldest first.</summary>
        public void Waveform(float[] dest)
        {
            var engine = AudioEngine.SharedIfCreated;
            if (engine == null)
                Array.Clear(dest);
            else
                engine.GetWaveform(dest);
        }

        /// <summary>
        /// Fills <paramref name="bands"/> with the current spectrum: bands.Length
        /// log-spaced bands from 30 Hz (index 0) to 16 kHz, each 0..1.
        /// </summary>
        public void Spectrum(float[] bands)
        {
            var engine = AudioEngine.SharedIfCreated;
            if (engine == null)
                Array.Clear(bands);
            else
                engine.GetSpectrum(bands);
        }

        /// <summary>
        /// Called (on the drawing thread, right before Draw) for every note or
        /// sample the moment it reaches the speakers -- override it to make
        /// visuals hit exactly on the beat. e.Sample is "kick"/"snare"/... for
        /// samples and null for notes; e.Loop is the [LiveLoop] that played it.
        /// </summary>
        public virtual void OnNote(NoteEvent e) { }

        /// <summary>Stops every live loop and silences all sound.</summary>
        public void StopAudio() => AudioEngine.StopEverything();

        internal void DispatchAudioEvents()
            => AudioEngine.SharedIfCreated?.DispatchAudibleNotes(_onNoteHandler ??= OnNote);
    }
}
