using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Support;
using DanaProcessing.Ide.Theme;
using System.Diagnostics;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// "Acerca de DanaProcessing": version, license, a link to the repo and
    /// the donation button.
    /// Plain native window chrome, same as ConfirmDialog -- it's a small
    /// info box, not a workspace.
    /// </summary>
    public class AboutWindow : Window
    {
        public AboutWindow()
        {
            Title = Loc.Tr("Acerca de DanaProcessing", "About DanaProcessing");
            Width = 440;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new Ellipse
                    {
                        Width = 14,
                        Height = 14,
                        Fill = ClayTheme.Accent,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    new TextBlock
                    {
                        Text = "DanaProcessing",
                        Foreground = ClayTheme.TextPrimary,
                        FontFamily = ClayTheme.FontDisplay,
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 20,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                }
            };

            TextBlock Line(string text, IBrush brush, double size = 12.5, double top = 0) => new()
            {
                Text = text,
                Foreground = brush,
                FontFamily = ClayTheme.FontBody,
                FontSize = size,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, top, 0, 0),
            };

            var version = Line($"{Loc.Tr("Versión", "Version")} {AppVersion.Current}", ClayTheme.TextMuted, 12, 6);
            ToolTip.SetTip(version, AppVersion.Full);

            var tagline = Line(
                Loc.Tr("Creative coding en C#, como Processing y p5.js, con una IDE hecha a medida.",
                       "Creative coding in C#, the way Processing and p5.js do it, with an IDE built just for it."),
                ClayTheme.TextSecondary, 13, 14);

            var support = Line(
                Loc.Tr("DanaProcessing es gratis y de código abierto. Si te sirve y querés que siga creciendo, podés apoyarlo con una donación por PayPal: cada aporte se traduce en más tiempo para nuevas funciones, ejemplos y correcciones.",
                       "DanaProcessing is free and open source. If it's useful to you and you'd like it to keep growing, you can support it with a donation via PayPal: every contribution turns into more time for new features, examples and fixes."),
                ClayTheme.TextSecondary, 12.5, 12);

            var license = Line(Loc.Tr("Licencia MIT · © 2026 Dana Processing", "MIT License · © 2026 Dana Processing"), ClayTheme.TextMuted, 11.5, 14);

            var donateButton = new Button
            {
                Content = Loc.Tr("♥  Donar por PayPal", "♥  Donate via PayPal"),
                Classes = { "clay-run" },
                Padding = new Thickness(16, 8),
            };
            donateButton.Click += (_, _) => SupportLinks.Open(SupportLinks.DonateUrl);

            var repoButton = new Button
            {
                Content = Loc.Tr("Ver en GitHub", "View on GitHub"),
                Classes = { "clay-secondary" },
                Padding = new Thickness(14, 8),
            };
            repoButton.Click += (_, _) => OpenRepo();

            var closeButton = new Button
            {
                Content = Loc.Tr("Cerrar", "Close"),
                Classes = { "clay-secondary" },
                Padding = new Thickness(14, 8),
            };
            closeButton.Click += (_, _) => Close();

            var buttons = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
                Margin = new Thickness(0, 22, 0, 0),
            };
            Grid.SetColumn(donateButton, 0);
            Grid.SetColumn(repoButton, 2);
            Grid.SetColumn(closeButton, 3);
            buttons.Children.Add(donateButton);
            buttons.Children.Add(repoButton);
            buttons.Children.Add(closeButton);

            Content = new StackPanel
            {
                Margin = new Thickness(26, 24, 26, 20),
                Children = { header, version, tagline, support, license, buttons },
            };
        }

        private static void OpenRepo()
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/BuddhaCodes/DanaProcessing") { UseShellExecute = true });
            }
            catch
            {
                // No default browser: nothing useful to do, never crash over a link.
            }
        }
    }
}
