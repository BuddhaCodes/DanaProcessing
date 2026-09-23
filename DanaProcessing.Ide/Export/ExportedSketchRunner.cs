using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using DanaProcessing.AvaloniaHost;
using DanaProcessing.Ide.Compilation;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide.Export
{
    /// <summary>
    /// Detects whether this exe is running as an exported standalone sketch — a
    /// plain copy of the IDE's own install directory with a `sketch.cs` file
    /// dropped next to the exe (see SketchExporter, which is what produces that
    /// copy) — rather than as the full IDE. Checked once in Program.Main, before
    /// Avalonia's lifetime even starts, same "handoff via a static, since classic
    /// desktop lifetime doesn't forward state to App itself" shape as PendingSketch.
    ///
    /// Deliberately reuses this same exe/codebase instead of a separate "runner"
    /// project: exporting is then just copying files (no `dotnet build`/`publish`
    /// involved, so it works even without the .NET SDK installed — the same
    /// audience the published self-contained IDE itself targets), and there's no
    /// second compile/run code path to drift out of sync with the real one over time.
    /// </summary>
    internal static class ExportedSketchRunner
    {
        public const string MarkerFileName = "sketch.cs";

        public static string? SketchFilePath { get; private set; }

        public static void DetectAndStash()
        {
            var candidate = Path.Combine(ExeDirectory, MarkerFileName);
            if (File.Exists(candidate))
                SketchFilePath = candidate;
        }

        /// <summary>
        /// Where the exe itself actually lives — deliberately NOT
        /// AppContext.BaseDirectory. For a single-file publish built with
        /// IncludeAllContentForSelfExtract=true (see SketchExporter's remark on
        /// why that flag is needed at all), BaseDirectory points at the temp
        /// folder the bundle unpacks itself into at startup, not at the exe's own
        /// folder — sketch.cs, dropped in next to the exe at export time, would
        /// never be found by looking there. Environment.ProcessPath still points
        /// at the real exe regardless of bundling, single-file or not.
        /// </summary>
        private static string ExeDirectory =>
            Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

        /// <summary>Compiles the sketch and builds the standalone window that shows
        /// it — no editor, no title bar chrome, just AvaloniaSketchWindow. If
        /// compilation fails (a corrupted export, or a bundled dependency that
        /// didn't survive being copied), shows a minimal error window instead of
        /// silently exiting, so double-clicking a broken export at least explains why.</summary>
        public static Window BuildWindow(string sketchFilePath)
        {
            var source = File.ReadAllText(sketchFilePath);
            var baseDir = Path.GetDirectoryName(sketchFilePath) ?? ExeDirectory;

            SketchCompiler.RegisterAssemblyDirectory(baseDir);
            var extraReferences = SketchCompiler.CollectExternalReferences(baseDir);
            var result = SketchCompiler.Compile(source, extraReferences);

            if (result.Success)
                return new AvaloniaSketchWindow(result.Sketch!, result.Sketch!.GetType().Name);

            return BuildErrorWindow(result.Errors);
        }

        private static Window BuildErrorWindow(IReadOnlyList<string> errors)
        {
            return new Window
            {
                Title = Loc.Tr("No se pudo iniciar el sketch", "Could not start the sketch"),
                Width = 640,
                Height = 420,
                Background = ClayTheme.Base,
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = Loc.Tr("No se pudo compilar este sketch exportado:\n\n", "Could not compile this exported sketch:\n\n") + string.Join("\n\n", errors),
                        Foreground = ClayTheme.TextPrimary,
                        FontFamily = ClayTheme.FontMono,
                        FontSize = 12.5,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Margin = new Avalonia.Thickness(20),
                    }
                }
            };
        }
    }
}
