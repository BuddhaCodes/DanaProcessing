using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DanaProcessing.Ide.Theme;
using System.Threading.Tasks;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// A small yes/no modal for confirming a destructive action (currently:
    /// discarding unsaved tabs when "Nuevo" or "Ejemplos" is about to replace
    /// the whole editor context). Plain native window chrome — unlike
    /// MainWindow this doesn't need a custom title bar, it's a two-button
    /// prompt shown for a few seconds at most.
    /// </summary>
    public class ConfirmDialog : Window
    {
        public ConfirmDialog(string title, string message, string confirmLabel, string cancelLabel)
        {
            Title = title;
            Width = 420;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            var messageText = new TextBlock
            {
                Text = message,
                Foreground = ClayTheme.TextSecondary,
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(24, 24, 24, 0),
            };

            var cancelButton = new Button
            {
                Content = cancelLabel,
                Classes = { "clay-secondary" },
                Padding = new Thickness(14, 8),
                FontSize = 12.5,
                Margin = new Thickness(0, 0, 8, 0),
            };
            cancelButton.Click += (_, _) => Close(false);

            // Styled as a danger action (not the usual accent "clay-run"
            // look) since confirming here always means discarding work.
            var confirmButton = new Button
            {
                Content = confirmLabel,
                Background = ClayTheme.Danger,
                Foreground = ClayTheme.TextOnDanger,
                CornerRadius = ClayTheme.RadiusButton,
                Padding = new Thickness(16, 8),
                FontSize = 12.5,
            };
            confirmButton.Click += (_, _) => Close(true);

            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(24, 20, 24, 20),
                Children = { cancelButton, confirmButton },
            };

            Content = new StackPanel { Children = { messageText, buttonRow } };
        }

        /// <summary>True if the user picked <paramref name="confirmLabel"/>; false for
        /// <paramref name="cancelLabel"/> or if they closed the window any other way
        /// (native close button, Alt+F4, etc.) — closing without an explicit choice
        /// defaults to NOT performing the destructive action.</summary>
        public static Task<bool> ShowAsync(
            Window owner, string title, string message,
            string confirmLabel = "Continuar", string cancelLabel = "Cancelar")
            => new ConfirmDialog(title, message, confirmLabel, cancelLabel).ShowDialog<bool>(owner);
    }
}
