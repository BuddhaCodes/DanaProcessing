namespace DanaProcessing
{
    /// <summary>
    /// The block editor's "step by step" switch. While one is active
    /// (<see cref="Turtle.Stepper"/>), a turtle waits before carrying out each
    /// command that comes from a block, until the IDE says "next step"
    /// (<see cref="Step"/>). Then it does that one command, at its usual
    /// speed, and waits again -- one block per click.
    /// </summary>
    internal sealed class BlockStepper
    {
        private int _ready;
        private int _current = -1;
        private int _waiting;

        /// <summary>Lets the next block go. Clicking twice before it starts still lets only one go.</summary>
        public void Step() => Volatile.Write(ref _ready, 1);

        /// <summary>The block that went last (it stays lit while the program waits), or -1 before the first step.</summary>
        public int Current => Volatile.Read(ref _current);

        /// <summary>True while a turtle has a block's command ready and is waiting for <see cref="Step"/>.</summary>
        public bool Waiting => Volatile.Read(ref _waiting) == 1;

        /// <summary>Called by a turtle about to start block <paramref name="block"/>'s command: may it go now?</summary>
        internal bool TryTake(int block)
        {
            if (Interlocked.Exchange(ref _ready, 0) == 1)
            {
                Volatile.Write(ref _current, block);
                Volatile.Write(ref _waiting, 0);
                return true;
            }
            Volatile.Write(ref _waiting, 1);
            return false;
        }
    }
}
