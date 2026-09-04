using System;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using DanaProcessing;
using DanaProcessing.AvaloniaHost;
using DanaProcessing.Ide.Compilation;
using DanaProcessing.Ide.Editor;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// The real IDE window: SketchEditorView on the left, AvaloniaSketchCanvas on
    /// the right, a Run button that compiles the active tab with Roslyn, and an
    /// error panel that appears only when compilation fails.
    ///
    /// Visual language: soft claymorphism on the black/navy/orange palette,
    /// dressed up with a custom title bar (no OS chrome — we draw our own),
    /// a resizable divider between editor and canvas, a status bar that
    /// reports what's actually happening (dirty tab, caret position, compile
    /// state), and a quiet ember glow that appears around whichever card
    /// currently has keyboard focus — the one animated "signature" of this
    /// window; everything else stays deliberately still.
    /// </summary>
    public class MainWindow : Window
    {
        // Below this client-area width the editor and canvas no longer fit
        // side by side at a usable size, so we collapse to one pane at a
        // time with a toggle instead of squeezing both into slivers.
        private const double NarrowBreakpoint = 900;
        private bool _hasRunOnce;
        private readonly SketchEditorView _editorView;
        private readonly AvaloniaSketchCanvas _canvas;
        private readonly TextBlock _outputText;
        private readonly Border _outputPanel;
        private readonly Border _editorGlow;
        private readonly Border _canvasGlow;
        private readonly Ellipse _statusDot;
        private readonly TextBlock _statusLabel;
        private readonly TextBlock _caretLabel;
        private readonly Border _statusPill;
        private readonly Grid _contentGrid;
        private readonly Border _editorCard;
        private readonly Border _canvasCard;
        private readonly GridSplitter _splitter;
        private readonly Border _paneTogglePill;
        private readonly Button _codeToggleButton;
        private readonly Button _resultToggleButton;

        // null until the first layout pass forces it one way or the other.
        // True whenever either the window itself is narrow, OR the loaded
        // sketch's own requested size (Size(w, h)) is too big for the canvas
        // column even at full window width — both cases collapse to the same
        // single-pane-with-toggle layout, since the fix in either case is the
        // same: give the canvas the whole window instead of half of it.
        private bool? _isNarrow;
        private bool _showCanvasInNarrow;
        private bool _canvasOversized;

        // Avalonia's base Window/TopLevel constructor can touch ClientSize
        // before our own constructor body has assigned _contentGrid etc.,
        // which would fire OnPropertyChanged → UpdateResponsiveLayout against
        // still-null fields. This stays false until construction finishes.
        private bool _layoutReady;

        public MainWindow()
        {
            Title = "DanaProcessing IDE";
            Width = 1200;
            Height = 700;
            Background = ClayTheme.WindowBackground;

            Styles.AddRange(ClayTheme.AllStyles());

            // --- Custom chrome: SystemDecorations.None removes the native
            // title bar and caption buttons entirely, so what you see is
            // only what we draw below. (We don't rely on
            // ExtendClientAreaToDecorationsHint/ChromeHints here — that API's
            // exact shape has moved around across Avalonia versions, and
            // SystemDecorations has been stable since the earliest releases.
            // Trade-off: the OS no longer offers edge-drag resize or snap on
            // its own — BuildResizeOverlay() below reimplements the edges by
            // hand via BeginResizeDrag.) ---
            WindowDecorations = WindowDecorations.None;
            CanResize = true;

            _editorView = new SketchEditorView();
            _canvas = new AvaloniaSketchCanvas(new PlaceholderSketch());
            _canvas.SketchSizeChanged += (_, _) => RecomputeCanvasOversized();
            _editorView.CaretPositionChanged += (line, col) => _caretLabel.Text = $"Ln {line}, Col {col}";

            var runButton = new Button
            {
                Content = "▶  Run",
                Classes = { "clay-run" },
                Padding = new Avalonia.Thickness(20, 8),
                CornerRadius = ClayTheme.RadiusButton,
            };
            runButton.Click += (_, _) => RunCurrentSketch();

            var settingsButton = new Button
            {
                Content = "⚙",
                Classes = { "clay-chrome" },
            };
            ToolTip.SetTip(settingsButton, "Configuración");
            settingsButton.Click += (_, _) => new SettingsWindow().ShowDialog(this);

            (_paneTogglePill, _codeToggleButton, _resultToggleButton) = BuildPaneToggle();
            UpdatePaneToggleVisuals();

            var titleBarRoot = BuildTitleBar(runButton, settingsButton, _paneTogglePill);

            _outputText = new TextBlock
            {
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontMono,
                FontSize = 12,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(16, 10, 16, 12),
            };

            var errorHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Avalonia.Thickness(16, 12, 16, 0),
                Spacing = 8,
                Children =
                {
                    new Ellipse { Width = 8, Height = 8, Fill = ClayTheme.Danger, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock
                    {
                        Text = "Error de compilación",
                        Foreground = ClayTheme.Danger,
                        FontFamily = ClayTheme.FontDisplay,
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 12.5,
                    }
                }
            };

            _outputPanel = new Border
            {
                Background = ClayTheme.DangerSurface,
                BorderBrush = ClayTheme.Danger,
                BorderThickness = new Avalonia.Thickness(0, 2, 0, 0),
                CornerRadius = ClayTheme.RadiusPanelTop,
                BoxShadow = ClayTheme.ShadowSubtle,
                IsVisible = false,
                MaxHeight = 200,
                Margin = new Avalonia.Thickness(20, 0, 20, 16),
                Child = new StackPanel
                {
                    Children = { errorHeader, new ScrollViewer { Content = _outputText, MaxHeight = 150 } }
                }
            };

            // Editor and canvas each get their own "clay card": rounded,
            // shadowed, clipped so children never poke past the rounded
            // corners — plus a same-size glow twin sitting behind it that
            // fades in only while that card holds keyboard focus.
            _editorGlow = new Border
            {
                CornerRadius = ClayTheme.RadiusCard,
                BoxShadow = ClayTheme.ShadowFocusRing,
                Opacity = 0,
                IsHitTestVisible = false,
                Margin = new Avalonia.Thickness(20, 16, 10, 16),
                Transitions = new Avalonia.Animation.Transitions
                {
                    new Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) }
                },
            };
            _editorCard = new Border
            {
                Background = ClayTheme.Surface,
                CornerRadius = ClayTheme.RadiusCard,
                BoxShadow = ClayTheme.ShadowRaised,
                ClipToBounds = true,
                Margin = new Avalonia.Thickness(20, 16, 10, 16),
                Child = _editorView
            };
            _editorCard.AddHandler(GotFocusEvent, (_, _) => SetCardFocused(_editorGlow, true), RoutingStrategies.Bubble);
            _editorCard.AddHandler(LostFocusEvent, (_, _) => SetCardFocused(_editorGlow, false), RoutingStrategies.Bubble);

            _canvasGlow = new Border
            {
                CornerRadius = ClayTheme.RadiusCard,
                BoxShadow = ClayTheme.ShadowFocusRing,
                Opacity = 0,
                IsHitTestVisible = false,
                Margin = new Avalonia.Thickness(10, 16, 20, 16),
                Transitions = new Avalonia.Animation.Transitions
                {
                    new Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) }
                },
            };
            // _canvas now sizes itself to whatever the loaded sketch requested
            // via Size(w, h) (see AvaloniaSketchCanvas.MeasureOverride) instead
            // of stretching to fill this card. Center it so a canvas smaller
            // than the card sits in the middle rather than pinned to a corner,
            // and wrap it in a ScrollViewer so a canvas too big for the card
            // scrolls into view instead of getting clipped or squeezing the
            // rest of the window's layout.
            _canvas.HorizontalAlignment = HorizontalAlignment.Center;
            _canvas.VerticalAlignment = VerticalAlignment.Center;
            var canvasScroll = new ScrollViewer { Content = _canvas };

            _canvasCard = new Border
            {
                Background = ClayTheme.Surface,
                CornerRadius = ClayTheme.RadiusCard,
                BoxShadow = ClayTheme.ShadowRaised,
                ClipToBounds = true,
                Margin = new Avalonia.Thickness(10, 16, 20, 16),
                Child = canvasScroll
            };
            _canvasCard.AddHandler(GotFocusEvent, (_, _) => SetCardFocused(_canvasGlow, true), RoutingStrategies.Bubble);
            _canvasCard.AddHandler(LostFocusEvent, (_, _) => SetCardFocused(_canvasGlow, false), RoutingStrategies.Bubble);

            _splitter = new GridSplitter
            {
                Width = 6,
                Background = Avalonia.Media.Brushes.Transparent,
                ResizeDirection = GridResizeDirection.Columns,
                Margin = new Avalonia.Thickness(0, 16, 0, 16),
            };
            _splitter.PointerEntered += (_, _) => _splitter.Background = ClayTheme.AccentDim;
            _splitter.PointerExited += (_, _) => _splitter.Background = Avalonia.Media.Brushes.Transparent;

            // --- Status bar: reports real state instead of decorating. ---
            _statusDot = new Ellipse { Width = 8, Height = 8, Fill = ClayTheme.Success, VerticalAlignment = VerticalAlignment.Center };
            _statusLabel = new TextBlock { Text = "Listo", Foreground = ClayTheme.TextSecondary, FontFamily = ClayTheme.FontBody, FontSize = 11.5 };
            _statusPill = new Border
            {
                Background = ClayTheme.SuccessSurface,
                CornerRadius = ClayTheme.RadiusPill,
                Padding = new Avalonia.Thickness(10, 4),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children = { _statusDot, _statusLabel }
                }
            };
            _caretLabel = new TextBlock
            {
                Text = "Ln 1, Col 1",
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontMono,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(12, 0, 0, 0),
            };
            var appLabel = new TextBlock
            {
                Text = "DanaProcessing IDE",
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            var statusGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            Grid.SetColumn(_statusPill, 0);
            Grid.SetColumn(_caretLabel, 1);
            Grid.SetColumn(appLabel, 2);
            statusGrid.Children.Add(_statusPill);
            statusGrid.Children.Add(_caretLabel);
            statusGrid.Children.Add(appLabel);

            var statusBar = new Border
            {
                Background = ClayTheme.SurfaceRaised,
                Padding = new Avalonia.Thickness(20, 10),
                Child = statusGrid,
            };

            // Column definitions, margins, and per-card visibility are all
            // owned by UpdateResponsiveLayout — it runs once below to set the
            // initial state and again on every resize that crosses the
            // narrow/wide breakpoint.
            _contentGrid = new Grid();
            _contentGrid.Children.Add(_editorGlow);
            _contentGrid.Children.Add(_editorCard);
            _contentGrid.Children.Add(_splitter);
            _contentGrid.Children.Add(_canvasGlow);
            _contentGrid.Children.Add(_canvasCard);

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(titleBarRoot, 0);
            Grid.SetRow(_contentGrid, 1);
            Grid.SetRow(_outputPanel, 2);
            Grid.SetRow(statusBar, 3);

            rootGrid.Children.Add(titleBarRoot);
            rootGrid.Children.Add(_contentGrid);
            rootGrid.Children.Add(_outputPanel);
            rootGrid.Children.Add(statusBar);

            Content = BuildResizeOverlay(rootGrid);

            // Prime the layout once with the constructor's starting Width —
            // ClientSize isn't reliably available until the window is shown,
            // and this guarantees a narrow-started window opens correct too.
            _layoutReady = true;
            UpdateResponsiveLayout(Width);

            // Quiet entrance instead of popping in at full opacity.
            Opacity = 0;
            Transitions = new Avalonia.Animation.Transitions
            {
                new Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) }
            };
            Opened += (_, _) => Opacity = 1;
        }

        private void SetCardFocused(Border glow, bool focused) => glow.Opacity = focused ? 1 : 0;

        /// <summary>
        /// WindowDecorations.None buys us fully custom chrome but also throws
        /// away the OS's edge-drag resize — there's no border left to grab.
        /// This lays a handful of invisible strips/corners on top of the real
        /// content, each just forwarding its PointerPressed into
        /// Window.BeginResizeDrag for the matching edge, so the window is
        /// still resizable by dragging exactly where you'd expect.
        /// </summary>
        private Grid BuildResizeOverlay(Control content)
        {
            const double edgeThickness = 6;
            const double cornerSize = 12;

            var root = new Grid();
            root.Children.Add(content);

            void AddGrip(WindowEdge edge, HorizontalAlignment h, VerticalAlignment v, double? width, double? height, StandardCursorType cursorType)
            {
                var grip = new Border
                {
                    Background = Avalonia.Media.Brushes.Transparent,
                    HorizontalAlignment = h,
                    VerticalAlignment = v,
                    Cursor = new Cursor(cursorType),
                };
                if (width.HasValue)
                    grip.Width = width.Value;
                if (height.HasValue)
                    grip.Height = height.Value;

                grip.PointerPressed += (_, e) =>
                {
                    if (e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
                        BeginResizeDrag(edge, e);
                };
                root.Children.Add(grip);
            }

            // Edges span the full side, corners are small squares layered on
            // top so diagonal resize wins right at the corner pixels.
            AddGrip(WindowEdge.North, HorizontalAlignment.Stretch, VerticalAlignment.Top, null, edgeThickness, StandardCursorType.TopSide);
            AddGrip(WindowEdge.South, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, null, edgeThickness, StandardCursorType.BottomSide);
            AddGrip(WindowEdge.West, HorizontalAlignment.Left, VerticalAlignment.Stretch, edgeThickness, null, StandardCursorType.LeftSide);
            AddGrip(WindowEdge.East, HorizontalAlignment.Right, VerticalAlignment.Stretch, edgeThickness, null, StandardCursorType.RightSide);

            AddGrip(WindowEdge.NorthWest, HorizontalAlignment.Left, VerticalAlignment.Top, cornerSize, cornerSize, StandardCursorType.TopLeftCorner);
            AddGrip(WindowEdge.NorthEast, HorizontalAlignment.Right, VerticalAlignment.Top, cornerSize, cornerSize, StandardCursorType.TopRightCorner);
            AddGrip(WindowEdge.SouthWest, HorizontalAlignment.Left, VerticalAlignment.Bottom, cornerSize, cornerSize, StandardCursorType.BottomLeftCorner);
            AddGrip(WindowEdge.SouthEast, HorizontalAlignment.Right, VerticalAlignment.Bottom, cornerSize, cornerSize, StandardCursorType.BottomRightCorner);

            return root;
        }

        /// <summary>
        /// Segmented "Código / Resultado" pill, shown only once the window is
        /// narrow enough that editor and canvas can't both fit side by side.
        /// Uses the "clay-toggle" style class (see ClayTheme.ButtonEffectStyles)
        /// so active/inactive is driven purely by the "active" CSS-like class
        /// instead of by setting Background/Foreground directly from code —
        /// setting those directly gets silently overridden by the
        /// ContentPresenter-level Setters that FluentTheme's Button template
        /// applies (the same bug documented throughout ClayTheme.cs).
        /// </summary>
        private (Border pill, Button codeButton, Button resultButton) BuildPaneToggle()
        {
            var codeButton = new Button
            {
                Content = "Código",
                Classes = { "clay-toggle" },
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var resultButton = new Button
            {
                Content = "Resultado",
                Classes = { "clay-toggle" },
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            codeButton.Click += (_, _) => SetNarrowPane(showCanvas: false);
            resultButton.Click += (_, _) => SetNarrowPane(showCanvas: true);

            var pill = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = ClayTheme.RadiusPill,
                Padding = new Avalonia.Thickness(0),
                IsVisible = false,
                Margin = new Avalonia.Thickness(0, 0, 8, 0),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
            {
                codeButton,
                resultButton
            }
                }
            };

            return (pill, codeButton, resultButton);
        }

        /// <summary>Which pane is shown while the window is narrow. No-op while wide (both panes are visible then).</summary>
        private void SetNarrowPane(bool showCanvas)
        {
            _showCanvasInNarrow = showCanvas;
            UpdatePaneToggleVisuals();
            ApplyPaneVisibility();
        }

        /// <summary>
        /// Toggles the "active" class on whichever pill button matches
        /// <see cref="_showCanvasInNarrow"/>. The "clay-toggle"/"active"
        /// styles in ClayTheme own the actual colors — this method just
        /// flips which button carries the "active" class, so there's a
        /// single source of truth instead of fighting FluentTheme's
        /// ContentPresenter Setters with direct property assignment.
        /// </summary>
        private void UpdatePaneToggleVisuals()
        {
            _codeToggleButton.Classes.Set("active", !_showCanvasInNarrow);
            _resultToggleButton.Classes.Set("active", _showCanvasInNarrow);
        }

        /// <summary>Shows both cards when wide; shows only the selected one (per <see cref="_showCanvasInNarrow"/>) when narrow.</summary>
        private void ApplyPaneVisibility()
        {
            if (_isNarrow != true)
            {
                _editorCard.IsVisible = true;
                _editorGlow.IsVisible = true;
                _canvasCard.IsVisible = true;
                // Canvas glow stays opacity-driven by focus (SetCardFocused), not IsVisible.
                return;
            }

            _editorCard.IsVisible = !_showCanvasInNarrow;
            _editorGlow.IsVisible = !_showCanvasInNarrow;
            _canvasCard.IsVisible = _showCanvasInNarrow;
        }

        /// <summary>
        /// Reflows between the 50/50 split (wide) and single-pane-with-toggle
        /// (narrow) layouts. Called once at startup and again on every resize
        /// that crosses <see cref="NarrowBreakpoint"/>.
        /// </summary>
        private void UpdateResponsiveLayout(double clientWidth)
        {
            if (!_layoutReady)
                return;

            bool narrow = clientWidth < NarrowBreakpoint || _canvasOversized;
            if (_isNarrow.HasValue && _isNarrow.Value == narrow)
                return;
            _isNarrow = narrow;

            _paneTogglePill.IsVisible = narrow;
            _splitter.IsVisible = !narrow;

            _contentGrid.ColumnDefinitions.Clear();
            if (narrow)
            {
                _contentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

                Grid.SetColumn(_editorGlow, 0);
                Grid.SetColumn(_editorCard, 0);
                Grid.SetColumn(_canvasGlow, 0);
                Grid.SetColumn(_canvasCard, 0);

                var fullMargin = new Avalonia.Thickness(20, 16, 20, 16);
                _editorGlow.Margin = fullMargin;
                _editorCard.Margin = fullMargin;
                _canvasGlow.Margin = fullMargin;
                _canvasCard.Margin = fullMargin;
            }
            else
            {
                // 50/50: both columns are equal Star widths, so the divider
                // starts out exactly centered; dragging the GridSplitter is
                // still free to unbalance it from there.
                _contentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
                _contentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                _contentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

                Grid.SetColumn(_editorGlow, 0);
                Grid.SetColumn(_editorCard, 0);
                Grid.SetColumn(_splitter, 1);
                Grid.SetColumn(_canvasGlow, 2);
                Grid.SetColumn(_canvasCard, 2);

                var editorMargin = new Avalonia.Thickness(20, 16, 10, 16);
                var canvasMargin = new Avalonia.Thickness(10, 16, 20, 16);
                _editorGlow.Margin = editorMargin;
                _editorCard.Margin = editorMargin;
                _canvasGlow.Margin = canvasMargin;
                _canvasCard.Margin = canvasMargin;
            }

            ApplyPaneVisibility();
        }

        protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (_layoutReady && change.Property == ClientSizeProperty)
            {
                // Shrinking the window can be what makes a previously-fine
                // canvas stop fitting the column (or a maximize can be what
                // makes it fit again), so re-check on every resize too — not
                // just when the sketch itself changes size.
                RecomputeCanvasOversized();
                UpdateResponsiveLayout(ClientSize.Width);
            }
        }

        /// <summary>
        /// Checks whether the currently loaded sketch's own requested size
        /// (via Size(w, h)) would still fit the canvas column at a normal
        /// 50/50 split of the current window — and if not, treats it exactly
        /// like a narrow window: single pane, with the toggle pill to flip
        /// between code and result, so the canvas gets the room it actually
        /// asked for instead of being squeezed into half the window.
        ///
        /// The width/height budget here is a deliberate approximation (a
        /// 50/50 split minus roughly the margins UpdateResponsiveLayout uses
        /// for the wide layout) rather than reading exact live Bounds — good
        /// enough to catch "this sketch clearly doesn't fit" without this
        /// check depending on a layout pass having already happened.
        /// </summary>
        private void RecomputeCanvasOversized()
        {
            if (!_layoutReady)
                return;

            double halfWidth = Math.Max(0, ClientSize.Width / 2.0 - 33); // ~splitter + card margins
            double availableHeight = Math.Max(0, ClientSize.Height - 56 - 32 - 32); // title bar + status bar + card margins

            bool oversized = _canvas.SketchWidth > halfWidth || _canvas.SketchHeight > availableHeight;
            if (oversized == _canvasOversized)
                return;

            _canvasOversized = oversized;
            UpdateResponsiveLayout(ClientSize.Width);

            // Solo aplicamos la cortesía de saltar a "Resultado" si el usuario ya
            // corrió un sketch — de lo contrario el placeholder inicial puede
            // marcarse "oversized" en el primer layout y forzaría abrir en
            // Resultado en vez de Código.
            if (_canvasOversized && _hasRunOnce)
                SetNarrowPane(showCanvas: true);
        }

        private Border BuildTitleBar(Button runButton, Button settingsButton, Border paneTogglePill)
        {
            var logoDot = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = ClayTheme.Accent,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var wordmark = new TextBlock
            {
                Text = "DanaProcessing",
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontDisplay,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13.5,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var brand = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Margin = new Avalonia.Thickness(22, 0, 0, 0),
                Children = { logoDot, wordmark }
            };

            var minButton = new Button { Content = "—", Classes = { "clay-chrome" } };
            minButton.Click += (_, _) => WindowState = WindowState.Minimized;

            var maxButton = new Button { Content = "▢", Classes = { "clay-chrome" } };
            maxButton.Click += (_, _) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

            var closeButton = new Button { Content = "✕", Classes = { "clay-chrome-close" } };
            closeButton.Click += (_, _) => Close();

            var controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Avalonia.Thickness(0, 0, 16, 0),
                Children = { paneTogglePill, settingsButton, runButton, minButton, maxButton, closeButton }
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Grid.SetColumn(brand, 0);
            Grid.SetColumn(controls, 1);
            grid.Children.Add(brand);
            grid.Children.Add(controls);

            var root = new Border
            {
                Background = ClayTheme.TitleBarBackground,
                Child = grid,
            };
            // Dragging: with SystemDecorations.None there's no OS-recognized
            // draggable title bar, so we start the move manually. Buttons
            // above mark PointerPressed handled themselves, so clicks on
            // Run / minimize / maximize / close don't also start a drag.
            root.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(root).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(e);
            };
            root.DoubleTapped += (_, _) =>
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

            return root;
        }

        private void RunCurrentSketch()
        {
            var source = _editorView.ActiveSourceText;
            if (string.IsNullOrWhiteSpace(source))
                return;

            var result = SketchCompiler.Compile(source);

            if (result.Success)
            {
                _outputPanel.IsVisible = false;
                _canvas.LoadSketch(result.Sketch!);
                _hasRunOnce = true;
                _statusDot.Fill = ClayTheme.Success;
                _statusLabel.Text = "Listo";
                ((Border)_statusPill).Background = ClayTheme.SuccessSurface;

                // On a narrow window the canvas is hidden until you ask for
                // it — but the whole point of pressing Run is to see the
                // result, so surface it automatically instead of making the
                // user tap "Resultado" themselves.
                if (_isNarrow == true)
                    SetNarrowPane(showCanvas: true);
            }
            else
            {
                _outputText.Text = string.Join(Environment.NewLine + Environment.NewLine, result.Errors);
                _outputPanel.IsVisible = true;

                _statusDot.Fill = ClayTheme.Danger;
                _statusLabel.Text = "Error de compilación";
                ((Border)_statusPill).Background = ClayTheme.DangerSurface;
            }
        }

        public void LoadAndRunSketch(string source)
        {
            _editorView.AddNewTab(null, source);
            RunCurrentSketch();
        }
    }

    /// <summary>Shown in the canvas before the user has pressed Run for the first time.</summary>
    internal class PlaceholderSketch : Sketch
    {
        public override void Setup() => Size(600, 400);

        public override void Draw()
        {
            Background(35, 61, 77);   // matches ClayTheme.Surface (#233D4D)
            Fill(234, 236, 240);      // matches ClayTheme.TextPrimary (#EAECF0)
            TextSize(16);
            Text("Presiona Run para ejecutar el sketch del editor.", 20, Height / 2f);
        }
    }
}