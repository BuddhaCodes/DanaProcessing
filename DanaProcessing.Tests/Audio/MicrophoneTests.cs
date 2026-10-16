using DanaProcessing.Audio;
using DanaProcessing.Tests.Support;
using Xunit;

namespace DanaProcessing.Tests.Audio;

/// <summary>ListenTo(AudioSource.Microphone): Amplitude/Spectrum/Waveform hearing the room, and the mic's lifetime.</summary>
public class MicrophoneTests : IDisposable
{
    /// <summary>A "microphone" that hears a pure tone: hands over 0.25 s of it as soon as it starts.</summary>
    private sealed class ToneInput : IAudioInput
    {
        private readonly double _hz;
        private readonly float _amp;
        public bool Disposed;
        public int Starts;
        public ToneInput(double hz, float amp) { _hz = hz; _amp = amp; }
        public int SampleRate => 48000;
        public string Description => "test tone";

        public void Start(AudioCaptureCallback onSamples)
        {
            Starts++;
            var block = new float[480];
            long n = 0;
            for (int b = 0; b < 25; b++)
            {
                for (int i = 0; i < block.Length; i++, n++)
                    block[i] = _amp * (float)Math.Sin(2 * Math.PI * _hz * n / SampleRate);
                onSamples(block, block.Length);
            }
        }

        public void Dispose() => Disposed = true;
    }

    private ToneInput _tone = new(1000, 0.5f);
    private readonly Func<IAudioInput?> _realFactory = Microphone.Factory;
    private readonly AudioEngine? _previousEngine;

    public MicrophoneTests()
    {
        Microphone.Factory = () => _tone;
        Microphone.Gain = 1f;
        _previousEngine = AudioEngine.SwapShared(null); // no sketch sound unless a test makes some
    }

    public void Dispose()
    {
        while (Microphone.Listeners > 0)
            Microphone.Release();
        Microphone.Factory = _realFactory;
        Microphone.Gain = 1f;
        AudioEngine.SwapShared(_previousEngine);
    }

    public class Listener : Sketch
    {
        public override void Setup() => ListenTo(AudioSource.Microphone);
        public override void Draw() { }
    }

    [Fact]
    public void Amplitude_hears_the_microphone_after_ListenTo()
    {
        var s = new Listener();
        Offscreen.Run(s, 1);
        Assert.Equal(AudioSource.Microphone, s.Listening);
        Assert.InRange(s.Amplitude, 0.33f, 0.37f);   // RMS of a 0.5 sine = 0.354
        Assert.Equal(s.Amplitude, s.MicLevel);

        s.ListenTo(AudioSource.Sketch);
        Assert.Equal(0f, s.Amplitude);               // no sketch sound at all
    }

    [Fact]
    public void Spectrum_finds_the_tone_in_the_right_band()
    {
        var s = new Listener();
        Offscreen.Run(s, 1);
        var bands = new float[32];
        s.Spectrum(bands);

        // 30 Hz..16 kHz log-spaced: 1 kHz falls in band floor(32 * ln(1000/30) / ln(16000/30)).
        int expected = (int)(32 * Math.Log(1000 / 30.0) / Math.Log(16000 / 30.0));
        int loudest = Array.IndexOf(bands, bands.Max());
        Assert.InRange(loudest, expected - 1, expected + 1);
        Assert.True(bands[2] < bands[loudest] - 0.3f, "low bands stay quiet");
    }

    [Fact]
    public void Waveform_returns_what_the_microphone_heard()
    {
        var s = new Listener();
        Offscreen.Run(s, 1);
        var wave = new float[480];
        s.Waveform(wave);
        Assert.InRange(wave.Max(), 0.45f, 0.5f);
        Assert.InRange(wave.Min(), -0.5f, -0.45f);
    }

    [Fact]
    public void Gain_boosts_a_quiet_microphone()
    {
        _tone = new ToneInput(440, 0.05f);
        Microphone.Gain = 4f;
        var s = new Listener();
        Offscreen.Run(s, 1);
        Assert.InRange(s.Amplitude, 0.13f, 0.15f);   // 0.05 * 4 * 0.707
    }

    [Fact]
    public void The_microphone_closes_when_the_last_listener_stops()
    {
        var a = new Listener();
        var b = new Listener();
        Offscreen.Run(a, 1);
        Offscreen.Run(b, 1);
        Assert.Equal(1, _tone.Starts);               // opened once, shared
        Assert.True(Microphone.IsOpen);

        a.RaiseStopped();
        Assert.False(_tone.Disposed);                // b still listens
        b.RaiseStopped();
        Assert.True(_tone.Disposed);
        Assert.False(Microphone.IsOpen);
    }

    public class Quiet : Sketch
    {
        public override void Draw() { }
    }

    [Fact]
    public void A_sketch_that_never_listens_never_opens_the_microphone()
    {
        var s = new Quiet();
        Offscreen.Run(s, 3);
        _ = s.Amplitude;
        s.Spectrum(new float[8]);
        Assert.Equal(0, _tone.Starts);
        Assert.False(Microphone.IsOpen);
    }

    [Fact]
    public void Both_hears_the_louder_of_sketch_and_microphone()
    {
        using var audio = new OfflineAudio();         // a sketch sound engine that's running but silent
        audio.RenderUntil(0.1);
        var s = new Listener();
        Offscreen.Run(s, 1);
        s.ListenTo(AudioSource.Both);
        Assert.InRange(s.Amplitude, 0.33f, 0.37f);
        var bands = new float[16];
        s.Spectrum(bands);
        Assert.True(bands.Max() > 0.5f);
    }

    [Fact]
    public void A_missing_microphone_is_reported_not_thrown()
    {
        Microphone.Factory = () => throw new InvalidOperationException("no device");
        var s = new Listener();
        Offscreen.Run(s, 1);
        Assert.Equal(0f, s.Amplitude);
        Assert.Contains("no device", s.MicStatus);
    }

    [Fact]
    public void Nothing_from_an_earlier_session_leaks_into_a_new_one()
    {
        // A session that heard a loud tone...
        var loud = new Listener();
        Offscreen.Run(loud, 1);
        Assert.True(loud.MicLevel > 0.3f);
        loud.RaiseStopped();                          // the sketch stops: the microphone closes
        Assert.Equal(0, Microphone.Listeners);
        Assert.Equal(0f, Microphone.Analyzer.Amplitude); // closed means silent, not frozen on the last sound

        // ...then a sketch whose microphone can't open reads silence, not the old tone.
        Microphone.Factory = () => throw new InvalidOperationException("no device");
        var next = new Listener();
        Offscreen.Run(next, 1);
        Assert.Equal(0f, next.Amplitude);
    }
}
