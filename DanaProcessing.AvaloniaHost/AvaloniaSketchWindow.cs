using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using DanaProcessing;

namespace DanaProcessing.AvaloniaHost
{
    /// <summary>
    /// Standalone Avalonia window that runs a Sketch full-window. Thin wrapper
    /// around AvaloniaSketchCanvas. A sketch that calls Widget() in Settings()
    /// gets a widget window instead: no borders, see-through where the
    /// system allows it, above other windows, dragged around, with a
    /// right-click menu, and back where it was left the next time.
    /// </summary>
    public class AvaloniaSketchWindow : Window
    {
        public AvaloniaSketchCanvas Canvas { get; }

        /// <summary>The sketch this window runs.</summary>
        public Sketch Sketch { get; }

        /// <summary>The widget settings this window runs with, or null for an ordinary window.</summary>
        public WidgetOptions? Widget { get; }

        /// <summary>True when the window really is see-through (the system granted it): see Sketch.IsTransparentWindow.</summary>
        public bool IsTransparent { get; private set; }

        private DispatcherTimer? _savePosition;
        private bool _placed;

        /// <summary>windowless: no OS title bar/caption buttons at all (see
        /// ExportedSketchRunner/SketchExporter — this is what "gadget mode"
        /// export produces) — dragged by Alt+left-click-holding anywhere on
        /// the drawing instead (plain left-click still reaches the sketch's
        /// own MousePressed/MouseClicked unmolested — see the Tunnel-phase
        /// remark below for why that split actually works), closed with
        /// Alt+F4 (still works with no visible chrome — it's an OS-level
        /// shortcut, not dependent on the title bar existing) since there's
        /// no close button to click.</summary>
        public AvaloniaSketchWindow(Sketch sketch, string title = "DanaProcessing Sketch", bool windowless = false)
        {
            Title = title;
            Sketch = sketch;

            // NOT `Width = sketch.Width; Height = sketch.Height;` here: Setup()
            // (where a sketch actually calls Size(w, h)) doesn't run at
            // construction time — AvaloniaSketchCanvas runs it lazily, during its
            // own first MeasureOverride (see the remark there on why). Reading
            // sketch.Width/Height this early would still be whatever placeholder
            // value a fresh Sketch starts with, so the window would open at some
            // unrelated default size with the canvas's real (correctly sized)
            // content floating in the corner of it — exactly the black-bordered
            // window this class used to produce. SizeToContent fits the window to
            // whatever the canvas's own MeasureOverride reports instead, which is
            // correct precisely because it's driven by the same layout pass that
            // makes Setup() run in the first place, and keeps re-fitting if a
            // sketch calls Size() again later to resize itself. CanResize = false
            // makes that guarantee permanent — dragging the window bigger than
            // the canvas is exactly how the black borders came back otherwise.
            SizeToContent = SizeToContent.WidthAndHeight;
            CanResize = false;

            // This window IS the widget, if the sketch asks for one: Settings()
            // runs now (before the window shows) so it can be set up as one.
            Canvas = new AvaloniaSketchCanvas(sketch) { HandsOffWidgets = false };
            Canvas.RunSettingsNow();
            Widget = sketch.WidgetMode;

            Content = Canvas;
            Closed += (_, _) => Canvas.StopSketch(); // the sketch's OnStop(): release cameras, ports, devices
            PositionChanged += (_, e) =>
            {
                var scaling = RenderScaling > 0 ? RenderScaling : 1;
                Sketch.WindowX = (float)(e.Point.X / scaling);
                Sketch.WindowY = (float)(e.Point.Y / scaling);
                if (Widget != null)
                    ScheduleSavePosition();
            };

            if (Widget != null)
                SetUpWidget(Widget);
            else if (windowless)
            {
                WindowDecorations = WindowDecorations.None;
                LockToSketchSize();
                MoveByDragging(withAlt: true);
            }
        }

        /// <summary>
        /// Full manual control over sizing instead of leaning on
        /// SizeToContent: running SizeToContent's own automatic re-fitting
        /// AND a separate Min/Max lock at the same time turned out to fight
        /// each other -- neither reliably won, so the window still visibly
        /// resized in testing. One explicit owner (this callback, setting
        /// Width/Height AND Min/Max together every time) replaces that with
        /// a single, unambiguous source of truth for this window's size.
        /// Starts at the sketch's size so far (Sketch's own 600x400 default
        /// until Settings()/Setup() call Size()).
        /// </summary>
        private void LockToSketchSize()
        {
            SizeToContent = SizeToContent.Manual;
            void Fit(int w, int h)
            {
                Width = w;
                Height = h;
                MinWidth = MaxWidth = w;
                MinHeight = MaxHeight = h;
            }
            Fit(Math.Max(1, Sketch.Width), Math.Max(1, Sketch.Height));
            Canvas.SketchSizeChanged += Fit;
        }

