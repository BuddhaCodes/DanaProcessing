namespace DanaProcessing
{
    /// <summary>
    /// When an attached sketch can draw fewer frames. Pure, so the rule is testable.
    /// </summary>
    internal static class EcoPolicy
    {
        /// <summary>Seconds without activity before a sketch nobody can see slows down.</summary>
        public const double IdleDelay = 2;

        /// <summary>
        /// Throttle when eco is on and either the battery is low, or nobody can
        /// see it: another window covers the target (or, with
        /// EcoMode(whenUnfocused: true), the target just isn't in front), it
        /// isn't moving, and nothing has happened (input, a shell signal, a
        /// focus or visibility change) for IdleDelay seconds. A terminal that's
        /// merely not in front -- beside the window you're using -- is still
        /// seen, so by default it keeps its full frame rate.
        /// </summary>
        public static bool Throttle(bool enabled, bool focused, bool moving, double secondsSinceActivity, bool lowBattery,
                                    bool covered = false, bool whenUnfocused = false) =>
            enabled && (lowBattery ||
                        (!moving && secondsSinceActivity >= IdleDelay && (covered || (whenUnfocused && !focused))));

        /// <summary>The frame rate to run at.</summary>
        public static int FrameRate(bool throttled, int target, int eco) =>
            Math.Clamp(throttled ? Math.Min(target, eco) : target, 1, 240);
    }

    public abstract partial class AttachedSketch
    {
        // =====================================================================
        // Time and eco mode. An attached sketch lives on the desktop all day,
        // so by default it slows down to EcoFrameRate while nobody can see it
        // (another window covers the target, nothing moving, no input for 2 s)
        // and whenever the laptop is on low battery. Use DeltaTime for anything
        // that moves, so motion keeps the same speed at any frame rate.
        // =====================================================================

        /// <summary>Seconds since the previous frame (1/60 on the first one; at most 0.1, so a pause doesn't make things jump).</summary>
        public float DeltaTime { get; internal set; } = 1 / 60f;

        /// <summary>True after EcoMode() (the default).</summary>
        public bool IsEcoEnabled { get; private set; } = true;

        /// <summary>Frames per second while eco mode is slowing the sketch down. 15 by default.</summary>
        public int EcoFrameRate { get; private set; } = 15;

        /// <summary>True after EcoMode(whenUnfocused: true): slow down as soon as the target isn't in front, not only when it's covered.</summary>
        public bool IsEcoWhenUnfocused { get; private set; }

        /// <summary>True while eco mode is slowing the sketch down right now.</summary>
        public bool IsEco { get; internal set; }

        /// <summary>
        /// Turns eco mode on (the default) or off, optionally with the frame rate
        /// to drop to. Off, the sketch always runs at FrameRate(). By default it
        /// slows down only while another window covers the target;
        /// <paramref name="whenUnfocused"/>: also whenever the target isn't the
        /// window in front (saves more, but a terminal you're watching beside
        /// another window runs at EcoFrameRate).
        /// </summary>
        public void EcoMode(bool on = true, int? fps = null, bool whenUnfocused = false)
        {
            IsEcoEnabled = on;
            IsEcoWhenUnfocused = whenUnfocused;
            if (fps is { } f)
                EcoFrameRate = Math.Clamp(f, 1, 240);
        }

        /// <summary>Same as EcoMode(false): always full speed, even in the background.</summary>
        public void NoEco() => EcoMode(false);
    }
}
