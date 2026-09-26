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
    public class ExportOptionsWindow : Window
    {
        private readonly CheckBox _windowlessCheckBox;

        public ExportOptionsWindow()
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
            exportButton.Click += (_, _) => Close(_windowlessCheckBox.IsChecked == true);

            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(24, 22, 24, 20),
                Children = { cancelButton, exportButton },
            };

            var body = new StackPanel { Children = { heading, _windowlessCheckBox, hint, buttonRow } };

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

        /// <summary>Null if cancelled (Cancelar, the ✕ button, Alt+F4, etc.);
        /// otherwise true/false for whether "windowless" was checked.</summary>
        public static Task<bool?> ShowAsync(Window owner) => new ExportOptionsWindow().ShowDialog<bool?>(owner);
    }
}
