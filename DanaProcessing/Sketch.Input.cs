using System;
using System.Diagnostics;

namespace DanaProcessing
{
    /// <summary>Which mouse button is involved in the current mouse event, mirroring Processing's LEFT/RIGHT/CENTER constants.</summary>
    public enum MouseButtonKind { None, Left, Right, Center }

    /// <summary>System cursor shapes a sketch can request via Sketch.Cursor(...). DanaProcessing can't move the OS pointer itself — see the note above CursorRequested.</summary>
    public enum CursorKind { Arrow, Cross, Hand, Move, Text, Wait }

    public abstract partial class Sketch
    {
        // =====================================================================
        // Mouse — https://processing.org/reference/mousePressed_.html and
        // siblings. Position (MouseX/MouseY/PMouseX/PMouseY) lives in the main
        // Sketch.cs since it predates this file; button state and the event
        // callbacks live here.
        // =====================================================================

        /// <summary>True while any mouse button is held down over the sketch, like Processing's mousePressed variable.</summary>
        public bool IsMousePressed { get; internal set; }

        /// <summary>Which button is/was involved in the current mouse event, like Processing's mouseButton.</summary>
        public MouseButtonKind MouseButton { get; internal set; } = MouseButtonKind.None;

        /// <summary>Override to react when a mouse button goes down.</summary>
        public virtual void MousePressed() { }

        /// <summary>Override to react when a mouse button is released.</summary>
        public virtual void MouseReleased() { }

        /// <summary>Override to react to a full press-and-release, like Processing's mouseClicked().</summary>
        public virtual void MouseClicked() { }

        /// <summary>Override to react to mouse movement while a button is held.</summary>
        public virtual void MouseDragged() { }

        /// <summary>Override to react to mouse movement with no button held.</summary>
        public virtual void MouseMoved() { }

        /// <summary>Override to react to the scroll wheel. Sign follows the host's raw input — treat it as relative motion, not an absolute count.</summary>
        public virtual void MouseWheel(float delta) { }

        // --- Host hooks: whatever runs the sketch (a window, a canvas control)
        // should update MouseX/MouseY/PMouseX/PMouseY itself, then call one of
        // these so the matching virtual method above actually fires — the same
        // pattern as SetCanvas() in Sketch.cs. ---

        internal void RaiseMousePressed(MouseButtonKind button)
        {
            IsMousePressed = true;
            MouseButton = button;
            MousePressed();
        }

        internal void RaiseMouseReleased(MouseButtonKind button)
        {
            IsMousePressed = false;
            MouseButton = button;
            MouseReleased();
        }

        internal void RaiseMouseClicked(MouseButtonKind button)
        {
            MouseButton = button;
            MouseClicked();
        }

        internal void RaiseMouseMoved()
        {
            if (IsMousePressed)
                MouseDragged();
            else
                MouseMoved();
        }

        internal void RaiseMouseWheel(float delta) => MouseWheel(delta);

        // =====================================================================
        // Keyboard extras — https://processing.org/reference/keyCode.html.
        // Key/IsKeyPressed/KeyPressed()/KeyReleased() live in the main
        // Sketch.cs; this adds KeyCode plus the special-key constants
        // Processing exposes for keys that don't map to a printable Key char.
        // =====================================================================

        /// <summary>Raw key code for non-printable keys (arrows, modifiers). Only meaningful when Key == CODED.</summary>
        public int KeyCode { get; internal set; }

        public const char CODED = '\uFFFF';
        public const char BACKSPACE = '\b';
        public const char TAB = '\t';
        public const char ENTER = '\n';
        public const char RETURN = '\r';
        public const char ESC = (char)27;
        public const char DELETE = (char)127;

        // KeyCode values for coded keys. Arbitrary but stable within
        // DanaProcessing — the host maps its own key/input enum onto these
        // when it calls SetKeyCode(), the same way it maps mouse buttons onto
        // MouseButtonKind above.
        public const int UP = 1;
        public const int DOWN = 2;
        public const int LEFT = 3;
        public const int RIGHT = 4;
        public const int ALT = 5;
        public const int CONTROL = 6;
        public const int SHIFT = 7;

        internal void SetKeyCode(int code) => KeyCode = code;

        /// <summary>Override to react to a printable character being typed, like Processing's keyTyped() — fires for printable keys only (Key != CODED), unlike KeyPressed()/KeyReleased() which fire for every key including arrows/modifiers.</summary>
        public virtual void KeyTyped() { }

        /// <summary>Host hook: call this alongside RaiseKeyPressed-equivalent handling whenever the pressed key produced a printable character (i.e. Key was set to something other than CODED) — mirrors how Processing fires keyTyped() as a companion to keyPressed() for printable keys.</summary>
        internal void RaiseKeyTyped()
        {
            if (Key != CODED)
                KeyTyped();
        }

        // =====================================================================
        // Time — https://processing.org/reference/millis_.html.
        // =====================================================================

        private readonly Stopwatch _clock = Stopwatch.StartNew();

        /// <summary>Milliseconds since the sketch started, like Processing's millis().</summary>
        public long Millis() => _clock.ElapsedMilliseconds;

        /// <summary>Nanoseconds since the sketch started, like Processing's nanoTime(). Stopwatch ticks are 100ns units on .NET, hence the *100 — precision is whatever the OS timer actually offers, same caveat Processing's own nanoTime() carries.</summary>
        public long NanoTime() => _clock.ElapsedTicks * 100L;

        // --- Wall-clock date/time — https://processing.org/reference/day_.html
        // and siblings (month/year/hour/minute/second). All read the local
        // system clock at call time, unlike Millis() which is relative to
        // sketch start. ---

        public int Day() => DateTime.Now.Day;
        public int Month() => DateTime.Now.Month;
        public int Year() => DateTime.Now.Year;
        public int Hour() => DateTime.Now.Hour;
        public int Minute() => DateTime.Now.Minute;
        public int Second() => DateTime.Now.Second;

        // =====================================================================
        // Cursor — https://processing.org/reference/cursor_.html. DanaProcessing
        // doesn't own a window, so it can't move the OS pointer itself; Cursor()
        // and NoCursor() just raise an event. Whatever hosts the sketch (e.g.
        // AvaloniaSketchCanvas) should subscribe to CursorRequested and set its
        // own Cursor property from the payload.
        // =====================================================================

        /// <summary>Raised when the sketch calls Cursor(...) or NoCursor(). Null payload means NoCursor().</summary>
        public event Action<CursorKind?>? CursorRequested;

        public void Cursor(CursorKind kind) => CursorRequested?.Invoke(kind);
        public void NoCursor() => CursorRequested?.Invoke(null);
    }
}