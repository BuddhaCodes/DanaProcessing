using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DanaProcessing.Ide.Agent;
using DanaProcessing.Ide.CrashReporting;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;
using DanaProcessing.Ide.Updates;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// Settings, one section at a time: a list of sections on the left
    /// (General, Appearance, Running sketches, Performance mode, Assistant)
    /// and only that section's options on the right -- the old window put
    /// every option on one long page. Appearance leads with the three colors
    /// and the one "Roundness" slider people actually change; the full list
    /// of 13 colors, 6 radii and 3 font stacks waits behind "Show all".
    ///
    /// Everything is a working copy until Save (colors preview live and are
    /// put back on Cancel or close). When a change only takes effect after a
    /// restart (language, shapes, fonts), the footer says so and offers
    /// "Save and restart".
    /// </summary>
    public class SettingsWindow : Window
    {
        public enum Section { General, Appearance, Running, Performance, Assistant }

        private readonly ThemeSettings _originalTheme;
        private ThemeSettings _theme;
        private RenderingSettings _rendering;
        private readonly AppLanguage _originalLanguage;
        private AppLanguage _language;
        private bool _autoUpdates;
        private bool _crashReports;
        private AgentSettings _agent;
        private PerformanceSettings _performance;

        private readonly Dictionary<Section, Button> _navButtons = new();
        private readonly ScrollViewer _pageHost = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        private readonly TextBlock _restartNote = new();
        private readonly Button _saveRestartButton = new();
        private Section _current;
        private bool _savedOrDiscarded;
        private bool _showAllAppearance;

        private static readonly (string Label, int Value)[] Supersample2DOptions =
        {
            (Loc.Tr("Desactivado (1x)", "Off (1x)"), 1),
            (Loc.Tr("2x (recomendado)", "2x (recommended)"), 2),
            ("3x", 3),
            (Loc.Tr("4x (máxima calidad, más lento)", "4x (best quality, slower)"), 4),
        };

        private static readonly (string Label, int Value)[] Msaa3DOptions =
        {
            (Loc.Tr("Desactivado", "Off"), 0),
            ("2x", 2),
            (Loc.Tr("4x (recomendado)", "4x (recommended)"), 4),
            (Loc.Tr("8x (máxima calidad, más lento)", "8x (best quality, slower)"), 8),
        };

        // Each language in its own language: easier to find your own.
        private static readonly (string Label, AppLanguage Value)[] LanguageOptions =
        {
            ("Español", AppLanguage.Spanish),
            ("English", AppLanguage.English),
        };

        private static readonly (string Label, string Value)[] AgentProviderOptions =
        {
            ("Anthropic (Claude)", "Anthropic"),
            ("OpenAI", "OpenAI"),
        };

        /// <summary>Accent presets: the accent, its pressed shade and the text that reads on it.</summary>
        private static readonly (string Name, string Accent, string Dim, string OnAccent)[] AccentPresets =
        {
            (Loc.Tr("Arcilla", "Clay"), "#FFB08C", "#F09B78", "#4A3728"),
            (Loc.Tr("Cielo", "Sky"), "#A8D0E6", "#89B8D0", "#1F3442"),
            (Loc.Tr("Menta", "Mint"), "#A5DEC0", "#86C9A6", "#1E3A2C"),
            (Loc.Tr("Lila", "Lilac"), "#CDBDF2", "#B5A2E6", "#2E2448"),
            (Loc.Tr("Sol", "Sun"), "#F6D06F", "#E8BC4F", "#3D3014"),
            (Loc.Tr("Rosa", "Rose"), "#F4A9BE", "#E78EA7", "#4A2230"),
        };

        public SettingsWindow(Section start = Section.General)
        {
            _originalTheme = ClayTheme.CurrentSettings.Clone();
            _theme = _originalTheme.Clone();
            _rendering = RenderingSettingsStore.Load();
            _originalLanguage = LocaleSettingsStore.Load().Language;
            _language = _originalLanguage;
            _autoUpdates = UpdateSettingsStore.Load().AutoCheckEnabled;
            _crashReports = CrashReportSettingsStore.Load().Enabled;
            _agent = AgentSettingsStore.Load();
            _performance = PerformanceSettingsStore.Load();

            Title = Loc.Tr("Configuración — DanaProcessing IDE", "Settings — DanaProcessing IDE");
            Width = 820;
            Height = 600;
            MinWidth = 600;
            MinHeight = 440;
            CanResize = true;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            // --- left: the sections ---
            var nav = new StackPanel { Spacing = 4, Margin = new Thickness(16, 20, 12, 16) };
            nav.Children.Add(new TextBlock
            {
                Text = Loc.Tr("Configuración", "Settings"),
                FontFamily = ClayTheme.FontDisplay,
                FontWeight = FontWeight.SemiBold,
                FontSize = 18,
                Foreground = ClayTheme.TextPrimary,
                Margin = new Thickness(8, 0, 0, 12),
            });
            foreach (var (section, label) in new[]
            {
                (Section.General, Loc.Tr("General", "General")),
                (Section.Appearance, Loc.Tr("Apariencia", "Appearance")),
                (Section.Running, Loc.Tr("Ejecutar sketches", "Running sketches")),
                (Section.Performance, Loc.Tr("Modo performance", "Performance mode")),
                (Section.Assistant, Loc.Tr("Asistente", "Assistant")),
            })
            {
                var button = new Button
                {
                    Content = label,
                    Classes = { "clay-toggle" },
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    MinHeight = 38,
                };
                var target = section;
                button.Click += (_, _) => ShowSection(target);
                _navButtons[section] = button;
                nav.Children.Add(button);
            }
            var navBorder = new Border
            {
                Background = ClayTheme.SurfaceHigher,
                Child = nav,
                Width = 210,
            };

            // --- bottom: restart note + buttons ---
            var footer = BuildFooter();

            var right = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(footer, Dock.Bottom);
            right.Children.Add(footer);
            right.Children.Add(_pageHost);

            var layout = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(navBorder, Dock.Left);
            layout.Children.Add(navBorder);
            layout.Children.Add(right);
            // The IDE's own title bar, like every other window (not the OS one).
            ClayChrome.Apply(this, layout, canMinimize: false);

            ShowSection(start);
            UpdateRestartNote();

            Closing += (_, _) =>
            {
                // Closed with the window's X: don't leave unsaved colors on screen.
                if (!_savedOrDiscarded)
                    ClayTheme.ApplyPalette(_originalTheme);
            };
        }

        /// <summary>True when a saved change only shows after restarting the IDE (language, shapes, fonts).</summary>
        public static bool NeedsRestart(ThemeSettings savedTheme, ThemeSettings theme, AppLanguage savedLanguage, AppLanguage language) =>
            language != savedLanguage || theme.DiffersInRestartOnlyValues(savedTheme);

        /// <summary>Shows one section on the right and marks it in the list.</summary>
        public void ShowSection(Section section)
        {
            _current = section;
            foreach (var (s, b) in _navButtons)
                b.Classes.Set("active", s == section);
            _pageHost.Content = section switch
            {
                Section.General => BuildGeneralPage(),
                Section.Appearance => BuildAppearancePage(),
                Section.Running => BuildRunningPage(),
                Section.Performance => BuildPerformancePage(),
                _ => BuildAssistantPage(),
            };
            _pageHost.Offset = default;
        }

        // ====================================================================
        // Pages
        // ====================================================================

        private Control BuildGeneralPage()
        {
            var page = Page(Loc.Tr("General", "General"), null);
            page.Children.Add(Row(Loc.Tr("Idioma de la interfaz", "Interface language"),
                Combo(LanguageOptions, _language, v => { _language = v; UpdateRestartNote(); }, Loc.Tr("Idioma de la interfaz", "Interface language")),
                Loc.Tr("Se ve al reiniciar la IDE.", "Takes effect when the IDE restarts.")));
            page.Children.Add(Check(Loc.Tr("Avisarme cuando haya una versión nueva", "Tell me when there's a new version"), _autoUpdates, v => _autoUpdates = v,
                Loc.Tr("Al arrancar, la IDE le pregunta a GitHub si hay una versión más nueva. Solo avisa: nunca descarga ni instala nada sola.",
                       "On startup the IDE asks GitHub whether a newer version exists. It only tells you: it never downloads or installs anything by itself.")));
            page.Children.Add(Check(Loc.Tr("Ofrecer reportar si la IDE se cierra sola", "Offer to report it if the IDE crashes"), _crashReports, v => _crashReports = v,
                Loc.Tr("La próxima vez que la abras te pregunta si querés reportarlo. Reportar abre GitHub con los detalles; vos revisás y enviás.",
                       "The next time you open it, it asks whether you want to report it. Reporting opens GitHub with the details filled in; you review and send it.")));
            return page;
        }

        private Control BuildAppearancePage()
        {
            var page = Page(Loc.Tr("Apariencia", "Appearance"), Loc.Tr("Los colores se ven al instante. Formas y fuentes, al reiniciar.", "Colors change right away. Shapes and fonts, after a restart."));

            // Accent: presets first, the exact value second.
            var presets = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var preset in AccentPresets)
            {
                var chosen = string.Equals(_theme.AccentColor, preset.Accent, StringComparison.OrdinalIgnoreCase);
                var swatch = new Button
                {
                    Width = 40,
                    Height = 40,
                    Margin = new Thickness(0, 0, 8, 8),
                    CornerRadius = new CornerRadius(20),
                    Background = new SolidColorBrush(Avalonia.Media.Color.Parse(preset.Accent)),
                    BorderBrush = chosen ? ClayTheme.TextPrimary : new SolidColorBrush(Avalonia.Media.Color.Parse("#E8E2DA")),
                    BorderThickness = new Thickness(chosen ? 3 : 1),
                    Content = chosen ? new TextBlock { Text = "✓", Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse(preset.OnAccent)), FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center } : null,
                };
                ToolTip.SetTip(swatch, preset.Name);
                AutomationProperties.SetName(swatch, Loc.Tr($"Acento {preset.Name}", $"{preset.Name} accent"));
                var p = preset;
                swatch.Click += (_, _) =>
                {
                    _theme.AccentColor = p.Accent;
                    _theme.AccentDimColor = p.Dim;
                    _theme.OnAccentColor = p.OnAccent;
                    ClayTheme.ApplyPalette(_theme);
                    ShowSection(Section.Appearance); // redraw the check mark
                };
                presets.Children.Add(swatch);
            }
            page.Children.Add(Row(Loc.Tr("Color de acento", "Accent color"), presets, Loc.Tr("Botón Run, pestañas activas, resaltados.", "The Run button, active tabs, highlights.")));
            page.Children.Add(ColorRow(Loc.Tr("Fondo", "Background"), t => t.BaseColor, (t, v) => t.BaseColor = v));
            page.Children.Add(ColorRow(Loc.Tr("Texto", "Text"), t => t.TextPrimaryColor, (t, v) => t.TextPrimaryColor = v));

            page.Children.Add(Row(Loc.Tr("Redondez", "Roundness"),
                SliderWithValue(0, 2, _theme.Roundness, v => $"{v * 100:0}%", v =>
                {
                    _theme = _theme.WithRoundness(v);
                    UpdateRestartNote();
                }, Loc.Tr("Redondez de las esquinas", "Corner roundness"), step: 0.05),
                Loc.Tr("0% esquinas rectas · 100% como viene · 200% muy redondo.", "0% square corners · 100% as shipped · 200% very round.")));

            // Everything else, for the curious.
            var all = new StackPanel { Spacing = 2, Margin = new Thickness(0, 8, 0, 0) };
            all.Children.Add(SubTitle(Loc.Tr("Todos los colores", "All colors")));
            foreach (var (label, get, set) in AllColors())
                all.Children.Add(ColorRow(label, get, set));
            all.Children.Add(SubTitle(Loc.Tr("Esquinas, una por una", "Corners, one by one")));
            var radiusLabels = new[]
            {
                Loc.Tr("Chicas (etiquetas, listas)", "Small (badges, lists)"), Loc.Tr("Medianas", "Medium"), Loc.Tr("Grandes", "Large"),
                Loc.Tr("Tarjetas (editor, canvas)", "Cards (editor, canvas)"), Loc.Tr("Botones", "Buttons"), Loc.Tr("Botones de la ventana", "Window buttons"),
            };
            for (int i = 0; i < ThemeSettings.Radii.Length; i++)
            {
                var (get, set, max) = ThemeSettings.Radii[i];
                all.Children.Add(Row(radiusLabels[i], SliderWithValue(0, max, get(_theme), v => $"{v:0} px", v =>
                {
                    set(_theme, Math.Round(v));
                    UpdateRestartNote();
                }, radiusLabels[i], step: 1), null));
            }
            all.Children.Add(SubTitle(Loc.Tr("Fuentes", "Fonts")));
            all.Children.Add(FontRow(Loc.Tr("Títulos", "Headings"), () => _theme.FontDisplay, v => _theme.FontDisplay = v));
            all.Children.Add(FontRow(Loc.Tr("Texto", "Body text"), () => _theme.FontBody, v => _theme.FontBody = v));
            all.Children.Add(FontRow(Loc.Tr("Código (editor)", "Code (editor)"), () => _theme.FontMono, v => _theme.FontMono = v));
            all.Children.Add(Hint(Loc.Tr("Listas separadas por coma: se usa la primera fuente instalada.", "Comma-separated lists: the first installed font is used.")));
            // A plain disclosure button (light, like the rest of the window)
            // instead of Avalonia's Expander, which comes out dark here.
            string showAll = Loc.Tr("Mostrar todo: colores, esquinas y fuentes", "Show all: colors, corners and fonts");
            string hideAll = Loc.Tr("Ocultar colores, esquinas y fuentes", "Hide colors, corners and fonts");
            all.IsVisible = _showAllAppearance;
            var toggle = new Button
            {
                Content = (_showAllAppearance ? "▴  " + hideAll : "▾  " + showAll),
                Classes = { "clay-secondary" },
                Padding = new Thickness(14, 8),
                Margin = new Thickness(0, 14, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            AutomationProperties.SetName(toggle, showAll);
            toggle.Click += (_, _) =>
            {
                _showAllAppearance = !_showAllAppearance;
                all.IsVisible = _showAllAppearance;
                toggle.Content = _showAllAppearance ? "▴  " + hideAll : "▾  " + showAll;
            };
            page.Children.Add(toggle);
            page.Children.Add(all);

            page.Children.Add(ResetButton(Loc.Tr("Volver a la apariencia original", "Back to the original look"), () =>
            {
                _theme = ThemeSettings.Default();
                ClayTheme.ApplyPalette(_theme);
                UpdateRestartNote();
            }));
            return page;
        }

        private Control BuildRunningPage()
        {
            var page = Page(Loc.Tr("Ejecutar sketches", "Running sketches"),
                Loc.Tr("Se aplica en el próximo ▶ Run, sin reiniciar. Más calidad = bordes más suaves, más trabajo por frame.",
                       "Applies on the next ▶ Run, no restart. More quality = smoother edges, more work per frame."));
            page.Children.Add(Row(Loc.Tr("Suavizado 2D", "2D smoothing"),
                Combo(Supersample2DOptions, _rendering.SupersampleScale2D, v => _rendering.SupersampleScale2D = v, Loc.Tr("Suavizado 2D", "2D smoothing")),
                Loc.Tr("Dibuja el canvas a mayor resolución y lo reduce (supersampling).", "Draws the canvas at a higher resolution and scales it down (supersampling).")));
            page.Children.Add(Row(Loc.Tr("Suavizado 3D", "3D smoothing"),
                Combo(Msaa3DOptions, _rendering.MsaaSamples3D, v => _rendering.MsaaSamples3D = v, Loc.Tr("Suavizado 3D", "3D smoothing")),
                Loc.Tr("Para sketches con Renderer3D (MSAA).", "For Renderer3D sketches (MSAA).")));
            page.Children.Add(Check(Loc.Tr("Código optimizado (más rápido)", "Optimized code (faster)"), _rendering.OptimizeCode, v => _rendering.OptimizeCode = v,
                Loc.Tr("Compila el sketch con el optimizador de C#: las cuentas pesadas (simulaciones, miles de partículas) corren hasta 2 veces más rápido. A cambio, cuando el sketch falla, la línea que se marca puede quedar corrida. Para depurar, dejalo apagado.",
                       "Compiles the sketch with the C# optimizer: heavy math (simulations, thousands of particles) runs up to 2x faster. In exchange, when the sketch crashes, the line it marks can be a little off. Leave it off while debugging.")));
            page.Children.Add(ResetButton(Loc.Tr("Volver a los valores recomendados", "Back to the recommended values"), () => _rendering = RenderingSettings.Default()));
            return page;
        }

        private Control BuildPerformancePage()
        {
            var page = Page(Loc.Tr("Modo performance", "Performance mode"),
                Loc.Tr("F11: el sketch a pantalla completa con tu código flotando encima. Así se ve el código sobre los visuales.",
                       "F11: the sketch full screen with your code floating on top. This is how the code looks over the visuals."));

            // Live preview: some "visuals", the band, the code.
            var codeText = new TextBlock
            {
                Text = "public override void Draw()\n{\n    Background(12, 14, 30);\n    Circle(MouseX, MouseY, 40 * Pulse());\n}",
                FontFamily = ClayTheme.FontMono,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White,
            };
            var band = new Border { Padding = new Thickness(16, 12), Child = codeText };
            var visuals = new Border
            {
                Height = 190,
                CornerRadius = new CornerRadius(14),
                ClipToBounds = true,
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Avalonia.Media.Color.Parse("#2B1055"), 0),
                        new GradientStop(Avalonia.Media.Color.Parse("#D53A9D"), 0.55),
                        new GradientStop(Avalonia.Media.Color.Parse("#FFB08C"), 1),
                    },
                },
                Child = new Panel
                {
                    Children =
                    {
                        new Avalonia.Controls.Shapes.Ellipse { Width = 120, Height = 120, Fill = new SolidColorBrush(Avalonia.Media.Color.Parse("#7DE2FF")), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 40, 0) },
                        new Avalonia.Controls.Shapes.Ellipse { Width = 60, Height = 60, Fill = new SolidColorBrush(Avalonia.Media.Color.Parse("#FFE678")), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(60, 0, 0, 16) },
                        band,
                    },
                },
            };
            AutomationProperties.SetName(visuals, Loc.Tr("Vista previa del código sobre los visuales", "Preview of the code over the visuals"));
            void Refresh()
            {
                band.Background = new SolidColorBrush(Colors.Black, _performance.BackdropOpacity);
                codeText.Opacity = _performance.CodeOpacity;
                codeText.FontSize = _performance.FontSize * 0.55; // the preview is smaller than a projector
            }
            Refresh();
            page.Children.Add(visuals);

            page.Children.Add(Row(Loc.Tr("Transparencia del código", "Code transparency"),
                SliderWithValue(PerformanceSettings.MinCodeOpacity, PerformanceSettings.MaxCodeOpacity, _performance.CodeOpacity,
                    v => Loc.Tr($"{v * 100:0}% visible", $"{v * 100:0}% visible"),
                    v => { _performance.CodeOpacity = v; Refresh(); }, Loc.Tr("Transparencia del código", "Code transparency"), step: 0.05),
                Loc.Tr("100% código sólido · 20% apenas un fantasma sobre los visuales. En escena: Ctrl+Shift ↑/↓.",
                       "100% solid code · 20% just a ghost over the visuals. On stage: Ctrl+Shift ↑/↓.")));
            page.Children.Add(Row(Loc.Tr("Fondo detrás del código", "Backdrop behind the code"),
                SliderWithValue(PerformanceMode.MinOpacity, PerformanceMode.MaxOpacity, _performance.BackdropOpacity,
                    v => $"{v * 100:0}%",
                    v => { _performance.BackdropOpacity = v; Refresh(); }, Loc.Tr("Fondo detrás del código", "Backdrop behind the code"), step: 0.05),
                Loc.Tr("Una banda oscura para leer mejor. En escena: Ctrl ↑/↓.", "A dark band so the code reads better. On stage: Ctrl ↑/↓.")));
            page.Children.Add(Row(Loc.Tr("Tamaño del código", "Code size"),
                SliderWithValue(PerformanceMode.MinFontSize, PerformanceMode.MaxFontSize, _performance.FontSize,
                    v => $"{v:0} pt",
                    v => { _performance.FontSize = Math.Round(v); Refresh(); }, Loc.Tr("Tamaño del código", "Code size"), step: 2),
                Loc.Tr("En escena: Ctrl + / Ctrl −. Lo que cambies en escena se recuerda acá.", "On stage: Ctrl + / Ctrl −. Whatever you change on stage is remembered here.")));
            page.Children.Add(ResetButton(Loc.Tr("Volver a los valores originales", "Back to the original values"), () => _performance = PerformanceSettings.Default()));
            return page;
        }

        private Control BuildAssistantPage()
        {
            var page = Page(Loc.Tr("Asistente", "Assistant"),
                Loc.Tr("Un asistente de IA que ve tu sketch y la referencia. Necesita tu propia API key; la IDE no incluye ninguna.",
                       "An AI assistant that sees your sketch and the reference. It needs your own API key; the IDE doesn't include one."));
            page.Children.Add(Row(Loc.Tr("Proveedor", "Provider"),
                Combo(AgentProviderOptions, _agent.Provider, v => _agent.Provider = v, Loc.Tr("Proveedor", "Provider")), null));
            var key = new TextBox { Text = _agent.ApiKey ?? "", PasswordChar = '•', Width = 300, FontFamily = ClayTheme.FontMono, FontSize = 12.5 };
            AutomationProperties.SetName(key, "API key");
            key.TextChanged += (_, _) => _agent.ApiKey = string.IsNullOrWhiteSpace(key.Text) ? null : key.Text;
            page.Children.Add(Row("API key", key, null));
            var model = new TextBox { Text = _agent.Model ?? "", Width = 300, FontFamily = ClayTheme.FontMono, FontSize = 12.5, PlaceholderText = Loc.Tr("vacío = el que elija el proveedor", "blank = the provider's default") };
            AutomationProperties.SetName(model, Loc.Tr("Modelo", "Model"));
            model.TextChanged += (_, _) => _agent.Model = string.IsNullOrWhiteSpace(model.Text) ? null : model.Text;
            page.Children.Add(Row(Loc.Tr("Modelo (opcional)", "Model (optional)"), model, null));
            page.Children.Add(new TextBlock
            {
                Text = Loc.Tr(
                    "La API key se guarda en texto plano en %APPDATA%\\DanaProcessingIde\\agent-settings.json: no la compartas ni la subas a un repositorio.",
                    "The API key is stored in plain text at %APPDATA%\\DanaProcessingIde\\agent-settings.json: don't share it or upload it to a repository."),
                Foreground = ClayTheme.DangerHover,
                FontFamily = ClayTheme.FontBody,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 0),
            });
            return page;
        }

        // ====================================================================
        // Footer
        // ====================================================================

        private Control BuildFooter()
        {
            _restartNote.Foreground = ClayTheme.TextSecondary;
            _restartNote.FontFamily = ClayTheme.FontBody;
            _restartNote.FontSize = 12.5;
            _restartNote.TextWrapping = TextWrapping.Wrap;
            _restartNote.VerticalAlignment = VerticalAlignment.Center;
            _restartNote.Text = Loc.Tr("Algunos cambios se ven al reiniciar la IDE.", "Some changes show after restarting the IDE.");

            var cancel = new Button { Content = Loc.Tr("Cancelar", "Cancel"), Classes = { "clay-secondary" }, Padding = new Thickness(16, 8), Margin = new Thickness(0, 0, 8, 0) };
            cancel.Click += (_, _) =>
            {
                _savedOrDiscarded = true;
                ClayTheme.ApplyPalette(_originalTheme);
                Close();
            };
            var save = new Button { Content = Loc.Tr("Guardar", "Save"), Classes = { "clay-run" }, Padding = new Thickness(18, 8), CornerRadius = ClayTheme.RadiusButton };
            save.Click += (_, _) =>
            {
                SaveAll();
                Close();
            };
            _saveRestartButton.Content = Loc.Tr("Guardar y reiniciar", "Save and restart");
            _saveRestartButton.Classes.Add("clay-run");
            _saveRestartButton.Padding = new Thickness(18, 8);
            _saveRestartButton.CornerRadius = ClayTheme.RadiusButton;
            _saveRestartButton.Margin = new Thickness(8, 0, 0, 0);
            _saveRestartButton.Click += (_, _) =>
            {
                SaveAll();
                RestartApp();
            };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, save, _saveRestartButton } };
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(24, 12, 20, 16) };
            Grid.SetColumn(buttons, 1);
            grid.Children.Add(_restartNote);
            grid.Children.Add(buttons);
            return new Border { BorderBrush = ClayTheme.SurfaceHover, BorderThickness = new Thickness(0, 1, 0, 0), Child = grid };
        }

        private void UpdateRestartNote()
        {
            bool restart = NeedsRestart(_originalTheme, _theme, _originalLanguage, _language);
            _restartNote.IsVisible = restart;
            _saveRestartButton.IsVisible = restart;
        }

        private void SaveAll()
        {
            _savedOrDiscarded = true;
            ClayTheme.ApplyPalette(_theme);
            ThemeSettingsStore.Save(_theme);
            RenderingSettingsStore.Save(_rendering);
            LocaleSettingsStore.Save(new LocaleSettings { Language = _language });
            var updates = UpdateSettingsStore.Load();
            updates.AutoCheckEnabled = _autoUpdates;
            UpdateSettingsStore.Save(updates);
            var crashes = CrashReportSettingsStore.Load();
            crashes.Enabled = _crashReports;
            CrashReportSettingsStore.Save(crashes);
            AgentSettingsStore.Save(_agent);
            PerformanceSettingsStore.Save(_performance);
        }

        /// <summary>Starts the IDE again so language, shapes and fonts are rebuilt from the saved settings.</summary>
        private static void RestartApp()
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                try
                {
                    Process.Start(exePath);
                }
                catch
                {
                    // Couldn't relaunch: the settings are saved, the next start picks them up.
                }
            }
            Environment.Exit(0);
        }

        // ====================================================================
        // Building blocks
        // ====================================================================

        private static StackPanel Page(string title, string? intro)
        {
            var page = new StackPanel { Spacing = 6, Margin = new Thickness(28, 24, 28, 24), MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };
            page.Children.Add(new TextBlock
            {
                Text = title,
                FontFamily = ClayTheme.FontDisplay,
                FontWeight = FontWeight.SemiBold,
                FontSize = 22,
                Foreground = ClayTheme.TextPrimary,
            });
            if (intro != null)
                page.Children.Add(new TextBlock { Text = intro, Foreground = ClayTheme.TextSecondary, FontFamily = ClayTheme.FontBody, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
            else
                page.Children.Add(new Border { Height = 8 });
            return page;
        }

        private static TextBlock SubTitle(string text) => new()
        {
            Text = text,
            FontFamily = ClayTheme.FontDisplay,
            FontWeight = FontWeight.SemiBold,
            FontSize = 14,
            Foreground = ClayTheme.TextPrimary,
            Margin = new Thickness(0, 14, 0, 4),
        };

        private static TextBlock Hint(string text) => new()
        {
            Text = text,
            Foreground = ClayTheme.TextMuted,
            FontFamily = ClayTheme.FontBody,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };

        /// <summary>A label on the left, the control on the right, an optional hint under the control.</summary>
        private static Control Row(string label, Control control, string? hint)
        {
            var labelBlock = new TextBlock
            {
                Text = label,
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontBody,
                FontSize = 13.5,
                FontWeight = FontWeight.Medium,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 16, 0),
            };
            var right = new StackPanel { Spacing = 4, Children = { control } };
            if (hint != null)
                right.Children.Add(Hint(hint));
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("190,*"), Margin = new Thickness(0, 6) };
            Grid.SetColumn(right, 1);
            grid.Children.Add(labelBlock);
            grid.Children.Add(right);
            return grid;
        }

        private static Control Check(string label, bool value, Action<bool> set, string hint)
        {
            var box = new CheckBox
            {
                IsChecked = value,
                Content = new TextBlock { Text = label, FontFamily = ClayTheme.FontBody, FontSize = 13.5, FontWeight = FontWeight.Medium, Foreground = ClayTheme.TextPrimary, TextWrapping = TextWrapping.Wrap },
            };
            box.IsCheckedChanged += (_, _) => set(box.IsChecked == true);
            var hintBlock = Hint(hint);
            hintBlock.Margin = new Thickness(28, 0, 0, 0);
            return new StackPanel { Spacing = 2, Margin = new Thickness(0, 8), Children = { box, hintBlock } };
        }

        private static ComboBox Combo<T>((string Label, T Value)[] options, T current, Action<T> set, string name)
        {
            var combo = new ComboBox { Width = 300, ItemsSource = options.Select(o => o.Label).ToList() };
            var index = Array.FindIndex(options, o => Equals(o.Value, current));
            combo.SelectedIndex = index >= 0 ? index : 0;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedIndex >= 0)
                    set(options[combo.SelectedIndex].Value);
            };
            AutomationProperties.SetName(combo, name);
            return combo;
        }

        private static Control SliderWithValue(double min, double max, double value, Func<double, string> format, Action<double> set, string name, double step)
        {
            var valueText = new TextBlock { Text = format(value), FontFamily = ClayTheme.FontMono, FontSize = 12.5, Foreground = ClayTheme.TextSecondary, VerticalAlignment = VerticalAlignment.Center, MinWidth = 92 };
            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = Math.Clamp(value, min, max),
                Width = 220,
                SmallChange = step,
                LargeChange = step * 4,
                TickFrequency = step,
                IsSnapToTickEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(slider, name);
            slider.ValueChanged += (_, e) =>
            {
                valueText.Text = format(e.NewValue);
                set(e.NewValue);
            };
            return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { slider, valueText } };
        }

        private Control ColorRow(string label, Func<ThemeSettings, string> get, Action<ThemeSettings, string> set)
        {
            var swatch = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#D8D0C6")),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
            };
            void Paint(string hex)
            {
                try { swatch.Background = new SolidColorBrush(Avalonia.Media.Color.Parse(hex)); }
                catch (FormatException) { /* still typing */ }
            }
            Paint(get(_theme));
            var box = new TextBox { Text = get(_theme), Width = 110, FontFamily = ClayTheme.FontMono, FontSize = 12.5 };
            AutomationProperties.SetName(box, label);
            box.TextChanged += (_, _) =>
            {
                var hex = box.Text?.Trim() ?? "";
                try
                {
                    Avalonia.Media.Color.Parse(hex);
                }
                catch (FormatException)
                {
                    return; // an unfinished value: wait for the rest
                }
                set(_theme, hex);
                Paint(hex);
                ClayTheme.ApplyPalette(_theme); // live preview
            };
            return Row(label, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { swatch, box } }, null);
        }

        private Control FontRow(string label, Func<string> get, Action<string> set)
        {
            var box = new TextBox { Text = get(), FontFamily = ClayTheme.FontMono, FontSize = 12, MinWidth = 300 };
            AutomationProperties.SetName(box, label);
            box.TextChanged += (_, _) =>
            {
                set(box.Text ?? "");
                UpdateRestartNote();
            };
            return Row(label, box, null);
        }

        private Control ResetButton(string text, Action reset)
        {
            var button = new Button
            {
                Content = text,
                Classes = { "clay-secondary" },
                Padding = new Thickness(14, 8),
                Margin = new Thickness(0, 18, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            button.Click += (_, _) =>
            {
                reset();
                ShowSection(_current); // redraw this page with the restored values
            };
            return button;
        }

        private static IEnumerable<(string Label, Func<ThemeSettings, string> Get, Action<ThemeSettings, string> Set)> AllColors() => new (string, Func<ThemeSettings, string>, Action<ThemeSettings, string>)[]
        {
            (Loc.Tr("Fondo", "Background"), s => s.BaseColor, (s, v) => s.BaseColor = v),
            (Loc.Tr("Superficie", "Surface"), s => s.SurfaceColor, (s, v) => s.SurfaceColor = v),
            (Loc.Tr("Superficie elevada", "Raised surface"), s => s.SurfaceRaisedColor, (s, v) => s.SurfaceRaisedColor = v),
            (Loc.Tr("Superficie bajo el puntero", "Surface on hover"), s => s.SurfaceHoverColor, (s, v) => s.SurfaceHoverColor = v),
            (Loc.Tr("Acento", "Accent"), s => s.AccentColor, (s, v) => s.AccentColor = v),
            (Loc.Tr("Acento presionado", "Accent, pressed"), s => s.AccentDimColor, (s, v) => s.AccentDimColor = v),
            (Loc.Tr("Texto sobre el acento", "Text on the accent"), s => s.OnAccentColor, (s, v) => s.OnAccentColor = v),
            (Loc.Tr("Secundario", "Secondary"), s => s.SecondaryColor, (s, v) => s.SecondaryColor = v),
            (Loc.Tr("Éxito", "Success"), s => s.SuccessColor, (s, v) => s.SuccessColor = v),
            (Loc.Tr("Peligro", "Danger"), s => s.DangerColor, (s, v) => s.DangerColor = v),
            (Loc.Tr("Texto", "Text"), s => s.TextPrimaryColor, (s, v) => s.TextPrimaryColor = v),
            (Loc.Tr("Texto secundario", "Secondary text"), s => s.TextSecondaryColor, (s, v) => s.TextSecondaryColor = v),
            (Loc.Tr("Texto tenue", "Muted text"), s => s.TextMutedColor, (s, v) => s.TextMutedColor = v),
        };
    }
}
