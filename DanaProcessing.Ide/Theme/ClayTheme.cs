using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.CodeCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using TextMateSharp.Themes;

namespace DanaProcessing.Ide.Theme
{
    public static class ClayTheme
    {
        // --- Superficies ---
        // NOTE: these used to be `readonly IBrush`. They are now `readonly
        // SolidColorBrush` — the field reference itself still never changes
        // (so every Style Setter / direct property assignment above keeps
        // pointing at the exact same object forever), but SolidColorBrush.Color
        // is a settable Avalonia property, so ApplyPalette() below can mutate
        // .Color on these shared instances and have it repaint instantly
        // everywhere they're used — no rebuild, no restart. See ApplyPalette.
        public static readonly SolidColorBrush Base = new(Avalonia.Media.Color.Parse("#F8F6F2"));
        public static readonly SolidColorBrush Surface = new(Avalonia.Media.Color.Parse("#FFFFFF"));
        public static readonly SolidColorBrush SurfaceRaised = new(Avalonia.Media.Color.Parse("#FDFBF8"));
        public static readonly SolidColorBrush SurfaceHigher = new(Avalonia.Media.Color.Parse("#FAF5EE"));

        // --- COLORES DE HOVER MUCHO MÁS VISIBLES ---
        public static readonly SolidColorBrush SurfaceHover = new(Avalonia.Media.Color.Parse("#E8DDD0"));  // Beige más oscuro
        public static readonly SolidColorBrush SurfacePressed = new(Avalonia.Media.Color.Parse("#D5C8B8")); // Beige aún más oscuro

        // --- Acentos ---
        public static readonly SolidColorBrush Accent = new(Avalonia.Media.Color.Parse("#FFB08C"));
        public static readonly SolidColorBrush AccentDim = new(Avalonia.Media.Color.Parse("#F09B78"));
        public static readonly SolidColorBrush AccentGlow = new(Avalonia.Media.Color.Parse("#FFB08C44"));
        public static readonly SolidColorBrush OnAccent = new(Avalonia.Media.Color.Parse("#4A3728"));

        public static readonly SolidColorBrush Secondary = new(Avalonia.Media.Color.Parse("#A8D0E6"));
        public static readonly SolidColorBrush SecondaryDim = new(Avalonia.Media.Color.Parse("#89B8D0"));

        // --- Estados ---
        public static readonly SolidColorBrush Success = new(Avalonia.Media.Color.Parse("#B5E6C3"));
        public static readonly SolidColorBrush SuccessSurface = new(Avalonia.Media.Color.Parse("#E8F5ED"));
        public static readonly SolidColorBrush Danger = new(Avalonia.Media.Color.Parse("#F5A0A0"));
        public static readonly SolidColorBrush DangerSurface = new(Avalonia.Media.Color.Parse("#FCE8E8"));
        public static readonly SolidColorBrush DangerHover = new(Avalonia.Media.Color.Parse("#E86A6A"));

        // --- Texto ---
        public static readonly SolidColorBrush TextPrimary = new(Avalonia.Media.Color.Parse("#2D2A24"));
        public static readonly SolidColorBrush TextSecondary = new(Avalonia.Media.Color.Parse("#5A554C"));
        public static readonly SolidColorBrush TextMuted = new(Avalonia.Media.Color.Parse("#9A958A"));
        public static readonly SolidColorBrush TextOnDanger = new(Avalonia.Media.Color.Parse("#FFFFFF"));

        // --- Radios ---
        // No longer `readonly`: ThemeSettingsStore.Initialize()/ApplyPalette()
        // can overwrite these with the user's saved "detalles" (roundness).
        // Unlike colors above, a CornerRadius is a struct copied by value into
        // every Setter/property at the moment it's assigned, so changing these
        // fields only takes effect for windows built *after* the change — see
        // the restart note on ClayTheme.ApplyPalette.
        public static CornerRadius RadiusSmall = new CornerRadius(10);
        public static CornerRadius RadiusMedium = new CornerRadius(14);
        public static CornerRadius RadiusLarge = new CornerRadius(20);
        public static CornerRadius RadiusCard = new CornerRadius(24);
        public static CornerRadius RadiusButton = new CornerRadius(16);
        public static CornerRadius RadiusChrome = new CornerRadius(8);
        public static CornerRadius RadiusPanelTop = new CornerRadius(20, 20, 0, 0);
        public static CornerRadius RadiusPill = new CornerRadius(999);

