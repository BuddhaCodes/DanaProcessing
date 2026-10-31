using System.ComponentModel;

namespace DanaProcessing
{
    public abstract partial class Sketch
    {
        // When each block (of a program built in the IDE's block editor) last
        // ran, in Environment.TickCount64 ms; 0 = never. Indexed by block id,
        // which the editor hands out as small consecutive numbers. Written on
        // the render thread (Draw, the turtle) and audio-loop threads, read by
        // the editor on the UI thread to light blocks up: plain longs, written
        // and read with Volatile, no lock -- a reading that's one frame stale
        // is harmless here.
        private long[] _blockHits = Array.Empty<long>();
        private readonly object _blockHitsGrow = new();

        /// <summary>
        /// Records that the block with this id just ran. The block editor puts
        /// a call to it before every block's code when it runs a program, so it
        /// can show which blocks are running; code written by hand never needs it.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void BlockHit(int blockId)
        {
            if (blockId < 0)
                return;
            var hits = _blockHits;
            if (blockId >= hits.Length)
            {
                lock (_blockHitsGrow)
                {
                    hits = _blockHits;
                    if (blockId >= hits.Length)
                    {
                        var bigger = new long[Math.Max(blockId + 1, hits.Length * 2 + 16)];
                        Array.Copy(hits, bigger, hits.Length);
                        Volatile.Write(ref _blockHits, bigger);
                        hits = bigger;
                    }
                }
            }
            Volatile.Write(ref hits[blockId], Environment.TickCount64);
        }

        /// <summary>When the block with this id last ran (Environment.TickCount64 ms), or 0 if it never has.</summary>
        internal long LastBlockHit(int blockId)
        {
            var hits = Volatile.Read(ref _blockHits);
            return blockId >= 0 && blockId < hits.Length ? Volatile.Read(ref hits[blockId]) : 0;
        }

        /// <summary>Every turtle made for this sketch with <c>new Turtle(this)</c>, oldest first.</summary>
        internal List<Turtle> Turtles { get; } = new();
    }
}
