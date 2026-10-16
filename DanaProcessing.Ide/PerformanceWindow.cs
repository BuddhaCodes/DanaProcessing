using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;
using TextMateSharp.Grammars;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// Pure decisions behind performance mode, kept apart from the window so
    /// they can be tested: which screen to take, and the size/opacity steps.
    /// </summary>
    public static class PerformanceMode
    {
        public const double MinFontSize = 12, MaxFontSize = 64, DefaultFontSize = 24;
        public const double MinOpacity = 0, MaxOpacity = 0.9, DefaultOpacity = 0.45;

        /// <summary>
        /// The screen to go full screen on: the first one that ISN'T the main
        /// display when there are several (the projector, at a gig), the main
        /// one otherwise. <paramref name="isPrimary"/> lists the screens in order.
        /// </summary>
        public static int PickScreen(IReadOnlyList<bool> isPrimary)
        {
            if (isPrimary.Count == 0)
                return -1;
            for (int i = 0; i < isPrimary.Count; i++)
                if (!isPrimary[i])
                    return isPrimary.Count > 1 ? i : 0;
            return 0;
        }

        /// <summary>Ctrl + / Ctrl -: two points at a time, within limits.</summary>
        public static double StepFontSize(double current, int direction) =>
            Math.Clamp(Math.Round(current + 2 * Math.Sign(direction)), MinFontSize, MaxFontSize);

        /// <summary>Ctrl+Up / Ctrl+Down: how dark the band behind the code is.</summary>
        public static double StepOpacity(double current, int direction) =>
            Math.Clamp(Math.Round(current + 0.1 * Math.Sign(direction), 2), MinOpacity, MaxOpacity);
    }

    /// <summary>
    /// Performance mode, as at an algorave: the running sketch fills the screen
    /// (the projector, when there's a second display) and its code floats on
    /// top, big and see-through, while you keep editing. The canvas is the
    /// IDE's own -- moved here while this window is open, so the sketch, its
    /// music and its state keep going -- and the code is the active tab's own
    /// document, so every keystroke is in the IDE too.
    ///
    /// Keys: Ctrl+Enter = Hot Reload (Run if it can't), Ctrl+Shift+Enter = Run,
    /// Ctrl+H = hide/show the code, Ctrl+T = hide/show the [Tweak] sliders,
    /// Ctrl +/- = text size, Ctrl+Up/Down = how dark the band behind the code
    /// is, Esc = back to the IDE.
    /// </summary>
    public sealed class PerformanceWindow : Window
    {
        private static double _fontSize = PerformanceMode.DefaultFontSize;
        private static double _opacity = PerformanceMode.DefaultOpacity;

        private readonly Control _canvas;
        private readonly Viewbox _stage;
        private readonly TextEditor _code;
        private readonly Border _codeLayer;
        private readonly TextBlock _status;
        private readonly Func<Task> _hotReload;
        private readonly Func<Task> _run;
        private readonly Func<(string Text, bool IsError)> _readStatus;
        private readonly DispatcherTimer _statusPoll;

        private readonly Action? _toggleTweaks;

        public PerformanceWindow(Control canvas, TextDocument document, Func<Task> hotReload, Func<Task> run,
                                 Func<(string Text, bool IsError)> readStatus, Action? toggleTweaks = null)
        {
            _toggleTweaks = toggleTweaks;
            _canvas = canvas;
            _hotReload = hotReload;
            _run = run;
            _readStatus = readStatus;

            Title = Loc.Tr("DanaProcessing · modo performance", "DanaProcessing · performance mode");
            Background = Brushes.Black;
            WindowDecorations = WindowDecorations.None;

            _stage = new Viewbox { Stretch = Stretch.Uniform, Child = canvas };

            _code = new TextEditor
            {
                Document = document,
                FontFamily = ClayTheme.FontMono,
                FontSize = _fontSize,
                FontWeight = FontWeight.SemiBold,
                ShowLineNumbers = false,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
                WordWrap = true,
            };
            var registry = new RegistryOptions(ThemeName.DarkPlus);
            var textMate = _code.InstallTextMate(registry);
            var csharp = registry.GetLanguageByExtension(".cs");
            if (csharp != null)
                textMate.SetGrammar(registry.GetScopeByLanguageId(csharp.Id));

            _codeLayer = new Border
            {
                Background = new SolidColorBrush(Avalonia.Media.Colors.Black, _opacity),
                Padding = new Thickness(48, 36, 48, 84), // room at the bottom for the status line
                Child = _code,
            };

            _status = new TextBlock
            {
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                Foreground = new SolidColorBrush(Avalonia.Media.Color.FromArgb(200, 255, 255, 255)),
                TextWrapping = TextWrapping.Wrap,
            };
            // A dark pill, so the status stays readable over any visual.
            var statusPill = new Border
            {
                Background = new SolidColorBrush(Avalonia.Media.Color.FromArgb(170, 0, 0, 0)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 8),
                Margin = new Thickness(40, 0, 40, 18),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = _status,
            };

            Content = new Panel { Children = { _stage, _codeLayer, statusPill } };

            AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            Opened += (_, _) =>
            {
                PlaceOnScreen();
                _code.TextArea.Focus();
                _code.TextArea.Caret.BringCaretToView();
            };

            _statusPoll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _statusPoll.Tick += (_, _) => RefreshStatus();
            _statusPoll.Start();
            RefreshStatus();
        }

        /// <summary>Gives the canvas back (the IDE puts it in its own layout again).</summary>
        public Control ReleaseCanvas()
        {
            _statusPoll.Stop();
            _stage.Child = null;
            return _canvas;
        }

        private void PlaceOnScreen()
        {
            var screens = Screens.All;
            int index = PerformanceMode.PickScreen(screens.Select(s => s.IsPrimary).ToList());
            if (index >= 0)
                Position = screens[index].Bounds.Position;
            WindowState = WindowState.FullScreen;
        }

        private void RefreshStatus()
        {
            var (text, isError) = _readStatus();
            string keys = Loc.Tr(
                "Ctrl+Enter ⚡   Ctrl+Shift+Enter ▶   Ctrl+H código   Ctrl+T sliders   Ctrl +/− tamaño   Ctrl ↑/↓ fondo   Esc salir",
                "Ctrl+Enter ⚡   Ctrl+Shift+Enter ▶   Ctrl+H code   Ctrl+T sliders   Ctrl +/− size   Ctrl ↑/↓ backdrop   Esc exit");
            _status.Text = string.IsNullOrWhiteSpace(text) ? keys : text + "\n" + keys;
            _status.Foreground = isError
                ? new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 130, 110))
                : new SolidColorBrush(Avalonia.Media.Color.FromArgb(200, 255, 255, 255));
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            var mods = e.KeyModifiers;
            bool ctrl = mods.HasFlag(KeyModifiers.Control) || mods.HasFlag(KeyModifiers.Meta);
            bool shift = mods.HasFlag(KeyModifiers.Shift);

            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
            else if (ctrl && e.Key == Key.Enter)
            {
                e.Handled = true;
                _ = shift ? _run() : _hotReload();
            }
            else if (ctrl && e.Key == Key.H)
            {
                e.Handled = true;
                _codeLayer.IsVisible = !_codeLayer.IsVisible;
                if (!_codeLayer.IsVisible)
                    _canvas.Focus(); // code hidden: the keyboard goes to the sketch
                else
                    _code.TextArea.Focus();
            }
            else if (ctrl && e.Key == Key.T && _toggleTweaks != null)
            {
                e.Handled = true;
                _toggleTweaks(); // the [Tweak] panel off stage; MIDI-mapped knobs keep working
            }
            else if (ctrl && e.Key is Key.OemPlus or Key.Add)
            {
                e.Handled = true;
                _code.FontSize = _fontSize = PerformanceMode.StepFontSize(_fontSize, +1);
            }
            else if (ctrl && e.Key is Key.OemMinus or Key.Subtract)
            {
                e.Handled = true;
                _code.FontSize = _fontSize = PerformanceMode.StepFontSize(_fontSize, -1);
            }
            else if (ctrl && e.Key is Key.Up or Key.Down)
            {
                e.Handled = true;
                _opacity = PerformanceMode.StepOpacity(_opacity, e.Key == Key.Up ? +1 : -1);
                _codeLayer.Background = new SolidColorBrush(Avalonia.Media.Colors.Black, _opacity);
            }
        }
    }
}
