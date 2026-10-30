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
                FontSize = 26, FontFamily = ClayTheme.FontDisplay, FontWeight = FontWeight.SemiBold,
                Foreground = ClayTheme.TextPrimary,
            };
            var summary = new TextBlock
            {
                Text = Loc.Tr($"Tenés {earned} de {total} estrellas ★", $"You have {earned} of {total} stars ★"),
                FontSize = 15, FontFamily = ClayTheme.FontBody, Foreground = ClayTheme.TextSecondary, Margin = new Thickness(0, 4, 0, 16),
            };

            var cards = new WrapPanel { Orientation = Orientation.Horizontal };
            int number = 1;
            foreach (var challenge in ChallengeCatalog.All)
            {
                int stars = progress.GetValueOrDefault(challenge.Id);
                var card = new Button
                {
                    Width = 190, Height = 170, Margin = new Thickness(0, 0, 14, 14), Padding = new Thickness(12),
                    Background = stars > 0 ? ClayTheme.SuccessSurface : ClayTheme.Surface,
                    CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                    Content = new StackPanel
                    {
                        Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center,
                        Children =
                        {
                            new TextBlock { Text = challenge.Icon, FontSize = 40, Foreground = ClayTheme.TextPrimary, HorizontalAlignment = HorizontalAlignment.Center },
                            new TextBlock
                            {
                                Text = Loc.Tr($"Reto {number}", $"Challenge {number}"), FontSize = 12, Foreground = ClayTheme.TextMuted,
                                FontFamily = ClayTheme.FontBody, HorizontalAlignment = HorizontalAlignment.Center,
                            },
                            new TextBlock
                            {
                                Text = challenge.Title, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = ClayTheme.TextPrimary,
                                FontFamily = ClayTheme.FontDisplay, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
                                HorizontalAlignment = HorizontalAlignment.Center,
                            },
                            new TextBlock
                            {
                                Text = new string('★', stars) + new string('☆', 3 - stars), FontSize = 20,
                                Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#F2B705")), HorizontalAlignment = HorizontalAlignment.Center,
                            },
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
