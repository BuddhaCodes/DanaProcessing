using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using DanaProcessing.Ide.Export;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide
{
    public class App : Application
    {
        public override void Initialize()
        {
            // Same reasoning as ClayTheme.Initialize() right below: every
            // Loc.Tr() call site is read once, when a control is constructed —
            // has to be set before MainWindow (or, for an exported standalone
            // sketch, ExportedSketchRunner's error window) builds anything.
            var language = LocaleSettingsStore.Load().Language;
            if (Export.ExportedSketchRunner.SketchFilePath != null)
                language = Loc.ForExport(LocaleSettingsStore.Exists, language, System.Globalization.CultureInfo.CurrentUICulture);
            Loc.Initialize(language);

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

            // Mismo motivo que arriba, pero para dos cosas que aparecen en
            // CUALQUIER ventana (MainWindow, SamplesWindow, SettingsWindow):
            // el Flyout del menú ☰ y cualquier scrollbar. RequestedThemeVariant
            // = Dark de arriba hace que FlyoutPresenter y ScrollBar caigan al
            // chrome oscuro de FluentTheme si nadie los re-temea -- de ahí el
            // panel negro detrás del menú y las scrollbars con fondo negro.
            // Va al final para ganarle en precedencia al StyleInclude de
            // AvaloniaEdit de arriba (también trae su propio ScrollBar).
            Styles.AddRange(ClayTheme.ChromeOverrideStyles());
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (ExportedSketchRunner.SketchFilePath is { } sketchFile)
                {
                    var compiled = ExportedSketchRunner.Compile(sketchFile);
                    if (ExportedSketchRunner.RunsInTray(compiled))
                    {
                        // An exported AttachedSketch: no window at all, it draws
                        // on another app's window and lives in the tray until
                        // its menu's Quit.
                        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                        if (!ExportedSketchRunner.TryClaimSingleInstance())
                        {
                            // Already running (autostart + a double-click, or the
                            // installer starting it again): one copy is enough.
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => desktop.Shutdown());
                            base.OnFrameworkInitializationCompleted();
                            return;
                        }
                        var sketch = (DanaProcessing.AttachedSketch)compiled.Sketch!;
                        var tray = new DanaProcessing.AvaloniaHost.Attach.AttachedSketchTray(
                            sketch, sketch.GetType().Name, () => desktop.Shutdown(), ExportedSketchRunner.Overrides);
                        desktop.Exit += (_, _) => tray.Dispose();
                    }
                    else
                    {
                        // Running as an exported standalone sketch (see
                        // DanaProcessing.Ide.Export) — just the sketch's own window,
                        // no editor/menu/title bar chrome at all.
                        desktop.MainWindow = ExportedSketchRunner.BuildWindow(compiled);
                    }
                }
                else
                {
                    // --- IDE completo: editor + canvas + Run ---
                    var mainWindow = new MainWindow();
                    desktop.MainWindow = mainWindow;

                    // Launched via the "Test in Dana" web button (danaide://run?code=...)
                    // — Program.cs already decoded the payload before Avalonia's
                    // lifetime even started. Opened for review, never auto-run.
                    if (PendingSketch.InitialSource is { } source)
                        mainWindow.OpenSketchFromLink(source);
                }

                // --- pruebas anteriores, por si necesitas volver a ellas ---
                // desktop.MainWindow = new EditorTestWindow();
                // var sketch = new DemoSketch();
                // desktop.MainWindow = new AvaloniaSketchWindow(sketch, "DanaProcessing IDE (prototipo minimo, host Avalonia)");
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}