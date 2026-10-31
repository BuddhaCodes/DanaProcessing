using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// Modo aula: the class without a server. "Yo" (the child: name, hand in
    /// progress, share to the gallery), "Galería" (programs shared into a
    /// folder, with a picture each) and "Docente" (every hand-in in a folder,
    /// as a table; CSV for a spreadsheet; click a star to see that program).
    /// </summary>
    public sealed class ClassroomWindow : Window
    {
        /// <summary>Open this program in a new blocks tab.</summary>
        public event Action<BlockProgram>? OpenProgram;

        private readonly Func<BlockProgram?> _current;
        private readonly Classroom.Settings _settings = Classroom.LoadSettings();
        private readonly ContentControl _page = new();
        private readonly Button _meTab, _galleryTab, _teacherTab;
        private string? _classFolder;

        public ClassroomWindow(Func<BlockProgram?> currentProgram, string? startOn = null)
        {
            _current = currentProgram;
            Title = Loc.Tr("Modo aula", "Classroom");
            Width = 980;
            Height = 680;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());
            Styles.AddRange(BlocksChrome.Styles());

            _meTab = BlocksChrome.Button(null, Loc.Tr("Yo", "Me"), "seg");
            _galleryTab = BlocksChrome.Button(null, Loc.Tr("Galería", "Gallery"), "seg");
            _teacherTab = BlocksChrome.Button(null, Loc.Tr("Docente", "Teacher"), "seg");
            _meTab.Click += (_, _) => Show("me");
            _galleryTab.Click += (_, _) => Show("gallery");
            _teacherTab.Click += (_, _) => Show("teacher");
            var tabs = new Border
            {
                Background = BlocksChrome.Well, CornerRadius = new CornerRadius(12), Padding = new Thickness(3), HorizontalAlignment = HorizontalAlignment.Left,
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { _meTab, _galleryTab, _teacherTab } },
            };
            var heading = new TextBlock { Text = Loc.Tr("Modo aula", "Classroom"), FontFamily = BlocksChrome.FontKids, FontSize = 28, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Ink };
            var note = Text(Loc.Tr("Sin internet ni cuentas: todo son archivos que se pasan en un pendrive o en una carpeta compartida.",
                                   "No internet, no accounts: everything is files, passed on a USB stick or in a shared folder."), 14.5, BlocksChrome.InkMuted);
            var header = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 16), Children = { heading, note, new Border { Height = 4 }, tabs } };

            var root = new DockPanel { Margin = new Thickness(28, 20, 28, 22) };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            root.Children.Add(_page);
            ClayChrome.Apply(this, root, canMinimize: true, canMaximize: true);
            Show(startOn ?? "me");
        }

        private void Show(string page)
        {
            _meTab.Classes.Set("on", page == "me");
            _galleryTab.Classes.Set("on", page == "gallery");
            _teacherTab.Classes.Set("on", page == "teacher");
            _page.Content = page switch
            {
                "gallery" => GalleryPage(),
                "teacher" => TeacherPage(),
                _ => MePage(),
            };
        }

        private static TextBlock Text(string text, double size, IBrush color, bool bold = false) => new()
        {
            Text = text, FontFamily = BlocksChrome.FontUi, FontSize = size, Foreground = color, TextWrapping = TextWrapping.Wrap,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
        };

        private static TextBlock Label(string text) => new()
        {
            Text = text.ToUpperInvariant(), FontFamily = BlocksChrome.FontUi, FontSize = 12, FontWeight = FontWeight.Bold, LetterSpacing = 1.1,
            Foreground = BlocksChrome.InkMuted, Margin = new Thickness(0, 14, 0, 4),
        };

        private static Border Card(Control child) => new()
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(18), Padding = new Thickness(20, 16),
            BoxShadow = BoxShadows.Parse("0 0 0 1 #EAE3D7, 0 4 14 0 #10000000"), Child = child,
        };

        private TextBlock Status() => new() { FontFamily = BlocksChrome.FontUi, FontSize = 14.5, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Brush("#2C8550"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

        // =================================================================
        // Yo
        // =================================================================

        private Control MePage()
        {
            var name = new TextBox
            {
                Text = _settings.StudentName, PlaceholderText = Loc.Tr("Tu nombre", "Your name"), FontFamily = BlocksChrome.FontKids, FontSize = 20,
                Width = 320, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 8),
            };
            name.TextChanged += (_, _) =>
            {
                _settings.StudentName = name.Text ?? "";
                Classroom.SaveSettings(_settings);
            };

            var progress = ChallengeProgress.Load();
            int solved = progress.Count(kv => kv.Value > 0 && !kv.Key.StartsWith("parsons:", StringComparison.Ordinal));
            int stars = progress.Where(kv => !kv.Key.StartsWith("parsons:", StringComparison.Ordinal)).Sum(kv => kv.Value);
            int puzzles = progress.Count(kv => kv.Key.StartsWith("parsons:", StringComparison.Ordinal));
            var summary = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0),
                Children =
                {
                    new StrokeIcon(BlocksChrome.Icons.Star, 20, filled: true) { Foreground = BlocksChrome.Gold },
                    Text(Loc.Tr($"{stars} estrellas · {solved} retos resueltos · {puzzles} códigos ordenados",
                                $"{stars} stars · {solved} challenges solved · {puzzles} code puzzles"), 16, BlocksChrome.InkSoft, bold: true),
                },
            };

            var status = Status();
            var handIn = BlocksChrome.Button(BlocksChrome.Icons.Forward, Loc.Tr("Entregar mi avance…", "Hand in my progress…"), "go");
            ToolTip.SetTip(handIn, Loc.Tr("Guarda un archivo con tus estrellas y los programas de cada reto, para tu docente",
                                          "Saves a file with your stars and each challenge's program, for your teacher"));
            handIn.Click += async (_, _) => status.Text = await HandInAsync() ?? "";
            var share = BlocksChrome.Button(BlocksChrome.Icons.Tiles, Loc.Tr("Compartir lo que estoy haciendo", "Share what I'm making"), "outline");
            ToolTip.SetTip(share, Loc.Tr("Pone el programa de la pestaña de bloques en la galería de la clase", "Puts the blocks tab's program in the class gallery"));
            share.IsEnabled = _current() is not null;
            share.Click += (_, _) => status.Text = ShareCurrent() ?? "";

            var card = Card(new StackPanel
            {
                Children =
                {
                    Label(Loc.Tr("¿Cómo te llamás?", "What's your name?")), name,
                    Label(Loc.Tr("Tu avance", "Your progress")), summary,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 18, 0, 0), Children = { handIn, share } },
                    status,
                },
            });
            card.VerticalAlignment = VerticalAlignment.Top;
            return card;
        }

        private bool NeedsName(out string message)
        {
            message = Loc.Tr("Primero escribí tu nombre.", "Write your name first.");
            return string.IsNullOrWhiteSpace(_settings.StudentName);
        }

        private async Task<string?> HandInAsync()
        {
            if (NeedsName(out var ask))
                return ask;
            var report = Classroom.CurrentReport(_settings.StudentName);
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Loc.Tr("Entregar mi avance", "Hand in my progress"),
                SuggestedFileName = Classroom.ReportFileName(report),
                DefaultExtension = Classroom.ProgressReport.Extension.TrimStart('.'),
                FileTypeChoices = new[] { new FilePickerFileType(Loc.Tr("Avance de DanaProcessing", "DanaProcessing progress")) { Patterns = new[] { "*" + Classroom.ProgressReport.Extension } } },
            });
            if (file is null)
                return null;
            try
            {
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(report.ToJson());
                return Loc.Tr("¡Listo! Pasale ese archivo a tu docente.", "Done! Give that file to your teacher.");
            }
            catch (Exception e)
            {
                return Loc.Tr("No se pudo guardar: ", "Couldn't save: ") + e.Message;
            }
        }

        private string? ShareCurrent()
        {
            if (NeedsName(out var ask))
                return ask;
            if (_current() is not { } program)
                return Loc.Tr("Abrí una pestaña de bloques para compartirla.", "Open a blocks tab to share it.");
            try
            {
                Classroom.Share(program, _settings.Gallery, _settings.StudentName);
                return Loc.Tr("¡Está en la galería!", "It's in the gallery!");
            }
            catch (Exception e)
            {
                return Loc.Tr("No se pudo compartir: ", "Couldn't share: ") + e.Message;
            }
        }

        // =================================================================
        // Galería
        // =================================================================

        private Control GalleryPage()
        {
            var folder = Text(_settings.Gallery, 13.5, BlocksChrome.InkMuted);
            var change = BlocksChrome.Button(null, Loc.Tr("Cambiar la carpeta…", "Change the folder…"), "outline");
            ToolTip.SetTip(change, Loc.Tr("Una carpeta compartida de la escuela, así todos ven la misma galería", "A school shared folder, so everyone sees the same gallery"));
            var refresh = BlocksChrome.Button(BlocksChrome.Icons.Retry, Loc.Tr("Actualizar", "Refresh"));
            var cards = new WrapPanel { Orientation = Orientation.Horizontal };
            var empty = Text(Loc.Tr("Todavía no hay nada. En \"Yo\", \"Compartir lo que estoy haciendo\" pone tu programa acá.",
                                    "Nothing here yet. In \"Me\", \"Share what I'm making\" puts your program here."), 15, BlocksChrome.InkSoft);

            void Fill()
            {
                cards.Children.Clear();
                var items = Classroom.Gallery(_settings.Gallery);
                empty.IsVisible = items.Count == 0;
                foreach (var item in items.Take(60))
                    cards.Children.Add(GalleryCard(item));
            }
            change.Click += async (_, _) =>
            {
                var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Loc.Tr("Carpeta de la galería", "Gallery folder") });
                if (picked.Count == 0)
                    return;
                _settings.GalleryFolder = picked[0].Path.LocalPath;
                Classroom.SaveSettings(_settings);
                folder.Text = _settings.Gallery;
                Fill();
            };
            refresh.Click += (_, _) => Fill();
            Fill();

            var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { change, refresh } };
            var head = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 12), Children = { top, folder, empty } };
            var panel = new DockPanel();
            DockPanel.SetDock(head, Dock.Top);
            panel.Children.Add(head);
            panel.Children.Add(new ScrollViewer { Content = cards });
            return panel;
        }

        private Control GalleryCard(Classroom.GalleryItem item)
        {
            var image = new Image { Width = 210, Height = 140, Stretch = Stretch.Uniform };
            var placeholder = new StrokeIcon(item.Program.Scripts().Any(s => s.Hat!.Type == BlockCatalog.MusicLoop) ? "M9 18V5l10-2v13M9 18a3 3 0 1 1-6 0a3 3 0 1 1 6 0M19 16a3 3 0 1 1-6 0a3 3 0 1 1 6 0" : BlocksChrome.Icons.Tiles, 40)
            {
                Foreground = BlocksChrome.InkMuted, HorizontalAlignment = HorizontalAlignment.Center,
            };
            var picture = new Border
            {
                Width = 210, Height = 140, CornerRadius = new CornerRadius(12), ClipToBounds = true, Background = BlocksChrome.Brush("#FAF8F5"),
                Child = new Grid { Children = { placeholder, image } },
            };
            // The picture is made by running the program: off the UI thread.
            var program = item.Program;
            Task.Run(() => Classroom.Thumbnail(program)).ContinueWith(t =>
            {
                if (t.Result is { } png)
                    Dispatcher.UIThread.Post(() =>
                    {
                        image.Source = new Bitmap(new MemoryStream(png));
                        placeholder.IsVisible = false;
                    });
            }, TaskScheduler.Default);

            var card = new Button
            {
                Width = 236, Margin = new Thickness(0, 0, 14, 14), Padding = new Thickness(12), Background = Brushes.White,
                BorderBrush = BlocksChrome.Line, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(18),
                HorizontalContentAlignment = HorizontalAlignment.Left, Cursor = new Cursor(StandardCursorType.Hand),
                Content = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        picture,
                        new TextBlock { Text = item.Title, FontFamily = BlocksChrome.FontKids, FontSize = 17, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Ink, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(2, 6, 0, 0) },
                        new TextBlock { Text = item.Author + " · " + item.Shared.ToString("d"), FontFamily = BlocksChrome.FontUi, FontSize = 13, Foreground = BlocksChrome.InkMuted, Margin = new Thickness(2, 0, 0, 0) },
                    },
                },
            };
            ToolTip.SetTip(card, Loc.Tr("Abrir una copia", "Open a copy"));
            card.Click += (_, _) => OpenProgram?.Invoke(program.Clone());
            return card;
        }

        // =================================================================
        // Docente
        // =================================================================

        private Control TeacherPage()
        {
            var pick = BlocksChrome.Button(BlocksChrome.Icons.Forward, Loc.Tr("Elegir la carpeta con las entregas…", "Pick the folder with the hand-ins…"), "go");
            var export = BlocksChrome.Button(null, Loc.Tr("Exportar a planilla (CSV)…", "Export to a spreadsheet (CSV)…"), "outline");
            var status = Status();
            var table = new ContentControl();
            List<Classroom.StudentRow> rows = new();

            void Fill()
            {
                if (_classFolder is null)
                {
                    table.Content = Text(Loc.Tr("Juntá en una carpeta los archivos .danaavance que te entregaron (de un pendrive, del correo, de una carpeta compartida) y elegila acá.",
                                                "Put the .danaavance files you got (from a USB stick, email, a shared folder) in one folder and pick it here."), 15, BlocksChrome.InkSoft);
                    export.IsEnabled = false;
                    return;
                }
                rows = Classroom.LoadClass(_classFolder, out var broken);
                status.Text = Loc.Tr($"{rows.Count} alumnos en {_classFolder}", $"{rows.Count} students in {_classFolder}")
                              + (broken.Count > 0 ? Loc.Tr($" · no se pudieron leer: {string.Join(", ", broken)}", $" · couldn't read: {string.Join(", ", broken)}") : "");
                table.Content = Table(rows);
                export.IsEnabled = rows.Count > 0;
            }
            pick.Click += async (_, _) =>
            {
                var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Loc.Tr("Carpeta con las entregas", "Folder with the hand-ins") });
                if (picked.Count == 0)
                    return;
                _classFolder = picked[0].Path.LocalPath;
                Fill();
            };
            export.Click += async (_, _) =>
            {
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = Loc.Tr("Exportar el avance de la clase", "Export the class progress"),
                    SuggestedFileName = Loc.Tr("avance-de-la-clase.csv", "class-progress.csv"), DefaultExtension = "csv",
                });
                if (file is null)
                    return;
                await using var stream = await file.OpenWriteAsync();
                // With a BOM, so spreadsheets read the accents right.
                await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(true));
                await writer.WriteAsync(Classroom.ClassCsv(rows));
                status.Text = Loc.Tr("Planilla guardada.", "Spreadsheet saved.");
            };
            Fill();

            var head = new StackPanel
            {
                Spacing = 4, Margin = new Thickness(0, 0, 0, 12),
                Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { pick, export } }, status },
            };
            var panel = new DockPanel();
            DockPanel.SetDock(head, Dock.Top);
            panel.Children.Add(head);
            panel.Children.Add(table);
            return panel;
        }

        /// <summary>One row per child, one column per challenge (numbered; the title is the tooltip), stars in the cells.</summary>
        private Control Table(List<Classroom.StudentRow> rows)
        {
            var columns = Classroom.Columns(rows);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(170)));
            foreach (var _ in columns)
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(38)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(70)));
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(34)));

            void Put(Control c, int row, int col)
            {
                Grid.SetRow(c, row);
                Grid.SetColumn(c, col);
                grid.Children.Add(c);
            }

            Put(Text(Loc.Tr("Alumno", "Student"), 13, BlocksChrome.InkMuted, bold: true), 0, 0);
            for (int c = 0; c < columns.Count; c++)
            {
                var head = Text((c + 1).ToString(), 13, BlocksChrome.InkMuted, bold: true);
                head.TextAlignment = TextAlignment.Center;
                ToolTip.SetTip(head, columns[c].Title);
                Put(head, 0, c + 1);
            }
            Put(Text("★", 15, BlocksChrome.Gold, bold: true), 0, columns.Count + 1);

            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                grid.RowDefinitions.Add(new RowDefinition(new GridLength(38)));
                var name = Text(row.Student, 15, BlocksChrome.Ink, bold: true);
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                name.VerticalAlignment = VerticalAlignment.Center;
                ToolTip.SetTip(name, Loc.Tr("Entregado: ", "Handed in: ") + row.Report.Created.ToString("g"));
                Put(name, r + 1, 0);
                for (int c = 0; c < columns.Count; c++)
                {
                    int stars = row.Report.Stars.GetValueOrDefault(columns[c].Id);
                    var program = row.Report.Program(columns[c].Id);
                    var dot = new Border
                    {
                        Width = 28, Height = 28, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                        Background = stars switch { 3 => BlocksChrome.Brush("#3DAA67"), 2 => BlocksChrome.Brush("#8CCB9F"), 1 => BlocksChrome.Brush("#CDE9D5"), _ => BlocksChrome.Brush("#F1ECE3") },
                        Child = stars > 0 ? new TextBlock { Text = stars.ToString(), FontFamily = BlocksChrome.FontUi, FontWeight = FontWeight.Bold, FontSize = 13, Foreground = stars >= 2 ? Brushes.White : BlocksChrome.Ink, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } : null,
                        Cursor = program is null ? Cursor.Default : new Cursor(StandardCursorType.Hand),
                    };
                    ToolTip.SetTip(dot, columns[c].Title + (program is null ? "" : Loc.Tr(" · tocá para ver su programa", " · click to see the program")));
                    if (program is not null)
                    {
                        var student = row.Student;
                        dot.PointerPressed += (_, _) =>
                        {
                            program.Name = $"{student} · {program.Name}";
                            OpenProgram?.Invoke(program.Clone());
                        };
                    }
                    Put(dot, r + 1, c + 1);
                }
                var total = Text(row.Report.TotalStars.ToString(), 15, BlocksChrome.Ink, bold: true);
                total.VerticalAlignment = VerticalAlignment.Center;
                Put(total, r + 1, columns.Count + 1);
            }
            return new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 8), Child = grid, HorizontalAlignment = HorizontalAlignment.Left },
            };
        }
    }
}
