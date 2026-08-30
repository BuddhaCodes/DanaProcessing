using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using AvaloniaEdit;
using DanaProcessing.AvaloniaHost;

namespace DanaProcessing.Ide
{
    public class App : Application
    {
        public override void Initialize()
        {
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            Styles.Add(new FluentTheme());

            // Cargar el tema de AvaloniaEdit
            try
            {
                var styleInclude = new StyleInclude((Uri?)null)
                {
                    Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml")
                };
                Styles.Add(styleInclude);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading AvaloniaEdit theme: {ex.Message}");
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // --- IDE completo: editor + canvas + Run ---
                desktop.MainWindow = new MainWindow();

                // --- pruebas anteriores, por si necesitas volver a ellas ---
                // desktop.MainWindow = new EditorTestWindow();
                // var sketch = new DemoSketch();
                // desktop.MainWindow = new AvaloniaSketchWindow(sketch, "DanaProcessing IDE (prototipo minimo, host Avalonia)");
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
