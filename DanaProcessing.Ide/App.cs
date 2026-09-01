using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using AvaloniaEdit;
using DanaProcessing.AvaloniaHost;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide
{
    public class App : Application
    {
        public override void Initialize()
        {
            // Has to happen before anything below touches ClayTheme (FluentTheme
            // itself doesn't, but CompletionWindowStyles() a few lines down does) —
            // otherwise the completion popup would build its Styles from the
            // hardcoded defaults instead of whatever the user saved last time.
            ClayTheme.Initialize(ThemeSettingsStore.Load());

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

            // El popup de autocompletado (CompletionWindow) es una Window
            // propia, no un control dentro del árbol de SketchEditorView, así
            // que sus estilos solo la alcanzan si viven en Application.Styles
            // — agregarlos al UserControl del editor no tendría efecto acá.
            Styles.AddRange(ClayTheme.CompletionWindowStyles());
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