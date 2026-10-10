using System.Diagnostics;

namespace DanaProcessing.Reels
{
    /// <summary>
    /// A reel, as a sketch: plays a <see cref="ReelTimeline"/> in any host
    /// that can run sketches (the IDE's preview is just an
    /// AvaloniaSketchCanvas running this). Draws at a reduced size --
    /// <paramref name="previewScale"/> of the reel's real resolution --
    /// since the timeline itself is resolution-independent.
    /// </summary>
    public sealed class ReelPlayerSketch : Sketch
    {
        private readonly ReelTimeline _timeline;
        private readonly float _scale;
        private readonly Stopwatch _clock = new();
        private double _time;
        private long _seekBits = BitConverter.DoubleToInt64Bits(-1);
        private TimeSpan _lastTick;

        public ReelPlayerSketch(ReelTimeline timeline, float previewScale)
        {
            _timeline = timeline;
            _scale = Math.Clamp(previewScale, 0.05f, 2f);
        }

        public ReelTimeline Timeline => _timeline;

        /// <summary>Current position, seconds.</summary>
        public double Time => Volatile.Read(ref _time);

        public double Duration => _timeline.Duration;

        /// <summary>Paused = the frame stays, the clock doesn't move.</summary>
        public bool IsPlaying { get; set; } = true;

        /// <summary>Start over at the end instead of stopping there.</summary>
        public bool LoopPlayback { get; set; } = true;

        /// <summary>Jump to <paramref name="seconds"/>. Safe to call from any thread; takes effect on the next frame.</summary>
        public void Seek(double seconds) => Interlocked.Exchange(ref _seekBits, BitConverter.DoubleToInt64Bits(Math.Max(0, seconds)));

        public override void Setup()
        {
            Size(Math.Max(1, (int)Math.Round(_timeline.Width * _scale)), Math.Max(1, (int)Math.Round(_timeline.Height * _scale)));
            FrameRate(_timeline.Fps);
            _clock.Start();
            _lastTick = _clock.Elapsed;
        }

        public override void Draw()
        {
            var now = _clock.Elapsed;
            double dt = Math.Min(0.1, (now - _lastTick).TotalSeconds);
            _lastTick = now;

            double seek = BitConverter.Int64BitsToDouble(Interlocked.Exchange(ref _seekBits, BitConverter.DoubleToInt64Bits(-1)));
            double t = seek >= 0 ? seek : _time + (IsPlaying ? dt : 0);
            if (t >= _timeline.Duration)
            {
                if (LoopPlayback)
                    t = 0;
                else
                {
                    t = _timeline.Duration;
                    IsPlaying = false;
                }
            }
            Volatile.Write(ref _time, t);

            Canvas.Save();
            Canvas.Scale(_scale);
            _timeline.Render(Canvas, t);
            Canvas.Restore();
        }
    }
}
