using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DanaProcessing.Audio
{
    /// <summary>
    /// Virtual time for live loops. Normally a loop's Sleep() waits with
    /// Task.Delay -- real time. When the audio is rendered faster (or slower)
    /// than real time, e.g. into a reel's video, loops instead wait here and
    /// are released by whoever drives the clock (<see cref="OfflineAudio"/>),
    /// which then waits until every released loop has reached its next
    /// Sleep() before rendering on. Same notes, same timing, any speed.
    /// </summary>
    internal sealed class OfflineClock
    {
        private readonly object _gate = new();
        private readonly List<(double Time, TaskCompletionSource Done, CancellationToken Token)> _waiters = new();
        private int _busy; // loops currently running user code (not parked in a wait)

        public void LoopStarted() => Interlocked.Increment(ref _busy);
        public void LoopEnded() => Interlocked.Decrement(ref _busy);

        public Task WaitUntil(double time, CancellationToken token)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
                _waiters.Add((time, tcs, token));
            Interlocked.Decrement(ref _busy); // parked
            return tcs.Task;
        }

        /// <summary>Wakes every loop due by <paramref name="now"/>, then waits (up to a limit) until they're all parked again.</summary>
        public void Advance(double now, TimeSpan patience)
        {
            List<TaskCompletionSource>? due = null;
            lock (_gate)
            {
                for (int i = _waiters.Count - 1; i >= 0; i--)
                {
                    var w = _waiters[i];
                    if (w.Token.IsCancellationRequested)
                    {
                        _waiters.RemoveAt(i);
                        Interlocked.Increment(ref _busy); // it resumes (to observe the cancellation) and ends
                        w.Done.TrySetCanceled(w.Token);
                    }
                    else if (w.Time <= now)
                    {
                        _waiters.RemoveAt(i);
                        (due ??= new()).Add(w.Done);
                    }
                }
            }
            if (due != null)
            {
                foreach (var d in due)
                {
                    Interlocked.Increment(ref _busy);
                    d.TrySetResult();
                }
            }

            var deadline = DateTime.UtcNow + patience;
            var spin = new SpinWait();
            while (Volatile.Read(ref _busy) > 0 && DateTime.UtcNow < deadline)
                spin.SpinOnce(sleep1Threshold: 50);
        }
    }

    /// <summary>An output with no device: the engine renders only when <see cref="OfflineAudio"/> asks.</summary>
    internal sealed class OfflineOutput : IAudioOutput
    {
        public AudioRenderCallback? Render { get; private set; }
        public int SampleRate => 48000;
        public int LatencyFrames => 0;
        public string Description => "offline (rendering to a file)";
        public void Start(AudioRenderCallback render) => Render = render;
        public void Dispose() { }
    }

    /// <summary>
    /// Renders a sketch's sound in step with something else that runs in its
    /// own time -- a reel being exported frame by frame. While it exists it
    /// is the engine every sketch talks to (AudioEngine.Shared); disposing it
    /// gives the previous engine back.
    /// </summary>
    internal sealed class OfflineAudio : IDisposable
    {
        private const int Block = 256;

        private readonly OfflineOutput _output = new();
        private readonly AudioEngine? _previous;
        private readonly float[] _block = new float[Block * 2];
        private readonly MemoryStream _pcm = new();
        private long _rendered;
        private bool _disposed;

        public AudioEngine Engine { get; }
        public int SampleRate => _output.SampleRate;
        public double Seconds => _rendered / (double)SampleRate;
        /// <summary>True once anything actually made a sound.</summary>
        public bool HeardAnything { get; private set; }

        public OfflineAudio()
        {
            Engine = new AudioEngine(() => _output) { Offline = new OfflineClock() };
            _previous = AudioEngine.SwapShared(Engine);
            Engine.EnsureStarted();
        }

        /// <summary>Renders (and keeps) audio up to <paramref name="seconds"/>.</summary>
        public void RenderUntil(double seconds)
        {
            long target = (long)Math.Round(seconds * SampleRate);
            var render = _output.Render ?? throw new InvalidOperationException("Audio engine not started.");
            while (_rendered < target)
            {
                Engine.Offline!.Advance(Engine.Now, TimeSpan.FromSeconds(2));
                int n = (int)Math.Min(Block, target - _rendered);
                Array.Clear(_block);
                render(_block, n);
                for (int i = 0; i < n * 2; i++)
                {
                    float v = Math.Clamp(_block[i], -1f, 1f);
                    if (v != 0) HeardAnything = true;
                    short s = (short)(v * 32767);
                    _pcm.WriteByte((byte)s);
                    _pcm.WriteByte((byte)(s >> 8));
                }
                _rendered += n;
            }
        }

        /// <summary>16-bit stereo WAV of everything rendered so far.</summary>
        public void WriteWav(string path)
        {
            using var w = new BinaryWriter(File.Create(path));
            int bytes = (int)_pcm.Length;
            w.Write("RIFF"u8); w.Write(36 + bytes); w.Write("WAVE"u8);
            w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)2);
            w.Write(SampleRate); w.Write(SampleRate * 4); w.Write((short)4); w.Write((short)16);
            w.Write("data"u8); w.Write(bytes);
            _pcm.Position = 0;
            _pcm.CopyTo(w.BaseStream);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Engine.Loops.StopAll();
            Engine.Offline!.Advance(double.MaxValue, TimeSpan.FromMilliseconds(200)); // let cancelled loops finish
            AudioEngine.SwapShared(_previous);
            Engine.Dispose();
        }
    }
}
