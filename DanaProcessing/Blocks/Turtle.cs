using System.ComponentModel;

namespace DanaProcessing
{
    /// <summary>One straight line a <see cref="Turtle"/> drew with its pen down.</summary>
    public readonly record struct TurtleLine(float X1, float Y1, float X2, float Y2, Color Color, float Width)
    {
        public float Length => MathF.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
    }

    /// <summary>
    /// Logo's turtle: a little animal that walks and turns, drawing with a pen
    /// as it goes. Commands don't happen at once -- they queue up and the
    /// turtle carries them out one by one, at walking speed, every time you
    /// call <see cref="Draw"/> from Draw(). That's what makes it good for
    /// learning: you SEE each step of the program happen.
    /// <code>
    /// Turtle t;
    /// public override void Setup()
    /// {
    ///     Size(600, 400);
    ///     t = new Turtle(this);
    ///     for (int i = 0; i &lt; 4; i++) { t.Forward(100); t.TurnRight(90); }
    /// }
    /// public override void Draw() { Background(255); t.Draw(); }
    /// </code>
    /// Starts in the middle of the canvas, facing up, pen down, black.
    /// Heading is in degrees: 0 = up, 90 = right (clockwise, like a clock).
    /// </summary>
    public sealed class Turtle
    {
        /// <summary>Walking speed at <see cref="Speed"/> 1: pixels per second.</summary>
        private const float BaseStepsPerSecond = 220f;
        /// <summary>Turning speed at <see cref="Speed"/> 1: degrees per second.</summary>
        private const float BaseDegreesPerSecond = 400f;

        private enum Kind { Move, Turn, Pen, PenColor, PenSize, Home }

        private readonly record struct Command(Kind Kind, float Amount, Color Color, int Block);

        private Sketch? _sketch;

        /// <summary>The sketch it draws on (null for a turtle that only works out its path).</summary>
        internal Sketch? Owner => _sketch;

        /// <summary>Hot reload: the sketch's fields (this turtle included) moved to a new instance; draw on that one now.</summary>
        internal void Rebind(Sketch sketch) => _sketch = sketch;
        private readonly Queue<Command> _queue = new();
        private readonly List<TurtleLine> _lines = new();
        private readonly float _homeX, _homeY;

        // What's being carried out right now, and how much of it is left.
        private Command? _current;
        private float _remaining;
        private long _lastMillis = -1;

        // The turtle as it is on screen right now (animated)...
        private float _x, _y, _heading;
        private bool _penDown = true;
        private Color _penColor = new(30, 30, 30);
        private float _penSize = 3;

        // ...and as it will be once every queued command is done, so a program
        // can ask "where will I be?" (X/Y/Heading) right after queueing.
        private float _planX, _planY, _planHeading;

        /// <summary>A turtle in the middle of <paramref name="sketch"/>'s canvas, facing up.</summary>
        public Turtle(Sketch sketch) : this(sketch, sketch.Width / 2f, sketch.Height / 2f) { }

        /// <summary>A turtle at (<paramref name="x"/>, <paramref name="y"/>) of <paramref name="sketch"/>'s canvas, facing up.</summary>
        public Turtle(Sketch sketch, float x, float y) : this(x, y)
        {
            _sketch = sketch;
            lock (sketch.Turtles)
                sketch.Turtles.Add(this);
        }

        /// <summary>A turtle with no sketch: it only works out its path (see <see cref="Finish"/> and <see cref="Lines"/>). Draw() does nothing.</summary>
        public Turtle(float x, float y)
        {
            _homeX = _x = _planX = x;
            _homeY = _y = _planY = y;
        }

        /// <summary>
        /// How fast it walks and turns: 1 is normal, 0.25 is slow enough to
        /// follow each step, 4 is a hurry. 0 or less does everything at once.
        /// </summary>
        public float Speed { get; set; } = 1;

        /// <summary>
        /// Multiplies every turtle's speed -- the IDE's "slow motion" switch,
        /// so a program can be watched step by step without changing its code.
        /// </summary>
        internal static float SpeedFactor { get; set; } = 1;

        /// <summary>
        /// The IDE's "step by step" switch: while set, every turtle waits for
        /// a click before each command that comes from a block. Null = run freely.
        /// </summary>
        internal static BlockStepper? Stepper { get; set; }

        /// <summary>Whether the turtle itself is drawn (its lines always are).</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Where it will be once it has done everything asked so far.</summary>
        public float X => _planX;
        /// <summary>Where it will be once it has done everything asked so far.</summary>
        public float Y => _planY;
        /// <summary>Which way it will face once it has done everything asked so far (degrees, 0 = up).</summary>
        public float Heading => _planHeading;

