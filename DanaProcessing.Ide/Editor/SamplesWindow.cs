using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DanaProcessing.Ide.Editor;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// Lists the sketches in SketchSamples.All so you can drop one into a
    /// fresh tab without retyping it. Modal, like SettingsWindow — picking
    /// a sample (or clicking away) is a one-shot action, not something you
    /// leave open while working.
    /// </summary>
    public class SamplesWindow : Window
    {
        /// <param name="onLoad">Called with the chosen sample's source once the user picks one. The window closes itself right after.</param>
        public SamplesWindow(Action<string> onLoad)
        {
            Title = "Samples — DanaProcessing IDE";
            Width = 640;
            Height = 560;
            MinWidth = 480;
            MinHeight = 360;
            CanResize = true;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            var root = new StackPanel { Spacing = 12, Margin = new Thickness(24, 20, 24, 20) };

            root.Children.Add(new TextBlock
            {
                Text = "Sketches de ejemplo",
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontDisplay,
                FontWeight = FontWeight.SemiBold,
                FontSize = 17,
            });
            root.Children.Add(new TextBlock
            {
                Text = "Abre uno en un tab nuevo del editor.",
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 12.5,
                Margin = new Thickness(0, 0, 0, 4),
            });

            foreach (var sample in SketchSamples.All)
                root.Children.Add(BuildSampleCard(sample, onLoad));

            Content = new ScrollViewer { Content = root };
        }

        private Border BuildSampleCard(SketchSample sample, Action<string> onLoad)
        {
            var nameBlock = new TextBlock
            {
                Text = sample.Name,
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontDisplay,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
            };

            var descBlock = new TextBlock
            {
                Text = sample.Description,
                Foreground = ClayTheme.TextSecondary,
                FontFamily = ClayTheme.FontBody,
                FontSize = 12.5,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0),
            };

            var loadButton = new Button
            {
                Content = "Cargar",
                Classes = { "clay-secondary" },
                Padding = new Thickness(14, 7),
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
            };
            loadButton.Click += (_, _) =>
            {
                onLoad(sample.Source);
                Close();
            };

            var textCol = new StackPanel
            {
                Children = { nameBlock, descBlock },
            };

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Grid.SetColumn(textCol, 0);
            Grid.SetColumn(loadButton, 1);
            row.Children.Add(textCol);
            row.Children.Add(loadButton);

            return new Border
            {
                Background = ClayTheme.Surface,
                CornerRadius = ClayTheme.RadiusButton,
                Padding = new Thickness(16, 14),
                Child = row,
            };
        }
    }
}