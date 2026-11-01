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

        /// <summary>"Crear un reto" with no answer to build it from: open an empty blocks tab for the grown-up to build one.</summary>
        public event Action? NewSolutionRequested;

        private readonly Func<BlockProgram?> _current;

        public ChallengesWindow(Func<BlockProgram?>? currentProgram = null)
        {
            _current = currentProgram ?? (() => null);
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

            // Challenges a teacher or a family made (Documents/DanaProcessing/Retos).
            var madeCards = new WrapPanel { Orientation = Orientation.Horizontal };
            number = 1;
            foreach (var challenge in ChallengeCatalog.Teacher)
                madeCards.Children.Add(MadeCard(challenge, number++, progress));

            var create = BlocksChrome.Button(BlocksChrome.Icons.Star, Loc.Tr("Crear un reto", "Create a challenge"), "go");
            ToolTip.SetTip(create, Loc.Tr("Para docentes y familias: armás la solución con bloques y la convertís en un reto",
                                          "For teachers and families: build the answer with blocks and turn it into a challenge"));
            create.Click += async (_, _) => await CreateAsync();
            var open = BlocksChrome.Button(BlocksChrome.Icons.Forward, Loc.Tr("Abrir retos…", "Open challenges…"), "outline");
            ToolTip.SetTip(open, Loc.Tr("Un archivo .danareto o .danaretos que te pasaron", "A .danareto or .danaretos file someone gave you"));
            open.Click += async (_, _) => await ImportAsync();
            var share = BlocksChrome.Button(null, Loc.Tr("Compartir todos…", "Share them all…"));
            ToolTip.SetTip(share, Loc.Tr("Guarda todos estos retos en un solo archivo, para pasarlos a otra compu", "Saves all these challenges in one file, to take them to another computer"));
            share.IsEnabled = ChallengeCatalog.Teacher.Count > 0;
            share.Click += async (_, _) => await ExportPackAsync();
            var folder = BlocksChrome.Button(null, Loc.Tr("Ver la carpeta", "Show the folder"));
            folder.Click += (_, _) => ShowFolder();
            var madeHeading = new TextBlock
            {
                Text = Loc.Tr("Retos hechos para vos", "Challenges made for you"), FontSize = 22, FontFamily = BlocksChrome.FontKids,
                FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Ink, Margin = new Thickness(0, 18, 0, 4),
            };
            var madeNote = new TextBlock
            {
                Text = ChallengeCatalog.Teacher.Count == 0
                    ? Loc.Tr("Docentes y familias pueden inventar retos: armás la solución con bloques, tocás «Crear un reto», y listo. Después se pasan en un archivo.",
                             "Teachers and families can invent challenges: build the answer with blocks, press \"Create a challenge\", done. Then pass them on in a file.")
                    : Loc.Tr("Los inventaron tu docente o tu familia. Con el lápiz se cambia un reto; con el tacho, se quita.", "Your teacher or your family made these. The pencil edits a challenge; the bin removes it."),
                FontSize = 15, FontFamily = BlocksChrome.FontUi, Foreground = BlocksChrome.InkSoft, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
            };
            var madeButtons = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14), Children = { create, Gap(), open, Gap(), share, Gap(), folder } };
            var made = new StackPanel { Children = { madeHeading, madeNote, madeButtons, madeCards } };
            var builtIn = new StackPanel { Children = { cards } };

            var content = new ScrollViewer
            {
                Padding = new Thickness(28, 24, 14, 24),
                // Challenges made for this child come first, when there are any.
                Content = new StackPanel
                {
                    Children = { heading, summary, ChallengeCatalog.Teacher.Count > 0 ? made : builtIn, ChallengeCatalog.Teacher.Count > 0 ? builtIn : made },
                },
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

        private static Control Gap() => new Border { Width = 8, Height = 8 };

        /// <summary>A challenge someone made, with its author and buttons to change or remove it.</summary>
        private Control MadeCard(Challenge challenge, int number, System.Collections.Generic.Dictionary<string, int> progress)
        {
            var card = Card(challenge, number, progress);
            card.Height = 206; // room for "de ..." too
            if (challenge.Author is { } author && card.Content is StackPanel inner)
                inner.Children.Insert(2, new TextBlock
                {
                    Text = Loc.Tr("de ", "by ") + author, FontSize = 12.5, FontFamily = BlocksChrome.FontUi, Foreground = BlocksChrome.InkMuted,
                    HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                });
            var edit = SmallButton("M4 20h4L19 9l-4-4L4 16z M14 6l4 4", Loc.Tr("Cambiar el reto", "Edit the challenge"));
            edit.Click += async (_, _) => await EditAsync(challenge);
            var remove = SmallButton("M4 7h16 M9 7V4h6v3 M6 7l1 13h10l1-13 M10 11v5 M14 11v5", Loc.Tr("Quitar el reto", "Remove the challenge"));
            remove.Click += async (_, _) =>
            {
                if (await Editor.ConfirmDialog.ShowAsync(this, Loc.Tr("Quitar el reto", "Remove the challenge"),
                        Loc.Tr($"¿Quitar «{challenge.Title}»? Las estrellas ganadas se guardan.", $"Remove \"{challenge.Title}\"? Stars earned are kept."),
                        Loc.Tr("Quitar", "Remove"), Loc.Tr("Cancelar", "Cancel")))
                {
                    ChallengeFiles.Delete(challenge);
                    Reopen();
                }
            };
            var tools = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 20, 0), Children = { edit, remove },
            };
            return new Grid { Children = { card, tools } };
        }

        private static Button SmallButton(string icon, string tip)
        {
            var b = new Button
            {
                Content = new StrokeIcon(icon, 16, thickness: 2.2) { HorizontalAlignment = HorizontalAlignment.Center },
                Width = 30, Height = 30, Padding = new Thickness(0), CornerRadius = new CornerRadius(15),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                Background = BlocksChrome.Well, Foreground = BlocksChrome.InkSoft, BorderThickness = new Thickness(0),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            };
            ToolTip.SetTip(b, tip);
            return b;
        }

        /// <summary>The same window again, after something changed in the list.</summary>
        private void Reopen()
        {
            var again = new ChallengesWindow(_current);
            if (ChallengeChosen is { } chosen)
                again.ChallengeChosen += chosen;
            if (NewSolutionRequested is { } fresh)
                again.NewSolutionRequested += fresh;
            if (Owner is Window owner)
                again.Show(owner);
            else
                again.Show();
            Close();
        }

        private async System.Threading.Tasks.Task CreateAsync()
        {
            var program = _current();
            bool hasAnswer = program is not null && program.Scripts().Any(st => st.Blocks.Count > 1);
            if (!hasAnswer)
            {
                // No answer yet: an empty blocks tab to build it in, then come back.
                NewSolutionRequested?.Invoke();
                Close();
                return;
            }
            var dialog = new CreateChallengeDialog(program!);
            await dialog.ShowDialog(this);
            if (dialog.SavedPath is not null)
                Reopen();
        }

        private async System.Threading.Tasks.Task EditAsync(Challenge challenge)
        {
            if (ChallengeFiles.Load(challenge) is not { } file || challenge.SourceFile is not { } path)
                return;
            var dialog = new CreateChallengeDialog(file, path);
            await dialog.ShowDialog(this);
            if (dialog.SavedPath is not null)
                Reopen();
        }

        private async System.Threading.Tasks.Task ExportPackAsync()
        {
            var target = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = Loc.Tr("Compartir todos los retos", "Share all the challenges"),
                SuggestedFileName = Loc.Tr("mis-retos", "my-challenges") + ChallengeFiles.PackExtension,
                DefaultExtension = ChallengeFiles.PackExtension.TrimStart('.'),
            });
            if (target is null)
                return;
            await using var stream = await target.OpenWriteAsync();
            await using var writer = new System.IO.StreamWriter(stream);
            await writer.WriteAsync(ChallengeFiles.ExportPack());
        }

        private async System.Threading.Tasks.Task ImportAsync()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = Loc.Tr("Abrir un reto", "Open a challenge"),
                AllowMultiple = false,
                FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType(Loc.Tr("Retos de DanaProcessing", "DanaProcessing challenges")) { Patterns = new[] { "*" + ChallengeFile.Extension, "*" + ChallengeFiles.PackExtension } } },
            });
            if (files.Count == 0)
                return;
            var path = files[0].Path.LocalPath;
            try
            {
                var imported = ChallengeFiles.ImportAny(path);
                if (imported.Count == 1)
                {
                    ChallengeChosen?.Invoke(imported[0]);
                    Close();
                }
                else
                {
                    Reopen(); // a whole set: show them in the list
                }
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
