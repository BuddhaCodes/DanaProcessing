using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using DanaProcessing.Audio;
using Xunit;

namespace DanaProcessing.Tests.Audio;

/// <summary>A real-time output that keeps everything it renders and measures allocations on the audio thread.</summary>
internal sealed class RecordingOutput : IAudioOutput
{
    public readonly List<float> Samples = new(48000 * 30);
    public long AllocatedBytes;
    public int Blocks;
    private Thread? _thread;
    private volatile bool _run;
    public int SampleRate => 48000;
    public int LatencyFrames => 0;
    public string Description => "recording";

    public void Start(AudioRenderCallback render)
    {
        _run = true;
        _thread = new Thread(() =>
        {
            var buf = new float[480 * 2];
            var sw = Stopwatch.StartNew();
            long done = 0;
            while (_run)
            {
                long due = (long)(sw.Elapsed.TotalSeconds * 48000);
                while (done + 480 <= due)
                {
                    Array.Clear(buf);
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    render(buf, 480);
                    long after = GC.GetAllocatedBytesForCurrentThread();
                    if (Blocks > 10) AllocatedBytes += after - before; // skip warm-up (JIT, tiering)
                    Blocks++;
                    lock (Samples) Samples.AddRange(buf);
                    done += 480;
                }
                Thread.Sleep(1);
            }
        }) { IsBackground = true, Priority = ThreadPriority.Highest };
        _thread.Start();
    }

    public void Dispose()
    {
        _run = false;
        _thread?.Join();
    }

    public List<float> Snapshot() { lock (Samples) return new List<float>(Samples); }
}

public class AudioEngineTests : IDisposable
{
    public class KickSketch : Sketch
    {
        private List<double>? _times; // field initializers don't run for Bare<T>()
        public List<double> Times => _times ??= new();
        public override void Draw() { }
        [LiveLoop] async Task Drums() { Play(100, release: 0.05); await Sleep(1); }
        public override void OnNote(NoteEvent e) { lock (Times) Times.Add(e.Time); }
    }

    public class KickSketchV2 : Sketch
    {
        private List<(double t, string what)>? _events;
        public List<(double t, string what)> Events => _events ??= new();
        public override void Draw() { }
        public override void OnNote(NoteEvent e) { lock (Events) Events.Add((e.Time, e.Sample ?? e.Loop ?? "?")); }
        [LiveLoop] async Task Drums() { Sample("kick"); await Sleep(0.5); Sample("snare"); await Sleep(0.5); }
        [LiveLoop] async Task Bass() { UseSynth(Synth.Saw); Play("E2", amp: 0.5, release: 0.4, cutoff: 80); await Sleep(0.5); }
    }

    public class BrokenSketch : Sketch
    {
        public override void Draw() { }
        [LiveLoop] async Task NoSleep() { Play(60); await Task.CompletedTask; }
        [LiveLoop] async Task Throws() { await Sleep(0.25); throw new InvalidOperationException("boom"); }
        [LiveLoop] void BadSignature() { }
    }

    // Live loops only need the sketch's audio side; skipping the constructor
    // keeps these tests independent of the native graphics library.
    private static T Bare<T>() where T : Sketch => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private readonly RecordingOutput _rec = new();
    private readonly AudioEngine _engine;

    public AudioEngineTests()
    {
        _engine = new AudioEngine(() => _rec);
        SetShared(_engine);
        _engine.Bpm = 120;
    }

    public void Dispose()
    {
        AudioEngine.StopEverything();
        _engine.Dispose();
        SetShared(null);
    }

    private static void SetShared(AudioEngine? e) =>
        typeof(AudioEngine).GetField("_shared", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, e);

    private static async Task RunFrames(Sketch s, double seconds)
    {
        for (int i = 0; i < seconds * 10; i++)
        {
            await Task.Delay(100);
            s.RenderFrame(); // delivers OnNote() like the host does every frame
        }
    }

    private static List<long> Onsets(List<float> s, float threshold, int minGap)
    {
        var result = new List<long>();
        long last = -minGap;
        for (int i = 0; i < s.Count / 2; i++)
            if (Math.Abs(s[2 * i]) > threshold && i - last >= minGap) { result.Add(i); last = i; }
        return result;
    }

