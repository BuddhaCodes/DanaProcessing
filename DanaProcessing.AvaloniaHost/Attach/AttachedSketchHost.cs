using System.Collections.Concurrent;
using System.Diagnostics;
using DanaProcessing;
using DanaProcessing.Audio;
using SkiaSharp;

namespace DanaProcessing.AvaloniaHost.Attach
{
    /// <summary>What an <see cref="AttachedSketchHost"/> is doing right now.</summary>
    public enum AttachState
    {
        Starting,
        /// <summary>No matching window is open; looking again twice a second.</summary>
        Searching,
        /// <summary>On the target and drawing.</summary>
        Attached,
        /// <summary>The target is minimized, hidden, on another desktop or too small: nothing drawn.</summary>
        TargetHidden,
        /// <summary>The sketch threw; it stays stopped until the next Run or Hot Reload.</summary>
        Crashed,
        Stopped,
        /// <summary>Paused by the IDE or the tray menu: nothing drawn, nothing read.</summary>
        Paused,
        /// <summary>The window this sketch was pinned to (AttachMode.EachWindow) closed; the host has stopped.</summary>
        Closed,
        /// <summary>Not on Windows.</summary>
        Unsupported,
    }

    /// <summary>
    /// Runs an <see cref="AttachedSketch"/> on top of (or behind) another app's
    /// window instead of in a canvas of its own.
    ///
    /// Everything happens on one background thread with its own message loop:
    /// find the target, read where it is, resize the surface if it changed,
    /// run Settings()/Setup() once and RenderFrame() every frame, then hand
    /// the pixels to a layered window placed exactly over the target. The
    /// IDE's UI thread only ever touches the sketch through HotReload()/Stop(),
    /// under the same lock as a frame -- the same guarantee AvaloniaSketchCanvas
    /// gives (never in the middle of a Draw()).
    ///
    /// Windows only (see IsSupported).
    /// </summary>
    public sealed class AttachedSketchHost : IDisposable, IShellSignalListener
    {
        /// <summary>Attached sketches need Win32 today.</summary>
        public static bool IsSupported => OperatingSystem.IsWindows();

        /// <summary>How often a missing target is looked for again, in seconds.</summary>
        private const double ScanInterval = 0.5;

        private readonly object _sketchLock = new();
        private readonly bool _connectsAudio;
        private AttachedSketch _sketch;
        private Thread? _thread;
        private volatile bool _stopRequested;
        private volatile bool _redrawRequested;

        private volatile AttachState _state = AttachState.Starting;
        private volatile string _status = "";

        // Host thread only (or under _sketchLock).
        private IOverlayWindow? _window;
        private IntPtr _target;
        private int _attachVersion = -1;
        private double _lastScan = double.NegativeInfinity;
        private bool _didSetup;
        private bool _crashed;
        private double _lastTick = -1;
        private InputHooks? _hooks;
        private string? _hooksError;
        private readonly Action<Action, string> _run;
        private readonly Stopwatch _clock = new();

        // AttachMode.EachWindow: the one window this host draws on (no scanning).
        private readonly IntPtr _pinned;
        private readonly bool _settingsAlreadyRun;

        // Eco mode, pause, time.
        private volatile bool _paused;
        private volatile int _frameRate = 60;
        private double _lastActivity, _lastDraw = -1, _lastPowerCheck = double.NegativeInfinity;
        private bool _lowBattery, _wasFocused;
        private (float X, float Y)? _lastCursor;

        // Shell signals (ShellSignalHub delivers on a pool thread; Tick drains).
        private readonly ConcurrentQueue<(ShellSignal Signal, DateTime Written)> _signals = new();

        // Event-driven waking: anything that changes (a key, a shell signal, the
        // target moving, the foreground changing) sets _wake and the frame loop
        // runs a frame at once instead of sleeping until the next one.
        private readonly AutoResetEvent _wake = new(false);
        private volatile bool _wakeRequested;
        private WinEventHooks? _winEvents; // host thread only
        // A drag reports the target's position hundreds of times a second. Each
        // report only marks a move as pending; the loop applies the latest one
        // at most every FrameScheduler.FastMoveSpacing. Moving the overlay right
        // in each callback (one DWM query + SetWindowPos each) starved the loop:
        // the message queue never emptied, so no frame ran while dragging.
        private bool _movePending;    // host thread only
        private uint _movePendingTime;
        private double _lastFastMove = double.NegativeInfinity;
        private readonly StatsMeter _meter = new();
        private readonly object _statsLock = new();
        private AttachedHostStats _stats = AttachedHostStats.None;
        private bool _shellSubscribed;
        private volatile IntPtr _targetHandle;
        private double _lastFocusedAt;