        /// <summary>A copy of every line drawn so far (safe to read from any thread).</summary>
        public IReadOnlyList<TurtleLine> Lines
        {
            get { lock (_lines) return _lines.ToArray(); }
        }

        /// <summary>True once it has carried out every command.</summary>
        public bool IsDone => Volatile.Read(ref _pending) == 0;

        // Commands queued but not finished yet: IsDone is asked from other
        // threads (the IDE checking a challenge) while Draw() works through them.
        private int _pending;

        /// <summary>
        /// The id of the block (in DanaProcessing's block editor) the next
        /// command comes from, so the editor can light that block up while the
        /// turtle carries it out. Code written by hand never needs it.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int Block { get; set; } = -1;

        // =================================================================
        // Commands
        // =================================================================

        /// <summary>Walks forward <paramref name="steps"/> pixels (drawing, if the pen is down).</summary>
        public void Forward(float steps)
        {
            float rad = _planHeading * MathF.PI / 180f;
            _planX += MathF.Sin(rad) * steps;
            _planY -= MathF.Cos(rad) * steps;
            Enqueue(new Command(Kind.Move, steps, default, Block));
        }

        /// <summary>Walks backwards <paramref name="steps"/> pixels.</summary>
        public void Back(float steps) => Forward(-steps);

        /// <summary>Turns clockwise by <paramref name="degrees"/>.</summary>
        public void TurnRight(float degrees)
        {
            _planHeading = Normalize(_planHeading + degrees);
            Enqueue(new Command(Kind.Turn, degrees, default, Block));
        }

        /// <summary>Turns counter-clockwise by <paramref name="degrees"/>.</summary>
        public void TurnLeft(float degrees) => TurnRight(-degrees);

        /// <summary>Lifts the pen: walking no longer draws.</summary>
        public void PenUp() => Enqueue(new Command(Kind.Pen, 0, default, Block));

        /// <summary>Puts the pen down: walking draws again.</summary>
        public void PenDown() => Enqueue(new Command(Kind.Pen, 1, default, Block));

        /// <summary>The color of the lines from now on.</summary>
        public void PenColor(Color color) => Enqueue(new Command(Kind.PenColor, 0, color, Block));

        /// <summary>The color of the lines from now on (0-255 each).</summary>
        public void PenColor(int r, int g, int b) => PenColor(new Color((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255)));

        /// <summary>How thick the lines are from now on, in pixels.</summary>
        public void PenSize(float size) => Enqueue(new Command(Kind.PenSize, Math.Max(0.5f, size), default, Block));

        /// <summary>Goes straight back to where it started, facing up (without drawing).</summary>
        public void Home()
        {
            _planX = _homeX;
            _planY = _homeY;
            _planHeading = 0;
            Enqueue(new Command(Kind.Home, 0, default, Block));
        }

        private void Enqueue(Command command)
        {
            _queue.Enqueue(command);
            Interlocked.Increment(ref _pending);
            Block = -1; // a block id belongs to one command
        }

        // =================================================================
        // Carrying them out
        // =================================================================

        /// <summary>
        /// Moves the turtle on by the time since the last call, then draws its
        /// lines and the turtle. Call it every frame from Draw(), after
        /// Background() (the lines are redrawn each time).
        /// </summary>
        public void Draw()
        {
            if (_sketch is null)
                return;

            long now = _sketch.Millis();
            float seconds = _lastMillis < 0 ? 0 : Math.Clamp((now - _lastMillis) / 1000f, 0, 0.1f);
            _lastMillis = now;
            Advance(seconds);
            Render(_sketch);
        }

        /// <summary>Carries out everything still queued, instantly.</summary>
        public void Finish() => Advance(float.PositiveInfinity);

