using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.TextMate;
using DanaProcessing.Ide.Compilation;
using DanaProcessing.Ide.Theme;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TextMateSharp.Grammars;

namespace DanaProcessing.Ide.Editor
{
    public class SketchEditorView : UserControl
    {
        public ObservableCollection<EditorTab> OpenTabs { get; } = new();

        public event Action<EditorTab>? TabSaved;
        public event Action<int, int>? CaretPositionChanged;

        private readonly TabStrip _tabStrip;
        private readonly TextEditor _editor;

        // FIX: ThemeName.DarkPlus pinta el texto base en gris claro/blanco,
        // pensado para un editor de fondo oscuro. Nuestro editor tiene fondo
        // claro (#FAF8F5), así que con DarkPlus el texto quedaba casi del
        // mismo color que el fondo (invisible por bajo contraste). LightPlus
        // es el equivalente claro (como "Light+" de VS Code) y combina con
        // la paleta clay/beige del resto del IDE.
        private readonly RegistryOptions _registryOptions = new(ThemeName.LightPlus);
        private readonly TextMate.Installation _textMate;
        private EditorTab? _activeTab;

        // Un solo engine para todas las tabs, igual que hay un solo TextEditor
        // compartido: su documento interno se re-sincroniza con el texto de la
        // tab activa (ver ActivateTab) en vez de crear un workspace por tab.
        private readonly RoslynCompletionEngine _completionEngine = new();
        private CompletionWindow? _completionWindow;

        public SketchEditorView()
        {
            // ================================================================
            // ESTILOS - AHORA CON CLASES PARA HOVER
            // ================================================================

            Styles.AddRange(ClayTheme.ButtonEffectStyles());
            Styles.AddRange(ClayTheme.TabStripStates());

            // ================================================================
            // TAB STRIP
            // ================================================================
            // Nuevo/Abrir/Guardar/Guardar como ya no viven en una barra propia
            // acá -- pasaron al menú ☰ colapsable del title bar (ver
            // MainWindow.BuildFileMenuButton), que llama a AddNewTab/
            // OpenFileAsync/SaveActiveTabAsync/SaveActiveTabAsAsync más abajo
            // directamente. Tenerlos duplicados acá (más un botón Run que
            // nunca llegaba a agregarse al Grid) solo restaba espacio vertical
            // al editor sin aportar nada que el menú no cubriera ya.

            _tabStrip = new TabStrip
            {
                Background = ClayTheme.Base,
                Padding = new Thickness(12, 4, 12, 0),
                ItemsSource = OpenTabs,
                ItemTemplate = new FuncDataTemplate<EditorTab>((tab, _) =>
                {
                    var stackPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                    };

                    var title = new TextBlock
                    {
                        Foreground = ClayTheme.TextPrimary,
                        FontSize = 13,
                        FontFamily = ClayTheme.FontBody,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    title.Bind(TextBlock.TextProperty, new Binding(nameof(EditorTab.Title)) { Source = tab });

                    var closeButton = new Button
                    {
                        Content = "✕",
                        Classes = { "clay-icon" },
                        Padding = new Thickness(4, 0),
                        FontSize = 10,
                        Foreground = ClayTheme.TextMuted,
                        Width = 18,
                        Height = 18,
                        VerticalAlignment = VerticalAlignment.Center,
                        Background = Brushes.Transparent,
                    };
                    closeButton.Click += (_, _) =>
                    {
                        if (tab == _activeTab)
                        {
                            var index = OpenTabs.IndexOf(tab);
                            OpenTabs.Remove(tab);
                            if (OpenTabs.Count > 0)
                            {
                                var newIndex = Math.Min(index, OpenTabs.Count - 1);
                                _tabStrip.SelectedItem = OpenTabs[newIndex];
                            }
                            else
                            {
                                AddNewTab();
                            }
                        }
                        else
                        {
                            OpenTabs.Remove(tab);
                        }
                    };

                    stackPanel.Children.Add(title);
                    stackPanel.Children.Add(closeButton);

                    var container = new Border
                    {
                        Padding = new Thickness(16, 10, 16, 10),
                        Child = stackPanel,
                    };

                    return container;
                }, supportsRecycling: false)
            };

            _tabStrip.SelectionChanged += (_, _) =>
            {
                if (_tabStrip.SelectedItem is EditorTab tab)
                    ActivateTab(tab);
            };

            // ================================================================
            // EDITOR
            // ================================================================

            var editorContainer = new Border
            {
                Margin = new Thickness(12, 0, 12, 12),
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#E8E2DA")),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
            };

            _editor = new TextEditor
            {
                FontFamily = ClayTheme.FontMono,
                FontSize = 14,
                ShowLineNumbers = true,
                Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#FAF8F5")),
                Foreground = ClayTheme.TextPrimary,
                Padding = new Thickness(20, 16),
            };

            editorContainer.Child = _editor;

            _textMate = _editor.InstallTextMate(_registryOptions);
            ApplyCSharpGrammar();

            _editor.TextArea.Caret.PositionChanged += (_, _) =>
                CaretPositionChanged?.Invoke(_editor.TextArea.Caret.Line, _editor.TextArea.Caret.Column);

            // ================================================================
            // AUTOCOMPLETADO (Roslyn CompletionService, no una lista de palabras)
            // ================================================================

            _editor.TextArea.TextEntered += OnEditorTextEntered;
            _editor.TextArea.TextEntering += OnEditorTextEntering;
            _editor.TextArea.KeyDown += OnEditorKeyDown;

            // ================================================================
            // LAYOUT FINAL
            // ================================================================

            var mainPanel = new DockPanel
            {
                Background = ClayTheme.Base,
                LastChildFill = true,
            };

            DockPanel.SetDock(_tabStrip, Dock.Top);
            mainPanel.Children.Add(_tabStrip);

            mainPanel.Children.Add(editorContainer);

            Content = mainPanel;

            // ================================================================
            // EVENTOS
            // ================================================================
            // Nuevo/Abrir/Guardar/Guardar como ahora se disparan desde el menú
            // ☰ de MainWindow (llaman directo a AddNewTab/OpenFileAsync/
            // SaveActiveTabAsync/SaveActiveTabAsAsync, públicos más abajo),
            // así que no hay botones locales que enganchar acá.

            AddNewTab();
        }

