using System.Diagnostics;
using DanaProcessing;

namespace DanaProcessing.AvaloniaHost.Attach
{
    /// <summary>
    /// Runs an <see cref="AttachedSketch"/> the way it asked in Attach(): one
    /// <see cref="AttachedSketchHost"/> on the window in front (AttachMode.Front),
    /// or one per matching window (AttachMode.EachWindow), adding a sketch as
    /// each window opens and dropping it when the window closes. This is what
    /// the IDE canvas and the tray app talk to; it looks like a single host
    /// from outside (State, StatusText, HotReload, Stop, Crashed...).
    ///
    /// Each window's sketch is its own instance of the class -- fields aren't
    /// shared -- with InstanceNumber 0, 1, 2... and Seed + InstanceNumber, so
    /// Random() differs per window. Only the first one connects to audio.
    /// </summary>
    public sealed class AttachedSketchFleet : IDisposable
    {
        public static bool IsSupported => AttachedSketchHost.IsSupported;

        /// <summary>
        /// New windows are noticed the moment they're shown (a WinEvent hook);
        /// this full scan is only the fallback, and also notices closed ones
        /// early when a host reports its window closed.
        /// </summary>
        private const double FallbackScanSeconds = 2;

        /// <summary>With one sketch per pane: how often the panes are measured again (splits, dividers, tab switches).</summary>
        // One sketch per pane: UI Automation queries go into Windows Terminal's
        // UI thread, so they're kept rare -- every ¼ s for a few seconds after
        // something changed (a split, a resize, another window in front), else
        // every ¾ s -- and never while the mouse is over the tab bar (see
        // PaneHold.CursorOverChrome).
        private const double PaneScanSeconds = 0.25, PaneIdleScanSeconds = 0.75, PaneBusySeconds = 3;

        /// <summary>A pane out of sight (another tab) keeps its sketch, hidden, this long before it's let go.</summary>
        private const double ParkSeconds = 60;

        private TerminalPanes? _panes;
        // Pane mode, scanner thread only: each window's title at the last scan
        // that saw panes (see PaneHold), and the last thing traced about it.
        private readonly Dictionary<IntPtr, string?> _paneTitles = new();
        private readonly Dictionary<IntPtr, string> _lastTrace = new();
        // Pane mode, scanner thread: each window's content area (its panes) relative to the window.
        private readonly Dictionary<IntPtr, Win32.RECT> _paneArea = new();
        private string? _paneSignature;
        private double _paneChangedAt;

        /// <summary>Scanner thread: traces what a scan saw for a window, only when it differs from the last scan.</summary>
        private void TraceOnce(IntPtr window, string what)
        {
            if (!AttachTrace.Enabled || (_lastTrace.TryGetValue(window, out var last) && last == what))
                return;
            _lastTrace[window] = what;
            AttachTrace.Write("scan " + AttachTrace.Describe(window), what);
        }
        private readonly Stopwatch _scanClock = Stopwatch.StartNew();

        private readonly AutoResetEvent _scanWake = new(false);
        private volatile bool _scanRequested = true;

        private readonly object _lock = new();
        private readonly bool _connectsAudio;
        private readonly List<AttachedSketchHost> _hosts = new();

        // Read without the lock: hosts call UpdateStatus() from their own
        // thread while holding their frame lock, and HotReload() holds _lock
        // while it waits for that same frame lock -- taking _lock there too
        // would deadlock. Replaced (never mutated) under _lock.
        private volatile AttachedSketchHost[] _snapshot = Array.Empty<AttachedSketchHost>();
        private AttachedSketch _primary;
        private bool _primaryInUse;
        private int _nextInstance = 1;
        private Thread? _scanner;
        private volatile bool _stopRequested, _started, _paused;
        private volatile AttachState _state = AttachState.Starting;
        private volatile string _status = UiText.Tr("Iniciando…", "Starting…");
        private AttachMode _mode = AttachMode.Front;
        // Checked once in the background (finding the profiles runs powershell.exe):
        // until then, no warning.
        private volatile bool _shellSnippetInstalled = true;

        private readonly AttachedRunOverrides _overrides;