        // What the IDE's status card shows as badges.
        private volatile bool _usesKeyboard, _usesMouse, _usesShell, _isEco;

        /// <param name="sketch">The sketch to run. Settings() and Setup() haven't run yet.</param>
        /// <param name="connectsAudio">Same meaning as AvaloniaSketchCanvas.ConnectsAudio.</param>
        /// <summary>
        /// For AttachedSketchFleet: a host pinned to one window (it never looks
        /// for another, and stops when that one closes), for a sketch whose
        /// Settings() already ran.
        /// </summary>
        internal AttachedSketchHost(AttachedSketch sketch, bool connectsAudio, IntPtr pinnedTarget, bool settingsAlreadyRun)
            : this(sketch, connectsAudio)
        {
            _pinned = pinnedTarget;
            _settingsAlreadyRun = settingsAlreadyRun;
        }

        public AttachedSketchHost(AttachedSketch sketch, bool connectsAudio = true)
        {
            _sketch = sketch ?? throw new ArgumentNullException(nameof(sketch));
            _connectsAudio = connectsAudio;
            _run = RunSafely;
            Hook(sketch);
        }

        /// <summary>Current state; read from any thread.</summary>
        public AttachState State => _state;

        /// <summary>One line describing the state, in Spanish, for the IDE's placeholder.</summary>
        public string StatusText => _status;

        /// <summary>The sketch currently running (changes with HotReload).</summary>
        public AttachedSketch Sketch => _sketch;

        /// <summary>Raised on the host thread whenever State or StatusText changes.</summary>
        public event Action? StatusChanged;

        /// <summary>Raised on the host thread when the sketch throws (like AvaloniaSketchCanvas.Crashed).</summary>
        public event Action<Exception, string>? Crashed;

        /// <summary>Raised on the host thread when the sketch calls Exit(). The host has already stopped itself.</summary>
        public event Action? ExitRequested;

        /// <summary>Raised on the host thread when the pinned window closes (AttachMode.EachWindow). The host has already stopped itself.</summary>
        public event Action? TargetClosed;

        /// <summary>Pauses the sketch: its overlay hides and nothing runs (not even events) until set back to false.</summary>
        /// <summary>
        /// Painted under the sketch every frame (null: nothing, the default).
        /// An export behind a translucent terminal profile uses it so the
        /// terminal's text reads well over the sketch. Set before Start().
        /// </summary>
        public SKColor? Backdrop { get; init; }

        /// <summary>
        /// The title the terminal window has while this sketch's own profile
        /// is the tab in front (see TitleGate); null: draw whatever tab is in front.
        /// </summary>
        public string? ProfileTitle { get; init; }

        /// <summary>
        /// One Windows Terminal pane instead of the whole window (see
        /// TerminalPanes): the sketch covers just that pane, and hides while the
        /// pane is out of sight (another tab). Measured by the fleet's scanner.
        /// </summary>
        internal PaneSlot? Pane { get; init; }

        /// <summary>
        /// Put frames on screen through the GPU (DirectComposition + a swap
        /// chain, see DCompWindow) instead of UpdateLayeredWindow. If that
        /// can't start, or fails later, the host goes back to the CPU presenter
        /// by itself (see GpuFallback). Set before Start().
        /// </summary>
        public bool UseGpu { get; init; }

        /// <summary>"GPU" or "CPU": how frames reach the screen right now.</summary>
        public string Presenter => _window?.Presenter ?? (UseGpu ? "GPU" : "CPU");

        /// <summary>Why the GPU presenter isn't in use although UseGpu asked for it; null if it is (or wasn't asked for).</summary>
        public string? GpuFallback { get; private set; }

        private readonly VBlankClock _vblank = new();
        private double _lastCoverCheck = double.NegativeInfinity;
        private bool _covered;
        private double _interval = 1 / 60.0;

        public bool IsPaused
        {
            get => _paused;
            set => _paused = value;
        }

        /// <summary>The window the sketch is on, or zero.</summary>
        public IntPtr TargetHandle => _targetHandle;

