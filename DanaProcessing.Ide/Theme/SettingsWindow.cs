using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// Lets the user edit ClayTheme's palette and "detalles" (roundness, fonts).
    /// Colors preview live in whatever window opened this one — see the
    /// SolidColorBrush/LinearGradientBrush remarks in ClayTheme.cs for why
    /// that works without any extra plumbing. Corner-radius, font, and
    /// language changes are saved too, but only take visual effect once the
    /// app restarts (struct/immutable values, and every Loc.Tr() call, baked
    /// into controls at construction time) — hence the "Guardar y reiniciar
    /// ahora" button.
    /// </summary>
    public class SettingsWindow : Window
    {
        private readonly ThemeSettings _original;
        private ThemeSettings _working;

        private readonly RenderingSettings _originalRendering;
        private RenderingSettings _workingRendering;

        private readonly AppLanguage _originalLanguage;
        private AppLanguage _workingLanguage;

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

        // Language names are shown in their OWN language regardless of the
        // current UI language — the standard convention for a language
        // picker (an English speaker still recognizes "Español" faster than
        // a translated "Spanish" would help a Spanish speaker find it).
        private static readonly (string Label, AppLanguage Value)[] LanguageOptions =
        {
            ("Español", AppLanguage.Spanish),
            ("English", AppLanguage.English),
        };

        private static readonly (string Label, bool Value)[] AutoUpdateCheckOptions =
        {
            (Loc.Tr("Sí", "Yes"), true),
            (Loc.Tr("No", "No"), false),
        };

        private readonly List<(string Label, Func<ThemeSettings, string> Get, Action<ThemeSettings, string> Set)> _colorFields;

        private readonly List<(string Label, Func<ThemeSettings, double> Get, Action<ThemeSettings, double> Set, double Max)> _radiusFields;

        /// <summary>`initialRendering`/`initialLanguage`: only passed by the
        /// "Restaurar valores predeterminados" flow below, to seed the
        /// replacement window with freshly-defaulted values instead of
        /// whatever's still on disk (which the reset button doesn't touch
        /// until "Guardar" is pressed) — every other caller leaves these null
        /// and gets the saved settings, same as ThemeSettings' own
        /// _original/_working pair.</summary>
        public SettingsWindow(RenderingSettings? initialRendering = null, AppLanguage? initialLanguage = null)
        {
            _colorFields = new()
            {
                (Loc.Tr("Fondo (Base)", "Background (Base)"), s => s.BaseColor, (s, v) => s.BaseColor = v),
                (Loc.Tr("Superficie", "Surface"), s => s.SurfaceColor, (s, v) => s.SurfaceColor = v),
                (Loc.Tr("Superficie elevada", "Raised surface"), s => s.SurfaceRaisedColor, (s, v) => s.SurfaceRaisedColor = v),
                (Loc.Tr("Hover de superficie", "Surface hover"), s => s.SurfaceHoverColor, (s, v) => s.SurfaceHoverColor = v),
                (Loc.Tr("Acento", "Accent"), s => s.AccentColor, (s, v) => s.AccentColor = v),
                (Loc.Tr("Acento (oscuro)", "Accent (dark)"), s => s.AccentDimColor, (s, v) => s.AccentDimColor = v),
                (Loc.Tr("Texto sobre acento", "Text on accent"), s => s.OnAccentColor, (s, v) => s.OnAccentColor = v),
                (Loc.Tr("Secundario", "Secondary"), s => s.SecondaryColor, (s, v) => s.SecondaryColor = v),
                (Loc.Tr("Éxito", "Success"), s => s.SuccessColor, (s, v) => s.SuccessColor = v),
                (Loc.Tr("Peligro", "Danger"), s => s.DangerColor, (s, v) => s.DangerColor = v),
                (Loc.Tr("Texto principal", "Primary text"), s => s.TextPrimaryColor, (s, v) => s.TextPrimaryColor = v),
                (Loc.Tr("Texto secundario", "Secondary text"), s => s.TextSecondaryColor, (s, v) => s.TextSecondaryColor = v),
                (Loc.Tr("Texto atenuado", "Muted text"), s => s.TextMutedColor, (s, v) => s.TextMutedColor = v),
            };

            _radiusFields = new()
            {
                (Loc.Tr("Radio chico (badges, ítems de lista)", "Small radius (badges, list items)"), s => s.RadiusSmall, (s, v) => s.RadiusSmall = v, 32),
                (Loc.Tr("Radio mediano", "Medium radius"), s => s.RadiusMedium, (s, v) => s.RadiusMedium = v, 32),
                (Loc.Tr("Radio grande", "Large radius"), s => s.RadiusLarge, (s, v) => s.RadiusLarge = v, 40),
                (Loc.Tr("Radio de tarjetas (editor/canvas)", "Card radius (editor/canvas)"), s => s.RadiusCard, (s, v) => s.RadiusCard = v, 48),
                (Loc.Tr("Radio de botones", "Button radius"), s => s.RadiusButton, (s, v) => s.RadiusButton = v, 32),
                (Loc.Tr("Radio de botones de ventana", "Window button radius"), s => s.RadiusChrome, (s, v) => s.RadiusChrome = v, 20),
            };

            _original = ClayTheme.CurrentSettings.Clone();
            _working = _original.Clone();

            _originalRendering = initialRendering ?? RenderingSettingsStore.Load();
            _workingRendering = _originalRendering.Clone();

            _originalLanguage = initialLanguage ?? LocaleSettingsStore.Load().Language;
            _workingLanguage = _originalLanguage;

            Title = Loc.Tr("Configuración — DanaProcessing IDE", "Settings — DanaProcessing IDE");
            Width = 720;
            Height = 820;
            MinWidth = 560;
            MinHeight = 480;
            CanResize = true;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            var root = new StackPanel { Spacing = 4, Margin = new Thickness(24, 20, 24, 16) };

            root.Children.Add(SectionTitle(Loc.Tr("Colores", "Colors")));
            root.Children.Add(new TextBlock
            {
                Text = Loc.Tr("Se aplican al instante en la ventana principal.", "Applied instantly in the main window."),
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 8),
            });
            foreach (var field in _colorFields)
                root.Children.Add(BuildColorRow(field.Label, field.Get, field.Set));

            root.Children.Add(SectionTitle(Loc.Tr("Formas", "Shapes"), topMargin: 20));
            root.Children.Add(new TextBlock
            {
                Text = Loc.Tr("Redondez de tarjetas, botones y ventana. Se ven al reiniciar la app.", "Roundness of cards, buttons and the window. Takes effect on app restart."),
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 8),
            });
            foreach (var field in _radiusFields)
                root.Children.Add(BuildRadiusRow(field.Label, field.Get, field.Set, field.Max));

            root.Children.Add(SectionTitle(Loc.Tr("Tipografía", "Typography"), topMargin: 20));
            root.Children.Add(new TextBlock
            {
                Text = Loc.Tr("Listas de fuentes separadas por coma (se usa la primera disponible). Se ven al reiniciar la app.", "Comma-separated font lists (the first available one is used). Takes effect on app restart."),
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 8),
            });
            root.Children.Add(BuildFontRow(Loc.Tr("Fuente de títulos", "Heading font"), () => _working.FontDisplay, v => _working.FontDisplay = v));
            root.Children.Add(BuildFontRow(Loc.Tr("Fuente de texto", "Body font"), () => _working.FontBody, v => _working.FontBody = v));
            root.Children.Add(BuildFontRow(Loc.Tr("Fuente monoespaciada (editor)", "Monospace font (editor)"), () => _working.FontMono, v => _working.FontMono = v));

            root.Children.Add(SectionTitle(Loc.Tr("Renderizado", "Rendering"), topMargin: 20));
            root.Children.Add(new TextBlock
            {
                Text = Loc.Tr(
                    "Calidad de antialiasing de los sketches. Se aplica la próxima vez que ejecutes un sketch (▶ Run) — no hace falta reiniciar la IDE. Niveles más altos se ven mejor pero consumen más CPU/GPU por frame.",
                    "Sketch antialiasing quality. Applied the next time you run a sketch (▶ Run) — no need to restart the IDE. Higher levels look better but cost more CPU/GPU per frame."),
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
            });
            root.Children.Add(BuildComboRow(
                Loc.Tr("Antialiasing 2D (supersampling)", "2D antialiasing (supersampling)"),
                Supersample2DOptions,
                () => _workingRendering.SupersampleScale2D,
                v => _workingRendering.SupersampleScale2D = v));
            root.Children.Add(BuildComboRow(
                Loc.Tr("Antialiasing 3D (MSAA)", "3D antialiasing (MSAA)"),
                Msaa3DOptions,
                () => _workingRendering.MsaaSamples3D,
                v => _workingRendering.MsaaSamples3D = v));

            root.Children.Add(SectionTitle(Loc.Tr("Idioma", "Language"), topMargin: 20));
            root.Children.Add(new TextBlock
            {
                Text = Loc.Tr("Cambia el idioma de toda la interfaz. Se ve al reiniciar la app.", "Changes the language of the whole interface. Takes effect on app restart."),
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 8),
            });
            root.Children.Add(BuildComboRow(
                Loc.Tr("Idioma de la interfaz", "Interface language"),
                LanguageOptions,
                () => _workingLanguage,
                v => _workingLanguage = v));

            root.Children.Add(SectionTitle(Loc.Tr("Actualizaciones", "Updates"), topMargin: 20));
            root.Children.Add(new TextBlock
            {
                Text = Loc.Tr(
                    "Si está activado, la IDE consulta GitHub al arrancar para avisarte si hay una versión más nueva -- solo avisa, nunca descarga ni reemplaza nada por su cuenta. \"Buscar actualizaciones...\" en el menú ☰ siempre funciona, esté activado esto o no.",
                    "When on, the IDE checks GitHub on startup to let you know if a newer version exists -- it only notifies, it never downloads or replaces anything on its own. \"Check for updates...\" in the ☰ menu always works regardless of this setting."),
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
            });
            // Saved immediately on change, unlike the working-copy fields
            // above -- nothing currently on screen depends on this setting,
            // so there's no live preview to apply-on-OK or revert-on-Cancel.
            root.Children.Add(BuildComboRow(
                Loc.Tr("Buscar actualizaciones automáticamente", "Automatically check for updates"),
                AutoUpdateCheckOptions,
                () => Updates.UpdateSettingsStore.Load().AutoCheckEnabled,
                v =>
                {
                    var settings = Updates.UpdateSettingsStore.Load();
                    settings.AutoCheckEnabled = v;
                    Updates.UpdateSettingsStore.Save(settings);
                }));

            root.Children.Add(BuildButtonRow());

            Content = new ScrollViewer { Content = root };

            Closing += (_, _) =>
            {
                // If the user closes the window with the OS "X" instead of
                // pressing "Cancelar", don't leave the live preview dangling
                // on unsaved colors — revert to whatever was actually saved.
                if (!_savedOrDiscarded)
                    ClayTheme.ApplyPalette(_original);
            };
        }

        private bool _savedOrDiscarded;

        private TextBlock SectionTitle(string text, double topMargin = 0) => new()
        {
            Text = text,
            Foreground = ClayTheme.TextPrimary,
            FontFamily = ClayTheme.FontDisplay,
            FontWeight = FontWeight.SemiBold,
            FontSize = 15,
            Margin = new Thickness(0, topMargin, 0, 0),
        };

        private Control BuildColorRow(string label, Func<ThemeSettings, string> get, Action<ThemeSettings, string> set)
        {
            var swatch = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(6),
                BorderBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#E8E2DA")),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
            };
            void RefreshSwatch(string hex)
            {
                try
                { swatch.Background = new SolidColorBrush(Avalonia.Media.Color.Parse(hex)); }
                catch { /* invalid hex while typing — leave swatch as-is */ }
            }
            RefreshSwatch(get(_working));

            var textBox = new TextBox
            {
                Text = get(_working),
                Width = 100,
                FontFamily = ClayTheme.FontMono,
                FontSize = 12.5,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            textBox.TextChanged += (_, _) =>
            {
                var hex = textBox.Text?.Trim() ?? "";
                try
                {
                    Avalonia.Media.Color.Parse(hex); // throws on invalid input
                    set(_working, hex);
                    RefreshSwatch(hex);
                    ClayTheme.ApplyPalette(_working); // live preview
                }
                catch
                {
                    // Still typing an incomplete hex value — wait for more input.
                }
            };

            var labelBlock = new TextBlock
            {
                Text = label,
                Foreground = ClayTheme.TextSecondary,
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 230,
            };

            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Margin = new Thickness(0, 3),
                Children = { swatch, labelBlock, textBox }
            };
        }

        private Control BuildRadiusRow(string label, Func<ThemeSettings, double> get, Action<ThemeSettings, double> set, double max)
        {
            var valueLabel = new TextBlock
            {
                Text = $"{get(_working):0} px",
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontMono,
                FontSize = 12,
                Width = 44,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var slider = new Slider
            {
                Minimum = 0,
                Maximum = max,
                Value = get(_working),
                Width = 160,
                VerticalAlignment = VerticalAlignment.Center,
            };
            slider.ValueChanged += (_, e) =>
            {
                set(_working, e.NewValue);
                valueLabel.Text = $"{e.NewValue:0} px";
            };

            var labelBlock = new TextBlock
            {
                Text = label,
                Foreground = ClayTheme.TextSecondary,
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 230,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            };

            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Margin = new Thickness(0, 3),
                Children = { labelBlock, slider, valueLabel }
            };
        }

        private Control BuildComboRow<T>(string label, (string Label, T Value)[] options, Func<T> get, Action<T> set)
        {
            var combo = new ComboBox
            {
                Width = 260,
                VerticalAlignment = VerticalAlignment.Center,
                ItemsSource = options.Select(o => o.Label).ToList(),
            };
            var currentValue = get();
            var selectedIndex = Array.FindIndex(options, o => Equals(o.Value, currentValue));
            combo.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedIndex >= 0)
                    set(options[combo.SelectedIndex].Value);
            };

            var labelBlock = new TextBlock
            {
                Text = label,
                Foreground = ClayTheme.TextSecondary,
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 230,
            };

            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Margin = new Thickness(0, 3),
                Children = { labelBlock, combo }
            };
        }

        private Control BuildFontRow(string label, Func<string> get, Action<string> set)
        {
            var textBox = new TextBox
            {
                Text = get(),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FontFamily = ClayTheme.FontMono,
                FontSize = 11.5,
            };
            textBox.TextChanged += (_, _) => set(textBox.Text ?? "");

            var labelBlock = new TextBlock
            {
                Text = label,
                Foreground = ClayTheme.TextSecondary,
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 2),
            };

            return new StackPanel
            {
                Spacing = 4,
                Margin = new Thickness(0, 6),
                Children = { labelBlock, textBox }
            };
        }

        private Control BuildButtonRow()
        {
            var resetButton = new Button
            {
                Content = Loc.Tr("Restaurar valores predeterminados", "Restore default values"),
                Classes = { "clay-secondary" },
                Padding = new Thickness(14, 8),
                FontSize = 12.5,
            };
            resetButton.Click += (_, _) =>
            {
                _savedOrDiscarded = true;
                _working = ThemeSettings.Default();
                ClayTheme.ApplyPalette(_working); // live-preview the reset colors too
                // Rebuilding the whole window is the simplest way to refresh
                // every slider/textbox/swatch/combo to the restored values —
                // the new window's constructor reads ClayTheme.CurrentSettings
                // (which ApplyPalette just updated above) for colors/fonts,
                // and the explicit RenderingSettings.Default()/AppLanguage.Spanish
                // here for the rendering/language combos (those have no
                // live-global equivalent to resync from the way colors do).
                var replacement = new SettingsWindow(RenderingSettings.Default(), AppLanguage.Spanish);
                Close();
                replacement.Show();
            };

            var cancelButton = new Button
            {
                Content = Loc.Tr("Cancelar", "Cancel"),
                Classes = { "clay-secondary" },
                Padding = new Thickness(14, 8),
                FontSize = 12.5,
            };
            cancelButton.Click += (_, _) =>
            {
                _savedOrDiscarded = true;
                ClayTheme.ApplyPalette(_original); // revert the live color preview
                Close();
            };

            var saveButton = new Button
            {
                Content = Loc.Tr("Guardar", "Save"),
                Classes = { "clay-run" },
                Padding = new Thickness(16, 8),
                CornerRadius = ClayTheme.RadiusButton,
                FontSize = 12.5,
            };
            saveButton.Click += (_, _) =>
            {
                _savedOrDiscarded = true;
                ClayTheme.ApplyPalette(_working);
                ThemeSettingsStore.Save(_working);
                RenderingSettingsStore.Save(_workingRendering);
                LocaleSettingsStore.Save(new LocaleSettings { Language = _workingLanguage });
                Close();
            };

            var saveRestartButton = new Button
            {
                Content = Loc.Tr("Guardar y reiniciar ahora", "Save and restart now"),
                Classes = { "clay-run" },
                Padding = new Thickness(16, 8),
                CornerRadius = ClayTheme.RadiusButton,
                FontSize = 12.5,
            };
            saveRestartButton.Click += (_, _) =>
            {
                _savedOrDiscarded = true;
                ThemeSettingsStore.Save(_working);
                RenderingSettingsStore.Save(_workingRendering);
                LocaleSettingsStore.Save(new LocaleSettings { Language = _workingLanguage });
                RestartApp();
            };

            resetButton.Margin = new Thickness(0, 0, 8, 8);
            cancelButton.Margin = new Thickness(0, 0, 8, 8);
            saveButton.Margin = new Thickness(0, 0, 8, 8);
            saveRestartButton.Margin = new Thickness(0, 0, 0, 8);

            return new WrapPanel
            {
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { resetButton, cancelButton, saveButton, saveRestartButton }
            };
        }

        /// <summary>
        /// Relaunches the whole process so every window is rebuilt from
        /// scratch against the newly saved settings — the only reliable way
        /// to propagate the corner-radius/font/language changes, since those
        /// are baked by value into controls that already exist.
        /// </summary>
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
                    // If relaunching fails for any reason, just exit — the
                    // user can start the app again manually, and the saved
                    // settings will already be in place next time.
                }
            }
            Environment.Exit(0);
        }
    }
}
