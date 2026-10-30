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

            Styles.AddRange(BlocksChrome.Styles());
            ChallengeCatalog.ReloadTeacher();

            var cards = new WrapPanel { Orientation = Orientation.Horizontal };
            int number = 1;
            foreach (var challenge in ChallengeCatalog.All)
                cards.Children.Add(Card(challenge, number++, progress));

            // Challenges teachers made (Documents/DanaProcessing/Retos).
            var teacherCards = new WrapPanel { Orientation = Orientation.Horizontal };
            number = 1;
            foreach (var challenge in ChallengeCatalog.Teacher)
                teacherCards.Children.Add(Card(challenge, number++, progress));
            var open = BlocksChrome.Button(BlocksChrome.Icons.Forward, Loc.Tr("Abrir un reto…", "Open a challenge…"), "outline");
            ToolTip.SetTip(open, Loc.Tr("Un archivo .danareto que te pasó tu docente", "A .danareto file your teacher gave you"));
            open.Click += async (_, _) => await ImportAsync();
            var folder = BlocksChrome.Button(null, Loc.Tr("Ver la carpeta", "Show the folder"));
            folder.Click += (_, _) => ShowFolder();
            var teacherHeading = new TextBlock
            {
                Text = Loc.Tr("Retos de tu docente", "Your teacher's challenges"), FontSize = 22, FontFamily = BlocksChrome.FontKids,
                FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Ink, Margin = new Thickness(0, 18, 0, 4),
            };
            var teacherNote = new TextBlock
            {
                Text = ChallengeCatalog.Teacher.Count == 0
                    ? Loc.Tr("Todavía no hay. Un docente arma la solución con bloques y usa Archivo → Crear un reto con estos bloques.",
                             "None yet. A teacher builds the answer with blocks and uses File → Create a challenge from these blocks.")
                    : Loc.Tr("Hechos con DanaProcessing, a la medida de tu clase.", "Made with DanaProcessing, for your class."),
                FontSize = 15, FontFamily = BlocksChrome.FontUi, Foreground = BlocksChrome.InkSoft, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
            };
            var teacherButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 14), Children = { open, folder } };

            var content = new ScrollViewer
            {
                Padding = new Thickness(28, 24, 14, 24),
                Content = new StackPanel { Children = { heading, summary, cards, teacherHeading, teacherNote, teacherButtons, teacherCards } },
            };
            ClayChrome.Apply(this, content, canMinimize: false, canMaximize: false);
        }

        private Button Card(Challenge challenge, int number, System.Collections.Generic.Dictionary<string, int> progress)
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
            return card;
        }

        private async System.Threading.Tasks.Task ImportAsync()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = Loc.Tr("Abrir un reto", "Open a challenge"),
                AllowMultiple = false,
                FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType(Loc.Tr("Reto de DanaProcessing", "DanaProcessing challenge")) { Patterns = new[] { "*" + ChallengeFile.Extension } } },
            });
            if (files.Count == 0)
                return;
            var path = files[0].Path.LocalPath;
            try
            {
                var challenge = ChallengeFiles.Import(path);
                ChallengeChosen?.Invoke(challenge);
                Close();
            }
            catch (Exception e)
            {
                Title = e.Message;
            }
        }

        private static void ShowFolder()
        {
            try
            {
                System.IO.Directory.CreateDirectory(ChallengeFiles.Folder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ChallengeFiles.Folder) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // no file manager: nothing to show
            }
        }
    }
}