        // --- Sombras ---
        public static readonly BoxShadows ShadowRaised = BoxShadows.Parse("0 8 32 0 #1A000000, 0 2 0 0 #FFFFFF, 0 6 20 0 #0D000000");
        public static readonly BoxShadows ShadowSubtle = BoxShadows.Parse("0 4 16 0 #1A000000");
        public static readonly BoxShadows ShadowDeep = BoxShadows.Parse("0 16 48 0 #2A000000, 0 4 24 0 #1A000000");
        public static readonly BoxShadows ShadowGlow = BoxShadows.Parse("0 0 40 0 #FFB08C44, 0 4 16 0 #1A000000");
        public static readonly BoxShadows ShadowFocusRing = BoxShadows.Parse("0 0 0 2 #FFB08C88, 0 0 40 8 #FFB08C33, 0 16 48 0 #2A000000");

        // --- Fuentes ---
        // Also non-readonly for the same reason as the radii above: FontFamily
        // has no settable properties of its own, so changing which font a
        // control uses means swapping in a whole new FontFamily instance —
        // which, like CornerRadius, only reaches windows built after the change.
        public static FontFamily FontDisplay = new FontFamily("Sora,Segoe UI Semibold,SF Pro Display,Inter,Arial");
        public static FontFamily FontBody = new FontFamily("Inter,Segoe UI,SF Pro Text,Arial");
        public static FontFamily FontMono = new FontFamily("Cascadia Code,JetBrains Mono,Consolas,Menlo,monospace");

        // --- Gradientes ---
        // Typed as LinearGradientBrush (not IBrush) so ApplyPalette can rewrite
        // each GradientStop's Color in place — same live-update trick as the
        // solid brushes above: same object, new colors, instant repaint.
        public static readonly LinearGradientBrush AccentGradient = new()
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

        public static readonly LinearGradientBrush AccentGradientHover = new()
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

        public static readonly LinearGradientBrush WindowBackground = new()
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

        public static readonly LinearGradientBrush TitleBarBackground = new()
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
        // SETTINGS: aplicar una paleta/detalles guardados por el usuario
        // ================================================================

        /// <summary>Raised after ApplyPalette finishes mutating the shared color brushes in place.
        /// MainWindow doesn't need to subscribe to repaint colors (that happens for free — see
        /// the SolidColorBrush/LinearGradientBrush remarks above) — this is only useful for UI
        /// (like the settings window itself) that wants to know a palette was just applied.</summary>
        public static event Action? PaletteChanged;

        /// <summary>The settings this theme was last initialized/applied with.</summary>
        public static ThemeSettings CurrentSettings { get; private set; } = ThemeSettings.Default();

        /// <summary>
        /// Call once at startup (before any Window/Style is built) to seed
        /// every color, radius, and font from a saved <see cref="ThemeSettings"/>.
        /// </summary>
        public static void Initialize(ThemeSettings settings)
        {
            ApplyColors(settings);
            ApplyDetails(settings);
            CurrentSettings = settings;
        }

        /// <summary>
        /// Applies a full settings object: colors update instantly everywhere
        /// (no restart needed — see the brush remarks above), but the "detalles"
        /// (corner radius roundness, fonts) only take effect for windows built
        /// after this call, since they're structs/immutable objects copied by
        /// value into existing controls. The caller (SettingsWindow) is
        /// responsible for asking the app to rebuild/relaunch when those
        /// changed, so every control ends up consistent.
        /// </summary>
        public static void ApplyPalette(ThemeSettings settings)
        {
            ApplyColors(settings);
            ApplyDetails(settings);
            CurrentSettings = settings;
            PaletteChanged?.Invoke();
        }