        /// <summary>The window it was pinned to (AttachMode.EachWindow), or zero.</summary>
        public IntPtr PinnedTarget => _pinned;

        /// <summary>The sketch has the keyboard hook on (ListenToTargetKeys).</summary>
        public bool UsesKeyboardHook => _usesKeyboard;

        /// <summary>The sketch has the mouse hook on (ListenToTargetMouse).</summary>
        public bool UsesMouseHook => _usesMouse;

        /// <summary>The sketch listens to shell signals (ListenToShell).</summary>
        public bool UsesShellSignals => _usesShell;

        /// <summary>Eco mode is slowing the sketch down right now.</summary>
        public bool IsEco => _isEco;

        /// <summary>The frame rate the host is running at right now (eco and hidden states lower it).</summary>
        public int CurrentFrameRate => _frameRate;

        double IShellSignalListener.LastFocusedAt => _lastFocusedAt;

        double IShellSignalListener.PaneFocusedAt => Pane?.FocusedAt ?? 0;

        void IShellSignalListener.Deliver(ShellSignal signal, DateTime writtenUtc)
        {
            _signals.Enqueue((signal, writtenUtc));
            Wake();
        }

        /// <summary>Frames per second, what a frame costs and how fast events arrive (see AttachedHostStats).</summary>
        public AttachedHostStats Stats
        {
            get { lock (_statsLock) return _stats; }
        }

        /// <summary>Runs a frame as soon as possible (from any thread): something happened.</summary>
        internal void Wake()
        {
            _wakeRequested = true;
            try { _wake.Set(); } catch (ObjectDisposedException) { }
        }

        /// <summary>Starts the host thread. Does nothing but report Unsupported off Windows.</summary>
        public void Start()
        {
            if (_thread != null)
                return;
            if (!IsSupported)
            {
                SetStatus(AttachState.Unsupported, UiText.Tr("Los sketches adjuntos solo funcionan en Windows por ahora.", "Attached sketches only work on Windows for now."));
                return;
            }
            _thread = new Thread(Run) { IsBackground = true, Name = "DanaProcessing attached sketch" };
            _thread.Start();
        }

        /// <summary>
        /// Swaps in <paramref name="newSketch"/> keeping every field's value, like
        /// AvaloniaSketchCanvas.HotReloadSketch. Setup() does not run again.
        /// Returns a plan with CanApply = false (and changes nothing) when the
        /// fields don't line up; the caller then restarts with a new host.
        /// </summary>
        public SketchHotReload.Plan HotReload(AttachedSketch newSketch)
        {
            ArgumentNullException.ThrowIfNull(newSketch);
            if (_stopRequested || _thread == null)
                return new SketchHotReload.Plan(false, new[] { UiText.Tr("el sketch adjunto estaba detenido", "the attached sketch was stopped") });

            lock (_sketchLock)
            {
                var plan = SketchHotReload.Analyze(_sketch, newSketch);
                if (!plan.CanApply)
                    return plan;

                SketchHotReload.Transplant(_sketch, newSketch);
                Unhook(_sketch);
                _sketch = newSketch;
                Hook(newSketch);
                _crashed = false;

                var surface = _window?.Surface;
                if (surface != null)
                    newSketch.SetCanvas(surface.Canvas, surface);

                if (_connectsAudio)
                    AudioEngine.ActivateSketch(newSketch);
                return plan;
            }
        }

        /// <summary>
        /// Stops the host thread and closes the overlay, waiting up to two
        /// seconds for the current frame to finish. Does NOT run the sketch's
        /// OnStop() -- the owner does that, the same way for every host.
        /// </summary>
        public void Stop()
        {
            _stopRequested = true;
            Wake(); // don't wait out a frame to notice
            var thread = _thread;
            if (thread != null && thread != Thread.CurrentThread)
                thread.Join(TimeSpan.FromSeconds(2));
        }

        public void Dispose() => Stop();

        private void Hook(AttachedSketch sketch)
        {
            sketch.RedrawRequested += OnRedrawRequested;
            sketch.ExitRequested += OnExitRequested;
            sketch.IgnoresAudio = !_connectsAudio;
        }

        private void Unhook(AttachedSketch sketch)
        {
            sketch.RedrawRequested -= OnRedrawRequested;
            sketch.ExitRequested -= OnExitRequested;
        }

