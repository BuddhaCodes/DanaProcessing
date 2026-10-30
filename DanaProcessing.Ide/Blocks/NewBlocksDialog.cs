using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// "Nuevo con bloques": who is it for? Picks how the new program is
    /// shown -- it can still be switched any time from the editor's toolbar.
    /// </summary>
    public sealed class NewBlocksDialog : Window
    {
        private BlockView? _choice;

        public NewBlocksDialog()
        {
            Title = Loc.Tr("Nuevo con bloques", "New with blocks");
            Width = 620;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            var heading = new TextBlock
            {
                Text = Loc.Tr("¿Para quién es el programa?", "Who is the program for?"),
                FontSize = 26, FontFamily = BlocksChrome.FontKids, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Ink,
            };
            var note = new TextBlock
            {
                Text = Loc.Tr("Es el mismo programa de las dos formas: se puede cambiar después con el botón Fichas / Renglones.",
                              "It's the same program either way: switch later with the Tiles / Lines button."),
                FontSize = 15, FontFamily = BlocksChrome.FontUi, Foreground = BlocksChrome.InkSoft, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 18),
            };

            var tiles = Card(Loc.Tr("Fichas", "Tiles"), Loc.Tr("6 a 8 años", "Ages 6-8"),
                Loc.Tr("Fichas grandes con dibujos. Casi no hace falta leer.", "Big picture tiles. Hardly any reading."), BlockView.Tiles);
            var lines = Card(Loc.Tr("Renglones", "Lines"), Loc.Tr("9 a 12 años", "Ages 9-12"),
                Loc.Tr("Frases en renglones, cada una al lado de su línea de C#.", "Sentences on lines, each beside its line of C#."), BlockView.Lines);
            var cards = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,*") };
            Grid.SetColumn(lines, 2);
            cards.Children.Add(tiles);
            cards.Children.Add(lines);

            var content = new StackPanel { Margin = new Thickness(28, 24, 28, 28), Children = { heading, note, cards } };
            ClayChrome.Apply(this, content, canMinimize: false, canMaximize: false);
        }

        private Button Card(string title, string age, string text, BlockView view)
        {
            bool kids = view == BlockView.Tiles;
            var (fill, edge) = kids ? ("#FF9F43", "#D97A1E") : ("#4F9BD9", "#2F78B5");
            var badge = new Border
            {
                Width = 52, Height = 52, CornerRadius = new CornerRadius(15), HorizontalAlignment = HorizontalAlignment.Left,
                Background = BlocksChrome.Brush(fill), BoxShadow = BoxShadows.Parse("0 3 0 0 " + edge), Margin = new Thickness(0, 0, 0, 8),
                Child = new StrokeIcon(kids ? BlocksChrome.Icons.Tiles : BlocksChrome.Icons.Lines, 26, thickness: 2.4)
                {
                    Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center,
                },
            };
            var card = new Button
            {
                Padding = new Thickness(20), CornerRadius = new CornerRadius(20), Background = Brushes.White,
                BorderThickness = new Thickness(1.5), BorderBrush = BlocksChrome.Line, HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Content = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        badge,
                        new TextBlock { Text = title, FontSize = 24, LineHeight = 32, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.Ink, FontFamily = kids ? BlocksChrome.FontKids : BlocksChrome.FontUi },
                        new TextBlock { Text = age.ToUpperInvariant(), FontSize = 12, LetterSpacing = 1.2, FontWeight = FontWeight.Bold, Foreground = BlocksChrome.InkMuted, FontFamily = BlocksChrome.FontUi },
                        new TextBlock { Text = text, FontSize = 15, Foreground = BlocksChrome.InkSoft, FontFamily = BlocksChrome.FontUi, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) },
                    },
                },
            };
            card.Click += (_, _) =>
            {
                _choice = view;
                Close();
            };
            return card;
        }

        /// <summary>The view picked, or null if the dialog was closed without picking.</summary>
        public static async Task<BlockView?> ShowAsync(Window owner)
        {
            var dialog = new NewBlocksDialog();
            await dialog.ShowDialog(owner);
            return dialog._choice;
        }
    }
}
