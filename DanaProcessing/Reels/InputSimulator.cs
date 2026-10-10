using System.Reflection;

namespace DanaProcessing.Reels
{
    /// <summary>
    /// Plays a "pretend user" into the sketch during the result part: mouse
    /// moves, drags, clicks and key presses, either the ones the markers ask
    /// for or -- with Mouse=Auto and a sketch that reacts to the mouse -- an
    /// invented, natural-looking path. Everything is a pure function of time,
    /// so preview and export see exactly the same interaction.
    /// </summary>
    internal sealed class InputSimulator
    {
        private enum Ev { Press, Release, Click, KeyDown, KeyUp }

        private sealed class Seg
        {
            public double T0, T1;
            public List<(float X, float Y)> Points = new();
            public float[] Cum = Array.Empty<float>();
            public float Length;
        }

        private readonly List<Seg> _segs = new();
        private readonly List<(double T, Ev Kind, float X, float Y, string? Key)> _events = new();
        private readonly bool _auto;
        private readonly float _w, _h;

        /// <summary>A cursor should be drawn (the sketch gets mouse input at all).</summary>
        public bool HasMouse { get; }

        public InputSimulator(ReelScript script, Type? sketchType, int sketchWidth, int sketchHeight, double duration)
        {
            _w = sketchWidth;
            _h = sketchHeight;

            bool scriptedMouse = script.Input.Any(a => a.Kind != ReelInputKind.Key);
            foreach (var a in script.Input.Where(a => a.Kind == ReelInputKind.Key))
            {
                _events.Add((a.At, Ev.KeyDown, 0, 0, a.Key));
                _events.Add((a.At + 0.12, Ev.KeyUp, 0, 0, a.Key));
            }

            if (script.Mouse == ReelMouseMode.None)
            {
                HasMouse = false;
            }
            else if (scriptedMouse)
            {
                HasMouse = true;
                foreach (var a in script.Input.Where(a => a.Kind != ReelInputKind.Key).OrderBy(a => a.At))
                {
                    var seg = new Seg { T0 = a.At, T1 = a.At + a.Duration, Points = a.Points };
                    Measure(seg);
                    _segs.Add(seg);
                    var first = a.Points[0];
                    var last = a.Points[^1];
                    switch (a.Kind)
                    {
                        case ReelInputKind.Drag:
                            _events.Add((seg.T0, Ev.Press, first.X, first.Y, null));
                            _events.Add((seg.T1, Ev.Release, last.X, last.Y, null));
                            break;
                        case ReelInputKind.Click:
                            _events.Add((seg.T0, Ev.Press, first.X, first.Y, null));
                            _events.Add((seg.T0 + 0.09, Ev.Click, first.X, first.Y, null));
                            break;
                    }
                }
            }
            else
            {
                bool Overrides(string name) =>
                    sketchType?.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, name == "MouseWheel" ? new[] { typeof(float) } : Type.EmptyTypes, null) is { } m
                    && m.DeclaringType != typeof(Sketch);

                bool presses = Overrides("MousePressed") || Overrides("MouseClicked") || Overrides("MouseReleased");
                bool drags = Overrides("MouseDragged");
                bool moves = Overrides("MouseMoved");
                HasMouse = presses || drags || moves || script.SourceUsesMouse;
                _auto = HasMouse;

                if (HasMouse && duration > 0.5)
                {
                    if (presses)
                    {
                        foreach (var f in new[] { 0.28, 0.46 })
                        {
                            var p = AutoPath(duration * f);
                            _events.Add((duration * f, Ev.Press, p.X, p.Y, null));
                            _events.Add((duration * f + 0.09, Ev.Click, p.X, p.Y, null));
                        }
                    }
                    if (drags || (script.SourceUsesMouse && !presses))
                    {
                        // Press-and-drag for a stretch: what most "draw with the
                        // mouse" sketches need to show anything at all.
                        var a = AutoPath(duration * 0.6);
                        var b = AutoPath(duration * 0.88);
                        _events.Add((duration * 0.6, Ev.Press, a.X, a.Y, null));
                        _events.Add((duration * 0.88, Ev.Release, b.X, b.Y, null));
                    }
                }
            }

