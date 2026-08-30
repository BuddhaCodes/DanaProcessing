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
            // Trade-off: the OS no longer gives you edge-drag resize or
            // snap — CanResize still lets the window be resized via the
            // maximize toggle and via code, just not by dragging the border.) ---
            WindowDecorations = WindowDecorations.None;
            CanResize = true;

            _editorView = new SketchEditorView();
            _canvas = new AvaloniaSketchCanvas(new PlaceholderSketch());
            _editorView.CaretPositionChanged += (line, col) => _caretLabel.Text = $"Ln {line}, Col {col}";

            var runButton = new Button
            {
                Content = "▶  Run",
                Classes = { "clay-run" },
                Padding = new Avalonia.Thickness(20, 8),
                CornerRadius = ClayTheme.RadiusButton,
            };
            runButton.Click += (_, _) => RunCurrentSketch();

            var titleBarRoot = BuildTitleBar(runButton);

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
            var editorCard = new Border
            {
                Background = ClayTheme.Surface,
                CornerRadius = ClayTheme.RadiusCard,
                BoxShadow = ClayTheme.ShadowRaised,
                ClipToBounds = true,
                Margin = new Avalonia.Thickness(20, 16, 10, 16),
                Child = _editorView
            };
            editorCard.AddHandler(GotFocusEvent, (_, _) => SetCardFocused(_editorGlow, true), RoutingStrategies.Bubble);
            editorCard.AddHandler(LostFocusEvent, (_, _) => SetCardFocused(_editorGlow, false), RoutingStrategies.Bubble);

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
            var canvasCard = new Border
            {
                Background = ClayTheme.Surface,
                CornerRadius = ClayTheme.RadiusCard,
                BoxShadow = ClayTheme.ShadowRaised,
                ClipToBounds = true,
                Margin = new Avalonia.Thickness(10, 16, 20, 16),
                Child = _canvas
            };
            canvasCard.AddHandler(GotFocusEvent, (_, _) => SetCardFocused(_canvasGlow, true), RoutingStrategies.Bubble);
            canvasCard.AddHandler(LostFocusEvent, (_, _) => SetCardFocused(_canvasGlow, false), RoutingStrategies.Bubble);

            var splitter = new GridSplitter
            {
                Width = 6,
                Background = Avalonia.Media.Brushes.Transparent,
                ResizeDirection = GridResizeDirection.Columns,
                Margin = new Avalonia.Thickness(0, 16, 0, 16),
            };
            splitter.PointerEntered += (_, _) => splitter.Background = ClayTheme.AccentDim;
            splitter.PointerExited += (_, _) => splitter.Background = Avalonia.Media.Brushes.Transparent;

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

            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(560) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(_editorGlow, 0);
            Grid.SetColumn(editorCard, 0);
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(_canvasGlow, 2);
            Grid.SetColumn(canvasCard, 2);
            contentGrid.Children.Add(_editorGlow);
            contentGrid.Children.Add(editorCard);
            contentGrid.Children.Add(splitter);
            contentGrid.Children.Add(_canvasGlow);
            contentGrid.Children.Add(canvasCard);

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(titleBarRoot, 0);
            Grid.SetRow(contentGrid, 1);
            Grid.SetRow(_outputPanel, 2);
            Grid.SetRow(statusBar, 3);

            rootGrid.Children.Add(titleBarRoot);
            rootGrid.Children.Add(contentGrid);
            rootGrid.Children.Add(_outputPanel);
            rootGrid.Children.Add(statusBar);

            Content = rootGrid;

            // Quiet entrance instead of popping in at full opacity.
            Opacity = 0;
            Transitions = new Avalonia.Animation.Transitions
            {
                new Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) }
            };
            Opened += (_, _) => Opacity = 1;
        }

        private void SetCardFocused(Border glow, bool focused) => glow.Opacity = focused ? 1 : 0;

        private Border BuildTitleBar(Button runButton)
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
                Children = { runButton, minButton, maxButton, closeButton }
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

                _statusDot.Fill = ClayTheme.Success;
                _statusLabel.Text = "Listo";
                ((Border)_statusPill).Background = ClayTheme.SuccessSurface;
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
            Text("Presiona ▶ Run para ejecutar el sketch del editor.", 20, Height / 2f);
        }
    }
}