        /// <summary>
        /// Tunnel-only, and marking Handled ourselves: the previous approach
        /// (listen on Bubble with handledEventsToo, after Canvas's own
        /// PointerPressed had already run) never actually triggered a drag in
        /// testing. Tunnel reaches this handler FIRST, before Canvas (or
        /// anything else) sees the event at all -- a drag press is consumed
        /// right here and never reaches Canvas; any other press continues on
        /// to Canvas exactly as before, so sketch interaction is unaffected.
        /// </summary>
        private void MoveByDragging(bool withAlt)
        {
            AddHandler(PointerPressedEvent, (_, e) =>
            {
                if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    return;
                if (withAlt && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                    return;
                e.Handled = true;
                BeginMoveDrag(e);
            }, RoutingStrategies.Tunnel);
        }

        // --- Widget mode ----------------------------------------------------------

        private void SetUpWidget(WidgetOptions options)
        {
            WindowDecorations = WindowDecorations.None;
            ShowInTaskbar = false;
            Topmost = options.AlwaysOnTop;
            LockToSketchSize();
            MoveByDragging(withAlt: !options.Draggable);

            if (options.Transparent)
            {
                // What Clear() leaves shows the desktop. The canvas keeps alpha
                // all the way (its offscreen surface is premultiplied RGBA and
                // the frame is drawn over a cleared, transparent lease), so the
                // window is all that's missing: no brush behind the canvas, and
                // a per-pixel transparent surface from the system. Windows and
                // macOS grant it; Linux only with a compositor running.
                Background = Brushes.Transparent;
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            }
            UpdateTransparency();

            ContextMenu = BuildWidgetMenu(options);

            // Placed before it shows (no jump from the default spot), and once
            // more when open: Setup() may have changed its size since.
            WindowStartupLocation = WindowStartupLocation.Manual;
            PlaceWidget();

            Opened += (_, _) =>
            {
                UpdateTransparency();
                PlaceWidget();
                _placed = true; // positions from here on are the person's: worth remembering
            };
            Closing += (_, _) => SavePosition();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ActualTransparencyLevelProperty)
                UpdateTransparency();
        }

        private void UpdateTransparency()
        {
            IsTransparent = Widget is { Transparent: true } && ActualTransparencyLevel == WindowTransparencyLevel.Transparent;
            Sketch.IsTransparentWindow = IsTransparent;
        }

        private ContextMenu BuildWidgetMenu(WidgetOptions options)
        {
            var onTop = new MenuItem
            {
                Header = UiText.Tr("Siempre encima", "Always on top"),
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = options.AlwaysOnTop,
            };
            onTop.Click += (_, _) =>
            {
                Topmost = !Topmost;
                onTop.IsChecked = Topmost;
            };

            var close = new MenuItem { Header = UiText.Tr("Cerrar", "Close") };
            close.Click += (_, _) => Close();

            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = Title, IsEnabled = false });
            menu.Items.Add(new Separator());
            menu.Items.Add(onTop);
            menu.Items.Add(close);
            return menu;
        }

        /// <summary>Where it was left last time, if that's still on a screen; otherwise the top-right corner of the main screen.</summary>
        private void PlaceWidget()
        {
            // Some systems name no primary screen (a bare X server): the first will do.
            var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
            var scaling = screen?.Scaling ?? 1;
            var size = new PixelSize((int)(Width * scaling), (int)(Height * scaling));
            var saved = WidgetPlacement.Load(WidgetKey);
            if (saved is { } point && Screens.All.Any(s => s.WorkingArea.Contains(new PixelPoint(point.X + 16, point.Y + 16))))
            {
                Position = point;
                return;
            }
            if (screen != null)
            {
                var area = screen.WorkingArea;
                int margin = (int)(24 * screen.Scaling);
                Position = new PixelPoint(area.Right - size.Width - margin, area.Y + margin);
            }
        }

        private string WidgetKey => Sketch.GetType().FullName ?? Sketch.GetType().Name;

        private void ScheduleSavePosition()
        {
            if (_savePosition == null)
            {
                _savePosition = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                _savePosition.Tick += (_, _) =>
                {
                    _savePosition!.Stop();
                    SavePosition();
                };
            }
            _savePosition.Stop();
            _savePosition.Start();
        }

        private void SavePosition()
        {
            if (_placed && IsVisible)
                WidgetPlacement.Save(WidgetKey, Position);
        }
    }
}
