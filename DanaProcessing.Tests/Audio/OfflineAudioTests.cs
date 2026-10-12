using System.Diagnostics;
using System.Runtime.CompilerServices;
using DanaProcessing.Audio;
using Xunit;

namespace DanaProcessing.Tests.Audio;

/// <summary>Sound recorded for a reel export: live loops run in rendered time, not wall-clock time.</summary>
public class OfflineAudioTests
{
    public class Beat : Sketch
    {
        public override void Draw() { }
        [LiveLoop] async Task Drums() { Play(100, release: 0.05); await Sleep(1); }
    }

    public class Silent : Sketch { public override void Draw() { } }

    private static T Bare<T>() where T : Sketch => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    [Fact]
    public void Live_loops_follow_rendered_time_exactly_and_faster_than_real_time()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dana_{Guid.NewGuid():N}.wav");
        try
        {
            var sw = Stopwatch.StartNew();
            using (var audio = new OfflineAudio())
            {
                audio.Engine.Bpm = 120;
                Assert.Same(audio.Engine, AudioEngine.Shared); // the sketch talks to the offline engine
                AudioEngine.ActivateSketch(Bare<Beat>());
                audio.RenderUntil(6.0);
                Assert.True(audio.HeardAnything);
                audio.WriteWav(path);
            }
            Assert.True(sw.Elapsed.TotalSeconds < 6, $"6 s of sound took {sw.Elapsed.TotalSeconds:0.0} s to render");

            var wav = SampleBuffer.LoadWav(path);
            Assert.Equal(6 * 48000, wav.Frames);
            var onsets = new List<int>();
            int last = -20000;
            for (int i = 0; i < wav.Frames; i++)
                if (Math.Abs(wav.Left[i]) > 0.02f && i - last > 12000) { onsets.Add(i); last = i; }
            Assert.True(onsets.Count >= 10, $"{onsets.Count} kicks");
            var gaps = onsets.Zip(onsets.Skip(1), (a, b) => b - a).ToList();
            Assert.All(gaps, g => Assert.InRange(g, 23990, 24010)); // 0.5 s at 120 BPM
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Restores_the_previous_engine_and_stays_silent_without_sound()
    {
        var before = AudioEngine.SwapShared(null);
        AudioEngine.SwapShared(before);
        using (var audio = new OfflineAudio())
        {
            AudioEngine.ActivateSketch(Bare<Silent>());
            audio.RenderUntil(1.0);
            Assert.False(audio.HeardAnything);
        }
        var after = AudioEngine.SwapShared(before);
        Assert.Same(before, after);
    }

    [Fact]
    public void Markers_decide_whether_a_reel_records_sound()
    {
        Assert.True(DanaProcessing.Reels.ReelScript.Parse("[LiveLoop] async Task A() { await Sleep(1); }").RecordsAudio);
        Assert.True(DanaProcessing.Reels.ReelScript.Parse("void MousePressed() => Play(60);").RecordsAudio);
        Assert.False(DanaProcessing.Reels.ReelScript.Parse("void Draw() { Circle(1, 2, 3); }").RecordsAudio);
        Assert.False(DanaProcessing.Reels.ReelScript.Parse("// @reel audio: off\n[LiveLoop] async Task A() { }").RecordsAudio);
        Assert.True(DanaProcessing.Reels.ReelScript.Parse("// @reel audio on\nvoid Draw() { }").RecordsAudio);
    }
}
