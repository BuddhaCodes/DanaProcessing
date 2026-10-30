using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;
using System.Threading.Tasks;

namespace DanaProcessing.Ide.Export
{
    /// <summary>
    /// A small modal shown right before the destination-folder picker in
    /// MainWindow.ExportActiveSketchAsync, letting the user pick export-time
    /// options -- currently just "windowless"/gadget mode (see
    /// SketchExporter/AvaloniaSketchWindow's own remarks) -- without needing a
    /// whole Settings-style window for one checkbox. Mirrors ConfirmDialog's
    /// own plain-native-chrome, ShowDialog&lt;T&gt; shape.
    /// </summary>
    /// <summary>What the export dialog chose. Attached is set only for an AttachedSketch.</summary>
    public sealed record ExportChoice(bool Windowless, AttachedExportOptions? Attached);

    public class ExportOptionsWindow : Window
    {
        private readonly CheckBox _windowlessCheckBox;
        private readonly CheckBox _eachWindowCheckBox;
        private readonly CheckBox _installerCheckBox;
        private readonly CheckBox _profileCheckBox;
        private readonly CheckBox _gpuCheckBox;

        /// <param name="attachedSketch">The sketch is an AttachedSketch: it exports as a tray app, so there's no window to choose a title bar for.</param>
        /// <param name="eachWindowByDefault">The sketch already asks for AttachMode.EachWindow (the box starts checked either way: one sketch per terminal is the usual wish).</param>
        public ExportOptionsWindow(bool attachedSketch = false, bool eachWindowByDefault = true)
        {
            Title = Loc.Tr("Opciones de exportación", "Export options");
            Width = 440;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            // Custom chrome, matching MainWindow/AgentWindow's own look
            // instead of the native OS title bar -- no resize-grip overlay
            // needed here (unlike those two): CanResize is false and
            // SizeToContent already fixes this dialog's size, so there's
            // nothing for a resize handle to do.
            WindowDecorations = WindowDecorations.None;
            var titleBar = BuildTitleBar();

            var heading = new TextBlock
            {
                Text = Loc.Tr("¿Cómo querés exportar este sketch?", "How do you want to export this sketch?"),
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontDisplay,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
                Margin = new Thickness(24, 24, 24, 0),
            };

            _windowlessCheckBox = new CheckBox
            {
                Content = Loc.Tr("Sin barra de título (modo gadget)", "No title bar (gadget mode)"),
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                Foreground = ClayTheme.TextPrimary,
                Margin = new Thickness(24, 16, 24, 0),
            };

            var hint = new TextBlock
            {
                Text = Loc.Tr(
                    "La ventana exportada no va a tener la barra de Windows (minimizar/maximizar/cerrar) -- se arrastra con Alt + click sostenido sobre el dibujo, y se cierra con Alt+F4. Útil para que el sketch se vea como un gadget de escritorio.",
                    "The exported window won't have the Windows title bar (minimize/maximize/close) -- drag it with Alt + click-and-hold anywhere on the drawing, close it with Alt+F4. Useful for making the sketch look like a desktop gadget."),
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(24, 6, 24, 0),
            };

            var cancelButton = new Button
            {
                Content = Loc.Tr("Cancelar", "Cancel"),
                Classes = { "clay-secondary" },
                Padding = new Thickness(14, 8),
                FontSize = 12.5,
                Margin = new Thickness(0, 0, 8, 0),
            };
            cancelButton.Click += (_, _) => Close(null);

            var exportButton = new Button
            {
                Content = Loc.Tr("Exportar", "Export"),
                Classes = { "clay-run" },
                Padding = new Thickness(16, 8),
                FontSize = 12.5,
            };
            exportButton.Click += (_, _) => Close(new ExportChoice(
                _windowlessCheckBox.IsChecked == true,
                attachedSketch ? new AttachedExportOptions(_eachWindowCheckBox.IsChecked == true, _installerCheckBox.IsChecked == true,
                                                           _installerCheckBox.IsChecked == true && _profileCheckBox.IsChecked == true,
                                                           _gpuCheckBox.IsChecked == true) : null));

            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(24, 22, 24, 20),
                Children = { cancelButton, exportButton },
            };

            _eachWindowCheckBox = new CheckBox
            {
                Content = Loc.Tr("Un sketch por cada terminal (también cada panel dividido)", "One sketch per terminal (each split pane too)"),
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                Foreground = ClayTheme.TextPrimary,
                Margin = new Thickness(24, 14, 24, 0),
                IsChecked = eachWindowByDefault,
                IsVisible = attachedSketch,
            };
            _installerCheckBox = new CheckBox
            {
                Content = Loc.Tr("Instalador de PowerShell (como Abyss Tide)", "PowerShell installer (like Abyss Tide)"),
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                Foreground = ClayTheme.TextPrimary,
                Margin = new Thickness(24, 8, 24, 0),
                IsChecked = true,
                IsVisible = attachedSketch,
            };
            _profileCheckBox = new CheckBox
            {
                Content = Loc.Tr("En su propio perfil de Windows Terminal", "In its own Windows Terminal profile"),
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                Foreground = ClayTheme.TextPrimary,
                Margin = new Thickness(44, 4, 24, 0),
                IsChecked = true,
                IsVisible = attachedSketch,
            };
            _gpuCheckBox = new CheckBox
            {
                Content = Loc.Tr("Usar la GPU (DirectComposition)", "Use the GPU (DirectComposition)"),
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                Foreground = ClayTheme.TextPrimary,
                Margin = new Thickness(24, 8, 24, 0),
                IsChecked = true,
                IsVisible = attachedSketch,
            };

