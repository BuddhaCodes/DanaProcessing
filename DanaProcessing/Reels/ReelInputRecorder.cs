using System.Diagnostics;
using System.Globalization;

namespace DanaProcessing.Reels
{
    public enum RecordedInputKind { Move, Down, Up, Key }

    /// <summary>One thing you did to the running sketch, <see cref="T"/> seconds after recording started.</summary>
    public sealed record RecordedInput(double T, RecordedInputKind Kind, float X, float Y, string? Key = null);

    /// <summary>
    /// Records how you play with a running sketch -- mouse paths, clicks, drags,
    /// keys -- and turns it into the <c>// @reel move/drag/click/key</c> markers
    /// the reel's result part already understands (see ReelScript.ParseInput /
    /// InputSimulator), so a reel replays what YOU did instead of an invented
    /// mouse path. Time 0 of the recording becomes time 0 of the result part.
    ///
    /// The host feeds it (AvaloniaSketchCanvas does, while recording);
    /// <see cref="ToMarkers(IReadOnlyList{RecordedInput})"/> is the pure part.
    /// </summary>
    public sealed class ReelInputRecorder
    {
        private readonly object _lock = new();
        private readonly List<RecordedInput> _events = new();
        private readonly Stopwatch _clock = new();
        private (double T, float X, float Y)? _lastMove;

        /// <summary>Stops taking events after this long; a reel's result part is short anyway.</summary>
        public double MaxSeconds { get; set; } = 30;

        public bool IsRecording => _clock.IsRunning;
        public double Elapsed => _clock.Elapsed.TotalSeconds;

        public void Start()
        {
            lock (_lock)
            {
                _events.Clear();
                _lastMove = null;
                _clock.Restart();
            }
        }

        /// <summary>Stops and returns everything recorded.</summary>
        public IReadOnlyList<RecordedInput> Stop()
        {
            lock (_lock)
            {
                _clock.Stop();
                return _events.ToList();
            }
        }

        public void Move(float x, float y)
        {
            double t = Elapsed;
            lock (_lock)
            {
                if (!Accepting(t))
                    return;
                // Mouse moves arrive far faster than a reel needs; keep one every
                // ~1/30 s unless the pointer jumped.
                if (_lastMove is { } last && t - last.T < 1.0 / 30 && Math.Abs(x - last.X) + Math.Abs(y - last.Y) < 6)
                    return;
                _lastMove = (t, x, y);
                _events.Add(new RecordedInput(t, RecordedInputKind.Move, x, y));
            }
        }

        public void Down(float x, float y) => Add(RecordedInputKind.Down, x, y, null);
        public void Up(float x, float y) => Add(RecordedInputKind.Up, x, y, null);

        /// <summary>A key, by the name a <c>// @reel key</c> marker uses ("space", "a", "1"...).</summary>
        public void Key(string name) => Add(RecordedInputKind.Key, 0, 0, name);

        private void Add(RecordedInputKind kind, float x, float y, string? key)
        {
            double t = Elapsed;
            lock (_lock)
            {
                if (Accepting(t))
                    _events.Add(new RecordedInput(t, kind, x, y, key));
            }
        }

        private bool Accepting(double t) => _clock.IsRunning && t <= MaxSeconds;

        // ---------------------------------------------------------------
        // Events -> markers
        // ---------------------------------------------------------------

        private const int MaxPointsPerPath = 14;
        private const float ClickTolerance = 4f;

        /// <summary>
        /// Turns recorded input into marker lines, in time order: pointer paths
        /// with no button become <c>move</c>, held-button paths become
        /// <c>drag</c> (or <c>click</c> if the pointer barely moved), keys
        /// become <c>key</c>.
        /// </summary>
        public static IReadOnlyList<string> ToMarkers(IReadOnlyList<RecordedInput> events)
        {
            var markers = new List<(double At, string Line)>();
            var path = new List<RecordedInput>();
            bool down = false;

            foreach (var e in events.OrderBy(e => e.T))
            {
                switch (e.Kind)
                {
                    case RecordedInputKind.Move:
                        path.Add(e);
                        break;

                    case RecordedInputKind.Down:
                        if (!down)
                            FlushMove(path, markers);
                        path.Clear();
                        path.Add(e);
                        down = true;
                        break;

                    case RecordedInputKind.Up:
                        if (down)
                        {
                            path.Add(e);
                            FlushPress(path, markers);
                            path.Clear();
                            path.Add(e); // the next move starts where the button came up
                            down = false;
                        }
                        break;

                    case RecordedInputKind.Key when !string.IsNullOrWhiteSpace(e.Key):
                        markers.Add((e.T, $"// @reel key {e.Key} at={Num(e.T)}"));
                        break;
                }
            }

            if (down)
                FlushPress(path, markers);
            else
                FlushMove(path, markers);

            return markers.OrderBy(m => m.At).Select(m => m.Line).ToList();
        }

        private static void FlushMove(List<RecordedInput> path, List<(double, string)> markers)
        {
            if (path.Count < 2 || Extent(path) < ClickTolerance)
                return;
            markers.Add((path[0].T, PathMarker("move", path)));
        }

        private static void FlushPress(List<RecordedInput> path, List<(double, string)> markers)
        {
            if (path.Count == 0)
                return;
            if (path.Count < 2 || Extent(path) < ClickTolerance)
                markers.Add((path[0].T, $"// @reel click {Point(path[0])} at={Num(path[0].T)}"));
            else
                markers.Add((path[0].T, PathMarker("drag", path)));
        }

        private static string PathMarker(string command, List<RecordedInput> path)
        {
            var points = Thin(path);
            double duration = Math.Max(0.05, path[^1].T - path[0].T);
            return $"// @reel {command} {string.Join(" -> ", points.Select(Point))} at={Num(path[0].T)} dur={Num(duration)}";
        }

        /// <summary>At most MaxPointsPerPath points, evenly spread, always keeping the first and last.</summary>
        private static List<RecordedInput> Thin(List<RecordedInput> path)
        {
            if (path.Count <= MaxPointsPerPath)
                return path;
            var thinned = new List<RecordedInput>();
            for (int i = 0; i < MaxPointsPerPath; i++)
                thinned.Add(path[(int)Math.Round(i * (path.Count - 1) / (double)(MaxPointsPerPath - 1))]);
            return thinned;
        }

        private static float Extent(List<RecordedInput> path)
        {
            float minX = path.Min(p => p.X), maxX = path.Max(p => p.X);
            float minY = path.Min(p => p.Y), maxY = path.Max(p => p.Y);
            return Math.Max(maxX - minX, maxY - minY);
        }

        private static string Point(RecordedInput e) =>
            $"{(int)Math.Round(e.X)},{(int)Math.Round(e.Y)}";

        private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