        /// <param name="overrides">What an export forces whatever the sketch's Attach() says (one sketch per terminal, behind the terminal for its own profile...); null = the sketch decides.</param>
        public AttachedSketchFleet(AttachedSketch sketch, bool connectsAudio = true, AttachedRunOverrides? overrides = null)
        {
            _primary = sketch ?? throw new ArgumentNullException(nameof(sketch));
            _connectsAudio = connectsAudio;
            _overrides = overrides ?? AttachedRunOverrides.None;
        }

        /// <summary>The first window's sketch (the one the IDE loaded; hot reload compares against it).</summary>
        public AttachedSketch Sketch => _primary;

        /// <summary>Known after Start(): what the sketch's Settings() asked for, or the forced mode.</summary>
        public AttachMode Mode => _mode;

        public AttachState State => _state;
        public string StatusText => _status;

        /// <summary>One host per window (EachWindow) or a single one (Front).</summary>
        public IReadOnlyList<AttachedSketchHost> Hosts => _snapshot;

        /// <summary>How many windows currently have the sketch drawn on them.</summary>
        public int AttachedCount => Hosts.Count(h => h.State == AttachState.Attached);

        public bool UsesKeyboardHook => Hosts.Any(h => h.UsesKeyboardHook);
        public bool UsesMouseHook => Hosts.Any(h => h.UsesMouseHook);
        public bool UsesShellSignals => Hosts.Any(h => h.UsesShellSignals);
        public bool IsEco => Hosts.Any(h => h.State == AttachState.Attached && h.IsEco);

        /// <summary>Speed of the first window that has the sketch on it (AttachedHostStats.None if none).</summary>
        public AttachedHostStats Stats =>
            (Hosts.FirstOrDefault(h => h.State == AttachState.Attached) ?? Hosts.FirstOrDefault())?.Stats ?? AttachedHostStats.None;

        /// <summary>True if a PowerShell profile has the current ShellSignals snippet (checked once, in the background, at Start()).</summary>
        public bool ShellSnippetInstalled => _shellSnippetInstalled;

        /// <summary>Raised (on some host thread) whenever State/StatusText or a host's state changes.</summary>
        public event Action? StatusChanged;

        /// <summary>A sketch threw (any window's). Raised on that host's thread.</summary>
        public event Action<Exception, string>? Crashed;

        /// <summary>A sketch called Exit(). The fleet keeps running; the owner decides (the IDE stops it).</summary>
        public event Action? ExitRequested;

        /// <summary>Pauses or resumes every window's sketch.</summary>
        public bool IsPaused
        {
            get => _paused;
            set
            {
                _paused = value;
                foreach (var h in Hosts)
                    h.IsPaused = value;
                UpdateStatus();
            }
        }

        /// <summary>
        /// The badges the IDE's status card shows, in order. Pure, so it's testable.
        /// </summary>
        /// <summary>The badges that mean "this sketch reads your keyboard/mouse" (the card shows them in orange).</summary>
        public static string KeyboardBadge => UiText.Tr("TECLADO", "KEYBOARD");
        public static string MouseBadge => UiText.Tr("RATÓN", "MOUSE");

        public static IReadOnlyList<string> StatusBadges(AttachMode mode, int windows, bool keyboard, bool mouse,
                                                         bool shell, bool eco, bool paused, bool gpu = false)
        {
            var badges = new List<string>();
            if (paused)
                badges.Add(UiText.Tr("PAUSA", "PAUSED"));
            if (mode == AttachMode.EachWindow)
                badges.Add(windows == 1 ? UiText.Tr("1 VENTANA", "1 WINDOW") : UiText.Tr($"{windows} VENTANAS", $"{windows} WINDOWS"));
            if (keyboard)
                badges.Add(KeyboardBadge);
            if (mouse)
                badges.Add(MouseBadge);
            if (shell)
                badges.Add("SHELL");
            if (eco)
                badges.Add("ECO");
            if (gpu)
                badges.Add("GPU");
            return badges;
        }

        /// <summary>The badges for right now.</summary>
        public IReadOnlyList<string> CurrentBadges() =>
            StatusBadges(_mode, AttachedCount, UsesKeyboardHook, UsesMouseHook, UsesShellSignals, IsEco, _paused,
                         gpu: Hosts.Any(h => h.Presenter == "GPU"));

        /// <summary>The mode the fleet runs in: the forced one if any, else the sketch's.</summary>
        internal static AttachMode ResolveMode(AttachMode sketchMode, AttachMode? forced) => forced ?? sketchMode;