            // The profile is installed by install.ps1: without the installer there's nothing to put it in.
            _installerCheckBox.IsCheckedChanged += (_, _) => _profileCheckBox.IsEnabled = _installerCheckBox.IsChecked == true;

            var attachedHint = new TextBlock
            {
                Text = Loc.Tr(
                    "Un sketch por terminal: cada ventana de Windows Terminal, y cada panel si la divides, tiene su propia copia, aunque el Attach() del sketch diga otra cosa. Instalador: junto al .exe va install.ps1; al ejecutarlo se copia a %LOCALAPPDATA%\\DanaProcessing\\Sketches, arranca con Windows, añade el hook del prompt (para saber si un comando falló) y arranca ya. install.ps1 -Uninstall lo quita. Perfil propio: como Abyss Tide, añade a Windows Terminal un perfil translúcido para que el sketch se vea a través de él; sin esta opción el sketch sigue detrás de todas las terminales (nunca encima del texto) y solo se ve en las que ya tengan un perfil translúcido. GPU: los frames llegan a la pantalla como los de Windows Terminal (DirectComposition, al ritmo del refresco) en vez de copiarse enteros en cada frame; si la GPU no arranca, vuelve sola al modo CPU (la tarjeta lo dice con la insignia GPU).",
                    "One sketch per terminal: every Windows Terminal window, and every pane when you split it, gets its own copy, whatever the sketch's Attach() says. Installer: install.ps1 goes next to the .exe; running it copies the app to %LOCALAPPDATA%\\DanaProcessing\\Sketches, starts it with Windows, adds the prompt hook (so it knows when a command fails) and starts it. install.ps1 -Uninstall removes it. Own profile: like Abyss Tide, it adds a see-through profile to Windows Terminal so the sketch shows through it; without it the sketch is still behind every terminal (never over the text) and only shows in the ones whose profile is already see-through. GPU: frames reach the screen the way Windows Terminal's do (DirectComposition, in step with the refresh) instead of being copied whole every frame; if the GPU can't start, it goes back to CPU by itself (the GPU badge tells)."),
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(24, 6, 24, 0),
                IsVisible = attachedSketch,
            };

            if (attachedSketch)
            {
                _windowlessCheckBox.IsVisible = false;
                _windowlessCheckBox.IsChecked = false;
                hint.Text = Loc.Tr(
                    "Este es un sketch adjunto (AttachedSketch): la app exportada no tiene ventana propia. Se dibuja sobre la ventana que elige en Attach() y vive en la bandeja del sistema, junto al reloj: su menú dice qué está haciendo y permite pausarla, instalar las señales de PowerShell y salir.",
                    "This is an attached sketch (AttachedSketch): the exported app has no window of its own. It draws on the window its Attach() picks and lives in the system tray, next to the clock: its menu says what it's doing and can pause it, install the PowerShell signals and quit.");
                hint.Margin = new Thickness(24, 16, 24, 0);
            }

            var body = new StackPanel { Children = { heading, _windowlessCheckBox, hint, _eachWindowCheckBox, _installerCheckBox, _profileCheckBox, _gpuCheckBox, attachedHint, buttonRow } };

            Content = new DockPanel
            {
                LastChildFill = true,
                Children = { titleBar, body },
            };
            DockPanel.SetDock(titleBar, Dock.Top);
        }

        /// <summary>Slim title bar matching MainWindow/AgentWindow's own
        /// custom chrome -- just a wordmark on the left and a close button
        /// on the right (no minimize/maximize: this is a small, fixed-size
        /// modal, not a real window someone would want to minimize).</summary>
        private Border BuildTitleBar()
        {
            var logoDot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = ClayTheme.Accent,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var wordmark = new TextBlock
            {
                Text = Loc.Tr("Exportar", "Export"),
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontDisplay,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var brand = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Children = { logoDot, wordmark },
            };

            var closeButton = new Button { Content = "✕", Classes = { "clay-chrome-close" } };
            closeButton.Click += (_, _) => Close(null);

            var controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Children = { closeButton },
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Height = 40 };
            Grid.SetColumn(brand, 0);
            Grid.SetColumn(controls, 1);
            grid.Children.Add(brand);
            grid.Children.Add(controls);

            var root = new Border { Background = ClayTheme.TitleBarBackground, Child = grid };
            root.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(root).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(e);
            };

            return root;
        }

        /// <summary>Null if cancelled (Cancelar, the ✕ button, Alt+F4, etc.); otherwise what was chosen.</summary>
        public static Task<ExportChoice?> ShowAsync(Window owner, bool attachedSketch = false, bool eachWindowByDefault = true) =>
            new ExportOptionsWindow(attachedSketch, eachWindowByDefault).ShowDialog<ExportChoice?>(owner);

        /// <summary>Whether <paramref name="source"/> declares an AttachedSketch (exports as a tray app). A text check, like the IDE's other pre-compile hints.</summary>
        public static bool IsAttachedSketchSource(string source) =>
            System.Text.RegularExpressions.Regex.IsMatch(source, @":\s*AttachedSketch\b");
    }
}
