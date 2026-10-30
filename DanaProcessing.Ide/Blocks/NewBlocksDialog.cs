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
                FontSize = 22, FontFamily = ClayTheme.FontDisplay, FontWeight = FontWeight.SemiBold, Foreground = ClayTheme.TextPrimary,
            };
            var note = new TextBlock
            {
                Text = Loc.Tr("Es el mismo programa de las dos formas: se puede cambiar después con el botón Fichas / Renglones.",
                              "It's the same program either way: switch later with the Tiles / Lines button."),
                FontSize = 13, FontFamily = ClayTheme.FontBody, Foreground = ClayTheme.TextSecondary, TextWrapping = TextWrapping.Wrap,
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
            var card = new Button
            {
                Padding = new Thickness(18), CornerRadius = new CornerRadius(20), Background = ClayTheme.Surface,
                BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = age, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = ClayTheme.TextMuted, FontFamily = ClayTheme.FontBody },
                        new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeight.Bold, Foreground = ClayTheme.TextPrimary, FontFamily = ClayTheme.FontDisplay },
                        new TextBlock { Text = text, FontSize = 14, Foreground = ClayTheme.TextSecondary, FontFamily = ClayTheme.FontBody, TextWrapping = TextWrapping.Wrap },
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