        private void ApplyCSharpGrammar()
        {
            var language = _registryOptions.GetLanguageByExtension(".cs");
            if (language != null)
                _textMate.SetGrammar(_registryOptions.GetScopeByLanguageId(language.Id));
        }

        private void OnEditorTextEntered(object? sender, TextInputEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
                return;

            var c = e.Text[0];

            // '.' siempre reabre completions (nuevo contexto: miembros de lo que
            // sea que esté antes del punto). Una letra/'_' abre la lista solo si
            // todavía no hay una abierta — si ya hay una, AvaloniaEdit filtra sola
            // a medida que se sigue escribiendo, sin volver a llamar a Roslyn.
            if (c == '.' || ((char.IsLetter(c) || c == '_') && _completionWindow is null))
                _ = ShowCompletionAsync();
        }

        private void OnEditorTextEntering(object? sender, TextInputEventArgs e)
        {
            // Patrón estándar de AvaloniaEdit: si se está escribiendo un
            // caracter que no puede formar parte de un identificador mientras
            // el popup está abierto (paréntesis, punto y coma, espacio, etc.),
            // se trata como "confirmar la selección actual" en vez de dejar que
            // se escriba normal y el popup se cierre solo sin insertar nada.
            if (!string.IsNullOrEmpty(e.Text) && _completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
                    _completionWindow.CompletionList.RequestInsertion(e);
            }
        }

