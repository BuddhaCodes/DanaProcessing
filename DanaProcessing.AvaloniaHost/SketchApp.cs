using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using DanaProcessing.AvaloniaHost.Attach;
using DanaProcessing;

namespace DanaProcessing.AvaloniaHost
{
    /// <summary>
    /// Runs a sketch in its own window from any .NET program -- a console
    /// app, a test bench, a tool -- the same way the DanaProcessing IDE does:
    /// <code>
    /// SketchApp.Run(new MySketch());
    /// </code>
    /// The call returns when the window is closed. Inside an Avalonia app
    /// that's already running, it just opens another window (and returns
    /// at once); to put a sketch inside your own window instead, use
    /// <see cref="AvaloniaSketchCanvas"/>. An <see cref="AttachedSketch"/>
    /// (one that draws on another app's window) gets no window: it runs from
    /// an icon in the notification area, like an IDE export.
    /// </summary>
    public static class SketchApp
    {
        /// <summary>Opens <paramref name="sketch"/> in a window and waits until it's closed.</summary>
        /// <param name="title">The window title (default: the sketch's class name).</param>
        /// <param name="args">The program's command-line arguments, passed on to Avalonia.</param>
        public static void Run(Sketch sketch, string? title = null, string[]? args = null)
        {
            ArgumentNullException.ThrowIfNull(sketch);
            title ??= sketch.GetType().Name;
            if (sketch.SketchFolder is null)
                sketch.SetSketchFolder(SketchFolderFor(Directory.GetCurrentDirectory(), AppContext.BaseDirectory));

            // Already inside an Avalonia app (embedding): just another window.
            if (Application.Current is not null)
            {
                if (sketch is AttachedSketch attached)
                {
                    AttachedSketchTray? tray = null;
                    tray = new AttachedSketchTray(attached, title, () => tray?.Dispose());
                }
                else
                    new AvaloniaSketchWindow(sketch, title).Show();
                return;
            }

            // Windows wants the UI on a single-threaded apartment (clipboard,
            // drag and drop, file dialogs). Top-level programs don't start on
            // one, so the window gets its own STA thread there.
            if (OperatingSystem.IsWindows() && Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Exception? failure = null;
                var ui = new Thread(() =>
                {
                    try { StartApp(sketch, title, args); }
                    catch (Exception e) { failure = e; }
                });
                ui.SetApartmentState(ApartmentState.STA);
                ui.Start();
                ui.Join();
                if (failure is not null)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
                return;
            }
            StartApp(sketch, title, args);
        }

        /// <summary>Opens a new <typeparamref name="T"/> in a window and waits until it's closed.</summary>
        public static void Run<T>(string? title = null, string[]? args = null) where T : Sketch, new() =>
            Run(new T(), title, args);

        /// <summary>
        /// Runs the program's sketch: the one non-abstract <see cref="Sketch"/>
        /// subclass in the program (the entry assembly). That's what makes a
        /// file copied out of the DanaProcessing IDE run as it is, whatever its
        /// class is called. Several sketches in the program: pass the one to
        /// run with <c>SketchApp.Run(new MySketch())</c>.
        /// </summary>
        public static void Run(string[]? args = null)
        {
            var assembly = Assembly.GetEntryAssembly()
                ?? throw new InvalidOperationException("SketchApp.Run(): there's no entry assembly to look for a sketch in.");
            Run(Create(FindSketchType(assembly)), args: args);
        }

        /// <summary>The one non-abstract <see cref="Sketch"/> subclass in <paramref name="assembly"/>.</summary>
        public static Type FindSketchType(Assembly assembly)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t is not null).ToArray()!; }
            var sketches = types.Where(t => typeof(Sketch).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null).ToList();
            return sketches.Count switch
            {
                1 => sketches[0],
                0 => throw new InvalidOperationException(
                    $"SketchApp.Run(): no sketch in {assembly.GetName().Name}. Add a class that inherits from Sketch (with Setup() and Draw())."),
                _ => throw new InvalidOperationException(
                    $"SketchApp.Run(): {sketches.Count} sketches in {assembly.GetName().Name} ({string.Join(", ", sketches.Select(t => t.Name))}). " +
                    "Say which one: SketchApp.Run(new MySketch())."),
            };
        }

        /// <summary>
        /// The sketch folder (where data/ is looked for, and where SaveFrame
        /// and friends write): the folder the program runs in -- the project
        /// folder with <c>dotnet run</c> -- unless only the program's own
        /// folder has a data/ folder, as in a published app started from
        /// somewhere else.
        /// </summary>
        internal static string SketchFolderFor(string workingDirectory, string programDirectory) =>
            !Directory.Exists(Path.Combine(workingDirectory, "data")) && Directory.Exists(Path.Combine(programDirectory, "data"))
                ? programDirectory
                : workingDirectory;

        private static Sketch Create(Type type) => (Sketch)Activator.CreateInstance(type)!;

        // ------------------------------------------------------------------

        private static void StartApp(Sketch sketch, string title, string[]? args)
        {
            SketchApplication.Pending = (sketch, title);
            AppBuilder.Configure<SketchApplication>()
                .UsePlatformDetect()
                .StartWithClassicDesktopLifetime(args ?? Array.Empty<string>(), ShutdownMode.OnMainWindowClose);
        }

        /// <summary>The small Avalonia app behind <see cref="SketchApp"/>: a theme and one window.</summary>
        private sealed class SketchApplication : Application
        {
            internal static (Sketch Sketch, string Title)? Pending;

            public override void Initialize() => Styles.Add(new FluentTheme());

            public override void OnFrameworkInitializationCompleted()
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && Pending is { } run)
                {
                    if (run.Sketch is AttachedSketch attached)
                    {
                        // It draws on another app's window (a terminal): no
                        // window of its own, an icon in the notification area
                        // until its menu's Quit -- same as an IDE export.
                        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                        var tray = new AttachedSketchTray(attached, run.Title, () => desktop.Shutdown());
                        desktop.Exit += (_, _) => tray.Dispose();
                    }
                    else
                        desktop.MainWindow = new AvaloniaSketchWindow(run.Sketch, run.Title);
                }
                base.OnFrameworkInitializationCompleted();
            }
        }
    }
}
