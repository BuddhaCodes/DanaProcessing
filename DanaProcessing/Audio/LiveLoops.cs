using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DanaProcessing
{
    /// <summary>
    /// Marks a sketch method as a live loop, like Sonic Pi's live_loop: it
    /// repeats forever, in time with every other loop, and keeps playing
    /// while you edit -- press Run again and each loop picks up its new code
    /// on its next pass, without stopping the music. Remove the method (or
    /// its attribute) and that loop stops at the end of its current pass.
    ///
    /// The method must be `async Task Name()` with no parameters, and every
    /// pass must `await Sleep(beats)` at least once:
    /// <code>
    /// [LiveLoop]
    /// async Task Drums()
    /// {
    ///     Sample("kick");
    ///     await Sleep(1);
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class LiveLoopAttribute : Attribute
    {
    }
}

namespace DanaProcessing.Audio
{
    /// <summary>
    /// Per-loop musical state that flows through the loop's async calls via
    /// AsyncLocal: its logical time (where in the music this loop is, which
    /// runs ScheduleAhead ahead of what you hear) and its current synth.
    /// </summary>
    internal sealed class LoopContext
    {
        public required string Name { get; init; }
        public double Time;
        public Synth Synth = Synth.Sine;
        public CancellationToken Token;
        public double LastLateReport = double.NegativeInfinity;
    }

    /// <summary>
    /// Runs the sketch's [LiveLoop] methods. Each loop is an async task that
    /// re-resolves its method on the CURRENT sketch instance every pass --
    /// that's what makes both Run and Hot Reload swap code seamlessly: the
    /// loop itself (and its place in time) belongs to the engine, only the
    /// body comes from the sketch.
    ///
    /// Timing follows Sonic Pi's model: Sleep() advances the loop's logical
    /// time exactly, then waits (roughly) until the engine clock reaches it.
    /// Every sound is stamped with logical time + ScheduleAhead, so however
    /// late the thread pool wakes the loop up, the sound still lands on the
    /// exact sample it should.
    /// </summary>
    public sealed class LiveLoopRunner
    {
        internal static readonly AsyncLocal<LoopContext?> Current = new();

        private sealed class LoopState
        {
            public required LoopContext Context { get; init; }
            public required CancellationTokenSource Cts { get; init; }
            public volatile bool StopAfterPass;
        }

        private readonly AudioEngine _engine;
        private readonly object _gate = new();
        private Sketch? _active;
        private Dictionary<string, MethodInfo> _methods = new();
        private readonly Dictionary<string, LoopState> _running = new();

        internal LiveLoopRunner(AudioEngine engine) => _engine = engine;

        /// <summary>Names of the loops currently running.</summary>
        public IReadOnlyList<string> RunningLoops
        {
            get
            {
                lock (_gate)
                    return _running.Keys.ToArray();
            }
        }

        /// <summary>
        /// Every valid [LiveLoop] method on <paramref name="type"/>, by name.
        /// Invalid ones (parameters, wrong return type) are reported, not thrown.
        /// </summary>
        internal static Dictionary<string, MethodInfo> FindLoops(Type type)
        {
            var result = new Dictionary<string, MethodInfo>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var method in type.GetMethods(flags))
            {
                if (method.GetCustomAttribute<LiveLoopAttribute>() == null)
                    continue;

                if (method.GetParameters().Length != 0 || !typeof(Task).IsAssignableFrom(method.ReturnType) || method.IsGenericMethodDefinition)
                {
                    AudioEngine.Report($"[LiveLoop] {method.Name}: tiene que ser `async Task {method.Name}()` sin parámetros. Este loop no se va a ejecutar.");
                    continue;
                }

                result[method.Name] = method;
            }

            return result;
        }

        /// <summary>
        /// Makes <paramref name="sketch"/> the one whose code the loops run:
        /// starts loops that are new, keeps the ones that already exist
        /// (they'll run the new code on their next pass), and lets loops that
        /// no longer exist finish their current pass and stop.
        /// </summary>
        internal void Activate(Sketch sketch, Dictionary<string, MethodInfo> methods)
        {
            lock (_gate)
            {
                _active = sketch;
                _methods = methods;

                foreach (var (name, state) in _running)
                    state.StopAfterPass = !methods.ContainsKey(name);

                if (methods.Count == 0)
                    return;

                _engine.EnsureStarted();
                double start = NextStartTime();

                foreach (var name in methods.Keys)
                {
                    if (_running.ContainsKey(name))
                        continue;

                    var cts = new CancellationTokenSource();
                    var state = new LoopState
                    {
                        Context = new LoopContext { Name = name, Time = start, Token = cts.Token },
                        Cts = cts,
                    };
                    _running[name] = state;
                    _ = Task.Run(() => RunAsync(state));
                }
            }
        }

