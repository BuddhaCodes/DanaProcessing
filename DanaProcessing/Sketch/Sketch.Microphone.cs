using DanaProcessing.Audio;

namespace DanaProcessing
{
    /// <summary>What Amplitude, Waveform() and Spectrum() listen to -- see <see cref="Sketch.ListenTo"/>.</summary>
    public enum AudioSource
    {
        /// <summary>The sketch's own sound (Play, Sample, live loops). The default.</summary>
        Sketch,
        /// <summary>The computer's default microphone (or line in): the real world.</summary>
        Microphone,
        /// <summary>Both at once: the louder of the two.</summary>
        Both,
    }

    public abstract partial class Sketch
    {
        // =====================================================================
        // Microphone -- Amplitude/Waveform()/Spectrum() can listen to the room
        // instead of (or as well as) the sketch's own sound. The microphone is
        // opened by the first sketch that listens and closed when the last one
        // stops (OnStop / a new Run), so the OS's "mic in use" light means a
        // sketch is really listening.
        // =====================================================================

        private AudioSource _listenTo;
        private bool _holdsMicrophone;
        private float[]? _audioScratch;

        /// <summary>
        /// Picks what Amplitude, Waveform() and Spectrum() hear:
        /// <c>ListenTo(AudioSource.Microphone)</c> makes them react to your voice,
        /// a guitar or the room; <c>AudioSource.Sketch</c> (the default) goes back
        /// to the sketch's own sound; <c>AudioSource.Both</c> hears both. The
        /// first time a sketch listens, the OS may ask for microphone permission.
        /// </summary>
        public void ListenTo(AudioSource source)
        {
            _listenTo = source;
            if (source == AudioSource.Sketch)
                ReleaseMicrophone();
            else
                HoldMicrophone();
        }

        /// <summary>What Amplitude/Waveform()/Spectrum() are listening to right now.</summary>
        public AudioSource Listening => _listenTo;

        /// <summary>How loud the microphone is right now, 0..1 (smoothed) -- whatever ListenTo() says. Opens the microphone if needed.</summary>
        public float MicLevel
        {
            get
            {
                HoldMicrophone();
                return Microphone.Analyzer.Amplitude;
            }
        }

        /// <summary>
        /// Boosts (or tames) the microphone before it's analysed: most mics are
        /// quiet next to the sketch's own sound, so 3-8 is common. 1 = as is.
        /// Shared by every sketch.
        /// </summary>
        public float MicGain
        {
            get => Microphone.Gain;
            set => Microphone.Gain = Math.Max(0f, value);
        }

        /// <summary>Which input is open ("WinMM (default microphone)"...) or why it couldn't open; empty before anything listened.</summary>
        public string MicStatus => Microphone.Status;

        private void HoldMicrophone()
        {
            if (_holdsMicrophone || IgnoresAudio)
                return;
            _holdsMicrophone = true;
            Microphone.Acquire();
        }

        /// <summary>Lets go of the microphone (RaiseStopped calls it too, so a stopped sketch never keeps it open).</summary>
        internal void ReleaseMicrophone()
        {
            if (!_holdsMicrophone)
                return;
            _holdsMicrophone = false;
            Microphone.Release();
        }

        private float[] Scratch(int length)
        {
            // Exactly `length`: GetWaveform() fills the whole array with the
            // latest samples, so a longer one would be shifted in time.
            if (_audioScratch == null || _audioScratch.Length != length)
                _audioScratch = new float[length];
            return _audioScratch;
        }
    }
}
