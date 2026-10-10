namespace DanaProcessing.Reels
{
    /// <summary>
    /// Entry point of the reel module. Importing it (<c>using DanaProcessing.Reels;</c>)
    /// is how a sketch opts in: the IDE then offers "Create reel", which reads
    /// the <c>// @reel</c> markers in the code (see <see cref="ReelScript"/>).
    /// </summary>
    public static class Reel
    {
        private static int _rendering;

        [ThreadStatic] private static bool _active;

        /// <summary>True while the sketch's code is being run by a reel (preview or export) rather than in a normal window. Lets a sketch skip, say, a slow intro.</summary>
        public static bool IsActive { get => _active; internal set => _active = value; }

        /// <summary>True while a reel is being written to a video file.</summary>
        public static bool IsExporting => Volatile.Read(ref _rendering) > 0;

        internal static void BeginExport() => Interlocked.Increment(ref _rendering);
        internal static void EndExport() => Interlocked.Decrement(ref _rendering);

        /// <summary>Reads the reel markers of a sketch's source code.</summary>
        public static ReelScript Parse(string source) => ReelScript.Parse(source);
    }
}