        /// <summary>Cancels every loop right away (pending Sleeps are interrupted).</summary>
        internal void StopAll()
        {
            lock (_gate)
            {
                foreach (var state in _running.Values)
                    state.Cts.Cancel();
                _running.Clear();
            }
        }

        /// <summary>
        /// When a newly added loop should start: on the next beat of the loops
        /// already playing (so it comes in in time with them), or a hair from
        /// now if nothing is playing yet. Called under _gate.
        /// </summary>
        private double NextStartTime()
        {
            double earliest = _engine.Now + 0.05;
            var reference = _running.Values.FirstOrDefault(s => !s.StopAfterPass);
            if (reference == null)
                return earliest;

            double beat = 60.0 / _engine.Bpm;
            double refTime = reference.Context.Time;
            if (refTime >= earliest)
                return refTime;
            return refTime + Math.Ceiling((earliest - refTime) / beat) * beat;
        }

        private async Task RunAsync(LoopState state)
        {
            var ctx = state.Context;
            Current.Value = ctx;

            try
            {
                await WaitUntil(ctx);

                while (!state.Cts.IsCancellationRequested && !state.StopAfterPass)
                {
                    Sketch? sketch;
                    MethodInfo? method;
                    lock (_gate)
                    {
                        sketch = _active;
                        _methods.TryGetValue(ctx.Name, out method);
                    }
                    if (sketch == null || method == null)
                        break;

                    double before = ctx.Time;

                    Task? pass;
                    try
                    {
                        pass = method.Invoke(sketch, null) as Task;
                    }
                    catch (TargetInvocationException tie) when (tie.InnerException != null)
                    {
                        throw tie.InnerException;
                    }

                    if (pass != null)
                        await pass;

                    if (ctx.Time <= before)
                    {
                        AudioEngine.Report($"Live loop '{ctx.Name}' terminó una vuelta sin llamar a `await Sleep(...)`. Se detuvo para no colgar el audio.");
                        break;
                    }

                    // Normally a no-op (the last Sleep already waited). Throttles
                    // a loop that called Sleep() without `await`, which would
                    // otherwise spin as fast as the CPU allows.
                    await WaitUntil(ctx);
                }
            }
            catch (OperationCanceledException) when (state.Cts.IsCancellationRequested)
            {
                // Stopped on purpose (Stop button / StopAudio()).
            }
            catch (Exception ex)
            {
                AudioEngine.Report($"Live loop '{ctx.Name}' se detuvo por un error: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                lock (_gate)
                {
                    if (_running.TryGetValue(ctx.Name, out var current) && current == state)
                        _running.Remove(ctx.Name);
                }
            }
        }

        /// <summary>
        /// Sonic Pi's sleep: inside a live loop, advances the loop's logical
        /// time by <paramref name="beats"/> (at the current Bpm) and waits for
        /// the clock to catch up. Outside a loop it's a plain real-time delay.
        /// </summary>
        internal Task Sleep(double beats)
        {
            if (beats < 0 || double.IsNaN(beats))
                throw new ArgumentOutOfRangeException(nameof(beats), "Sleep(): los beats tienen que ser 0 o más.");

            double seconds = beats * 60.0 / _engine.Bpm;
            var ctx = Current.Value;
            if (ctx == null)
                return Task.Delay(TimeSpan.FromSeconds(seconds));

            ctx.Time += seconds;
            return WaitUntil(ctx);
        }

        private Task WaitUntil(LoopContext ctx)
        {
            double now = _engine.Now;
            double wait = ctx.Time - now;

            if (wait > 0.001)
                return Task.Delay(TimeSpan.FromSeconds(wait), ctx.Token);

            if (wait < -_engine.ScheduleAhead)
            {
                // Behind by more than the cushion: these notes will sound late.
                if (now - ctx.LastLateReport > 2.0)
                {
                    ctx.LastLateReport = now;
                    AudioEngine.Report($"Live loop '{ctx.Name}' va atrasado {(-wait * 1000):0} ms: el código de cada vuelta tarda demasiado.");
                }
                // Hopelessly behind (e.g. a breakpoint): jump back onto the clock.
                if (wait < -1.0)
                    ctx.Time = now;
            }

            ctx.Token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
