using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide.Export
{
    /// <summary>File > Export for print: what to make (big PNG, SVG, plotter SVG), how big, and which frame.</summary>
    public sealed class PrintExportWindow : Window
    {
        public enum Format { Png, Svg, PlotterSvg }

        public sealed record Options(Format Format, float Scale, int Frames);

        private static readonly float[] Scales = { 2, 4, 8, 16 };

        private readonly ComboBox _format;
        private readonly ComboBox _scale;
        private readonly ComboBox _frame;
        private readonly TextBlock _size;
        private readonly int _width, _height, _currentFrame;

        public PrintExportWindow(int sketchWidth, int sketchHeight, int currentFrame)
        {
            _width = Math.Max(1, sketchWidth);
            _height = Math.Max(1, sketchHeight);
            _currentFrame = Math.Max(1, currentFrame);

            Title = Loc.Tr("Exportar para imprimir", "Export for print");
            Width = 480;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            _format = Combo(new[]
            {
                Loc.Tr("PNG grande (póster, impresión)", "Big PNG (poster, print)"),
                Loc.Tr("SVG (vectores, para editar)", "SVG (vectors, to edit)"),
                Loc.Tr("SVG para plotter (trazos ordenados, una pluma por color)", "Plotter SVG (ordered strokes, one pen per color)"),
            });
            _scale = Combo(Scales.Select(s => $"{s}x").ToArray(), 1);
            _frame = Combo(new[]
            {
                Loc.Tr("Primer frame", "First frame"),
                Loc.Tr($"Frame actual ({_currentFrame})", $"Current frame ({_currentFrame})"),
            }, currentFrame > 1 ? 1 : 0);
            _size = new TextBlock
            {
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0),
            };
            _format.SelectionChanged += (_, _) => UpdateHint();
            _scale.SelectionChanged += (_, _) => UpdateHint();
            UpdateHint();

            var note = new TextBlock
            {
                Text = Loc.Tr(
                    "Se vuelve a dibujar desde cero con la misma semilla y los mismos valores de [Tweak], como vectores, así que formas y texto salen nítidos a cualquier tamaño. El panel de [Tweak] no sale en la imagen.",
                    "It's redrawn from scratch with the same seed and [Tweak] values, as vectors, so shapes and text come out sharp at any size. The [Tweak] panel isn't part of the image."),
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 0),
            };

            var cancel = new Button { Content = Loc.Tr("Cancelar", "Cancel"), Classes = { "clay-secondary" }, Padding = new Thickness(14, 8), Margin = new Thickness(0, 0, 8, 0) };
            cancel.Click += (_, _) => Close(null);
            var export = new Button { Content = Loc.Tr("Exportar...", "Export..."), Classes = { "clay-run" }, Padding = new Thickness(16, 8) };
            export.Click += (_, _) => Close(new Options(
                (Format)Math.Max(0, _format.SelectedIndex),
                Scales[Math.Max(0, _scale.SelectedIndex)],
                _frame.SelectedIndex == 1 ? _currentFrame : 1));

            var body = new StackPanel
            {
                Margin = new Thickness(24, 20, 24, 20),
                Spacing = 6,
                Children =
                {
                    Label(Loc.Tr("Formato", "Format")), _format,
                    Label(Loc.Tr("Tamaño", "Size")), _scale, _size,
                    Label(Loc.Tr("Qué frame", "Which frame")), _frame,
                    note,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 16, 0, 0),
                        Children = { cancel, export },
                    },
                },
            };
            ClayChrome.Apply(this, body, canMinimize: false, canMaximize: false);
        }

        private void UpdateHint()
        {
            bool png = _format.SelectedIndex == 0;
            _scale.IsEnabled = png;
            float s = Scales[Math.Max(0, _scale.SelectedIndex)];
            int w = (int)Math.Round(_width * s), h = (int)Math.Round(_height * s);
            // A print at 300 dpi: pixels / 300 inches * 2.54 cm.
            _size.Text = png
                ? Loc.Tr($"{w} × {h} px  ·  a 300 dpi: {w / 300.0 * 2.54:0.#} × {h / 300.0 * 2.54:0.#} cm",
                         $"{w} × {h} px  ·  at 300 dpi: {w / 300.0:0.#} × {h / 300.0:0.#} in")
                : Loc.Tr($"{_width} × {_height} (vectores: se escala sin perder calidad)", $"{_width} × {_height} (vectors: scales without losing quality)");
        }

        private static TextBlock Label(string text) => new()
        {
            Text = text,
            Foreground = ClayTheme.TextSecondary,
            FontFamily = ClayTheme.FontBody,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0),
        };

        private static ComboBox Combo(string[] items, int selected = 0) => new()
        {
            ItemsSource = items,
            SelectedIndex = selected,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontFamily = ClayTheme.FontBody,
            FontSize = 12.5,
        };

        public static Task<Options?> ShowAsync(Window owner, int width, int height, int currentFrame) =>
            new PrintExportWindow(width, height, currentFrame).ShowDialog<Options?>(owner);
    }
}
