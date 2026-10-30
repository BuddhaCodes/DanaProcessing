using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using DanaProcessing.Ide.Editor;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;
using TextMateSharp.Grammars;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// What a blocks tab shows instead of the text editor: the block
    /// workspace, the C# those blocks become (read-only, right beside them,
    /// with the selected block's lines lit up), a toolbar sized for small
    /// hands -- and, for a challenge, its goal and the automatic check.
    /// </summary>
    public sealed class BlocksEditorView : UserControl
    {
        private readonly BlocksWorkspace _workspace;
        private readonly BlocksCanvas _canvas;
        private readonly TextEditor _code;
        private readonly LinesRenderer _codeHighlight = new();
        private readonly Border _codePanel;
        private readonly GridSplitter _splitter;
        private readonly ColumnDefinition _codeColumn;
        private readonly Border _challengeCard;
        private readonly TextBlock _challengeTitle, _challengeGoal, _challengeIcon;
        private readonly Border _resultBanner;
        private readonly TextBlock _resultText;
        private readonly Button _nextButton, _hintButton;
        private readonly TextBlock _hintText;
        private readonly Button _slowToggle, _codeToggle;
        private bool _slow, _codeVisible;
        private readonly Button _tilesButton, _linesButton;
        private readonly DispatcherTimer _checkTimer;

        private EditorTab? _tab;
        private GeneratedCode? _clean;
        private Sketch? _running;
        private DateTime _runStarted;
        private bool _syncingCaret;

        /// <summary>The child pressed ▶ Probar.</summary>
        public event Action? RunRequested;
        /// <summary>The child pressed ⏹.</summary>
        public event Action? StopRequested;
        /// <summary>"Pasar a código": open this C# as an ordinary sketch.</summary>
        public event Action<string>? ConvertToCodeRequested;
        /// <summary>"🏆 Retos": show the list of challenges.</summary>
        public event Action? ChallengesRequested;
        /// <summary>"Siguiente reto": open this challenge.</summary>
        public event Action<Challenge>? ChallengeRequested;

        public BlocksEditorView()
        {
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            _workspace = new BlocksWorkspace(new BlockProgram());
            _canvas = new BlocksCanvas(_workspace);
            _workspace.ProgramChanged += OnProgramChanged;
            _workspace.SelectionChanged += OnSelectionChanged;

            // ---------------- toolbar ----------------
            var run = BigButton("▶  " + Loc.Tr("Probar", "Try it"), "clay-run");
            run.Click += (_, _) => RunRequested?.Invoke();
            var stop = BigButton("⏹", "clay-secondary");
            ToolTip.SetTip(stop, Loc.Tr("Detener", "Stop"));
            stop.Click += (_, _) => StopRequested?.Invoke();

            // Plain theme buttons that show their on/off state themselves: the
            // Fluent ToggleButton look (white text, blue when on) doesn't fit
            // the Clay palette and was hard to read.
            _slowToggle = BigButton("🐢  " + Loc.Tr("Lento", "Slow"), "clay-secondary");
            ToolTip.SetTip(_slowToggle, Loc.Tr("La tortuga camina despacito, para ver cada paso", "The turtle walks slowly so you can see each step"));
            _slowToggle.Click += (_, _) =>
            {
                _slow = !_slow;
                Turtle.SpeedFactor = _slow ? 0.3f : 1f;
                ShowToggle(_slowToggle, _slow);
            };

            var undo = BigButton("↶", "clay-secondary");
            ToolTip.SetTip(undo, Loc.Tr("Deshacer", "Undo"));
            undo.Click += (_, _) => _workspace.Undo();
            var redo = BigButton("↷", "clay-secondary");
            ToolTip.SetTip(redo, Loc.Tr("Rehacer", "Redo"));
            redo.Click += (_, _) => _workspace.Redo();

            var challenges = BigButton("🏆  " + Loc.Tr("Retos", "Challenges"), "clay-secondary");
            challenges.Click += (_, _) => ChallengesRequested?.Invoke();

            // The same program, two ways of seeing it: picture tiles for the
            // youngest, sentences beside their C# for the next step up.
            _tilesButton = BigButton(Loc.Tr("Fichas", "Tiles"), "clay-secondary");
            ToolTip.SetTip(_tilesButton, Loc.Tr("Fichas con dibujos, casi sin leer (6 a 8 años)", "Picture tiles, almost no reading (ages 6-8)"));
            _tilesButton.Click += (_, _) => _workspace.SetView(BlockView.Tiles);
            _linesButton = BigButton(Loc.Tr("Renglones", "Lines"), "clay-secondary");
            ToolTip.SetTip(_linesButton, Loc.Tr("Frases en renglones, cada una al lado de su C# (9 a 12 años)", "Sentences on lines, each beside its C# (ages 9-12)"));
            _linesButton.Click += (_, _) => _workspace.SetView(BlockView.Lines);
            var viewSwitch = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { _tilesButton, _linesButton } };

            _codeToggle = BigButton("</>  " + Loc.Tr("Todo el código", "All the code"), "clay-secondary");
            ToolTip.SetTip(_codeToggle, Loc.Tr("Mostrar u ocultar el programa completo en C#", "Show or hide the whole program in C#"));
            _codeToggle.Click += (_, _) =>
            {
                _codeVisible = !_codeVisible;
                SetCodeVisible(_codeVisible);
                ShowToggle(_codeToggle, _codeVisible);
            };
            ShowToggle(_codeToggle, false);

            var convert = BigButton(Loc.Tr("Pasar a código  ➜", "Turn into code  ➜"), "clay-secondary");
            ToolTip.SetTip(convert, Loc.Tr("Abre este programa como código C# normal, para seguir escribiéndolo",
                                           "Opens this program as ordinary C# so you can keep writing it"));
            convert.Click += (_, _) => ConvertToCodeRequested?.Invoke(BlockCodeGenerator.Generate(_workspace.Program).Source);

            var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { run, stop, _slowToggle, undo, redo } };
            var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { viewSwitch, challenges, _codeToggle, convert } };
            var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(12, 6, 12, 8) };
            Grid.SetColumn(right, 1);
            toolbar.Children.Add(left);
            toolbar.Children.Add(right);

            // ---------------- challenge card ----------------
            _challengeIcon = new TextBlock { FontSize = 34, Foreground = ClayTheme.TextPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
            _challengeTitle = new TextBlock { FontSize = 13, FontFamily = ClayTheme.FontBody, Foreground = ClayTheme.TextMuted, FontWeight = FontWeight.SemiBold };
            _challengeGoal = new TextBlock { FontSize = 20, FontFamily = ClayTheme.FontDisplay, Foreground = ClayTheme.TextPrimary, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold };
            _hintText = new TextBlock { FontSize = 15, FontFamily = ClayTheme.FontBody, Foreground = ClayTheme.TextSecondary, TextWrapping = TextWrapping.Wrap, IsVisible = false, Margin = new Thickness(0, 6, 0, 0) };
            _hintButton = BigButton("💡  " + Loc.Tr("Pista", "Hint"), "clay-secondary");
            _hintButton.Click += (_, _) => _hintText.IsVisible = !_hintText.IsVisible;

            _resultText = new TextBlock { FontSize = 16, FontFamily = ClayTheme.FontBody, Foreground = ClayTheme.TextPrimary, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold };
            _nextButton = BigButton(Loc.Tr("Siguiente reto  ▶", "Next challenge  ▶"), "clay-run");
            _nextButton.Click += (_, _) =>
            {
                if (_workspace.Program.ChallengeId is { } id && ChallengeCatalog.Next(id) is { } next)
                    ChallengeRequested?.Invoke(next);
                else
                    ChallengesRequested?.Invoke();
            };
            var resultRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Grid.SetColumn(_nextButton, 1);
            resultRow.Children.Add(_resultText);
            resultRow.Children.Add(_nextButton);
            _resultBanner = new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10), Margin = new Thickness(0, 10, 0, 0), IsVisible = false, Child = resultRow };

            var cardText = new StackPanel { Children = { _challengeTitle, _challengeGoal, _hintText } };
            var cardTop = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            Grid.SetColumn(cardText, 1);
            Grid.SetColumn(_hintButton, 2);
            _hintButton.VerticalAlignment = VerticalAlignment.Top;
            cardTop.Children.Add(_challengeIcon);
            cardTop.Children.Add(cardText);
            cardTop.Children.Add(_hintButton);
            _challengeCard = new Border
            {
                Background = ClayTheme.Surface, CornerRadius = new CornerRadius(18), Padding = new Thickness(16, 12),
                Margin = new Thickness(12, 0, 12, 8), BoxShadow = ClayTheme.ShadowSubtle, IsVisible = false,
                Child = new StackPanel { Children = { cardTop, _resultBanner } },
            };

            // ---------------- code panel ----------------
            _code = new TextEditor
            {
                IsReadOnly = true, ShowLineNumbers = true, FontFamily = ClayTheme.FontMono, FontSize = 13,
                Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#FAF8F5")), Foreground = ClayTheme.TextPrimary,
                Padding = new Thickness(10, 8), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            };
            var registry = new RegistryOptions(ThemeName.LightPlus);
            var textMate = _code.InstallTextMate(registry);
            if (registry.GetLanguageByExtension(".cs") is { } cs)
                textMate.SetGrammar(registry.GetScopeByLanguageId(cs.Id));
            _code.TextArea.TextView.BackgroundRenderers.Add(_codeHighlight);
            _code.TextArea.Caret.PositionChanged += (_, _) => OnCodeCaretMoved();

            var codeHeader = new TextBlock
            {
                Text = Loc.Tr("Tus bloques en C#", "Your blocks in C#"), FontSize = 13, FontWeight = FontWeight.SemiBold,
                Foreground = ClayTheme.TextSecondary, FontFamily = ClayTheme.FontBody, Margin = new Thickness(12, 8, 12, 6),
            };
            var codeHint = new TextBlock
            {
                Text = Loc.Tr("Tocá un bloque para ver su código", "Click a block to see its code"), FontSize = 11.5,
                Foreground = ClayTheme.TextMuted, FontFamily = ClayTheme.FontBody, Margin = new Thickness(12, 0, 12, 6),
            };
            var codeStack = new DockPanel();
            DockPanel.SetDock(codeHeader, Dock.Top);
            DockPanel.SetDock(codeHint, Dock.Top);
            codeStack.Children.Add(codeHeader);
            codeStack.Children.Add(codeHint);
            codeStack.Children.Add(_code);
            _codePanel = new Border
            {
                Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#FAF8F5")), CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#E8E2DA")), BorderThickness = new Thickness(1),
                ClipToBounds = true, Child = codeStack,
            };

            var canvasFrame = new Border
            {
                CornerRadius = new CornerRadius(12), ClipToBounds = true,
                BorderBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#E8E2DA")), BorderThickness = new Thickness(1),
                Child = _canvas,
            };

            _codeColumn = new ColumnDefinition(new GridLength(360));
            var content = new Grid { Margin = new Thickness(12, 0, 12, 12) };
            content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            content.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(8)));
            content.ColumnDefinitions.Add(_codeColumn);
            _splitter = new GridSplitter { Background = Brushes.Transparent, ResizeDirection = GridResizeDirection.Columns };
            Grid.SetColumn(_splitter, 1);
            Grid.SetColumn(_codePanel, 2);
            content.Children.Add(canvasFrame);
            content.Children.Add(_splitter);
            content.Children.Add(_codePanel);

            var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Background = ClayTheme.Base };
            Grid.SetRow(_challengeCard, 1);
            Grid.SetRow(content, 2);
            root.Children.Add(toolbar);
            root.Children.Add(_challengeCard);
            root.Children.Add(content);
            Content = root;

            _checkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _checkTimer.Tick += (_, _) => PollTurtleChallenge();

            // Both views show the C# themselves (Renglones line by line; Fichas
            // under "Todo el código"): the full program panel starts closed.
            SetCodeVisible(false);
        }

        /// <summary>An on/off button: "on" gets the accent look, "off" the plain one.</summary>
        private static void ShowToggle(Button button, bool on)
        {
            button.Classes.Set("clay-run", on);
            button.Classes.Set("clay-secondary", !on);
        }

        private static Button BigButton(string text, string cls) => new()
        {
            Content = text, Classes = { cls }, FontSize = 15, Padding = new Thickness(14, 8),
            VerticalAlignment = VerticalAlignment.Center,
        };

        // =================================================================
        // Showing a tab
        // =================================================================

        /// <summary>The blocks tab being shown, or null.</summary>
        public EditorTab? Tab => _tab;

        /// <summary>Shows <paramref name="tab"/>'s program (a blocks tab).</summary>
        public void ShowTab(EditorTab tab)
        {
            if (ReferenceEquals(_tab, tab))
                return;
            SketchStopped();
            _tab = tab;
            var program = tab.Blocks!;
            var challenge = ChallengeCatalog.Get(program.ChallengeId);
            _workspace.Load(program, challenge?.Palette);
            ShowChallenge(challenge);
            ShowViewButtons();
            RefreshCode();
        }

        private void ShowViewButtons()
        {
            ShowToggle(_tilesButton, _workspace.View == BlockView.Tiles);
            ShowToggle(_linesButton, _workspace.View == BlockView.Lines);
        }

        private void ShowChallenge(Challenge? challenge)
        {
            _challengeCard.IsVisible = challenge is not null;
            _resultBanner.IsVisible = false;
            _hintText.IsVisible = false;
            if (challenge is null)
                return;
            int number = ChallengeCatalog.All.ToList().IndexOf(challenge) + 1;
            _challengeIcon.Text = challenge.Icon;
            _challengeTitle.Text = Loc.Tr($"RETO {number} · ", $"CHALLENGE {number} · ") + challenge.Title.ToUpperInvariant()
                                   + Stars(ChallengeProgress.Load().GetValueOrDefault(challenge.Id));
            _challengeGoal.Text = challenge.Goal;
            _hintText.Text = challenge.Hint;
        }

        private static string Stars(int n) => n <= 0 ? "" : "   " + new string('★', n) + new string('☆', 3 - n);

        private void OnProgramChanged()
        {
            if (_tab is null)
                return;
            _tab.Blocks = _workspace.Program;
            _tab.RefreshFromBlocks();
            _resultBanner.IsVisible = false;
            ShowViewButtons();
            RefreshCode();
        }

        private void RefreshCode()
        {
            _clean = BlockCodeGenerator.Generate(_workspace.Program);
            _syncingCaret = true;
            _code.Document.Text = _clean.Source;
            _syncingCaret = false;
            OnSelectionChanged(_workspace.SelectedBlockId);
        }

        private void SetCodeVisible(bool visible)
        {
            _codePanel.IsVisible = visible;
            _splitter.IsVisible = visible;
            _codeColumn.Width = visible ? new GridLength(360) : new GridLength(0);
        }

        /// <summary>Lights up the selected block's lines in the code panel and scrolls to them.</summary>
        private void OnSelectionChanged(int? id)
        {
            if (id is { } blockId && _clean?.BlockLines.TryGetValue(blockId, out var lines) == true)
            {
                _codeHighlight.First = lines.First;
                _codeHighlight.Last = lines.Last;
                _code.ScrollToLine(lines.First);
            }
            else
            {
                _codeHighlight.First = _codeHighlight.Last = 0;
            }
            _code.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        }

        /// <summary>The other way round: clicking a line of code selects its block.</summary>
        private void OnCodeCaretMoved()
        {
            if (_syncingCaret || _clean is null)
                return;
            if (_clean.BlockAtLine(_code.TextArea.Caret.Line) is { } id && id != _workspace.SelectedBlockId)
                _workspace.Select(id);
        }

        public void Undo() => _workspace.Undo();
        public void Redo() => _workspace.Redo();

        /// <summary>Selects the block whose code is at <paramref name="runLine"/> of the code that ran (a runtime error's line).</summary>
        public void ShowErrorAt(int runLine)
        {
            var challenge = ChallengeCatalog.Get(_workspace.Program.ChallengeId);
            var ran = BlockCodeGenerator.Generate(_workspace.Program, instrument: true, scene: challenge?.Scene());
            if (ran.BlockAtLine(runLine) is { } id)
                _workspace.Select(id);
        }

        // =================================================================
        // Running and checking
        // =================================================================

        /// <summary>The IDE started running this tab's program: light blocks up, and check the challenge.</summary>
        public void SketchStarted(Sketch sketch)
        {
            _running = sketch;
            _runStarted = DateTime.UtcNow;
            _workspace.LastHit = id => sketch.LastBlockHit(id);
            _canvas.Animating = true;
            _resultBanner.IsVisible = false;

            if (ChallengeCatalog.Get(_workspace.Program.ChallengeId) is { } challenge)
            {
                if (challenge.IsTurtle)
                    _checkTimer.Start();
                else
                    ShowResult(challenge, ChallengeChecker.Check(challenge, _workspace.Program, null));
            }
        }

        /// <summary>Nothing of this tab is running any more.</summary>
        public void SketchStopped()
        {
            _running = null;
            _checkTimer.Stop();
            _workspace.LastHit = null;
            _canvas.Animating = false;
        }

        private void PollTurtleChallenge()
        {
            if (_running is not { } sketch || ChallengeCatalog.Get(_workspace.Program.ChallengeId) is not { } challenge)
            {
                _checkTimer.Stop();
                return;
            }
            Turtle[] turtles;
            lock (sketch.Turtles)
                turtles = sketch.Turtles.ToArray();
            bool waitedEnough = (DateTime.UtcNow - _runStarted).TotalSeconds > 1.5;
            if (turtles.Length == 0 ? !waitedEnough : !turtles.All(t => t.IsDone))
                return; // still walking: check once it's done

            _checkTimer.Stop();
            var lines = turtles.SelectMany(t => t.Lines).ToList();
            ShowResult(challenge, ChallengeChecker.Check(challenge, _workspace.Program, lines));
        }

        private void ShowResult(Challenge challenge, ChallengeResult result)
        {
            _resultBanner.IsVisible = true;
            _resultBanner.Background = result.Passed ? ClayTheme.SuccessSurface : new SolidColorBrush(Avalonia.Media.Color.Parse("#FFF1E0"));
            _resultText.Text = result.Message;
            _nextButton.IsVisible = result.Passed;
            if (result.Passed)
            {
                ChallengeProgress.Record(challenge.Id, result.Stars);
                ShowChallenge(challenge);
                _resultBanner.IsVisible = true;
                _nextButton.Content = ChallengeCatalog.Next(challenge.Id) is null
                    ? Loc.Tr("Ver todos los retos", "See all challenges")
                    : Loc.Tr("Siguiente reto  ▶", "Next challenge  ▶");
            }
        }

        /// <summary>Soft yellow band behind the selected block's lines.</summary>
        private sealed class LinesRenderer : IBackgroundRenderer
        {
            private static readonly IBrush Band = new SolidColorBrush(Avalonia.Media.Color.Parse("#55FFD65C"));
            private static readonly IBrush Marker = new SolidColorBrush(Avalonia.Media.Color.Parse("#F2B705"));
            public int First, Last;
            public KnownLayer Layer => KnownLayer.Background;

            public void Draw(TextView textView, DrawingContext drawingContext)
            {
                if (First <= 0 || textView.Document is null)
                    return;
                textView.EnsureVisualLines();
                foreach (var visual in textView.VisualLines)
                {
                    int n = visual.FirstDocumentLine.LineNumber;
                    if (n < First || n > Last)
                        continue;
                    double top = visual.VisualTop - textView.VerticalOffset;
                    drawingContext.FillRectangle(Band, new Rect(0, top, textView.Bounds.Width, visual.Height));
                    drawingContext.FillRectangle(Marker, new Rect(0, top, 3, visual.Height));
                }
            }
        }
    }
}
