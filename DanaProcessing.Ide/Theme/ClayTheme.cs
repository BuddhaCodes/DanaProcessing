using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.CodeCompletion;
using System.Collections.Generic;
using System.Linq;

namespace DanaProcessing.Ide.Theme
{
    public static class ClayTheme
    {
        // --- Superficies ---
        public static readonly IBrush Base = new SolidColorBrush(Avalonia.Media.Color.Parse("#F8F6F2"));
        public static readonly IBrush Surface = new SolidColorBrush(Avalonia.Media.Color.Parse("#FFFFFF"));
        public static readonly IBrush SurfaceRaised = new SolidColorBrush(Avalonia.Media.Color.Parse("#FDFBF8"));
        public static readonly IBrush SurfaceHigher = new SolidColorBrush(Avalonia.Media.Color.Parse("#FAF5EE"));

        // --- COLORES DE HOVER MUCHO MÁS VISIBLES ---
        public static readonly IBrush SurfaceHover = new SolidColorBrush(Avalonia.Media.Color.Parse("#E8DDD0"));  // Beige más oscuro
        public static readonly IBrush SurfacePressed = new SolidColorBrush(Avalonia.Media.Color.Parse("#D5C8B8")); // Beige aún más oscuro

        // --- Acentos ---
        public static readonly IBrush Accent = new SolidColorBrush(Avalonia.Media.Color.Parse("#FFB08C"));
        public static readonly IBrush AccentDim = new SolidColorBrush(Avalonia.Media.Color.Parse("#F09B78"));
        public static readonly IBrush AccentGlow = new SolidColorBrush(Avalonia.Media.Color.Parse("#FFB08C44"));
        public static readonly IBrush OnAccent = new SolidColorBrush(Avalonia.Media.Color.Parse("#4A3728"));

        public static readonly IBrush Secondary = new SolidColorBrush(Avalonia.Media.Color.Parse("#A8D0E6"));
        public static readonly IBrush SecondaryDim = new SolidColorBrush(Avalonia.Media.Color.Parse("#89B8D0"));

        // --- Estados ---
        public static readonly IBrush Success = new SolidColorBrush(Avalonia.Media.Color.Parse("#B5E6C3"));
        public static readonly IBrush SuccessSurface = new SolidColorBrush(Avalonia.Media.Color.Parse("#E8F5ED"));
        public static readonly IBrush Danger = new SolidColorBrush(Avalonia.Media.Color.Parse("#F5A0A0"));
        public static readonly IBrush DangerSurface = new SolidColorBrush(Avalonia.Media.Color.Parse("#FCE8E8"));
        public static readonly IBrush DangerHover = new SolidColorBrush(Avalonia.Media.Color.Parse("#E86A6A"));

        // --- Texto ---
        public static readonly IBrush TextPrimary = new SolidColorBrush(Avalonia.Media.Color.Parse("#2D2A24"));
        public static readonly IBrush TextSecondary = new SolidColorBrush(Avalonia.Media.Color.Parse("#5A554C"));
        public static readonly IBrush TextMuted = new SolidColorBrush(Avalonia.Media.Color.Parse("#9A958A"));
        public static readonly IBrush TextOnDanger = new SolidColorBrush(Avalonia.Media.Color.Parse("#FFFFFF"));

        // --- Radios ---
        public static readonly CornerRadius RadiusSmall = new CornerRadius(10);
        public static readonly CornerRadius RadiusMedium = new CornerRadius(14);
        public static readonly CornerRadius RadiusLarge = new CornerRadius(20);
        public static readonly CornerRadius RadiusCard = new CornerRadius(24);
        public static readonly CornerRadius RadiusButton = new CornerRadius(16);
        public static readonly CornerRadius RadiusChrome = new CornerRadius(8);
        public static readonly CornerRadius RadiusPanelTop = new CornerRadius(20, 20, 0, 0);
        public static readonly CornerRadius RadiusPill = new CornerRadius(999);