        private static void ApplyColors(ThemeSettings s)
        {
            Base.Color = Avalonia.Media.Color.Parse(s.BaseColor);
            Surface.Color = Avalonia.Media.Color.Parse(s.SurfaceColor);
            SurfaceRaised.Color = Avalonia.Media.Color.Parse(s.SurfaceRaisedColor);
            SurfaceHigher.Color = Avalonia.Media.Color.Parse(s.SurfaceHigherColor);
            SurfaceHover.Color = Avalonia.Media.Color.Parse(s.SurfaceHoverColor);
            SurfacePressed.Color = Avalonia.Media.Color.Parse(s.SurfacePressedColor);

            Accent.Color = Avalonia.Media.Color.Parse(s.AccentColor);
            AccentDim.Color = Avalonia.Media.Color.Parse(s.AccentDimColor);
            OnAccent.Color = Avalonia.Media.Color.Parse(s.OnAccentColor);
            AccentGlow.Color = WithAlpha(s.AccentColor, 0x44);

            Secondary.Color = Avalonia.Media.Color.Parse(s.SecondaryColor);
            SecondaryDim.Color = Avalonia.Media.Color.Parse(s.SecondaryDimColor);

            Success.Color = Avalonia.Media.Color.Parse(s.SuccessColor);
            SuccessSurface.Color = Avalonia.Media.Color.Parse(s.SuccessSurfaceColor);
            Danger.Color = Avalonia.Media.Color.Parse(s.DangerColor);
            DangerSurface.Color = Avalonia.Media.Color.Parse(s.DangerSurfaceColor);
            DangerHover.Color = Avalonia.Media.Color.Parse(s.DangerHoverColor);

            TextPrimary.Color = Avalonia.Media.Color.Parse(s.TextPrimaryColor);
            TextSecondary.Color = Avalonia.Media.Color.Parse(s.TextSecondaryColor);
            TextMuted.Color = Avalonia.Media.Color.Parse(s.TextMutedColor);

            // Gradients derive their stops from the same accent/base colors so
            // they stay visually consistent with whatever palette was chosen,
            // rather than needing their own set of settings fields.
            SetGradientStops(AccentGradient, Lighten(s.AccentColor, 0x1D), s.AccentColor, s.AccentDimColor);
            SetGradientStops(AccentGradientHover, Lighten(s.AccentColor, 0x38), Lighten(s.AccentColor, 0x14), Lighten(s.AccentDimColor, 0x10));
            SetGradientStops(WindowBackground, s.SurfaceRaisedColor, s.BaseColor, Darken(s.BaseColor, 0x08));
            SetGradientStops(TitleBarBackground, s.SurfaceRaisedColor, Darken(s.SurfaceRaisedColor, 0x08));
        }

        private static void ApplyDetails(ThemeSettings s)
        {
            RadiusSmall = new CornerRadius(s.RadiusSmall);
            RadiusMedium = new CornerRadius(s.RadiusMedium);
            RadiusLarge = new CornerRadius(s.RadiusLarge);
            RadiusCard = new CornerRadius(s.RadiusCard);
            RadiusButton = new CornerRadius(s.RadiusButton);
            RadiusChrome = new CornerRadius(s.RadiusChrome);
            RadiusPanelTop = new CornerRadius(s.RadiusCard, s.RadiusCard, 0, 0);

            FontDisplay = new FontFamily(s.FontDisplay);
            FontBody = new FontFamily(s.FontBody);
            FontMono = new FontFamily(s.FontMono);
        }

        private static void SetGradientStops(LinearGradientBrush brush, params string[] hexColors)
        {
            for (int i = 0; i < brush.GradientStops.Count && i < hexColors.Length; i++)
                brush.GradientStops[i].Color = Avalonia.Media.Color.Parse(hexColors[i]);
        }

        private static Avalonia.Media.Color WithAlpha(string hex, byte alpha)
        {
            var c = Avalonia.Media.Color.Parse(hex);
            return new Avalonia.Media.Color(alpha, c.R, c.G, c.B);
        }