            _events.Sort((x, y) => x.T.CompareTo(y.T));
        }

        private static void Measure(Seg s)
        {
            s.Cum = new float[s.Points.Count];
            float total = 0;
            for (int i = 1; i < s.Points.Count; i++)
            {
                var dx = s.Points[i].X - s.Points[i - 1].X;
                var dy = s.Points[i].Y - s.Points[i - 1].Y;
                total += MathF.Sqrt(dx * dx + dy * dy);
                s.Cum[i] = total;
            }
            s.Length = total;
        }

        private (float X, float Y) AutoPath(double t)
        {
            // Two incommensurate sines: a loose, hand-like figure that covers
            // most of the canvas without ever looking mechanical. Starts
            // drifting in from the lower right.
            double ax = 2 * Math.PI * 0.19, ay = 2 * Math.PI * 0.31;
            float x = _w * 0.5f + _w * 0.32f * (float)Math.Sin(ax * t + 0.9);
            float y = _h * 0.5f + _h * 0.28f * (float)Math.Sin(ay * t + 0.4);
            x += _w * 0.04f * (float)Math.Sin(2 * Math.PI * 0.9 * t);
            y += _h * 0.03f * (float)Math.Cos(2 * Math.PI * 0.7 * t);
            return (x, y);
        }

        public (float X, float Y) PositionAt(double t)
        {
            if (_auto)
                return AutoPath(t);
            if (_segs.Count == 0)
                return (_w * 0.5f, _h * 0.5f);

            if (t <= _segs[0].T0)
                return _segs[0].Points[0];

            for (int i = 0; i < _segs.Count; i++)
            {
                var s = _segs[i];
                if (t <= s.T1)
                {
                    if (t < s.T0)
                    {
                        // Gliding from the previous action to this one.
                        var prev = _segs[i - 1];
                        var from = prev.Points[^1];
                        var to = s.Points[0];
                        double gap = Math.Max(1e-6, s.T0 - prev.T1);
                        float u = Smooth((float)((t - prev.T1) / gap));
                        return (from.X + (to.X - from.X) * u, from.Y + (to.Y - from.Y) * u);
                    }
                    if (s.Points.Count == 1 || s.T1 <= s.T0)
                        return s.Points[^1];
                    float k = Smooth((float)((t - s.T0) / (s.T1 - s.T0)));
                    return Along(s, k * s.Length);
                }
            }
            return _segs[^1].Points[^1];
        }

        private static (float X, float Y) Along(Seg s, float dist)
        {
            for (int i = 1; i < s.Points.Count; i++)
            {
                if (dist <= s.Cum[i] || i == s.Points.Count - 1)
                {
                    float segLen = s.Cum[i] - s.Cum[i - 1];
                    float u = segLen <= 0 ? 1 : Math.Clamp((dist - s.Cum[i - 1]) / segLen, 0, 1);
                    var a = s.Points[i - 1];
                    var b = s.Points[i];
                    return (a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u);
                }
            }
            return s.Points[^1];
        }

        private static float Smooth(float u)
        {
            u = Math.Clamp(u, 0, 1);
            return u * u * (3 - 2 * u);
        }

        public bool PressedAt(double t)
        {
            bool down = false;
            foreach (var e in _events)
            {
                if (e.T > t) break;
                if (e.Kind == Ev.Press) down = true;
                else if (e.Kind is Ev.Release or Ev.Click) down = false;
            }
            return down;
        }