        // --- Sombras ---
        public static readonly BoxShadows ShadowRaised = BoxShadows.Parse("0 8 32 0 #1A000000, 0 2 0 0 #FFFFFF, 0 6 20 0 #0D000000");
        public static readonly BoxShadows ShadowSubtle = BoxShadows.Parse("0 4 16 0 #1A000000");
        public static readonly BoxShadows ShadowDeep = BoxShadows.Parse("0 16 48 0 #2A000000, 0 4 24 0 #1A000000");
        public static readonly BoxShadows ShadowGlow = BoxShadows.Parse("0 0 40 0 #FFB08C44, 0 4 16 0 #1A000000");
        public static readonly BoxShadows ShadowFocusRing = BoxShadows.Parse("0 0 0 2 #FFB08C88, 0 0 40 8 #FFB08C33, 0 16 48 0 #2A000000");

        // --- Fuentes ---
        public static readonly FontFamily FontDisplay = new FontFamily("Sora,Segoe UI Semibold,SF Pro Display,Inter,Arial");
        public static readonly FontFamily FontBody = new FontFamily("Inter,Segoe UI,SF Pro Text,Arial");
        public static readonly FontFamily FontMono = new FontFamily("Cascadia Code,JetBrains Mono,Consolas,Menlo,monospace");