        /// <summary>Runs Settings() on the first sketch, then starts one host or the window scanner.</summary>
        public void Start()
        {
            if (_started)
                return;
            _started = true;
            if (!IsSupported)
            {
                SetStatus(AttachState.Unsupported, UiText.Tr("Los sketches adjuntos solo funcionan en Windows por ahora.", "Attached sketches only work on Windows for now."));
                return;
            }

            if (!RunSettings(_primary))
                return;
            _mode = ResolveMode(_primary.Mode, _overrides.Mode);

            Task.Run(() =>
            {
                try { _shellSnippetInstalled = ShellSignals.PowerShellProfiles().Any(ShellSignals.IsInstalled); }
                catch { /* leave the warning off */ }
                UpdateStatusChanged();
            });

            if (_mode == AttachMode.Front)
            {
                lock (_lock)
                {
                    _primaryInUse = true;
                    AddHost(_primary, IntPtr.Zero);
                }
                return;
            }

            SetStatus(AttachState.Searching, UiText.Tr($"Buscando {_primary.Target.Description}…", $"Looking for {_primary.Target.Description}…"));
            _scanner = new Thread(ScanLoop) { IsBackground = true, Name = "DanaProcessing attached sketch windows" };
            _scanner.Start();
        }

        private bool RunSettings(AttachedSketch sketch)
        {
            try
            {
                sketch.Settings();
                if (_overrides.Layer is { } layer)
                    sketch.ForceLayer(layer);
                return true;
            }
            catch (Exception ex)
            {
                DanaLogger.ErrorFromException(ex, "Error en Settings()");
                SetStatus(AttachState.Crashed, UiText.Tr($"Error en Settings(): {ex.Message}", $"Error in Settings(): {ex.Message}"));
                try { Crashed?.Invoke(ex, "Settings"); } catch { }
                return false;
            }
        }

        /// <summary>Scan as soon as possible (a window opened or closed). Any thread.</summary>
        internal void RequestScan()
        {
            _scanRequested = true;
            try { _scanWake.Set(); } catch (ObjectDisposedException) { }
        }

        private bool PaneMode => _overrides.EachPane && _primary.Target.ClassName == WindowTarget.Terminal.ClassName;