        private void OnEditorKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control)
            {
                e.Handled = true;
                _ = ShowCompletionAsync();
            }
        }

        private async Task ShowCompletionAsync()
        {
            if (_activeTab is null)
                return;

            // Re-sincronizar antes de preguntar: TextEntered se dispara ya con
            // el caracter insertado en el Document, así que esto siempre le
            // manda a Roslyn el texto real que se ve en pantalla en este instante.
            _completionEngine.UpdateText(_editor.Document.Text);

            var caretOffset = _editor.CaretOffset;

            // CLAVE: CompletionWindow por defecto arranca el rango a reemplazar
            // justo en el caret, SIN mirar hacia atrás. Si ya había texto
            // tecleado antes de abrir la ventana (ya sea porque se abrió
            // después de la primera letra, o porque se invocó Ctrl+Espacio a
            // mitad de palabra), ese prefijo queda "fijo" y la sugerencia se
            // inserta después de él en vez de reemplazarlo — eso es lo que
            // causaba "CoColorSpaceMode" y el filtrado roto (sin prefijo que
            // filtrar, mostraba la lista completa sin acotar). Buscar el
            // inicio real de la palabra actual y fijarlo como StartOffset
            // arregla ambos síntomas a la vez.
            var wordStart = FindWordStart(_editor.Document.Text, caretOffset);

            var items = await _completionEngine.GetCompletionsAsync(caretOffset);
            if (items.Count == 0)
                return;

            // El usuario pudo haber seguido escribiendo (o cerrado la tab)
            // mientras esto era async; si el caret ya no está donde arrancamos,
            // esta lista quedó obsoleta.
            if (_editor.CaretOffset != caretOffset)
                return;

            _completionWindow = new CompletionWindow(_editor.TextArea)
            {
                CloseWhenCaretAtBeginning = false,
                StartOffset = wordStart,
            };

            var data = _completionWindow.CompletionList.CompletionData;
            foreach (var item in items)
                data.Add(new SketchCompletionData(_completionEngine, item, caretOffset));

            _completionWindow.Closed += (_, _) => _completionWindow = null;
            _completionWindow.Show();
        }

        /// <summary>Scans back from <paramref name="offset"/> over identifier characters to find where the current word begins.</summary>
        private static int FindWordStart(string text, int offset)
        {
            var start = offset;
            while (start > 0 && IsIdentifierChar(text[start - 1]))
                start--;
            return start;
        }

        private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        public EditorTab AddNewTab(string? filePath = null, string? initialText = null)
        {
            var tab = new EditorTab(filePath, initialText ?? DefaultSketchTemplate());
            OpenTabs.Add(tab);
            _tabStrip.SelectedItem = tab;

            // FIX: no confiar únicamente en que SelectionChanged dispare acá.
            // Justo después de agregar el primer item a un ObservableCollection
            // recién enlazado como ItemsSource, el contenedor visual del
            // TabStripItem puede no estar listo todavía, y en ese caso
            // SelectionChanged no siempre se dispara de forma síncrona. Si eso
            // pasa, _editor.Document nunca se asigna y AvaloniaEdit muestra un
            // TextDocument vacío por defecto — el editor se ve pero sin texto.
            // Llamando ActivateTab acá directamente garantizamos que el
            // documento quede enlazado sin depender de ese evento.
            ActivateTab(tab);

            return tab;
        }

        public string? ActiveSourceText => _activeTab?.Document.Text;

        private void ActivateTab(EditorTab tab)
        {
            _activeTab = tab;
            _editor.Document = tab.Document;
            _completionEngine.UpdateText(tab.Document.Text);
        }

        public async Task OpenFileAsync()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
                return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Abrir sketch",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("C# / Sketch") { Patterns = new[] { "*.cs" } } }
            });
            if (files.Count == 0)
                return;

            var path = files[0].Path.LocalPath;
            var text = await File.ReadAllTextAsync(path);
            AddNewTab(path, text);
        }

        public async Task SaveActiveTabAsync()
        {
            if (_activeTab is null)
                return;

            if (_activeTab.FilePath is null)
            {
                await SaveActiveTabAsAsync();
                return;
            }

            await File.WriteAllTextAsync(_activeTab.FilePath, _activeTab.Document.Text);
            _activeTab.MarkSaved();
            TabSaved?.Invoke(_activeTab);
        }

        public async Task SaveActiveTabAsAsync()
        {
            if (_activeTab is null)
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
                return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Guardar sketch",
                SuggestedFileName = _activeTab.FilePath is null ? "Sketch.cs" : Path.GetFileName(_activeTab.FilePath),
                DefaultExtension = "cs",
                FileTypeChoices = new[] { new FilePickerFileType("C# / Sketch") { Patterns = new[] { "*.cs" } } }
            });
            if (file is null)
                return;

            var path = file.Path.LocalPath;
            await File.WriteAllTextAsync(path, _activeTab.Document.Text);
            _activeTab.FilePath = path;
            _activeTab.MarkSaved();
            TabSaved?.Invoke(_activeTab);
        }

        // Parche para SketchEditorView.cs — reemplazar el método
        // DefaultSketchTemplate() (línea ~510, el árbol fractal) por este. El
        // árbol no se pierde: quedó como sample "Árbol fractal" en
        // SketchSamples.cs, accesible desde la ventana de Samples.

        private static string DefaultSketchTemplate() => SketchSamples.All[2].Source; // "Cubo 3D (Silk.NET)"
    }
}