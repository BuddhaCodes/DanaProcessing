using System.Diagnostics;
using SkiaSharp;

namespace DanaProcessing
{
    /// <summary>
    /// The performance overlay (F3, or ShowStats()): frames per second, how long
    /// Draw() takes, how much it allocates per frame and how many garbage
    /// collections happened since the panel opened, plus a bar graph of the
    /// last 120 Draw() times against the frame budget. Measured by the engine
    /// around every Draw() (cheap enough to always run), drawn after it in raw
    /// Skia like the [Tweak] panel -- top left, while that one is top right.
    /// </summary>
    internal sealed class StatsPanel
    {
        private const int History = 120;
        private const float PanelWidth = 232f;
        private const float PanelHeight = 128f;
        private const float Margin = 10f;

        private readonly double[] _drawMs = new double[History];
        private readonly double[] _frameMs = new double[History];
        private readonly long[] _allocated = new long[History];
        private int _count;
        private int _next;

        private long _frameStartTicks;
        private long _lastFrameStartTicks;
        private long _allocBefore;
        private int[] _gcAtOpen = new int[3];

        public bool Visible { get; private set; }

        // Paints are created once and reused every frame.
        private readonly SKPaint _bg = new() { Color = new SKColor(12, 14, 20, 225), IsAntialias = true };
        private readonly SKPaint _title = new() { Color = new SKColor(140, 220, 160), IsAntialias = true, TextSize = 12, FakeBoldText = true };
        private readonly SKPaint _text = new() { Color = new SKColor(225, 228, 235), IsAntialias = true, TextSize = 12 };
        private readonly SKPaint _dim = new() { Color = new SKColor(150, 156, 170), IsAntialias = true, TextSize = 11 };
        private readonly SKPaint _ok = new() { Color = new SKColor(110, 210, 140) };
        private readonly SKPaint _slow = new() { Color = new SKColor(240, 110, 90) };
        private readonly SKPaint _budget = new() { Color = new SKColor(255, 255, 255, 70), StrokeWidth = 1 };

        public void Show()
        {
            if (Visible)
                return;
            Visible = true;
            for (int g = 0; g < 3; g++)
                _gcAtOpen[g] = GC.CollectionCount(g);
        }

        public void Hide() => Visible = false;

        public void Toggle()
        {
            if (Visible)
                Hide();
            else
                Show();
        }

        /// <summary>Right before Draw(): remembers when the frame started and how much this thread had allocated.</summary>
        public void BeginFrame()
        {
            _frameStartTicks = Stopwatch.GetTimestamp();
            _allocBefore = GC.GetAllocatedBytesForCurrentThread();
        }

        /// <summary>Right after Draw(): records its time, its allocations and the time since the previous frame.</summary>
        public void EndFrame()
        {
            long end = Stopwatch.GetTimestamp();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - _allocBefore;
            double drawMs = (end - _frameStartTicks) * 1000.0 / Stopwatch.Frequency;
            double frameMs = _lastFrameStartTicks == 0 ? 0 : (_frameStartTicks - _lastFrameStartTicks) * 1000.0 / Stopwatch.Frequency;
            _lastFrameStartTicks = _frameStartTicks;

            _drawMs[_next] = drawMs;
            _frameMs[_next] = frameMs;
            _allocated[_next] = Math.Max(0, allocated);
            _next = (_next + 1) % History;
            if (_count < History)
                _count++;
        }

        /// <summary>Average Draw() time over the last frames, in milliseconds.</summary>
        public double AverageDrawMs => Average(_drawMs);

        /// <summary>Slowest Draw() of the last frames.</summary>
        public double MaxDrawMs
        {
            get
            {
                double max = 0;
                for (int i = 0; i < _count; i++)
                    max = Math.Max(max, _drawMs[i]);
                return max;
            }
        }

        /// <summary>Frames per second, from the time between frames (0 until there are two).</summary>
        public double Fps
        {
            get
            {
                double ms = Average(_frameMs, skipZeros: true);
                return ms <= 0 ? 0 : 1000.0 / ms;
            }
        }

        /// <summary>Average bytes Draw() allocated per frame.</summary>
        public double AverageAllocatedBytes
        {
            get
            {
                double sum = 0;
                for (int i = 0; i < _count; i++)
                    sum += _allocated[i];
                return _count == 0 ? 0 : sum / _count;
            }
        }

        private double Average(double[] values, bool skipZeros = false)
        {
            double sum = 0;
            int n = 0;
            for (int i = 0; i < _count; i++)
            {
                if (skipZeros && values[i] <= 0)
                    continue;
                sum += values[i];
                n++;
            }
            return n == 0 ? 0 : sum / n;
        }

        public void Draw(Sketch sketch, SKCanvas canvas)
        {
            if (!Visible)
                return;

            float x = Margin, y = Margin;
            canvas.DrawRoundRect(new SKRect(x, y, x + PanelWidth, y + PanelHeight), 8, 8, _bg);
            canvas.DrawText("STATS  (F3)", x + 12, y + 18, _title);

            double budget = 1000.0 / Math.Max(1, sketch.TargetFrameRate);
            double fps = Fps;
            double draw = AverageDrawMs;
            double alloc = AverageAllocatedBytes;
            int gen0 = GC.CollectionCount(0) - _gcAtOpen[0];
            int gen1 = GC.CollectionCount(1) - _gcAtOpen[1];
            int gen2 = GC.CollectionCount(2) - _gcAtOpen[2];

            canvas.DrawText($"{fps,5:0.0} fps", x + 12, y + 38, _text);
            canvas.DrawText($"target {sketch.TargetFrameRate}", x + 120, y + 38, _dim);
            canvas.DrawText($"Draw {draw,5:0.00} ms", x + 12, y + 55, _text);
            canvas.DrawText($"max {MaxDrawMs:0.0}", x + 120, y + 55, _dim);
            canvas.DrawText($"{FormatBytes(alloc)}/frame", x + 12, y + 72, _text);
            canvas.DrawText($"GC {gen0}/{gen1}/{gen2}", x + 120, y + 72, _dim);

            // Bar graph: one bar per Draw(), green inside the frame budget, red over it.
            float gx = x + 12, gy = y + 84, gw = PanelWidth - 24, gh = 34;
            double scaleMs = Math.Max(budget * 1.5, MaxDrawMs);
            float barW = gw / History;
            for (int i = 0; i < _count; i++)
            {
                int index = (_next - _count + i + History) % History;
                double ms = _drawMs[index];
                float h = (float)Math.Min(gh, ms / scaleMs * gh);
                float bx = gx + (History - _count + i) * barW;
                canvas.DrawRect(new SKRect(bx, gy + gh - h, bx + Math.Max(1f, barW - 0.3f), gy + gh), ms > budget ? _slow : _ok);
            }
            float budgetY = gy + gh - (float)(budget / scaleMs * gh);
            canvas.DrawLine(gx, budgetY, gx + gw, budgetY, _budget);
        }

        private static string FormatBytes(double bytes) => bytes switch
        {
            < 1024 => $"{bytes:0} B",
            < 1024 * 1024 => $"{bytes / 1024:0.0} KB",
            _ => $"{bytes / (1024 * 1024):0.0} MB",
        };
    }
}
