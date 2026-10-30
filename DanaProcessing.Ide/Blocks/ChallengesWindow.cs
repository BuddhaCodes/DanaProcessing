using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// The list of block challenges as big cards, in order, each with the
    /// stars already earned. Every challenge is open: a teacher can send the
    /// class straight to any of them.
    /// </summary>
    public sealed class ChallengesWindow : Window
    {
        /// <summary>A card was picked: open that challenge.</summary>
        public event Action<Challenge>? ChallengeChosen;

        public ChallengesWindow()
        {
            Title = Loc.Tr("Retos con bloques", "Block challenges");
            Width = 860;
            Height = 640;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            var progress = ChallengeProgress.Load();
            int earned = progress.Values.Sum();
            int total = ChallengeCatalog.All.Count * 3;

            var heading = new TextBlock
            {
                Text = Loc.Tr("Elegí un reto", "Pick a challenge"),
                FontSize = 28, FontFamily = BlocksChrome.FontKids, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Ink,
            };
            var summaryStar = new StrokeIcon(BlocksChrome.Icons.Star, 18, filled: true) { Foreground = BlocksChrome.Gold };
            var summary = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 4, 0, 20),
                Children =
                {
                    summaryStar,
                    new TextBlock
                    {
                        Text = Loc.Tr($"{earned} de {total} estrellas", $"{earned} of {total} stars"),
                        FontSize = 16, FontFamily = BlocksChrome.FontUi, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.InkSoft,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            };

            var cards = new WrapPanel { Orientation = Orientation.Horizontal };
            int number = 1;
            foreach (var challenge in ChallengeCatalog.All)
            {
                int stars = progress.GetValueOrDefault(challenge.Id);
                bool lines = challenge.View == BlockView.Lines;
                // Orange for the Fichas challenges, blue for Renglones: the
                // older-kids ones look a step up at a glance.
                var (fill, edge) = lines ? ("#4F9BD9", "#2F78B5") : ("#FF9F43", "#D97A1E");
                var starRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center };
                for (int i = 0; i < 3; i++)
                    starRow.Children.Add(new StrokeIcon(BlocksChrome.Icons.Star, 20, filled: i < stars)
                    {
                        Foreground = i < stars ? BlocksChrome.Gold : BlocksChrome.Brush("#D9CFBF"),
                    });
                var card = new Button
                {
                    Width = 184, Height = 186, Margin = new Thickness(0, 0, 14, 14), Padding = new Thickness(12),
                    Background = stars > 0 ? BlocksChrome.Brush("#EEF8F1") : Brushes.White,
                    CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1.5),
                    BorderBrush = stars > 0 ? BlocksChrome.Brush("#BFE3CB") : BlocksChrome.Line,
                    Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                    Content = new StackPanel
                    {
                        Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center,
                        Children =
                        {
                            new Border
                            {
                                Width = 50, Height = 50, CornerRadius = new CornerRadius(15), HorizontalAlignment = HorizontalAlignment.Center,
                                Background = BlocksChrome.Brush(fill), BoxShadow = BoxShadows.Parse("0 3 0 0 " + edge),
                                Child = new TextBlock
                                {
                                    Text = number.ToString(), FontSize = 22, FontWeight = FontWeight.Bold, FontFamily = BlocksChrome.FontKids,
                                    Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                                },
                            },
                            new TextBlock
                            {
                                Text = challenge.Title, FontSize = 17, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Ink,
                                FontFamily = lines ? BlocksChrome.FontUi : BlocksChrome.FontKids, TextAlignment = TextAlignment.Center,
                                TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0),
                            },
                            new TextBlock
                            {
                                Text = lines ? Loc.Tr("RENGLONES", "LINES") : Loc.Tr("FICHAS", "TILES"), FontSize = 11, LetterSpacing = 1.2,
                                FontWeight = FontWeight.Bold, FontFamily = BlocksChrome.FontUi, Foreground = BlocksChrome.InkMuted,
                                HorizontalAlignment = HorizontalAlignment.Center,
                            },
                            starRow,
                        },
                    },
                };
                ToolTip.SetTip(card, challenge.Goal);
                card.Click += (_, _) =>
                {
                    ChallengeChosen?.Invoke(challenge);
                    Close();
                };
                cards.Children.Add(card);
                number++;
            }

            var content = new ScrollViewer
            {
                Padding = new Thickness(28, 24, 14, 24),
                Content = new StackPanel { Children = { heading, summary, cards } },
            };
            ClayChrome.Apply(this, content, canMinimize: false, canMaximize: false);
        }
    }
}
