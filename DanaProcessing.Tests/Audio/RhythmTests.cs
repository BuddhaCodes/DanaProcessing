using System.Runtime.CompilerServices;
using DanaProcessing.Audio;
using DanaProcessing.Tests.Support;
using Xunit;

namespace DanaProcessing.Tests.Audio;

/// <summary>Beat(), Bar(), Phase(), Pulse(), OnBeat() and the easings: musical time for visuals.</summary>
public class RhythmTests
{
    public class Watcher : Sketch
    {
        public List<double> Beats = new();
        public int Downbeats, Bars;
        public override void Draw()
        {
            Beats.Add(Beat());
            if (OnBeat()) Downbeats++;
            if (OnBeat(4)) Bars++;
        }
    }

    private static void Frame(Sketch s, SkiaSharp.SKSurface surface)
    {
        surface.Canvas.Save();
        s.RenderFrame();
        surface.Canvas.Restore();
    }

    [Fact]
    public void Beat_follows_the_engine_clock_at_the_current_tempo()
    {
        using var audio = new OfflineAudio();
        audio.Engine.Bpm = 120;
        var s = new Watcher();
        var surface = Offscreen.Run(s, 0);

        audio.RenderUntil(1.0);
        Frame(s, surface);
        Assert.Equal(2.0, s.Beat(), 3);        // 1 s at 120 BPM
        Assert.Equal(0.5, s.Bar(), 3);         // half a 4/4 bar
        Assert.Equal(0f, s.Phase(), 3);        // exactly on a beat
        Assert.Equal(0.5f, s.Phase(4), 3);     // halfway through the bar
    }

    [Fact]
    public void A_tempo_change_never_makes_the_beat_jump()
    {
        using var audio = new OfflineAudio();
        audio.Engine.Bpm = 120;
        audio.RenderUntil(1.0);                // 2 beats
        audio.Engine.Bpm = 60;                 // from now on, 1 beat per second
        audio.RenderUntil(2.0);
        Assert.Equal(3.0, audio.Engine.BeatsAt(audio.Engine.Now), 3);
    }

    [Fact]
    public void A_tempo_change_from_a_loop_takes_effect_at_the_loops_time()
    {
        using var audio = new OfflineAudio();
        audio.Engine.SetBpmAt(120, 0);
        audio.Engine.SetBpmAt(60, 2.0);        // scheduled ahead, like a loop does
        Assert.Equal(2.0, audio.Engine.BeatsAt(1.0), 3);   // still 120 BPM before 2 s
        Assert.Equal(4.0, audio.Engine.BeatsAt(2.0), 3);
        Assert.Equal(5.0, audio.Engine.BeatsAt(3.0), 3);   // 60 BPM after
    }

    [Fact]
    public void OnBeat_fires_once_per_beat_and_once_per_bar()
    {
        using var audio = new OfflineAudio();
        audio.Engine.Bpm = 240;                // 4 beats per second
        var s = new Watcher();
        var surface = Offscreen.Run(s, 0);
        Frame(s, surface);                     // beat 0: the first frame has no "previous" yet

        for (int i = 1; i <= 60; i++)          // 2 s at 30 fps = 8 beats = 2 bars
        {
            audio.RenderUntil(i / 30.0);
            Frame(s, surface);
        }
        Assert.Equal(8, s.Downbeats);
        Assert.Equal(2, s.Bars);
    }

    public class Loop : Sketch
    {
        private List<double>? _noteBeats; // field initializers don't run for Bare<T>()
        public List<double> NoteBeats => _noteBeats ??= new();
        public override void Draw() { }
        [LiveLoop] async Task Kick() { Sample("kick"); await Sleep(1); }
        public override void OnNote(NoteEvent e) => NoteBeats.Add(AudioEngine.Shared.BeatsAt(e.Time));
    }

    private static T Bare<T>() where T : Sketch => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    [Fact]
    public void Loops_starting_from_silence_land_on_whole_bars()
    {
        using var audio = new OfflineAudio();
        audio.Engine.Bpm = 100;
        audio.RenderUntil(0.37);               // the clock is somewhere in the middle of a beat
        var s = Bare<Loop>();
        AudioEngine.ActivateSketch(s);
        for (double t = 0.4; t <= 3.0; t += 0.05)
        {
            audio.RenderUntil(t);
            s.DispatchAudioEvents();
        }
        Assert.True(s.NoteBeats.Count >= 3, $"{s.NoteBeats.Count} notes");
        Assert.Equal(0.0, s.NoteBeats[0] % 4, 6);             // first kick: on a bar line
        Assert.All(s.NoteBeats, b => Assert.Equal(Math.Round(b), b, 6)); // every kick on a beat
    }

    public class Silent : Sketch { public override void Draw() { } }

    [Fact]
    public void Without_sound_it_follows_millis_at_the_current_bpm()
    {
        var previous = AudioEngine.SwapShared(null);
        try
        {
            var s = new Silent();
            s.VirtualMillis = 0;
            var surface = Offscreen.Run(s, 1);
            s.VirtualMillis = 1500;            // 1.5 s at the default 60 BPM
            Frame(s, surface);
            Assert.Equal(1.5, s.Beat(), 3);
            Assert.Equal(0.5f, s.Phase(), 3);
            Assert.Equal(1f, s.PingPong(1), 3);
            Assert.InRange(s.Pulse(), 0.04f, 0.06f); // e^(-6 * 0.5)
        }
        finally
        {
            AudioEngine.SwapShared(previous);
        }
    }

    [Fact]
    public void Every_easing_starts_at_0_and_ends_at_1()
    {
        foreach (Ease e in Enum.GetValues<Ease>())
        {
            Assert.Equal(0f, Easing.Apply(e, 0f), 3);
            Assert.Equal(1f, Easing.Apply(e, 1f), 3);
        }
        Assert.Equal(0.5f, Easing.Apply(Ease.InOutCubic, 0.5f), 3);
        Assert.True(Easing.Apply(Ease.OutBack, 0.8f) > 1f);   // Back overshoots on purpose
        Assert.True(Easing.Apply(Ease.InQuad, 0.5f) < 0.5f);
        Assert.True(Easing.Apply(Ease.OutQuad, 0.5f) > 0.5f);
    }

    [Fact]
    public void BeatTween_moves_between_two_values_once_per_period()
    {
        var previous = AudioEngine.SwapShared(null);
        try
        {
            var s = new Silent();
            s.VirtualMillis = 0;
            var surface = Offscreen.Run(s, 1);
            s.VirtualMillis = 1000;            // phase of a 2-beat period = 0.5
            Frame(s, surface);
            Assert.Equal(150f, s.BeatTween(100, 200, Ease.Linear, beats: 2), 2);
            Assert.Equal(0.5f, s.BeatEase(Ease.InOutSine, beats: 2), 3);
        }
        finally
        {
            AudioEngine.SwapShared(previous);
        }
    }
}