        // --- Gradientes ---
        public static readonly IBrush AccentGradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Avalonia.Media.Color.Parse("#FFCDB2"), 0.0),
                new GradientStop(Avalonia.Media.Color.Parse("#FFB08C"), 0.55),
                new GradientStop(Avalonia.Media.Color.Parse("#F09B78"), 1.0),
            }
        };

        public static readonly IBrush AccentGradientHover = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Avalonia.Media.Color.Parse("#FFD9C0"), 0.0),
                new GradientStop(Avalonia.Media.Color.Parse("#FFC4A0"), 0.55),
                new GradientStop(Avalonia.Media.Color.Parse("#F5A888"), 1.0),
            }
        };

        public static readonly IBrush WindowBackground = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Avalonia.Media.Color.Parse("#FDFBF8"), 0.0),
                new GradientStop(Avalonia.Media.Color.Parse("#F8F6F2"), 0.55),
                new GradientStop(Avalonia.Media.Color.Parse("#F5F0EA"), 1.0),
            }
        };

        public static readonly IBrush TitleBarBackground = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Avalonia.Media.Color.Parse("#FDFBF8"), 0.0),
                new GradientStop(Avalonia.Media.Color.Parse("#F8F4EE"), 1.0),
            }
        };

        // ================================================================
        // ESTILOS DE BOTONES CON COLORES DE HOVER VISIBLES
        // ================================================================
        //
        // NOTA IMPORTANTE SOBRE EL BUG DE FLUENTTHEME:
        // El ControlTheme por defecto de Button en FluentTheme renderiza su
        // contenido a través de un ContentPresenter interno, y las reglas
        // internas del theme para :pointerover / :pressed fijan Background
        // y Foreground DIRECTAMENTE sobre ese ContentPresenter (no sobre la
        // propiedad Button.Background/Foreground que nosotros seteamos).
        // Como esas reglas son más específicas (apuntan al part exacto),
        // pisan lo que definimos a nivel de Button.
        //
        // Por eso, además de setear Button.Background/Foreground para el
        // estado base, replicamos el mismo valor apuntando explícitamente a
        // ".Template().OfType<ContentPresenter>()" en cada pseudo-clase
        // (normal, :pointerover, :pressed). Esto garantiza que el color que
        // realmente se pinta en pantalla sea el nuestro y no el del theme.
        // ================================================================

        public static Style[] ButtonEffectStyles() => new[]
        {
            // ============================================================
            // === RUN BUTTON ===
            // ============================================================
            new Style(x => x.OfType<Button>().Class("clay-run"))
            {
                Setters =
                {
                    new Setter(Button.ForegroundProperty, OnAccent),
                    new Setter(Button.FontFamilyProperty, FontDisplay),
                    new Setter(Button.FontWeightProperty, FontWeight.SemiBold),
                    new Setter(Button.FontSizeProperty, 13.0),
                    new Setter(Button.PaddingProperty, new Thickness(20, 8)),
                    new Setter(Button.CursorProperty, new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)),
                    new Setter(Button.BackgroundProperty, AccentGradient),
                    new Setter(Button.BorderThicknessProperty, new Thickness(0)),
                    new Setter(Button.CornerRadiusProperty, RadiusButton),
                    new Setter(Control.TransitionsProperty, new Transitions
                    {
                        new BrushTransition { Property = Button.BackgroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                        new DoubleTransition { Property = Visual.OpacityProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                    }),
                }
            },
            new Style(x => x.OfType<Button>().Class("clay-run").Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, AccentGradientHover),
                    new Setter(Visual.OpacityProperty, 0.92),
                }
            },
            new Style(x => x.OfType<Button>().Class("clay-run").Class(":pressed"))
            {
                Setters =
                {
                    new Setter(Visual.OpacityProperty, 0.8),
                }
            },

            // --- FIX ContentPresenter: Foreground (texto) en todos los estados ---
            new Style(x => x.OfType<Button>().Class("clay-run")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, OnAccent) }
            },
            new Style(x => x.OfType<Button>().Class("clay-run").Class(":pointerover")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, OnAccent) }
            },
            new Style(x => x.OfType<Button>().Class("clay-run").Class(":pressed")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, OnAccent) }
            },

            // --- FIX ContentPresenter: Background (fondo) en todos los estados ---
            new Style(x => x.OfType<Button>().Class("clay-run")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, AccentGradient) }
            },
            new Style(x => x.OfType<Button>().Class("clay-run").Class(":pointerover")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, AccentGradientHover) }
            },
            new Style(x => x.OfType<Button>().Class("clay-run").Class(":pressed")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, AccentGradientHover) }
            },

            // ============================================================
            // === SECONDARY BUTTON (Nuevo, Abrir, Guardar, Guardar como) ===
            // ============================================================
            new Style(x => x.OfType<Button>().Class("clay-secondary"))
            {
                Setters =
                {
                    new Setter(Button.ForegroundProperty, TextSecondary),
                    new Setter(Button.FontFamilyProperty, FontBody),
                    new Setter(Button.FontSizeProperty, 13.0),
                    new Setter(Button.PaddingProperty, new Thickness(14, 8)),
                    new Setter(Button.CursorProperty, new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)),
                    new Setter(Button.BackgroundProperty, Brushes.Transparent),
                    new Setter(Button.BorderThicknessProperty, new Thickness(0)),
                    new Setter(Button.CornerRadiusProperty, RadiusSmall),
                    new Setter(Control.TransitionsProperty, new Transitions
                    {
                        new BrushTransition { Property = Button.BackgroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                        new BrushTransition { Property = Button.ForegroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                    }),
                }
            },
            new Style(x => x.OfType<Button>().Class("clay-secondary").Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, SurfaceHover),
                }
            },
            new Style(x => x.OfType<Button>().Class("clay-secondary").Class(":pressed"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, SurfacePressed),
                }
            },

            // --- FIX ContentPresenter: Foreground (texto) ---
            new Style(x => x.OfType<Button>().Class("clay-secondary")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, TextSecondary) }
            },
            new Style(x => x.OfType<Button>().Class("clay-secondary").Class(":pointerover")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, TextPrimary) }
            },
            new Style(x => x.OfType<Button>().Class("clay-secondary").Class(":pressed")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, TextPrimary) }
            },

            // --- FIX ContentPresenter: Background (fondo) ---
            new Style(x => x.OfType<Button>().Class("clay-secondary")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent) }
            },
            new Style(x => x.OfType<Button>().Class("clay-secondary").Class(":pointerover")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, SurfaceHover) }
            },
            new Style(x => x.OfType<Button>().Class("clay-secondary").Class(":pressed")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, SurfacePressed) }
            },

            // ============================================================
            // === ICON BUTTON (usado por ej. en la ✕ de cada tab) ===
            // ============================================================
            new Style(x => x.OfType<Button>().Class("clay-icon"))
            {
                Setters =
                {
                    new Setter(Button.ForegroundProperty, TextMuted),
                    new Setter(Button.FontSizeProperty, 10.0),
                    new Setter(Button.PaddingProperty, new Thickness(4, 0)),
                    new Setter(Button.CursorProperty, new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)),
                    new Setter(Button.BackgroundProperty, Brushes.Transparent),
                    new Setter(Button.BorderThicknessProperty, new Thickness(0)),
                    new Setter(Button.CornerRadiusProperty, new CornerRadius(6)),
                    new Setter(Button.WidthProperty, 18.0),
                    new Setter(Button.HeightProperty, 18.0),
                    new Setter(Control.TransitionsProperty, new Transitions
                    {
                        new BrushTransition { Property = Button.BackgroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                        new BrushTransition { Property = Button.ForegroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                    }),
                }
            },
            new Style(x => x.OfType<Button>().Class("clay-icon").Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, SurfaceHover),
                    new Setter(Button.ForegroundProperty, TextPrimary),
                }
            },
            new Style(x => x.OfType<Button>().Class("clay-icon").Class(":pressed"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, SurfacePressed),
                }
            },

            // --- FIX ContentPresenter: Foreground (texto/icono) ---
            new Style(x => x.OfType<Button>().Class("clay-icon")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, TextMuted) }
            },
            new Style(x => x.OfType<Button>().Class("clay-icon").Class(":pointerover")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, TextPrimary) }
            },
            new Style(x => x.OfType<Button>().Class("clay-icon").Class(":pressed")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, TextPrimary) }
            },

            // --- FIX ContentPresenter: Background (fondo) ---
            new Style(x => x.OfType<Button>().Class("clay-icon")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent) }
            },
            new Style(x => x.OfType<Button>().Class("clay-icon").Class(":pointerover")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, SurfaceHover) }
            },
            new Style(x => x.OfType<Button>().Class("clay-icon").Class(":pressed")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, SurfacePressed) }
            },

            // ============================================================
            // === CHROME BUTTONS: minimizar (—) y maximizar (▢) ===
            // Antes NO estaban definidos en absoluto -> caían al estilo por
            // defecto de FluentTheme, casi invisibles sobre el titlebar claro.
            // Ahora: icono en negro (TextPrimary), fondo transparente en
            // reposo y un gris beige visible en hover/pressed.
            // ============================================================
            new Style(x => x.OfType<Button>().Class("clay-chrome"))
            {
                Setters =
                {
                    new Setter(Button.ForegroundProperty, TextPrimary),
                    new Setter(Button.FontFamilyProperty, FontBody),
                    new Setter(Button.FontSizeProperty, 12.0),
                    new Setter(Button.PaddingProperty, new Thickness(12, 8)),
                    new Setter(Button.CursorProperty, new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)),
                    new Setter(Button.BackgroundProperty, Brushes.Transparent),
                    new Setter(Button.BorderThicknessProperty, new Thickness(0)),
                    new Setter(Button.CornerRadiusProperty, RadiusChrome),
                    new Setter(Control.TransitionsProperty, new Transitions
                    {
                        new BrushTransition { Property = Button.BackgroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                        new BrushTransition { Property = Button.ForegroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                    }),
                }
            },
            new Style(x => x.OfType<Button>().Class("clay-chrome").Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, SurfaceHover),
                    new Setter(Button.ForegroundProperty, TextPrimary),
                }
            },
            new Style(x => x.OfType<Button>().Class("clay-chrome").Class(":pressed"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, SurfacePressed),
                }
            },

            // --- FIX ContentPresenter: Foreground ---
            new Style(x => x.OfType<Button>().Class("clay-chrome")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, TextPrimary) }
            },
            new Style(x => x.OfType<Button>().Class("clay-chrome").Class(":pointerover")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, TextPrimary) }
            },
            new Style(x => x.OfType<Button>().Class("clay-chrome").Class(":pressed")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, TextPrimary) }
            },

            // --- FIX ContentPresenter: Background ---
            new Style(x => x.OfType<Button>().Class("clay-chrome")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent) }
            },
            new Style(x => x.OfType<Button>().Class("clay-chrome").Class(":pointerover")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, SurfaceHover) }
            },
            new Style(x => x.OfType<Button>().Class("clay-chrome").Class(":pressed")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, SurfacePressed) }
            },

            // ============================================================
            // === CHROME CLOSE BUTTON (✕) — hover en rojo, texto blanco ===
            // ============================================================
            new Style(x => x.OfType<Button>().Class("clay-chrome-close"))
            {
                Setters =
                {
                    new Setter(Button.ForegroundProperty, TextPrimary),
                    new Setter(Button.FontFamilyProperty, FontBody),
                    new Setter(Button.FontSizeProperty, 12.0),
                    new Setter(Button.PaddingProperty, new Thickness(12, 8)),
                    new Setter(Button.CursorProperty, new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)),
                    new Setter(Button.BackgroundProperty, Brushes.Transparent),
                    new Setter(Button.BorderThicknessProperty, new Thickness(0)),
                    new Setter(Button.CornerRadiusProperty, RadiusChrome),
                    new Setter(Control.TransitionsProperty, new Transitions
                    {
                        new BrushTransition { Property = Button.BackgroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                        new BrushTransition { Property = Button.ForegroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                    }),
                }
            },
            new Style(x => x.OfType<Button>().Class("clay-chrome-close").Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, Danger),
                    new Setter(Button.ForegroundProperty, TextOnDanger),
                }
            },
            new Style(x => x.OfType<Button>().Class("clay-chrome-close").Class(":pressed"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, DangerHover),
                }
            },

            // --- FIX ContentPresenter: Foreground ---
            new Style(x => x.OfType<Button>().Class("clay-chrome-close")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, TextPrimary) }
            },
            new Style(x => x.OfType<Button>().Class("clay-chrome-close").Class(":pointerover")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, TextOnDanger) }
            },
            new Style(x => x.OfType<Button>().Class("clay-chrome-close").Class(":pressed")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(TextBlock.ForegroundProperty, TextOnDanger) }
            },

            // --- FIX ContentPresenter: Background ---
            new Style(x => x.OfType<Button>().Class("clay-chrome-close")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent) }
            },
            new Style(x => x.OfType<Button>().Class("clay-chrome-close").Class(":pointerover")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, Danger) }
            },
            new Style(x => x.OfType<Button>().Class("clay-chrome-close").Class(":pressed")
                .Template().OfType<ContentPresenter>())
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, DangerHover) }
            },
        };

        /// <summary>
        /// Estilos para el TabStrip
        /// </summary>
        public static Style[] TabStripStates() => new[]
        {
            new Style(x => x.OfType<TabStripItem>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
                    new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0, 0, 0, 3)),
                    new Setter(TemplatedControl.BorderBrushProperty, Brushes.Transparent),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(6, 2)),
                    new Setter(TemplatedControl.ForegroundProperty, TextMuted),
                    new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(10, 10, 0, 0)),
                    new Setter(Button.FontFamilyProperty, FontBody),
                    new Setter(Button.FontSizeProperty, 13.0),
                    new Setter(Control.TransitionsProperty, new Transitions
                    {
                        new BrushTransition { Property = TemplatedControl.BackgroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                        new BrushTransition { Property = TemplatedControl.BorderBrushProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                        new BrushTransition { Property = TemplatedControl.ForegroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                    }),
                }
            },
            new Style(x => x.OfType<TabStripItem>().Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(TemplatedControl.ForegroundProperty, TextSecondary),
                    new Setter(TemplatedControl.BackgroundProperty, SurfaceHover),
                }
            },
            new Style(x => x.OfType<TabStripItem>().Class(":selected"))
            {
                Setters =
                {
                    new Setter(TemplatedControl.BackgroundProperty, Surface),
                    new Setter(TemplatedControl.BorderBrushProperty, Accent),
                    new Setter(TemplatedControl.ForegroundProperty, TextPrimary),
                }
            },
        };

        /// <summary>
        /// Estilos para el popup de autocompletado (AvaloniaEdit.CodeCompletion).
        ///
        /// IMPORTANTE: CompletionWindow es una Window propia — no vive dentro
        /// del árbol visual de SketchEditorView — así que Avalonia no la
        /// alcanza si estos estilos se agregan solo a los Styles locales del
        /// UserControl (como se hace con ButtonEffectStyles/TabStripStates).
        /// Tienen que agregarse a Application.Styles (en App.cs) para que
        /// lleguen a cualquier Window nueva, incluida esta.
        ///
        /// Los selectores para CompletionListBox / sus ListBoxItem apuntan a
        /// tipos públicos de AvaloniaEdit; el popup de descripción (el que
        /// muestra "objeto (Keyword)" al fondo) no se toca acá porque no hay
        /// forma de confirmar su nombre de control exacto sin el código fuente
        /// de AvaloniaEdit a mano — si querés que combine también, conviene
        /// revisarlo con el DevTools de Avalonia (F12 con la app corriendo)
        /// para ver el árbol visual real y ajustar el selector.
        /// </summary>
        public static Style[] CompletionWindowStyles() => new[]
        {
            new Style(x => x.OfType<CompletionWindow>())
            {
                Setters =
                {
                    new Setter(Window.BackgroundProperty, SurfaceRaised),
                }
            },
            new Style(x => x.OfType<CompletionListBox>())
            {
                Setters =
                {
                    // El fondo real y opaco tiene que ir acá, no solo en la
                    // Window: CompletionWindow se renderiza con transparencia
                    // de verdad a nivel de píxel (para permitir sombra/bordes
                    // redondeados), así que Window.Background de arriba no
                    // garantiza nada visible — sin esto, el popup queda
                    // literalmente transparente sobre el código de atrás.
                    new Setter(TemplatedControl.BackgroundProperty, SurfaceRaised),
                    new Setter(TemplatedControl.BorderBrushProperty, new SolidColorBrush(Avalonia.Media.Color.Parse("#E8E2DA"))),
                    new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
                    new Setter(TemplatedControl.CornerRadiusProperty, RadiusChrome),
                    new Setter(TemplatedControl.FontFamilyProperty, FontMono),
                    new Setter(TemplatedControl.FontSizeProperty, 13.0),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(4)),
                }
            },
            new Style(x => x.OfType<CompletionListBox>().Descendant().OfType<ListBoxItem>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(10, 5)),
                    new Setter(TemplatedControl.ForegroundProperty, TextPrimary),
                    new Setter(TemplatedControl.CornerRadiusProperty, RadiusChrome),
                    new Setter(TemplatedControl.MarginProperty, new Thickness(2, 1)),
                }
            },
            new Style(x => x.OfType<CompletionListBox>().Descendant().OfType<ListBoxItem>().Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(TemplatedControl.BackgroundProperty, SurfaceHover),
                }
            },
            new Style(x => x.OfType<CompletionListBox>().Descendant().OfType<ListBoxItem>().Class(":selected"))
            {
                Setters =
                {
                    new Setter(TemplatedControl.BackgroundProperty, Accent),
                    new Setter(TemplatedControl.ForegroundProperty, OnAccent),
                }
            },
        };

        /// <summary>Todos los estilos combinados.</summary>
        public static Style[] AllStyles() => ButtonEffectStyles().Concat(TabStripStates()).Concat(CompletionWindowStyles()).ToArray();
    }
}