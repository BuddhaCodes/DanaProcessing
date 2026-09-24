using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.TextMate;
using DanaProcessing.Ide.Compilation;
using DanaProcessing.Ide.Compilation.PackageManagement;
using DanaProcessing.Ide.Localization;
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

        /// <summary>Fired every time the live diagnostics list changes (debounced text edits,
        /// or immediately on tab switch) — MainWindow's "Errores en vivo" tab renders from this.</summary>
        public event Action<IReadOnlyList<LiveDiagnosticInfo>>? LiveDiagnosticsChanged;

        private readonly TabStrip _tabStrip;
        private readonly TextEditor _editor;
        private readonly Border _emptyStateOverlay;

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

        // ================================================================
        // DIAGNÓSTICOS EN VIVO (antes solo aparecían al apretar Run)
        // ================================================================
        // Mismo GetDiagnosticsAsync que ya existía en RoslynCompletionEngine
        // (semantic model, sin emit) — antes nadie lo llamaba. Se repinta con
        // un debounce corto en cada cambio de texto, no en cada tecla, para
        // no relanzar Roslyn en cada letra.
        private readonly EditorDiagnosticsColorizer _diagnosticsColorizer = new();
        private readonly DispatcherTimer _diagnosticsTimer;
        private readonly Border _diagnosticBanner;
        private readonly TextBlock _diagnosticBannerText;

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
                                // Closing the last tab no longer conjures a fresh
                                // one automatically -- it leaves the editor
                                // genuinely empty, and _emptyStateOverlay (wired
                                // to OpenTabs.CollectionChanged below) takes over
                                // with its own "crear un nuevo sketch" prompt.
                                _activeTab = null;
                                ClearDiagnosticsDisplay();
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

            // "+" lives next to the tab strip itself (not the ☰ menu) so
            // adding a tab *within* the current context has its own obvious,
            // always-visible affordance -- distinct from ☰ ▸ Nuevo, which
            // now replaces the whole context instead (see MainWindow.
            // ReplaceContextIfConfirmedAsync).
            var addTabButton = new Button
            {
                Content = "+",
                Classes = { "clay-icon" },
                Width = 28,
                Height = 28,
                FontSize = 15,
                Margin = new Thickness(2, 4, 8, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            ToolTip.SetTip(addTabButton, Loc.Tr("Nueva pestaña", "New tab"));
            addTabButton.Click += (_, _) => AddNewTab();

            var tabStripRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Grid.SetColumn(_tabStrip, 0);
            Grid.SetColumn(addTabButton, 1);
            tabStripRow.Children.Add(_tabStrip);
            tabStripRow.Children.Add(addTabButton);

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
            {
                CaretPositionChanged?.Invoke(_editor.TextArea.Caret.Line, _editor.TextArea.Caret.Column);
                UpdateDiagnosticBanner();
            };

            // ================================================================
            // DIAGNÓSTICOS EN VIVO
            // ================================================================

            _editor.TextArea.TextView.LineTransformers.Add(_diagnosticsColorizer);

            _diagnosticBannerText = new TextBlock
            {
                FontFamily = ClayTheme.FontBody,
                FontSize = 12,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            };
            _diagnosticBanner = new Border
            {
                Padding = new Thickness(14, 6),
                Margin = new Thickness(12, 0, 12, 6),
                CornerRadius = new CornerRadius(8),
                IsVisible = false,
                Child = _diagnosticBannerText,
            };

            // Debounce: recompilar el semantic model en cada tecla sería
            // carísimo y redundante -- se espera a que el usuario haga una
            // pausa breve antes de volver a preguntarle a Roslyn.
            _diagnosticsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            _diagnosticsTimer.Tick += async (_, _) =>
            {
                _diagnosticsTimer.Stop();
                await RefreshDiagnosticsAsync();
            };

            _editor.TextChanged += (_, _) =>
            {
                // El mensaje bajo el cursor queda desactualizado hasta que el
                // debounce de abajo recalcule contra el texto nuevo.
                _diagnosticBanner.IsVisible = false;
                _diagnosticsTimer.Stop();
                _diagnosticsTimer.Start();
            };

            // ================================================================
            // AUTOCOMPLETADO (Roslyn CompletionService, no una lista de palabras)
            // ================================================================

            _editor.TextArea.TextEntered += OnEditorTextEntered;
            _editor.TextArea.TextEntering += OnEditorTextEntering;
            _editor.TextArea.KeyDown += OnEditorKeyDown;

            // ================================================================
            // ESTADO VACÍO: se muestra en vez del editor cuando se cierra la
            // última pestaña (ver el closeButton.Click de más arriba) -- ya
            // no se crea una pestaña en blanco automáticamente, así que esto
            // es lo único que ocuparía ese lugar. Background opaco (no un
            // overlay semitransparente): cubre del todo el editorContainer
            // que queda debajo con contenido de la última pestaña cerrada.
            // Insignia con gradiente + glow, mismo lenguaje visual que el
            // resto del IDE (AccentGradient del botón Run, ShadowGlow del
            // foco de editor/canvas en MainWindow), y un fade-in propio en
            // vez de aparecer de golpe -- el "toque animado" que el resto de
            // la app reserva para pocos lugares en vez de repartirlo por todos.
            // ================================================================
            // Background paints the logo directly (instead of an Image child)
            // so the Border's CornerRadius clips it for free -- Avalonia
            // doesn't clip child content to a rounded border on its own.
            Bitmap? emptyStateLogoBitmap = null;
            try
            {
                using var logoStream = AssetLoader.Open(new Uri("avares://DanaProcessing.Ide/Theme/dana.png"));
                emptyStateLogoBitmap = new Bitmap(logoStream);
            }
            catch
            {
                // Missing/corrupt logo asset -- badge falls back to the old
                // accent-gradient look instead of breaking the empty state.
            }

            var emptyStateIconBadge = new Border
            {
                Width = 72,
                Height = 72,
                CornerRadius = ClayTheme.RadiusMedium,
                Background = emptyStateLogoBitmap is not null
                    ? new ImageBrush(emptyStateLogoBitmap) { Stretch = Stretch.UniformToFill }
                    : ClayTheme.AccentGradient,
                BoxShadow = ClayTheme.ShadowRaised,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var newSketchFromEmptyStateButton = new Button
            {
                Content = Loc.Tr("+ Crear nuevo sketch", "+ Create new sketch"),
                Classes = { "clay-run" },
                Padding = new Thickness(20, 10),
                CornerRadius = ClayTheme.RadiusButton,
                FontSize = 13.5,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            newSketchFromEmptyStateButton.Click += (_, _) => AddNewTab();

            _emptyStateOverlay = new Border
            {
                Background = ClayTheme.SurfaceRaised,
                IsVisible = false,
                Opacity = 0,
                Transitions = new Avalonia.Animation.Transitions
                {
                    new Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(260) }
                },
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    MaxWidth = 300,
                    Spacing = 4,
                    Children =
                    {
                        emptyStateIconBadge,
                        new TextBlock
                        {
                            Text = Loc.Tr("No hay ningún sketch abierto", "No sketch is open"),
                            Foreground = ClayTheme.TextPrimary,
                            FontFamily = ClayTheme.FontDisplay,
                            FontWeight = FontWeight.SemiBold,
                            FontSize = 18,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            TextAlignment = TextAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 18, 0, 0),
                        },
                        new TextBlock
                        {
                            Text = Loc.Tr("Empezá uno nuevo para seguir dibujando.", "Start a new one to keep drawing."),
                            Foreground = ClayTheme.TextMuted,
                            FontFamily = ClayTheme.FontBody,
                            FontSize = 13,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            TextAlignment = TextAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 4, 0, 20),
                        },
                        newSketchFromEmptyStateButton,
                    }
                }
            };

            // ================================================================
            // LAYOUT FINAL
            // ================================================================

            var mainPanel = new DockPanel
            {
                Background = ClayTheme.Base,
                LastChildFill = true,
            };

            DockPanel.SetDock(tabStripRow, Dock.Top);
            mainPanel.Children.Add(tabStripRow);

            DockPanel.SetDock(_diagnosticBanner, Dock.Top);
            mainPanel.Children.Add(_diagnosticBanner);

            var editorArea = new Panel { Children = { editorContainer, _emptyStateOverlay } };
            mainPanel.Children.Add(editorArea);

            Content = mainPanel;

            // ================================================================
            // EVENTOS
            // ================================================================
            // Nuevo/Abrir/Guardar/Guardar como ahora se disparan desde el menú
            // ☰ de MainWindow (llaman directo a AddNewTab/OpenFileAsync/
            // SaveActiveTabAsync/SaveActiveTabAsAsync, públicos más abajo),
            // así que no hay botones locales que enganchar acá.

            OpenTabs.CollectionChanged += (_, _) =>
            {
                if (OpenTabs.Count == 0)
                {
                    // Two ticks, not one assignment: IsVisible has to actually take
                    // effect (a render frame has to happen) before bumping Opacity
                    // back to 1 gives the DoubleTransition above something to
                    // animate *from* -- setting both in the same tick would just
                    // jump straight to the end state with no visible fade.
                    _emptyStateOverlay.Opacity = 0;
                    _emptyStateOverlay.IsVisible = true;
                    Dispatcher.UIThread.Post(() => _emptyStateOverlay.Opacity = 1);
                }
                else
                {
                    _emptyStateOverlay.IsVisible = false;
                }
            };

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

        /// <summary>True if any open tab has edits since it was last saved —
        /// checked before "Nuevo"/"Ejemplos" would otherwise discard them.</summary>
        public bool HasUnsavedChanges => OpenTabs.Any(t => t.IsDirty);

        /// <summary>
        /// Closes every open tab and starts a single fresh one. Used by "Nuevo"
        /// and "Ejemplos" (see MainWindow.BuildFileMenuButton): picking either one
        /// means switching to a different sketch entirely, same "one sketch at a
        /// time" model as Processing's own IDE — not adding one more tab alongside
        /// whatever was already open (that's what "Abrir" is for). Callers are
        /// responsible for confirming with the user first when HasUnsavedChanges
        /// is true; this method itself always discards without asking.
        /// </summary>
        public EditorTab ReplaceAllTabs(string? filePath = null, string? initialText = null)
        {
            OpenTabs.Clear();
            return AddNewTab(filePath, initialText);
        }

        public string? ActiveSourceText => _activeTab?.Document.Text;

        /// <summary>A reasonable default name to suggest for the active tab when
        /// exporting it (see MainWindow's "Exportar sketch..."): the saved file's
        /// name if it has one, otherwise a generic fallback for an unsaved tab.</summary>
        public string SuggestedExportName =>
            _activeTab?.FilePath is { } path ? Path.GetFileNameWithoutExtension(path) : "MiSketch";

        /// <summary>Rewrites the active tab's `// nuget:` directives to exactly
        /// <paramref name="directives"/> (see PackageDirectiveParser.Apply) — used
        /// by the "Paquetes NuGet" dialog. Setting Document.Text fires the same
        /// TextChanged path a real keystroke would, so the tab's dirty flag and
        /// live diagnostics update themselves; a no-op if there's no active tab.</summary>
        public void ApplyPackageDirectives(IReadOnlyList<PackageDirective> directives)
        {
            if (_activeTab is null)
                return;

            _activeTab.Document.Text = PackageDirectiveParser.Apply(_activeTab.Document.Text, directives);
        }

        /// <summary>Clears the squiggles/banner/live-errors-tab for whatever was
        /// showing before — used when switching tabs (a new document's diagnostics
        /// haven't been computed yet) and when the last tab closes (nothing left to
        /// show diagnostics for). Pulled out as its own method rather than inlined
        /// in the tab strip's ItemTemplate closures: referencing these fields from
        /// a closure defined earlier in the constructor than their own assignment
        /// trips the compiler's (here, false-positive) possibly-null warning —
        /// a plain method isn't part of that constructor-body definite-assignment
        /// trace, so it doesn't have the same problem.</summary>
        private void ClearDiagnosticsDisplay()
        {
            _diagnosticsColorizer.SetDiagnostics(Array.Empty<SketchDiagnostic>());
            _diagnosticBanner.IsVisible = false;
            LiveDiagnosticsChanged?.Invoke(Array.Empty<LiveDiagnosticInfo>());
        }

        private void ActivateTab(EditorTab tab)
        {
            _activeTab = tab;
            _editor.Document = tab.Document;
            _completionEngine.UpdateText(tab.Document.Text);

            // Diagnostics are per-document; painting the previous tab's
            // squiggles for even a frame while the new document's copy
            // compiles would be worse than showing nothing for a moment.
            ClearDiagnosticsDisplay();
            _editor.TextArea.TextView.Redraw();

            _diagnosticsTimer.Stop();
            _ = RefreshDiagnosticsAsync();
        }

        /// <summary>
        /// Re-asks Roslyn for diagnostics against the *active* tab's current text and
        /// repaints the squiggles. Called on a debounce after every edit, and immediately
        /// on tab switch. GetDiagnosticsAsync is the same semantic-model-only check
        /// RoslynCompletionEngine already exposed — no full emit, much cheaper than
        /// SketchCompiler.Compile() (which is still what "Run" uses).
        /// </summary>
        private async Task RefreshDiagnosticsAsync()
        {
            var tab = _activeTab;
            if (tab is null)
                return;

            _completionEngine.UpdateText(_editor.Document.Text);
            var diagnostics = await _completionEngine.GetDiagnosticsAsync();

            // The user may have switched tabs (or closed this one) while the
            // above was in flight -- a diagnostics list for a document that's
            // no longer showing would just paint garbage over whatever is
            // showing now.
            if (_activeTab != tab)
                return;

            _diagnosticsColorizer.SetDiagnostics(diagnostics);
            _editor.TextArea.TextView.Redraw();
            UpdateDiagnosticBanner();

            var info = diagnostics
                .Select(d =>
                {
                    var loc = _editor.Document.GetLocation(Math.Min(d.Start, _editor.Document.TextLength));
                    return new LiveDiagnosticInfo(d.Start, loc.Line, loc.Column, d.Message, d.IsError);
                })
                .ToList();
            LiveDiagnosticsChanged?.Invoke(info);
        }

        /// <summary>Moves the caret to <paramref name="offset"/> and scrolls it into view —
        /// used by MainWindow's "Errores en vivo" tab to jump to a diagnostic on click.</summary>
        public void GoToOffset(int offset)
        {
            var clamped = Math.Clamp(offset, 0, _editor.Document.TextLength);
            _editor.CaretOffset = clamped;
            _editor.TextArea.Caret.BringCaretToView();
            _editor.Focus();
        }

        /// <summary>Shows the message of whichever diagnostic sits under the caret right
        /// now, or hides the banner if there isn't one.</summary>
        private void UpdateDiagnosticBanner()
        {
            var diag = _diagnosticsColorizer.FindAt(_editor.CaretOffset);
            if (diag is null)
            {
                _diagnosticBanner.IsVisible = false;
                return;
            }

            _diagnosticBannerText.Text = diag.Message;
            if (diag.IsError)
            {
                _diagnosticBannerText.Foreground = ClayTheme.DangerHover;
                _diagnosticBanner.Background = ClayTheme.DangerSurface;
            }
            else
            {
                _diagnosticBannerText.Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#8A6A1F"));
                _diagnosticBanner.Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#FBF1DC"));
            }
            _diagnosticBanner.IsVisible = true;
        }

        public async Task OpenFileAsync()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
                return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Loc.Tr("Abrir sketch", "Open sketch"),
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
                Title = Loc.Tr("Guardar sketch", "Save sketch"),
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

        /// <summary>
        /// What "Nuevo", the "+" tab button, and the empty-state prompt all
        /// start from — a genuinely blank sketch, not one of the samples.
        /// Draw() has to be overridden (it's abstract on Sketch); Setup() is
        /// filled in too since an unsized, unfilled canvas is a confusing
        /// first thing to look at. Samples stay reachable from their own
        /// "Ejemplos..." menu entry instead of being what "new" secretly means.
        /// </summary>
        private static string DefaultSketchTemplate() =>
            "public class MySketch : Sketch\n" +
            "{\n" +
            "    public override void Setup()\n" +
            "    {\n" +
            "        Size(600, 400);\n" +
            "    }\n" +
            "\n" +
            "    public override void Draw()\n" +
            "    {\n" +
            "        Background(255);\n" +
            "    }\n" +
            "}\n";
    }
}