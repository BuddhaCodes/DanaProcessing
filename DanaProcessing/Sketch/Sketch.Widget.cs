namespace DanaProcessing
{
    /// <summary>
    /// How a sketch that called <see cref="Sketch.Widget"/> sits on the desktop.
    /// </summary>
    /// <param name="Transparent">Asked for a see-through background: what Clear() leaves shows the desktop.</param>
    /// <param name="AlwaysOnTop">Stays above other windows.</param>
    /// <param name="Draggable">A plain left-drag anywhere moves it (otherwise Alt + drag, and left clicks reach the sketch).</param>
    public sealed record WidgetOptions(bool Transparent, bool AlwaysOnTop, bool Draggable);

    public abstract partial class Sketch
    {
        // =====================================================================
        // Widgets -- a sketch that lives on the desktop: no title bar or
        // borders, optionally see-through and above other windows, moved by
        // dragging it. The host does the window part; the sketch just asks
        // in Settings() and draws.
        //
        //     public override void Settings() { Size(480, 200); Widget(); }
        //     public override void Draw()
        //     {
        //         if (IsTransparentWindow) Clear(); else Background(20);
        //         ...
        //     }
        //
        // Right-click shows the widget's menu (always on top, close); it
        // remembers where it was left, per sketch class.
        // =====================================================================

        /// <summary>The widget settings asked for with Widget(), or null for an ordinary window.</summary>
        public WidgetOptions? WidgetMode { get; private set; }

        /// <summary>True after Widget(): the sketch runs as a desktop widget.</summary>
        public bool IsWidget => WidgetMode != null;

        /// <summary>
        /// True when the window really is see-through: what Clear() leaves
        /// (and any alpha below 255) shows the desktop behind it. Set by the
        /// host, and false where the system can't do it (Linux without a
        /// compositor, for example) -- there those pixels come out black, so
        /// draw a Background() instead.
        /// </summary>
        public bool IsTransparentWindow { get; internal set; }

        /// <summary>
        /// Runs the sketch as a desktop widget: no title bar or borders, a
        /// see-through background where Draw() calls Clear() (if
        /// <paramref name="transparent"/> and the system allows it: check
        /// IsTransparentWindow), above other windows (if
        /// <paramref name="alwaysOnTop"/>), and moved by dragging it (if
        /// <paramref name="draggable"/>; otherwise with Alt + drag, so left
        /// clicks reach MousePressed()). Right-click opens its menu. Call it
        /// in Settings(), before the window exists. In the IDE the sketch
        /// opens as a widget on the desktop and the canvas says so.
        /// </summary>
        public void Widget(bool transparent = true, bool alwaysOnTop = true, bool draggable = true) =>
            WidgetMode = new WidgetOptions(transparent, alwaysOnTop, draggable);

        /// <summary>Left edge of the sketch's window on screen, in logical pixels (0 where the host doesn't say). Read it to react to the window being dragged.</summary>
        public float WindowX { get; internal set; }

        /// <summary>Top edge of the sketch's window on screen, in logical pixels (0 where the host doesn't say).</summary>
        public float WindowY { get; internal set; }

        /// <summary>True once Settings() has run (a host may run it early, to read Widget() before making the window).</summary>
        internal bool SettingsRan { get; private set; }

        /// <summary>Host hook: Settings() once, through <paramref name="run"/>. Does nothing the second time.</summary>
        internal void RunSettings(Action<Action, string> run)
        {
            if (SettingsRan)
                return;
            SettingsRan = true;
            run(Settings, nameof(Settings));
        }
    }
}
