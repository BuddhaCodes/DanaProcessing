using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// Lets the user edit ClayTheme's palette and "detalles" (roundness, fonts).
    /// Colors preview live in whatever window opened this one — see the
    /// SolidColorBrush/LinearGradientBrush remarks in ClayTheme.cs for why
    /// that works without any extra plumbing. Corner-radius and font changes
    /// are saved too, but only take visual effect once the app restarts
    /// (struct/immutable values baked into controls at construction time) —
    /// hence the "Guardar y reiniciar ahora" button.
    /// </summary>
    public class SettingsWindow : Window
    {
        private readonly ThemeSettings _original;
        private ThemeSettings _working;

        private readonly List<(string Label, Func<ThemeSettings, string> Get, Action<ThemeSettings, string> Set)> _colorFields = new()
        {
            ("Fondo (Base)", s => s.BaseColor, (s, v) => s.BaseColor = v),
            ("Superficie", s => s.SurfaceColor, (s, v) => s.SurfaceColor = v),
            ("Superficie elevada", s => s.SurfaceRaisedColor, (s, v) => s.SurfaceRaisedColor = v),
            ("Hover de superficie", s => s.SurfaceHoverColor, (s, v) => s.SurfaceHoverColor = v),
            ("Acento", s => s.AccentColor, (s, v) => s.AccentColor = v),
            ("Acento (oscuro)", s => s.AccentDimColor, (s, v) => s.AccentDimColor = v),
            ("Texto sobre acento", s => s.OnAccentColor, (s, v) => s.OnAccentColor = v),
            ("Secundario", s => s.SecondaryColor, (s, v) => s.SecondaryColor = v),
            ("Éxito", s => s.SuccessColor, (s, v) => s.SuccessColor = v),
            ("Peligro", s => s.DangerColor, (s, v) => s.DangerColor = v),
            ("Texto principal", s => s.TextPrimaryColor, (s, v) => s.TextPrimaryColor = v),
            ("Texto secundario", s => s.TextSecondaryColor, (s, v) => s.TextSecondaryColor = v),
            ("Texto atenuado", s => s.TextMutedColor, (s, v) => s.TextMutedColor = v),
        };

        private readonly List<(string Label, Func<ThemeSettings, double> Get, Action<ThemeSettings, double> Set, double Max)> _radiusFields = new()
        {
            ("Radio chico (badges, ítems de lista)", s => s.RadiusSmall, (s, v) => s.RadiusSmall = v, 32),
            ("Radio mediano", s => s.RadiusMedium, (s, v) => s.RadiusMedium = v, 32),
            ("Radio grande", s => s.RadiusLarge, (s, v) => s.RadiusLarge = v, 40),
            ("Radio de tarjetas (editor/canvas)", s => s.RadiusCard, (s, v) => s.RadiusCard = v, 48),
            ("Radio de botones", s => s.RadiusButton, (s, v) => s.RadiusButton = v, 32),
            ("Radio de botones de ventana", s => s.RadiusChrome, (s, v) => s.RadiusChrome = v, 20),
        };

        public SettingsWindow()
        {
            _original = ClayTheme.CurrentSettings.Clone();
            _working = _original.Clone();

            Title = "Configuración — DanaProcessing IDE";
            Width = 720;
            Height = 820;
            MinWidth = 560;
            MinHeight = 480;
            CanResize = true;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            var root = new StackPanel { Spacing = 4, Margin = new Thickness(24, 20, 24, 16) };

            root.Children.Add(SectionTitle("Colores"));
            root.Children.Add(new TextBlock
            {
                Text = "Se aplican al instante en la ventana principal.",
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 8),
            });
            foreach (var field in _colorFields)
                root.Children.Add(BuildColorRow(field.Label, field.Get, field.Set));

            root.Children.Add(SectionTitle("Formas", topMargin: 20));
            root.Children.Add(new TextBlock
            {
                Text = "Redondez de tarjetas, botones y ventana. Se ven al reiniciar la app.",
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 8),
            });
            foreach (var field in _radiusFields)
                root.Children.Add(BuildRadiusRow(field.Label, field.Get, field.Set, field.Max));

            root.Children.Add(SectionTitle("Tipografía", topMargin: 20));
            root.Children.Add(new TextBlock
            {
                Text = "Listas de fuentes separadas por coma (se usa la primera disponible). Se ven al reiniciar la app.",
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 8),
            });
            root.Children.Add(BuildFontRow("Fuente de títulos", () => _working.FontDisplay, v => _working.FontDisplay = v));
            root.Children.Add(BuildFontRow("Fuente de texto", () => _working.FontBody, v => _working.FontBody = v));
            root.Children.Add(BuildFontRow("Fuente monoespaciada (editor)", () => _working.FontMono, v => _working.FontMono = v));

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
                Content = "Restaurar valores predeterminados",
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
                // every slider/textbox/swatch to the restored values — the new
                // window's constructor reads ClayTheme.CurrentSettings, which
                // ApplyPalette just updated above.
                var replacement = new SettingsWindow();
                Close();
                replacement.Show();
            };

            var cancelButton = new Button
            {
                Content = "Cancelar",
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
                Content = "Guardar",
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
                Close();
            };

            var saveRestartButton = new Button
            {
                Content = "Guardar y reiniciar ahora",
                Classes = { "clay-run" },
                Padding = new Thickness(16, 8),
                CornerRadius = ClayTheme.RadiusButton,
                FontSize = 12.5,
            };
            saveRestartButton.Click += (_, _) =>
            {
                _savedOrDiscarded = true;
                ThemeSettingsStore.Save(_working);
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
        /// scratch against the newly saved ThemeSettings — the only reliable
        /// way to propagate the corner-radius/font changes, since those are
        /// baked by value into controls that already exist.
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