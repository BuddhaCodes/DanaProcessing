namespace DanaProcessing
{
    /// <summary>
    /// Where an <see cref="AttachedSketch"/> draws relative to the window it's
    /// attached to.
    /// </summary>
    public enum AttachLayer
    {
        /// <summary>
        /// On top of the target window, letting every click through to it.
        /// Works with any window; call Clear() at the start of Draw() so the
        /// target stays visible under what you draw. Not for Windows Terminal:
        /// a sketch on the terminal is always Behind, whatever Attach() says.
        /// </summary>
        Above,

        /// <summary>
        /// Right behind the target window. Only visible where the target
        /// itself is see-through (Windows Terminal with "opacity" below 100,
        /// for example) -- the text then stays on top of the sketch. Always
        /// the layer for Windows Terminal.
        /// </summary>
        Behind,
    }

    /// <summary>How many windows an <see cref="AttachedSketch"/> runs on.</summary>
    public enum AttachMode
    {
        /// <summary>One sketch, on the matching window in front (it moves to another one only when that one closes).</summary>
        Front,

        /// <summary>
        /// One sketch per matching window -- every open terminal gets its own,
        /// each a separate instance of the class with its own fields (see
        /// InstanceNumber). New windows get one as they open (up to MaxInstances).
        /// </summary>
        EachWindow,
    }

    /// <summary>
    /// Which window an <see cref="AttachedSketch"/> follows. Matched by window
    /// class or by process name; the one in front wins when several match.
    /// </summary>
    public sealed class WindowTarget
    {
        /// <summary>Win32 window class to match, or null.</summary>
        public string? ClassName { get; }

        /// <summary>Process name to match (without ".exe", any case), or null.</summary>
        public string? ProcessName { get; }

        /// <summary>
        /// Logical pixels left uncovered at the top of the target (its title
        /// bar or tabs) when the sketch doesn't ask for a specific inset.
        /// </summary>
        public int DefaultTopInset { get; }

        /// <summary>Human-readable name, for status messages.</summary>
        public string Description { get; }

        private WindowTarget(string? className, string? processName, int defaultTopInset, string description)
        {
            ClassName = className;
            ProcessName = processName;
            DefaultTopInset = Math.Max(0, defaultTopInset);
            Description = description;
        }

        /// <summary>Windows Terminal. Leaves its tab bar (40 px) uncovered.</summary>
        public static WindowTarget Terminal { get; } =
            new("CASCADIA_HOSTING_WINDOW_CLASS", null, 40, "Windows Terminal");

        /// <summary>Any window of the given Win32 class (Spy++ shows it).</summary>
        public static WindowTarget Class(string className, int topInset = 0)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(className);
            return new(className, null, topInset, className);
        }

        /// <summary>The main window of a process, by name: "Code", "notepad", "chrome"...</summary>
        public static WindowTarget Process(string processName, int topInset = 0)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(processName);
            var name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName[..^4]
                : processName;
            return new(null, name, topInset, name);
        }

        /// <summary>
        /// True for Windows Terminal: a sketch on it always draws behind it
        /// (under its text), never on top.
        /// </summary>
        public bool IsTerminal => ClassName == Terminal.ClassName;

        public override string ToString() => Description;
    }

    /// <summary>
    /// A sketch with no window of its own: its canvas is another app's window.
    /// It follows that window around the screen, takes its size (Width/Height
    /// change with it, and WindowResized() runs), and hides when it's minimized.
    ///
    /// <code>
    /// public class Rain : AttachedSketch
    /// {
    ///     public override void Settings() => Attach(WindowTarget.Terminal);
    ///
    ///     public override void Draw()
    ///     {
    ///         Clear();                       // see-through where nothing is drawn
    ///         Fill(120, 200, 255, 160);
    ///         Ellipse(Width / 2, Height / 2, 80, 80);
    ///     }
    /// }
    /// </code>
    ///
    /// Call Attach() in Settings(): it runs before the target is looked for,
    /// so Width/Height are already the target's when Setup() runs. Without
    /// Attach() the sketch follows Windows Terminal. Windows only for now.
    /// </summary>
    public abstract partial class AttachedSketch : Sketch
    {
        /// <summary>The window this sketch follows. Windows Terminal unless Attach() says otherwise.</summary>
        public WindowTarget Target { get; private set; } = WindowTarget.Terminal;

        /// <summary>
        /// Above or behind the target. Always Behind on Windows Terminal (the
        /// default target): the sketch stays under the text and only shows
        /// where the terminal is see-through (a profile with "opacity" below
        /// 100). On other windows, Above unless Attach() asks for Behind.
        /// </summary>
        public AttachLayer Layer => Target.IsTerminal ? AttachLayer.Behind : _layer;

        private AttachLayer _layer = AttachLayer.Above;

        /// <summary>Logical pixels left uncovered at the top of the target.</summary>
        public int TopInset { get; private set; } = WindowTarget.Terminal.DefaultTopInset;

        /// <summary>True while a target window has been found and the sketch is on it.</summary>
        public bool IsAttached { get; internal set; }

        /// <summary>Left edge of the sketch's area on screen, in logical pixels.</summary>
        public int TargetX { get; internal set; }

        /// <summary>Top edge of the sketch's area on screen (below the inset), in logical pixels.</summary>
        public int TargetY { get; internal set; }

        /// <summary>Physical pixels per logical pixel on the target's monitor (1.5 at 150 %).</summary>
        public float TargetScale { get; internal set; } = 1;

        /// <summary>Front (default): one sketch on the window in front. EachWindow: one per matching window.</summary>
        public AttachMode Mode { get; private set; } = AttachMode.Front;

        /// <summary>With AttachMode.EachWindow, at most this many windows get a sketch.</summary>
        public const int MaxInstances = 8;

        /// <summary>
        /// 0 for the first window's sketch, 1 for the second... (always 0 with
        /// AttachMode.Front). Each window's sketch is its own instance, also
        /// seeded differently (Seed + InstanceNumber), so Random() differs per window.
        /// </summary>
        public int InstanceNumber { get; internal set; }

        /// <summary>Bumped every time Attach() changes something, so the host knows to look again.</summary>
        internal int AttachVersion { get; private set; }

        /// <summary>
        /// Chooses the window to draw on. Best called from Settings(); calling
        /// it later (Setup(), Draw(), a key press) moves the sketch to the new
        /// target on the next frame.
        /// </summary>
        /// <param name="target">Window to follow.</param>
        /// <param name="layer">Above (any window) or Behind (see-through windows only). Ignored for Windows Terminal: always Behind.</param>
        /// <param name="topInset">Logical pixels to leave uncovered at the top; null uses the target's default.</param>
        /// <param name="mode">Front (one sketch) or EachWindow (one per matching window). Only read before the sketch starts: call Attach() in Settings() to use EachWindow.</param>
        public void Attach(WindowTarget target, AttachLayer layer = AttachLayer.Above, int? topInset = null,
                           AttachMode mode = AttachMode.Front)
        {
            ArgumentNullException.ThrowIfNull(target);
            Target = target;
            _layer = layer;
            TopInset = Math.Max(0, topInset ?? target.DefaultTopInset);
            Mode = mode;
            AttachVersion++;
        }

        /// <summary>
        /// Host hook: an export that lives in its own Windows Terminal profile
        /// (see AttachedRunOverrides) draws behind the terminal whatever the
        /// sketch's Attach() asked for.
        /// </summary>
        internal void ForceLayer(AttachLayer layer)
        {
            var before = Layer;
            _layer = layer;
            if (Layer != before)
                AttachVersion++;
        }

        /// <summary>
        /// Host hook: the target's area changed size. Sets Width/Height directly
        /// (not through Size(), which would also try to set the renderer) and
        /// lets the sketch know through WindowResized().
        /// </summary>
        internal void ApplyAttachedSize(int width, int height, bool notify)
        {
            if (width == Width && height == Height)
                return;
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            if (notify)
                WindowResized();
        }
    }
}