        private static string Lighten(string hex, int amount) => ShiftRgb(hex, amount);
        private static string Darken(string hex, int amount) => ShiftRgb(hex, -amount);

        private static string ShiftRgb(string hex, int amount)
        {
            var c = Avalonia.Media.Color.Parse(hex);
            byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);
            var shifted = new Avalonia.Media.Color(c.A, Clamp(c.R + amount), Clamp(c.G + amount), Clamp(c.B + amount));
            return shifted.ToString();
        }

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
        //
        // El mismo bug aplica a cualquier estado "custom" que se quiera
        // reflejar en un botón (no solo :pointerover/:pressed nativos) —
        // por eso "clay-toggle" más abajo usa una clase CSS-like ("active")
        // en vez de asignar Background/Foreground desde código: así el
        // Setter correcto (el de ".clay-toggle.active") gana por
        // especificidad de selector, en el Button y en su ContentPresenter,
        // sin pelear con nada seteado imperativamente.
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

            // ============================================================
            // === TOGGLE BUTTON (pill "Código / Resultado") ===
            // A diferencia de los demás, este botón tiene un estado extra
            // que no es un pseudo-clase nativa de Avalonia (:pointerover,
            // :pressed) sino un estado de aplicación ("está seleccionado
            // este segmento del toggle o no"). Por eso se modela como una
            // clase propia, "active", que MainWindow.UpdatePaneToggleVisuals()
            // agrega/quita con Classes.Set("active", ...) en vez de asignar
            // Background/Foreground directamente — esto último quedaría
            // pisado por los Setters del ContentPresenter, igual que le
            // pasaría a cualquiera de los estilos de arriba.
            // ============================================================
            new Style(x => x.OfType<Button>().Class("clay-toggle"))
            {
                Setters =
                {
                    new Setter(Button.ForegroundProperty, TextMuted),
                    new Setter(Button.BackgroundProperty, Brushes.Transparent),
                    new Setter(Button.BorderThicknessProperty, new Thickness(0)),
                    new Setter(Button.CornerRadiusProperty, RadiusPill),
                    new Setter(Button.FontFamilyProperty, FontBody),
                    new Setter(Button.FontWeightProperty, FontWeight.SemiBold),
                    new Setter(Button.FontSizeProperty, 13.0),
                    new Setter(Button.PaddingProperty, new Thickness(16, 8)),
                    new Setter(Button.CursorProperty, new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)),
                    new Setter(Control.TransitionsProperty, new Transitions
                    {
                        new BrushTransition { Property = Button.BackgroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                        new BrushTransition { Property = Button.ForegroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                        new DoubleTransition { Property = Visual.OpacityProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                    }),
                }
            },
            new Style(x => x.OfType<Button>().Class("clay-toggle").Class(":pointerover"))
            {
                Setters = { new Setter(Visual.OpacityProperty, 0.85) }
            },
            new Style(x => x.OfType<Button>().Class("clay-toggle").Class(":pressed"))
            {
                Setters = { new Setter(Visual.OpacityProperty, 0.7) }
            },

            // --- Estado activo (segmento seleccionado del toggle) ---
            new Style(x => x.OfType<Button>().Class("clay-toggle").Class("active"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, Accent),
                    new Setter(Button.ForegroundProperty, OnAccent),
                }
            },

            // --- FIX ContentPresenter: inactivo (reposo) ---
            new Style(x => x.OfType<Button>().Class("clay-toggle")
                .Template().OfType<ContentPresenter>())
            {
                Setters =
                {
                    new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent),
                    new Setter(TextBlock.ForegroundProperty, TextMuted),
                }
            },

            // --- FIX ContentPresenter: activo (selector más específico, gana sobre el de arriba) ---
            new Style(x => x.OfType<Button>().Class("clay-toggle").Class("active")
                .Template().OfType<ContentPresenter>())
            {
                Setters =
                {
                    new Setter(ContentPresenter.BackgroundProperty, Accent),
                    new Setter(TextBlock.ForegroundProperty, OnAccent),
                }
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