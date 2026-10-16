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
    /// 1920x1080 one about 16 frames. Rewind is visual only -- the sketch's
    /// state stays wherever it really is.
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
        private readonly List<(SKImage Image, int FrameCount)> _frames = new();
        private bool _clearRequested;

        public long BudgetBytes { get; set; } = 128L * 1024 * 1024;
        public int MaxFrames { get; set; } = 600;

        public int Count
        {
            get { lock (_lock) return _clearRequested ? 0 : _frames.Count; }
        }

        /// <summary>Render thread only. Takes ownership of <paramref name="image"/>.</summary>
        public void Push(SKImage image, int frameCount)
        {
            lock (_lock)
            {
                DisposeIfRequested();
                _frames.Add((image, frameCount));
                long bytesPerFrame = Math.Max(1L, (long)image.Width * image.Height * 4);
                int capacity = (int)Math.Clamp(BudgetBytes / bytesPerFrame, 1, MaxFrames);
                while (_frames.Count > capacity)
                {
                    _frames[0].Image.Dispose();
                    _frames.RemoveAt(0);
                }
            }
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
            if (!_clearRequested)
                return;
            foreach (var frame in _frames)
                frame.Image.Dispose();
            _frames.Clear();
            _clearRequested = false;
        }
    }
}