        /// <summary>Presses that happened in the last <paramref name="window"/> seconds, for the ripple effect.</summary>
        public IEnumerable<(float X, float Y, double Age)> RecentPresses(double t, double window = 0.7)
        {
            foreach (var e in _events)
            {
                if (e.T > t) yield break;
                if (e.Kind == Ev.Press && t - e.T <= window)
                    yield return (e.X, e.Y, t - e.T);
            }
        }

        /// <summary>The most recent key press within <paramref name="window"/> seconds, for the keycap badge.</summary>
        public (string Label, double Age)? RecentKey(double t, double window = 0.9)
        {
            (string, double)? found = null;
            foreach (var e in _events)
            {
                if (e.T > t) break;
                if (e.Kind == Ev.KeyDown && t - e.T <= window)
                    found = (KeyLabel(e.Key!), t - e.T);
            }
            return found;
        }

        /// <summary>
        /// Brings the sketch's input state from time <paramref name="from"/> to
        /// <paramref name="to"/> (seconds since the result started), firing the
        /// sketch's callbacks exactly like the real host would.
        /// </summary>
        public void Apply(Sketch s, double from, double to)
        {
            foreach (var e in _events)
            {
                if (e.T <= from) continue;
                if (e.T > to) break;
                switch (e.Kind)
                {
                    case Ev.Press:
                        s.MouseX = e.X; s.MouseY = e.Y;
                        s.RaiseMousePressed(MouseButtonKind.Left);
                        break;
                    case Ev.Release:
                        s.MouseX = e.X; s.MouseY = e.Y;
                        s.RaiseMouseReleased(MouseButtonKind.Left);
                        break;
                    case Ev.Click:
                        s.RaiseMouseReleased(MouseButtonKind.Left);
                        s.RaiseMouseClicked(MouseButtonKind.Left);
                        break;
                    case Ev.KeyDown:
                    {
                        var (ch, code) = MapKey(e.Key!);
                        s.Key = ch;
                        s.SetKeyCode(code);
                        s.IsKeyPressed = true;
                        s.KeyPressed();
                        s.RaiseKeyTyped();
                        break;
                    }
                    case Ev.KeyUp:
                        s.IsKeyPressed = false;
                        s.KeyReleased();
                        break;
                }
            }

            if (!HasMouse)
                return;

            var (x, y) = PositionAt(to);
            if (x != s.MouseX || y != s.MouseY)
            {
                s.MouseX = x;
                s.MouseY = y;
                s.RaiseMouseMoved();
            }
        }

        internal static (char Key, int Code) MapKey(string name)
        {
            switch (name.Trim().ToLowerInvariant())
            {
                case "space": case "spacebar": return (' ', ' ');
                case "enter": case "return": return (Sketch.ENTER, 10);
                case "tab": return (Sketch.TAB, 9);
                case "backspace": return (Sketch.BACKSPACE, 8);
                case "esc": case "escape": return (Sketch.ESC, 27);
                case "delete": case "del": return (Sketch.DELETE, 127);
                case "up": return (Sketch.CODED, Sketch.UP);
                case "down": return (Sketch.CODED, Sketch.DOWN);
                case "left": return (Sketch.CODED, Sketch.LEFT);
                case "right": return (Sketch.CODED, Sketch.RIGHT);
                case "shift": return (Sketch.CODED, Sketch.SHIFT);
                case "ctrl": case "control": return (Sketch.CODED, Sketch.CONTROL);
                case "alt": return (Sketch.CODED, Sketch.ALT);
            }
            var c = name.Length > 0 ? name[0] : ' ';
            return (c, char.ToUpperInvariant(c));
        }

        internal static string KeyLabel(string name) => name.Trim().ToLowerInvariant() switch
        {
            "space" or "spacebar" => "Space",
            "enter" or "return" => "Enter",
            "esc" or "escape" => "Esc",
            "up" => "↑",
            "down" => "↓",
            "left" => "←",
            "right" => "→",
            var other when other.Length == 1 => other.ToUpperInvariant(),
            var other => char.ToUpperInvariant(other[0]) + other.Substring(1),
        };
    }
}
