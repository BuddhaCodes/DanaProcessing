using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;
using SkiaSharp;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// "Ordenar el código": real C# lines, shuffled. Tap a line in the pile
    /// to add it to the program; tap a program line to pick it, then move it
    /// up or down or send it back. ▶ Probar runs it and shows the drawing
    /// over the yellow path.
    /// </summary>
    public sealed class ParsonsWindow : Window
    {
        private ParsonsPuzzle _puzzle;
        private readonly List<string> _pile = new();
        private readonly List<string> _program = new();
        private int _selected = -1;

        private readonly StackPanel _puzzleTabs, _pileList, _programList;
        private readonly TextBlock _title, _goal, _resultText;
        private readonly Border _result, _resultIcon;
        private readonly Image _preview;
        private readonly Button _up, _down, _back;

        public ParsonsWindow(string? puzzleId = null)
        {
            Title = Loc.Tr("Ordenar el código", "Put the code in order");
            Width = 1120;
            Height = 720;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());
            Styles.AddRange(BlocksChrome.Styles());

            _puzzle = ParsonsCatalog.Get(puzzleId) ?? ParsonsCatalog.All[0];

            _puzzleTabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            _title = new TextBlock { FontFamily = BlocksChrome.FontUi, FontSize = 24, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Ink };
            _goal = new TextBlock { FontFamily = BlocksChrome.FontUi, FontSize = 17, Foreground = BlocksChrome.InkSoft, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };

            _pileList = new StackPanel { Spacing = 8 };
            _programList = new StackPanel { Spacing = 2 };

            _up = BlocksChrome.Button("M12 19V5M5 12l7-7 7 7", null);
            ToolTip.SetTip(_up, Loc.Tr("Subir la línea", "Move the line up"));
            _up.Click += (_, _) => Move(-1);
            _down = BlocksChrome.Button("M12 5v14M5 12l7 7 7-7", null);
            ToolTip.SetTip(_down, Loc.Tr("Bajar la línea", "Move the line down"));
            _down.Click += (_, _) => Move(+1);
            _back = BlocksChrome.Button(BlocksChrome.Icons.Undo, Loc.Tr("A la pila", "To the pile"));
            ToolTip.SetTip(_back, Loc.Tr("Devolver la línea a la pila", "Send the line back to the pile"));
            _back.Click += (_, _) => SendBack();

            var run = BlocksChrome.Button(BlocksChrome.Icons.Play, Loc.Tr("Probar", "Try it"), "go");
            run.Click += (_, _) => Try();
            var restart = BlocksChrome.Button(BlocksChrome.Icons.Retry, Loc.Tr("Empezar de nuevo", "Start over"));
            restart.Click += (_, _) => Load(_puzzle);

            _preview = new Image { Width = 330, Height = 220, Stretch = Stretch.Uniform };
            _resultIcon = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top };
            _resultText = new TextBlock { FontFamily = BlocksChrome.FontUi, FontSize = 16, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Ink, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            var resultRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Grid.SetColumn(_resultText, 1);
            resultRow.Children.Add(_resultIcon);
            resultRow.Children.Add(_resultText);
            _result = new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 10), IsVisible = false, Child = resultRow };

            // --- the three columns: pile | program | drawing ---
            var pileColumn = Column(Loc.Tr("Las líneas sueltas", "The loose lines"), Loc.Tr("Tocá una para sumarla a tu programa", "Tap one to add it to your program"),
                new ScrollViewer { Content = _pileList }, light: true);
            var programTools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { _up, _down, _back } };
            var programColumn = Column(Loc.Tr("Tu programa", "Your program"), Loc.Tr("Tocá una línea para elegirla", "Tap a line to pick it"),
                new DockPanel
                {
                    Children =
                    {
                        Docked(new Border { Padding = new Thickness(0, 0, 0, 8), Child = programTools }, Dock.Top),
                        new Border
                        {
                            Background = BlocksChrome.Brush("#221F1C"), CornerRadius = new CornerRadius(14), Padding = new Thickness(6, 10),
                            Child = new ScrollViewer { Content = _programList, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto },
                        },
                    },
                }, light: false);
            var drawColumn = Column(Loc.Tr("El dibujo", "The drawing"), Loc.Tr("El camino amarillo es lo que tiene que dibujar", "The yellow path is what it has to draw"),
                new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new Border { Background = Brushes.White, CornerRadius = new CornerRadius(14), BorderBrush = BlocksChrome.Line, BorderThickness = new Thickness(1), Padding = new Thickness(6), Child = _preview },
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { run, restart } },
                        _result,
                    },
                }, light: true);

            var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*,362"), Margin = new Thickness(0, 14, 0, 0) };
            Grid.SetColumn(programColumn, 1);
            Grid.SetColumn(drawColumn, 2);
            columns.Children.Add(pileColumn);
            columns.Children.Add(programColumn);
            columns.Children.Add(drawColumn);

            var header = new StackPanel
            {
                Spacing = 4,
                Children = { _puzzleTabs, new Border { Height = 8 }, _title, _goal },
            };
            var root = new DockPanel { Margin = new Thickness(24, 18, 24, 20) };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            root.Children.Add(columns);
            ClayChrome.Apply(this, root, canMinimize: true, canMaximize: true);

            KeyDown += OnKey;
            Load(_puzzle);
        }

        private static Control Docked(Control c, Dock dock)
        {
            DockPanel.SetDock(c, dock);
            return c;
        }

        private static Control Column(string title, string help, Control body, bool light)
        {
            var head = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 8),
                Children =
                {
                    new TextBlock { Text = title.ToUpperInvariant(), FontFamily = BlocksChrome.FontUi, FontSize = 12.5, FontWeight = FontWeight.Bold, LetterSpacing = 1.2, Foreground = BlocksChrome.InkMuted },
                    new TextBlock { Text = help, FontFamily = BlocksChrome.FontUi, FontSize = 13.5, Foreground = BlocksChrome.InkMuted },
                },
            };
            var panel = new DockPanel { Margin = new Thickness(0, 0, 16, 0) };
            DockPanel.SetDock(head, Dock.Top);
            panel.Children.Add(head);
            panel.Children.Add(body);
            return panel;
        }

        // =================================================================
        // Puzzles
        // =================================================================

        private void Load(ParsonsPuzzle puzzle)
        {
            _puzzle = puzzle;
            _pile.Clear();
            _pile.AddRange(puzzle.Shuffled(new Random()));
            _program.Clear();
            _program.Add(puzzle.Lines[0]);
            _selected = -1;
            _result.IsVisible = false;
            _title.Text = puzzle.Title;
            _goal.Text = puzzle.Goal;
            ShowTabs();
            ShowLines();
            ShowPreview(Array.Empty<TurtleLine>());
        }

        private void ShowTabs()
        {
            var progress = ChallengeProgress.Load();
            _puzzleTabs.Children.Clear();
            int n = 1;
            foreach (var p in ParsonsCatalog.All)
            {
                bool current = p == _puzzle, done = progress.GetValueOrDefault(p.ProgressId) > 0;
                var tab = new Button
                {
                    Content = new TextBlock { Text = (n++).ToString(), FontFamily = BlocksChrome.FontUi, FontWeight = FontWeight.Bold, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center },
                    Width = 38, Height = 38, CornerRadius = new CornerRadius(19), Padding = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                    Background = current ? BlocksChrome.Brush("#4F9BD9") : done ? BlocksChrome.Brush("#E6F5EB") : Brushes.White,
                    Foreground = current ? Brushes.White : done ? BlocksChrome.Brush("#2C8550") : BlocksChrome.InkSoft,
                    BorderBrush = done && !current ? BlocksChrome.Brush("#BFE3CB") : BlocksChrome.Line, BorderThickness = new Thickness(current ? 0 : 1.5),
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                ToolTip.SetTip(tab, p.Title);
                var puzzle = p;
                tab.Click += (_, _) => Load(puzzle);
                _puzzleTabs.Children.Add(tab);
            }
        }

        // =================================================================
        // Lines
        // =================================================================

        private void ShowLines()
        {
            _pileList.Children.Clear();
            for (int i = 0; i < _pile.Count; i++)
            {
                int index = i;
                var card = new Button
                {
                    Content = CodeText(_pile[i], dark: false),
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = Brushes.White, BorderBrush = BlocksChrome.Line, BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 9), Cursor = new Cursor(StandardCursorType.Hand),
                };
                card.Click += (_, _) => AddFromPile(index);
                _pileList.Children.Add(card);
            }
            if (_pile.Count == 0)
                _pileList.Children.Add(new TextBlock { Text = Loc.Tr("¡No quedan líneas!", "No lines left!"), FontFamily = BlocksChrome.FontUi, FontSize = 15, Foreground = BlocksChrome.InkMuted });

            _programList.Children.Clear();
            var indents = ParsonsChecker.Indents(_program);
            if (_puzzle.Fields.Count > 0)
                foreach (var f in _puzzle.Fields)
                    _programList.Children.Add(Row(null, f, 0, locked: true));
            for (int i = 0; i < _program.Count; i++)
                _programList.Children.Add(Row(i, _program[i], indents[i], locked: i == 0));

            _up.IsEnabled = _selected > 1;
            _down.IsEnabled = _selected >= 1 && _selected < _program.Count - 1;
            _back.IsEnabled = _selected >= 1;
        }

        private Control Row(int? index, string line, int indent, bool locked)
        {
            bool selected = index is { } i && i == _selected;
            var number = new TextBlock
            {
                Text = index is { } n ? (n + 1).ToString() : "", Width = 26, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 0, 12, 0),
                FontFamily = BlocksChrome.FontCode, FontSize = 13, Foreground = BlocksChrome.Brush("#7D7466"), VerticalAlignment = VerticalAlignment.Center,
            };
            var text = CodeText(line, dark: true);
            text.Margin = new Thickness(indent * 26, 0, 0, 0);
            var row = new DockPanel { Children = { Docked(number, Dock.Left), text } };
            var border = new Border
            {
                Child = row, Padding = new Thickness(4, 6), CornerRadius = new CornerRadius(8),
                Background = selected ? BlocksChrome.Brush("#4DFFD35E") : Brushes.Transparent,
                BorderBrush = selected ? BlocksChrome.Brush("#FFD35E") : Brushes.Transparent, BorderThickness = new Thickness(2, 0, 0, 0),
                Cursor = locked ? Cursor.Default : new Cursor(StandardCursorType.Hand), Opacity = locked && index is null ? 0.7 : 1,
            };
            if (!locked && index is { } k)
                border.PointerPressed += (_, e) =>
                {
                    _selected = _selected == k ? -1 : k;
                    ShowLines();
                    e.Handled = true;
                };
            return border;
        }

        /// <summary>A line of C#, colored like the Lines view's code column.</summary>
        private static TextBlock CodeText(string line, bool dark)
        {
            var text = new TextBlock { FontFamily = BlocksChrome.FontCode, FontSize = 15.5, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap };
            var inlines = new InlineCollection();
            foreach (var (part, kind) in CSharpTokens.Split(line.Trim()))
                inlines.Add(new Run(part)
                {
                    Foreground = new SolidColorBrush(Color(kind, dark)),
                    FontWeight = kind == CSharpTokens.Kind.Keyword ? FontWeight.SemiBold : FontWeight.Normal,
                });
            text.Inlines = inlines;
            return text;
        }

        private static Avalonia.Media.Color Color(CSharpTokens.Kind kind, bool dark) => (kind, dark) switch
        {
            (CSharpTokens.Kind.Keyword, true) => Avalonia.Media.Color.FromRgb(242, 140, 108),
            (CSharpTokens.Kind.Number, true) => Avalonia.Media.Color.FromRgb(184, 214, 130),
            (CSharpTokens.Kind.Call, true) => Avalonia.Media.Color.FromRgb(132, 196, 236),
            (CSharpTokens.Kind.Punctuation, true) => Avalonia.Media.Color.FromRgb(170, 162, 148),
            (_, true) => Avalonia.Media.Color.FromRgb(233, 228, 218),
            (CSharpTokens.Kind.Keyword, false) => Avalonia.Media.Color.FromRgb(196, 74, 40),
            (CSharpTokens.Kind.Number, false) => Avalonia.Media.Color.FromRgb(46, 125, 60),
            (CSharpTokens.Kind.Call, false) => Avalonia.Media.Color.FromRgb(30, 100, 170),
            (CSharpTokens.Kind.Punctuation, false) => Avalonia.Media.Color.FromRgb(120, 112, 100),
            _ => Avalonia.Media.Color.FromRgb(43, 38, 32),
        };

        private void AddFromPile(int index)
        {
            var line = _pile[index];
            _pile.RemoveAt(index);
            // After the picked line, or at the end.
            int at = _selected >= 0 ? _selected + 1 : _program.Count;
            _program.Insert(at, line);
            _selected = at;
            _result.IsVisible = false;
            ShowLines();
        }

        private void Move(int delta)
        {
            int to = _selected + delta;
            if (_selected < 1 || to < 1 || to >= _program.Count)
                return;
            (_program[_selected], _program[to]) = (_program[to], _program[_selected]);
            _selected = to;
            _result.IsVisible = false;
            ShowLines();
        }

        private void SendBack()
        {
            if (_selected < 1)
                return;
            _pile.Add(_program[_selected]);
            _program.RemoveAt(_selected);
            _selected = Math.Min(_selected, _program.Count - 1);
            if (_selected == 0)
                _selected = -1;
            _result.IsVisible = false;
            ShowLines();
        }

        private void OnKey(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up when e.KeyModifiers.HasFlag(KeyModifiers.Alt): Move(-1); e.Handled = true; break;
                case Key.Down when e.KeyModifiers.HasFlag(KeyModifiers.Alt): Move(+1); e.Handled = true; break;
                case Key.Delete or Key.Back: SendBack(); e.Handled = true; break;
            }
        }

        // =================================================================
        // Trying it
        // =================================================================

        private void Try()
        {
            var result = ParsonsChecker.Check(_puzzle, _program, out var drawn);
            ShowPreview(drawn);
            _result.IsVisible = true;
            _result.Background = BlocksChrome.Brush(result.Passed ? "#E6F5EB" : "#FFF1E0");
            _resultIcon.Background = result.Passed ? BlocksChrome.Go : BlocksChrome.Brush("#FF9F43");
            _resultIcon.Child = new StrokeIcon(result.Passed ? BlocksChrome.Icons.Check : BlocksChrome.Icons.Retry, 18, thickness: 2.8)
            {
                Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center,
            };
            _resultText.Text = result.Message;
            if (result.Passed)
            {
                ChallengeProgress.Record(_puzzle.ProgressId, 3);
                ShowTabs();
            }
        }

        /// <summary>The yellow path and what the turtle drew, on a small canvas.</summary>
        private void ShowPreview(IReadOnlyList<TurtleLine> drawn)
        {
            _preview.Source = new Bitmap(new MemoryStream(RenderPreview(_puzzle, drawn)));
        }

        internal static byte[] RenderPreview(ParsonsPuzzle puzzle, IReadOnlyList<TurtleLine> drawn)
        {
            using var surface = SKSurface.Create(new SKImageInfo(600, 400));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            using (var guide = new SKPaint { Color = new SKColor(255, 196, 61, 110), StrokeWidth = 18, StrokeCap = SKStrokeCap.Round, IsAntialias = true, Style = SKPaintStyle.Stroke })
                foreach (var l in puzzle.Shape.TargetLines())
                    canvas.DrawLine(l.X1, l.Y1, l.X2, l.Y2, guide);
            foreach (var l in drawn)
            {
                using var pen = new SKPaint
                {
                    Color = new SKColor(l.Color.R, l.Color.G, l.Color.B), StrokeWidth = Math.Max(2, l.Width), StrokeCap = SKStrokeCap.Round,
                    IsAntialias = true, Style = SKPaintStyle.Stroke,
                };
                canvas.DrawLine(l.X1, l.Y1, l.X2, l.Y2, pen);
            }
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
    }
}