    [Fact]
    public async Task Live_loop_is_sample_accurate()
    {
        var kick = Bare<KickSketch>();
        AudioEngine.ActivateSketch(kick);
        await RunFrames(kick, 3);

        var onsets = Onsets(_rec.Snapshot(), 0.02f, 12000);
        var gaps = onsets.Zip(onsets.Skip(1), (a, b) => b - a).ToList();
        Assert.True(onsets.Count >= 4, $"onsets: {onsets.Count}");
        Assert.All(gaps, g => Assert.Equal(24000, g)); // 0.5 s at 120 BPM, to the sample
        Assert.True(kick.Times.Count >= 4);
    }

    [Fact]
    public async Task Hot_swap_keeps_the_beat()
    {
        var kick = Bare<KickSketch>();
        AudioEngine.ActivateSketch(kick);
        await RunFrames(kick, 2);
        double lastOld = kick.Times.Max();

        var v2 = Bare<KickSketchV2>();
        AudioEngine.ActivateSketch(v2);
        await RunFrames(v2, 3);

        Assert.True(v2.Events.Count > 6);
        Assert.All(v2.Events, e =>
        {
            double steps = (e.t - lastOld) / 0.25;
            Assert.True(Math.Abs(steps - Math.Round(steps)) < 1e-6, $"{e.what} @ {e.t} is off the grid");
        });
        Assert.Contains(v2.Events, e => e.what == "Bass");
        Assert.Equal(new[] { "Bass", "Drums" }, _engine.Loops.RunningLoops.OrderBy(x => x));
    }

    [Fact]
    public async Task Broken_loops_are_reported_not_thrown()
    {
        var reports = new List<string>();
        void OnReport(string m) { lock (reports) reports.Add(m); }
        AudioEngine.DiagnosticReported += OnReport;
        try
        {
            AudioEngine.ActivateSketch(Bare<BrokenSketch>());
            await Task.Delay(1200);
        }
        finally
        {
            AudioEngine.DiagnosticReported -= OnReport;
        }
        lock (reports)
        {
            Assert.Contains(reports, r => r.Contains("NoSleep") && r.Contains("Sleep"));
            Assert.Contains(reports, r => r.Contains("Throws") && r.Contains("boom"));
            Assert.Contains(reports, r => r.Contains("BadSignature"));
        }
        Assert.Empty(_engine.Loops.RunningLoops);
    }

    [Fact]
    public async Task Audio_thread_never_allocates_and_never_clips()
    {
        var v2 = Bare<KickSketchV2>();
        AudioEngine.ActivateSketch(v2);
        await RunFrames(v2, 3);

        Assert.True(_rec.Blocks > 100);
        Assert.Equal(0, _rec.AllocatedBytes);
        var all = _rec.Snapshot();
        Assert.All(all, f => Assert.False(float.IsNaN(f)));
        Assert.True(all.Max(Math.Abs) <= 1f);
    }

    [Fact]
    public void Notes_and_scales()
    {
        Assert.Equal(60, Notes.Parse("C4"));
        Assert.Equal(69, Notes.Parse("A4"));
        Assert.Equal(54, Notes.Parse("F#3"));
        Assert.Equal(39, Notes.Parse("Eb2"));
        Assert.Equal(new[] { 60, 63, 67 }, Notes.Chord("C4", "minor"));
        Assert.Equal(new[] { 40, 43, 45, 47, 50, 52 }, Notes.Scale("E2", "minor_pentatonic"));
        Assert.Equal(440, Notes.ToFrequency(69), 9);
        Assert.Throws<ArgumentException>(() => Notes.Chord("C4", "nope"));
    }

    [Fact]
    public void Wav_round_trip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dana_{Guid.NewGuid():N}.wav");
        try
        {
            var samples = Enumerable.Range(0, 48000 * 2).Select(i => (float)Math.Sin(i * 0.05) * 0.5f).ToList();
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int n = samples.Count;
                w.Write("RIFF"u8); w.Write(36 + n * 2); w.Write("WAVE"u8);
                w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(48000); w.Write(48000 * 4); w.Write((short)4); w.Write((short)16);
                w.Write("data"u8); w.Write(n * 2);
                foreach (var f in samples) w.Write((short)(f * 32767));
            }
            var loaded = SampleBuffer.LoadWav(path);
            Assert.Equal((48000, 48000, 2), (loaded.Frames, loaded.SampleRate, loaded.Channels));
            Assert.Equal(samples[2000], loaded.Left[1000], 3);
        }
        finally { File.Delete(path); }
    }
}
