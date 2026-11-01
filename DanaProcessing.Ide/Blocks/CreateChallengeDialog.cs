using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// "Crear un reto": a teacher builds the answer with blocks, then opens
    /// this to give it a name, a goal and a hint, and to choose how it's
    /// checked. Saved as a .danareto file in the challenges folder (and,
    /// optionally, a copy anywhere to share with students).
    /// </summary>
    public sealed class CreateChallengeDialog : Window
    {
        private readonly BlockProgram _solution;
        private readonly TextBox _title, _goal, _hint;
        private readonly RadioButton _byShape, _byBlocks, _tiles, _lines;
        private readonly TextBox _author;
        private readonly ChallengeFile? _editing;
        private readonly string? _editingPath;
        private readonly TextBlock _problem;

        /// <summary>The saved file, once saved.</summary>
        public string? SavedPath { get; private set; }

        /// <summary>Edits a challenge already saved (its text and how it's checked; the answer stays).</summary>
        public CreateChallengeDialog(ChallengeFile existing, string path) : this(existing.Solution)
        {
            _editing = existing;
            _editingPath = path;
            Title = Loc.Tr("Cambiar el reto", "Edit the challenge");
            _title.Text = existing.Title;
            _goal.Text = existing.Goal;
            _hint.Text = existing.Hint;
            _author.Text = existing.Author ?? "";
            _byShape.IsChecked = existing.Check == ChallengeCheck.Shape;
            _byBlocks.IsChecked = existing.Check == ChallengeCheck.Blocks;
            _tiles.IsChecked = existing.View == BlockView.Tiles;
            _lines.IsChecked = existing.View == BlockView.Lines;
        }

        public CreateChallengeDialog(BlockProgram solution)
        {
            _solution = solution;
            Title = Loc.Tr("Crear un reto", "Create a challenge");
            Width = 560;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());
            Styles.AddRange(BlocksChrome.Styles());

            bool turtle = solution.Scripts().SelectMany(s => s.Blocks).SelectMany(b => b.SelfAndDescendants())
                                  .Any(b => b.Def.Category == BlockCategory.Turtle);

            _title = Field(Loc.Tr("Por ejemplo: El pentágono", "For example: The pentagon"));
            // The program's own name is a good start -- unless it's one of the names the IDE gives.
            string[] given = { "MiPrograma", "Mi programa", "My program", "Solución de mi reto", "My challenge's answer" };
            _title.Text = solution.ChallengeId is null && !given.Contains(solution.Name) ? solution.Name : "";
            _goal = Field(Loc.Tr("Por ejemplo: Dibujá un pentágono de lados iguales.", "For example: Draw a pentagon with equal sides."), lines: 2);
            _hint = Field(Loc.Tr("Opcional. Por ejemplo: son cinco lados; girá 72.", "Optional. For example: five sides; turn 72."), lines: 2);
            _author = Field(Loc.Tr("Opcional. Por ejemplo: la seño Ana, papá, la abuela", "Optional. For example: Ms. Ana, Dad, Grandma"));
            _author.Text = Classroom.LoadSettings().ChallengeAuthor ?? "";

            _byShape = Choice(Loc.Tr("Que la tortuga dibuje lo mismo que mi solución", "The turtle draws the same as my answer"), "check", turtle);
            _byShape.IsEnabled = turtle;
            _byBlocks = Choice(Loc.Tr("Que use los mismos tipos de bloques (música, juegos...)", "It uses the same kinds of blocks (music, games...)"), "check", !turtle);
            _tiles = Choice(Loc.Tr("Fichas (6 a 8 años)", "Tiles (ages 6-8)"), "view", solution.View == BlockView.Tiles);
            _lines = Choice(Loc.Tr("Renglones (9 a 12 años)", "Lines (ages 9-12)"), "view", solution.View == BlockView.Lines);

            _problem = new TextBlock
            {
                FontFamily = BlocksChrome.FontUi, FontSize = 14, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Brush("#C0392B"),
                TextWrapping = TextWrapping.Wrap, IsVisible = false, Margin = new Thickness(0, 4, 0, 0),
            };

            var save = BlocksChrome.Button(BlocksChrome.Icons.Check, Loc.Tr("Guardar el reto", "Save the challenge"), "go");
            save.Click += async (_, _) => await SaveAsync(share: false);
            var share = BlocksChrome.Button(BlocksChrome.Icons.Forward, Loc.Tr("Guardar y compartir…", "Save and share…"), "outline");
            ToolTip.SetTip(share, Loc.Tr("Además guarda una copia donde elijas (un pendrive, una carpeta compartida, un mensaje) para pasársela a los chicos",
                                         "Also saves a copy wherever you choose (a USB stick, a shared folder, a message) to give to the kids"));
            share.Click += async (_, _) => await SaveAsync(share: true);

            var intro = new TextBlock
            {
                Text = Loc.Tr("Para docentes y familias. Tu programa es la solución: los chicos empiezan con los mismos bloques de comienzo, vacíos, y solo ven los bloques que usaste.",
                              "For teachers and families. Your program is the answer: the kids start with the same start blocks, empty, and only see the blocks you used."),
                FontFamily = BlocksChrome.FontUi, FontSize = 15, Foreground = BlocksChrome.InkSoft, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 14),
            };

            Content = new StackPanel
            {
                Margin = new Thickness(26, 22, 26, 24),
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = Loc.Tr("Crear un reto", "Create a challenge"), FontFamily = BlocksChrome.FontKids, FontSize = 26, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Ink },
                    intro,
                    Label(Loc.Tr("Nombre", "Name")), _title,
                    Label(Loc.Tr("Qué tienen que hacer", "What they have to do")), _goal,
                    Label(Loc.Tr("Pista", "Hint")), _hint,
                    Label(Loc.Tr("Lo hizo", "Made by")), _author,
                    Label(Loc.Tr("Cómo se corrige", "How it's checked")), _byShape, _byBlocks,
                    Label(Loc.Tr("Se abre en", "Opens in")), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18, Children = { _tiles, _lines } },
                    _problem,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 16, 0, 0), Children = { save, share } },
                },
            };
        }

        private static TextBox Field(string watermark, int lines = 1) => new()
        {
            PlaceholderText = watermark, FontFamily = BlocksChrome.FontUi, FontSize = 15,
            AcceptsReturn = false, TextWrapping = lines > 1 ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = lines > 1 ? 56 : 36, CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 7),
        };

        private static TextBlock Label(string text) => new()
        {
            Text = text.ToUpperInvariant(), FontFamily = BlocksChrome.FontUi, FontSize = 12, FontWeight = FontWeight.Bold, LetterSpacing = 1.1,
            Foreground = BlocksChrome.InkMuted, Margin = new Thickness(0, 10, 0, 2),
        };

        private static RadioButton Choice(string text, string group, bool on) => new()
        {
            Content = text, GroupName = group, IsChecked = on, FontFamily = BlocksChrome.FontUi, FontSize = 15, Foreground = BlocksChrome.Ink,
        };

        /// <summary>The challenge as filled in so far.</summary>
        internal ChallengeFile Build()
        {
            var file = ChallengeFile.FromSolution(
                _solution, _title.Text ?? "", _goal.Text ?? "", _hint.Text ?? "",
                _byShape.IsChecked == true ? ChallengeCheck.Shape : ChallengeCheck.Blocks,
                _lines.IsChecked == true ? BlockView.Lines : BlockView.Tiles,
                string.IsNullOrWhiteSpace(_author.Text) ? null : _author.Text.Trim());
            if (_editing is not null)
            {
                // The same challenge, changed: same id (stars earned stay), same palette and start.
                file.Id = _editing.Id;
                file.Palette = _editing.Palette;
                file.Start = _editing.Start;
            }
            return file;
        }

        private async Task SaveAsync(bool share)
        {
            var file = Build();
            if (file.Problem() is { } problem)
            {
                _problem.Text = problem;
                _problem.IsVisible = true;
                return;
            }
            try
            {
                // Remembered for next time: who makes the challenges on this computer.
                var settings = Classroom.LoadSettings();
                settings.ChallengeAuthor = file.Author;
                Classroom.SaveSettings(settings);
                if (_editingPath is not null && File.Exists(_editingPath))
                    File.Delete(_editingPath); // the title may have changed, and with it the file's name
                SavedPath = ChallengeFiles.SaveToFolder(file);
                if (share)
                {
                    var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = Loc.Tr("Guardar una copia para compartir", "Save a copy to share"),
                        SuggestedFileName = ChallengeFiles.SafeName(file.Title) + ChallengeFile.Extension,
                        DefaultExtension = ChallengeFile.Extension.TrimStart('.'),
                        FileTypeChoices = new[] { new FilePickerFileType(Loc.Tr("Reto de DanaProcessing", "DanaProcessing challenge")) { Patterns = new[] { "*" + ChallengeFile.Extension } } },
                    });
                    if (target is not null)
                    {
                        await using var stream = await target.OpenWriteAsync();
                        await using var writer = new StreamWriter(stream);
                        await writer.WriteAsync(file.ToJson());
                    }
                }
                ChallengeCatalog.ReloadTeacher();
                Close();
            }
            catch (Exception e)
            {
                _problem.Text = Loc.Tr("No se pudo guardar: ", "Couldn't save: ") + e.Message;
                _problem.IsVisible = true;
            }
        }
    }
}
