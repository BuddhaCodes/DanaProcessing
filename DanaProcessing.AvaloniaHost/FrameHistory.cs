using System;
using System.Collections.Generic;
using SkiaSharp;

namespace DanaProcessing.AvaloniaHost
{
    /// <summary>
    /// The last few seconds of a running sketch, frame by frame -- what the
    /// canvas's rewind (Freeze/StepBack/ScrubTo) shows. Frames are stored at the
    /// sketch's LOGICAL size (supersampling already averaged away), and the
    /// buffer keeps as many as fit in <see cref="BudgetBytes"/>, up to
    /// <see cref="MaxFrames"/>: a 600x400 sketch gets the full 10 seconds, a
    /// 1920x1080 one about 16 frames. Frames can also carry a SketchState --
    /// the sketch's fields at that frame -- which is what lets the canvas
    /// continue running from a rewound frame (AvaloniaSketchCanvas.ResumeFromHere).
    ///
    /// Threading: Push/Get/Count are called from the render thread and the UI
    /// thread, so everything is under one lock. Clear() (UI thread, e.g. on
    /// Run) never disposes images itself -- the render thread might be drawing
    /// one right now -- it just flags them, and the next render-thread call
    /// disposes them.
    /// </summary>
    internal sealed class FrameHistory
    {
        private readonly object _lock = new();
        private readonly List<(SKImage Image, int FrameCount, SketchState? State)> _frames = new();
        private bool _clearRequested;
        private int _truncateAfter = -1;

        /// <summary>Objects kept across all states at most (old frames lose their state first; their pictures stay).</summary>
        public long StateBudgetObjects { get; set; } = 3_000_000;

        public long BudgetBytes { get; set; } = 128L * 1024 * 1024;
        public int MaxFrames { get; set; } = 600;

        public int Count
        {
            get { lock (_lock) return _clearRequested ? 0 : _frames.Count; }
        }

        /// <summary>Render thread only. Takes ownership of <paramref name="image"/>.</summary>
        public void Push(SKImage image, int frameCount, SketchState? state = null)
        {
            lock (_lock)
            {
                DisposeIfRequested();
                _frames.Add((image, frameCount, state));
                long bytesPerFrame = Math.Max(1L, (long)image.Width * image.Height * 4);
                int capacity = (int)Math.Clamp(BudgetBytes / bytesPerFrame, 1, MaxFrames);
                while (_frames.Count > capacity)
                {
                    _frames[0].Image.Dispose();
                    _frames.RemoveAt(0);
                }

                long objects = 0;
                for (int i = _frames.Count - 1; i >= 0; i--)
                {
                    if (_frames[i].State is not { } st)
                        continue;
                    objects += st.ObjectCount;
                    if (objects > StateBudgetObjects)
                        _frames[i] = (_frames[i].Image, _frames[i].FrameCount, null);
                }
            }
        }

        /// <summary>The newest frame at or before <paramref name="index"/> that carries a state, or -1.</summary>
        public int StateIndexAtOrBefore(int index)
        {
            lock (_lock)
            {
                if (_clearRequested)
                    return -1;
                for (int i = Math.Min(index, _frames.Count - 1); i >= 0; i--)
                    if (_frames[i].State != null)
                        return i;
                return -1;
            }
        }

        public SketchState? StateAt(int index)
        {
            lock (_lock)
                return !_clearRequested && index >= 0 && index < _frames.Count ? _frames[index].State : null;
        }

        /// <summary>Forgets every frame after <paramref name="index"/> (the timeline forks from there). Applied on the render thread's next call.</summary>
        public void TruncateAfter(int index)
        {
            lock (_lock)
                _truncateAfter = index;
        }

        /// <summary>Render thread only: the image stays valid until the next Push/Get on that thread.</summary>
        public SKImage? Get(int index)
        {
            lock (_lock)
            {
                DisposeIfRequested();
                return index >= 0 && index < _frames.Count ? _frames[index].Image : null;
            }
        }

        /// <summary>The sketch's FrameCount when frame <paramref name="index"/> was drawn, or -1.</summary>
        public int FrameCountAt(int index)
        {
            lock (_lock)
                return !_clearRequested && index >= 0 && index < _frames.Count ? _frames[index].FrameCount : -1;
        }

        public void Clear()
        {
            lock (_lock)
                _clearRequested = true;
        }

        private void DisposeIfRequested()
        {
            if (_truncateAfter >= 0)
            {
                for (int i = _frames.Count - 1; i > _truncateAfter; i--)
                {
                    _frames[i].Image.Dispose();
                    _frames.RemoveAt(i);
                }
                _truncateAfter = -1;
            }
            if (!_clearRequested)
                return;
            foreach (var frame in _frames)
                frame.Image.Dispose();
            _frames.Clear();
            _clearRequested = false;
        }
    }
}