        private void ScanLoop()
        {
            try { Win32.SetThreadDpiAwarenessContext(Win32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
            catch { /* older Windows */ }
            if (PaneMode)
                _panes = new TerminalPanes();
            // EVENT_OBJECT_SHOW for every top-level window that matches the target:
            // a terminal you open gets its sketch at once, not at the next scan.
            Win32.WinEventProc proc = (_, _, hwnd, idObject, idChild, _, _) =>
            {
                try
                {
                    if (idObject == Win32.OBJID_WINDOW && idChild == Win32.CHILDID_SELF && hwnd != IntPtr.Zero
                        && Win32.GetAncestor(hwnd, Win32.GA_ROOT) == hwnd && WindowTracker.MatchesTarget(hwnd, _primary.Target))
                        RequestScan();
                }
                catch { }
            };
            var hook = Win32.SetWinEventHook(Win32.EVENT_OBJECT_SHOW, Win32.EVENT_OBJECT_SHOW, IntPtr.Zero, proc, 0, 0,
                Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);
            try
            {
                var handles = new[] { _scanWake.SafeWaitHandle.DangerousGetHandle() };
                var clock = Stopwatch.StartNew();
                double nextFallback = 0;
                while (!_stopRequested)
                {
                    Win32.PumpMessages();
                    double now = clock.Elapsed.TotalSeconds;
                    if (_scanRequested || now >= nextFallback)
                    {
                        _scanRequested = false;
                        nextFallback = now + (!PaneMode ? FallbackScanSeconds
                            : _scanClock.Elapsed.TotalSeconds - _paneChangedAt < PaneBusySeconds ? PaneScanSeconds : PaneIdleScanSeconds);
                        try { Scan(); }
                        catch (Exception ex) { DanaLogger.ErrorFromException(ex, "Buscando ventanas"); }
                    }
                    uint wait = (uint)Math.Clamp((int)Math.Ceiling((nextFallback - clock.Elapsed.TotalSeconds) * 1000), 1, 2000);
                    Win32.MsgWaitForMultipleObjectsEx(1, handles, wait, Win32.QS_ALLINPUT, Win32.MWMO_INPUTAVAILABLE);
                }
            }
            finally
            {
                if (hook != IntPtr.Zero)
                    Win32.UnhookWinEvent(hook);
                GC.KeepAlive(proc);
                _panes?.Dispose();
                _panes = null;
            }
        }

        /// <summary>EachWindow: a host for every new matching window, none for closed ones.</summary>
        private void Scan()
        {
            var windows = WindowTracker.FindAll(_primary.Target, IntPtr.Zero);

            // One sketch per pane: measure every window's panes first (UI Automation, outside the lock).
            Dictionary<IntPtr, (IReadOnlyList<PaneInfo> Panes, Win32.RECT Window, Win32.RECT? Content)>? panesByWindow = null;
            HashSet<IntPtr>? held = null;
            if (_panes != null)
            {
                panesByWindow = new();
                held = new();
                var foreground = Win32.GetForegroundWindow();
                foreach (var hw in windows)
                {
                    // The window's bounds just before and just after asking UI
                    // Automation: if they differ it's being dragged or resized,
                    // and the panes it reported don't line up with either.
                    var before = WindowTracker.Bounds(hw);
                    bool hadPanesBefore = _snapshot.Any(h => h.Pane?.Window == hw && h.Pane.Visible);
                    // The mouse on the tab bar (or a border): asking UI Automation now
                    // made the terminal's tab bar flash transparent on hover. Nothing
                    // there changes the panes anyway: keep them as they are.
                    if (hadPanesBefore && _paneArea.TryGetValue(hw, out var area)
                        && PaneHold.CursorOverChrome(WindowTracker.Cursor(), before, area))
                    {
                        TraceOnce(hw, "held: mouse over the tab bar");
                        held.Add(hw);
                        continue;
                    }
                    var panes = _panes.Find(hw);
                    var after = WindowTracker.Bounds(hw);
                    if (panes == null)
                    {
                        TraceOnce(hw, "UI Automation can't say: one sketch for the whole window");
                        continue; // UI Automation can't say: that window gets one sketch, as before
                    }
                    string? title = TitleGate.Read(hw);
                    bool hadPanes = _snapshot.Any(h => h.Pane?.Window == hw && h.Pane.Visible);
                    bool popup = OwnPopupInFront(hw, foreground);
                    bool moving = !before.Equals(after);
                    if (PaneHold.Hold(panes.Count, hadPanes, title, _paneTitles.GetValueOrDefault(hw), popup) || (hadPanes && moving))
                    {
                        TraceOnce(hw, $"held: {panes.Count} panes, popup {popup} ({AttachTrace.Describe(foreground)}), moving {moving}, title '{title}'");
                        held.Add(hw); // a menu of the terminal's is open, or it's moving: keep every sketch where it is
                        continue;
                    }
                    if (panes.Count > 0)
                        _paneTitles[hw] = title;
                    var content = panes.Count > 0 ? PaneLayout.Union(panes) : (Win32.RECT?)null;
                    if (content is { } c)
                        _paneArea[hw] = PaneLayout.Relative(c, after);
                    var mine = PaneFilter.ForProfile(panes, _overrides.ProfileTitle);
                    TraceOnce(hw, $"window {AttachTrace.Rect(after)}, {panes.Count} panes, {mine.Count} for the sketch: "
                        + string.Join(", ", panes.Select(p => $"'{p.Name}' {AttachTrace.Rect(p.Bounds)}{(p.Focused ? " focused" : "")}")));
                    panesByWindow[hw] = (mine, after, content);
                }
                foreach (var gone in _paneTitles.Keys.Where(k => !windows.Contains(k)).ToList())
                    _paneTitles.Remove(gone);
                foreach (var gone in _lastTrace.Keys.Where(k => !windows.Contains(k)).ToList())
                    _lastTrace.Remove(gone);
                foreach (var gone in _paneArea.Keys.Where(k => !windows.Contains(k)).ToList())
                    _paneArea.Remove(gone);

                // Anything different from the last scan (windows, their bounds, the
                // panes, the window in front)? Then scan often for a while.
                string signature = foreground + "|" + string.Join(";", windows.Select(w =>
                    AttachTrace.Rect(WindowTracker.Bounds(w)) + (panesByWindow.TryGetValue(w, out var m)
                        ? ":" + string.Join(",", m.Panes.Select(p => AttachTrace.Rect(p.Bounds))) : held.Contains(w) ? ":held" : "")));
                if (signature != _paneSignature)
                {
                    _paneSignature = signature;
                    _paneChangedAt = _scanClock.Elapsed.TotalSeconds;
                }
            }
            var toStop = new List<AttachedSketchHost>();

            lock (_lock)
            {
                if (_stopRequested)
                    return;

                // Windows that closed: their host already stopped itself.
                foreach (var gone in _hosts.Where(h => h.State == AttachState.Closed).ToList())
                {
                    _hosts.Remove(gone);
                    if (!ReferenceEquals(gone.Sketch, _primary))
                        StopQuietly(gone.Sketch);
                }
                _snapshot = _hosts.ToArray();

                foreach (var hw in windows)
                {
                    if (held != null && held.Contains(hw))
                        continue;
                    if (panesByWindow != null && panesByWindow.TryGetValue(hw, out var measured))
                    {
                        ScanPanes(hw, measured.Panes, measured.Window, measured.Content, toStop);
                        continue;
                    }
                    if (_hosts.Count >= AttachedSketch.MaxInstances)
                        break;
                    if (_hosts.Any(h => h.PinnedTarget == hw))
                        continue;

                    AttachedSketch sketch;
                    if (!_primaryInUse)
                    {
                        _primaryInUse = true;
                        sketch = _primary;
                    }
                    else if (CreateSibling(_primary.GetType(), _nextInstance) is { } sibling && RunSettings(sibling))
                    {
                        sketch = sibling;
                        _nextInstance++;
                    }
                    else
                    {
                        break;
                    }
                    AddHost(sketch, hw);
                }
                _snapshot = _hosts.ToArray();
            }
            foreach (var h in toStop)
            {
                h.Stop();
                if (!ReferenceEquals(h.Sketch, _primary))
                    StopQuietly(h.Sketch);
            }
            UpdateStatus();
        }

        /// <summary>
        /// Under _lock. The panes of one window now vs. the sketches it has:
        /// each pane keeps the sketch whose pane it overlaps most (PaneLayout.Match),
        /// new panes get a new sketch, panes gone out of sight hide theirs and,
        /// after ParkSeconds, let it go.
        /// </summary>
        private void ScanPanes(IntPtr window, IReadOnlyList<PaneInfo> panes, Win32.RECT windowBounds, Win32.RECT? content,
                               List<AttachedSketchHost> toStop)
        {
            double now = _scanClock.Elapsed.TotalSeconds;
            var mine = _hosts.Where(h => h.Pane?.Window == window).ToList();
            // Matched in window coordinates: a window dragged since the last
            // scan still matches every pane with itself.
            var relative = panes.Select(p => PaneLayout.Relative(p.Bounds, windowBounds)).ToList();
            var match = PaneLayout.Match(mine.Select(h => h.Pane!.LastBounds).ToList(), relative);

            var kept = new HashSet<AttachedSketchHost>();
            for (int i = 0; i < panes.Count; i++)
            {
                var place = PaneLayout.ToFraction(panes[i].Bounds, windowBounds, content ?? panes[i].Bounds);
                if (match[i] >= 0)
                {
                    var host = mine[match[i]];
                    if (!host.Pane!.Visible)
                        AttachTrace.Write("fleet", $"pane back: sketch #{host.Sketch.InstanceNumber} {AttachTrace.Rect(relative[i])}");
                    host.Pane!.Update(place, relative[i]);
                    host.Pane.HiddenSince = double.NaN;
                    if (panes[i].Focused)
                        host.Pane.FocusedAt = now;
                    kept.Add(host);
                    continue;
                }
                if (_hosts.Count >= AttachedSketch.MaxInstances)
                    continue;
                AttachedSketch sketch;
                if (!_primaryInUse)
                {
                    _primaryInUse = true;
                    sketch = _primary;
                }
                else if (CreateSibling(_primary.GetType(), _nextInstance) is { } sibling && RunSettings(sibling))
                {
                    sketch = sibling;
                    _nextInstance++;
                }
                else
                {
                    continue;
                }
                AttachTrace.Write("fleet", $"new pane: sketch #{sketch.InstanceNumber} {AttachTrace.Rect(relative[i])}");
                AddHost(sketch, window, new PaneSlot(window, place, relative[i]) { FocusedAt = panes[i].Focused ? now : 0 });
            }

            foreach (var host in mine.Where(h => !kept.Contains(h)))
            {
                var slot = host.Pane!;
                if (slot.Visible)
                {
                    AttachTrace.Write("fleet", $"pane gone: sketch #{host.Sketch.InstanceNumber} {AttachTrace.Rect(slot.LastBounds)}");
                    slot.Hide();
                    slot.HiddenSince = now;
                    host.Wake();
                }
                else if (now - slot.HiddenSince > ParkSeconds && !ReferenceEquals(host.Sketch, _primary))
                {
                    AttachTrace.Write("fleet", $"parked: sketch #{host.Sketch.InstanceNumber}");
                    _hosts.Remove(host);
                    toStop.Add(host);
                }
            }
        }

        /// <summary>
        /// One of <paramref name="window"/>'s own popups is in front: a window
        /// it owns, or another window of its process that isn't a terminal
        /// window itself (a menu or flyout; a tab dragged out into a new
        /// terminal window doesn't count).
        /// </summary>
        private bool OwnPopupInFront(IntPtr window, IntPtr foreground)
        {
            if (foreground == IntPtr.Zero || foreground == window)
                return false;
            if (Win32.GetAncestor(foreground, Win32.GA_ROOTOWNER) == window)
                return true;
            Win32.GetWindowThreadProcessId(foreground, out uint fgPid);
            Win32.GetWindowThreadProcessId(window, out uint pid);
            return fgPid != 0 && fgPid == pid && !WindowTracker.MatchesTarget(foreground, _primary.Target);
        }

        /// <summary>A fresh instance for another window: same folder, its own seed and number.</summary>
        private AttachedSketch? CreateSibling(Type type, int instance)
        {
            try
            {
                var s = (AttachedSketch)Activator.CreateInstance(type)!;
                s.SetSketchFolder(_primary.SketchFolder);
                s.ApplySeed(unchecked(_primary.Seed + instance));
                s.InstanceNumber = instance;
                return s;
            }
            catch (Exception ex)
            {
                DanaLogger.ErrorFromException(ex, "Creando otra instancia del sketch");
                return null;
            }
        }

        /// <summary>Under _lock.</summary>
        private void AddHost(AttachedSketch sketch, IntPtr window, PaneSlot? pane = null)
        {
            bool audio = _connectsAudio && ReferenceEquals(sketch, _primary);
            var host = new AttachedSketchHost(sketch, audio, window, settingsAlreadyRun: true)
            {
                IsPaused = _paused,
                Backdrop = _overrides.Backdrop,
                // Per pane, the pane list already says which panes are the profile's: no window-title gate.
                ProfileTitle = pane == null ? _overrides.ProfileTitle : null,
                UseGpu = _overrides.UseGpu,
                Pane = pane,
            };
            host.StatusChanged += UpdateStatus;
            host.TargetClosed += RequestScan; // drop it now, not at the next fallback scan
            host.Crashed += (ex, context) =>
            {
                try { Crashed?.Invoke(ex, context); } catch { /* a listener's bug must not mask the sketch's */ }
            };
            host.ExitRequested += () =>
            {
                try { ExitRequested?.Invoke(); } catch { }
            };
            _hosts.Add(host);
            _snapshot = _hosts.ToArray();
            host.Start();
        }

        /// <summary>
        /// Hot reload for every window: the first sketch is compared and swapped
        /// like AttachedSketchHost.HotReload; every other window gets a new
        /// instance of the new class with its own state carried over. Returns
        /// CanApply = false (changing nothing) when the fields don't line up.
        /// </summary>
        public SketchHotReload.Plan HotReload(AttachedSketch newSketch)
        {
            ArgumentNullException.ThrowIfNull(newSketch);
            if (!_started || _stopRequested)
                return new SketchHotReload.Plan(false, new[] { UiText.Tr("el sketch adjunto estaba detenido", "the attached sketch was stopped") });

            lock (_lock)
            {
                var plan = SketchHotReload.Analyze(_primary, newSketch);
                if (!plan.CanApply)
                    return plan;

                var primaryHost = _hosts.FirstOrDefault(h => ReferenceEquals(h.Sketch, _primary));
                if (primaryHost != null)
                {
                    plan = primaryHost.HotReload(newSketch);
                    if (!plan.CanApply)
                        return plan;
                }
                else
                {
                    SketchHotReload.Transplant(_primary, newSketch);
                }
                _primary = newSketch;

                foreach (var host in _hosts.Where(h => !ReferenceEquals(h.Sketch, newSketch)))
                {
                    try
                    {
                        var sibling = (AttachedSketch)Activator.CreateInstance(newSketch.GetType())!;
                        host.HotReload(sibling); // carries this window's own state over
                    }
                    catch (Exception ex)
                    {
                        DanaLogger.ErrorFromException(ex, "Hot reload de otra ventana");
                    }
                }
                return plan;
            }
        }

        /// <summary>
        /// Stops every window's sketch and the scanner. Runs OnStop() for the
        /// extra windows' instances; the first sketch's OnStop() is the owner's
        /// job, the same as for any other host.
        /// </summary>
        public void Stop()
        {
            _stopRequested = true;
            try { _scanWake.Set(); } catch (ObjectDisposedException) { }
            _scanner?.Join(TimeSpan.FromSeconds(2));
            AttachedSketchHost[] hosts;
            lock (_lock)
            {
                hosts = _hosts.ToArray();
                _hosts.Clear();
                _snapshot = Array.Empty<AttachedSketchHost>();
            }
            foreach (var h in hosts)
                h.Stop();
            foreach (var h in hosts)
                if (!ReferenceEquals(h.Sketch, _primary))
                    StopQuietly(h.Sketch);
            if (_state is not (AttachState.Crashed or AttachState.Unsupported))
                SetStatus(AttachState.Stopped, UiText.Tr("Detenido.", "Stopped."));
        }

        public void Dispose() => Stop();

        private static void StopQuietly(Sketch sketch)
        {
            try { sketch.RaiseStopped(); }
            catch (Exception ex) { DanaLogger.ErrorFromException(ex, "Error en OnStop()"); }
        }

        /// <summary>Folds the hosts' states into one for the IDE card / tray menu.</summary>
        private void UpdateStatus()
        {
            if (_stopRequested)
                return;
            var hosts = _snapshot;
            var crashed = hosts.FirstOrDefault(h => h.State == AttachState.Crashed);
            if (crashed != null)
            {
                SetStatus(AttachState.Crashed, crashed.StatusText);
                return;
            }
            if (_paused)
            {
                SetStatus(AttachState.Paused, UiText.Tr("En pausa.", "Paused."));
                return;
            }
            if (_mode == AttachMode.Front)
            {
                if (hosts.Length > 0)
                    SetStatus(hosts[0].State, hosts[0].StatusText);
                return;
            }

            string target = _primary.Target.Description;
            var live = hosts.Where(h => h.State != AttachState.Closed).ToArray();
            int attached = live.Count(h => h.State == AttachState.Attached);
            if (attached == 1 && live.Length == 1)
                SetStatus(AttachState.Attached, live[0].StatusText);
            else if (attached > 0)
                SetStatus(AttachState.Attached, UiText.Tr($"Sobre {attached} de {live.Length} ventanas de {target}, una instancia del sketch en cada una.",
                                                         $"On {attached} of {live.Length} {target} windows, one instance of the sketch on each."));
            else if (live.Length > 0)
                SetStatus(AttachState.TargetHidden, live.Length == 1
                    ? UiText.Tr($"{target} está minimizada u oculta.", $"{target} is minimized or hidden.")
                    : UiText.Tr($"Las {live.Length} ventanas de {target} están minimizadas u ocultas.", $"All {live.Length} {target} windows are minimized or hidden."));
            else
                SetStatus(AttachState.Searching, UiText.Tr($"Buscando {target}… cada ventana que abras tendrá su propio sketch.", $"Looking for {target}… every window you open gets its own sketch."));
        }

        private void UpdateStatusChanged()
        {
            try { StatusChanged?.Invoke(); } catch { }
        }

        private void SetStatus(AttachState state, string text)
        {
            bool changed = _state != state || _status != text;
            _state = state;
            _status = text;
            if (changed)
                try { StatusChanged?.Invoke(); } catch (Exception ex) { DanaLogger.ErrorFromException(ex, "StatusChanged"); }
        }
    }
}