        private void Advance(float seconds)
        {
            float factor = Speed * SpeedFactor;
            bool instant = factor <= 0 || float.IsPositiveInfinity(seconds);

            // Walking and turning use up time; everything else is instant.
            // A loop, not one step per frame: a fast turtle can finish several
            // short commands within one frame.
            while (true)
            {
                if (_current is null)
                {
                    if (_queue.Count == 0)
                        return;
                    // Step by step: a block's command waits for its click
                    // (never when finishing everything at once).
                    if (!instant && _queue.Peek().Block >= 0 && Stepper is { } stepper && !stepper.TryTake(_queue.Peek().Block))
                        return;
                    var next = _queue.Dequeue();
                    _current = next;
                    _remaining = Math.Abs(next.Amount);
                    if (next.Block >= 0)
                        _sketch?.BlockHit(next.Block);
                }

                var cmd = _current.Value;
                switch (cmd.Kind)
                {
                    case Kind.Move:
                    {
                        float budget = instant ? _remaining : seconds * BaseStepsPerSecond * factor;
                        float step = Math.Min(budget, _remaining);
                        float sign = Math.Sign(cmd.Amount);
                        float rad = _heading * MathF.PI / 180f;
                        float nx = _x + MathF.Sin(rad) * step * sign;
                        float ny = _y - MathF.Cos(rad) * step * sign;
                        if (_penDown && step > 0)
                            AddLine(_x, _y, nx, ny);
                        _x = nx;
                        _y = ny;
                        _remaining -= step;
                        if (!instant)
                            seconds -= step / (BaseStepsPerSecond * factor);
                        break;
                    }
                    case Kind.Turn:
                    {
                        float budget = instant ? _remaining : seconds * BaseDegreesPerSecond * factor;
                        float step = Math.Min(budget, _remaining);
                        _heading = Normalize(_heading + step * Math.Sign(cmd.Amount));
                        _remaining -= step;
                        if (!instant)
                            seconds -= step / (BaseDegreesPerSecond * factor);
                        break;
                    }
                    case Kind.Pen: _penDown = cmd.Amount > 0; _remaining = 0; break;
                    case Kind.PenColor: _penColor = cmd.Color; _remaining = 0; break;
                    case Kind.PenSize: _penSize = cmd.Amount; _remaining = 0; break;
                    case Kind.Home: _x = _homeX; _y = _homeY; _heading = 0; _remaining = 0; break;
                }

                if (_remaining > 1e-4f)
                    return; // out of time this frame, mid-command

                // Snap to the exact planned end, so rounding never adds up.
                if (cmd.Kind == Kind.Turn)
                    _heading = MathF.Round(_heading * 1000) / 1000;
                _current = null;
                Interlocked.Decrement(ref _pending);
                if (!instant && seconds <= 0)
                    return;
            }
        }

        /// <summary>
        /// Adds a line, joining it to the previous one when it simply continues
        /// it (same direction, color and size): a line walked over several
        /// frames is still ONE line.
        /// </summary>
        private void AddLine(float x1, float y1, float x2, float y2)
        {
            if (_lines.Count > 0 && _current is { } cmd)
            {
                var last = _lines[^1];
                bool continues = MathF.Abs(last.X2 - x1) < 1e-3f && MathF.Abs(last.Y2 - y1) < 1e-3f
                                 && last.Color.Equals(_penColor) && last.Width == _penSize
                                 && _remaining < MathF.Abs(cmd.Amount); // same command, already started
                if (continues)
                {
                    lock (_lines)
                        _lines[^1] = last with { X2 = x2, Y2 = y2 };
                    return;
                }
            }
            lock (_lines)
                _lines.Add(new TurtleLine(x1, y1, x2, y2, _penColor, _penSize));
        }

        private void Render(Sketch s)
        {
            s.PushStyle();
            s.PushMatrix();
            try
            {
                s.StrokeCap(StrokeCapKind.Round);
                foreach (var line in _lines)
                {
                    s.Stroke(line.Color);
                    s.StrokeWeight(line.Width);
                    s.Line(line.X1, line.Y1, line.X2, line.Y2);
                }
                if (Visible)
                    DrawTurtle(s);
            }
            finally
            {
                s.PopMatrix();
                s.PopStyle();
            }
        }

        /// <summary>A friendly little green turtle seen from above, nose pointing where it's going.</summary>
        private void DrawTurtle(Sketch s)
        {
            s.Translate(_x, _y);
            s.Rotate(_heading * MathF.PI / 180f);
            s.EllipseMode(ShapeAlignMode.Center);
            s.Stroke(new Color(40, 90, 50));
            s.StrokeWeight(1.5f);

            // legs
            s.Fill(new Color(120, 190, 120));
            s.Ellipse(-11, -8, 9, 7);
            s.Ellipse(11, -8, 9, 7);
            s.Ellipse(-11, 9, 9, 7);
            s.Ellipse(11, 9, 9, 7);
            // head (in front: negative y is "forward" when heading 0 = up)
            s.Ellipse(0, -17, 11, 12);
            // shell
            s.Fill(new Color(70, 150, 90));
            s.Ellipse(0, 0, 24, 28);
            s.Fill(new Color(100, 175, 110));
            s.Ellipse(0, 0, 13, 15);
            // eyes
            s.NoStroke();
            s.Fill(new Color(30, 30, 30));
            s.Ellipse(-3, -19, 2.5f, 2.5f);
            s.Ellipse(3, -19, 2.5f, 2.5f);
        }

        private static float Normalize(float degrees)
        {
            degrees %= 360f;
            return degrees < 0 ? degrees + 360f : degrees;
        }
    }
}