        private void OnRedrawRequested() => _redrawRequested = true;

        private void OnExitRequested()
        {
            _stopRequested = true;
            try { ExitRequested?.Invoke(); } catch (Exception ex) { DanaLogger.ErrorFromException(ex, "ExitRequested"); }
        }

        // ------------------------------------------------------------ host thread

        private void Run()
        {
            try { Win32.SetThreadDpiAwarenessContext(Win32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
            catch { /* older Windows: the process-wide setting applies */ }

            bool fineTimer = false;
            try { fineTimer = Win32.timeBeginPeriod(1) == 0; } catch { }

            try
            {
                lock (_sketchLock)
                {
                    _window = CreateWindow();
                    // Before the target is looked for, so Attach() in Settings() decides it.
                    if (!_settingsAlreadyRun)
                        RunSafely(_sketch.Settings, "Settings");
                }

                _clock.Start();
                _winEvents = new WinEventHooks(OnWinEvent) { WatchTitle = ProfileTitle != null };
                var handles = new[] { _wake.SafeWaitHandle.DangerousGetHandle() };
                double next = 0;
                while (!_stopRequested)
                {
                    PumpMessages(); // also delivers the WinEvents (OnWinEvent); bounded, see there

                    double now = _clock.Elapsed.TotalSeconds;
                    if (FrameScheduler.ShouldFastMove(_movePending, now, _lastFastMove))
                    {
                        _lastFastMove = now;
                        FastMove();
                    }
                    double interval = 1.0 / Math.Clamp(_frameRate, 1, 240);
                    // On the screen's refresh grid when DWM can tell (see VBlankClock).
                    bool synced = _vblank.TryGet(_clock, out double vblank, out double period);
                    if (synced)
                        interval = FrameScheduler.SnapToRefresh(interval, period);
                    _interval = interval;
                    bool woken = _wakeRequested;
                    var (run, newNext) = FrameScheduler.Decide(now, next, interval, woken, _lastTick);
                    if (synced)
                        newNext = FrameScheduler.AlignToRefresh(newNext, vblank, period);
                    if (!run)
                    {
                        // Sleep until the next frame, a wake, or a window message -- whichever comes first.
                        Win32.MsgWaitForMultipleObjectsEx(1, handles, FrameScheduler.WaitMilliseconds(now, next, woken, _movePending),
                            Win32.QS_ALLINPUT, Win32.MWMO_INPUTAVAILABLE);
                        continue;
                    }
                    _wakeRequested = false;
                    next = newNext;

                    lock (_sketchLock)
                        Tick(now);
                }
            }
            catch (Exception ex)
            {
                DanaLogger.ErrorFromException(ex, "Host de sketch adjunto");
                SetStatus(AttachState.Crashed, UiText.Tr("El host del sketch adjunto falló: ", "The attached sketch's host failed: ") + ex.Message);
            }
            finally
            {
                if (_shellSubscribed)
                    ShellSignalHub.Unsubscribe(this);
                _shellSubscribed = false;
                _winEvents?.Dispose();
                _winEvents = null;
                lock (_sketchLock)
                {
                    _hooks?.Dispose();
                    _hooks = null;
                    _window?.Dispose();
                    _window = null;
                    _sketch.IsAttached = false;
                }
                if (fineTimer)
                    try { Win32.timeEndPeriod(1); } catch { }
                if (_state is not (AttachState.Crashed or AttachState.Closed))
                    SetStatus(AttachState.Stopped, UiText.Tr("Detenido.", "Stopped."));
            }
        }

        /// <summary>At most this many messages per pump, so a flood of WinEvents can never keep frames from running.</summary>
        private const int MaxMessagesPerPump = 64;

        private static void PumpMessages()
        {
            for (int i = 0; i < MaxMessagesPerPump && Win32.PeekMessage(out var msg, IntPtr.Zero, 0, 0, Win32.PM_REMOVE); i++)
            {
                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessage(ref msg);
            }
        }

        /// <summary>One frame. Called under _sketchLock.</summary>
        private void Tick(double now)
        {
            var window = _window!;
            var sketch = _sketch;
            double dt = _lastTick < 0 ? 1.0 / 60 : now - _lastTick;
            _lastTick = now;

            if (now - _lastPowerCheck >= 10)
            {
                _lastPowerCheck = now;
                _lowBattery = Power.IsLowBattery();
            }

            if (_crashed)
            {
                window.Hide();
                return;
            }

            if (_paused)
            {
                window.Hide();
                _hooks?.Update(IntPtr.Zero, null);
                _frameRate = 10; // only to notice the resume quickly
                _lastDraw = -1;  // no huge DeltaTime when it comes back
                SetStatus(AttachState.Paused, UiText.Tr("En pausa.", "Paused."));
                return;
            }

            // Pinned to one window (AttachMode.EachWindow): no looking around.
            if (_pinned != IntPtr.Zero)
            {
                if (!WindowTracker.Exists(_pinned))
                {
                    Detach(sketch, dt);
                    SetStatus(AttachState.Closed, UiText.Tr("La ventana se cerró.", "The window was closed."));
                    _stopRequested = true;
                    try { TargetClosed?.Invoke(); } catch (Exception ex) { DanaLogger.ErrorFromException(ex, "TargetClosed"); }
                    return;
                }
                _target = _pinned;
                _attachVersion = sketch.AttachVersion;
            }

            // Attach() was called again (in Setup(), Draw(), an event...): look again.
            if (sketch.AttachVersion != _attachVersion)
            {
                _attachVersion = sketch.AttachVersion;
                _target = IntPtr.Zero;
                _lastScan = double.NegativeInfinity;
            }

            // 1. Find the target (or notice it closed).
            if (!WindowTracker.Exists(_target))
            {
                _target = IntPtr.Zero;
                if (now - _lastScan >= ScanInterval)
                {
                    _lastScan = now;
                    _target = WindowTracker.Find(sketch.Target, window.Handle);
                }
                if (_target == IntPtr.Zero)
                {
                    _winEvents?.Watch(IntPtr.Zero);
                    Detach(sketch, dt);
                    _frameRate = 10;
                    SetStatus(AttachState.Searching, UiText.Tr($"Buscando {sketch.Target.Description}… ábrela y el sketch aparecerá {(sketch.Layer == AttachLayer.Behind ? "detrás (solo se ve con un perfil translúcido)" : "encima")}.",
                                        $"Looking for {sketch.Target.Description}… open it and the sketch will appear {(sketch.Layer == AttachLayer.Behind ? "behind it (only shows through a see-through profile)" : "on it")}."));
                    return;
                }
            }

            // 2. Is it on screen? (From here on Windows tells us about it: moves,
            //    minimize, hide, close, focus -- see OnWinEvent.)
            _targetHandle = _target;
            _winEvents?.Watch(_target);
            if (!WindowTracker.IsShowing(_target))
            {
                Detach(sketch, dt);
                _frameRate = 10;
                SetStatus(AttachState.TargetHidden, UiText.Tr($"{sketch.Target.Description} está minimizada u oculta.", $"{sketch.Target.Description} is minimized or hidden."));
                return;
            }

            // 2a. One pane of the window: only while that pane is on screen.
            if (Pane is { Visible: false })
            {
                Detach(sketch, dt);
                _frameRate = 10; // the scanner wakes it when the pane comes back
                SetStatus(AttachState.TargetHidden, UiText.Tr("Su panel no está a la vista (otra pestaña).", "Its pane isn't in view (another tab)."));
                return;
            }

            // 2b. Its own terminal profile: only while that profile's tab is in front.
            if (ProfileTitle != null && !TitleGate.Allows(TitleGate.Read(_target), ProfileTitle))
            {
                Detach(sketch, dt);
                _frameRate = 10; // the title-change event wakes it at once anyway
                SetStatus(AttachState.TargetHidden, UiText.Tr("Otra pestaña al frente: el sketch espera a la de su perfil.",
                                                              "Another tab is in front: the sketch waits for its profile's tab."));
                return;
            }

            // 3. Where, and how big.
            var p = MeasureArea(sketch);
            if (p.Width < 16 || p.Height < 16)
            {
                Detach(sketch, dt);
                SetStatus(AttachState.TargetHidden, UiText.Tr($"{sketch.Target.Description} es demasiado pequeña.", $"{sketch.Target.Description} is too small."));
                return;
            }
            int logicalW = Math.Max(1, (int)Math.Round(p.Width / p.Scale));
            int logicalH = Math.Max(1, (int)Math.Round(p.Height / p.Scale));

            if (window.EnsureSize(p.Width, p.Height))
            {
                var surface = window.Surface!;
                sketch.SetCanvas(surface.Canvas, surface);
            }

            sketch.IsAttached = true;
            sketch.TargetScale = (float)p.Scale;
            sketch.TargetX = (int)Math.Round(p.X / p.Scale);
            sketch.TargetY = (int)Math.Round(p.Y / p.Scale);
            bool focused = Win32.GetForegroundWindow() == _target;
            sketch.SetFocused(focused);
            if (focused)
                _lastFocusedAt = now;
            if (focused != _wasFocused)
            {
                _wasFocused = focused;
                _lastActivity = now;
            }
            var area = new Area(p.X, p.Y, p.Width, p.Height, p.Scale);
            (float X, float Y)? cursor = WindowTracker.Cursor() is { } c ? InputGeometry.ToLocal(c.X, c.Y, area) : null;
            if (!_didSetup && cursor is { } first)
            {
                sketch.MouseX = first.X;
                sketch.MouseY = first.Y;
            }

            // 4. Setup() once, at the target's size -- Width/Height are right inside it.
            bool drew = false;
            if (!_didSetup)
            {
                sketch.ApplyAttachedSize(logicalW, logicalH, notify: false);
                sketch.PMouseX = sketch.MouseX;
                sketch.PMouseY = sketch.MouseY;
                Draw(p.Scale, sketch.Setup, "Setup");
                _didSetup = true;
                drew = true;
                if (_crashed)
                {
                    window.Hide();
                    return;
                }
                if (_connectsAudio)
                    AudioEngine.ActivateSketch(sketch);
                // Size() inside Setup() doesn't apply here: the target decides.
                sketch.ApplyAttachedSize(logicalW, logicalH, notify: false);
            }
            else if (sketch.Width != logicalW || sketch.Height != logicalH)
            {
                RunSafely(() => sketch.ApplyAttachedSize(logicalW, logicalH, notify: true), "WindowResized");
            }

            // 5. Events, between frames like the canvas host: the target's own
            //    (moved, focus, shown), the cursor, then keys/clicks from the hooks.
            if (!_crashed)
            {
                sketch.Track(new TargetSample(true, focused, sketch.TargetX, sketch.TargetY, dt), _run);
                if (sketch.IsTargetMoving)
                    _lastActivity = now;
                if (cursor is { } local)
                {
                    if (_lastCursor != local && local.X >= 0 && local.Y >= 0 && local.X < logicalW && local.Y < logicalH)
                        _lastActivity = now;
                    _lastCursor = local;
                    sketch.TrackCursor(local.X, local.Y, _run);
                }
                if (PumpInput(sketch, area) > 0)
                    _lastActivity = now;
                if (PumpShell(sketch) > 0)
                    _lastActivity = now;
            }

            // Eco: fewer frames while nobody's looking (or on low battery).
            // Covered by the window in front? Checked twice a second, only while it could matter.
            if (!focused && sketch.IsEcoEnabled && now - _lastCoverCheck >= 0.5)
            {
                _lastCoverCheck = now;
                _covered = WindowTracker.IsCovered(_target, Win32.GetForegroundWindow());
            }
            else if (focused)
                _covered = false;
            bool eco = EcoPolicy.Throttle(sketch.IsEcoEnabled, focused, sketch.IsTargetMoving, now - _lastActivity, _lowBattery,
                                          _covered, sketch.IsEcoWhenUnfocused);
            sketch.IsEco = eco;
            _isEco = eco;
            _frameRate = EcoPolicy.FrameRate(eco, sketch.TargetFrameRate, sketch.EcoFrameRate);

            // 6. Draw().
            if (!_crashed && (sketch.IsLooping || _redrawRequested))
            {
                _redrawRequested = false;
                sketch.DeltaTime = _lastDraw < 0 ? 1 / 60f : (float)Math.Min(0.1, now - _lastDraw);
                _lastDraw = now;
                long t0 = Stopwatch.GetTimestamp();
                Draw(p.Scale, sketch.RenderFrame, "Draw");
                _meter.Draw(StatsMeter.Ms(t0, Stopwatch.GetTimestamp()));
                drew = true;
                sketch.PMouseX = sketch.MouseX;
                sketch.PMouseY = sketch.MouseY;
            }

            if (_crashed)
            {
                window.Hide();
                return;
            }

            // A backdrop under whatever the sketch drew (DstOver: only where it's
            // still see-through), for an export that lives behind a translucent
            // terminal profile: the terminal's text stays readable over it.
            if (drew && Backdrop is { } backdrop)
                window.Surface!.Canvas.DrawColor(backdrop, SKBlendMode.DstOver);

            // 7. On screen, glued to the target.
            long t1 = Stopwatch.GetTimestamp();
            if (!window.Present(p.X, p.Y, _target, sketch.Layer, drew))
            {
                // The GPU presenter failed (device lost...): carry on with the CPU one.
                SwitchToCpu(UiText.Tr("la GPU dejó de presentar", "the GPU stopped presenting"));
                return; // the next frame draws into the new window
            }
            _movePending = false; // the frame just put the overlay where the target is
            if (drew)
            {
                _meter.Present(StatsMeter.Ms(t1, Stopwatch.GetTimestamp()));
                _meter.Frame(now, _interval);
            }
            lock (_statsLock)
                _stats = _meter.Snapshot(now);
            SetStatus(AttachState.Attached, UiText.Tr($"Sobre {sketch.Target.Description} · {logicalW}×{logicalH}", $"On {sketch.Target.Description} · {logicalW}×{logicalH}")
                + (GpuFallback != null ? UiText.Tr(" · CPU (sin GPU: ", " · CPU (no GPU: ") + GpuFallback + ")" : ""));
        }

        /// <summary>
        /// A WinEvent about the target or the foreground (host thread, while it
        /// pumps messages). A move is handled right here: the overlay follows the
        /// window the moment Windows reports it, without waiting for a frame, so
        /// it no longer trails a drag. Anything else wakes the frame loop.
        /// </summary>
        private void OnWinEvent(uint eventId, IntPtr hwnd, int idObject, int idChild, uint timeMs)
        {
            switch (WinEventFilter.Classify(eventId, hwnd, idObject, idChild, _target))
            {
                case WinEventAction.Moved:
                    // Just note it: the loop applies the latest position (see _movePending).
                    if (!_movePending)
                        _movePendingTime = timeMs;
                    _movePending = true;
                    if (_isEco)
                        Wake(); // dragging is activity: leave eco now, not at the next slow frame
                    return;
                case WinEventAction.Wake:
                    Wake();
                    return;
            }
        }

        /// <summary>Runs sketch code with the canvas scaled to the target's DPI: the sketch draws in logical pixels.</summary>
        /// <summary>
        /// Where the sketch goes: the target window minus its top inset, or just
        /// its pane (its place in the window, scaled to the window's bounds now).
        /// </summary>
        private WindowTracker.Placement MeasureArea(AttachedSketch sketch)
        {
            var p = WindowTracker.Measure(_target, sketch.TopInset);
            if (Pane is not { } pane)
                return p;
            var r = PaneLayout.FromFraction(pane.Place, WindowTracker.Bounds(_target));
            return new WindowTracker.Placement(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, p.Scale);
        }

        /// <summary>The window frames go to: the GPU one if asked for and it starts, else the CPU one.</summary>
        private IOverlayWindow CreateWindow()
        {
            if (UseGpu)
            {
                try
                {
                    var gpu = new DCompWindow(clickThrough: DCompWindow.ShouldBeLayered(_sketch.Layer));
                    GpuFallback = null;
                    return gpu;
                }
                catch (Exception ex)
                {
                    DanaLogger.ErrorFromException(ex, "GPU (DirectComposition)");
                    GpuFallback = ex.Message;
                }
            }
            return new LayeredWindow();
        }

        /// <summary>Replaces a failing GPU window by a CPU one; the sketch gets the new surface on the next frame.</summary>
        private void SwitchToCpu(string why)
        {
            GpuFallback = why;
            _window?.Dispose();
            _window = new LayeredWindow();
            _lastDraw = -1;
            Wake();
        }

        private void Draw(double scale, Action userCode, string context)
        {
            var canvas = _window!.Surface!.Canvas;
            int save = canvas.Save();
            if (scale != 1)
                canvas.Scale((float)scale);
            try
            {
                RunSafely(userCode, context);
            }
            finally
            {
                canvas.RestoreToCount(save);
            }
        }

        /// <summary>
        /// Moves the overlay to where the target is now, without a frame (the
        /// target moved since the last one). A resize instead wakes the loop
        /// for a full frame at the new size.
        /// </summary>
        private void FastMove()
        {
            _movePending = false;
            var window = _window;
            if (window == null || _paused || _crashed || _target == IntPtr.Zero)
                return;
            var p = MeasureArea(_sketch);
            if (p.Width != window.PixelWidth || p.Height != window.PixelHeight)
            {
                Wake();
                return;
            }
            if (window.MoveTo(p.X, p.Y))
                _meter.Move(unchecked((int)(Win32.GetTickCount() - _movePendingTime)));
        }

        /// <summary>The target is gone or hidden: hide, stop listening, and tell the sketch (once).</summary>
        private void Detach(AttachedSketch sketch, double dt)
        {
            _targetHandle = IntPtr.Zero;
            _lastActivity = _clock.Elapsed.TotalSeconds; // coming back counts as activity
            _lastDraw = -1;
            sketch.IsAttached = false;
            _window?.Hide();
            _hooks?.Update(IntPtr.Zero, null);
            if (_didSetup && !_crashed)
                sketch.Track(new TargetSample(false, false, 0, 0, dt), _run);
        }

        /// <summary>
        /// Starts/stops the input hooks to match what the sketch asked for
        /// (ListenToTargetKeys/ListenToTargetMouse can change at any time),
        /// tells them where the target is, and hands queued input to the sketch.
        /// </summary>
        private int PumpInput(AttachedSketch sketch, Area area)
        {
            bool keys = sketch.ListensToTargetKeys, mouse = sketch.ListensToTargetMouse;
            _usesKeyboard = keys;
            _usesMouse = mouse;
            if (_hooks != null && (_hooks.Keys != keys || _hooks.Mouse != mouse))
            {
                _hooks.Dispose();
                _hooks = null;
            }
            if (_hooks == null && (keys || mouse))
            {
                _hooks = new InputHooks(keys, mouse, Wake);
                if (_hooks.Error != null && _hooks.Error != _hooksError)
                {
                    _hooksError = _hooks.Error;
                    DanaLogger.Error(_hooks.Error);
                }
            }
            if (_hooks == null)
                return 0;

            _hooks.Update(_target, area);
            int n = 0;
            while (!_crashed && _hooks.Queue.TryDequeue(out var input))
            {
                if (input.EnqueuedAt > 0)
                    _meter.Input(Environment.TickCount64 - input.EnqueuedAt);
                sketch.DispatchTargetInput(input, _run);
                n++;
            }
            return n;
        }

        /// <summary>Subscribes to shell signals while the sketch wants them, and hands queued ones to it.</summary>
        private int PumpShell(AttachedSketch sketch)
        {
            bool want = sketch.ListensToShell;
            _usesShell = want;
            if (want && !_shellSubscribed)
            {
                ShellSignalHub.Subscribe(this);
                _shellSubscribed = true;
            }
            else if (!want && _shellSubscribed)
            {
                ShellSignalHub.Unsubscribe(this);
                _shellSubscribed = false;
                _signals.Clear();
            }

            int n = 0;
            while (!_crashed && _signals.TryDequeue(out var item))
            {
                _meter.Signal((DateTime.UtcNow - item.Written).TotalMilliseconds);
                sketch.DispatchShellSignal(item.Signal, _run);
                n++;
            }
            return n;
        }

        private void RunSafely(Action userCode, string context)
        {
            if (_crashed)
                return;
            try
            {
                userCode();
            }
            catch (Exception ex)
            {
                _crashed = true;
                DanaLogger.ErrorFromException(ex, $"Error en {context}()");
                SetStatus(AttachState.Crashed, UiText.Tr($"Error en {context}(): {ex.Message}", $"Error in {context}(): {ex.Message}"));
                try { Crashed?.Invoke(ex, context); } catch { /* a listener's bug must not mask the sketch's */ }
            }
        }

        private void SetStatus(AttachState state, string text)
        {
            if (_state == state && _status == text)
                return;
            if (_state != state)
                AttachTrace.Write("host#" + _sketch.InstanceNumber, $"{_state} -> {state}: {text}"
                    + (Pane is { } pane ? $" (pane {AttachTrace.Rect(pane.LastBounds)}, visible {pane.Visible})" : ""));
            _state = state;
            _status = text;
            try { StatusChanged?.Invoke(); } catch (Exception ex) { DanaLogger.ErrorFromException(ex, "StatusChanged"); }
        }
    }
}
