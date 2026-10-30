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
        private readonly TextBlock _challengeTitle, _challengeGoal, _challengeNumber;
        private readonly StackPanel _challengeStars, _challengeTrail, _resultStars;
        private readonly Border _resultBanner, _resultIcon, _hintBox;
        private readonly TextBlock _resultText;
        private readonly Button _nextButton, _hintButton;
        private readonly TextBlock _hintText;
        private readonly Button _slowToggle, _codeToggle, _stepToggle, _nextStep;
        private readonly Border _stepStrip;
        private readonly TextBlock _stepCode, _stepHelp;
        private readonly DispatcherTimer _stepTimer;
        private bool _stepping;
        private int _stepShown = -1;
        private readonly Button[] _compactable;
        private readonly StackPanel _toolbarLeft, _toolbarRight;
        private readonly Button _listenButton;
        private readonly DispatcherTimer _speechTimer;
        private bool _readAloud;
        private Challenge? _shownChallenge;
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
            Styles.AddRange(BlocksChrome.Styles());

            _workspace = new BlocksWorkspace(new BlockProgram());
            _canvas = new BlocksCanvas(_workspace);
            _workspace.ProgramChanged += OnProgramChanged;
            _workspace.SelectionChanged += OnSelectionChanged;

            // ---------------- toolbar ----------------
            // Left: what makes the program go. Right: how to look at it and
            // where to go next. Icons are drawn (not emoji), so they look the
            // same on every computer.
            var run = BlocksChrome.Button(BlocksChrome.Icons.Play, Loc.Tr("Probar", "Try it"), "go");
            run.Click += (_, _) => RunRequested?.Invoke();
            var stop = BlocksChrome.Button(BlocksChrome.Icons.Stop, null);
            ToolTip.SetTip(stop, Loc.Tr("Detener", "Stop"));
            stop.Click += (_, _) => StopRequested?.Invoke();

            _slowToggle = BlocksChrome.Button(BlocksChrome.Icons.Turtle, Loc.Tr("Lento", "Slow"));
            ToolTip.SetTip(_slowToggle, Loc.Tr("La tortuga camina despacito, para ver cada paso", "The turtle walks slowly so you can see each step"));
            _slowToggle.Click += (_, _) =>
            {
                _slow = !_slow;
                Turtle.SpeedFactor = _slow ? 0.3f : 1f;
                ShowToggle(_slowToggle, _slow);
            };

            // Paso a paso: the program waits before each block until the child
            // clicks "Siguiente paso"; that block and its C# stay lit meanwhile.
            _stepToggle = BlocksChrome.Button(BlocksChrome.Icons.Steps, Loc.Tr("Paso a paso", "Step by step"));
            ToolTip.SetTip(_stepToggle, Loc.Tr("El programa espera antes de cada bloque: vos decidís cuándo sigue",
                                               "The program waits before each block: you decide when it goes on"));
            _stepToggle.Click += (_, _) => SetStepping(!_stepping);
            _nextStep = BlocksChrome.Button(BlocksChrome.Icons.NextStep, Loc.Tr("Siguiente paso", "Next step"), "step");
            _nextStep.IsVisible = false;
            _nextStep.Click += (_, _) => Turtle.Stepper?.Step();

            var undo = BlocksChrome.Button(BlocksChrome.Icons.Undo, null);
            ToolTip.SetTip(undo, Loc.Tr("Deshacer", "Undo"));
            undo.Click += (_, _) => _workspace.Undo();
            var redo = BlocksChrome.Button(BlocksChrome.Icons.Redo, null);
            ToolTip.SetTip(redo, Loc.Tr("Rehacer", "Redo"));
            redo.Click += (_, _) => _workspace.Redo();

            // The same program, two ways of seeing it: picture tiles for the
            // youngest, sentences beside their C# for the next step up.
            _tilesButton = BlocksChrome.Button(BlocksChrome.Icons.Tiles, Loc.Tr("Fichas", "Tiles"), "seg");
            ToolTip.SetTip(_tilesButton, Loc.Tr("Fichas con dibujos, casi sin leer (6 a 8 años)", "Picture tiles, almost no reading (ages 6-8)"));
            _tilesButton.Click += (_, _) => _workspace.SetView(BlockView.Tiles);
            _linesButton = BlocksChrome.Button(BlocksChrome.Icons.Lines, Loc.Tr("Renglones", "Lines"), "seg");
            ToolTip.SetTip(_linesButton, Loc.Tr("Frases en renglones, cada una al lado de su C# (9 a 12 años)", "Sentences on lines, each beside its C# (ages 9-12)"));
            _linesButton.Click += (_, _) => _workspace.SetView(BlockView.Lines);
            var viewSwitch = new Border
            {
                Background = BlocksChrome.Well, CornerRadius = new CornerRadius(12), Padding = new Thickness(3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { _tilesButton, _linesButton } },
            };

            var challenges = BlocksChrome.Button(BlocksChrome.Icons.Trophy, Loc.Tr("Retos", "Challenges"));
            challenges.Click += (_, _) => ChallengesRequested?.Invoke();

            _codeToggle = BlocksChrome.Button(BlocksChrome.Icons.Code, Loc.Tr("Código", "Code"));
            ToolTip.SetTip(_codeToggle, Loc.Tr("Mostrar u ocultar el programa completo en C#", "Show or hide the whole program in C#"));
            _codeToggle.Click += (_, _) =>
            {
                _codeVisible = !_codeVisible;
                SetCodeVisible(_codeVisible);
                ShowToggle(_codeToggle, _codeVisible);
            };

            var convert = BlocksChrome.Button(BlocksChrome.Icons.Forward, Loc.Tr("Pasar a código", "Turn into code"), "outline");
            // Arrow after the words: "blocks -> code".
            if (convert.Content is StackPanel convertRow && convertRow.Children.Count == 2)
                convertRow.Children.Move(0, 1);
            ToolTip.SetTip(convert, Loc.Tr("Abre este programa como código C# normal, para seguir escribiéndolo",
                                           "Opens this program as ordinary C# so you can keep writing it"));
            convert.Click += (_, _) => ConvertToCodeRequested?.Invoke(BlockCodeGenerator.Generate(_workspace.Program).Source);

            var left = _toolbarLeft = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 6,
                Children = { run, _nextStep, stop, BlocksChrome.Divider(), _slowToggle, _stepToggle, BlocksChrome.Divider(), undo, redo },
            };
            var right = _toolbarRight = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right,
                Children = { viewSwitch, BlocksChrome.Divider(), challenges, _codeToggle, convert },
            };
            // On a narrow window the secondary buttons keep only their icons.
            _compactable = new[] { _slowToggle, _stepToggle, challenges, _codeToggle };
            foreach (var b in _compactable)
                if (ToolTip.GetTip(b) is null && b.Content is StackPanel r && r.Children.OfType<TextBlock>().FirstOrDefault() is { } label)
                    ToolTip.SetTip(b, label.Text);
            var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(14, 10, 14, 10) };
            toolbar.SizeChanged += (_, e) => Compact(e.NewSize.Width);
            _nextStep.PropertyChanged += (_, e) =>
            {
                if (e.Property == IsVisibleProperty)
                    Compact(toolbar.Bounds.Width);
            };
            Grid.SetColumn(right, 1);
            toolbar.Children.Add(left);
            toolbar.Children.Add(right);

            // ---------------- challenge card ----------------
            // [ 3 ]  RETO 3 · CUADRADO  ★★☆          [ Pista ]
            //        Dibujá un cuadrado usando "repetir".
            //        ● ● ◉ ○ ○ ○ ...  (where you are among all the challenges)
            _challengeNumber = new TextBlock
            {
                FontFamily = BlocksChrome.FontKids, FontSize = 24, FontWeight = FontWeight.Bold, Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
            var badge = new Border
            {
                Width = 52, Height = 52, CornerRadius = new CornerRadius(16), Background = BlocksChrome.Brush("#FF9F43"),
                BoxShadow = BoxShadows.Parse("0 3 0 0 #D97A1E"), Margin = new Thickness(0, 2, 16, 0), VerticalAlignment = VerticalAlignment.Top,
                Child = _challengeNumber,
            };
            _challengeTitle = new TextBlock
            {
                FontSize = 12.5, FontFamily = BlocksChrome.FontUi, Foreground = BlocksChrome.InkMuted, FontWeight = FontWeight.Bold,
                LetterSpacing = 1.2, VerticalAlignment = VerticalAlignment.Center,
            };
            _challengeStars = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            _challengeGoal = new TextBlock
            {
                FontSize = 21, FontFamily = BlocksChrome.FontKids, Foreground = BlocksChrome.Ink, TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeight.Bold, Margin = new Thickness(0, 2, 0, 0),
            };
            _challengeTrail = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(0, 8, 0, 0) };
            _hintText = new TextBlock
            {
                FontSize = 16, FontFamily = BlocksChrome.FontKids, Foreground = BlocksChrome.InkSoft, TextWrapping = TextWrapping.Wrap,
            };
            _hintBox = new Border
            {
                Background = BlocksChrome.GoldSoft, CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 8),
                Margin = new Thickness(0, 10, 0, 0), IsVisible = false, Child = _hintText,
            };
            // Escuchar: the computer's own voice reads the goal (and the hint,
            // if it's open) for children who don't read well yet. After the
            // first time, results are read out too.
            _listenButton = BlocksChrome.Button(BlocksChrome.Icons.Speaker, Loc.Tr("Escuchar", "Listen"));
            ToolTip.SetTip(_listenButton, Loc.Tr("La computadora lee el reto en voz alta", "The computer reads the challenge aloud"));
            _listenButton.IsVisible = Narrator.IsAvailable;
            _listenButton.Click += (_, _) =>
            {
                if (Narrator.IsSpeaking)
                {
                    Narrator.Stop();
                    return;
                }
                _readAloud = true;
                var text = _challengeGoal.Text ?? "";
                if (_hintBox.IsVisible)
                    text += ". " + Loc.Tr("Pista: ", "Hint: ") + _hintText.Text;
                if (_resultBanner.IsVisible)
                    text = _resultText.Text + ". " + text;
                Say(text);
            };
            _speechTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _speechTimer.Tick += (_, _) =>
            {
                bool speaking = Narrator.IsSpeaking;
                ShowToggle(_listenButton, speaking);
                if (!speaking)
                    _speechTimer.Stop();
            };

            _hintButton = BlocksChrome.Button(BlocksChrome.Icons.Bulb, Loc.Tr("Pista", "Hint"));
            _hintButton.Click += (_, _) =>
            {
                _hintBox.IsVisible = !_hintBox.IsVisible;
                ShowToggle(_hintButton, _hintBox.IsVisible);
            };

            _resultIcon = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            _resultText = new TextBlock
            {
                FontSize = 17, FontFamily = BlocksChrome.FontKids, Foreground = BlocksChrome.Ink, TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.Bold,
            };
            _nextButton = BlocksChrome.Button(BlocksChrome.Icons.Forward, Loc.Tr("Siguiente reto", "Next challenge"), "go");
            if (_nextButton.Content is StackPanel nextRow && nextRow.Children.Count == 2)
                nextRow.Children.Move(0, 1);
            _nextButton.Click += (_, _) =>
            {
                if (_workspace.Program.ChallengeId is { } id && ChallengeCatalog.Next(id) is { } next)
                    ChallengeRequested?.Invoke(next);
                else
                    ChallengesRequested?.Invoke();
            };
            _resultStars = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
            var resultRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
            Grid.SetColumn(_resultStars, 1);
            Grid.SetColumn(_resultText, 2);
            Grid.SetColumn(_nextButton, 3);
            resultRow.Children.Add(_resultIcon);
            resultRow.Children.Add(_resultStars);
            resultRow.Children.Add(_resultText);
            resultRow.Children.Add(_nextButton);
            _resultBanner = new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 10), Margin = new Thickness(0, 12, 0, 0), IsVisible = false, Child = resultRow };

            var overline = new StackPanel { Orientation = Orientation.Horizontal, Children = { _challengeTitle, _challengeStars } };
            var cardText = new StackPanel { Children = { overline, _challengeGoal, _challengeTrail } };
            var cardTop = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            Grid.SetColumn(cardText, 1);
            var cardButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Top, Children = { _listenButton, _hintButton } };
            Grid.SetColumn(cardButtons, 2);
            cardTop.Children.Add(badge);
            cardTop.Children.Add(cardText);
            cardTop.Children.Add(cardButtons);
            _challengeCard = new Border
            {
                Background = Brushes.White, CornerRadius = new CornerRadius(18), Padding = new Thickness(16, 14),
                Margin = new Thickness(14, 0, 14, 10), BoxShadow = BoxShadows.Parse("0 0 0 1 #EAE3D7, 0 4 14 0 #12000000"), IsVisible = false,
                Child = new StackPanel { Children = { cardTop, _hintBox, _resultBanner } },
            };

            // ---------------- code panel ----------------
            _code = new TextEditor
            {
                IsReadOnly = true, ShowLineNumbers = true, FontFamily = BlocksChrome.FontCode, FontSize = 13.5,
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
                Text = Loc.Tr("Tus bloques en C#", "Your blocks in C#"), FontSize = 15, FontWeight = FontWeight.Bold,
                Foreground = BlocksChrome.Ink, FontFamily = BlocksChrome.FontUi, Margin = new Thickness(14, 12, 14, 2),
            };
            var codeHint = new TextBlock
            {
                Text = Loc.Tr("Tocá un bloque para ver su código", "Click a block to see its code"), FontSize = 13,
                Foreground = BlocksChrome.InkMuted, FontFamily = BlocksChrome.FontUi, Margin = new Thickness(14, 0, 14, 8),
            };
            var codeStack = new DockPanel();
            DockPanel.SetDock(codeHeader, Dock.Top);
            DockPanel.SetDock(codeHint, Dock.Top);
            codeStack.Children.Add(codeHeader);
            codeStack.Children.Add(codeHint);
            codeStack.Children.Add(_code);
            _codePanel = new Border
            {
                Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#FAF8F5")), CornerRadius = new CornerRadius(16),
                BorderBrush = BlocksChrome.Line, BorderThickness = new Thickness(1),
                ClipToBounds = true, Child = codeStack,
            };

            // While stepping: the block that just went, as C#, and what to do next.
            _stepCode = new TextBlock
            {
                FontFamily = BlocksChrome.FontCode, FontSize = 16, Foreground = BlocksChrome.Brush("#F3EEE4"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            _stepHelp = new TextBlock
            {
                FontFamily = BlocksChrome.FontUi, FontSize = 14, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Brush("#FFD35E"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0),
            };
            _stepStrip = new Border
            {
                Background = BlocksChrome.Brush("#EE24211D"), CornerRadius = new CornerRadius(14), Padding = new Thickness(10, 8, 16, 8),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(340, 0, 80, 18),
                BoxShadow = BoxShadows.Parse("0 6 20 0 #33000000"), IsVisible = false, IsHitTestVisible = false,
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new Border
                        {
                            Background = BlocksChrome.Brush("#3A3631"), CornerRadius = new CornerRadius(7), Padding = new Thickness(7, 2),
                            Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center,
                            Child = new TextBlock { Text = "C#", FontFamily = BlocksChrome.FontCode, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = BlocksChrome.Brush("#B5AC9C") },
                        },
                        _stepCode,
                        _stepHelp,
                    },
                },
            };
            var canvasFrame = new Border
            {
                CornerRadius = new CornerRadius(16), ClipToBounds = true,
                BorderBrush = BlocksChrome.Line, BorderThickness = new Thickness(1),
                Child = new Grid { Children = { _canvas, _stepStrip } },
            };

            _codeColumn = new ColumnDefinition(new GridLength(360));
            var content = new Grid { Margin = new Thickness(14, 0, 14, 14) };
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

            _stepTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            _stepTimer.Tick += (_, _) => ShowStep();

            _checkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _checkTimer.Tick += (_, _) => PollTurtleChallenge();

            // Both views show the C# themselves (Renglones line by line; Fichas
            // under "Todo el código"): the full program panel starts closed.
            SetCodeVisible(false);
        }

        private void Compact(double width)
        {
            if (width <= 0)
                return;
            // Try with every label; if the two halves don't fit side by side, icons only.
            foreach (var b in _compactable)
                BlocksChrome.ShowLabel(b, true);
            _toolbarLeft.Measure(Size.Infinity);
            _toolbarRight.Measure(Size.Infinity);
            bool fits = _toolbarLeft.DesiredSize.Width + _toolbarRight.DesiredSize.Width + 16 <= width;
            if (!fits)
                foreach (var b in _compactable)
                    BlocksChrome.ShowLabel(b, false);
        }

        /// <summary>An on/off button: "on" gets the warm gold look.</summary>
        private static void ShowToggle(Button button, bool on) => button.Classes.Set("on", on);

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
            // The challenge speaks in the same letters as the blocks.
            var font = _workspace.View == BlockView.Tiles ? BlocksChrome.FontKids : BlocksChrome.FontUi;
            _challengeGoal.FontFamily = _hintText.FontFamily = _resultText.FontFamily = font;
        }

        private void ShowChallenge(Challenge? challenge)
        {
            if (!ReferenceEquals(challenge, _shownChallenge))
                Narrator.Stop();
            _shownChallenge = challenge;
            _challengeCard.IsVisible = challenge is not null;
            _resultBanner.IsVisible = false;
            _hintBox.IsVisible = false;
            ShowToggle(_hintButton, false);
            if (challenge is null)
                return;
            var all = ChallengeCatalog.All.ToList();
            int number = all.IndexOf(challenge) + 1;
            var progress = ChallengeProgress.Load();
            _challengeNumber.Text = number.ToString();
            _challengeTitle.Text = Loc.Tr($"RETO {number} · ", $"CHALLENGE {number} · ") + challenge.Title.ToUpperInvariant();
            _challengeGoal.Text = challenge.Goal;
            _hintText.Text = challenge.Hint;

            int stars = progress.GetValueOrDefault(challenge.Id);
            _challengeStars.Children.Clear();
            FillStars(_challengeStars, stars, 15);

            // The trail: done ones filled, this one a ring, the rest pale.
            _challengeTrail.Children.Clear();
            foreach (var c in all)
            {
                bool current = c == challenge, done = progress.GetValueOrDefault(c.Id) > 0;
                _challengeTrail.Children.Add(new Border
                {
                    Width = current ? 14 : 9, Height = current ? 14 : 9, CornerRadius = new CornerRadius(7),
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = current ? Brushes.White : done ? BlocksChrome.Go : BlocksChrome.Well,
                    BorderBrush = current ? BlocksChrome.Brush("#FF9F43") : null,
                    BorderThickness = new Thickness(current ? 3 : 0),
                });
            }
        }

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
            if (_stepping)
                StartStepping();
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
            StopStepping();
            _workspace.LastHit = null;
            _canvas.Animating = false;
        }

        // =================================================================
        // Step by step
        // =================================================================

        private void SetStepping(bool on)
        {
            _stepping = on;
            ShowToggle(_stepToggle, on);
            if (on && _running is not null)
                StartStepping();
            else if (!on)
            {
                StopStepping();
                if (_running is { } sketch)
                    _workspace.LastHit = id => sketch.LastBlockHit(id);
            }
        }

        /// <summary>From now on the running program waits for a click before each block.</summary>
        private void StartStepping()
        {
            var stepper = new BlockStepper();
            Turtle.Stepper = stepper;
            // Only the block that went last is lit, and it stays lit while waiting.
            _workspace.LastHit = id => id == stepper.Current ? Environment.TickCount64 : 0;
            _stepShown = -1;
            _nextStep.IsVisible = true;
            _stepStrip.IsVisible = true;
            _stepTimer.Start();
            ShowStep();
        }

        private void StopStepping()
        {
            Turtle.Stepper = null; // anything still queued runs on freely
            _stepTimer.Stop();
            _nextStep.IsVisible = false;
            _stepStrip.IsVisible = false;
        }

        /// <summary>Keeps the strip (the current block's C#) and the "next step" button up to date.</summary>
        private void ShowStep()
        {
            if (Turtle.Stepper is not { } stepper)
                return;
            int current = stepper.Current;
            if (current != _stepShown)
            {
                _stepShown = current;
                if (current >= 0)
                    OnSelectionChanged(current); // the full code panel follows too
            }
            _stepCode.Text = current >= 0 ? CodeOf(current) : Loc.Tr("(todavía nada)", "(nothing yet)");
            bool waiting = stepper.Waiting;
            bool finished = !waiting && _running is { } sketch && TurtlesDone(sketch);
            bool noTurtle = _running is { } s && (DateTime.UtcNow - _runStarted).TotalSeconds > 1 && NoTurtles(s);
            _nextStep.IsEnabled = waiting;
            _stepHelp.Text = noTurtle
                ? Loc.Tr("Paso a paso es para programas con la tortuga", "Step by step works with turtle programs")
                : waiting
                ? Loc.Tr("Tocá «Siguiente paso»", "Press \u201cNext step\u201d")
                : finished ? Loc.Tr("¡Terminó!", "All done!") : Loc.Tr("La tortuga está caminando…", "The turtle is walking…");
        }

        private static bool NoTurtles(Sketch sketch)
        {
            lock (sketch.Turtles)
                return sketch.Turtles.Count == 0;
        }

        private static bool TurtlesDone(Sketch sketch)
        {
            lock (sketch.Turtles)
                return sketch.Turtles.Count > 0 && sketch.Turtles.All(t => t.IsDone);
        }

        /// <summary>The C# a block became (its first line, trimmed), as the child would read it.</summary>
        private string CodeOf(int blockId)
        {
            if (_clean is null || !_clean.BlockLines.TryGetValue(blockId, out var lines))
                return "";
            var all = _clean.Source.Split('\n');
            return lines.First >= 1 && lines.First <= all.Length ? all[lines.First - 1].Trim() : "";
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
            if (result.Passed)
            {
                ChallengeProgress.Record(challenge.Id, result.Stars);
                ShowChallenge(challenge);
                BlocksChrome.SetText(_nextButton, ChallengeCatalog.Next(challenge.Id) is null
                    ? Loc.Tr("Ver todos los retos", "See all challenges")
                    : Loc.Tr("Siguiente reto", "Next challenge"));
            }
            _resultBanner.IsVisible = true;
            _resultBanner.Background = BlocksChrome.Brush(result.Passed ? "#E6F5EB" : "#FFF1E0");
            _resultIcon.Background = result.Passed ? BlocksChrome.Go : BlocksChrome.Brush("#FF9F43");
            _resultIcon.Child = new StrokeIcon(result.Passed ? BlocksChrome.Icons.Check : BlocksChrome.Icons.Retry, 20, thickness: 2.8)
            {
                Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center,
            };
            _resultText.Text = result.Message;
            _nextButton.IsVisible = result.Passed;
            if (_readAloud)
                Say(result.Message);
            _resultStars.IsVisible = result.Passed;
            FillStars(_resultStars, result.Stars, 24);
        }

        private void Say(string text)
        {
            if (Narrator.Speak(text, Loc.Current == AppLanguage.Spanish))
            {
                ShowToggle(_listenButton, true);
                _speechTimer.Start();
            }
        }

        private static void FillStars(StackPanel panel, int stars, double size)
        {
            panel.Children.Clear();
            for (int i = 0; i < 3; i++)
                panel.Children.Add(new StrokeIcon(BlocksChrome.Icons.Star, size, filled: i < stars)
                {
                    Foreground = i < stars ? BlocksChrome.Gold : BlocksChrome.Brush("#D9CFBF"),
                });
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
