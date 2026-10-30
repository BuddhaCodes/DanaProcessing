namespace DanaProcessing
{
    /// <summary>One reading of the target window, taken by the host every frame. Logical pixels.</summary>
    /// <param name="Showing">Found, visible, not minimized and not on another desktop.</param>
    /// <param name="Focused">The target is the foreground window.</param>
    /// <param name="X">Left edge of the sketch's area on screen.</param>
    /// <param name="Y">Top edge of the sketch's area on screen (below the inset).</param>
    /// <param name="Dt">Seconds since the previous reading.</param>
    internal readonly record struct TargetSample(bool Showing, bool Focused, int X, int Y, double Dt);

    /// <summary>What kind of input a <see cref="TargetInput"/> carries.</summary>
    internal enum TargetInputKind { KeyDown, KeyUp, MouseDown, MouseUp, Wheel }

    /// <summary>
    /// A key or mouse button that reached the target window, read by the
    /// host's low-level hooks (only while the sketch opted in). Mouse
    /// coordinates are already local to the sketch, in logical pixels.
    /// EnqueuedAt is Environment.TickCount64 when the hook saw it (for the
    /// host's input-latency measurement; 0 = unknown).
    /// </summary>
    internal readonly record struct TargetInput(
        TargetInputKind Kind,
        char Key = '\0',
        int KeyCode = 0,
        MouseButtonKind Button = MouseButtonKind.None,
        float X = 0,
        float Y = 0,
        float Wheel = 0,
        long EnqueuedAt = 0);

    public abstract partial class AttachedSketch
    {
        // =====================================================================
        // Motion -- how the target window is moving, for inertia effects
        // (water that sloshes when you drag the terminal, things that fall
        // off when you shake it). Same smoothing as Abyss Tide: raw velocity
        // from the position change, then exponential smoothing so a single
        // jittery frame doesn't spike it.
        // =====================================================================

        /// <summary>Above this speed (logical px/s) the target counts as moving.</summary>
        public const float TargetMovingSpeed = 5;

        /// <summary>Acceleration is clamped to ±this (logical px/s²): a window that jumps across the screen in one frame shouldn't launch everything into orbit.</summary>
        public const float TargetAccelerationLimit = 30000;

        private double _vx, _vy, _vxPrev, _vyPrev, _ax, _ay;
        private int _lastX, _lastY;
        private bool _hasLastPosition;
        private bool? _showing, _focused;
        private bool _cursorKnown;

        /// <summary>
        /// How fast the target window is moving right now, in logical pixels
        /// per second (smoothed). Zero while it's still or hidden.
        /// </summary>
        public PVector TargetVelocity => new((float)_vx, (float)_vy);

        /// <summary>
        /// How the target's velocity is changing, in logical px/s² (smoothed
        /// and clamped to ±TargetAccelerationLimit). Things inside a moving
        /// box feel the opposite of this: drag right and stop suddenly, and
        /// the water hits the right wall. Use -TargetAcceleration.X as a
        /// sideways force and Gravity - TargetAcceleration.Y as gravity.
        /// </summary>
        public PVector TargetAcceleration => new((float)_ax, (float)_ay);

        /// <summary>True while the target is being dragged (speed above TargetMovingSpeed).</summary>
        public bool IsTargetMoving => Math.Sqrt(_vx * _vx + _vy * _vy) > TargetMovingSpeed;

        /// <summary>True while the target is the foreground window (the one you're typing in).</summary>
        public bool IsTargetFocused { get; private set; }

        // =====================================================================
        // Events -- called between frames, on the same thread as Draw(), never
        // in the middle of one. None fire for the state the target is in when
        // the sketch starts (read IsTargetFocused/IsAttached in Setup() for
        // that), only for changes after it.
        // =====================================================================

        /// <summary>The target moved since the last frame (fires every frame while it's dragged). Read TargetVelocity/TargetAcceleration for how.</summary>
        public virtual void TargetMoved() { }

        /// <summary>The target became the foreground window.</summary>
        public virtual void TargetFocused() { }

        /// <summary>Another window took the foreground from the target.</summary>
        public virtual void TargetBlurred() { }

        /// <summary>The target came back: restored, back on this desktop, or opened again. Draw() runs again from this frame.</summary>
        public virtual void TargetShown() { }

        /// <summary>The target went away: minimized, hidden, on another virtual desktop, too small, or closed. Draw() stops until TargetShown().</summary>
        public virtual void TargetHidden() { }

        // =====================================================================
        // Input -- opt-in, because reading another app's keys needs a global
        // keyboard hook. Only keys typed while the target is in the foreground
        // reach the sketch, and only clicks inside its area; the target still
        // gets every one of them (nothing is swallowed).
        // =====================================================================

        /// <summary>True after ListenToTargetKeys().</summary>
        public bool ListensToTargetKeys { get; private set; }

        /// <summary>True after ListenToTargetMouse().</summary>
        public bool ListensToTargetMouse { get; private set; }

        /// <summary>
        /// Keys typed into the target window reach KeyPressed(), KeyTyped() and
        /// KeyReleased(), with Key/KeyCode set as usual (letters respect Shift
        /// and the keyboard layout). Only while the target is the foreground
        /// window -- nothing typed anywhere else is ever read. The target
        /// still receives every key. Uses a low-level keyboard hook, which
        /// some antivirus tools flag; call it only when the sketch needs it.
        /// </summary>
        public void ListenToTargetKeys(bool listen = true) => ListensToTargetKeys = listen;

        /// <summary>
        /// Clicks and the wheel over the sketch's area reach MousePressed(),
        /// MouseReleased(), MouseClicked() and MouseWheel(). The click still
        /// goes to the target underneath. MouseX/MouseY and MouseMoved() work
        /// without this.
        /// </summary>
        public void ListenToTargetMouse(bool listen = true) => ListensToTargetMouse = listen;

        // --- Host hooks ---------------------------------------------------------

        /// <summary>
        /// Host hook, once per frame after Setup(): updates motion and focus,
        /// and fires the matching events through <paramref name="run"/> (the
        /// host wraps each in its crash handling: run(callback, "Name")).
        /// </summary>
        internal void Track(TargetSample sample, Action<Action, string> run)
        {
            if (!sample.Showing)
            {
                ResetMotion();
                if (_showing != false)
                {
                    bool known = _showing != null;
                    _showing = false;
                    if (known)
                        run(TargetHidden, nameof(TargetHidden));
                }
                return;
            }

            if (_showing != true)
            {
                bool known = _showing != null;
                _showing = true;
                if (known)
                    run(TargetShown, nameof(TargetShown));
            }

            if (_focused != sample.Focused)
            {
                bool known = _focused != null;
                _focused = sample.Focused;
                IsTargetFocused = sample.Focused;
                if (known)
                {
                    if (sample.Focused)
                        run(TargetFocused, nameof(TargetFocused));
                    else
                        run(TargetBlurred, nameof(TargetBlurred));
                }
            }

            double dt = Math.Clamp(sample.Dt, 0.0005, 0.05);
            bool moved = _hasLastPosition && (sample.X != _lastX || sample.Y != _lastY);
            if (_hasLastPosition)
            {
                double rawVx = (sample.X - _lastX) / dt, rawVy = (sample.Y - _lastY) / dt;
                double kv = Math.Min(1, dt * 22);
                _vx += (rawVx - _vx) * kv;
                _vy += (rawVy - _vy) * kv;

                double rawAx = (_vx - _vxPrev) / dt, rawAy = (_vy - _vyPrev) / dt;
                double ka = Math.Min(1, dt * 28);
                _ax = Math.Clamp(_ax + (rawAx - _ax) * ka, -TargetAccelerationLimit, TargetAccelerationLimit);
                _ay = Math.Clamp(_ay + (rawAy - _ay) * ka, -TargetAccelerationLimit, TargetAccelerationLimit);
            }
            _vxPrev = _vx;
            _vyPrev = _vy;
            _lastX = sample.X;
            _lastY = sample.Y;
            _hasLastPosition = true;

            if (moved)
                run(TargetMoved, nameof(TargetMoved));
        }

        /// <summary>
        /// Host hook, once per frame: the cursor in sketch coordinates (logical
        /// pixels). Fires MouseMoved()/MouseDragged() when it moved inside the area.
        /// </summary>
        internal void TrackCursor(float x, float y, Action<Action, string> run)
        {
            bool changed = !_cursorKnown || x != MouseX || y != MouseY;
            MouseX = x;
            MouseY = y;
            bool inside = x >= 0 && y >= 0 && x < Width && y < Height;
            if (_cursorKnown && changed && inside)
                run(RaiseMouseMoved, IsMousePressed ? nameof(MouseDragged) : nameof(MouseMoved));
            _cursorKnown = true;
        }

        /// <summary>
        /// Host hook: one key or mouse event read from the target. Ignored
        /// unless the sketch opted in with ListenToTargetKeys()/ListenToTargetMouse().
        /// </summary>
        internal void DispatchTargetInput(TargetInput input, Action<Action, string> run)
        {
            switch (input.Kind)
            {
                case TargetInputKind.KeyDown when ListensToTargetKeys:
                    IsKeyPressed = true;
                    Key = input.Key;
                    KeyCode = input.KeyCode;
                    run(KeyPressed, nameof(KeyPressed));
                    run(RaiseKeyTyped, nameof(KeyTyped));
                    break;

                case TargetInputKind.KeyUp when ListensToTargetKeys:
                    IsKeyPressed = false;
                    Key = input.Key;
                    KeyCode = input.KeyCode;
                    run(KeyReleased, nameof(KeyReleased));
                    break;

                case TargetInputKind.MouseDown when ListensToTargetMouse:
                    MouseX = input.X;
                    MouseY = input.Y;
                    run(() => RaiseMousePressed(input.Button), nameof(MousePressed));
                    break;

                case TargetInputKind.MouseUp when ListensToTargetMouse:
                    MouseX = input.X;
                    MouseY = input.Y;
                    run(() => RaiseMouseReleased(input.Button), nameof(MouseReleased));
                    run(() => RaiseMouseClicked(input.Button), nameof(MouseClicked));
                    break;

                case TargetInputKind.Wheel when ListensToTargetMouse:
                    run(() => RaiseMouseWheel(input.Wheel), nameof(MouseWheel));
                    break;
            }
        }

        private void ResetMotion()
        {
            _vx = _vy = _vxPrev = _vyPrev = _ax = _ay = 0;
            _hasLastPosition = false;
        }
    }
}
