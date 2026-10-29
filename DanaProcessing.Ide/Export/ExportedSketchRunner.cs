using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DanaProcessing;
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

        /// <summary>Presence alone means "gadget mode" (see SketchExporter's
        /// own windowless option) -- an empty file, same "existence is the
        /// whole signal" shape as MarkerFileName itself, just for one extra
        /// boolean instead of inventing a config file format for it.</summary>
        public const string WindowlessMarkerFileName = "sketch.windowless";

        /// <summary>Presence alone means "one instance on every matching window"
        /// for an exported AttachedSketch (AttachMode.EachWindow), whatever its
        /// own Attach() asked for -- see AttachedExportOptions.</summary>
        public const string EachWindowMarkerFileName = "sketch.eachwindow";

        /// <summary>An export that lives in its own Windows Terminal profile
        /// (see TerminalProfile): the sketch draws behind the terminal, over
        /// the backdrop color this file holds ("#RRGGBBAA").</summary>
        public const string TerminalProfileMarkerFileName = "sketch.profile";

        /// <summary>Non-null when the export lives in its own terminal profile: the color under the sketch.</summary>
        public static SkiaSharp.SKColor? ProfileBackdrop { get; private set; }

        /// <summary>With its own terminal profile: the window title while that profile's tab is in front.</summary>
        public static string? ProfileTitle { get; private set; }

        /// <summary>Presence alone: present the frames through the GPU (see AttachedExportOptions.UseGpu).</summary>
        public const string GpuMarkerFileName = "sketch.gpu";

        public static bool UseGpu { get; private set; }

        public static string? SketchFilePath { get; private set; }
        public static bool IsWindowless { get; private set; }
        public static bool ForceEachWindow { get; private set; }

        public static void DetectAndStash()
        {
            var candidate = Path.Combine(ExeDirectory, MarkerFileName);
            if (File.Exists(candidate))
                SketchFilePath = candidate;

            IsWindowless = File.Exists(Path.Combine(ExeDirectory, WindowlessMarkerFileName));
            ForceEachWindow = File.Exists(Path.Combine(ExeDirectory, EachWindowMarkerFileName));
            UseGpu = File.Exists(Path.Combine(ExeDirectory, GpuMarkerFileName));
            var profileMarker = Path.Combine(ExeDirectory, TerminalProfileMarkerFileName);
            if (File.Exists(profileMarker))
                (ProfileBackdrop, ProfileTitle) = ParseProfileMarker(File.ReadAllText(profileMarker));
        }

        /// <summary>What the tray app forces over the sketch's own Attach(): see AttachedRunOverrides.</summary>
        public static DanaProcessing.AvaloniaHost.Attach.AttachedRunOverrides Overrides => BuildOverrides(ForceEachWindow, ProfileBackdrop, ProfileTitle, UseGpu);

        internal static DanaProcessing.AvaloniaHost.Attach.AttachedRunOverrides BuildOverrides(bool eachWindow, SkiaSharp.SKColor? profileBackdrop, string? profileTitle = null, bool useGpu = false) =>
            new(eachWindow ? AttachMode.EachWindow : null,
                profileBackdrop is null ? null : AttachLayer.Behind,
                profileBackdrop,
                profileBackdrop is null ? null : profileTitle,
                useGpu,
                EachPane: eachWindow);   // "one sketch per terminal": every pane of a split is a terminal

        /// <summary>sketch.profile: line 1 the backdrop, line 2 (optional) the tab title.</summary>
        internal static (SkiaSharp.SKColor Backdrop, string? Title) ParseProfileMarker(string text)
        {
            var lines = text.Replace("\r", "").Split('\n');
            string? title = lines.Length > 1 && lines[1].Trim().Length > 0 ? lines[1].Trim() : null;
            return (ParseBackdrop(lines[0]), title);
        }

        /// <summary>"#RRGGBBAA" or "#RRGGBB" (whitespace allowed); anything else: the default backdrop.</summary>
        internal static SkiaSharp.SKColor ParseBackdrop(string text) =>
            SkiaSharp.SKColor.TryParse(ToSkiaOrder(text.Trim()), out var c) ? c : TerminalProfile.DefaultBackdrop;

        // Skia reads 8 hex digits as AARRGGBB; the file says RRGGBBAA, like CSS.
        private static string ToSkiaOrder(string hex) =>
            hex.Length == 9 && hex[0] == '#' ? "#" + hex.Substring(7, 2) + hex.Substring(1, 6) : hex;

        public const string InstallShellSignalsArg = "--install-shell-signals";
        public const string UninstallShellSignalsArg = "--uninstall-shell-signals";

        /// <summary>
        /// Command-line jobs an exported sketch does instead of running, for
        /// install.ps1 (see PowerShellInstaller): put the PowerShell prompt hook
        /// in (or take it out of) the user's profiles. Returns the process exit
        /// code, or null when there's no such argument and the sketch should run.
        /// </summary>
        public static int? HandleCommandLine(string[] args) =>
            HandleCommandLine(args, ShellSignals.InstallForPowerShell, UninstallShellSignals);

        /// <summary>The testable part: <paramref name="install"/>/<paramref name="uninstall"/> do the work.</summary>
        internal static int? HandleCommandLine(string[] args, Func<IReadOnlyList<string>> install, Action uninstall)
        {
            try
            {
                if (args.Contains(InstallShellSignalsArg, StringComparer.OrdinalIgnoreCase))
                {
                    install();
                    return 0;
                }
                if (args.Contains(UninstallShellSignalsArg, StringComparer.OrdinalIgnoreCase))
                {
                    uninstall();
                    return 0;
                }
            }
            catch (Exception ex)
            {
                DanaLogger.ErrorFromException(ex, "Señales de PowerShell (instalador)");
                return 1;
            }
            return null;
        }

        private static void UninstallShellSignals()
        {
            foreach (var profile in ShellSignals.PowerShellProfiles().Concat(ShellSignals.LegacyProfiles()))
                ShellSignals.Uninstall(profile);
        }

        // Held for the life of the process: the OS releases it when the process ends.
        private static System.Threading.Mutex? _singleInstance;

        /// <summary>
        /// The tray app runs once per exe: autostart plus a double-click (or the
        /// installer starting it again) would otherwise put two overlays on
        /// every terminal. False when another process of this same exe (same
        /// path) already runs.
        /// </summary>
        public static bool TryClaimSingleInstance() => TryClaimSingleInstance(Environment.ProcessPath ?? ExeDirectory, out _singleInstance);

        internal static bool TryClaimSingleInstance(string exePath, out System.Threading.Mutex? mutex)
        {
            mutex = new System.Threading.Mutex(initiallyOwned: true, SingleInstanceName(exePath), out bool createdNew);
            if (createdNew)
                return true;
            mutex.Dispose();
            mutex = null;
            return false;
        }

        /// <summary>A per-user mutex name for one exe path (case-insensitive, like Windows paths).</summary>
        internal static string SingleInstanceName(string exePath)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(exePath).ToUpperInvariant()));
            return @"Local\DanaSketch-" + Convert.ToHexString(bytes, 0, 12);
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

        /// <summary>Compiles the exported sketch.cs, with data/ (copied next to it)
        /// as the sketch folder so relative paths work like in the IDE.</summary>
        public static CompileResult Compile(string sketchFilePath)
        {
            var source = File.ReadAllText(sketchFilePath);
            var baseDir = Path.GetDirectoryName(sketchFilePath) ?? ExeDirectory;

            SketchCompiler.RegisterAssemblyDirectory(baseDir);
            var extraReferences = SketchCompiler.CollectExternalReferences(baseDir);
            var result = SketchCompiler.Compile(source, extraReferences);
            if (result.Success)
                result.Sketch!.SetSketchFolder(baseDir);
            return result;
        }

        /// <summary>
        /// True when the export runs as a tray app instead of a window: an
        /// AttachedSketch draws on another app's window, so it has no window of
        /// its own -- only a notification-area icon (see AttachedSketchTray).
        /// </summary>
        public static bool RunsInTray(CompileResult result) =>
            result.Success && result.Sketch is AttachedSketch;

        /// <summary>Builds the standalone window that shows the sketch — no editor,
        /// no title bar chrome, just AvaloniaSketchWindow. If compilation failed
        /// (a corrupted export, or a bundled dependency that didn't survive being
        /// copied), shows a minimal error window instead of silently exiting, so
        /// double-clicking a broken export at least explains why.</summary>
        public static Window BuildWindow(CompileResult result)
        {
            if (result.Success)
                return new AvaloniaSketchWindow(result.Sketch!, result.Sketch!.GetType().Name, windowless: IsWindowless);

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
